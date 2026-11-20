using System;
using HarmonyLib;
using UnityEngine;

namespace AgeWellTooltip
{
    // 숙성기(wine_rack) 툴팁에 하루 숙성 일수를 표시한다.
    // 게임 규칙 (MachineWineRack): 밤마다 전력이 있으면 1 + max(0, 품질%) / 125 일을 추가로 숙성.
    // 와인병은 숙성기와 무관하게 밤마다 스스로 +1일 숙성되므로 합계 = 1 + 숙성기 몫.
    [HarmonyPatch(typeof(MachineryHelper), nameof(MachineryHelper.CreateMachineryTooltip))]
    internal static class CreateMachineryTooltipPatch
    {
        private const int QualityPerDay = 125;

        private static void Postfix(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (builder == null || item == null || item.identifier != "wine_rack") return;
                int quality = Math.Max(0, MachineryHelper.GetCurrentQualityBonus(item));
                int machineDays = 1 + quality / QualityPerDay;
                int total = 1 + machineDays;
                int nextAt = (quality / QualityPerDay + 1) * QualityPerDay;

                builder.AddLine(" ");
                builder.AddLine(L("agewell_days", "Aging: +{0} days per day (wine +1 / machine +{1})", total, machineDays),
                    true, RenderHandler.ColorPalette.LightCyan);
                builder.AddLine(L("agewell_next", "Next +1 day at quality +{0}%", nextAt),
                    true, RenderHandler.ColorPalette.Gray);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AgeWellTooltip] " + ex.Message);
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
