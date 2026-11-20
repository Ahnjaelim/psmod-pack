using System;

namespace PenLabelerAssist;

internal static class PatchBlueLabeler
{
	private static void OpenPrefix(GameItem gameItem)
	{
		try
		{
			Assist.OnBluePanelOpening(gameItem);
		}
		catch (Exception ex)
		{
			Log.Warn("blue labeler open failed: " + ex.Message);
		}
	}

	private static void OpenPostfix(LabelerBlueUIManager __instance, GameItem gameItem)
	{
		try
		{
			Assist.OnBluePanelOpened(__instance, gameItem);
		}
		catch (Exception ex)
		{
			Log.Warn("blue labeler prefill failed: " + ex.Message);
		}
	}

	private static void EditEndPostfix(LabelerBlueUIManager __instance)
	{
		try
		{
			Assist.OnBlueEditEnd(__instance);
		}
		catch (Exception ex)
		{
			Log.Warn("blue labeler edit failed: " + ex.Message);
		}
	}

	private static void ClosePrefix()
	{
		try
		{
			Assist.OnBlueClosing();
		}
		catch (Exception ex)
		{
			Log.Warn("blue labeler close failed: " + ex.Message);
		}
	}

	private static void ClearPostfix(LabelerBlueUIManager __instance)
	{
		try
		{
			Assist.OnBlueClear(__instance);
		}
		catch (Exception ex)
		{
			Log.Warn("blue labeler clear failed: " + ex.Message);
		}
	}

	private static void ExternalOpenPrefix()
	{
		try
		{
			Assist.OnBlueExternalOpen();
		}
		catch (Exception ex)
		{
			Log.Warn("blue labeler external open failed: " + ex.Message);
		}
	}
}
