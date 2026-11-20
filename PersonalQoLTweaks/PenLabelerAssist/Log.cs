namespace PenLabelerAssist;

internal static class Log
{
	private static ModLog _log;

	public static void Init(ModManifest manifest)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Expected O, but got Unknown
		try
		{
			if (manifest != null)
			{
				_log = new ModLog(manifest);
			}
		}
		catch
		{
		}
	}

	public static void Info(object message)
	{
		try
		{
			if (_log != null)
			{
				_log.Log(message);
			}
		}
		catch
		{
		}
	}

	public static void Debug(object message)
	{
		try
		{
			if (ModSettings.DebugEnabled && _log != null)
			{
				_log.Log(message);
			}
		}
		catch
		{
		}
	}

	public static void Warn(object message)
	{
		try
		{
			if (_log != null)
			{
				_log.Warning(message);
			}
		}
		catch
		{
		}
	}

	public static void Error(object message)
	{
		try
		{
			if (_log != null)
			{
				_log.Error(message);
			}
		}
		catch
		{
		}
	}
}
