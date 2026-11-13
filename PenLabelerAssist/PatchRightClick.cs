using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenLabelerAssist;

internal static class PatchRightClick
{
	private static bool Prefix(ItemSelectHandler __instance, ISet<KeyCode> keysPressed)
	{
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_0258: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if ((Object)(object)__instance == (Object)null || keysPressed == null)
			{
				return true;
			}
			if (keysPressed.Contains((KeyCode)324))
			{
				if (Assist.EquippedBlueLabeler() != null)
				{
					LabelerBlueUIManager instance = LabelerBlueUIManager.Instance;
					if ((Object)(object)instance != (Object)null && (Object)(object)instance.panel != (Object)null && instance.panel.activeSelf)
					{
						return true;
					}
					if ((Object)(object)StoreUIManager.Instance != (Object)null && StoreUIManager.Instance.hoveringStoreUI)
					{
						return true;
					}
					Vector3 mousePosition = Input.mousePosition;
					GameItem val = RenderHandler.RaycastElement<GameItem>(new Vector2(mousePosition.x, mousePosition.y), (TreeNodeRender)null);
					if (val == null)
					{
						return true;
					}
					if (!GeneralHelper.IsItemEffectivelyOwned(val))
					{
						return true;
					}
					if (!Assist.TryQuickApplyBlue(val, out var changed))
					{
						return true;
					}
					if (changed > 0)
					{
						Assist.PlayFeedback();
						Log.Debug("blue quick apply: " + val.identifier);
					}
					return false;
				}
				if (Assist.EquippedLabeler() == null)
				{
					return true;
				}
				LabelerUIManager instance2 = LabelerUIManager.Instance;
				if ((Object)(object)instance2 != (Object)null && (Object)(object)instance2.panel != (Object)null && instance2.panel.activeSelf)
				{
					return true;
				}
				if ((Object)(object)StoreUIManager.Instance != (Object)null && StoreUIManager.Instance.hoveringStoreUI)
				{
					return true;
				}
				Vector3 mousePosition2 = Input.mousePosition;
				GameItem val2 = RenderHandler.RaycastElement<GameItem>(new Vector2(mousePosition2.x, mousePosition2.y), (TreeNodeRender)null);
				if (val2 == null)
				{
					return true;
				}
				if (!GeneralHelper.IsItemEffectivelyOwned(val2))
				{
					return true;
				}
				if (!Assist.TryQuickApply(val2, out var changed2))
				{
					return true;
				}
				if (changed2 > 0)
				{
					Assist.PlayFeedback();
					Log.Debug("quick apply: " + val2.identifier + " changed=" + changed2);
				}
				return false;
			}
			if (!keysPressed.Contains(__instance.key))
			{
				return true;
			}
			if (!__instance.IsEquipped)
			{
				return true;
			}
			GameItem equippedItem = __instance.GetEquippedItem();
			if (!Assist.IsBatchTool(equippedItem))
			{
				return true;
			}
			if (ItemPinHelper.BlocksClicks)
			{
				return true;
			}
			if (UIDragHandler.isDraggingAnyUI)
			{
				return true;
			}
			if ((Object)(object)StoreUIManager.Instance != (Object)null && StoreUIManager.Instance.hoveringStoreUI)
			{
				return true;
			}
			Vector3 mousePosition3 = Input.mousePosition;
			if (RenderHandler.RaycastElement<GameItem>(new Vector2(mousePosition3.x, mousePosition3.y), (TreeNodeRender)null) != null)
			{
				return true;
			}
			Assist.ArmWith(equippedItem);
			Assist.MarkBandTransparent();
			Log.Debug("band start: keep tool equipped " + Assist.DisplayName(equippedItem));
			return false;
		}
		catch (Exception ex)
		{
			Log.Warn("item select press hook failed: " + ex.Message);
			return true;
		}
	}
}
