using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PenLabelerAssist;

internal static class WindowAltBoxSelect
{
	private static bool _bypassDragSurface;

	public static bool Active { get; private set; }

	public static bool BypassDragSurface => _bypassDragSurface;

	public static bool CanStart(ItemMultiSelectHandler handler, ISet<KeyCode> keysPressed)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!ModSettings.WindowAltBoxSelect)
			{
				return false;
			}
			if ((Object)(object)handler == (Object)null || keysPressed == null)
			{
				return false;
			}
			if (MultiSelectCompat.HasSelection())
			{
				return false;
			}
			if (!keysPressed.Contains(handler.key))
			{
				return false;
			}
			if (ItemPinHelper.BlocksClicks)
			{
				return false;
			}
			if (UIDragHandler.isDraggingAnyUI)
			{
				return false;
			}
			if (!QoLKeyBindings.IsBoxSelectModifierDown())
			{
				return false;
			}
			if (QoLKeyBindings.SnapConflictsWithBoxSelect())
			{
				return false;
			}
			if (QoLKeyBindings.IsSnapModifierDown())
			{
				return false;
			}
			Vector2 screenPoint = (Vector2)(Input.mousePosition);
			if (!IsPointerInsideWindow(screenPoint))
			{
				return false;
			}
			return !IsPointerOverInteractive(screenPoint);
		}
		catch
		{
			return false;
		}
	}

	public static void SetDragSurfaceBypass(bool value)
	{
		_bypassDragSurface = value;
	}

	public static void MarkActive(ItemMultiSelectHandler handler)
	{
		Active = (Object)(object)handler != (Object)null && handler.HasSelection;
	}

	public static bool ShouldBypassMove(ItemMultiSelectHandler handler)
	{
		if (!Active)
		{
			return false;
		}
		if ((Object)(object)handler == (Object)null || !handler.HasSelection)
		{
			Active = false;
			return false;
		}
		if ((Object)(object)StoreUIManager.Instance != (Object)null)
		{
			return StoreUIManager.Instance.hoveringStoreUI;
		}
		return false;
	}

	public static bool ShouldBlockWindowDrag(TreeNodeRender render)
	{
		if (!Active || (Object)(object)render == (Object)null)
		{
			return false;
		}
		try
		{
			if (render.FindParentElement<GameItemElement>() != null)
			{
				return false;
			}
			if (render.FindParentElement<GameInventory>() != null)
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

	public static void RestoreUi()
	{
		try
		{
			if ((Object)(object)StoreUIManager.Instance != (Object)null)
			{
				StoreUIManager.Instance.hoveringStoreUI = true;
			}
		}
		catch
		{
		}
	}

	public static void Clear()
	{
		Active = false;
		_bypassDragSurface = false;
	}

	private static bool IsPointerInsideWindow(Vector2 screenPoint)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			return RenderHandler.RaycastFindParentElement<PixelWindow>(screenPoint, (TreeNodeRender)null) != null;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsPointerOverInteractive(Vector2 screenPoint)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			EventSystem current = EventSystem.current;
			if ((Object)(object)current == (Object)null)
			{
				return false;
			}
			PointerEventData val = new PointerEventData(current)
			{
				position = screenPoint
			};
			List<RaycastResult> list = new List<RaycastResult>();
			current.RaycastAll(val, list);
			if (list.Count == 0)
			{
				return false;
			}
			RaycastResult val2 = list[0];
			GameObject gameObject = val2.gameObject;
			if ((Object)(object)gameObject == (Object)null)
			{
				return false;
			}
			if ((Object)(object)gameObject.GetComponentInParent<UIDragHandler>() != (Object)null)
			{
				return true;
			}
			if ((Object)(object)gameObject.GetComponentInParent<Selectable>() != (Object)null)
			{
				return true;
			}
			TreeNodeRender component = gameObject.GetComponent<TreeNodeRender>();
			if ((Object)(object)component == (Object)null)
			{
				return false;
			}
			PixelElement val3 = component.FindParentElement();
			if (val3 is GameItemElement)
			{
				return true;
			}
			if (val3 is SelectableElement)
			{
				return true;
			}
			if (val3 is MachinerySlider)
			{
				return true;
			}
			if (component.eventHandler == null)
			{
				return false;
			}
			if (component.eventHandler.mouseClickFunc.Count > 0 || component.eventHandler.mouseUpFunc.Count > 0)
			{
				return true;
			}
			if (val3 == null)
			{
				return false;
			}
			if (!(val3 is PixelWindow) && (component.eventHandler.mouseBeginDragFunc.Count > 0 || component.eventHandler.mouseDragFunc.Count > 0))
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}
}
