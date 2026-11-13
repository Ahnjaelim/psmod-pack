using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class PatchMultiSelectToggleDrag
{
	private static bool PressPrefix(ISet<KeyCode> keysPressed)
	{
		try
		{
			if (keysPressed == null)
			{
				return true;
			}
			if (!MultiSelectCompat.ShouldToggleItem())
			{
				return true;
			}
			return false;
		}
		catch
		{
			return true;
		}
	}

	private static bool MovePrefix()
	{
		try
		{
			if (!MultiSelectCompat.IsToggleDown())
			{
				return true;
			}
			return !MultiSelectCompat.HasSelection();
		}
		catch
		{
			return true;
		}
	}
}
