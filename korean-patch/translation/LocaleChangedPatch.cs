// 원본: Korean Translation (korean_translation_rnin) by Gangmin(RNIN), Steam Workshop 3828896687, v1.0.10.
// 디컴파일해서 한국어 패치에 합침 (개인 사용). 네임스페이스/모드 ID/데이터 경로만 바꿈.
using HarmonyLib;
using UnityEngine;

namespace KoreanPatch.Translation;

[HarmonyPatch(typeof(MainMenuUIController), "OnLocaleChanged")]
internal static class LocaleChangedPatch
{
	private static bool Prefix(MainMenuUIController __instance, int localeIndex)
	{
		if (localeIndex < LocaleList.Game().Length || localeIndex >= LocaleList.All().Length)
		{
			return true;
		}
		string item = LocaleList.All()[localeIndex].Item1;
		LocaleList.Change(__instance, item);
		ES3.Save<string>("locale", item);
		Debug.Log((object)("Locale changed to: " + item));
		return false;
	}
}
