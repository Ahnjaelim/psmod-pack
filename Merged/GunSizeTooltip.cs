using System;
using HarmonyLib;
using UnityEngine;

namespace GunSizeTooltip
{
    // 총기/총기 부품 툴팁에 크기(차지하는 칸 수)를 표시한다.
    // 총기: 총기 주문 조건이 보는 GUN_SIZE_TAG 값 (부품 장착 포함, 스탯 갱신 때 기록됨). 없으면 현재 모양 칸 수.
    // 부품: 부품 자체 모양 칸 수.
    [HarmonyPatch(typeof(GunHelper), nameof(GunHelper.CreateTooltip))]
    internal static class GunTooltipPatch
    {
        private static void Postfix(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (builder == null || item == null || !item.IsTag("ADV_GUN_TAG")) return;
                int size = item.IsTag("GUN_SIZE_TAG") ? item.GetTagReadonly("GUN_SIZE_TAG").GetInt() : 0;
                if (size <= 0) size = item.GetModifiedItemSize();
                SizeLine.Add(builder, size);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GunSizeTooltip] " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(GunModHelper), nameof(GunModHelper.CreateTooltip))]
    internal static class GunModTooltipPatch
    {
        private static void Postfix(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (builder == null || item == null || !item.IsTag("WEAPON_MOD_TAG") || item.IsTag("ADV_GUN_TAG")) return;
                SizeLine.Add(builder, item.GetModifiedItemSize());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GunSizeTooltip] " + ex.Message);
            }
        }
    }

    internal static class SizeLine
    {
        internal static void Add(RichTextBuilder builder, int size)
        {
            if (size <= 0) return;
            string label = LocHelper.GetLocalizedMechanicOrDefault("mech_gun_size", "Size");
            builder.Add(label + ": ", RenderHandler.ColorPalette.LightBlue);
            builder.AddLine(size.ToString(), true, RenderHandler.ColorPalette.LightBlue);
        }
    }
}
