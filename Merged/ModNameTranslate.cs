using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ModNameTranslate
{
    // 모드 메뉴에 중국어로 나오는 다른 모드 이름을 번역해서 보여준다 (파일은 건드리지 않음).
    //  1) Localization 의 "modname_<모드ID>" 가 있으면 그 이름
    //  2) 없고 이름에 한자가 있으면 "中文 / English" 에서 영어 부분만
    // 모드 스캐너가 ModLoader 접근을 막으므로, 모드 메뉴가 이름을 그리는 곳(목록 줄 / 상세 화면)에서 바꾼다.
    internal static class Translator
    {
        internal static string Translate(string id, string original)
        {
            if (string.IsNullOrEmpty(id)) return original;
            string key = "modname_" + id;
            string s = PenLabelerAssist.Text.Get(key);
            if (!string.IsNullOrEmpty(s) && s != key) return s;
            if (string.IsNullOrEmpty(original) || !HasHan(original)) return original;
            return LatinPart(original) ?? original;
        }

        private static bool IsHan(char c) => (c >= '一' && c <= '鿿') || (c >= '㐀' && c <= '䶿');

        private static bool HasHan(string s)
        {
            foreach (char c in s)
                if (IsHan(c)) return true;
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
                if (!(IsHan(c) || (c >= '　' && c <= '〿') || (c >= '＀' && c <= '￯'))) sb.Append(c);
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

    // 모드 목록 한 줄
    [HarmonyPatch(typeof(ModElement), nameof(ModElement.Init))]
    internal static class ModElementInitPatch
    {
        private static void Prefix(string id, ref string name)
        {
            try { name = Translator.Translate(id, name); }
            catch (Exception ex) { Debug.LogWarning("[ModNameTranslate] " + ex.Message); }
        }
    }

    // 오른쪽 상세 화면 (manifest.Name 을 직접 읽으므로 그 전에 메모리상의 이름만 바꿔 둔다)
    [HarmonyPatch(typeof(ModUIManager), "ShowMod")]
    internal static class ShowModPatch
    {
        private static readonly HashSet<string> done = new HashSet<string>();

        private static void Prefix(LoadedMod mod)
        {
            try
            {
                ModManifest m = mod?.Manifest;
                if (m == null || string.IsNullOrEmpty(m.ID) || done.Contains(m.ID)) return;
                done.Add(m.ID);
                m.Name = Translator.Translate(m.ID, m.Name);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ModNameTranslate] " + ex.Message);
            }
        }
    }
}
