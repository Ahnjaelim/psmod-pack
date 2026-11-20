using System;
using HarmonyLib;
using UnityEngine;

namespace BlockProgressBar
{
    // 툴팁 진행 막대 "[=====     ]"를 꽉 찬 블록 막대로 바꾼다: 진행한 칸은 밝은 색 █, 남은 칸은 어두운 색 █.
    // 모든 툴팁 막대(기계 진행, 체력, 내구도, 배고픔 등)가 RichTextBuilder.AddProgressBar 하나를 쓰므로 여기만 바꾼다.
    // █ 글자는 게임이 라벨 배경(예: "기계" 태그 박스)을 그릴 때 쓰는 글자라 폰트에 확실히 있다.
    [HarmonyPatch(typeof(RichTextBuilder), nameof(RichTextBuilder.AddProgressBar))]
    internal static class AddProgressBarPatch
    {
        private const char Block = '█';

        private static bool Prefix(RichTextBuilder __instance, ref RichTextBuilder __result, float ratio, int length,
            RenderHandler.ColorPalette color, RenderHandler.ColorPalette backgroundColor, string end, bool addFullLength, bool bold)
        {
            if (MachineProgressMove.State.Suppress)
            {
                // 진행 막대가 "진행 속도" 밑으로 옮겨진 기계: 원래 자리의 막대와 다음 "현재/목표" 줄은 그리지 않는다.
                MachineProgressMove.State.SkipLine = true;
                __result = __instance;
                return false;
            }
            try
            {
                if (length <= 0) return true;
                if (!float.IsFinite(ratio)) ratio = 0f;
                ratio = Mathf.Clamp01(ratio);

                int filled = (int)(ratio * length);
                if (filled == 0 && ratio > 0f) filled = 1;          // 조금이라도 진행했으면 한 칸은 보이게
                if (filled == length && ratio < 1f) filled = length - 1; // 완료 전에는 꽉 차 보이지 않게
                int empty = addFullLength ? length - filled : 0;

                RenderHandler.ColorPalette fillColor = color == RenderHandler.ColorPalette.None ? RenderHandler.ColorPalette.LightGray : color;
                RenderHandler.ColorPalette emptyColor = backgroundColor == RenderHandler.ColorPalette.None ? RenderHandler.ColorPalette.VeryDarkGray : backgroundColor;
                end = end ?? "";

                if (filled > 0)
                {
                    __instance.Add(new string(Block, filled), fillColor, empty > 0 ? "" : end, bold);
                    Stubs.MarkLastSliceNoBreak(__instance);
                }
                if (empty > 0)
                {
                    __instance.Add(new string(Block, empty), emptyColor, end, bold);
                    Stubs.MarkLastSliceNoBreak(__instance);
                }
                __result = __instance;
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BlockProgressBar] " + ex.Message);
                return true;
            }
        }
    }

    [HarmonyPatch]
    internal static class Stubs
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(RichTextBuilder), "MarkLastSliceNoBreak")]
        public static RichTextBuilder MarkLastSliceNoBreak(RichTextBuilder instance)
        {
            throw new NotImplementedException("stub");
        }
    }
}
