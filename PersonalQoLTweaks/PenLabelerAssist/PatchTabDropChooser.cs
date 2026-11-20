using System;
using UnityEngine;

namespace PenLabelerAssist;

internal static class PatchTabDropChooser
{
	public static void DragPostfix()
	{
		try
		{
			if (TabDropChooser.Enabled)
			{
				TabDropChooser.OnDragFrame();
			}
		}
		catch (Exception ex)
		{
			Log.Debug("tab chooser drag hook failed: " + ex.Message);
		}
	}

	public static void EndDragPostfix()
	{
		try
		{
			TabDropChooser.Hide();
			TabDropChooser.ClearCachedContainers();
		}
		catch
		{
		}
	}

	public static bool FindDropPrefix(Vector2 screenPosition, ref GameInventory __result)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!TabDropChooser.TryGetDropInventory(screenPosition, out var inventory))
			{
				return true;
			}
			__result = inventory;
			return false;
		}
		catch
		{
			return true;
		}
	}

	public static bool MayHavePrefix(GameItem __instance, GameItem item, ref bool __result)
	{
		try
		{
			if (!TabDropChooser.TryGetItemDropInventory(__instance, out var inventory))
			{
				return true;
			}
			__result = inventory != null && inventory.MayHaveValidInventorySlot(item);
			return false;
		}
		catch
		{
			return true;
		}
	}

	public static bool FindOneSlotPrefix(GameItem __instance, GameItem item, ref SlotMarker __result)
	{
		try
		{
			if (!TabDropChooser.TryGetItemDropInventory(__instance, out var inventory))
			{
				return true;
			}
			__result = ((inventory != null) ? inventory.TryFindOneValidInventorySlot(item) : null);
			return false;
		}
		catch
		{
			return true;
		}
	}
}
