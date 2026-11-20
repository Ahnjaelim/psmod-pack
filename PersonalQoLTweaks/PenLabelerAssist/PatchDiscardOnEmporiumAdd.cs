using System;

namespace PenLabelerAssist;

internal static class PatchDiscardOnEmporiumAdd
{
	private static void Postfix(GameItem __1)
	{
		try
		{
			AutoDiscard.Consider(__1);
		}
		catch (Exception ex)
		{
			Log.Warn("auto-discard emporium hook failed: " + ex.Message);
		}
	}
}
