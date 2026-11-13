using System;
using UnityEngine;

namespace PenLabelerAssist;

internal static class PatchWindowFollow
{
	private static void HidePostfix(PixelWindow __instance)
	{
		try
		{
			WindowFollow.OnWindowHidden(__instance);
		}
		catch (Exception ex)
		{
			Log.Warn("window follow hide hook failed: " + ex.Message);
		}
	}

	private static void EndDragPostfix(ItemMouseDragHandler __instance)
	{
		try
		{
			if (!((Object)(object)__instance == (Object)null))
			{
				WindowFollow.OnDragEnd(__instance.GetCurrentItem());
			}
		}
		catch (Exception ex)
		{
			Log.Warn("window follow drag end hook failed: " + ex.Message);
		}
	}

	private static void RestorePositionPostfix(GameItem __instance)
	{
		try
		{
			if (__instance != null)
			{
				WindowFollow.OnWindowShown(__instance.contentWindow);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("window follow restore hook failed: " + ex.Message);
		}
	}
}
