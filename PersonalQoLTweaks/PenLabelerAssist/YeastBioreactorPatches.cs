namespace PenLabelerAssist;

internal static class YeastBioreactorPatches
{
	public static bool MayTargetPrefix(GameItem __instance, GameItem targetItem, ref bool __result)
	{
		if (!YeastBioreactor.CanUseOn(__instance, targetItem))
		{
			return true;
		}
		__result = true;
		return false;
	}

	public static bool CanTargetPrefix(GameItem __instance, GameItem targetItem, ref bool __result)
	{
		if (!YeastBioreactor.CanUseOn(__instance, targetItem))
		{
			return true;
		}
		__result = true;
		return false;
	}

	public static bool TargetPrefix(GameItem __instance, GameItem targetItem)
	{
		return !YeastBioreactor.TryUse(__instance, targetItem);
	}
}
