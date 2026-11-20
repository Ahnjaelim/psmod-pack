using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

using Preset = MachineStorageCoordinator.Preset;

namespace ComboKeyPresets
{
    // 보관 조정기(Storage Coordinator) 프리셋 단축키에 Ctrl / Shift / Alt 조합을 허용하는 모드.
    // 프리셋의 int keyCode 상위 비트에 조합키 플래그를 저장한다 (기본 KeyCode 값은 모두 0x1FF 이하).
    // Merged into the Personal QoL Tweaks DLL: the loader only instantiates one IMod per DLL,
    // so PenLabelerAssistMod calls these from its own OnEnable / OnDisable.
    internal static class ComboKeyPresetsModule
    {
        public static void OnEnable()
        {
            // 옵션 레지스트리가 이미 만들어진 뒤에 모드가 켜진 경우에도 항목을 추가
            if (GameOptionRegistry.FindSection(GameOptionRegistry.SECTION_KEYBIND) != null)
                ModHotkeys.RegisterAll();
            Debug.Log("[ComboKeyPresets] enabled");
        }

        public static void OnDisable()
        {
            ModHotkeys.RemoveAll();
        }
    }

    internal static class Combo
    {
        public const int CTRL = 1 << 12;
        public const int SHIFT = 1 << 13;
        public const int ALT = 1 << 14;
        public const int MOD_MASK = CTRL | SHIFT | ALT;
        public const int BASE_MASK = 0xFFF;

        public static bool IsModifier(KeyCode k)
        {
            return k == KeyCode.LeftControl || k == KeyCode.RightControl
                || k == KeyCode.LeftShift || k == KeyCode.RightShift
                || k == KeyCode.LeftAlt || k == KeyCode.RightAlt
                || k == KeyCode.AltGr;
        }

