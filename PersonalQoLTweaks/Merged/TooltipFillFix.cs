using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace TooltipFillFix
{
    // RichTextBuilder draws background boxes (e.g. the black box behind "Low-risk contraband") on a
    // separate text layer built by GetFillString, which replaces EVERY non-control character with ' ' or '█'
    // — including the characters of rich-text tags. A line full of tags (category pills, value-block sprites)
    // therefore becomes ~100 blanks in that layer, wraps onto extra lines, and every background box below
    // it is drawn one or more lines too low.
    //
    // Fix: in the fill layer keep layout tags verbatim (size / spacing / offsets, so line heights and widths
    // match the visible layer), turn a <sprite> into one fill character (it renders ~one glyph wide), and drop
    // all other tags (mark / color / alpha / link …) so they neither take space nor paint anything.
    // Text without tags is filled exactly like vanilla.
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.

    [HarmonyPatch(typeof(RichTextBuilder), nameof(RichTextBuilder.GetFillString))]
    internal static class GetFillStringPatch
    {
        private static readonly Regex Tag = new Regex(@"<(/?)([a-zA-Z-]+)[^<>]*>", RegexOptions.Compiled);

        private static readonly string[] KeepTags =
        {
            "size", "space", "voffset", "line-height", "cspace", "mspace", "b", "i",
            "font", "pos", "indent", "margin", "nobr", "width", "align",
        };

        private static bool Prefix(string text, char replace, ref string __result)
        {
            if (text == null || text.IndexOf('<') < 0) return true; // no tags: vanilla

            var sb = new StringBuilder(text.Length);
            int last = 0;
            foreach (Match m in Tag.Matches(text))
            {
                AppendFill(sb, text, last, m.Index - last, replace);
                string name = m.Groups[2].Value.ToLowerInvariant();
                if (name == "sprite")
                {
                    if (m.Groups[1].Value.Length == 0) sb.Append(replace);
                }
                else if (System.Array.IndexOf(KeepTags, name) >= 0)
                {
                    sb.Append(m.Value);
                }
                last = m.Index + m.Length;
            }
            AppendFill(sb, text, last, text.Length - last, replace);
            __result = sb.ToString();
            return false;
        }

        private static void AppendFill(StringBuilder sb, string text, int start, int count, char replace)
        {
            for (int i = start; i < start + count; i++)
            {
                char c = text[i];
                sb.Append(char.IsControl(c) ? c : replace);
            }
        }
    }
}
