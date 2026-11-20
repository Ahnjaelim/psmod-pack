// 원본: Korean Translation (korean_translation_rnin) by Gangmin(RNIN), Steam Workshop 3828896687, v1.0.10.
// 디컴파일해서 한국어 패치에 합침 (개인 사용). 네임스페이스/모드 ID/데이터 경로만 바꿈.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace KoreanPatch.Translation;

internal static class LocaleList
{
	private static (string, string)[] all;

	private static readonly MethodInfo changeLanguage = AccessTools.Method(typeof(MainMenuUIController), "ChangeLanguage", (Type[])null, (Type[])null);

	internal static (string, string)[] Game()
	{
		return ((string, string)[])AccessTools.Field(typeof(MainMenuUIController), "LOCALE_DROPDOWN").GetValue(null);
	}

	internal static (string, string)[] All()
	{
		if (all == null)
		{
			List<(string, string)> list = new List<(string, string)>(Game());
			if (list.FindIndex(((string, string) x) => x.Item1 == "ko") < 0)
			{
				list.Add(("ko", "Korean"));
			}
			all = list.ToArray();
		}
		return all;
	}

	internal static void Change(MainMenuUIController menu, string code)
	{
		changeLanguage.Invoke(menu, new object[1] { code });
	}
}
