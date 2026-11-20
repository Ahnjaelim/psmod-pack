using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class MachineBatteryAutoSwap
{
	private sealed class BatteryCheck
	{
		public GameSlotInventory Slot;

		public bool Exhausted;
	}

	private sealed class BatteryPool
	{
		public readonly List<GameItem> Items = new List<GameItem>();

		public bool Scanned;

		public float ScannedAt;
	}

	private const float PoolLifetime = 2f;

	private static bool _swapping;

	private static GameItem _redirectOld;

	private static GameItem _redirectNew;

	private static int _redirectFrame = -1;

	private static readonly Dictionary<GameItem, BatteryCheck> Checks = new Dictionary<GameItem, BatteryCheck>();

	private static readonly Dictionary<GraphNodeStorage, BatteryPool> Pools = new Dictionary<GraphNodeStorage, BatteryPool>();

	private static readonly HashSet<GameItem> Seen = new HashSet<GameItem>();

	public static void Reset()
	{
		_swapping = false;
		_redirectOld = null;
		_redirectNew = null;
		_redirectFrame = -1;
		Checks.Clear();
		Pools.Clear();
		Seen.Clear();
	}

	public static bool Prefix(GameItem gameItem, int amount, ref bool __result)
	{
		if (!ModSettings.MachineAutoBattery)
		{
			return true;
		}
		if (_swapping || gameItem == null || amount <= 0)
		{
			return true;
		}
		if (TryUseRedirect(gameItem, amount, ref __result))
		{
			return false;
		}
		if (!gameItem.IsTag("power_source_item"))
		{
			return true;
		}
		if (IsPinnedSafe(gameItem))
		{
			return true;
		}
		if (IsNonSwappablePowerSource(gameItem))
		{
			return true;
		}
		GameInventory parentInventory = gameItem.parentInventory;
		GameSlotInventory slot = (GameSlotInventory)(object)((parentInventory is GameSlotInventory) ? parentInventory : null);
		if (!IsBatterySlot(slot, gameItem))
		{
			return true;
		}
		BatteryCheck check = GetCheck(gameItem, slot);
		if (CanDrawActual(gameItem, amount))
		{
			return true;
		}
		if (check.Exhausted)
		{
			return true;
		}
		if (!TrySwap(gameItem, slot, amount, out var replacement))
		{
			check.Exhausted = true;
			return true;
		}
		check.Exhausted = false;
		_redirectOld = gameItem;
		_redirectNew = replacement;
		_redirectFrame = Time.frameCount;
		_swapping = true;
		try
		{
			__result = PowerHelper.DrawPowerSource(replacement, amount);
		}
		finally
		{
			_swapping = false;
		}
		Log.Debug("battery swap: replaced " + SafeName(gameItem) + " -> " + SafeName(replacement) + " result=" + __result);
		return false;
	}

	public static void CanDrawPostfix(GameItem gameItem, int amount, ref bool __result)
	{
		try
		{
			if (_swapping || gameItem == null || amount <= 0 || !ModSettings.MachineAutoBattery || !gameItem.IsTag("power_source_item") || IsPinnedSafe(gameItem) || IsNonSwappablePowerSource(gameItem))
			{
				return;
			}
			GameInventory parentInventory = gameItem.parentInventory;
			GameSlotInventory slot = (GameSlotInventory)(object)((parentInventory is GameSlotInventory) ? parentInventory : null);
			if (!IsBatterySlot(slot, gameItem))
			{
				return;
			}
			BatteryCheck check = GetCheck(gameItem, slot);
			if (__result || check.Exhausted)
			{
				return;
			}
			Log.Debug("battery swap: CanDraw first false id=" + gameItem.identifier + " amount=" + amount + " host=" + SafeName(slot));
			if (!TrySwap(gameItem, slot, amount, out var replacement))
			{
				check.Exhausted = true;
				__result = false;
				Log.Debug("battery swap: CanDraw no replacement; search exhausted");
				return;
			}
			check.Exhausted = false;
			_redirectOld = gameItem;
			_redirectNew = replacement;
			_redirectFrame = Time.frameCount;
			_swapping = true;
			try
			{
				__result = CanDrawActual(replacement, amount);
			}
			finally
			{
				_swapping = false;
			}
			Log.Debug("battery swap: CanDraw replaced " + SafeName(gameItem) + " -> " + SafeName(replacement) + " result=" + __result);
		}
		catch (Exception ex)
		{
			Log.Debug("battery swap: CanDraw postfix failed " + ex.Message);
		}
	}

	public static void DrawPostfix(GameItem gameItem, int amount, bool __result)
	{
		try
		{
			if (gameItem == null || amount <= 0 || !ModSettings.MachineAutoBattery || !gameItem.IsTag("power_source_item") || IsPinnedSafe(gameItem) || IsNonSwappablePowerSource(gameItem))
			{
				return;
			}
			GameInventory parentInventory = gameItem.parentInventory;
			GameSlotInventory slot = (GameSlotInventory)(object)((parentInventory is GameSlotInventory) ? parentInventory : null);
			if (IsBatterySlot(slot, gameItem))
			{
				BatteryCheck check = GetCheck(gameItem, slot);
				if (__result)
				{
					check.Exhausted = false;
				}
			}
		}
		catch
		{
		}
	}

	private static bool TryUseRedirect(GameItem gameItem, int amount, ref bool __result)
	{
		if (gameItem != _redirectOld || Time.frameCount != _redirectFrame)
		{
			return false;
		}
		GameItem redirectNew = _redirectNew;
		_redirectOld = null;
		_redirectNew = null;
		if (redirectNew == null)
		{
			return false;
		}
		_swapping = true;
		try
		{
			__result = PowerHelper.DrawPowerSource(redirectNew, amount);
		}
		finally
		{
			_swapping = false;
		}
		return true;
	}

	private static BatteryCheck GetCheck(GameItem battery, GameSlotInventory slot)
	{
		if (!Checks.TryGetValue(battery, out var value) || value.Slot != slot)
		{
			value = new BatteryCheck
			{
				Slot = slot
			};
			Checks[battery] = value;
		}
		return value;
	}

	private static bool CanDrawActual(GameItem item, int amount)
	{
		try
		{
			if (item == null)
			{
				return false;
			}
			if (CurrentConverterHelper.IsCurrentConverter(item))
			{
				return CurrentConverterHelper.CanSupply(item, amount);
			}
			if (HypertechBatteryHelper.IsHypertechBattery(item))
			{
				return HypertechBatteryHelper.CanSupply(item, amount);
			}
			if (!item.IsTag("power_source_item"))
			{
				return false;
			}
			return item.GetTagReadonly("power_source_item_energy").GetInt() - amount >= 0;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsBatterySlot(GameSlotInventory slot, GameItem battery)
	{
		if (slot == null || battery == null || slot.childItem != battery)
		{
			return false;
		}
		try
		{
			GameItem parentItem = ((GameInventory)slot).GetParentItem();
			if (parentItem == null)
			{
				return false;
			}
			if (MachineHelper.GetBatterySlot(parentItem) == slot)
			{
				return true;
			}
		}
		catch
		{
		}
		try
		{
			GameItem parentItem2 = ((GameInventory)slot).GetParentItem();
			return parentItem2 != null && parentItem2.contentWindow != null;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsPinnedSafe(GameItem item)
	{
		try
		{
			return item != null && ItemPinHelper.IsPinned(item);
		}
		catch
		{
			return false;
		}
	}

	private static bool IsNonSwappablePowerSource(GameItem item)
	{
		try
		{
			return item != null && (CurrentConverterHelper.IsCurrentConverter(item) || HypertechBatteryHelper.IsHypertechBattery(item));
		}
		catch
		{
			return false;
		}
	}

	private static bool IsInstalledBatterySlot(GameItem item)
	{
		GameInventory parentInventory = item.parentInventory;
		GameSlotInventory val = (GameSlotInventory)(object)((parentInventory is GameSlotInventory) ? parentInventory : null);
		if (val == null)
		{
			return false;
		}
		return IsBatterySlot(val, item);
	}

	private static bool IsLockedAway(GameItem item)
	{
		try
		{
			if (item == null)
			{
				return true;
			}
			if (item.IsTag("ITEM_BEHIND_LOCK_TAG"))
			{
				return true;
			}
			GameInventory parentInventory = item.parentInventory;
			int num = 0;
			while (parentInventory != null && num++ < 32)
			{
				if (parentInventory.IsRemoveLocked())
				{
					return true;
				}
				GameItem parentItem = parentInventory.GetParentItem();
				if (parentItem == null)
				{
					break;
				}
				if (parentItem.IsTag("LOCKED_CONTAINER_TAG"))
				{
					try
					{
						if (parentItem.GetTagReadonly("LOCK_STATUS").GetBool())
						{
							return true;
						}
					}
					catch
					{
					}
				}
				try
				{
					if (parentItem.contentWindow != null && parentItem.contentWindow.isLocked)
					{
						return true;
					}
				}
				catch
				{
				}
				parentInventory = parentItem.parentInventory;
			}
		}
		catch
		{
			return true;
		}
		return false;
	}

	private static bool CanSearchContainer(GameItem container)
	{
		try
		{
			if (container == null)
			{
				return false;
			}
			if (IsLockedAway(container))
			{
				return false;
			}
			if (!GeneralHelper.IsItemEffectivelyOwned(container))
			{
				return false;
			}
			if (container.IsTag("LOCKED_CONTAINER_TAG"))
			{
				try
				{
					if (container.GetTagReadonly("LOCK_STATUS").GetBool())
					{
						return false;
					}
				}
				catch
				{
				}
			}
			if (container.contentWindow != null && container.contentWindow.isLocked)
			{
				return false;
			}
		}
		catch
		{
			return false;
		}
		return true;
	}

	private static bool TrySwap(GameItem oldBattery, GameSlotInventory slot, int amount, out GameItem replacement)
	{
		replacement = null;
		if (_swapping)
		{
			return false;
		}
		_swapping = true;
		try
		{
			return TrySwapCore(oldBattery, slot, amount, out replacement);
		}
		finally
		{
			_swapping = false;
		}
	}

	private static bool TrySwapCore(GameItem oldBattery, GameSlotInventory slot, int amount, out GameItem replacement)
	{
		replacement = null;
		Seen.Clear();
		List<GraphNodeStorage> list = CandidateRoots();
		GraphNodeStorage val = FindPreferredRoot(slot, list);
		GameItem best = null;
		if (val != null)
		{
			FindBest(val, oldBattery, amount, ref best);
		}
		if (best == null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				GraphNodeStorage val2 = list[i];
				if (val2 != null && val2 != val)
				{
					FindBest(val2, oldBattery, amount, ref best);
					if (best != null)
					{
						break;
					}
				}
			}
		}
		if (best == null)
		{
			return false;
		}
		try
		{
			if (!GraphUtils.SwapItemsInPlace(oldBattery, best))
			{
				Log.Debug("battery swap: SwapItemsInPlace failed");
				return false;
			}
		}
		catch (Exception ex)
		{
			Log.Debug("battery swap: SwapItemsInPlace exception " + ex.Message);
			return false;
		}
		replacement = best;
		RemoveFromPools(best);
		Log.Debug("battery swap: swapped in place " + SafeName(oldBattery) + " <-> " + SafeName(best));
		return true;
	}

	private static void FindBest(GraphNodeStorage root, GameItem oldBattery, int amount, ref GameItem best)
	{
		if (!Pools.TryGetValue(root, out var value))
		{
			value = new BatteryPool();
			Pools[root] = value;
		}
		if (!value.Scanned || Time.unscaledTime - value.ScannedAt >= 2f)
		{
			ScanPool(root, value);
		}
		for (int i = 0; i < value.Items.Count; i++)
		{
			GameItem val = value.Items[i];
			if (!IsPoolCandidate(val, oldBattery))
			{
				value.Items.RemoveAt(i);
				i--;
			}
			else if (Seen.Add(val) && CanDrawActual(val, amount) && (best == null || PowerHelper.GetPowerSourceItemValue(val) > PowerHelper.GetPowerSourceItemValue(best)))
			{
				best = val;
			}
		}
	}

	private static void ScanPool(GraphNodeStorage root, BatteryPool pool)
	{
		pool.Items.Clear();
		pool.Scanned = true;
		pool.ScannedAt = Time.unscaledTime;
		List<GameItem> list;
		try
		{
			list = GraphUtils.FindAllChildrenType<GameItem>(root, (Func<GameItem, bool>)null, (Func<GameItem, bool>)CanSearchContainer);
		}
		catch
		{
			return;
		}
		HashSet<GameItem> hashSet = new HashSet<GameItem>();
		for (int i = 0; i < list.Count; i++)
		{
			GameItem val = list[i];
			if (val != null && hashSet.Add(val) && IsPoolCandidate(val, null))
			{
				pool.Items.Add(val);
			}
		}
	}

	private static bool IsPoolCandidate(GameItem item, GameItem oldBattery)
	{
		try
		{
			if (item == null || item == oldBattery)
			{
				return false;
			}
			if (!item.IsTag("power_source_item"))
			{
				return false;
			}
			if (IsLockedAway(item))
			{
				return false;
			}
			if (IsPinnedSafe(item))
			{
				return false;
			}
			if (IsNonSwappablePowerSource(item))
			{
				return false;
			}
			if (IsInstalledBatterySlot(item))
			{
				return false;
			}
			if (!GeneralHelper.IsItemEffectivelyOwned(item))
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

	private static void RemoveFromPools(GameItem item)
	{
		if (item == null)
		{
			return;
		}
		foreach (KeyValuePair<GraphNodeStorage, BatteryPool> pool in Pools)
		{
			pool.Value?.Items.Remove(item);
		}
	}

	private static GraphNodeStorage FindPreferredRoot(GameSlotInventory slot, List<GraphNodeStorage> roots)
	{
		try
		{
			for (int i = 0; i < roots.Count; i++)
			{
				GraphNodeStorage val = roots[i];
				if (val != null && GraphUtils.FindParentEquals((GraphNodeStorage)(object)slot, val))
				{
					return val;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static List<GraphNodeStorage> CandidateRoots()
	{
		List<GraphNodeStorage> list = new List<GraphNodeStorage>();
		try
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				return list;
			}
			AddIfNotNull(list, (GraphNodeStorage)(object)instance.invElement);
			AddIfNotNull(list, (GraphNodeStorage)(object)instance.frontInvinvElement);
			AddIfNotNull(list, (GraphNodeStorage)(object)instance.backInvinvElement);
			AddIfNotNull(list, (GraphNodeStorage)(object)instance.hirelingInv);
		}
		catch
		{
		}
		return list;
	}

	private static void AddIfNotNull(List<GraphNodeStorage> roots, GraphNodeStorage root)
	{
		if (root != null)
		{
			roots.Add(root);
		}
	}

	private static string SafeName(GameSlotInventory slot)
	{
		try
		{
			if (slot == null)
			{
				return "null";
			}
			GameItem parentItem = ((GameInventory)slot).GetParentItem();
			return (parentItem != null) ? parentItem.identifier : "slot-no-host";
		}
		catch
		{
			return "slot-error";
		}
	}

	private static string SafeName(GameItem item)
	{
		try
		{
			return (item != null) ? (item.identifier + "#" + item.uniqueId) : "null";
		}
		catch
		{
			return "item-error";
		}
	}
}
