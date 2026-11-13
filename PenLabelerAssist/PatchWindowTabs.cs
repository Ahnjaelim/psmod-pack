using System;

namespace PenLabelerAssist;

internal static class PatchWindowTabs
{
	private static void ShowPostfix(PixelWindow __instance)
	{
		try
		{
			TabSwitcher.RememberFromWindow(__instance);
			WindowFollow.OnWindowShown(__instance);
		}
		catch (Exception ex)
		{
			Log.Warn("tab switch show hook failed: " + ex.Message);
		}
	}

	private static void ToFrontPostfix(PixelWindow __instance)
	{
		try
		{
			TabSwitcher.RememberFromWindow(__instance);
		}
		catch (Exception ex)
		{
			Log.Warn("tab switch front hook failed: " + ex.Message);
		}
	}
}
