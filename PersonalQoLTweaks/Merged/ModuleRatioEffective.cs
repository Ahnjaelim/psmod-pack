using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace ModuleRatioEffective
{
    // 모듈 효과 줄 "성능: +1400% (비율 0.5)" 뒤에 실제 적용되는 값 " → +700%"를 붙인다.
    // 게임 규칙 (ModuleHelper): 성능 = 기본 × (1 + 보너스% / 비율값 / 100), 품질 = 보너스% / 비율값 (정수 나눗셈).
    // 툴팁에 보이는 "비율 0.5"는 1 / 비율값(MACHINE_*_RATIO_INT) 이다.
    [HarmonyPatch(typeof(MachineryHelper), nameof(MachineryHelper.AddTooltipModuleBonus))]
    internal static class AddTooltipModuleBonusPatch
    {
        private static void Prefix(RichTextBuilder builder, out int __state)
        {
            __state = -1;
            try
            {
                var slices = Traverse.Create(builder).Field("slices").GetValue() as IList;
                if (slices != null) __state = slices.Count;
            }
            catch
            {
            }
        }

        private static void Postfix(RichTextBuilder builder, GameItem item, int __state)
        {
            try
            {
                if (__state < 0 || builder == null || item == null) return;
                if (!item.GetTagReadonly("IS_RECEIVING_BONUS_FROM_MODULE_BOOL").GetBool()) return;
                var slices = Traverse.Create(builder).Field("slices").GetValue() as IList;
                if (slices == null) return;

                string ratioLabel = LocHelper.GetLocalizedMechanic("mech_ratio");
                int perf = item.GetTagReadonly("TOTAL_PERCENTAGE_PERFORMANCE_BONUS_INT").GetInt();
                int qual = item.GetTagReadonly("TOTAL_PERCENTAGE_QUALITY_BONUS_INT").GetInt();

                string perfFrom = null, perfTo = null, qualFrom = null, qualTo = null;
                if (item.IsTag("MACHINE_PERFORMANCE_RATIO_INT"))
                {
                    int r = item.GetTagReadonly("MACHINE_PERFORMANCE_RATIO_INT").GetInt();
                    if (r > 0 && perf != 0)
                    {
                        perfFrom = Signed(perf) + "%" + Suffix(ratioLabel, r);
                        perfTo = perfFrom + " → " + Signed(perf / r) + "%";
                    }
                }
                if (item.IsTag("MACHINE_QUALITY_RATIO_INT"))
                {
                    int r = item.GetTagReadonly("MACHINE_QUALITY_RATIO_INT").GetInt();
                    if (r > 0 && qual != 0)
                    {
                        qualFrom = Signed(qual) + "%" + Suffix(ratioLabel, r);
                        qualTo = qualFrom + " → " + Signed(qual / r) + "%";
                    }
                }
                if (perfFrom == null && qualFrom == null) return;

                string perfName = LocHelper.GetLocalizedMechanic("mech_module_stat_performance");
                for (int i = __state; i < slices.Count; i++)
                {
                    var tr = Traverse.Create(slices[i]).Field("text");
                    if (!(tr.GetValue() is string s) || s.IndexOf(ratioLabel, StringComparison.Ordinal) < 0) continue;
                    // 성능 줄 / 품질 줄 구분: 성능 줄이 먼저 나온다. 두 줄 문자열이 같으면 어느 쪽이든 같은 값.
                    if (perfFrom != null && s.EndsWith(perfFrom, StringComparison.Ordinal) && (qualFrom == null || s.Contains(perfName) || perfFrom != qualFrom))
                    {
                        tr.SetValue(s.Substring(0, s.Length - perfFrom.Length) + perfTo);
                        perfFrom = null;
                    }
                    else if (qualFrom != null && s.EndsWith(qualFrom, StringComparison.Ordinal))
                    {
                        tr.SetValue(s.Substring(0, s.Length - qualFrom.Length) + qualTo);
                        qualFrom = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ModuleRatioEffective] " + ex.Message);
            }
        }

        private static string Signed(int v) => v >= 0 ? "+" + v : "-" + (-v);

        // 게임과 같은 문자열: " (" + 비율 + " " + 1f / 비율값 + ")"
        private static string Suffix(string label, int r) => " (" + label + " " + (1f / r) + ")";
    }
}
