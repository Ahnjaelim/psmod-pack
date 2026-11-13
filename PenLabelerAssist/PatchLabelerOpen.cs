using System;

namespace PenLabelerAssist;

internal static class PatchLabelerOpen
{
	private static void Prefix(LabelerUIManager __instance, GameItem gameItem)
	{
		try
		{
			Patches.EnsureDropdownPatch();
			Assist.OnPanelOpening(__instance, gameItem);
		}
		catch (Exception ex)
		{
			Log.Warn("labeler open failed: " + ex.Message);
		}
	}

	private static void Postfix(LabelerUIManager __instance, GameItem gameItem)
	{
		try
		{
			Assist.OnPanelOpened(__instance, gameItem);
		}
		catch (Exception ex)
		{
			Log.Warn("labeler opened failed: " + ex.Message);
		}
	}
}
