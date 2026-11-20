using System;
using HarmonyLib;
using UnityEngine;

namespace TooltipFixedWidth
{
    // 아이템 툴팁(OverlayHandler.tooltipElement) 가로 폭을 고정한다.
    // 게임은 폭 = min(가장 긴 줄, 60자) 라서 아이템마다 툴팁 폭이 들쭉날쭉하다.
    //   - 폭(WidthPixelsClamp) = Width 고정
    //   - 글자 칸 네 겹(글자/배경/밑줄/취소선)은 자동 줄바꿈 켜고, 폭 = Width - 좌우 여백
    //   - 높이(HeightPixelsClamp) = 그 폭으로 줄바꿈했을 때 높이 + 위아래 여백
    // 다른 RichTextElement(창 안 글 등)는 건드리지 않는다.
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.
    internal static class Tip
    {
        internal const int Width = 210; // UI 단위 (게임 창 캔버스 기준)

        private static RichTextElement cached;
        private static OverlayHandler cachedOwner;

        internal static bool Is(RichTextElement e)
        {
            if (e == null) return false;
            OverlayHandler oh = OverlayHandler.current;
            if (oh == null) return false;
            // 툴팁 칸은 OverlayHandler 가 생긴 뒤에 만들어지므로, 못 찾았으면 다음에 다시 찾는다
            if (oh != cachedOwner || cached == null)
            {
                cachedOwner = oh;
                cached = null;
                try { cached = AccessTools.Field(typeof(OverlayHandler), "tooltipElement").GetValue(oh) as RichTextElement; }
                catch (Exception ex) { Debug.LogWarning("[TooltipFixedWidth] " + ex.Message); }
            }
            return cached != null && ReferenceEquals(e, cached);
        }

        internal static void Wrap(TreeNodeRender n, float width)
        {
            if (n == null || n.text == null) return;
            n.text.enableWordWrapping = true;
            Vector2 s = n.rectTransform.sizeDelta;
            n.rectTransform.sizeDelta = new Vector2(width, s.y);
        }
    }

    [HarmonyPatch(typeof(RichTextElement), nameof(RichTextElement.WidthPixelsClamp))]
    internal static class WidthPatch
    {
        private static bool Prefix(RichTextElement __instance, ref int __result)
        {
            if (!Tip.Is(__instance)) return true;
            __result = Tip.Width;
            return false;
        }
    }

    [HarmonyPatch(typeof(RichTextElement), nameof(RichTextElement.HeightPixelsClamp))]
    internal static class HeightPatch
    {
        private static bool Prefix(RichTextElement __instance, ref int __result,
                                   TreeNodeRender ___textNode, TreeNodeRender ___textBackgroundNode,
                                   TreeNodeRender ___textUnderlineNode, TreeNodeRender ___textStrikethroughNode,
                                   int ___leftPadding, int ___rightPadding, int ___topPadding, int ___bottomPadding)
        {
            if (!Tip.Is(__instance) || ___textNode == null || ___textNode.text == null) return true;
            float w = Mathf.Max(16f, Tip.Width - ___leftPadding - ___rightPadding);
            Tip.Wrap(___textNode, w);
            Tip.Wrap(___textBackgroundNode, w);
            Tip.Wrap(___textUnderlineNode, w);
            Tip.Wrap(___textStrikethroughNode, w);
            float h = ___textNode.text.GetPreferredValues(___textNode.text.text, w, 0f).y;
            __result = Mathf.CeilToInt(h) + ___topPadding + ___bottomPadding;
            return false;
        }
    }

    // Validate 가 글자 칸 폭을 전체 폭으로 되돌리므로, 그 뒤에 여백을 뺀 폭 + 줄바꿈으로 다시 맞춘다
    [HarmonyPatch(typeof(RichTextElement), nameof(RichTextElement.Validate))]
    internal static class ValidatePatch
    {
        private static void Postfix(RichTextElement __instance,
                                    TreeNodeRender ___textNode, TreeNodeRender ___textBackgroundNode,
                                    TreeNodeRender ___textUnderlineNode, TreeNodeRender ___textStrikethroughNode,
                                    int ___leftPadding, int ___rightPadding)
        {
            if (!Tip.Is(__instance)) return;
            float w = Mathf.Max(16f, Tip.Width - ___leftPadding - ___rightPadding);
            Tip.Wrap(___textNode, w);
            Tip.Wrap(___textBackgroundNode, w);
            Tip.Wrap(___textUnderlineNode, w);
            Tip.Wrap(___textStrikethroughNode, w);
        }
    }
}
