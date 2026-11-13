using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PenLabelerAssist;

internal static class TabDropChooser
{
	private sealed class CachedContainer
	{
		public MachineryTabs Tabs;

		public GameItem Owner;

		public RectTransform[] Rects;
	}

	private const float MinWidth = 48f;

	private const float MaxWidth = 240f;

	private const float MaxMultiWidth = 360f;

	private const float MinRowHeight = 14f;

	private const float MinFontSize = 5f;

	private const float MaxFontSize = 11f;

	private const float MaxPanelHeight = 560f;

	private static float _width = 160f;

	private static float _height = 120f;

	private static float _padding = 6f;

	private static int _columns = 1;

	private static float _cellWidth = 148f;

	private static float _cellHeight = 24f;

	private static float _gap = 2f;

	private static MachineryTabs _tabs;

	private static GameItem _ownerItem;

	private static TreeNodeRender _root;

	private static readonly List<TreeNodeRender> RowNodes = new List<TreeNodeRender>();

	private static int _hover = -1;

	private static bool _loggedNoTabs;

	private static bool _enabled;

	private static PointerEventData _raycastEventData;

	private static EventSystem _raycastEventSystem;

	private static readonly List<RaycastResult> RaycastResults = new List<RaycastResult>();

	private static readonly List<CachedContainer> CachedContainers = new List<CachedContainer>();

	public static bool Enabled => _enabled;

	public static void SetEnabled(bool enabled)
	{
		_enabled = enabled;
		if (!enabled)
		{
			Hide();
			ClearCachedContainers();
		}
	}

