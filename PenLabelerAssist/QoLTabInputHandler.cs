using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

public class QoLTabInputHandler : InputActionHandler
{
	private static readonly List<KeyCode> Keys = new List<KeyCode>();

	public override List<KeyCode> keyListeners => Keys;

	public static void RefreshKeys()
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Keys.Clear();
			KeyCode tabLeft = QoLKeyBindings.TabLeft;
			KeyCode tabRight = QoLKeyBindings.TabRight;
			if ((int)tabLeft != 0)
			{
				Keys.Add(tabLeft);
			}
			if ((int)tabRight != 0 && tabRight != tabLeft)
			{
				Keys.Add(tabRight);
			}
		}
		catch
		{
		}
	}

	private void Awake()
	{
		((InputActionHandler)this).priority = 10000;
		RefreshKeys();
	}

	protected override bool OnEventInit()
	{
		return true;
	}

	protected override void OnEventPress(ISet<KeyCode> keysPressed)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (keysPressed != null && keysPressed.Count != 0 && ModSettings.TabSwitchKeys && (!((Object)(object)StoreUIManager.Instance != (Object)null) || !StoreUIManager.IsAnyTyping()) && !KeyBindHelper.SuppressHotkeys)
			{
				MachineryTabs lastTabs = TabSwitcher.LastTabs;
				if (lastTabs != null && lastTabs.count > 1)
				{
					int num = (keysPressed.Contains(QoLKeyBindings.TabRight) ? 1 : (-1));
					int num2 = (lastTabs.selected + num + lastTabs.count) % lastTabs.count;
					lastTabs.Select(num2, true);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("tab switch failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	protected override void OnEventRelease(ISet<KeyCode> keysReleased)
	{
	}

	protected override void OnEventMouseMove(Vector3 mousePosition, Vector3 mouseDelta)
	{
	}

	protected override void OnEventMouseScroll(Vector2 scrollDelta)
	{
	}

	protected override bool OnEventUpdate()
	{
		return false;
	}

	protected override void OnEventReset()
	{
	}
}
