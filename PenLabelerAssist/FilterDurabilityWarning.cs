using System;

namespace PenLabelerAssist;

internal static class FilterDurabilityWarning
{
	public const int InvalidDurability = -1;

	public static int Prepare(GameItem filter)
	{
		try
		{
			if (!ModSettings.FilterDurabilityWarning)
			{
				return -1;
			}
			if (!IsDurableFilter(filter))
			{
				return -1;
			}
			return DurabilityHelper.GetRealDurability(filter);
		}
		catch
		{
			return -1;
		}
	}

	public static void Apply(GameItem filter, int before)
	{
		try
		{
			if (ModSettings.FilterDurabilityWarning && before > 0 && IsDurableFilter(filter) && DurabilityHelper.GetRealDurability(filter) <= 0)
			{
				GameInventory parentInventory = filter.parentInventory;
				GameItem val = ((parentInventory != null) ? parentInventory.GetParentItem() : null);
				if (val != null && val.IsTag("FILTER_CONTAINER_TAG"))
				{
					FilterDurabilityPop.Show(Text.Get("filter_durability_low"), filter, val);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Debug("filter durability warning failed: " + ex.Message);
		}
	}

	private static bool IsDurableFilter(GameItem filter)
	{
		try
		{
			return filter != null && filter.IsTag("WATER_FILTER") && filter.identifier != "uv_filter";
		}
		catch
		{
			return false;
		}
	}
}
