using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class PatchWindowAltBoxSelectPress
{
	private const int PressNone = 0;

	private const int PressRestoreUi = 1;

	private const int PressUiAlreadyClear = 2;

	private static void Prefix(ItemMultiSelectHandler __instance, ISet<KeyCode> keysPressed, out int __state)
	{
		__state = 0;
		try
		{
			StoreUIManager instance = StoreUIManager.Instance;
			if (!((Object)(object)instance == (Object)null) && WindowAltBoxSelect.CanStart(__instance, keysPressed))
			{
				Assist.MarkQolBandStart();
				WindowAltBoxSelect.SetDragSurfaceBypass(value: true);
				if (instance.hoveringStoreUI)
				{
					instance.hoveringStoreUI = false;
					__state = 1;
				}
				else
				{
					__state = 2;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("alt box select press failed: " + ex.Message);
		}
	}

	private static void Postfix(ItemMultiSelectHandler __instance, int __state)
	{
		if (__state == 0)
		{
			return;
		}
		try
		{
			RestoreIfNeeded(__state);
			WindowAltBoxSelect.MarkActive(__instance);
			WindowAltBoxSelect.SetDragSurfaceBypass(value: false);
		}
		catch (Exception ex)
		{
			Log.Warn("alt box select press postfix failed: " + ex.Message);
		}
	}

	private static Exception Finalizer(Exception __exception, int __state)
	{
		if (__state != 0)
		{
			RestoreIfNeeded(__state);
			WindowAltBoxSelect.SetDragSurfaceBypass(value: false);
		}
		return __exception;
	}

	private static void RestoreIfNeeded(int state)
	{
		if (state == 1)
		{
			WindowAltBoxSelect.RestoreUi();
		}
	}
}
