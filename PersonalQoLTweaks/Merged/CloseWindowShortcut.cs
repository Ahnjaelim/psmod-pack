using System;
using HarmonyLib;
using UnityEngine;

namespace CloseWindowShortcut
{
    // Ctrl + W (키는 설정에서 바꿀 수 있음, Ctrl 은 고정): Esc 와 똑같이 창 닫기.
    // 게임의 Esc 처리(StoreUIManager.ResolveEscape)를 그대로 부른다: 설정 창, 하루 보고서, 외출 지도, 선택/끌기 취소,
    // 그다음 가장 최근에 만진 창이나 패널(아이템 창, 호퍼 설정, 모니터 등)을 하나 닫는다.
    // 다만 닫을 게 없을 때 Esc 는 일시정지 메뉴를 여는데, Ctrl + W 는 열지 않는다.
    [HarmonyPatch(typeof(MapUIManager), "Update")]
    internal static class CloseWindowKeyPatch
    {
        internal static bool suppressEscapeMenu;

        private static void Postfix()
        {
            try
            {
                if (!(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) return;
                if (!PenLabelerAssist.QoLKeyBindings.IsBoundKeyDown(PenLabelerAssist.QoLKeyBindings.CloseWindow)) return;
                StoreUIManager ui = StoreUIManager.Instance;
                if (ui == null || ui.IsTyping()) return;
                suppressEscapeMenu = true;
                try { ui.ResolveEscape(); }
                finally { suppressEscapeMenu = false; }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CloseWindowShortcut] " + ex.Message);
            }
        }
    }

    // Ctrl + W 로 부른 Esc 처리에서는 일시정지 메뉴를 열지 않는다
    [HarmonyPatch(typeof(EscapeUIManager), nameof(EscapeUIManager.OpenEscapeMenu))]
    internal static class NoEscapeMenuPatch
    {
        private static bool Prefix() => !CloseWindowKeyPatch.suppressEscapeMenu;
    }
}
