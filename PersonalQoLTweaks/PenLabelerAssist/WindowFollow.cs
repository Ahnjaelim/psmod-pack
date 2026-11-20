using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class WindowFollow
{
	private sealed class DragRecord
	{
		public bool FollowRequested;

		public readonly List<PixelWindow> Windows = new List<PixelWindow>();
	}

	private static readonly HashSet<PixelWindow> Logged = new HashSet<PixelWindow>();

	private static readonly HashSet<PixelWindow> OpenWindows = new HashSet<PixelWindow>();

	private static readonly Dictionary<GameItem, DragRecord> DragHidden = new Dictionary<GameItem, DragRecord>();

	public static void Reset()
	{
		Logged.Clear();
		OpenWindows.Clear();
		DragHidden.Clear();
	}

	public static void OnWindowShown(PixelWindow window)
	{
		if (window != null)
		{
			OpenWindows.Add(window);
		}
	}

	public static void OnWindowHidden(PixelWindow window)
	{
		if (!ModSettings.WindowFollow)
		{
			return;
		}
		try
		{
			if (window == null || !OpenWindows.Remove(window))
			{
				return;
			}
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			if ((Object)(object)current == (Object)null)
			{
				return;
			}
			GameItem currentItem = current.GetCurrentItem();
			if (currentItem == null)
			{
				return;
			}
			GameItem val = null;
			if (window.parentItems != null && window.parentItems.Count > 0)
			{
				val = window.parentItems[0];
			}
			if (val != null && val == currentItem)
			{
				if (!DragHidden.TryGetValue(currentItem, out var value))
				{
					value = new DragRecord();
					DragHidden[currentItem] = value;
				}
				value.FollowRequested |= QoLKeyBindings.IsFollowModifierDown();
				if (!value.Windows.Contains(window))
				{
					value.Windows.Add(window);
				}
			}
		}
		catch
		{
		}
	}

	public static void OnDragEnd(GameItem item)
	{
		if (item == null)
		{
			return;
		}
		if (!ModSettings.WindowFollow)
		{
			try
			{
				DragHidden.Remove(item);
				return;
			}
			catch
			{
				return;
			}
		}
		DragRecord value;
		try
		{
			DragHidden.TryGetValue(item, out value);
			DragHidden.Remove(item);
		}
		catch
		{
			return;
		}
		if (value == null || (!value.FollowRequested && !QoLKeyBindings.IsFollowModifierDown()))
		{
			return;
		}
		if (!IsItemVisible(item))
		{
			for (int i = 0; i < value.Windows.Count; i++)
			{
				CloseWindow(item, value.Windows[i]);
			}
		}
		else
		{
			for (int j = 0; j < value.Windows.Count; j++)
			{
				Reopen(value.Windows[j]);
			}
		}
	}

	private static void Reopen(PixelWindow window)
	{
		if (window == null)
		{
			return;
		}
		try
		{
			if (!window.IsVisible())
			{
				window.Show();
			}
			FollowWindow(window);
		}
		catch (Exception ex)
		{
			Log.Warn("window follow reopen failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private static void CloseWindow(GameItem item, PixelWindow window)
	{
		try
		{
			if (item != null && window != null && item.contentWindow == window)
			{
				item.CloseContentWindow(false);
			}
			else if (window != null)
			{
				window.Hide();
			}
		}
		catch
		{
			try
			{
				if (window != null)
				{
					window.Hide();
				}
			}
			catch
			{
			}
		}
	}

	private static void FollowWindow(PixelWindow window)
	{
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		if (window == null || !window.IsVisible())
		{
			return;
		}
		GameItem val = null;
		try
		{
			if (window.parentItems != null && window.parentItems.Count > 0)
			{
				val = window.parentItems[0];
			}
		}
		catch
		{
		}
		if (val == null || !IsItemVisible(val))
		{
			return;
		}
		GameItemElement val2 = (GameItemElement)(object)((val is GameItemElement) ? val : null);
		if (val2 == null || (Object)(object)val2.middle == (Object)null || !((Component)val2.handler).gameObject.activeInHierarchy || (Object)(object)window.rectTransform == (Object)null || (Object)(object)((Transform)window.rectTransform).parent == (Object)null)
		{
			return;
		}
		Transform parent = ((Transform)window.rectTransform).parent;
		RectTransform val3 = (RectTransform)(object)((parent is RectTransform) ? parent : null);
		if (!((Object)(object)val3 == (Object)null) && GetItemScreenRect(val2, val3, out var min, out var max))
		{
			float num = window.widthPixels;
			float num2 = max.x + 1f;
			float num3 = num2 + num;
			Rect rect = val3.rect;
			if (num3 > rect.xMax)
			{
				num2 = min.x - num - 1f;
			}
			float num4 = num2;
			rect = val3.rect;
			if (num4 < rect.xMin)
			{
				rect = val3.rect;
				num2 = rect.xMin;
			}
			float num5 = num2 + num;
			rect = val3.rect;
			if (num5 > rect.xMax)
			{
				rect = val3.rect;
				num2 = rect.xMax - num;
			}
			window.rectTransform.anchoredPosition = new Vector2(num2, max.y);
			window.FixBounds();
			if (Logged.Add(window))
			{
				Log.Debug("window follow: " + val.identifier);
			}
		}
	}

	private static bool IsItemVisible(GameItem item)
	{
		try
		{
			GameItemElement val = (GameItemElement)(object)((item is GameItemElement) ? item : null);
			return val != null && (Object)(object)val.handler != (Object)null && ((Component)val.handler).gameObject.activeInHierarchy;
		}
		catch
		{
			return false;
		}
	}

	private static bool GetItemScreenRect(GameItemElement element, RectTransform parent, out Vector2 min, out Vector2 max)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		min = Vector2.zero;
		max = Vector2.zero;
		try
		{
			Vector3[] array = (Vector3[])(object)new Vector3[4];
			element.middle.rectTransform.GetWorldCorners(array);
			Vector2 val2 = default(Vector2);
			for (int i = 0; i < array.Length; i++)
			{
				Vector2 val = RectTransformUtility.WorldToScreenPoint((Camera)null, array[i]);
				if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, val, (Camera)null, out val2))
				{
					return false;
				}
				if (i == 0)
				{
					min = val2;
					max = val2;
				}
				else
				{
					min = Vector2.Min(min, val2);
					max = Vector2.Max(max, val2);
				}
			}
			return true;
		}
		catch
		{
			return false;
		}
	}
}
