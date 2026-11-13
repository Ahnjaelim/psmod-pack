using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PenLabelerAssist;

internal static class Selection
{
	private static FieldInfo _selectedField;

	private static FieldInfo _selectedHomesField;

	private static FieldInfo _stateField;

	private static MethodInfo _refreshHighlightMethod;

	private static MethodInfo _clearSelectionMethod;

	private static object _stateIdle;

	private static bool _resolved;

	public static bool HasAnySelected()
	{
		try
		{
			ItemMultiSelectHandler current = ItemMultiSelectHandler.current;
			if ((Object)(object)current == (Object)null)
			{
				return false;
			}
			List<GameItemElement> raw = GetRaw(current);
			if (raw == null)
			{
				return false;
			}
			for (int i = 0; i < raw.Count; i++)
			{
				GameItemElement val = raw[i];
				if (val != null && !val.IsDestroyed() && ((GameItem)val).parentInventory != null)
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	public static List<GameItemElement> GetSelected()
	{
		List<GameItemElement> list = new List<GameItemElement>();
		try
		{
			ItemMultiSelectHandler current = ItemMultiSelectHandler.current;
			if ((Object)(object)current == (Object)null || !current.HasSelection)
			{
				return list;
			}
			List<GameItemElement> raw = GetRaw(current);
			if (raw == null)
			{
				return list;
			}
			HashSet<GameItemElement> hashSet = new HashSet<GameItemElement>();
			for (int i = 0; i < raw.Count; i++)
			{
				GameItemElement val = raw[i];
				if (val != null && !val.IsDestroyed() && ((GameItem)val).parentInventory != null && hashSet.Add(val))
				{
					list.Add(val);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("read selection failed: " + ex.Message);
		}
		return list;
	}

	public static bool ToggleShiftClick(ItemMultiSelectHandler handler, GameItemElement item)
	{
		try
		{
			if ((Object)(object)handler == (Object)null || item == null)
			{
				return false;
			}
			Resolve();
			if (_stateField == null)
			{
				return false;
			}
			if (_stateField.GetValue(handler)?.ToString() != "Selected")
			{
				return false;
			}
			List<GameItemElement> raw = GetRaw(handler);
			if (raw == null || raw.Count == 0)
			{
				return false;
			}
			Dictionary<GameItemElement, GameInventory> homes = GetHomes(handler);
			if (raw.Contains(item))
			{
				raw.Remove(item);
				homes?.Remove(item);
				if (raw.Count == 0)
				{
					if (_clearSelectionMethod != null)
					{
						_clearSelectionMethod.Invoke(handler, null);
					}
					else
					{
						raw.Clear();
						homes?.Clear();
					}
					_stateField.SetValue(handler, _stateIdle);
				}
				else
				{
					RefreshHighlight(handler);
				}
			}
			else
			{
				if (item.IsDestroyed() || ((GameItem)item).parentInventory == null)
				{
					return false;
				}
				if (!((GameItem)item).MayRemove() || ((GameItem)item).MaxNumRemove() < 1)
				{
					return false;
				}
				if (ItemPinHelper.IsPinned((GameItem)(object)item))
				{
					return false;
				}
				raw.Add(item);
				if (homes != null)
				{
					homes[item] = ((GameItem)item).parentInventory;
				}
				RefreshHighlight(handler);
			}
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn("multi-select toggle failed: " + ex.Message);
			return false;
		}
	}

	private static void Resolve()
	{
		if (_resolved)
		{
			return;
		}
		_resolved = true;
		try
		{
			_selectedField = AccessTools.Field(typeof(ItemMultiSelectHandler), "selectedItems");
			_selectedHomesField = AccessTools.Field(typeof(ItemMultiSelectHandler), "selectedHomes");
			_stateField = AccessTools.Field(typeof(ItemMultiSelectHandler), "state");
			_refreshHighlightMethod = AccessTools.Method(typeof(ItemMultiSelectHandler), "RefreshSelectionHighlight", Type.EmptyTypes, (Type[])null);
			_clearSelectionMethod = AccessTools.Method(typeof(ItemMultiSelectHandler), "ClearSelection", Type.EmptyTypes, (Type[])null);
			if (_stateField != null)
			{
				_stateIdle = Enum.Parse(_stateField.FieldType, "Idle");
			}
		}
		catch (Exception ex)
		{
			Log.Warn("resolve selection members failed: " + ex.Message);
		}
	}

	private static List<GameItemElement> GetRaw(ItemMultiSelectHandler handler)
	{
		Resolve();
		return _selectedField?.GetValue(handler) as List<GameItemElement>;
	}

	private static Dictionary<GameItemElement, GameInventory> GetHomes(ItemMultiSelectHandler handler)
	{
		Resolve();
		return _selectedHomesField?.GetValue(handler) as Dictionary<GameItemElement, GameInventory>;
	}

	private static void RefreshHighlight(ItemMultiSelectHandler handler)
	{
		try
		{
			_refreshHighlightMethod?.Invoke(handler, null);
		}
		catch (Exception ex)
		{
			Log.Warn("refresh selection highlight failed: " + ex.Message);
		}
	}
}
