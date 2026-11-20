using System;
using UnityEngine;

namespace PenLabelerAssist;

internal static class PatchWindowAltBoxSelectMove
{
	private static void Prefix(ItemMultiSelectHandler __instance, out bool __state)
	{
		__state = false;
		try
		{
			if (WindowAltBoxSelect.ShouldBypassMove(__instance))
			{
				StoreUIManager instance = StoreUIManager.Instance;
				if (!((Object)(object)instance == (Object)null) && instance.hoveringStoreUI)
				{
					instance.hoveringStoreUI = false;
					__state = true;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("alt box select move failed: " + ex.Message);
		}
	}

	private static void Postfix(bool __state)
	{
		if (__state)
		{
			WindowAltBoxSelect.RestoreUi();
		}
	}

	private static Exception Finalizer(Exception __exception, bool __state)
	{
		if (__state)
		{
			WindowAltBoxSelect.RestoreUi();
		}
		return __exception;
	}
}
