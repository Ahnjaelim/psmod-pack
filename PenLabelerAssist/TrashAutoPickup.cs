using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class TrashAutoPickup
{
	public static void Scan()
	{
		if (!ModSettings.TrashAutoPickup)
		{
			return;
		}
		try
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				return;
			}
			GameInventory val = ResolveTrash(instance);
			if (val != null)
			{
				List<GameItem> list = new List<GameItem>(val.childItems);
				Log.Debug("trash auto-pickup: scanning " + list.Count + " item(s)");
				for (int i = 0; i < list.Count; i++)
				{
					Consider(list[i]);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("trash auto-pickup scan failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	public static void ScanOnLoad()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		try
		{
			PlayerStore instance = PlayerStore.Instance;
			if (instance != null && (int)instance.storeState == 2)
			{
				Scan();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("trash auto-pickup load scan failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	public static void Consider(GameItem item)
	{
		if (!ModSettings.TrashAutoPickup || SaveManager.IsDecoding)
		{
			return;
		}
		try
		{
			if (item == null || item.parentInventory == null)
			{
				return;
			}
			EmporiumEntry instance = EmporiumEntry.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				return;
			}
			GameInventory val = ResolveTrash(instance);
			if (val == null || item.parentInventory != val)
			{
				return;
			}
			string text = SafeId(item);
			if (text == "newspaper" || AutoDiscard.IsDocumentItem(item) || GeneralHelper.IsHazardousWaste(item) || IsKeptInTrash(item) || (ModSettings.TrashBlackMarketOnly && !BlackMarketFilter.Contains(text)))
			{
				return;
			}
			GameInventory target = null;
			string targetName = null;
			if (!TryPickTarget((GameInventory)(object)instance.backInvinvElement, item, "back", out target, out targetName))
			{
				ShowPickupFailed(item, instance);
				Log.Debug("trash auto-pickup skipped (back counter full): " + text);
				return;
			}
			bool flag = false;
			try
			{
				flag = val.Expel(item);
			}
			catch (Exception ex)
			{
				Log.Warn("trash auto-pickup expel warning: " + ex.GetType().Name + ": " + ex.Message);
			}
			bool flag2;
			try
			{
				flag2 = item.parentInventory == null;
			}
			catch
			{
				flag2 = true;
			}
			if (!flag && !flag2)
			{
				Log.Warn("trash auto-pickup failed (cannot remove from trash): " + text);
				return;
			}
			int num = -1;
			try
			{
				num = GraphUtils.TryAcceptOnce((GraphNodeStorage)(object)target, item, -1);
			}
			catch (Exception ex2)
			{
				Log.Warn("trash auto-pickup accept warning: " + ex2.GetType().Name + ": " + ex2.Message);
			}
			bool flag3 = false;
			try
			{
				flag3 = item.parentInventory == target;
			}
			catch
			{
			}
			if (flag3 || num == 0)
			{
				Log.Debug("trash auto-pickup: " + text + " -> " + targetName);
				return;
			}
			try
			{
				if (val != null && item != null)
				{
					val.UncheckedAccept(item);
				}
			}
			catch (Exception ex3)
			{
				Log.Warn("trash auto-pickup rollback warning: " + ex3.GetType().Name + ": " + ex3.Message);
			}
			ShowPickupFailed(item, instance);
			Log.Warn("trash auto-pickup rolled back: " + text);
		}
		catch (Exception ex4)
		{
			Log.Warn("trash auto-pickup failed: " + ex4.GetType().Name + ": " + ex4.Message);
		}
	}

	private static bool TryPickTarget(GameInventory candidate, GameItem item, string name, out GameInventory target, out string targetName)
	{
		target = null;
		targetName = null;
		if (candidate == null)
		{
			return false;
		}
		try
		{
			SlotMarker val = candidate.TryFindOneValidInventorySlot(item);
			if (val == null || val.numTransfer <= 0)
			{
				return false;
			}
			if (val.numTransfer < item.unitCount)
			{
				return false;
			}
		}
		catch
		{
			return false;
		}
		target = candidate;
		targetName = name;
		return true;
	}

	private static void ShowPickupFailed(GameItem item, EmporiumEntry store)
	{
		try
		{
			GameItem owner = (((Object)(object)store != (Object)null) ? store.trashcan : null);
			FilterDurabilityPop.Show(Text.Get("trash_pickup_failed"), item, owner, requireWarningSetting: false);
		}
		catch (Exception ex)
		{
			Log.Debug("trash pickup failed popup error: " + ex.Message);
		}
	}

	private static GameInventory ResolveTrash(EmporiumEntry store)
	{
		try
		{
			GameItem trashcan = store.trashcan;
			if (trashcan != null && trashcan.contentWindow != null)
			{
				GraphNodeStorage child = trashcan.contentWindow.child;
				GameInventory val = (GameInventory)(object)((child is GameInventory) ? child : null);
				if (val != null)
				{
					return val;
				}
			}
		}
		catch
		{
		}
		try
		{
			return (GameInventory)(object)store.trashcanInvElement;
		}
		catch
		{
			return null;
		}
	}

	private static bool IsKeptInTrash(GameItem item)
	{
		try
		{
			if (item == null)
			{
				return false;
			}
			if (item.IsID("smuggler_bay_mod"))
			{
				return true;
			}
			return GraphUtils.FindParentType<GameItem>((GraphNodeStorage)(object)item, (Func<GameItem, bool>)((GameItem parent) => parent.IsID("smuggler_bay_mod")), (Func<GameItem, bool>)null) != null;
		}
		catch
		{
			return true;
		}
	}

	private static string SafeId(GameItem item)
	{
		try
		{
			return (item != null && item.identifier != null) ? item.identifier : "?";
		}
		catch
		{
			return "?";
		}
	}
}
