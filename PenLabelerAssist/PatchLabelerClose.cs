using System;

namespace PenLabelerAssist;

internal static class PatchLabelerClose
{
	private static void Prefix(LabelerUIManager __instance)
	{
		try
		{
			Assist.OnPanelClosing(__instance);
		}
		catch (Exception ex)
		{
			Log.Warn("labeler close failed: " + ex.Message);
		}
	}
}
