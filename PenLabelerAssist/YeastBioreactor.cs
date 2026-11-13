using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class YeastBioreactor
{
	private const string BioreactorId = "wine_yeast_infinite";

	private const string WineContainerTag = "WINE_CONTAINER_TAG";

	private const string WineContainerState = "WINE_CONTAINER_STATE";

	private const string WineIngredientTag = "WINE_INGREDIENT_TAG";

	private const string WineYeastTag = "WINE_YEAST_TAG";

	private const string WineYeastPower = "WINE_YEAST_POWER_INT";

	private const string WineScore = "WINE_SCORE_INT";

	private const int BioreactorPower = 2;

	private const int RequiredWaterMl = 749;

	public static void Postfix(GameItem item)
	{
		try
		{
			if (item != null)
			{
				((GameItemFunc)item).maySelectSlotItemFunc = (GameItem s, GameInventory _, SlotMarker _) => GeneralHelper.IsItemEffectivelyOwned(s);
				((GameItemFunc)item).canSelectSlotItemFunc = (GameItem s, GameInventory _, SlotMarker _) => GeneralHelper.IsItemEffectivelyOwned(s);
				((GameItemFunc)item).mayThisTargetItemFunc = CanTargetBottle;
				((GameItemFunc)item).canThisTargetItemFunc = CanTargetBottle;
				((GameItemFunc)item).onThisTargetItemFunc = UseOnBottle;
			}
		}
		catch (Exception ex)
		{
			Log.Debug("yeast bioreactor init failed: " + ex.Message);
		}
	}

	public static void OnEquipped(GameItem item)
	{
		if (item != null && item.identifier == "wine_yeast_infinite")
		{
			Postfix(item);
		}
	}

	public static bool CanUseOn(GameItem source, GameItem target)
	{
		return CanTargetBottle(source, target);
	}

	public static bool TryUse(GameItem source, GameItem target)
	{
		if (!CanTargetBottle(source, target))
		{
			return false;
		}
		UseOnBottle(source, target);
		return true;
	}

	public static void ApplyToExisting()
	{
		try
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				return;
			}
			int num = 0;
			List<GameItem> allOwnedItems = instance.GetAllOwnedItems(false, true);
			for (int i = 0; i < allOwnedItems.Count; i++)
			{
				GameItem val = allOwnedItems[i];
				if (val != null && !(val.identifier != "wine_yeast_infinite"))
				{
					Postfix(val);
					num++;
				}
			}
			GameItem val2 = (((Object)(object)ItemSelectHandler.current != (Object)null) ? ItemSelectHandler.current.GetEquippedItem() : null);
			if (val2 != null && val2.identifier == "wine_yeast_infinite")
			{
				Postfix(val2);
				num++;
			}
			if (num > 0)
			{
				Log.Debug("yeast bioreactor applied to existing items: " + num);
			}
		}
		catch (Exception ex)
		{
			Log.Debug("yeast bioreactor existing scan failed: " + ex.Message);
		}
	}

	private static bool CanTargetBottle(GameItem source, GameItem target)
	{
		try
		{
			if (source == null || target == null)
			{
				return false;
			}
			if (source.identifier != "wine_yeast_infinite")
			{
				return false;
			}
			if (!target.IsTag("WINE_CONTAINER_TAG"))
			{
				return false;
			}
			if (target.GetTagReadonly("WINE_CONTAINER_STATE").GetInt() != 0)
			{
				return false;
			}
			if (!GeneralHelper.IsItemEffectivelyOwned(source))
			{
				return false;
			}
			if (!GeneralHelper.IsItemEffectivelyOwned(target))
			{
				return false;
			}
			if (!HasIngredient(target))
			{
				return false;
			}
			if (WaterHelper.GetCurrentCapacityML(target) < 749)
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

	private static void UseOnBottle(GameItem source, GameItem target)
	{
		try
		{
			if (!CanTargetBottle(source, target))
			{
				return;
			}
			bool flag = HasYeast(target);
			if (!flag)
			{
				target.ModifyTag("WINE_YEAST_POWER_INT", (Action<TagState>)delegate(TagState s)
				{
					s.Enable().SetInt(2);
				}, false);
			}
			WineHelper.TryStartFermentation(target);
			if (!flag && target.GetTagReadonly("WINE_CONTAINER_STATE").GetInt() == 1)
			{
				target.ModifyTag("WINE_SCORE_INT", (Action<TagState>)delegate(TagState s)
				{
					s.Enable().SetInt(s.GetInt() + 1);
				}, false);
			}
			else if (!flag && target.GetTagReadonly("WINE_CONTAINER_STATE").GetInt() == 0)
			{
				target.ModifyTag("WINE_YEAST_POWER_INT", (Action<TagState>)delegate(TagState s)
				{
					s.Disable().SetInt(1);
				}, false);
			}
		}
		catch (Exception ex)
		{
			Log.Debug("yeast bioreactor use failed: " + ex.Message);
		}
	}

	private static bool HasYeast(GameItem bottle)
	{
		try
		{
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)bottle, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (item != null && item.IsTag("WINE_YEAST_TAG"))
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

	private static bool HasIngredient(GameItem bottle)
	{
		try
		{
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)bottle, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (item != null && item.IsTag("WINE_INGREDIENT_TAG"))
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
}
