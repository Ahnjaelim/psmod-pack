namespace PenLabelerAssist;

internal static class PatchQuickActions
{
	private static bool Prefix()
	{
		return QuickActions.Prefix();
	}
}
