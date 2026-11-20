using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.TextCore.LowLevel;

namespace KoreanPatch
{
    // 한국어일 때 장식용(Lubrifont)·SF(Tektur, Tektur Bold) 역할 폰트만 물마루(Mulmaru)로 바꾼다.
    // 본문(Noto Sans/Serif 등)과 손글씨(Caveat)는 원래 한국어 폰트 그대로.
    // 한국어 번역(translation/, 원본 KoreanTranslation)은 기본 설정에서 SF 칸을 본문 폰트로 바꾸므로, 이 패치는 그보다 늦게(Priority.Last) 돈다.
    public class KoreanPatchMod : IMod
    {
        internal const string ModId = "korean-patch";

        public void Init(ModManifest manifest)
        {
            Debug.Log("[KoreanPatch] init v" + (manifest != null ? manifest.ModVersion : "?"));
            Translation.KoreanMod.Init(manifest); // 한국어 번역 (원본 Korean Translation by Gangmin(RNIN))
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
        }
    }

    internal static class Mulmaru
    {
        private static TMP_FontAsset asset;
        private static bool failed;

        internal static TMP_FontAsset Get(TMP_FontAsset fallback)
        {
            if (asset != null || failed) return asset;
            try
            {
                // 파일 확인은 게임의 모드 전용 API로 (자기 모드 폴더 안만 본다 → 스캐너 "files" 권한 불필요)
                string path = FontPath();
                if (path == null || !ModHelper.ModFileExists(KoreanPatchMod.ModId, "Fonts/Mulmaru.ttf"))
                {
                    failed = true;
                    Debug.LogWarning("[KoreanPatch] Mulmaru.ttf not found");
                    return null;
                }
                var font = new Font(path) { name = "Mulmaru" };
                asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                if (asset == null)
                {
                    failed = true;
                    Debug.LogWarning("[KoreanPatch] could not create Mulmaru font asset");
                    return null;
                }
                asset.name = "Mulmaru SDF";
                // 물마루에 없는 글자(한자 등)는 원래 한국어 폰트로
                if (fallback != null)
                    asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
                Debug.Log("[KoreanPatch] Mulmaru font asset created");
            }
            catch (Exception ex)
            {
                failed = true;
                Debug.LogWarning("[KoreanPatch] font load failed: " + ex.Message);
            }
            return asset;
        }

        private static string FontPath()
        {
            // ModLoader 는 모드 스캐너가 막으므로 게임의 모드 전용 API로 자기 폴더를 얻는다
            string folder = ModHelper.GetModFolder(KoreanPatchMod.ModId);
            return string.IsNullOrEmpty(folder) ? null : Path.Combine(folder, "Fonts", "Mulmaru.ttf");
        }
    }

    [HarmonyPatch(typeof(FontLoader), nameof(FontLoader.GetFontByRole))]
    internal static class FontRolePatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(FontLoader __instance, FontLoader.FontRole role, Locale locale, ref TMP_FontAsset __result)
        {
            try
            {
                if (locale == null || locale.Identifier.Code != "ko") return;
                if (role != FontLoader.FontRole.Lubrifont && role != FontLoader.FontRole.Tektur && role != FontLoader.FontRole.Tektur_Bold) return;
                TMP_FontAsset m = Mulmaru.Get(__instance != null ? __instance.koreanFont : null);
                if (m != null) __result = m;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[KoreanPatch] " + ex.Message);
            }
        }
    }
}
