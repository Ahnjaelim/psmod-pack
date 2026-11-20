// 원본: Korean Translation (korean_translation_rnin) by Gangmin(RNIN), Steam Workshop 3828896687, v1.0.10.
// 디컴파일해서 한국어 패치에 합침 (개인 사용). 네임스페이스/모드 ID/데이터 경로만 바꿈.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace KoreanPatch.Translation;

[HarmonyPatch]
internal static class LocaleMappingPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(LocHelper), "SteamLanguageToLocale", (Type[])null, (Type[])null);
		yield return AccessTools.Method(typeof(LocHelper), "SystemLanguageToLocale", (Type[])null, (Type[])null);
	}

	private static void Postfix(string __result)
	{
		DetectLocalePatch.lastMapped = __result;
	}
}
