using System;
using HarmonyLib;
using UnityEngine;

namespace ShopExpansion
{
    // 방 배경의 수조 자리에 아이템을 끌어다 놓으면 = 대형 수조 기계(숨김 인벤) 위에 놓은 것으로 게임이 처리하게 한다.
    // 게임 ItemMouseDragHandler 는 드래그 중 마우스 아래 아이템을 lastItem 에 담아 두고, 놓을 때(EndDrag) 그 아이템에
    //   넣기(칸에 들어갈 수 있으면) / 겹치기 / 대상 지정(전자 부품 → 효율 업그레이드 등)을 한다.
    // → 놓는 순간 게임이 아무것도 못 찾았고 마우스가 수조 자리 위(다른 창 위가 아님)면 lastItem 을 대형 수조로 바꿔 준다.
    [HarmonyPatch(typeof(ItemMouseDragHandler), "EndDrag")]
    internal static class TankDropPatch
    {
        private static void Prefix(ItemMouseDragHandler __instance, bool cancelled)
        {
            if (cancelled || !Rooms.InRoom) return;
            try
            {
                var t = Traverse.Create(__instance);
                if (t.Field("lastItem").GetValue() != null || t.Field("lastInventory").GetValue() != null) return;
                Vector2 mouse = Input.mousePosition;
                if (!RoomPanel.IsOverTankSpot(mouse) || OverAnyWindow(mouse)) return;
                GameItem tank = AquaTank.Get(Rooms.Current);
                if (tank == null) return;
                t.Field("lastItem").SetValue(tank);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ShopExpansion] tank drop: " + ex.Message);
            }
        }

        // 수조 자리 위에 열린 창(수조 창 등)이 있으면 그 창에 놓은 것 → 우리가 가로채지 않는다
        private static bool OverAnyWindow(Vector2 mouse)
        {
            if (WindowsHandler.current?.visibleWindows == null) return false;
            foreach (PixelWindow w in WindowsHandler.current.visibleWindows)
            {
                try
                {
                    if (w?.handler == null || !w.handler.gameObject.activeInHierarchy) continue;
                    if (RectTransformUtility.RectangleContainsScreenPoint(w.rectTransform, mouse, null)) return true;
                }
                catch { }
            }
            return false;
        }
    }
}