        public static int HeldMods()
        {
            int m = 0;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) m |= CTRL;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) m |= SHIFT;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr)) m |= ALT;
            return m;
        }

        public static string ModPrefix(int code)
        {
            string s = "";
            if ((code & CTRL) != 0) s += "Ctrl+";
            if ((code & SHIFT) != 0) s += "Shift+";
            if ((code & ALT) != 0) s += "Alt+";
            return s;
        }

        private static KeyCode[] nonModifierKeys;

        public static bool AnyNonModifierKeyDown()
        {
            if (nonModifierKeys == null)
            {
                nonModifierKeys = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>()
                    .Where(k => k != KeyCode.None && k < KeyCode.Mouse0 && !IsModifier(k))
                    .Distinct().ToArray();
            }
            foreach (KeyCode k in nonModifierKeys)
            {
                if (Input.GetKeyDown(k)) return true;
            }
            return false;
        }

        // 이번 프레임에 조합키 프리셋이 쓰는 기본 키 목록 (게임 기본 단축키 충돌 방지용)
        private static int cacheFrame = -1;
        private static readonly HashSet<KeyCode> comboBaseKeys = new HashSet<KeyCode>();

        public static bool IsComboBaseKey(KeyCode key)
        {
            if (cacheFrame != Time.frameCount)
            {
                cacheFrame = Time.frameCount;
                comboBaseKeys.Clear();
                try
                {
                    GameItem coord = MachineStorageCoordinator.FindCoordinatorInReach();
                    if (coord != null)
                    {
                        foreach (Preset p in MachineStorageCoordinator.LoadPresets(coord))
                        {
                            if ((p.keyCode & MOD_MASK) != 0)
                                comboBaseKeys.Add((KeyCode)(p.keyCode & BASE_MASK));
                        }
                    }
                }
                catch (Exception) { }
            }
            return comboBaseKeys.Contains(key);
        }
    }

    // 게임의 private 메서드 호출용 (Harmony 역패치: 리플렉션 API를 쓰지 않음)
    [HarmonyPatch]
    internal static class CoordinatorCalls
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(MachineStorageCoordinator), "SaveOpenPresets")]
        public static void SaveOpenPresets() { throw new NotImplementedException("reverse patch stub"); }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(MachineStorageCoordinator), "RebuildList")]
        public static void RebuildList() { throw new NotImplementedException("reverse patch stub"); }
    }

    // ── 1. 키 입력 캡처: 조합키 단독 입력은 무시하고, 일반 키가 눌릴 때 함께 눌린 조합키를 기록 ──
    [HarmonyPatch(typeof(MachineStorageCoordinator), "HandleKeyCapture")]
    internal static class Patch_HandleKeyCapture
    {
        internal struct State
        {
            public Preset preset;
            public int mods;
            public bool cancelling;
        }

        private static bool Prefix(Preset ___capturingPreset, out State __state)
        {
            __state = new State
            {
                preset = ___capturingPreset,
                mods = Combo.HeldMods(),
                cancelling = Input.GetKeyDown(KeyCode.Escape) || KeyBindHelper.GetKeyDown("secondary_click"),
            };
            if (!Input.anyKeyDown || __state.cancelling) return true;

            // Ctrl/Shift/Alt만 눌렸으면 원본 로직을 건너뛰고 다음 키를 기다린다
            if (!Combo.AnyNonModifierKeyDown()) return false;
            return true;
        }

        private static void Postfix(Preset ___capturingPreset, State __state)
        {
            // 캡처가 이번 프레임에 '수락'으로 끝났을 때만 조합키 플래그를 덧붙인다
            if (__state.preset == null || ___capturingPreset != null || __state.cancelling) return;
            if (__state.mods == 0) return;

            __state.preset.keyCode = (__state.preset.keyCode & Combo.BASE_MASK) | __state.mods;
            CoordinatorCalls.SaveOpenPresets();
            CoordinatorCalls.RebuildList();
        }
    }

    // ── 2. 단축키 실행: 조합키까지 정확히 일치할 때만 프리셋 토글 ──
    [HarmonyPatch(typeof(MachineStorageCoordinator), "HandleHotkeys")]
    internal static class Patch_HandleHotkeys
    {
        private static bool Prefix()
        {
            if (!Input.anyKeyDown || (DialogUIManager.Instance != null && DialogUIManager.Instance.IsChoice()))
                return false;

            GameItem coord = MachineStorageCoordinator.FindCoordinatorInReach();
            if (coord == null) return false;

            int held = Combo.HeldMods();
            var groups = MachineStorageCoordinator.LoadPresets(coord)
                .Where(p => p.keyCode != 0
                         && (p.keyCode & Combo.MOD_MASK) == held
                         && Input.GetKeyDown((KeyCode)(p.keyCode & Combo.BASE_MASK)))
                .GroupBy(p => p.keyCode)
                .ToList();

            foreach (var g in groups)
                MachineStorageCoordinator.TogglePresets(g.ToList());

            return false;
        }
    }

    // ── 3. 키 이름 표시: "Ctrl+Shift+1" 형식 ──
    [HarmonyPatch(typeof(MachineStorageCoordinator), "KeyName")]
    internal static class Patch_KeyName
    {
        private static void Prefix(ref int keyCode, out int __state)
        {
            __state = keyCode & Combo.MOD_MASK;
            keyCode &= Combo.BASE_MASK;
        }

        private static void Postfix(ref string __result, int __state)
        {
            if (__state != 0) __result = Combo.ModPrefix(__state) + __result;
        }
    }

    // ════════════ 공구함 슬롯 키 설정 (게임 옵션 > 키 설정) ════════════

    internal static class Toolbox
    {
        public const string BINDING_PREFIX = "toolbox_slot_";
        public const string OPTION_PREFIX = "key_toolbox_slot_";

        public static bool IsToolboxBinding(string id)
        {
            return id != null && id.StartsWith(BINDING_PREFIX, StringComparison.Ordinal);
        }

        private static KeyCode[] captureKeys;

        // 옵션 화면 캡처 대상 키: 키보드 + 마우스 버튼(좌클릭 제외), 조합키 제외
        public static KeyCode[] CaptureKeys()
        {
            if (captureKeys == null)
            {
                captureKeys = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>()
                    .Where(k => k != KeyCode.None && !Combo.IsModifier(k)
                             && (k < KeyCode.Mouse0 || (k > KeyCode.Mouse0 && k <= KeyCode.Mouse6)))
                    .Distinct().ToArray();
            }
            return captureKeys;
        }
    }

    // ── 5. 옵션 화면에서 공구함 슬롯 키 입력: 조합키를 누른 채 키를 누르면 조합으로 저장 ──
    [HarmonyPatch(typeof(KeyBindHelper), nameof(KeyBindHelper.PollCapture))]
    internal static class Patch_KeyBindPollCapture
    {
        private static bool Prefix(out KeyCode key, ref KeyBindHelper.CaptureResult __result)
        {
            key = KeyCode.None;
            string optionId = KeyBindHelper.CapturingOptionId;
            if (optionId == null || !(optionId.StartsWith(Toolbox.OPTION_PREFIX, StringComparison.Ordinal) || ModHotkeys.IsModOption(optionId)))
                return true; // 다른 키 설정은 원래 동작

            __result = KeyBindHelper.CaptureResult.None;
            if (!Input.anyKeyDown) return false;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                __result = KeyBindHelper.CaptureResult.Cancelled;
                return false;
            }
            foreach (KeyCode k in Toolbox.CaptureKeys())
            {
                if (Input.GetKeyDown(k))
                {
                    key = (KeyCode)((int)k | Combo.HeldMods());
                    __result = KeyBindHelper.CaptureResult.Key;
                    return false;
                }
            }
            return false; // 조합키만 눌림 → 다음 키 대기
        }
    }

    // ── 6. 공구함 슬롯 단축키 판정: 조합키까지 정확히 일치해야 동작 ──
    [HarmonyPatch(typeof(KeyBindHelper), nameof(KeyBindHelper.GetKeyDown))]
    internal static class Patch_KeyBindGetKeyDown_Toolbox
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(string id, ref bool __result)
        {
            if (!Toolbox.IsToolboxBinding(id)) return true;
            __result = false;
            if (KeyBindHelper.SuppressHotkeys) return false;
            int code = (int)KeyBindHelper.Get(id);
            int baseKey = code & Combo.BASE_MASK;
            if (baseKey == 0) return false;
            __result = (code & Combo.MOD_MASK) == Combo.HeldMods() && Input.GetKeyDown((KeyCode)baseKey);
            return false;
        }
    }

    [HarmonyPatch(typeof(KeyBindHelper), nameof(KeyBindHelper.GetKey))]
    internal static class Patch_KeyBindGetKey_Toolbox
    {
        private static bool Prefix(string id, ref bool __result)
        {
            if (!Toolbox.IsToolboxBinding(id)) return true;
            __result = false;
            if (KeyBindHelper.SuppressHotkeys) return false;
            int code = (int)KeyBindHelper.Get(id);
            int baseKey = code & Combo.BASE_MASK;
            if (baseKey == 0) return false;
            __result = (code & Combo.MOD_MASK) == Combo.HeldMods() && Input.GetKey((KeyCode)baseKey);
            return false;
        }
    }

    // ── 7. 키 설정 화면 / 공구함 라벨의 키 이름: "Alt+1" 형식 ──
    [HarmonyPatch(typeof(KeyBindHelper), nameof(KeyBindHelper.KeyName))]
    internal static class Patch_KeyBindKeyName
    {
        private static void Prefix(ref KeyCode key, out int __state)
        {
            __state = (int)key & Combo.MOD_MASK;
            key = (KeyCode)((int)key & Combo.BASE_MASK);
        }

        private static void Postfix(ref string __result, int __state)
        {
            if (__state != 0) __result = Combo.ModPrefix(__state) + __result;
        }
    }

    // ════════════ 돋보기 단축키 (게임 옵션 > 키 설정 맨 아래) ════════════

    // 모드가 추가하는 단축키 하나 (옵션 > 키 설정에 표시, PlayerPrefs에 저장, 조합키 지원)
    internal class ModHotkey
    {
        public string OptionId;
        public string PrefsKey;
        public int DefaultCode;
        public Func<string> Label;
        public Func<string> Tooltip;
        public Action OnPress;

        public int Code
        {
            get { return PlayerPrefs.GetInt(PrefsKey, DefaultCode); }
            set { PlayerPrefs.SetInt(PrefsKey, value); PlayerPrefs.Save(); }
        }

        public void RegisterOption()
        {
            GameOptionRegistry.Add(GameOptionRegistry.SECTION_KEYBIND, GameOptionRegistry.KeyBind(
                OptionId, Label, () => (KeyCode)Code, v => Code = (int)v, Tooltip));
        }

        public bool PressedThisFrame()
        {
            int code = Code;
            int baseKey = code & Combo.BASE_MASK;
            if (baseKey == 0 || KeyBindHelper.SuppressHotkeys) return false;
            return (code & Combo.MOD_MASK) == Combo.HeldMods() && Input.GetKeyDown((KeyCode)baseKey);
        }
    }

    internal static class ModHotkeys
    {
        public static readonly List<ModHotkey> All = new List<ModHotkey>
        {
            new ModHotkey
            {
                OptionId = "key_magnifier",
                PrefsKey = "ComboKeyPresets.magnifier_key",
                DefaultCode = 0,
                Label = () => LocHelper.GetLocalizedItem("item_magnifier_name"),
                Tooltip = () => "누르면 손이 닿는 곳(열린 창, 공구함 포함)의 돋보기를 들고, 다시 누르면 내려놓습니다.",
                OnPress = Magnifier.Toggle,
            },
            new ModHotkey
            {
                OptionId = "key_close_all_windows",
                PrefsKey = "ComboKeyPresets.close_all_windows_key",
                DefaultCode = (int)KeyCode.W | Combo.CTRL | Combo.SHIFT,
                Label = () => "모든 창 닫기",
                Tooltip = () => "ESC로 하나씩 닫히는 아이템/기계 창을 한 번에 모두 닫습니다.",
                OnPress = WindowCloser.CloseAll,
            },
        };

        public static bool IsModOption(string optionId)
        {
            return All.Exists(h => h.OptionId == optionId);
        }

        public static void RegisterAll()
        {
            foreach (ModHotkey h in All) h.RegisterOption();
        }

        public static void RemoveAll()
        {
            foreach (ModHotkey h in All) GameOptionRegistry.Remove(h.OptionId);
        }

        public static void HandleInput()
        {
            if (DialogUIManager.Instance != null && DialogUIManager.Instance.IsChoice()) return;
            foreach (ModHotkey h in All)
            {
                if (h.PressedThisFrame()) h.OnPress();
            }
        }
    }

    internal static class WindowCloser
    {
        // ESC가 하나씩 닫는 대상(움직일 수 있는 PixelWindow)을 전부 같은 방식으로 닫는다
        public static void CloseAll()
        {
            if (WindowsHandler.current == null) return;
            List<PixelWindow> targets = WindowsHandler.current.visibleWindows.Where(w => w != null && w.canMove).ToList();
            foreach (PixelWindow w in targets)
                WindowsHandler.CloseWindow(w);
        }
    }

    internal static class Magnifier
    {
        private static bool IsMagnifier(GameItem i)
        {
            return i != null && (i.identifier == "magnifier"
                || (i.IsTag("MAGNIFYING_GLASS") && i.IsTag("CAN_USE_ON_NON_OWNED_TAG")));
        }

        private static GameItem FindIn(GraphNodeStorage root)
        {
            if (root == null) return null;
            List<GameItem> list = GraphUtils.FindAllChildrenType(root, (GameItem i) => IsMagnifier(i) && GeneralHelper.IsItemOwned(i));
            return list.Count > 0 ? list[0] : null;
        }

        private static GameItem FindInReach()
        {
            if (MapUIManager.Instance != null && MapUIManager.Instance.isOutside)
                return FindIn(EmporiumEntry.Instance.afterhourWindow);
            foreach (PixelWindow w in WindowsHandler.current.visibleWindows)
            {
                GameItem found = FindIn(w);
                if (found != null) return found;
            }
            return null;
        }

        public static void Toggle()
        {
            ItemSelectHandler sel = ItemSelectHandler.current;
            if (sel == null) return;
            if (IsMagnifier(sel.GetEquippedItem()))
            {
                OneShotItem = null;
                sel.EndSelect();
                return;
            }
            GameItem glass = FindInReach();
            if (glass == null) return;
            sel.TryEquipItem(glass);
            OneShotItem = sel.GetEquippedItem() == glass ? glass : null;
        }

        // 단축키로 집은 돋보기: 한 번 사용하면 내려놓는다 (원래 클릭으로 집었을 때와 같게)
        public static GameItem OneShotItem;
    }

    [HarmonyPatch(typeof(ItemSelectHandler), "ContinueEquipAfterUse")]
    internal static class Patch_MagnifierOneShot
    {
        private static bool Prefix(ItemSelectHandler __instance, GameItem ___currentItem)
        {
            GameItem oneShot = Magnifier.OneShotItem;
            Magnifier.OneShotItem = null;
            if (oneShot == null || oneShot != ___currentItem) return true; // 클릭으로 집은 도구 등은 원래대로 유지
            __instance.EndSelect();
            return false;
        }
    }

    // 공구함 단축키로 집은 도구도 한 번 쓰면 내려놓는다 (돋보기 단축키와 같은 규칙).
    [HarmonyPatch(typeof(ToolboxHelper), nameof(ToolboxHelper.TryToggleEquipSlot))]
    internal static class Patch_ToolboxOneShot
    {
        private static void Prefix(out GameItem __state)
        {
            __state = ItemSelectHandler.current != null ? ItemSelectHandler.current.GetEquippedItem() : null;
        }

        private static void Postfix(GameItem __state)
        {
            ItemSelectHandler sel = ItemSelectHandler.current;
            if (sel == null) return;
            GameItem now = sel.GetEquippedItem();
            if (now != null && now != __state) Magnifier.OneShotItem = now;      // 새로 집음 → 1회용
            else if (now == null) Magnifier.OneShotItem = null;                  // 단축키로 내려놓음
        }
    }

    // 옵션 키 설정 섹션이 만들어질 때 모드 단축키 항목 추가
    [HarmonyPatch(typeof(GameOptionRegistry), nameof(GameOptionRegistry.EnsureVanilla))]
    internal static class Patch_OptionRegistry
    {
        private static void Postfix() { ModHotkeys.RegisterAll(); }
    }

    // 공구함 단축키 처리 직후 (타이핑 중이 아닐 때만 호출됨) 모드 단축키 처리
    [HarmonyPatch(typeof(ToolboxHelper), nameof(ToolboxHelper.HandleHotkeys))]
    internal static class Patch_ModHotkeys
    {
        private static void Postfix() { ModHotkeys.HandleInput(); }
    }

    // ── 4. 조합키 프리셋이 쓰는 키는 조합키를 누른 상태에서 게임 기본 단축키로 동작하지 않게 ──
    //      (예: Ctrl+1 프리셋을 누를 때 툴박스 1번 슬롯이 같이 선택되는 것 방지)
    [HarmonyPatch(typeof(KeyBindHelper), nameof(KeyBindHelper.GetKeyDown))]
    internal static class Patch_KeyBindGetKeyDown
    {
        private static void Postfix(string id, ref bool __result)
        {
            if (!__result || Combo.HeldMods() == 0) return;
            KeyCode key = KeyBindHelper.Get(id);
            if (Combo.IsModifier(key)) return;
            if (Combo.IsComboBaseKey(key)) __result = false;
        }
    }
}
