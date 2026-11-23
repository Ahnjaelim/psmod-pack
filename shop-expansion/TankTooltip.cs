using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShopExpansion
{
    // 방 배경의 수조 자리에 마우스를 올리면 대형 수조(숨김 인벤의 진짜 기계)의 게임 툴팁을 띄운다.
    // 게임 ItemTooltipHandler 는 매 프레임 마우스 아래 아이템(TooltipListener)을 찾아 툴팁을 띄우고, 없으면 숨긴다.
    // → 그 뒤(Postfix)에 게임이 아무것도 못 찾았고 수조 자리 위면 우리 수조 툴팁을 띄운다 (Shift 자세히 보기도 같음).
    internal class TankHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal static bool Over;

        public void OnPointerEnter(PointerEventData e) => Over = true;
        public void OnPointerExit(PointerEventData e) => Over = false;
        private void OnDisable() => Over = false;
    }

    [HarmonyPatch(typeof(ItemTooltipHandler), "OnEventUpdate")]
    internal static class TankTooltipPatch
    {
        private const bool ShowTankTooltip = false; // 수조 자리 빈 곳 = 대형 수조 기계 툴팁 (사용자 요청으로 잠시 끔, 0.8.1)

        private static void Postfix(ItemTooltipHandler __instance)
        {
            if (!TankHover.Over || !Rooms.InRoom || UIDragHandler.isDraggingAnyUI) return;
            try
            {
                if (Traverse.Create(__instance).Field("currentTooltipListener").GetValue() != null) return; // 게임이 다른 아이템 툴팁을 띄움
                bool advanced = Traverse.Create(__instance).Field("advanced").GetValue() is bool b && b;
                // 헤엄치는 물고기 위면 그 물고기 아이템의 게임 툴팁
                if (FishSwim.FishAt(Input.mousePosition) is TooltipListener fish && fish.HasTooltip())
                {
                    OverlayHandler.current.ShowTooltip(Input.mousePosition, __instance.tooltipTranslation,
                        advanced ? fish.GetTooltipAdvanced() : fish.GetTooltip(), 60);
                    return;
                }
                // 수조 기계 툴팁은 잠시 끔 (ShowTankTooltip = true 로 되살림)
                if (!ShowTankTooltip) return;
                if (!(AquaTank.Get(Rooms.Current) is TooltipListener tank) || !tank.HasTooltip()) return;
                OverlayHandler.current.ShowTooltip(Input.mousePosition, __instance.tooltipTranslation,
                    advanced ? tank.GetTooltipAdvanced() : tank.GetTooltip(), 72);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ShopExpansion] tank tooltip: " + ex.Message);
                TankHover.Over = false;
            }
        }
    }
}
