using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class PatchMultiSelectToggle
{
	private static bool Prefix(ItemMultiSelectHandler __instance, ISet<KeyCode> keysPressed)
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
			if (!MultiSelectCompat.IsToggleDown())
			{
				return true;
			}
			if (!MultiSelectCompat.HasSelection())
			{
				return true;
			}
			GameItemElement val = MultiSelectCompat.ItemUnderPointer();
			if (val == null)
			{
				return true;
			}
			bool num = Selection.ToggleShiftClick(__instance, val);
			if (num)
			{
				Assist.MarkQolSelectionOwned();
			}
			return !num;
		}
		catch (Exception ex)
		{
			Log.Warn("multi-select toggle hook failed: " + ex.Message);
			return true;
		}
	}
}
