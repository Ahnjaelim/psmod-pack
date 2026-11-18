using System;
using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace WaterVolumeCommas
{
    // 물/액체 용기 툴팁의 "8189 ml / 20000 ml" 숫자에 천 단위 쉼표를 찍는다 → "8,189 ml / 20,000 ml".
    // WaterHelper.CreateWaterContainerTooltip이 이번에 추가한 줄 중 "ml"이 들어간 줄만 고친다.
    [HarmonyPatch(typeof(WaterHelper), nameof(WaterHelper.CreateWaterContainerTooltip))]
    internal static class CreateWaterContainerTooltipPatch
    {
        private static readonly Regex Number = new Regex(@"(?<![\d.,])\d{4,}(?![\d.,])", RegexOptions.Compiled);

        private static void Prefix(RichTextBuilder builder, out int __state)
        {
            __state = -1;
            try
            {
                if (Traverse.Create(builder).Field("slices").GetValue() is IList slices) __state = slices.Count;
            }
            catch
            {
            }
        }

        private static void Postfix(RichTextBuilder builder, int __state)
        {
            try
            {
                if (__state < 0 || !(Traverse.Create(builder).Field("slices").GetValue() is IList slices)) return;
                for (int i = __state; i < slices.Count; i++)
                {
                    var tr = Traverse.Create(slices[i]).Field("text");
                    if (!(tr.GetValue() is string s) || s.IndexOf("ml", StringComparison.Ordinal) < 0) continue;
                    string fixedText = Number.Replace(s, m =>
                        long.TryParse(m.Value, NumberStyles.None, CultureInfo.InvariantCulture, out long v)
                            ? v.ToString("#,0", CultureInfo.InvariantCulture)
                            : m.Value);
                    if (fixedText != s) tr.SetValue(fixedText);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WaterVolumeCommas] " + ex.Message);
            }
        }
    }
}
