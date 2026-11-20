// 원본: Korean Translation (korean_translation_rnin) by Gangmin(RNIN), Steam Workshop 3828896687, v1.0.10.
// 디컴파일해서 한국어 패치에 합침 (개인 사용). 네임스페이스/모드 ID/데이터 경로만 바꿈.
using System.Collections.Generic;
using HarmonyLib;

namespace KoreanPatch.Translation;

[HarmonyPatch(typeof(MainMenuUIController), "HandleLocaleDropdown")]
internal static class LocaleDropdownPatch
{
	private static bool Prefix(MainMenuUIController __instance)
	{
		(string, string)[] array = LocaleList.All();
		string text = LocHelper.ResolveStartupLocale();
		List<string> list = new List<string>();
		int num = 0;
		for (int i = 0; i < array.Length; i++)
		{
			list.Add(array[i].Item2);
			if (array[i].Item1 == text)
			{
				num = i;
			}
		}
		__instance.localeDropdown.ClearOptions();
		__instance.localeDropdown.AddOptions(list);
		__instance.localeDropdown.SetValueWithoutNotify(num);
		LocaleList.Change(__instance, array[num].Item1);
		return false;
	}
}
