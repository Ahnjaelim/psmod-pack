using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace KeybindClear
{
    // 옵션 > 키 설정: 각 단축키 버튼 오른쪽에 [x] 버튼을 붙여서 단축키를 "없음"(KeyCode.None)으로 비운다.
    // 게임 옵션 창은 변경을 pendingKeys 에 모았다가 적용할 때 entry.setKey 로 저장하므로,
    // [x] 도 pendingKeys 만 바꾼다 → 적용/취소 흐름이 게임 기본 동작과 같다 (바닐라·QoL·모드 단축키 모두).
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.

    [HarmonyPatch(typeof(GameOptionUIManager), "BuildEntry")]
    internal static class BuildEntryPatch
    {
        [System.ThreadStatic] internal static GameOptionRegistry.Entry Current;
        [System.ThreadStatic] internal static GameOptionUIManager Manager;

        private static void Prefix(GameOptionUIManager __instance, GameOptionRegistry.Entry entry)
        {
            Current = entry != null && entry.kind == GameOptionRegistry.Kind.KeyBind ? entry : null;
            Manager = __instance;
        }

        // 게임은 키 버튼을 만든 직후 builder.Last.SetFixedWidth(160) 을 부르는데, 그 사이에 [x] 를 끼워 넣어서
        // Last 가 [x] 가 된다. 줄이 다 만들어진 뒤 두 버튼 폭을 다시 맞춘다 (키 160, [x] 작게).
        [System.ThreadStatic] internal static CustomUIElement KeyButton;
        [System.ThreadStatic] internal static CustomUIElement ClearButton;

        private static void Postfix()
        {
            try
            {
                if (KeyButton != null) KeyButton.SetFixedWidth(160f);
                if (ClearButton != null)
                {
                    // [x] 는 정사각형: 폭 = 키 버튼 높이. 레이아웃이 끝난 다음 프레임에 실제 높이로 다시 맞춘다.
                    CustomUIElement key = KeyButton, clear = ClearButton;
                    float guess = key != null && key.root != null
                        ? LayoutUtility.GetPreferredHeight((RectTransform)key.root.transform) : 0f;
                    clear.SetFixedWidth(guess > 0f ? guess : ClearWidth);
                    LetterboxManager.RunNextUpdate(() =>
                    {
                        try
                        {
                            if (key == null || clear == null || key.root == null || clear.root == null) return;
                            float h = ((RectTransform)key.root.transform).rect.height;
                            if (h > 0f) clear.SetFixedWidth(h);
                        }
                        catch
                        {
                        }
                    });
                }
            }
            catch
            {
            }
            Current = null;
            Manager = null;
            KeyButton = null;
            ClearButton = null;
        }

        internal const float ClearWidth = 26f;
    }

    // 키 버튼(AddButton 의 id == entry.id)이 만들어진 직후, 같은 줄에 [x] 버튼을 추가한다.
    [HarmonyPatch(typeof(CustomUIBuilder), nameof(CustomUIBuilder.AddButton))]
    internal static class AddButtonPatch
    {
        private const string Prefix_ = "psmp_clear_";

        private static void Postfix(CustomUIBuilder __instance, string id)
        {
            GameOptionRegistry.Entry entry = BuildEntryPatch.Current;
            GameOptionUIManager mgr = BuildEntryPatch.Manager;
            if (entry == null || mgr == null || id == null || id != entry.id) return;
            BuildEntryPatch.Current = null; // 우리 AddButton 호출로 다시 들어오지 않게
            try
            {
                string optionId = entry.id;
                BuildEntryPatch.KeyButton = __instance.Last;
                __instance.AddButton("x", () => Clear(mgr, optionId), Prefix_ + optionId);
                BuildEntryPatch.ClearButton = __instance.Last;
                __instance.Last.SetFixedWidth(BuildEntryPatch.ClearWidth);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[KeybindClear] add failed: " + ex.Message);
            }
        }

        private static void Clear(GameOptionUIManager mgr, string optionId)
        {
            try
            {
                if (KeyBindHelper.IsCapturing)
                {
                    string capturing = KeyBindHelper.CapturingOptionId;
                    KeyBindHelper.StopCapture();
                    if (capturing != null && capturing != optionId) Stubs.RefreshKeyBindButton(mgr, capturing);
                }
                var pending = Traverse.Create(mgr).Field("pendingKeys").GetValue() as Dictionary<string, KeyCode>;
                if (pending == null) return;
                pending[optionId] = KeyCode.None;
                Stubs.RefreshKeyBindButton(mgr, optionId);
                Stubs.RefreshRules(mgr);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[KeybindClear] clear failed: " + ex.Message);
            }
        }
    }

    [HarmonyPatch]
    internal static class Stubs
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(GameOptionUIManager), "RefreshKeyBindButton")]
        public static void RefreshKeyBindButton(GameOptionUIManager instance, string optionId)
        {
            throw new System.NotImplementedException("stub");
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(GameOptionUIManager), "RefreshRules")]
        public static void RefreshRules(GameOptionUIManager instance)
        {
            throw new System.NotImplementedException("stub");
        }
    }
}
