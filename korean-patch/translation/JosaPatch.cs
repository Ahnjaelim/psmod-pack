// 원본: Korean Translation (korean_translation_rnin) by Gangmin(RNIN), Steam Workshop 3828896687, v1.0.10.
// 디컴파일해서 한국어 패치에 합침 (개인 사용). 네임스페이스/모드 ID/데이터 경로만 바꿈.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.Localization.Pseudo;
using UnityEngine.Localization.Tables;

namespace KoreanPatch.Translation;

[HarmonyPatch(typeof(StringTableEntry), "GetLocalizedString", new Type[]
{
	typeof(IFormatProvider),
	typeof(IList<object>),
	typeof(PseudoLocale)
})]
internal static class JosaPatch
{
	private static void Postfix(ref string __result)
	{
		__result = Josa.Fix(__result);
	}
}
