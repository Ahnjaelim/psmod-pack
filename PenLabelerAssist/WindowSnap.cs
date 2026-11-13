using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class WindowSnap
{
	private struct ScreenRect
	{
		public float Left;

		public float Right;

		public float Bottom;

		public float Top;
	}

	private const float SnapDistance = 16f;

	private static readonly Dictionary<PixelWindow, RectTransform> GeometryCache = new Dictionary<PixelWindow, RectTransform>();

	private static readonly Vector3[] Corners = (Vector3[])(object)new Vector3[4];

	public static void Reset()
	{
		GeometryCache.Clear();
	}

	public static void OnFixBounds(PixelWindow window)
	{
		if (!ModSettings.WindowSnap || !CanCheck(window) || !Input.GetMouseButton(0))
		{
			return;
		}
		try
		{
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			if ((Object)(object)current != (Object)null && current.IsDraggingItem)
			{
				return;
			}
		}
		catch
		{
		}
		if (QoLKeyBindings.IsSnapModifierDown() && TryGetScreenRect(window, out var rect) && FindSnap(window, rect, out var dx, out var dy))
		{
			Log.Debug("window snap: " + SafeName(window));
			ApplyDelta(window, dx, dy);
		}
	}

	private static bool CanCheck(PixelWindow window)
	{
		try
		{
			return window != null && window.IsVisible() && window.hasTitle && (Object)(object)window.rectTransform != (Object)null;
		}
		catch
		{
			return false;
		}
	}

	private static bool FindSnap(PixelWindow dragged, ScreenRect self, out float dx, out float dy)
	{
		dx = 0f;
		dy = 0f;
		float best = float.MaxValue;
		float best2 = float.MaxValue;
		try
		{
			WindowsHandler current = WindowsHandler.current;
			if ((Object)(object)current == (Object)null || current.visibleWindows == null)
			{
				return false;
			}
			List<PixelWindow> visibleWindows = current.visibleWindows;
			for (int i = 0; i < visibleWindows.Count; i++)
			{
				PixelWindow val = visibleWindows[i];
				if (CanTarget(dragged, val) && TryGetScreenRect(val, out var rect))
				{
					bool num = self.Bottom <= rect.Top + 16f && self.Top >= rect.Bottom - 16f;
					bool flag = self.Left <= rect.Right + 16f && self.Right >= rect.Left - 16f;
					if (num)
					{
						Consider(rect.Right - self.Left, ref best);
						Consider(rect.Left - self.Right, ref best);
						Consider(rect.Left - self.Left, ref best);
						Consider(rect.Right - self.Right, ref best);
					}
					if (flag)
					{
						Consider(rect.Top - self.Bottom, ref best2);
						Consider(rect.Bottom - self.Top, ref best2);
						Consider(rect.Top - self.Top, ref best2);
						Consider(rect.Bottom - self.Bottom, ref best2);
					}
				}
			}
		}
		catch
		{
			return false;
		}
		bool result = best != float.MaxValue || best2 != float.MaxValue;
		dx = ((best == float.MaxValue) ? 0f : best);
		dy = ((best2 == float.MaxValue) ? 0f : best2);
		return result;
	}

	private static void Consider(float candidate, ref float best)
	{
		if (Mathf.Abs(candidate) <= 16f && Mathf.Abs(candidate) < Mathf.Abs(best))
		{
			best = candidate;
		}
	}

	private static bool CanTarget(PixelWindow dragged, PixelWindow other)
	{
		if (other == null || other == dragged)
		{
			return false;
		}
		try
		{
			return other.IsVisible() && other.hasTitle && (Object)(object)other.rectTransform != (Object)null;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryGetScreenRect(PixelWindow window, out ScreenRect rect)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		rect = default(ScreenRect);
		try
		{
			if (window == null || (Object)(object)window.rectTransform == (Object)null)
			{
				return false;
			}
			RectTransform value = window.rectTransform;
			Rect rect2 = value.rect;
			if (!(rect2.width <= 0f))
			{
				rect2 = value.rect;
				if (!(rect2.height <= 0f))
				{
					goto IL_0087;
				}
			}
			if (!GeometryCache.TryGetValue(window, out value) || (Object)(object)value == (Object)null)
			{
				value = FindLargestRect(window);
				if ((Object)(object)value != (Object)null)
				{
					GeometryCache[window] = value;
				}
			}
			goto IL_0087;
			IL_0087:
			if ((Object)(object)value == (Object)null)
			{
				return false;
			}
			Vector3[] corners = Corners;
			value.GetWorldCorners(corners);
			Camera camera = GetCamera(window);
			float num = float.MaxValue;
			float num2 = float.MinValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			for (int i = 0; i < corners.Length; i++)
			{
				Vector2 val = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
				num = Mathf.Min(num, val.x);
				num2 = Mathf.Max(num2, val.x);
				num3 = Mathf.Min(num3, val.y);
				num4 = Mathf.Max(num4, val.y);
			}
			if (num2 <= num || num4 <= num3)
			{
				return false;
			}
			rect.Left = num;
			rect.Right = num2;
			rect.Bottom = num3;
			rect.Top = num4;
			return true;
		}
		catch (Exception ex)
		{
			Log.Debug("window snap rect failed: " + ex.Message);
			return false;
		}
	}

	private static RectTransform FindLargestRect(PixelWindow window)
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (window == null || (Object)(object)window.handler == (Object)null)
			{
				return null;
			}
			RectTransform[] componentsInChildren = ((Component)window.handler).gameObject.GetComponentsInChildren<RectTransform>(true);
			RectTransform result = null;
			float num = 0f;
			foreach (RectTransform val in componentsInChildren)
			{
				if (!((Object)(object)val == (Object)null) && !((Object)(object)val == (Object)(object)window.handler.rectTransform))
				{
					Rect rect = val.rect;
					float width = rect.width;
					rect = val.rect;
					float num2 = width * rect.height;
					if (num2 > num)
					{
						num = num2;
						result = val;
					}
				}
			}
			return result;
		}
		catch
		{
			return null;
		}
	}

	private static string SafeName(PixelWindow window)
	{
		try
		{
			return window.titleString ?? "?";
		}
		catch
		{
			return "?";
		}
	}

	private static Camera GetCamera(PixelWindow window)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Canvas componentInParent = ((Component)window.handler).GetComponentInParent<Canvas>(true);
			if ((Object)(object)componentInParent != (Object)null && (int)componentInParent.renderMode != 0)
			{
				return componentInParent.worldCamera;
			}
		}
		catch
		{
		}
		return null;
	}

	private static void ApplyDelta(PixelWindow window, float dx, float dy)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (window != null && !((Object)(object)window.rectTransform == (Object)null))
			{
				Transform parent = ((Transform)window.rectTransform).parent;
				RectTransform val = (RectTransform)(object)((parent is RectTransform) ? parent : null);
				if (!((Object)(object)val == (Object)null))
				{
					Vector3 val2 = ((Transform)val).InverseTransformVector(new Vector3(dx, dy, 0f));
					RectTransform rectTransform = window.rectTransform;
					rectTransform.anchoredPosition += new Vector2(val2.x, val2.y);
				}
			}
		}
		catch
		{
		}
	}
}
