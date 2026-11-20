using System;

namespace PenLabelerAssist;

internal static class PatchWindowSnap
{
	private static void FixBoundsPostfix(PixelWindow __instance)
	{
		try
		{
			WindowSnap.OnFixBounds(__instance);
		}
		catch (Exception ex)
		{
			Log.Debug("window snap bounds hook failed: " + ex.Message);
		}
	}
}
