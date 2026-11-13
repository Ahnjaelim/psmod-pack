using System;
using UnityEngine;

namespace PenLabelerAssist;

internal static class DragSlotReplace
{
	private static bool _swapping;

	private static bool _active;

	private static GameItem _dragged;

	private static int _draggedKind;

	private const int KindNone = 0;

	private const int KindBattery = 1;

	private const int KindFilter = 2;

	public static bool IsActive => _active;

	public static void SetActive(bool active)
	{
		_active = active;
		_dragged = null;
		_draggedKind = 0;
	}

	public static bool IsSingleDragging()
	{
		try
		{
			return (Object)(object)ItemMouseDragHandler.current != (Object)null && ItemMouseDragHandler.current.IsDraggingItem;
		}
		catch
		{
			return false;
		}
	}

	public static bool CanReplace(GameSlotInventory slot, GameItem dragged)
	{
		if (!_active)
		{
			return false;
		}
		if (_swapping)
		{
			return false;
		}
		if (!ModSettings.DragSlotReplace)
		{
			return false;
		}
		if (slot == null || dragged == null)
		{
			return false;
		}
		if (!IsSingleDragging())
		{
			return false;
		}
		if (_dragged != dragged || _draggedKind == 0)
		{
			_dragged = dragged;
			_draggedKind = KindOf(dragged);
		}
		if (_draggedKind == 0)
		{
			return false;
		}
		if (((GameInventory)slot).IsInsertLocked() || ((GameInventory)slot).IsRemoveLocked())
		{
			return false;
		}
		GameItem childItem = slot.childItem;
		if (childItem == null || childItem == dragged)
		{
			return false;
		}
		if (childItem.unitCount != 1 || dragged.unitCount != 1)
		{
			return false;
		}
		if (_draggedKind == 1 && !IsBattery(childItem))
		{
			return false;
		}
		if (_draggedKind == 2 && !IsFilter(childItem))
		{
			return false;
		}
		if (ItemPinHelper.IsPinned(childItem) || ItemPinHelper.IsPinned(dragged))
		{
			return false;
		}
		if (!childItem.MayRemove() || childItem.MaxNumRemove() < 1)
		{
			return false;
		}
		if (!GeneralHelper.IsItemEffectivelyOwned(dragged))
		{
			return false;
		}
		if (!GeneralHelper.IsItemEffectivelyOwned(childItem))
		{
			return false;
		}
		GameItem parentItem = ((GameInventory)slot).GetParentItem();
		if (parentItem == null)
		{
			return false;
		}
		try
		{
			if (_draggedKind == 1)
			{
				return MachineHelper.GetBatterySlot(parentItem) == slot;
			}
			if (_draggedKind == 2)
			{
				return parentItem.IsTag("FILTER_CONTAINER_TAG");
			}
		}
		catch
		{
		}
		return false;
	}

	public static bool CanSwap(GameSlotInventory slot, GameItem dragged)
	{
		if (!CanReplace(slot, dragged))
		{
			return false;
		}
		return CanReturnToTargetShape((slot != null) ? slot.childItem : null, dragged);
	}

	private static bool CanReturnToTargetShape(GameItem old, GameItem dragged)
	{
		try
		{
			GridShape val = ((old != null) ? old.modifiedShape : null);
			GridShape val2 = ((dragged != null) ? dragged.modifiedShape : null);
			if (val == null || val2 == null)
			{
				return false;
			}
			return val.globalWidth <= val2.globalWidth && val.globalHeight <= val2.globalHeight;
		}
		catch
		{
			return false;
		}
	}

	private static int KindOf(GameItem item)
	{
		if (IsBattery(item))
		{
			return 1;
		}
		if (IsFilter(item))
		{
			return 2;
		}
		return 0;
	}

	public static void BeginSwap()
	{
		_swapping = true;
	}

	public static void EndSwap()
	{
		_swapping = false;
	}

	public static SlotMarker MakeMarker(GameSlotInventory slot, GameItem dragged, int amount)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		return new SlotMarker
		{
			item = dragged,
			inventory = (GameInventory)(object)slot,
			itemGridShape = ((dragged != null) ? dragged.modifiedShape : null),
			numTransfer = Math.Min(1, Math.Max(0, amount))
		};
	}

	private static bool IsBattery(GameItem item)
	{
		try
		{
			if (item == null || !item.IsTag("power_source_item"))
			{
				return false;
			}
			if (CurrentConverterHelper.IsCurrentConverter(item))
			{
				return false;
			}
			if (HypertechBatteryHelper.IsHypertechBattery(item))
			{
				return false;
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsFilter(GameItem item)
	{
		try
		{
			return item != null && item.IsTag("WATER_FILTER") && item.identifier != "uv_filter";
		}
		catch
		{
			return false;
		}
	}
}
