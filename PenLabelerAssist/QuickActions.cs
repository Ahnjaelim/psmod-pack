using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace PenLabelerAssist;

internal static class QuickActions
{
	public static bool Prefix()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			bool flag = QoLKeyBindings.IsBoundKeyDown(QoLKeyBindings.QuickAppraisal);
			bool flag2 = QoLKeyBindings.IsBoundKeyDown(QoLKeyBindings.QuickConfront);
			bool flag3 = QoLKeyBindings.IsBoundKeyDown(QoLKeyBindings.QuickSell);
			bool flag4 = QoLKeyBindings.IsBoundKeyDown(QoLKeyBindings.QuickBuy);
			if (!flag && !flag2 && !flag3 && !flag4)
			{
				return true;
			}
			if (KeyBindHelper.SuppressHotkeys)
			{
				return true;
			}
			StoreUIManager instance = StoreUIManager.Instance;
			if ((Object)(object)instance == (Object)null || instance.IsTyping())
			{
				return true;
			}
			if (IsDialogueActive())
			{
				return true;
			}
			if (ModSettings.QuickAppraisal && flag && TryQuickAppraisal())
			{
				return false;
			}
			if (ModSettings.QuickConfront && flag2 && TryQuickConfront())
			{
				return false;
			}
			if (ModSettings.QuickSell && flag3 && TryQuickSell())
			{
				return false;
			}
			if (ModSettings.QuickBuy && flag4 && TryQuickBuy())
			{
				return false;
			}
		}
		catch (Exception ex)
		{
			Log.Debug("quick action failed: " + ex.Message);
		}
		return true;
	}

	private static bool IsDialogueActive()
	{
		try
		{
			DialogUIManager instance = DialogUIManager.Instance;
			return (Object)(object)instance != (Object)null && (instance.currentDialog != null || instance.isWaitingForManualAdvance);
		}
		catch
		{
			return true;
		}
	}

	private static bool TryQuickAppraisal()
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Invalid comparison between Unknown and I4
		try
		{
			InspectionUIManager instance = InspectionUIManager.Instance;
			if ((Object)(object)instance == (Object)null || (Object)(object)instance.appraisalPanel == (Object)null)
			{
				return false;
			}
			if (!instance.appraisalPanel.activeSelf)
			{
				return false;
			}
			if (instance.currentInspectingItem == null)
			{
				return false;
			}
			if (instance.isEditingAppraisalField)
			{
				return false;
			}
			StoreClient val = CurrentClient();
			if (val == null || (int)val.clientIntent != 14)
			{
				return false;
			}
			instance.OnAppraisalServiceEstimated();
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryQuickConfront()
	{
		try
		{
			InspectionUIManager instance = InspectionUIManager.Instance;
			if ((Object)(object)instance == (Object)null || (Object)(object)instance.confrontPanel == (Object)null)
			{
				return false;
			}
			if (!instance.confrontPanel.activeSelf)
			{
				return false;
			}
			if (instance.currentInspectingItem == null)
			{
				return false;
			}
			if (!instance.IsAtLeastOneIssueSelected())
			{
				return false;
			}
			if (InspectionUIManager.IsConfrontLocked())
			{
				return false;
			}
			if (PlayerStore.Instance == null || PlayerStore.Instance.currentClientInstance == null)
			{
				return false;
			}
			instance.OnConfront();
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryQuickSell()
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Invalid comparison between Unknown and I4
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Invalid comparison between Unknown and I4
		try
		{
			NegociationUIManager instance = NegociationUIManager.Instance;
			if ((Object)(object)instance == (Object)null || !instance.IsOpen)
			{
				return false;
			}
			if (NegociationUIManager.IsTradeLocked())
			{
				return false;
			}
			StoreClient val = CurrentClient();
			if (val == null)
			{
				return false;
			}
			if ((int)val.clientIntent != 1 && (int)val.clientIntent != 3)
			{
				return false;
			}
			if (!IsBuyingMode(instance))
			{
				return false;
			}
			NegotiationTabletFrame val2 = (((Object)(object)instance.uiObject != (Object)null) ? instance.uiObject.GetComponent<NegotiationTabletFrame>() : null);
			if ((Object)(object)val2 != (Object)null && (Object)(object)val2.confirmRoot != (Object)null && val2.confirmRoot.activeSelf)
			{
				instance.OnConfirmPanelYes();
				return true;
			}
			instance.OnSellAllClick();
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryQuickBuy()
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Invalid comparison between Unknown and I4
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Invalid comparison between Unknown and I4
		try
		{
			NegociationUIManager instance = NegociationUIManager.Instance;
			if ((Object)(object)instance == (Object)null || !instance.IsOpen)
			{
				return false;
			}
			if (NegociationUIManager.IsTradeLocked())
			{
				return false;
			}
			StoreClient val = CurrentClient();
			if (val == null)
			{
				return false;
			}
			if ((int)val.clientIntent != 2 && (int)val.clientIntent != 3)
			{
				return false;
			}
			if (!IsSellingMode(instance))
			{
				Log.Debug("quick buy skipped: mode=" + ReadMode(instance) + " intent=" + val.clientIntent.ToString());
				return false;
			}
			TabletButton val2 = ReadButton(instance, "primaryButton");
			if ((Object)(object)val2 != (Object)null && val2.Interactable)
			{
				instance.OnBuyAllClick();
				return true;
			}
			TabletButton val3 = ReadButton(instance, "secondaryButton");
			if ((Object)(object)val3 != (Object)null && val3.Interactable && val.isOfferWholesale && PlayerStore.Instance != null && PlayerStore.Instance.playerCash >= PlayerStore.Instance.GetWholesalePrice())
			{
				instance.OnWholesaleClick();
				return true;
			}
			Log.Debug("quick buy skipped: primary/secondary unavailable intent=" + val.clientIntent.ToString() + " mode=" + ReadMode(instance));
			return false;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsSellingMode(NegociationUIManager nego)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Invalid comparison between Unknown and I4
		string text = ReadMode(nego);
		if (!string.IsNullOrEmpty(text))
		{
			return text == "Selling";
		}
		StoreClient val = CurrentClient();
		if (val != null)
		{
			return (int)val.clientIntent == 2;
		}
		return false;
	}

	private static bool IsBuyingMode(NegociationUIManager nego)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Invalid comparison between Unknown and I4
		string text = ReadMode(nego);
		if (!string.IsNullOrEmpty(text))
		{
			return text == "Buying";
		}
		StoreClient val = CurrentClient();
		if (val != null)
		{
			return (int)val.clientIntent == 1;
		}
		return false;
	}

	private static string ReadMode(NegociationUIManager nego)
	{
		try
		{
			FieldInfo fieldInfo = AccessTools.Field(typeof(NegociationUIManager), "mode");
			return ((fieldInfo != null) ? fieldInfo.GetValue(nego) : null)?.ToString();
		}
		catch
		{
			return null;
		}
	}

	private static TabletButton ReadButton(NegociationUIManager nego, string fieldName)
	{
		try
		{
			FieldInfo fieldInfo = AccessTools.Field(typeof(NegociationUIManager), fieldName);
			return (fieldInfo != null) ? (fieldInfo.GetValue(nego) as TabletButton) : null;
		}
		catch
		{
			return null;
		}
	}

	private static StoreClient CurrentClient()
	{
		try
		{
			if (PlayerStore.Instance == null || PlayerStore.Instance.currentClientInstance == null)
			{
				return null;
			}
			return PlayerStore.Instance.currentClientInstance.GetClientBlueprint();
		}
		catch
		{
			return null;
		}
	}
}
