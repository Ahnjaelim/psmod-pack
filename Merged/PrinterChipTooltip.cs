using System;
using HarmonyLib;
using UnityEngine;

namespace PrinterChipTooltip
{
    // 3D 프린터(PRINTER_TAG) 툴팁에 칩 슬롯에 꽂힌 칩 이름을 표시한다.
    // 칩 슬롯 = 프린터 창 안에서 PRINTER_CHIP_TAG 아이템이 들어 있는 슬롯 인벤토리.
    [HarmonyPatch(typeof(MachineryHelper), nameof(MachineryHelper.CreateMachineryTooltip))]
    internal static class CreateMachineryTooltipPatch
    {
        private static void Postfix(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (builder == null || item == null || !item.IsTag("PRINTER_TAG") || item.contentWindow == null) return;

                GameItem chip = null;
                foreach (GameSlotInventory inv in GraphUtils.FindChildrenType<GameSlotInventory>(item.contentWindow))
                {
                    GameItem c = inv?.childItem;
                    if (c != null && c.IsTag("PRINTER_CHIP_TAG")) { chip = c; break; }
                }

                if (chip != null)
                    builder.AddLine(L("printer_chip", "Chip: {0}", chip.GetDisplayName()), true, RenderHandler.ColorPalette.LightCyan);
                else
                    builder.AddLine(L("printer_chip_none", "Chip: none"), true, RenderHandler.ColorPalette.Gray);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PrinterChipTooltip] " + ex.Message);
            }
        }

        private static string L(string key, string en, params object[] args)
        {
            string s = PenLabelerAssist.Text.Get(key, args);
            if (!string.IsNullOrEmpty(s) && s != key && !s.Contains("{0}")) return s;
            try { return string.Format(string.IsNullOrEmpty(s) || s == key ? en : s, args); }
            catch { return en; }
        }
    }
}
