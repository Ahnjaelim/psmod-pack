namespace PenLabelerAssist;

internal static class PatchWindowAltBoxSelectDragSurface
{
	private static bool Prefix(ref bool __result)
	{
		if (!WindowAltBoxSelect.BypassDragSurface)
		{
			return true;
		}
		__result = false;
		return false;
	}
}
