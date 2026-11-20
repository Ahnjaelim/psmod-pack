using System;
using HarmonyLib;
using UnityEngine;

namespace LabelPriceEffect
{
    // 아이템 툴팁의 "[라벨] 슬쩍 할인 I" 줄에 가격 영향을 색과 퍼센트로 표시한다.
    //   남의 물건: 가격을 올리는 라벨 -> 빨강, 내리는 라벨 -> 초록 (살 때 기준)
    //   내 물건:   가격을 올리는 라벨 -> 초록, 내리는 라벨 -> 빨강 (팔 때 기준)
    //   영향 없음           -> 원래 색, 퍼센트 생략
    // 퍼센트는 게임이 가격 계산에 쓰는 ItemFeature.GetCurrentValueMod() (가격 × (1 + n/100)) 그대로.
    // "[라벨]" 머리말 색(감정으로 진위 확인 시 초록/빨강)은 원본 규칙을 그대로 따른다.
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.

    [HarmonyPatch(typeof(LabelerHelper), nameof(LabelerHelper.HandleLine))]
    internal static class HandleLinePatch
    {
        private static bool Prefix(RichTextBuilder builder, ItemFeature itemFeature)
        {
            try
            {
                if (builder == null || itemFeature == null) return true;

                RenderHandler.ColorPalette prefixColor = RenderHandler.ColorPalette.White;
                if (itemFeature.IsFeatureDiscovered())
                    prefixColor = itemFeature.IsFeatureMatch() ? RenderHandler.ColorPalette.LightGreen : RenderHandler.ColorPalette.LightRed;

                int mod = itemFeature.GetCurrentValueMod();
                // 남의 물건: 비싸지면 사는 쪽 손해(빨강). 내 물건: 비싸지면 파는 쪽 이득(초록) → 색 반대.
                GameItem owner = itemFeature.GetParentItem();
                bool mine = owner != null && GeneralHelper.IsItemOwned(owner);
                bool good = mine ? mod > 0 : mod < 0;
                RenderHandler.ColorPalette textColor =
                    mod == 0 ? RenderHandler.ColorPalette.None :
                    good ? RenderHandler.ColorPalette.Green :
                    RenderHandler.ColorPalette.LightRed;

                builder.Add(LocHelper.GetLocalizedMechanic("mech_label_tooltip_prefix") + " ", prefixColor);
                builder.Add(itemFeature.GetBaseCurrentDisplay(), textColor);
                if (mod != 0)
                    builder.Add(" (" + (mod > 0 ? "+" : "") + mod + "%)", textColor);
                builder.AddLine("");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[LabelPriceEffect] failed, using vanilla: " + ex.Message);
                return true;
            }
        }
    }
}
