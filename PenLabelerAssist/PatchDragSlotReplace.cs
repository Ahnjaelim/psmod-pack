using System;
using UnityEngine;

namespace PenLabelerAssist;

internal static class PatchDragSlotReplace
{
	public static void StartPostfix()
	{
		try
		{
			if (!ModSettings.DragSlotReplace)
			{
				DragSlotReplace.SetActive(active: false);
			}
			else
			{
				DragSlotReplace.SetActive((Object)(object)ItemMouseDragHandler.current != (Object)null && ItemMouseDragHandler.current.IsDraggingItem);
			}
		}
		catch
		{
			DragSlotReplace.SetActive(active: false);
		}
	}

	public static void EndPostfix()
	{
		DragSlotReplace.SetActive(active: false);
	}

	public static bool TryInventorySlotPrefix(GameSlotInventory __instance, GameItem item, int requestedNum, ref SlotMarker __result)
	{
		try
		{
			if (!DragSlotReplace.CanReplace(__instance, item))
			{
				return true;
			}
			__result = DragSlotReplace.MakeMarker(__instance, item, requestedNum);
			return false;
		}
		catch
		{
			return true;
		}
	}

	public static bool TryFindOneSlotPrefix(GameSlotInventory __instance, GameItem item, ref SlotMarker __result)
	{
		try
		{
			if (!DragSlotReplace.CanReplace(__instance, item))
			{
				return true;
			}
			__result = DragSlotReplace.MakeMarker(__instance, item, 1);
			return false;
		}
		catch
		{
			return true;
		}
	}

	public static bool TryAcceptOncePrefix(SlotMarker __instance, int amount, ref int __result)
	{
		try
		{
			if (!DragSlotReplace.IsActive)
			{
				return true;
			}
			if (!ModSettings.DragSlotReplace)
			{
				return true;
			}
			if (__instance == null)
			{
				return true;
			}
			GameInventory inventory = __instance.inventory;
			GameSlotInventory val = (GameSlotInventory)(object)((inventory is GameSlotInventory) ? inventory : null);
			GameItem item = __instance.item;
			if (!DragSlotReplace.CanReplace(val, item))
			{
				return true;
			}
			if (!DragSlotReplace.CanSwap(val, item))
			{
				__result = 0;
				return false;
			}
			GameItem childItem = val.childItem;
			DragSlotReplace.BeginSwap();
			bool flag;
			try
			{
				flag = GraphUtils.SwapItemsInPlace(childItem, item);
			}
			finally
			{
				DragSlotReplace.EndSwap();
			}
			if (!flag)
			{
				Log.Debug("drag slot replace: swap failed " + ((childItem != null) ? childItem.identifier : "?") + " <-> " + ((item != null) ? item.identifier : "?"));
				__result = 0;
				return false;
			}
			__result = 1;
			Log.Debug("drag slot replace: " + childItem.identifier + " <-> " + item.identifier);
			return false;
		}
		catch (Exception ex)
		{
			Log.Debug("drag slot replace failed: " + ex.Message);
			return true;
		}
	}
}
