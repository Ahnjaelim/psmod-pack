// 원본: Korean Translation (korean_translation_rnin) by Gangmin(RNIN), Steam Workshop 3828896687, v1.0.10.
// 디컴파일해서 한국어 패치에 합침 (개인 사용). 네임스페이스/모드 ID/데이터 경로만 바꿈.
using HarmonyLib;

namespace KoreanPatch.Translation;

[HarmonyPatch(typeof(LocHelper), "DetectPlayerLocale")]
internal static class DetectLocalePatch
{
	internal static string lastMapped;

	private static void Prefix()
	{
		lastMapped = null;
	}

	private static void Postfix(ref string __result)
	{
		if (!(lastMapped != "ko"))
		{
			__result = "ko";
			KoreanMod.Log.Log((object)"First launch: Korean language detected");
		}
	}
}
