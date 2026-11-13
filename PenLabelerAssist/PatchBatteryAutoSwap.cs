namespace PenLabelerAssist;

internal static class PatchBatteryAutoSwap
{
	private static bool Prefix(GameItem gameItem, int amount, ref bool __result)
	{
		return MachineBatteryAutoSwap.Prefix(gameItem, amount, ref __result);
	}

	private static void CanDrawPostfix(GameItem gameItem, int amount, ref bool __result)
	{
		MachineBatteryAutoSwap.CanDrawPostfix(gameItem, amount, ref __result);
	}

	private static void DrawPostfix(GameItem gameItem, int amount, bool __result)
	{
		MachineBatteryAutoSwap.DrawPostfix(gameItem, amount, __result);
	}
}
