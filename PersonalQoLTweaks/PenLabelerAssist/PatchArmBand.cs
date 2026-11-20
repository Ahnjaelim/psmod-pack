using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class PatchArmBand
{
	private static void Prefix(ItemMultiSelectHandler __instance, ISet<KeyCode> keysPressed)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!((Object)(object)__instance == (Object)null) && keysPressed != null && keysPressed.Contains(__instance.key) && Time.frameCount != Assist.ArmedFrame)
			{
				Assist.ArmForBand();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("arm band failed: " + ex.Message);
		}
	}
}
