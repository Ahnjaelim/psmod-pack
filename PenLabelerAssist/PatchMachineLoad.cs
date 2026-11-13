namespace PenLabelerAssist;

internal static class PatchMachineLoad
{
	private static bool Prefix(GameItem __0)
	{
		try
		{
			if (__0 == null)
			{
				return false;
			}
			if (__0.contentWindow == null)
			{
				if (__0.IsTag("STANDARD_MACHINE_TAG"))
				{
					Log.Warn("skip LoadUpgrade without contentWindow: " + (string.IsNullOrEmpty(__0.identifier) ? "?" : __0.identifier));
					return false;
				}
				return true;
			}
			return true;
		}
		catch
		{
			return false;
		}
	}
}
