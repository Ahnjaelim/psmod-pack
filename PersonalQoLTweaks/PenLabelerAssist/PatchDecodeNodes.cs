using System;
using System.Collections.Generic;

namespace PenLabelerAssist;

internal static class PatchDecodeNodes
{
	private static void Prefix(List<SaveItemNode> __0)
	{
		try
		{
			SaveSanitizer.Filter(__0);
		}
		catch (Exception ex)
		{
			Log.Warn("save sanitize failed: " + ex.Message);
		}
	}
}
