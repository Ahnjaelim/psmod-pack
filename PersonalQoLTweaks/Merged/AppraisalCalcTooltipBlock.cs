using System;
using HarmonyLib;
using UnityEngine;

namespace AppraisalPresets
{
    // 게임 툴팁은 마우스 아래를 레이캐스트할 때 아이템이 아닌 UI 는 건너뛰므로,
    // 감정 계산기 상자 위에 마우스를 올리면 상자 뒤에 있는 아이템/물건의 툴팁이 뜬다.
    // 마우스가 상자 위에 있는 동안에는 툴팁을 숨기고 원본 처리를 건너뛴다.
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.
    [HarmonyPatch(typeof(ItemTooltipHandler), "OnEventUpdate")]
    internal static class CalcTooltipBlockPatch
    {
        private static bool Prefix(ref bool __result)
        {
            try
            {
                if (!AppraisalCalculator.IsMouseOverPanel()) return true;
                if (OverlayHandler.current != null) OverlayHandler.current.HideTooltip();
                __result = false;
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AppraisalCalculator] tooltip block failed: " + ex.Message);
                return true;
            }
        }
    }
}
