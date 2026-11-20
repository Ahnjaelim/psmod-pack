namespace PenLabelerAssist;

internal static class PatchWindowAltBoxSelectWindowDrag
{
	private static bool Prefix(TreeNodeRender __instance)
	{
		try
		{
			return !WindowAltBoxSelect.ShouldBlockWindowDrag(__instance);
		}
		catch
		{
			return true;
		}
	}
}
