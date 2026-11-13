using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class PatchMultiSelectQuickTransfer
{
	private static bool ShowcasePrefix(ItemQuickTransferAltHandler __instance, ISet<KeyCode> keysPressed)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)__instance == (Object)null))
		{
			return !MultiSelectCompat.IsQuickTransferConflict(__instance.quickTransferKey);
		}
		return true;
	}

	private static bool StoragePrefix(ItemQuickTransferAltHandler1 __instance, ISet<KeyCode> keysPressed)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)__instance == (Object)null))
		{
			return !MultiSelectCompat.IsQuickTransferConflict(__instance.quickTransferKey);
		}
		return true;
	}

	private static bool InventoryPrefix(ItemQuickTransferHandler __instance, ISet<KeyCode> keysPressed)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)__instance == (Object)null))
		{
			return !MultiSelectCompat.IsQuickTransferConflict(__instance.quickTransferKey);
		}
		return true;
	}
}
