using System;

namespace PenLabelerAssist;

internal static class PatchDiscardOnTableAdd
{
	private static void Postfix(GameItem __0)
	{
		try
		{
			AutoDiscard.Consider(__0);
		}
		catch (Exception ex)
		{
			Log.Warn("auto-discard table hook failed: " + ex.Message);
		}
	}
}
