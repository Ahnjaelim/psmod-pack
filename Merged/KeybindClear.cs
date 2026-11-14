using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

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

        private static void Postfix()
        {
            Current = null;
            Manager = null;
        }
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
                __instance.AddButton("x", () => Clear(mgr, optionId), Prefix_ + optionId);
                __instance.Last.SetFixedWidth(32f);
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
