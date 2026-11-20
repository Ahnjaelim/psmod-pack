using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PenLabelerAssist;

internal static class Patches
{
	private const string HarmonyId = "personal-qol-tweak.patches";

	private static Harmony _harmony;

	private static bool _coreApplied;

	private static bool _dropdownApplied;

	private static bool _dropdownFailed;

	private static int _ok;

	private static int _failed;

	internal static void ApplyCore()
	{
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Expected O, but got Unknown
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Expected O, but got Unknown
		//IL_0143: Expected O, but got Unknown
		//IL_0143: Expected O, but got Unknown
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Expected O, but got Unknown
		//IL_01d4: Expected O, but got Unknown
		//IL_01d4: Expected O, but got Unknown
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Expected O, but got Unknown
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Expected O, but got Unknown
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Expected O, but got Unknown
		//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0302: Expected O, but got Unknown
		//IL_0346: Unknown result type (might be due to invalid IL or missing references)
		//IL_0351: Expected O, but got Unknown
		//IL_0395: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a0: Expected O, but got Unknown
		//IL_03e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ef: Expected O, but got Unknown
		//IL_0440: Unknown result type (might be due to invalid IL or missing references)
		//IL_044b: Expected O, but got Unknown
		//IL_048f: Unknown result type (might be due to invalid IL or missing references)
		//IL_049a: Expected O, but got Unknown
		//IL_04de: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e9: Expected O, but got Unknown
		//IL_052d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0538: Expected O, but got Unknown
		//IL_057c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0587: Expected O, but got Unknown
		//IL_05cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f0: Expected O, but got Unknown
		//IL_05f0: Expected O, but got Unknown
		//IL_0626: Unknown result type (might be due to invalid IL or missing references)
		//IL_0631: Expected O, but got Unknown
		//IL_0675: Unknown result type (might be due to invalid IL or missing references)
		//IL_0680: Expected O, but got Unknown
		//IL_06b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c1: Expected O, but got Unknown
		//IL_0705: Unknown result type (might be due to invalid IL or missing references)
		//IL_0710: Expected O, but got Unknown
		//IL_0754: Unknown result type (might be due to invalid IL or missing references)
		//IL_075f: Expected O, but got Unknown
		//IL_07d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_07e2: Expected O, but got Unknown
		//IL_0834: Unknown result type (might be due to invalid IL or missing references)
		//IL_083e: Expected O, but got Unknown
		//IL_089d: Unknown result type (might be due to invalid IL or missing references)
		//IL_08a7: Expected O, but got Unknown
		//IL_08f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0903: Expected O, but got Unknown
		//IL_093a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0944: Expected O, but got Unknown
		//IL_09a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_09ad: Expected O, but got Unknown
		//IL_09f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_09fc: Expected O, but got Unknown
		//IL_0a33: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a3d: Expected O, but got Unknown
		//IL_0a82: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a8c: Expected O, but got Unknown
		//IL_0ac3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0acd: Expected O, but got Unknown
		//IL_0b04: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b0e: Expected O, but got Unknown
		//IL_0b52: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b5d: Expected O, but got Unknown
		//IL_0bd6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0be0: Expected O, but got Unknown
		//IL_0c17: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c21: Expected O, but got Unknown
		//IL_0c73: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c7d: Expected O, but got Unknown
		//IL_0cf6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d00: Expected O, but got Unknown
		//IL_0d51: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d6c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d76: Expected O, but got Unknown
		//IL_0d76: Expected O, but got Unknown
		//IL_0de1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dfc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e06: Expected O, but got Unknown
		//IL_0e06: Expected O, but got Unknown
		//IL_0e65: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e6f: Expected O, but got Unknown
		//IL_0ea5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0eb0: Expected O, but got Unknown
		//IL_0f01: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f0c: Expected O, but got Unknown
		//IL_0f5e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f68: Expected O, but got Unknown
		//IL_0fba: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fc4: Expected O, but got Unknown
		//IL_1009: Unknown result type (might be due to invalid IL or missing references)
		//IL_1013: Expected O, but got Unknown
		//IL_1065: Unknown result type (might be due to invalid IL or missing references)
		//IL_106f: Expected O, but got Unknown
		//IL_10b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_10be: Expected O, but got Unknown
		//IL_1102: Unknown result type (might be due to invalid IL or missing references)
		//IL_110d: Expected O, but got Unknown
		//IL_1151: Unknown result type (might be due to invalid IL or missing references)
		//IL_115c: Expected O, but got Unknown
		//IL_11a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_11ab: Expected O, but got Unknown
		//IL_11e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_11ec: Expected O, but got Unknown
		//IL_124a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1255: Expected O, but got Unknown
		//IL_1299: Unknown result type (might be due to invalid IL or missing references)
		//IL_12b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_12be: Expected O, but got Unknown
		//IL_12be: Expected O, but got Unknown
		//IL_1303: Unknown result type (might be due to invalid IL or missing references)
		//IL_130d: Expected O, but got Unknown
		//IL_1343: Unknown result type (might be due to invalid IL or missing references)
		//IL_134e: Expected O, but got Unknown
		//IL_1385: Unknown result type (might be due to invalid IL or missing references)
		//IL_138f: Expected O, but got Unknown
		//IL_13fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_1405: Expected O, but got Unknown
		//IL_144a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1454: Expected O, but got Unknown
		//IL_1499: Unknown result type (might be due to invalid IL or missing references)
		//IL_14a3: Expected O, but got Unknown
		//IL_1501: Unknown result type (might be due to invalid IL or missing references)
		//IL_150c: Expected O, but got Unknown
		//IL_1550: Unknown result type (might be due to invalid IL or missing references)
		//IL_155b: Expected O, but got Unknown
		//IL_159f: Unknown result type (might be due to invalid IL or missing references)
		//IL_15aa: Expected O, but got Unknown
		//IL_15fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_1606: Expected O, but got Unknown
		//IL_163d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1647: Expected O, but got Unknown
		//IL_16bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_16ca: Expected O, but got Unknown
		//IL_170e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1719: Expected O, but got Unknown
		//IL_175d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1768: Expected O, but got Unknown
		//IL_179f: Unknown result type (might be due to invalid IL or missing references)
		//IL_17a9: Expected O, but got Unknown
		//IL_17ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_17f8: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		if (!_coreApplied)
		{
			_coreApplied = true;
			_ok = 0;
			_failed = 0;
			if (_harmony == null)
			{
				_harmony = new Harmony("personal-qol-tweak.patches");
			}
			Add("1 ItemMultiSelectHandler.OnEventPress", AccessTools.Method(typeof(ItemMultiSelectHandler), "OnEventPress", new Type[1] { typeof(ISet<KeyCode>) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchArmBand), "Prefix", (Type[])null, (Type[])null)), null);
			Add("2 ItemMultiSelectHandler.FinalizeBand", AccessTools.Method(typeof(ItemMultiSelectHandler), "FinalizeBand", Type.EmptyTypes, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchFinalizeBand), "Postfix", (Type[])null, (Type[])null)));
			Add("35 ItemMultiSelectHandler.OnEventPress alt select", AccessTools.Method(typeof(ItemMultiSelectHandler), "OnEventPress", new Type[1] { typeof(ISet<KeyCode>) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchWindowAltBoxSelectPress), "Prefix", (Type[])null, (Type[])null)), new HarmonyMethod(AccessTools.Method(typeof(PatchWindowAltBoxSelectPress), "Postfix", (Type[])null, (Type[])null)), new HarmonyMethod(AccessTools.Method(typeof(PatchWindowAltBoxSelectPress), "Finalizer", (Type[])null, (Type[])null)));
			Add("36 ItemMultiSelectHandler.OnEventMouseMove alt select", AccessTools.Method(typeof(ItemMultiSelectHandler), "OnEventMouseMove", new Type[2]
			{
				typeof(Vector3),
				typeof(Vector3)
			}, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchWindowAltBoxSelectMove), "Prefix", (Type[])null, (Type[])null)), new HarmonyMethod(AccessTools.Method(typeof(PatchWindowAltBoxSelectMove), "Postfix", (Type[])null, (Type[])null)), new HarmonyMethod(AccessTools.Method(typeof(PatchWindowAltBoxSelectMove), "Finalizer", (Type[])null, (Type[])null)));
			Add("37 ItemMultiSelectHandler.OnEventRelease alt select", AccessTools.Method(typeof(ItemMultiSelectHandler), "OnEventRelease", new Type[1] { typeof(ISet<KeyCode>) }, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchWindowAltBoxSelectReset), "ClearPostfix", (Type[])null, (Type[])null)));
			Add("38 ItemMultiSelectHandler.OnEventReset alt select", AccessTools.Method(typeof(ItemMultiSelectHandler), "OnEventReset", Type.EmptyTypes, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchWindowAltBoxSelectReset), "ClearPostfix", (Type[])null, (Type[])null)));
			Add("39 ItemMultiSelectHandler.IsPointerOverDragSurface alt select", AccessTools.Method(typeof(ItemMultiSelectHandler), "IsPointerOverDragSurface", new Type[1] { typeof(Vector2) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchWindowAltBoxSelectDragSurface), "Prefix", (Type[])null, (Type[])null)), null);
			Add("40 TreeNodeRender.OnBeginDrag alt select", AccessTools.Method(typeof(TreeNodeRender), "OnBeginDrag", new Type[1] { typeof(PointerEventData) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchWindowAltBoxSelectWindowDrag), "Prefix", (Type[])null, (Type[])null)), null);
			Add("41 TreeNodeRender.OnDrag alt select", AccessTools.Method(typeof(TreeNodeRender), "OnDrag", new Type[1] { typeof(PointerEventData) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchWindowAltBoxSelectWindowDrag), "Prefix", (Type[])null, (Type[])null)), null);
			Add("42 ItemMultiSelectHandler.OnEventPress multi-select toggle", AccessTools.Method(typeof(ItemMultiSelectHandler), "OnEventPress", new Type[1] { typeof(ISet<KeyCode>) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchMultiSelectToggle), "Prefix", (Type[])null, (Type[])null)), null);
			Add("43 ItemMouseDragHandler.OnEventPress multi-select toggle", AccessTools.Method(typeof(ItemMouseDragHandler), "OnEventPress", new Type[1] { typeof(ISet<KeyCode>) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchMultiSelectToggleDrag), "PressPrefix", (Type[])null, (Type[])null)), null);
			Add("44 ItemMouseDragHandler.OnEventMouseMove multi-select toggle", AccessTools.Method(typeof(ItemMouseDragHandler), "OnEventMouseMove", new Type[2]
			{
				typeof(Vector3),
				typeof(Vector3)
			}, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchMultiSelectToggleDrag), "MovePrefix", (Type[])null, (Type[])null)), null);
			Add("45 ItemQuickTransferAltHandler.OnEventPress multi-select toggle", AccessTools.Method(typeof(ItemQuickTransferAltHandler), "OnEventPress", new Type[1] { typeof(ISet<KeyCode>) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchMultiSelectQuickTransfer), "ShowcasePrefix", (Type[])null, (Type[])null)), null);
			Add("46 ItemQuickTransferAltHandler1.OnEventPress multi-select toggle", AccessTools.Method(typeof(ItemQuickTransferAltHandler1), "OnEventPress", new Type[1] { typeof(ISet<KeyCode>) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchMultiSelectQuickTransfer), "StoragePrefix", (Type[])null, (Type[])null)), null);
			Add("47 ItemQuickTransferHandler.OnEventPress multi-select toggle", AccessTools.Method(typeof(ItemQuickTransferHandler), "OnEventPress", new Type[1] { typeof(ISet<KeyCode>) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchMultiSelectQuickTransfer), "InventoryPrefix", (Type[])null, (Type[])null)), null);
			Add("48 ItemSelectHandler.OnEventPress multi-select toggle", AccessTools.Method(typeof(ItemSelectHandler), "OnEventPress", new Type[1] { typeof(ISet<KeyCode>) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchMultiSelectToolBlock), "Prefix", (Type[])null, (Type[])null)), null);
			Add("3 LabelerUIManager.OpenUI", AccessTools.Method(typeof(LabelerUIManager), "OpenUI", new Type[1] { typeof(GameItem) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchLabelerOpen), "Prefix", (Type[])null, (Type[])null)), new HarmonyMethod(AccessTools.Method(typeof(PatchLabelerOpen), "Postfix", (Type[])null, (Type[])null)));
			Add("4 LabelerUIManager.CloseUI", AccessTools.Method(typeof(LabelerUIManager), "CloseUI", Type.EmptyTypes, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchLabelerClose), "Prefix", (Type[])null, (Type[])null)), null);
			Add("5 ItemSelectHandler.OnEventPress", AccessTools.Method(typeof(ItemSelectHandler), "OnEventPress", new Type[1] { typeof(ISet<KeyCode>) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchRightClick), "Prefix", (Type[])null, (Type[])null)), null);
			Add("9b ItemSelectHandler.OnEventUpdate", AccessTools.Method(typeof(ItemSelectHandler), "OnEventUpdate", Type.EmptyTypes, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchItemSelectUpdate), "Postfix", (Type[])null, (Type[])null)));
			Add("10 SaveManager.DecodeNodes", AccessTools.Method(typeof(SaveManager), "DecodeNodes", new Type[1] { typeof(List<SaveItemNode>) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchDecodeNodes), "Prefix", (Type[])null, (Type[])null)), null);
			Add("11 MachineHelper.LoadUpgrade", AccessTools.Method(typeof(MachineHelper), "LoadUpgrade", new Type[1] { typeof(GameItem) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchMachineLoad), "Prefix", (Type[])null, (Type[])null)), null);
			Add("14 PlayerStore.TryAddDirectSellingItemToTable", AccessTools.Method(typeof(PlayerStore), "TryAddDirectSellingItemToTable", new Type[5]
			{
				typeof(GameItem),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(int)
			}, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchDiscardOnTableAdd), "Postfix", (Type[])null, (Type[])null)));
			Add("15 GraphUtils.EmporiumTryAdd", AccessTools.Method(typeof(GraphUtils), "EmporiumTryAdd", new Type[2]
			{
				typeof(GraphNodeStorage),
				typeof(GameItem)
			}, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchDiscardOnEmporiumAdd), "Postfix", (Type[])null, (Type[])null)));
			Add("16 TransitUI.BuildEventList", AccessTools.Method(typeof(TransitUI), "BuildEventList", new Type[3]
			{
				typeof(RectTransform),
				typeof(AfterhourLocation),
				typeof(float)
			}, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchTransitCard), "Postfix", (Type[])null, (Type[])null)));
			Add("18 MachineryTabs.Select", AccessTools.Method(typeof(MachineryTabs), "Select", new Type[2]
			{
				typeof(int),
				typeof(bool)
			}, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchTabsSelect), "Postfix", (Type[])null, (Type[])null)));
			Add("19 PixelWindow.Show", AccessTools.Method(typeof(PixelWindow), "Show", Type.EmptyTypes, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchWindowTabs), "ShowPostfix", (Type[])null, (Type[])null)));
			Add("20 PixelWindow.Show(mouse)", AccessTools.Method(typeof(PixelWindow), "Show", new Type[3]
			{
				typeof(Vector2),
				typeof(Vector2),
				typeof(bool)
			}, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchWindowTabs), "ShowPostfix", (Type[])null, (Type[])null)));
			Add("21 PixelWindow.ToFront", AccessTools.Method(typeof(PixelWindow), "ToFront", new Type[1] { typeof(bool) }, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchWindowTabs), "ToFrontPostfix", (Type[])null, (Type[])null)));
			Add("23 PixelWindow.Hide", AccessTools.Method(typeof(PixelWindow), "Hide", Type.EmptyTypes, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchWindowFollow), "HidePostfix", (Type[])null, (Type[])null)));
			Add("24 ItemMouseDragHandler.EndDrag", AccessTools.Method(typeof(ItemMouseDragHandler), "EndDrag", new Type[1] { typeof(bool) }, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchWindowFollow), "EndDragPostfix", (Type[])null, (Type[])null)));
			Add("25 GameItem.RestoreContentWindowPosition", AccessTools.Method(typeof(GameItem), "RestoreContentWindowPosition", Type.EmptyTypes, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchWindowFollow), "RestorePositionPostfix", (Type[])null, (Type[])null)));
			Add("26 PixelWindow.FixBounds", AccessTools.Method(typeof(PixelWindow), "FixBounds", Type.EmptyTypes, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchWindowSnap), "FixBoundsPostfix", (Type[])null, (Type[])null)));
			Add("28 MachineryButton.SetCallback", AccessTools.Method(typeof(MachineryButton), "SetCallback", new Type[1] { typeof(Action) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchChemBatch), "CallbackPrefix", (Type[])null, (Type[])null)), null);
			Add("29 MachineChemFinisher.CreateMachineInventoryWindow", AccessTools.Method(typeof(MachineChemFinisher), "CreateMachineInventoryWindow", new Type[5]
			{
				typeof(int),
				typeof(int),
				typeof(bool),
				typeof(bool),
				typeof(int)
			}, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchChemBatch), "CreatePostfix", (Type[])null, (Type[])null)));
			Add("30 MachineChemFinisher.ChemFinisher", AccessTools.Method(typeof(MachineChemFinisher), "ChemFinisher", Type.EmptyTypes, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchChemBatch), "MachinePostfix", (Type[])null, (Type[])null)));
			Add("31 MachineChemFinisher.CanOutput", AccessTools.Method(typeof(MachineChemFinisher), "CanOutput", new Type[2]
			{
				typeof(GameItem),
				typeof(GameSlotInventory)
			}, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchChemBatch), "CanOutputPostfix", (Type[])null, (Type[])null)));
			Add("32 CustomUIManager.CreateWindow", AccessTools.Method(typeof(CustomUIManager), "CreateWindow", new Type[5]
			{
				typeof(string),
				typeof(string),
				typeof(string),
				typeof(GameObject),
				typeof(CustomUIManager.Style)
			}, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchNegotiationTablet), "CreateWindowPostfix", (Type[])null, (Type[])null)));
			Add("33 WaterFeatureHelper.UpdateWaterFeature", AccessTools.Method(typeof(WaterFeatureHelper), "UpdateWaterFeature", new Type[2]
			{
				typeof(GameItem),
				typeof(bool)
			}, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchWaterLabelSync), "Prefix", (Type[])null, (Type[])null)), new HarmonyMethod(AccessTools.Method(typeof(PatchWaterLabelSync), "Postfix", (Type[])null, (Type[])null)));
			Add("34 WaterHelper.HandleFilter", AccessTools.Method(typeof(WaterHelper), "HandleFilter", new Type[4]
			{
				typeof(string),
				typeof(int),
				typeof(GameItem),
				typeof(GameItem)
			}, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchFilterDurability), "Prefix", (Type[])null, (Type[])null)), new HarmonyMethod(AccessTools.Method(typeof(PatchFilterDurability), "Postfix", (Type[])null, (Type[])null)));
			Add("49 MachineHydroponic.GrowOneCycle hydroponic auto replant", AccessTools.Method(typeof(MachineHydroponic), "GrowOneCycle", new Type[3]
			{
				typeof(GameItem),
				typeof(GameItem),
				typeof(GameGridInventory)
			}, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchHydroponicReplant), "Postfix", (Type[])null, (Type[])null)));
			Add("50 StoreUIManager.Update space quick actions", AccessTools.Method(typeof(StoreUIManager), "Update", Type.EmptyTypes, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchQuickActions), "Prefix", (Type[])null, (Type[])null)), null);
			Add("51 PowerHelper.DrawPowerSource auto battery swap", AccessTools.Method(typeof(PowerHelper), "DrawPowerSource", new Type[2]
			{
				typeof(GameItem),
				typeof(int)
			}, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchBatteryAutoSwap), "Prefix", (Type[])null, (Type[])null)), null);
			Add("52 PowerHelper.CanDrawPowerSource battery swap log", AccessTools.Method(typeof(PowerHelper), "CanDrawPowerSource", new Type[2]
			{
				typeof(GameItem),
				typeof(int)
			}, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchBatteryAutoSwap), "CanDrawPostfix", (Type[])null, (Type[])null)));
			Add("53 PowerHelper.DrawPowerSource battery state refresh", AccessTools.Method(typeof(PowerHelper), "DrawPowerSource", new Type[2]
			{
				typeof(GameItem),
				typeof(int)
			}, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchBatteryAutoSwap), "DrawPostfix", (Type[])null, (Type[])null)));
			Add("54 ChemFinisherUIManager.OnChemRecipeSelected close recipe picker", AccessTools.Method(typeof(ChemFinisherUIManager), "OnChemRecipeSelected", new Type[1] { typeof(string) }, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchChemRecipeClose), "Postfix", (Type[])null, (Type[])null)));
			Add("55 WineHelper.InitInfiniteYeast bioreactor equip effect", AccessTools.Method(typeof(WineHelper), "InitInfiniteYeast", new Type[2]
			{
				typeof(GameItem),
				typeof(int)
			}, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(YeastBioreactor), "Postfix", (Type[])null, (Type[])null)));
			Add("56 ItemSelectHandler.TryEquipItem bioreactor equip effect", AccessTools.Method(typeof(ItemSelectHandler), "TryEquipItem", new Type[1] { typeof(GameItem) }, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(YeastBioreactor), "OnEquipped", (Type[])null, (Type[])null)));
			Add("57 GameItem.MayTarget bioreactor fallback", AccessTools.Method(typeof(GameItem), "MayTarget", new Type[1] { typeof(GameItem) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(YeastBioreactorPatches), "MayTargetPrefix", (Type[])null, (Type[])null)), null);
			Add("58 GameItem.CanTarget bioreactor fallback", AccessTools.Method(typeof(GameItem), "CanTarget", new Type[1] { typeof(GameItem) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(YeastBioreactorPatches), "CanTargetPrefix", (Type[])null, (Type[])null)), null);
			Add("59 GameItem.Target bioreactor fallback", AccessTools.Method(typeof(GameItem), "Target", new Type[1] { typeof(GameItem) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(YeastBioreactorPatches), "TargetPrefix", (Type[])null, (Type[])null)), null);
			Add("61 StoreUIManager.Update pin shortcut", AccessTools.Method(typeof(StoreUIManager), "Update", Type.EmptyTypes, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(ItemPinShortcut), "Postfix", (Type[])null, (Type[])null)));
			Add("62 ChemicalHelper.TransferLiquidToChem batch defer", AccessTools.Method(typeof(ChemicalHelper), "TransferLiquidToChem", new Type[3]
			{
				typeof(GameItem),
				typeof(GameItem),
				typeof(int)
			}, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(BatchLiquidTransferPatch), "Prefix", (Type[])null, (Type[])null)), null);
			Add("63 LabelerBlueUIManager.OpenUI batch capture", AccessTools.Method(typeof(LabelerBlueUIManager), "OpenUI", new Type[1] { typeof(GameItem) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchBlueLabeler), "OpenPrefix", (Type[])null, (Type[])null)), new HarmonyMethod(AccessTools.Method(typeof(PatchBlueLabeler), "OpenPostfix", (Type[])null, (Type[])null)));
			Add("64 LabelerBlueUIManager.OnEditEnd batch apply", AccessTools.Method(typeof(LabelerBlueUIManager), "OnEditEnd", new Type[1] { typeof(string) }, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchBlueLabeler), "EditEndPostfix", (Type[])null, (Type[])null)));
			Add("65 LabelerBlueUIManager.CloseUI clear batch", AccessTools.Method(typeof(LabelerBlueUIManager), "CloseUI", Type.EmptyTypes, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchBlueLabeler), "ClosePrefix", (Type[])null, (Type[])null)), null);
			Add("66 LabelerBlueUIManager.OnClearButtonClick clear batch", AccessTools.Method(typeof(LabelerBlueUIManager), "OnClearButtonClick", Type.EmptyTypes, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchBlueLabeler), "ClearPostfix", (Type[])null, (Type[])null)));
			Add("67 LabelerBlueUIManager.OpenUI external clear batch", AccessTools.Method(typeof(LabelerBlueUIManager), "OpenUI", new Type[4]
			{
				typeof(string),
				typeof(Action<string>),
				typeof(Action),
				typeof(int)
			}, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchBlueLabeler), "ExternalOpenPrefix", (Type[])null, (Type[])null)), null);
			Add("68 ItemMouseDragHandler.Drag tab chooser", AccessTools.Method(typeof(ItemMouseDragHandler), "Drag", new Type[1] { typeof(Vector3) }, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchTabDropChooser), "DragPostfix", (Type[])null, (Type[])null)));
			Add("69 ItemMouseDragHandler.EndDrag tab chooser cleanup", AccessTools.Method(typeof(ItemMouseDragHandler), "EndDrag", new Type[1] { typeof(bool) }, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchTabDropChooser), "EndDragPostfix", (Type[])null, (Type[])null)));
			Add("70 MachineryTabs.FindDropInventoryAt tab chooser", AccessTools.Method(typeof(MachineryTabs), "FindDropInventoryAt", new Type[3]
			{
				typeof(Vector2),
				typeof(TreeNodeRender),
				typeof(PixelElement)
			}, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchTabDropChooser), "FindDropPrefix", (Type[])null, (Type[])null)), null);
			Add("71 GameItem.MayHaveValidInventorySlot tab chooser", AccessTools.Method(typeof(GameItem), "MayHaveValidInventorySlot", new Type[1] { typeof(GameItem) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchTabDropChooser), "MayHavePrefix", (Type[])null, (Type[])null)), null);
			Add("72 GameItem.TryFindOneValidInventorySlot tab chooser", AccessTools.Method(typeof(GameItem), "TryFindOneValidInventorySlot", new Type[1] { typeof(GameItem) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchTabDropChooser), "FindOneSlotPrefix", (Type[])null, (Type[])null)), null);
			Add("73 ItemMultiSelectHandler.OnEventMouseMove tab chooser", AccessTools.Method(typeof(ItemMultiSelectHandler), "OnEventMouseMove", new Type[2]
			{
				typeof(Vector3),
				typeof(Vector3)
			}, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchTabDropChooser), "DragPostfix", (Type[])null, (Type[])null)));
			Add("74 ItemMultiSelectHandler.EndGroupDrag tab chooser cleanup", AccessTools.Method(typeof(ItemMultiSelectHandler), "EndGroupDrag", Type.EmptyTypes, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchTabDropChooser), "EndDragPostfix", (Type[])null, (Type[])null)));
			Add("75 GameSlotInventory.TryInventorySlot drag replace", AccessTools.Method(typeof(GameSlotInventory), "TryInventorySlot", new Type[5]
			{
				typeof(GameItem),
				typeof(int),
				typeof(Vector2),
				typeof(GridShape),
				typeof(TagSystem)
			}, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchDragSlotReplace), "TryInventorySlotPrefix", (Type[])null, (Type[])null)), null);
			Add("76 GameSlotInventory.TryFindOneValidInventorySlot drag replace", AccessTools.Method(typeof(GameSlotInventory), "TryFindOneValidInventorySlot", new Type[1] { typeof(GameItem) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchDragSlotReplace), "TryFindOneSlotPrefix", (Type[])null, (Type[])null)), null);
			Add("77 SlotMarker.TryAcceptOnce drag replace", AccessTools.Method(typeof(SlotMarker), "TryAcceptOnce", new Type[1] { typeof(int) }, (Type[])null), new HarmonyMethod(AccessTools.Method(typeof(PatchDragSlotReplace), "TryAcceptOncePrefix", (Type[])null, (Type[])null)), null);
			Add("78 ItemMouseDragHandler.StartDrag drag replace activate", AccessTools.Method(typeof(ItemMouseDragHandler), "StartDrag", Type.EmptyTypes, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchDragSlotReplace), "StartPostfix", (Type[])null, (Type[])null)));
			Add("79 ItemMouseDragHandler.EndDrag drag replace deactivate", AccessTools.Method(typeof(ItemMouseDragHandler), "EndDrag", new Type[1] { typeof(bool) }, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchDragSlotReplace), "EndPostfix", (Type[])null, (Type[])null)));
			Log.Info("core patches: " + _ok + " applied, " + _failed + " skipped");
		}
	}

	internal static void EnsureDropdownPatch()
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		if (!_dropdownApplied && !_dropdownFailed)
		{
			if (_harmony == null)
			{
				ApplyCore();
			}
			if (_harmony == null)
			{
				_dropdownFailed = true;
				return;
			}
			if (Add("7 LabelerElement.OnDropdownValueChanged", AccessTools.Method(typeof(LabelerElement), "OnDropdownValueChanged", Type.EmptyTypes, (Type[])null), null, new HarmonyMethod(AccessTools.Method(typeof(PatchDropdownChanged), "Postfix", (Type[])null, (Type[])null))))
			{
				_dropdownApplied = true;
				return;
			}
			_dropdownFailed = true;
			Log.Warn("dropdown patch unavailable - live memory will update on panel close only");
		}
	}

	internal static void Remove()
	{
		try
		{
			if (_harmony != null)
			{
				_harmony.UnpatchAll("personal-qol-tweak.patches");
			}
		}
		catch
		{
		}
		_harmony = null;
		_coreApplied = false;
		_dropdownApplied = false;
		_dropdownFailed = false;
	}

	private static bool Add(string label, MethodBase target, HarmonyMethod prefix, HarmonyMethod postfix)
	{
		return Add(label, target, prefix, postfix, null);
	}

	private static bool Add(string label, MethodBase target, HarmonyMethod prefix, HarmonyMethod postfix, HarmonyMethod finalizer)
	{
		try
		{
			if (target == null)
			{
				_failed++;
				Log.Warn("[patch " + label + "] target not found - skipped");
				return false;
			}
			_harmony.Patch(target, prefix, postfix, (HarmonyMethod)null, finalizer);
			_ok++;
			return true;
		}
		catch (Exception ex)
		{
			_failed++;
			Log.Warn("[patch " + label + "] failed: " + ex.Message);
			return false;
		}
	}
}
