using System;

namespace PenLabelerAssist;

internal static class PatchFinalizeBand
{
	private static void Postfix()
	{
		try
		{
			Assist.OnBandFinalized();
		}
		catch (Exception ex)
		{
			Log.Warn("finalize band failed: " + ex.Message);
		}
	}
}
