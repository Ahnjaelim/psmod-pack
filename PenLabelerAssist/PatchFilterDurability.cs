namespace PenLabelerAssist;

internal static class PatchFilterDurability
{
	private static void Prefix(GameItem filter, out int __state)
	{
		__state = FilterDurabilityWarning.Prepare(filter);
	}

	private static void Postfix(GameItem filter, int __state)
	{
		FilterDurabilityWarning.Apply(filter, __state);
	}
}
