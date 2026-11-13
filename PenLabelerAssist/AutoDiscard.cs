using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class AutoDiscard
{
	private enum Kind
	{
		None,
		Appraisal,
		EventReceipt,
		SecurityReceipt,
		Handwritten,
		ExpeditionLog
	}

	public static void ScanExisting()
	{
		if (!ModSettings.AnyAutoDiscardEnabled || SaveManager.IsDecoding)
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
			GameGridInventory frontInvinvElement = instance.frontInvinvElement;
			if (frontInvinvElement == null)
			{
				return;
			}
			List<GameItem> list = new List<GameItem>(((GameInventory)frontInvinvElement).childItems);
			if (list.Count != 0)
			{
				List<GameItem> list2 = new List<GameItem>(list);
				for (int i = 0; i < list2.Count; i++)
				{
					Consider(list2[i]);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("auto-discard scan failed: " + ex.Message);
		}
	}

	public static void Consider(GameItem item)
	{
		if (!ModSettings.AnyAutoDiscardEnabled || SaveManager.IsDecoding)
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
			if ((Object)(object)instance == (Object)null || (object)item.parentInventory != instance.frontInvinvElement || !GeneralHelper.IsItemOwned(item))
			{
				return;
			}
			GameInventory val = null;
			try
			{
				GameItem trashcan = instance.trashcan;
				if (trashcan != null && trashcan.contentWindow != null)
				{
					GraphNodeStorage child = trashcan.contentWindow.child;
					val = (GameInventory)(object)((child is GameInventory) ? child : null);
				}
			}
			catch
			{
			}
			if (val == null)
			{
				val = (GameInventory)(object)instance.trashcanInvElement;
			}
			if (val != null && item.parentInventory == val)
			{
				return;
			}
			Kind kind = Classify(item);
			if (kind == Kind.None || !Enabled(kind) || (kind == Kind.Handwritten && (item.IsTag("MACHINE_MANUAL_TAG") || item.parentInventory is GameSlotInventory)))
			{
				return;
			}
			string text = SafeId(item);
			if (val == null)
			{
				Log.Warn("auto-discard failed (trash unavailable): " + kind.ToString() + " " + text);
				return;
			}
			GameInventory parentInventory = item.parentInventory;
			if (parentInventory == null)
			{
				Log.Warn("auto-discard failed (item detached): " + kind.ToString() + " " + text);
				return;
			}
			bool flag = false;
			try
			{
				SlotMarker val2 = val.TryFindOneValidInventorySlot(item);
				flag = val2 != null && val2.numTransfer > 0;
			}
			catch (Exception ex)
			{
				Log.Warn("auto-discard trash check warning: " + ex.GetType().Name + ": " + ex.Message);
			}
			if (!flag)
			{
				Log.Debug("auto-discard skipped (trash full): " + kind.ToString() + " " + text);
				return;
			}
			bool flag2 = false;
			try
			{
				flag2 = parentInventory.Expel(item);
			}
			catch (Exception ex2)
			{
				Log.Warn("auto-discard expel warning: " + ex2.GetType().Name + ": " + ex2.Message);
			}
			bool flag3;
			try
			{
				flag3 = item.parentInventory == null;
			}
			catch
			{
				flag3 = true;
			}
			if (!flag2 && !flag3)
			{
				Log.Warn("auto-discard failed (cannot remove from source): " + kind.ToString() + " " + text);
				return;
			}
			bool flag4 = false;
			int num = -1;
			try
			{
				num = GraphUtils.TryAcceptAll((GraphNodeStorage)(object)val, item, -1);
				try
				{
					flag4 = item != null && item.parentInventory == val;
				}
				catch
				{
					flag4 = false;
				}
			}
			catch (Exception ex3)
			{
				Log.Warn("auto-discard trash accept warning: " + ex3.GetType().Name + ": " + ex3.Message);
			}
			if (flag4 || num == 0)
			{
				Log.Debug("auto-discard: " + kind.ToString() + " " + text);
				return;
			}
			try
			{
				if (parentInventory != null && item != null)
				{
					parentInventory.UncheckedAccept(item);
				}
			}
			catch (Exception ex4)
			{
				Log.Warn("auto-discard rollback warning: " + ex4.GetType().Name + ": " + ex4.Message);
			}
			Log.Warn("auto-discard rolled back (trash rejected): " + kind.ToString() + " " + text);
		}
		catch (Exception ex5)
		{
			Log.Warn("auto-discard failed: " + ex5.GetType().Name + ": " + ex5.Message);
		}
	}

	public static bool IsDocumentItem(GameItem item)
	{
		if (item != null)
		{
			return Classify(item) != Kind.None;
		}
		return false;
	}

	private static bool Enabled(Kind kind)
	{
		return kind switch
		{
			Kind.Appraisal => ModSettings.DiscardAppraisal, 
			Kind.EventReceipt => ModSettings.DiscardEventReceipt, 
			Kind.SecurityReceipt => ModSettings.DiscardSecurityReceipt, 
			Kind.Handwritten => ModSettings.DiscardHandwritten, 
			Kind.ExpeditionLog => ModSettings.DiscardExpeditionLog, 
			_ => false, 
		};
	}

	private static Kind Classify(GameItem item)
	{
		string text = SafeId(item);
		switch (text)
		{
		case "expedition_log":
			return Kind.ExpeditionLog;
		case "sec_slip":
			return Kind.SecurityReceipt;
		case "postit":
			return Kind.Handwritten;
		default:
		{
			if (text != "slip")
			{
				return Kind.None;
			}
			string a = null;
			try
			{
				a = item.name;
			}
			catch
			{
			}
			string slipTitle = GetSlipTitle(item);
			string b = LocalizedMechanic("appraisal_slip_name");
			string b2 = LocalizedMechanic("mech_intel_slip_name");
			string b3 = LocalizedMechanic("mech_intel_slip_title");
			if (EqualsText(a, b))
			{
				return Kind.Appraisal;
			}
			if (EqualsText(a, b2) || EqualsText(slipTitle, b3))
			{
				return Kind.EventReceipt;
			}
			return Kind.None;
		}
		}
	}

	private static string GetSlipTitle(GameItem item)
	{
		try
		{
			if (!item.IsTag("SLIP_TITLE"))
			{
				return null;
			}
			return item.GetTagReadonly("SLIP_TITLE").GetString();
		}
		catch
		{
			return null;
		}
	}

	private static string LocalizedMechanic(string key)
	{
		try
		{
			return LocHelper.GetLocalizedMechanic(key, Array.Empty<object>());
		}
		catch
		{
			return null;
		}
	}

	private static bool EqualsText(string a, string b)
	{
		if (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b))
		{
			return string.Equals(a, b, StringComparison.Ordinal);
		}
		return false;
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
