using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ModNameTranslate
{
    // 모드 메뉴에 중국어로 나오는 다른 모드 이름을 번역해서 보여준다 (메모리의 manifest.Name 만 바꾸고 파일은 건드리지 않음).
    //  1) Localization 의 "modname_<모드ID>" 가 있으면 그 이름
    //  2) 없고 이름에 한자가 있으면 "中文 / English" 에서 영어 부분만
    // 원래 이름을 기억해 두고 모드 패널을 열 때마다 다시 적용하므로 게임 언어를 바꿔도 따라간다.
    internal static class Translator
    {
        private static readonly Dictionary<string, string> Originals = new Dictionary<string, string>();

        internal static void ApplyAll()
        {
            try
            {
                foreach (LoadedMod mod in ModLoader.Mods)
                {
                    ModManifest m = mod?.Manifest;
                    if (m == null || string.IsNullOrEmpty(m.ID)) continue;
                    if (!Originals.TryGetValue(m.ID, out string original))
                    {
                        original = m.Name ?? "";
                        Originals[m.ID] = original;
                    }
                    string translated = Translate(m.ID, original);
                    if (!string.IsNullOrEmpty(translated)) m.Name = translated;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ModNameTranslate] " + ex.Message);
            }
        }

        private static string Translate(string id, string original)
        {
            string key = "modname_" + id;
            string s = PenLabelerAssist.Text.Get(key);
            if (!string.IsNullOrEmpty(s) && s != key) return s;
            return HasHan(original) ? LatinPart(original) ?? original : original;
        }

        private static bool HasHan(string s)
        {
            foreach (char c in s)
                if ((c >= '一' && c <= '鿿') || (c >= '㐀' && c <= '䶿')) return true;
            return false;
        }

        // "水箱液位显示 / Water Tank Liquid Visuals" → "Water Tank Liquid Visuals"
        private static string LatinPart(string s)
        {
            foreach (string part in s.Split(new[] { " / ", " | ", "/", "|" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = part.Trim();
                if (!HasHan(t) && CountLatin(t) >= 3) return t;
            }
            var sb = new StringBuilder();
            foreach (char c in s)
                if (!((c >= '一' && c <= '鿿') || (c >= '㐀' && c <= '䶿') || (c >= '　' && c <= '〿') || (c >= '＀' && c <= '￯'))) sb.Append(c);
            string rest = sb.ToString().Trim(' ', '/', '|', '-', '(', ')');
            return CountLatin(rest) >= 3 ? rest : null;
        }

        private static int CountLatin(string s)
        {
            int n = 0;
            foreach (char c in s)
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) n++;
            return n;
        }
    }

    [HarmonyPatch(typeof(ModUIManager), nameof(ModUIManager.OpenPanel))]
    internal static class OpenPanelPatch
    {
        private static void Prefix() => Translator.ApplyAll();
    }
}
