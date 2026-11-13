using System;

namespace PenLabelerAssist;

internal static class PatchDropdownChanged
{
	private static void Postfix(LabelerElement __instance)
	{
		try
		{
			Assist.OnDropdownChanged(__instance);
		}
		catch (Exception ex)
		{
			Log.Warn("dropdown changed hook failed: " + ex.Message);
		}
	}
}