	public unsafe static void OnDragFrame()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!_enabled || !IsDraggingAny())
			{
				Hide();
				return;
			}
			Vector2 val = (Vector2)(Input.mousePosition);
			if ((Object)(object)_root != (Object)null && IsInsideRoot(val))
			{
				UpdateHover(val);
				return;
			}
			CachedContainer cachedContainer = FindCachedContainer(val);
			if (cachedContainer != null)
			{
				if (cachedContainer.Tabs != _tabs || cachedContainer.Owner != _ownerItem || (Object)(object)_root == (Object)null)
				{
					Show(cachedContainer.Tabs, cachedContainer.Owner);
				}
				UpdateHover(val);
				return;
			}
			GameItem ownerItem;
			MachineryTabs val2 = FindTabsAt(val, out ownerItem);
			if (val2 == null)
			{
				if (!_loggedNoTabs)
				{
					_loggedNoTabs = true;
					Vector2 val3 = val;
					Log.Debug("tab chooser: no multi-tab container at " + ((object)(*(Vector2*)(&val3))/*cast due to .constrained prefix*/).ToString());
				}
				Hide();
			}
			else
			{
				_loggedNoTabs = false;
				RememberContainer(val2, ownerItem);
				if (val2 != _tabs || ownerItem != _ownerItem || (Object)(object)_root == (Object)null)
				{
					Show(val2, ownerItem);
				}
				UpdateHover(val);
			}
		}
		catch (Exception ex)
		{
			Log.Debug("tab chooser update failed: " + ex.Message);
			Hide();
		}
	}

	public static bool TryGetDropInventory(Vector2 screenPosition, out GameInventory inventory)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		inventory = null;
		try
		{
			if (!_enabled)
			{
				return false;
			}
			if ((Object)(object)_root == (Object)null || _tabs == null)
			{
				return false;
			}
			if (!IsDraggingAny())
			{
				return false;
			}
			if (!IsInsideRoot(screenPosition))
			{
				return false;
			}
			int num = HitRow(screenPosition);
			if (num < 0)
			{
				num = _hover;
			}
			if (num < 0 || num >= _tabs.count)
			{
				return false;
			}
			inventory = _tabs.GetTabInventory(num);
			if (inventory != null)
			{
				Log.Debug("tab chooser: drop tab=" + num + " inventory=" + ((object)inventory).GetType().Name);
			}
			return inventory != null;
		}
		catch
		{
			return false;
		}
	}

	public static bool TryGetItemDropInventory(GameItem target, out GameInventory inventory)
	{
		inventory = null;
		try
		{
			if (!_enabled)
			{
				return false;
			}
			if (target == null || _ownerItem != target || _tabs == null || (Object)(object)_root == (Object)null)
			{
				return false;
			}
			if (_hover < 0 || _hover >= _tabs.count)
			{
				return false;
			}
			inventory = _tabs.GetTabInventory(_hover);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static void Hide()
	{
		if ((Object)(object)_root != (Object)null)
		{
			try
			{
				Object.Destroy((Object)(object)((Component)_root).gameObject);
			}
			catch
			{
			}
		}
		_root = null;
		_tabs = null;
		_ownerItem = null;
		_hover = -1;
		RowNodes.Clear();
	}

	private static bool IsDraggingAny()
	{
		bool num = (Object)(object)ItemMouseDragHandler.current != (Object)null && ItemMouseDragHandler.current.IsDraggingItem;
		bool flag = (Object)(object)ItemMultiSelectHandler.current != (Object)null && ItemMultiSelectHandler.current.IsGroupDragging;
		return num || flag;
	}

	private static CachedContainer FindCachedContainer(Vector2 screenPosition)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		for (int i = 0; i < CachedContainers.Count; i++)
		{
			CachedContainer cachedContainer = CachedContainers[i];
			if (cachedContainer == null || cachedContainer.Rects == null)
			{
				continue;
			}
			for (int j = 0; j < cachedContainer.Rects.Length; j++)
			{
				if (ContainsScreenPoint(cachedContainer.Rects[j], screenPosition))
				{
					return cachedContainer;
				}
			}
		}
		return null;
	}

	private static void RememberContainer(MachineryTabs tabs, GameItem owner)
	{
		if (tabs == null)
		{
			return;
		}
		CachedContainer cachedContainer = null;
		for (int i = 0; i < CachedContainers.Count; i++)
		{
			CachedContainer cachedContainer2 = CachedContainers[i];
			if (cachedContainer2 != null && cachedContainer2.Tabs == tabs && cachedContainer2.Owner == owner)
			{
				cachedContainer = cachedContainer2;
				break;
			}
		}
		if (cachedContainer == null)
		{
			cachedContainer = new CachedContainer();
			CachedContainers.Add(cachedContainer);
		}
		cachedContainer.Tabs = tabs;
		cachedContainer.Owner = owner;
		List<RectTransform> list = new List<RectTransform>();
		RectTransform val = FindTarget(tabs, owner);
		if ((Object)(object)val != (Object)null)
		{
			list.Add(val);
		}
		GameItemElement val2 = (GameItemElement)(object)((owner is GameItemElement) ? owner : null);
		if (val2 != null)
		{
			if ((Object)(object)val2.middle != (Object)null && (Object)(object)val2.middle.rectTransform != (Object)null)
			{
				list.Add(val2.middle.rectTransform);
			}
			if ((Object)(object)val2.handler != (Object)null)
			{
				list.Add(val2.handler.rectTransform);
			}
		}
		if ((Object)(object)tabs.handler != (Object)null)
		{
			list.Add(tabs.handler.rectTransform);
		}
		List<RectTransform> list2 = new List<RectTransform>();
		for (int j = 0; j < list.Count; j++)
		{
			if ((Object)(object)list[j] != (Object)null && !list2.Contains(list[j]))
			{
				list2.Add(list[j]);
			}
		}
		cachedContainer.Rects = list2.ToArray();
	}

	public static void ClearCachedContainers()
	{
		CachedContainers.Clear();
	}

	private static MachineryTabs FindTabsAt(Vector2 screenPosition, out GameItem ownerItem)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		ownerItem = null;
		return FindTabsByRaycast(screenPosition, out ownerItem);
	}

	private static MachineryTabs FindTabsByRaycast(Vector2 screenPosition, out GameItem ownerItem)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		ownerItem = null;
		EventSystem current = EventSystem.current;
		if ((Object)(object)current == (Object)null)
		{
			return null;
		}
		if (_raycastEventData == null || (Object)(object)_raycastEventSystem != (Object)(object)current)
		{
			_raycastEventSystem = current;
			_raycastEventData = new PointerEventData(current);
		}
		_raycastEventData.position = screenPosition;
		RaycastResults.Clear();
		current.RaycastAll(_raycastEventData, RaycastResults);
		for (int i = 0; i < RaycastResults.Count; i++)
		{
			RaycastResult val = RaycastResults[i];
			GameObject gameObject = val.gameObject;
			if ((Object)(object)gameObject == (Object)null)
			{
				continue;
			}
			TreeNodeRender component = gameObject.GetComponent<TreeNodeRender>();
			if ((Object)(object)component == (Object)null)
			{
				continue;
			}
			GameItem val2 = component.FindParentElement<GameItem>();
			if (val2 != null && TryResolveTabsForItem(val2, out var tabs))
			{
				ownerItem = val2;
				return tabs;
			}
			try
			{
				if (component.FindParentWindow() != null)
				{
					return null;
				}
			}
			catch
			{
			}
		}
		return null;
	}

	private static bool TryResolveTabsForItem(GameItem item, out MachineryTabs tabs)
	{
		tabs = null;
		try
		{
			PixelWindow val = ((item != null) ? item.contentWindow : null);
			if (val == null)
			{
				return false;
			}
			PixelElement childElement = val.childElement;
			MachineryPanelRow val2 = (MachineryPanelRow)(object)((childElement is MachineryPanelRow) ? childElement : null);
			if (val2 == null)
			{
				return false;
			}
			MachineryPanel panel = val2.GetPanel("storage");
			tabs = (panel != null) ? (panel.content as MachineryTabs) : null;
			if (tabs == null || tabs.count <= 1)
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

	private static MachineryTabs FindTabsInVisibleWindows(Vector2 screenPosition)
	{
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		WindowsHandler current = WindowsHandler.current;
		if ((Object)(object)current == (Object)null || current.visibleWindows == null)
		{
			return null;
		}
		for (int i = 0; i < current.visibleWindows.Count; i++)
		{
			PixelWindow val = current.visibleWindows[i];
			if (val == null || !val.IsVisible() || (Object)(object)val.handler == (Object)null)
			{
				continue;
			}
			TreeNodeRender[] componentsInChildren;
			try
			{
				componentsInChildren = ((Component)val.handler).gameObject.GetComponentsInChildren<TreeNodeRender>(true);
			}
			catch
			{
				continue;
			}
			if (componentsInChildren == null || componentsInChildren.Length == 0)
			{
				continue;
			}
			foreach (TreeNodeRender val2 in componentsInChildren)
			{
				if (!((Object)(object)val2 == (Object)null))
				{
					MachineryTabs val3 = val2.FindParentElement<MachineryTabs>();
					if (val3 != null && val3.count > 1 && !((Object)(object)val3.handler == (Object)null) && ((Component)val3.handler).gameObject.activeInHierarchy && (ContainsScreenPoint(val3.handler.rectTransform, screenPosition) || ContainsWindowScreenPoint(val, screenPosition)))
					{
						return val3;
					}
				}
			}
		}
		return null;
	}

	private static bool ContainsWindowScreenPoint(PixelWindow window, Vector2 screenPosition)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		if (window == null)
		{
			return false;
		}
		if (ContainsScreenPoint(window.rectTransform, screenPosition))
		{
			return true;
		}
		try
		{
			RectTransform[] componentsInChildren = ((Component)window.handler).gameObject.GetComponentsInChildren<RectTransform>(true);
			RectTransform rect = null;
			float num = 0f;
			foreach (RectTransform val in componentsInChildren)
			{
				if (!((Object)(object)val == (Object)null))
				{
					Rect rect2 = val.rect;
					Vector2 size = rect2.size;
					float num2 = Mathf.Abs(size.x * size.y);
					if (num2 > num)
					{
						num = num2;
						rect = val;
					}
				}
			}
			return ContainsScreenPoint(rect, screenPosition);
		}
		catch
		{
			return false;
		}
	}

	private static bool ContainsScreenPoint(RectTransform rect, Vector2 screenPosition)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)rect == (Object)null)
		{
			return false;
		}
		try
		{
			return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, GetCamera(rect));
		}
		catch
		{
			return false;
		}
	}

	private static Camera GetCamera(RectTransform rect)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Canvas val = (((Object)(object)rect != (Object)null) ? ((Component)rect).GetComponentInParent<Canvas>() : null);
			return ((Object)(object)val != (Object)null && (int)val.renderMode != 0) ? val.worldCamera : null;
		}
		catch
		{
			return null;
		}
	}

	private static void Show(MachineryTabs tabs, GameItem owner)
	{
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Unknown result type (might be due to invalid IL or missing references)
		//IL_038b: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0430: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_045d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0468: Unknown result type (might be due to invalid IL or missing references)
		//IL_047d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0491: Unknown result type (might be due to invalid IL or missing references)
		//IL_0565: Unknown result type (might be due to invalid IL or missing references)
		Hide();
		if (tabs == null || tabs.count <= 1)
		{
			return;
		}
		if ((Object)(object)RenderHandler.current == (Object)null || (Object)(object)RenderHandler.current.overlayRoot == (Object)null)
		{
			Log.Debug("tab chooser: overlay root unavailable");
			return;
		}
		_tabs = tabs;
		_ownerItem = owner;
		_hover = 0;
		RectTransform val = FindTarget(tabs, owner);
		Vector2 val2;
		if (!((Object)(object)val != (Object)null))
		{
			val2 = new Vector2(160f, 120f);
		}
		else
		{
			Rect rect = val.rect;
			val2 = rect.size;
		}
		Vector2 val3 = (Vector2)val2;
		int count = tabs.count;
		_columns = ((count <= 6) ? 1 : ((count <= 12) ? 2 : 3));
		_gap = 1f;
		float num = Mathf.Clamp(val3.x * 0.03f, 1f, 4f);
		float num2 = ((_columns == 1) ? val3.x : ((float)_columns * 92f + num * 2f));
		float num3 = ((_columns == 1) ? Mathf.Min(240f, Mathf.Max(48f, (float)Screen.width - 16f)) : Mathf.Max(48f, Mathf.Min(360f, (float)Screen.width - 16f)));
		_width = Mathf.Clamp(num2, 48f, num3);
		_padding = Mathf.Clamp(_width * 0.03f, 1f, 4f);
		int num4 = (count + _columns - 1) / _columns;
		float num5 = Mathf.Max(48f, Mathf.Min(560f, (float)Screen.height * 0.75f));
		float num6 = _padding * 2f + (float)num4 * 14f + (float)(num4 - 1) * _gap;
		_height = Mathf.Clamp(Mathf.Max(val3.y, num6), 48f, num5);
		float num7 = Mathf.Max(1f, _height - _padding * 2f);
		_cellWidth = Mathf.Max(1f, (_width - _padding * 2f - _gap * (float)(_columns - 1)) / (float)_columns);
		_cellHeight = Mathf.Max(1f, (num7 - _gap * (float)(num4 - 1)) / (float)num4);
		_root = TreeNodeRender.Instantiate().SetParent(RenderHandler.current.overlayRoot);
		if ((Object)(object)_root == (Object)null)
		{
			Log.Debug("tab chooser: root node creation failed");
			return;
		}
		((Object)((Component)_root).gameObject).name = "PenQoL_TabChooser";
		RectTransform rectTransform = _root.rectTransform;
		rectTransform.pivot = new Vector2(0f, 1f);
		rectTransform.anchorMin = new Vector2(0f, 1f);
		rectTransform.anchorMax = new Vector2(0f, 1f);
		rectTransform.sizeDelta = new Vector2(_width, _height);
		if ((Object)(object)_root.image != (Object)null)
		{
			((Graphic)_root.image).raycastTarget = true;
			MachineryStyle.SetFlat(_root, new Color(0.08f, 0.06f, 0.05f, 0.94f));
		}
		((Transform)rectTransform).SetAsLastSibling();
		for (int i = 0; i < count; i++)
		{
			int num8 = i % _columns;
			int num9 = i / _columns;
			TreeNodeRender val4 = TreeNodeRender.Instantiate().SetParent(_root);
			if ((Object)(object)val4 == (Object)null)
			{
				continue;
			}
			RectTransform rectTransform2 = val4.rectTransform;
			rectTransform2.anchorMin = new Vector2(0f, 1f);
			rectTransform2.anchorMax = new Vector2(0f, 1f);
			rectTransform2.pivot = new Vector2(0f, 1f);
			rectTransform2.sizeDelta = new Vector2(_cellWidth, _cellHeight);
			rectTransform2.anchoredPosition = new Vector2(_padding + (float)num8 * (_cellWidth + _gap), 0f - _padding - (float)num9 * (_cellHeight + _gap));
			if ((Object)(object)val4.image != (Object)null)
			{
				((Graphic)val4.image).raycastTarget = true;
				MachineryStyle.SetFlat(val4, RowColor(i == _hover));
			}
			TreeNodeRender val5 = TreeNodeRender.InstantiateText().SetParent(val4);
			if ((Object)(object)val5 != (Object)null)
			{
				RectTransform rectTransform3 = val5.rectTransform;
				rectTransform3.anchorMin = Vector2.zero;
				rectTransform3.anchorMax = Vector2.one;
				rectTransform3.offsetMin = new Vector2(3f, 0f);
				rectTransform3.offsetMax = new Vector2(-2f, 0f);
				if ((Object)(object)val5.text != (Object)null)
				{
					((TMP_Text)val5.text).text = SafeCaption(tabs, i);
					((TMP_Text)val5.text).enableAutoSizing = true;
					((TMP_Text)val5.text).fontSizeMin = 5f;
					((TMP_Text)val5.text).fontSizeMax = 11f;
					((TMP_Text)val5.text).fontSize = Mathf.Clamp(Mathf.Min(_cellWidth * 0.2f, _cellHeight * 0.55f), 5f, 11f);
					((TMP_Text)val5.text).enableWordWrapping = false;
					((TMP_Text)val5.text).overflowMode = (TextOverflowModes)1;
					((TMP_Text)val5.text).maxVisibleLines = 1;
					((TMP_Text)val5.text).alignment = (TextAlignmentOptions)4097;
					((Graphic)val5.text).color = Color.white;
					((Graphic)val5.text).raycastTarget = false;
					((Behaviour)val5.text).enabled = true;
				}
			}
			RowNodes.Add(val4);
		}
		PositionNear(tabs, owner);
		Log.Debug("tab chooser: show tabs=" + tabs.count + " owner=" + ((owner != null) ? owner.identifier : "window") + " root=" + ((object)rectTransform.anchoredPosition/*cast due to .constrained prefix*/).ToString());
	}

	private static RectTransform FindTarget(MachineryTabs tabs, GameItem owner)
	{
		RectTransform val = null;
		GameItemElement val2 = (GameItemElement)(object)((owner is GameItemElement) ? owner : null);
		if (val2 != null)
		{
			if ((Object)(object)val2.middle != (Object)null && (Object)(object)val2.middle.rectTransform != (Object)null)
			{
				val = val2.middle.rectTransform;
			}
			else if ((Object)(object)val2.handler != (Object)null)
			{
				val = val2.handler.rectTransform;
			}
		}
		if ((Object)(object)val == (Object)null && owner != null && owner.contentWindow != null)
		{
			val = owner.contentWindow.rectTransform;
		}
		if ((Object)(object)val == (Object)null && tabs != null && (Object)(object)tabs.handler != (Object)null)
		{
			val = tabs.handler.rectTransform;
		}
		return val;
	}

	private static void PositionNear(MachineryTabs tabs, GameItem owner)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			RectTransform val = FindTarget(tabs, owner);
			if (!((Object)(object)val == (Object)null) && !((Object)(object)_root == (Object)null))
			{
				Canvas componentInParent = ((Component)val).GetComponentInParent<Canvas>();
				Camera val2 = (((Object)(object)componentInParent != (Object)null && (int)componentInParent.renderMode != 0) ? componentInParent.worldCamera : null);
				Vector3[] array = (Vector3[])(object)new Vector3[4];
				val.GetWorldCorners(array);
				float num = float.MaxValue;
				float num2 = float.MinValue;
				float num3 = float.MaxValue;
				float num4 = float.MinValue;
				for (int i = 0; i < 4; i++)
				{
					Vector2 val3 = RectTransformUtility.WorldToScreenPoint(val2, array[i]);
					num = Mathf.Min(num, val3.x);
					num2 = Mathf.Max(num2, val3.x);
					num3 = Mathf.Min(num3, val3.y);
					num4 = Mathf.Max(num4, val3.y);
				}
				float num5 = (num + num2) * 0.5f - _width * 0.5f;
				num5 = Mathf.Clamp(num5, 8f, Mathf.Max(8f, (float)Screen.width - _width - 8f));
				float num6 = Mathf.Max(8f, (float)Screen.height - 8f);
				float num7 = 8f;
				float num8 = num4 + 3f + _height;
				float num9 = num3 - 3f;
				float num10 = ((num8 <= num6) ? num8 : ((!(num9 - _height >= num7)) ? Mathf.Clamp(num8, num7 + _height, num6) : num9));
				num10 = Mathf.Clamp(num10, num7 + _height, num6);
				RectTransform val4 = (((Object)(object)componentInParent != (Object)null) ? (((Component)componentInParent).transform as RectTransform) : null) ?? (_root.rectTransform.parent as RectTransform);
				Vector3 position = default(Vector3);
				if ((Object)(object)val4 != (Object)null && RectTransformUtility.ScreenPointToWorldPointInRectangle(val4, new Vector2(num5, num10), val2, out position))
				{
					((Transform)_root.rectTransform).position = position;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Debug("tab chooser position failed: " + ex.Message);
		}
	}

	private static void UpdateHover(Vector2 screenPosition)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		int num = HitRow(screenPosition);
		if (num == _hover)
		{
			return;
		}
		_hover = num;
		for (int i = 0; i < RowNodes.Count; i++)
		{
			if ((Object)(object)RowNodes[i] != (Object)null && (Object)(object)RowNodes[i].image != (Object)null)
			{
				MachineryStyle.SetFlat(RowNodes[i], RowColor(i == _hover));
			}
		}
	}

	private static int HitRow(Vector2 screenPosition)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_root == (Object)null)
		{
			return -1;
		}
		Camera camera = GetCamera();
		for (int i = 0; i < RowNodes.Count; i++)
		{
			TreeNodeRender val = RowNodes[i];
			if (!((Object)(object)val == (Object)null) && !((Object)(object)val.rectTransform == (Object)null) && RectTransformUtility.RectangleContainsScreenPoint(val.rectTransform, screenPosition, camera))
			{
				return i;
			}
		}
		return -1;
	}

	private static bool IsInsideRoot(Vector2 screenPosition)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_root != (Object)null && (Object)(object)_root.rectTransform != (Object)null)
		{
			return RectTransformUtility.RectangleContainsScreenPoint(_root.rectTransform, screenPosition, GetCamera());
		}
		return false;
	}

	private static Camera GetCamera()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Canvas val = (((Object)(object)_root != (Object)null) ? ((Component)_root).GetComponentInParent<Canvas>() : null);
			return ((Object)(object)val != (Object)null && (int)val.renderMode != 0) ? val.worldCamera : null;
		}
		catch
		{
			return null;
		}
	}

	private static string SafeCaption(MachineryTabs tabs, int index)
	{
		try
		{
			string caption = tabs.GetCaption(index);
			return string.IsNullOrEmpty(caption) ? ("Tab " + (index + 1)) : caption;
		}
		catch
		{
			return "Tab " + (index + 1);
		}
	}

	private static Color RowColor(bool highlighted)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		if (!highlighted)
		{
			return new Color(0.14f, 0.11f, 0.08f, 0.96f);
		}
		return new Color(0.28f, 0.2f, 0.12f, 0.98f);
	}
}
