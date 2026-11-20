using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class ItemPinShortcut
{
	private static bool _enabled;

	public static bool Enabled => _enabled;

	public static void SetEnabled(bool enabled)
	{
		_enabled = enabled;
	}

	public static void Postfix()
	{
		try
		{
			if (_enabled)
			{
				Handle();
			}
		}
		catch (Exception ex)
		{
			Log.Debug("pin shortcut failed: " + ex.Message);
		}
	}

	private static void Handle()
	{
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		if (!ModSettings.QuickPin || !KeyBindHelper.GetKeyDown("pen_qol_quick_pin") || PlayerStore.Instance == null)
		{
			return;
		}
		StoreUIManager instance = StoreUIManager.Instance;
		if ((Object)(object)instance == (Object)null || instance.IsTyping() || ItemPinHelper.BlocksClicks || ItemPinHelper.IsPinMode || ((Object)(object)ItemMouseDragHandler.current != (Object)null && ItemMouseDragHandler.current.IsDraggingItem))
		{
			return;
		}
		List<GameItemElement> selected = Selection.GetSelected();
		if (selected.Count > 0)
		{
			ToggleBatch(selected);
		}
		else if (!instance.hoveringStoreUI)
		{
			GameItem val = RenderHandler.RaycastElement<GameItem>((Vector2)(Input.mousePosition), (TreeNodeRender)null);
			if (val != null && ItemPinHelper.MayPin(val))
			{
				bool flag = ItemPinHelper.TogglePinned(val);
				Log.Debug("pin shortcut: " + val.identifier + (flag ? " pinned" : " unpinned"));
			}
		}
	}

	private static void ToggleBatch(List<GameItemElement> selected)
	{
		bool flag = false;
		bool flag2 = false;
		for (int i = 0; i < selected.Count; i++)
		{
			GameItem val = (GameItem)(object)selected[i];
			if (val != null && ItemPinHelper.MayPin(val))
			{
				flag = true;
				if (!ItemPinHelper.IsPinned(val))
				{
					flag2 = true;
					break;
				}
			}
		}
		if (!flag)
		{
			return;
		}
		bool flag3 = flag2;
		int num = 0;
		for (int j = 0; j < selected.Count; j++)
		{
			GameItem val2 = (GameItem)(object)selected[j];
			if (val2 != null && ItemPinHelper.MayPin(val2) && ItemPinHelper.IsPinned(val2) != flag3)
			{
				ItemPinHelper.SetPinned(val2, flag3);
				num++;
			}
		}
		Log.Debug("pin shortcut: batch " + (flag3 ? "pin " : "unpin ") + num);
	}
}
