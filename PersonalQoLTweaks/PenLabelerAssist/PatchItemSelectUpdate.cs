using System;

namespace PenLabelerAssist;

internal static class PatchItemSelectUpdate
{
	private static void Postfix(ref bool __result)
	{
		try
		{
			if (__result && Assist.ShouldKeepItemSelectTransparent())
			{
				__result = false;
			}
		}
		catch (Exception ex)
		{
			Log.Warn("item select update hook failed: " + ex.Message);
		}
	}
}
