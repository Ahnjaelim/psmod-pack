using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class PatchMultiSelectToolBlock
{
	private static bool Prefix(ItemSelectHandler __instance, ISet<KeyCode> keysPressed)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if ((Object)(object)__instance == (Object)null || keysPressed == null)
			{
				return true;
			}
			if (!keysPressed.Contains(__instance.key))
			{
				return true;
			}
			if (ItemPinHelper.BlocksClicks)
			{
				return true;
			}
			return !MultiSelectCompat.ShouldToggleItem();
		}
		catch
		{
			return true;
		}
	}
}
