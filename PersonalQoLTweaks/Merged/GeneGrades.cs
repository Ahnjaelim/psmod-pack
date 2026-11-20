using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GeneGrades
{
    // 동물 툴팁의 유전자 능력치 줄마다 등급 [S]~[F] 를 붙인다.
    //
    // 등급은 유전 발현값(두 대립유전자 평균, 혈청 등 보너스 제외)을 종 기준값과 비교한 비율로 매긴다.
    // 허기 속도처럼 낮을수록 좋은 항목은 비율을 뒤집는다.
    //   S: +30% 이상 / A: +15% 이상 / B: +5% 이상 / C: ±5% 이내 / D: -5% 미만 / F: -15% 미만
    // (게임 자체 색 구분이 ±5% 기준이라 C 구간을 거기에 맞춤)
    //
    // 줄은 HusbandryHelper.CreateItemTooltip 안의 로컬 함수 AddStatLine 이 그리고, 마지막에 builder.AddLine("") 로 줄을 끝낸다.
    // 그래서 AddStatLine 실행 중에 오는 첫 AddLine 직전에 등급을 끼워 넣는다.
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.

    internal static class GradeContext
    {
        [ThreadStatic] public static GameItem Item;
        [ThreadStatic] public static AnimalSpecies Species;
        [ThreadStatic] public static string PendingTag;
    }

    [HarmonyPatch(typeof(HusbandryHelper), nameof(HusbandryHelper.CreateItemTooltip))]
    internal static class CreateItemTooltipPatch
    {
        // 등급 표기 때문에 줄이 넘어가지 않도록 동물 툴팁 폭을 조금 넓힌다 (글자 수 기준).
        private const int MinTooltipChars = 52;

        private static void Prefix(RichTextBuilder builder, GameItem item)
        {
            if (builder != null && item != null && item.IsTag("ANIMAL_ITEM_TAG") && builder.maxCharWidth < MinTooltipChars)
                builder.maxCharWidth = MinTooltipChars;
            GradeContext.Item = item;
            try
            {
                GradeContext.Species = item != null ? HusbandryHelper.GetSpecies(item) : null;
            }
            catch
            {
                GradeContext.Species = null;
            }
        }

        private static void Finalizer()
        {
            GradeContext.Item = null;
            GradeContext.Species = null;
            GradeContext.PendingTag = null;
        }
    }

    [HarmonyPatch]
    internal static class AddStatLinePatch
    {
        // 게임 모드 검사기가 조건 검색(FirstMethod + 람다)을 reflection-external 로 막으므로 컴파일된 이름을 그대로 쓴다.
        // 게임 업데이트로 이름이 바뀌면 Prepare 가 false 를 돌려 이 패치만 건너뛴다 (모드팩 전체는 정상 로드).
        private const string LocalFunctionName = "<CreateItemTooltip>g__AddStatLine|93_1";

        private static bool Prepare()
        {
            bool found = AccessTools.Method(typeof(HusbandryHelper), LocalFunctionName) != null;
            if (!found) Debug.LogWarning("[GeneGrades] AddStatLine not found; gene grades disabled for this game version");
            return found;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(HusbandryHelper), LocalFunctionName);
        }

        private static void Prefix(string tagID)
        {
            GradeContext.PendingTag = tagID;
        }

        private static void Finalizer()
        {
            GradeContext.PendingTag = null;
        }
    }

    [HarmonyPatch(typeof(RichTextBuilder), nameof(RichTextBuilder.AddLine))]
    internal static class AddLinePatch
    {
        private static void Prefix(RichTextBuilder __instance)
        {
            string tag = GradeContext.PendingTag;
            if (tag == null) return;
            GradeContext.PendingTag = null; // 한 줄에 한 번만
            try
            {
                GameItem item = GradeContext.Item;
                AnimalSpecies species = GradeContext.Species;
                if (!Grades.TryGetBase(item, species, tag, out float baseValue)) return;

                // 유전자 스캐너로 두 대립유전자 값이 보일 때만 좌/우 각각 등급 [A / S], 아니면 발현값 하나 [B]
                bool scanning = PlayerStore.Instance != null && PlayerStore.Instance.isGeneScannerScanning;
                if (scanning)
                {
                    float a = item.GetTagReadonly(tag + "_ALLELE_A").GetFloat();
                    float b = item.GetTagReadonly(tag + "_ALLELE_B").GetFloat();
                    Grades.Grade(tag, a, baseValue, out string ga, out RenderHandler.ColorPalette ca);
                    Grades.Grade(tag, b, baseValue, out string gb, out RenderHandler.ColorPalette cb);
                    __instance.Add("  " + ga, ca);
                    __instance.Add(" / ", RenderHandler.ColorPalette.Gray);
                    __instance.Add(gb, cb);
                }
                else
                {
                    float expressed = GeneticHelper.GetExpressedValue(item, tag);
                    Grades.Grade(tag, expressed, baseValue, out string g, out RenderHandler.ColorPalette c);
                    __instance.Add("  " + g, c);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GeneGrades] grade failed: " + ex.Message);
            }
        }
    }

    internal static class Grades
    {
        public static bool TryGetBase(GameItem item, AnimalSpecies species, string tagID, out float baseValue)
        {
            baseValue = 0f;
            if (item == null || species == null || species.geneticTraitIDs == null || !species.geneticTraitIDs.Contains(tagID)) return false;
            if (species.geneticBaseValues == null || !species.geneticBaseValues.TryGetValue(tagID, out baseValue) || baseValue <= 0f) return false;
            return item.IsTag(tagID + "_ALLELE_A");
        }

        public static void Grade(string tagID, float value, float baseValue, out string grade, out RenderHandler.ColorPalette color)
        {
            bool higherIsBetter = !HusbandryHelper.StatHigherIsBetter.TryGetValue(tagID, out bool h) || h;
            value = Mathf.Max(value, 0.0001f);
            float ratio = higherIsBetter ? value / baseValue : baseValue / value;

            if (ratio >= 1.30f) { grade = "S"; color = RenderHandler.ColorPalette.Purple; }
            else if (ratio >= 1.15f) { grade = "A"; color = RenderHandler.ColorPalette.Green; }
            else if (ratio >= 1.05f) { grade = "B"; color = RenderHandler.ColorPalette.LightGreen; }
            else if (ratio > 0.95f) { grade = "C"; color = RenderHandler.ColorPalette.LightCyan; }
            else if (ratio > 0.85f) { grade = "D"; color = RenderHandler.ColorPalette.Orange; }
            else { grade = "F"; color = RenderHandler.ColorPalette.LightRed; }
        }
    }
}
