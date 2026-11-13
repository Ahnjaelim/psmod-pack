namespace PenLabelerAssist;

public static class BatchToolCompat
{
	public const string BoxSelectExcludeTag = "PEN_MOD_QOL_BOXSELECT_EXCLUDE";

	public static void ExcludeFromBoxSelect(GameItem item)
	{
		try
		{
			if (item != null)
			{
				item.EnableTag("PEN_MOD_QOL_BOXSELECT_EXCLUDE", false);
			}
		}
		catch
		{
		}
	}

	public static bool IsExcludedFromBoxSelect(GameItem item)
	{
		try
		{
			return item != null && item.IsTag("PEN_MOD_QOL_BOXSELECT_EXCLUDE");
		}
		catch
		{
			return false;
		}
	}
}
