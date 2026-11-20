// 원본: Korean Translation (korean_translation_rnin) by Gangmin(RNIN), Steam Workshop 3828896687, v1.0.10.
// 디컴파일해서 한국어 패치에 합침 (개인 사용). 네임스페이스/모드 ID/데이터 경로만 바꿈. Gugi 폰트 설정(ui_font_gugi)은 뺌 = 항상 꺼짐.
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

using Object = UnityEngine.Object;

namespace KoreanPatch.Translation;

[HarmonyPatch(typeof(FontLoader), "GetFontByRole")]
internal static class FontPatch
{
	private static void Postfix(Locale locale, ref TMP_FontAsset __result, TMP_FontAsset ___koreanFont, TMP_FontAsset ___koreanScifiFont)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)locale == (Object)null))
		{
			LocaleIdentifier identifier = locale.Identifier;
			if (!(identifier.Code != "ko") && (Object)(object)___koreanFont != (Object)null && (Object)(object)__result != (Object)null && (Object)(object)__result == (Object)(object)___koreanScifiFont)
			{
				__result = ___koreanFont;
			}
		}
	}
}
