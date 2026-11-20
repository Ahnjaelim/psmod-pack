namespace PenLabelerAssist;

internal static class PatchWaterLabelSync
{
	private static void Prefix(GameItem gameItem, out int __state)
	{
		__state = WaterLabelSync.Prepare(gameItem);
	}

	private static void Postfix(GameItem gameItem, int __state)
	{
		WaterLabelSync.Apply(gameItem, __state);
	}
}
