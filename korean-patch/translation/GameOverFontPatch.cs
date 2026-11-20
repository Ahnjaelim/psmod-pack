// 원본: Korean Translation (korean_translation_rnin) by Gangmin(RNIN), Steam Workshop 3828896687, v1.0.10.
// 디컴파일해서 한국어 패치에 합침 (개인 사용). 네임스페이스/모드 ID/데이터 경로만 바꿈.
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

using Object = UnityEngine.Object;

namespace KoreanPatch.Translation;

[HarmonyPatch(typeof(GameOverUIManager), "DisplayUI")]
internal static class GameOverFontPatch
{
	private static void Prefix(TextMeshProUGUI ___reason, TextMeshProUGUI ___description)
	{
		if (!KoreanTableFiller.KoreanSelected() || (Object)(object)FontLoader.Instance == (Object)null)
		{
			return;
		}
		TMP_FontAsset fontByRole = FontLoader.Instance.GetFontByRole(FontLoader.FontRole.Noto_Sans, LocalizationSettings.SelectedLocale);
		if (!((Object)(object)fontByRole == (Object)null))
		{
			if ((Object)(object)___reason != (Object)null)
			{
				((TMP_Text)___reason).font = fontByRole;
			}
			if ((Object)(object)___description != (Object)null)
			{
				((TMP_Text)___description).font = fontByRole;
			}
		}
	}
}
