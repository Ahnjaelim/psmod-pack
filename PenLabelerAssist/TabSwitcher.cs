using System;
using UnityEngine;

namespace PenLabelerAssist;

internal static class TabSwitcher
{
	private static GameObject _handlerObject;

	private static MachineryTabs _lastTabs;

	public static MachineryTabs LastTabs
	{
		get
		{
			if (_lastTabs == null)
			{
				return null;
			}
			try
			{
				if ((Object)(object)_lastTabs.handler == (Object)null || _lastTabs.count <= 1)
				{
					_lastTabs = null;
					return null;
				}
			}
			catch
			{
				_lastTabs = null;
				return null;
			}
			return _lastTabs;
		}
	}

	public static void Remember(MachineryTabs tabs)
	{
		if (tabs != null)
		{
			_lastTabs = tabs;
		}
	}

	public static void RememberFromWindow(PixelWindow window)
	{
		try
		{
			if (window == null || (Object)(object)window.handler == (Object)null)
			{
				return;
			}
			TreeNodeRender[] componentsInChildren = ((Component)window.handler).GetComponentsInChildren<TreeNodeRender>(true);
			foreach (TreeNodeRender val in componentsInChildren)
			{
				if (!((Object)(object)val == (Object)null))
				{
					PixelElement element = val.element;
					MachineryTabs val2 = (MachineryTabs)(object)((element is MachineryTabs) ? element : null);
					if (val2 != null && val2.count > 1)
					{
						_lastTabs = val2;
						break;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("tab switch window lookup failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	public static void Ensure()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		try
		{
			if (!((Object)(object)_handlerObject != (Object)null))
			{
				if ((Object)(object)InputActionManager.current == (Object)null)
				{
					Log.Debug("tab switch: input manager not ready");
					return;
				}
				_handlerObject = new GameObject("PenQoL_TabSwitch");
				_handlerObject.AddComponent<QoLTabInputHandler>();
				Log.Debug("tab switch handler created");
			}
		}
		catch (Exception ex)
		{
			Log.Warn("tab switch handler failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	public static void Dispose()
	{
		try
		{
			if ((Object)(object)_handlerObject != (Object)null)
			{
				Object.Destroy((Object)(object)_handlerObject);
			}
		}
		catch
		{
		}
		_handlerObject = null;
		_lastTabs = null;
	}
}
