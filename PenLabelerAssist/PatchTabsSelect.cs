using System;

namespace PenLabelerAssist;

internal static class PatchTabsSelect
{
	private static void Postfix(MachineryTabs __instance, bool __1)
	{
		try
		{
			if (__1)
			{
				TabSwitcher.Remember(__instance);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("tab switch remember failed: " + ex.Message);
		}
	}
}
