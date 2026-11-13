using System;

namespace PenLabelerAssist;

internal static class PatchHydroponicReplant
{
	private static void Postfix(GameItem machine)
	{
		try
		{
			HydroponicAutoReplant.OnGrowCycleEnd(machine);
		}
		catch (Exception ex)
		{
			Log.Debug("hydroponic replant hook failed: " + ex.Message);
		}
	}
}
