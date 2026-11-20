using System;

namespace PenLabelerAssist;

internal static class ModSettings
{
	private static ConfigEntry<bool> _discardAppraisal;

	private static ConfigEntry<bool> _discardEventReceipt;

	private static ConfigEntry<bool> _discardSecurityReceipt;

	private static ConfigEntry<bool> _discardHandwritten;

	private static ConfigEntry<bool> _discardExpeditionLog;

	private static ConfigEntry<bool> _showTransitVoucher;

	private static ConfigEntry<bool> _trashAutoPickup;

	private static ConfigEntry<bool> _trashBlackMarketOnly;

	private static ConfigEntry<bool> _chemBatch;

	private static ConfigEntry<bool> _chemBatchDefaultMax;

	private static ConfigEntry<bool> _chemCloseRecipe;

	private static ConfigEntry<bool> _jacksonTabletLeft;

	private static ConfigEntry<bool> _hydroponicAutoReplant;

	private static ConfigEntry<bool> _quickBuySpace;

	private static ConfigEntry<bool> _quickAppraisalSpace;

	private static ConfigEntry<bool> _quickConfrontSpace;

	private static ConfigEntry<bool> _quickPinToggle;

	private static ConfigEntry<bool> _quickSellSpace;

	private static ConfigEntry<bool> _machineAutoBattery;

	private static ConfigEntry<bool> _filterDurabilityWarning;

	private static ConfigEntry<bool> _waterLabelSync;

	private static ConfigEntry<bool> _windowAltBoxSelect;

	private static ConfigEntry<bool> _windowFollow;

	private static ConfigEntry<bool> _windowSnap;

	private static ConfigEntry<bool> _multiSelectToggle;

	private static ConfigEntry<bool> _labelerMemory;

	private static ConfigEntry<bool> _blueLabelerMemory;

	private static ConfigEntry<bool> _batchLabeler;

	private static ConfigEntry<bool> _batchBlueLabeler;

	private static ConfigEntry<bool> _batchAquaScan;

	private static ConfigEntry<bool> _batchScrewdriver;

	private static ConfigEntry<bool> _batchWelder;

	private static ConfigEntry<bool> _batchWireCutter;

	private static ConfigEntry<bool> _batchSurgery;

	private static ConfigEntry<bool> _batchBlade;

	private static ConfigEntry<bool> _dragSlotReplace;

	private static ConfigEntry<bool> _batchUvFilter;

	private static ConfigEntry<bool> _batchLiquidContainer;

	private static ConfigEntry<bool> _tabDragChooser;

	private static ConfigEntry<bool> _tabSwitchKeys;

	private static ConfigEntry<bool> _debug;

	public static bool DebugEnabled
	{
		get
		{
			if (_debug != null)
			{
				return _debug.Value;
			}
			return false;
		}
	}

	public static bool DiscardAppraisal
	{
		get
		{
			if (_discardAppraisal != null)
			{
				return _discardAppraisal.Value;
			}
			return true;
		}
	}

	public static bool DiscardEventReceipt
	{
		get
		{
			if (_discardEventReceipt != null)
			{
				return _discardEventReceipt.Value;
			}
			return true;
		}
	}

	public static bool DiscardSecurityReceipt
	{
		get
		{
			if (_discardSecurityReceipt != null)
			{
				return _discardSecurityReceipt.Value;
			}
			return true;
		}
	}

	public static bool DiscardHandwritten
	{
		get
		{
			if (_discardHandwritten != null)
			{
				return _discardHandwritten.Value;
			}
			return true;
		}
	}

	public static bool DiscardExpeditionLog
	{
		get
		{
			if (_discardExpeditionLog != null)
			{
				return _discardExpeditionLog.Value;
			}
			return true;
		}
	}

	public static bool AnyAutoDiscardEnabled
	{
		get
		{
			if (!DiscardAppraisal && !DiscardEventReceipt && !DiscardSecurityReceipt && !DiscardHandwritten)
			{
				return DiscardExpeditionLog;
			}
			return true;
		}
	}

	public static bool ShowTransitVoucher
	{
		get
		{
			if (_showTransitVoucher != null)
			{
				return _showTransitVoucher.Value;
			}
			return true;
		}
	}

	public static bool TabSwitchKeys
	{
		get
		{
			if (_tabSwitchKeys != null)
			{
				return _tabSwitchKeys.Value;
			}
			return true;
		}
	}

	public static bool ChemBatch
	{
		get
		{
			if (_chemBatch != null)
			{
				return _chemBatch.Value;
			}
			return true;
		}
	}

	public static bool ChemBatchDefaultMax
	{
		get
		{
			if (_chemBatchDefaultMax != null)
			{
				return _chemBatchDefaultMax.Value;
			}
			return true;
		}
	}

	public static bool ChemCloseRecipe
	{
		get
		{
			if (_chemCloseRecipe != null)
			{
				return _chemCloseRecipe.Value;
			}
			return true;
		}
	}

	public static bool JacksonTabletLeft
	{
		get
		{
			if (_jacksonTabletLeft != null)
			{
				return _jacksonTabletLeft.Value;
			}
			return true;
		}
	}

	public static bool HydroponicAutoReplant
	{
		get
		{
			if (_hydroponicAutoReplant != null)
			{
				return _hydroponicAutoReplant.Value;
			}
			return true;
		}
	}

	public static bool QuickSell
	{
		get
		{
			if (_quickSellSpace != null)
			{
				return _quickSellSpace.Value;
			}
			return true;
		}
	}

	public static bool QuickBuy
	{
		get
		{
			if (_quickBuySpace != null)
			{
				return _quickBuySpace.Value;
			}
			return true;
		}
	}

	public static bool QuickAppraisal
	{
		get
		{
			if (_quickAppraisalSpace != null)
			{
				return _quickAppraisalSpace.Value;
			}
			return true;
		}
	}

	public static bool QuickConfront
	{
		get
		{
			if (_quickConfrontSpace != null)
			{
				return _quickConfrontSpace.Value;
			}
			return true;
		}
	}

	public static bool QuickPin
	{
		get
		{
			if (_quickPinToggle != null)
			{
				return _quickPinToggle.Value;
			}
			return true;
		}
	}

	public static bool MachineAutoBattery
	{
		get
		{
			if (_machineAutoBattery != null)
			{
				return _machineAutoBattery.Value;
			}
			return true;
		}
	}

	public static bool FilterDurabilityWarning
	{
		get
		{
			if (_filterDurabilityWarning != null)
			{
				return _filterDurabilityWarning.Value;
			}
			return true;
		}
	}

	public static bool WaterLabelSync
	{
		get
		{
			if (_waterLabelSync != null)
			{
				return _waterLabelSync.Value;
			}
			return true;
		}
	}

	public static bool WindowAltBoxSelect
	{
		get
		{
			if (_windowAltBoxSelect != null)
			{
				return _windowAltBoxSelect.Value;
			}
			return true;
		}
	}

	public static bool WindowFollow
	{
		get
		{
			if (_windowFollow != null)
			{
				return _windowFollow.Value;
			}
			return true;
		}
	}

	public static bool WindowSnap
	{
		get
		{
			if (_windowSnap != null)
			{
				return _windowSnap.Value;
			}
			return true;
		}
	}

	public static bool MultiSelectToggle
	{
		get
		{
			if (_multiSelectToggle != null)
			{
				return _multiSelectToggle.Value;
			}
			return true;
		}
	}

	public static bool LabelerMemory
	{
		get
		{
			if (_labelerMemory != null)
			{
				return _labelerMemory.Value;
			}
			return true;
		}
	}

	public static bool BlueLabelerMemory
	{
		get
		{
			if (_blueLabelerMemory != null)
			{
				return _blueLabelerMemory.Value;
			}
			return true;
		}
	}

	public static bool BatchLabeler
	{
		get
		{
			if (_batchLabeler != null)
			{
				return _batchLabeler.Value;
			}
			return true;
		}
	}

	public static bool BatchBlueLabeler
	{
		get
		{
			if (_batchBlueLabeler != null)
			{
				return _batchBlueLabeler.Value;
			}
			return true;
		}
	}

	public static bool BatchAquaScan
	{
		get
		{
			if (_batchAquaScan != null)
			{
				return _batchAquaScan.Value;
			}
			return true;
		}
	}

	public static bool BatchScrewdriver
	{
		get
		{
			if (_batchScrewdriver != null)
			{
				return _batchScrewdriver.Value;
			}
			return true;
		}
	}

	public static bool BatchWelder
	{
		get
		{
			if (_batchWelder != null)
			{
				return _batchWelder.Value;
			}
			return true;
		}
	}

	public static bool BatchWireCutter
	{
		get
		{
			if (_batchWireCutter != null)
			{
				return _batchWireCutter.Value;
			}
			return true;
		}
	}

	public static bool BatchSurgery
	{
		get
		{
			if (_batchSurgery != null)
			{
				return _batchSurgery.Value;
			}
			return true;
		}
	}

	public static bool BatchUvFilter
	{
		get
		{
			if (_batchUvFilter != null)
			{
				return _batchUvFilter.Value;
			}
			return true;
		}
	}

	public static bool BatchBlade
	{
		get
		{
			if (_batchBlade != null)
			{
				return _batchBlade.Value;
			}
			return true;
		}
	}

	public static bool BatchLiquidContainer
	{
		get
		{
			if (_batchLiquidContainer != null)
			{
				return _batchLiquidContainer.Value;
			}
			return true;
		}
	}

	public static bool DragSlotReplace
	{
		get
		{
			if (_dragSlotReplace != null)
			{
				return _dragSlotReplace.Value;
			}
			return true;
		}
	}

	public static bool TabDragChooser
	{
		get
		{
			if (_tabDragChooser != null)
			{
				return _tabDragChooser.Value;
			}
			return true;
		}
	}

	public static bool TrashAutoPickup
	{
		get
		{
			if (_trashAutoPickup != null)
			{
				return _trashAutoPickup.Value;
			}
			return true;
		}
	}

	public static bool TrashBlackMarketOnly
	{
		get
		{
			if (_trashBlackMarketOnly != null)
			{
				return _trashBlackMarketOnly.Value;
			}
			return true;
		}
	}

	public static void Initialize(string modId)
	{
		try
		{
			ModConfig config = ModHelper.GetConfig(modId);
			_debug = config.Define("debug", false, "Debug", (Func<string>)(() => "Debug"), (Func<string>)(() => "Enable verbose diagnostic logging (English only)."), false);
			string text = Text.Get("settings_section_documents");
			_discardAppraisal = config.Define("discard_appraisal_slip", true, text, (Func<string>)(() => Text.Get("settings_appraisal_slip")), (Func<string>)(() => Text.Get("settings_appraisal_slip_tip")), false);
			_discardEventReceipt = config.Define("discard_event_receipt", true, text, (Func<string>)(() => Text.Get("settings_event_receipt")), (Func<string>)(() => Text.Get("settings_event_receipt_tip")), false);
			_discardSecurityReceipt = config.Define("discard_security_receipt", true, text, (Func<string>)(() => Text.Get("settings_security_receipt")), (Func<string>)(() => Text.Get("settings_security_receipt_tip")), false);
			_discardHandwritten = config.Define("discard_handwritten_note", true, text, (Func<string>)(() => Text.Get("settings_handwritten_note")), (Func<string>)(() => Text.Get("settings_handwritten_note_tip")), false);
			_discardExpeditionLog = config.Define("discard_expedition_log", true, text, (Func<string>)(() => Text.Get("settings_expedition_log")), (Func<string>)(() => Text.Get("settings_expedition_log_tip")), false);
			string text2 = Text.Get("settings_section_ui");
			_showTransitVoucher = config.Define("transit_voucher_balance", true, text2, (Func<string>)(() => Text.Get("settings_transit_voucher")), (Func<string>)(() => Text.Get("settings_transit_voucher_tip")), false);
			_tabSwitchKeys = config.Define("tab_switch_keys", true, text2, (Func<string>)(() => Text.Get("settings_tab_switch")), (Func<string>)(() => Text.Get("settings_tab_switch_tip")), false);
			_chemBatch = config.Define("chem_batch", true, text2, (Func<string>)(() => Text.Get("settings_chem_batch")), (Func<string>)(() => Text.Get("settings_chem_batch_tip")), false);
			_chemBatch.OnChanged += delegate(bool enabled)
			{
				try
				{
					PenLabelerAssist.ChemBatch.SetEnabled(enabled);
				}
				catch
				{
				}
			};
			_chemBatchDefaultMax = config.Define("chem_batch_default_max", true, text2, (Func<string>)(() => Text.Get("settings_chem_batch_default_max")), (Func<string>)(() => Text.Get("settings_chem_batch_default_max_tip")), false);
			_chemCloseRecipe = config.Define("chem_close_recipe", true, text2, (Func<string>)(() => Text.Get("settings_chem_close_recipe")), (Func<string>)(() => Text.Get("settings_chem_close_recipe_tip")), false);
			_jacksonTabletLeft = config.Define("jackson_tablet_left", true, text2, (Func<string>)(() => Text.Get("settings_jackson_tablet_left")), (Func<string>)(() => Text.Get("settings_jackson_tablet_left_tip")), false);
			_filterDurabilityWarning = config.Define("filter_durability_warning", true, text2, (Func<string>)(() => Text.Get("settings_filter_durability_warning")), (Func<string>)(() => Text.Get("settings_filter_durability_warning_tip")), false);
			_waterLabelSync = config.Define("water_label_sync", true, text2, (Func<string>)(() => Text.Get("settings_water_label_sync")), (Func<string>)(() => Text.Get("settings_water_label_sync_tip")), false);
			_windowAltBoxSelect = config.Define("window_band_tolerance", true, text2, (Func<string>)(() => Text.Get("settings_window_band_tolerance")), (Func<string>)(() => Text.Get("settings_window_band_tolerance_tip")), false);
			_windowAltBoxSelect.OnChanged += delegate(bool enabled)
			{
				try
				{
					QoLKeyBindings.SetAltBoxSelectVisible(enabled);
				}
				catch
				{
				}
			};
			_windowFollow = config.Define("window_follow", true, text2, (Func<string>)(() => Text.Get("settings_window_follow")), (Func<string>)(() => Text.Get("settings_window_follow_tip")), false);
			_windowFollow.OnChanged += delegate(bool enabled)
			{
				try
				{
					QoLKeyBindings.SetFollowVisible(enabled);
				}
				catch
				{
				}
				if (!enabled)
				{
					try
					{
						PenLabelerAssist.WindowFollow.Reset();
					}
					catch
					{
					}
				}
			};
			_windowSnap = config.Define("window_snap", true, text2, (Func<string>)(() => Text.Get("settings_window_snap")), (Func<string>)(() => Text.Get("settings_window_snap_tip")), false);
			_windowSnap.OnChanged += delegate(bool enabled)
			{
				try
				{
					QoLKeyBindings.SetSnapVisible(enabled);
				}
				catch
				{
				}
			};
			_multiSelectToggle = config.Define("multi_select_toggle", true, text2, (Func<string>)(() => Text.Get("settings_multi_select_toggle")), (Func<string>)(() => Text.Get("settings_multi_select_toggle_tip")), false);
			_multiSelectToggle.OnChanged += delegate(bool enabled)
			{
				try
				{
					QoLKeyBindings.SetMultiSelectVisible(enabled);
				}
				catch
				{
				}
			};
			_tabDragChooser = config.Define("tab_drag_chooser", false, text2, (Func<string>)(() => Text.Get("settings_tab_drag_chooser")), (Func<string>)(() => Text.Get("settings_tab_drag_chooser_tip")), false);
			TabDropChooser.SetEnabled(_tabDragChooser.Value);
			_tabDragChooser.OnChanged += delegate(bool enabled)
			{
				try
				{
					TabDropChooser.SetEnabled(enabled);
				}
				catch
				{
				}
			};
			string text3 = Text.Get("settings_section_tool_batch");
			_labelerMemory = config.Define("labeler_memory", true, text3, (Func<string>)(() => Text.Get("settings_labeler_memory")), (Func<string>)(() => Text.Get("settings_labeler_memory_tip")), false);
			_labelerMemory.OnChanged += delegate(bool enabled)
			{
				if (!enabled)
				{
					try
					{
						Assist.ClearMemory();
					}
					catch
					{
					}
				}
			};
			_batchLabeler = config.Define("batch_labeler", true, text3, (Func<string>)(() => ToolName("item_labeler_name", "Labeler")), (Func<string>)(() => Text.Get("settings_batch_labeler_tip")), false);
			_blueLabelerMemory = config.Define("blue_labeler_memory", true, text3, (Func<string>)(() => Text.Get("settings_blue_labeler_memory")), (Func<string>)(() => Text.Get("settings_blue_labeler_memory_tip")), false);
			_blueLabelerMemory.OnChanged += delegate(bool enabled)
			{
				if (!enabled)
				{
					try
					{
						Assist.ClearBlueMemory();
					}
					catch
					{
					}
				}
			};
			_batchBlueLabeler = config.Define("batch_blue_labeler", true, text3, (Func<string>)(() => ToolName("item_labeler_blue_name", "Blue Labeler")), (Func<string>)(() => Text.Get("settings_batch_blue_labeler_tip")), false);
			_batchAquaScan = config.Define("batch_aquascan", true, text3, (Func<string>)(() => ToolName("item_aquascan_name", "Aquascan")), (Func<string>)(() => Text.Get("settings_batch_aquascan_tip")), false);
			_batchScrewdriver = config.Define("batch_screwdriver", true, text3, (Func<string>)(() => ToolName("item_screwdriver_name", "Screwdriver")), (Func<string>)(() => Text.Get("settings_batch_screwdriver_tip")), false);
			_batchWelder = config.Define("batch_welder", true, text3, (Func<string>)(() => ToolName("item_welder_name", "Welder")), (Func<string>)(() => Text.Get("settings_batch_welder_tip")), false);
			_batchWireCutter = config.Define("batch_wire_cutter", true, text3, (Func<string>)(() => ToolName("item_wire_cutter_name", "Wire Cutter")), (Func<string>)(() => Text.Get("settings_batch_wire_cutter_tip")), false);
			_batchSurgery = config.Define("batch_surgery_tool", true, text3, (Func<string>)(() => ToolName("item_surgery_tool_name", "Surgery Tool")), (Func<string>)(() => Text.Get("settings_batch_surgery_tip")), false);
			_batchUvFilter = config.Define("batch_uv_filter", true, text3, (Func<string>)(() => Text.Get("settings_batch_uv_filter")), (Func<string>)(() => Text.Get("settings_batch_uv_filter_tip")), false);
			_batchBlade = config.Define("batch_blade", true, text3, (Func<string>)(() => Text.Get("settings_batch_blade")), (Func<string>)(() => Text.Get("settings_batch_blade_tip")), false);
			_batchLiquidContainer = config.Define("batch_liquid_container", true, text3, (Func<string>)(() => Text.Get("settings_batch_liquid_container")), (Func<string>)(() => Text.Get("settings_batch_liquid_container_tip")), false);
			_dragSlotReplace = config.Define("drag_slot_replace", true, text3, (Func<string>)(() => Text.Get("settings_drag_slot_replace")), (Func<string>)(() => Text.Get("settings_drag_slot_replace_tip")), false);
			string text4 = Text.Get("settings_section_farming");
			_hydroponicAutoReplant = config.Define("hydroponic_auto_replant", true, text4, (Func<string>)(() => Text.Get("settings_hydroponic_auto_replant")), (Func<string>)(() => Text.Get("settings_hydroponic_auto_replant_tip")), false);
			string text5 = Text.Get("settings_section_quick_actions");
			_quickBuySpace = config.Define("quick_buy_space", true, text5, (Func<string>)(() => Text.Get("settings_quick_buy_space")), (Func<string>)(() => Text.Get("settings_quick_buy_space_tip")), false);
			_quickSellSpace = config.Define("quick_sell_space", true, text5, (Func<string>)(() => Text.Get("settings_quick_sell_space")), (Func<string>)(() => Text.Get("settings_quick_sell_space_tip")), false);
			_quickAppraisalSpace = config.Define("quick_appraisal_space", true, text5, (Func<string>)(() => Text.Get("settings_quick_appraisal_space")), (Func<string>)(() => Text.Get("settings_quick_appraisal_space_tip")), false);
			_quickConfrontSpace = config.Define("quick_confront_space", true, text5, (Func<string>)(() => Text.Get("settings_quick_confront_space")), (Func<string>)(() => Text.Get("settings_quick_confront_space_tip")), false);
			_quickPinToggle = config.Define("quick_pin_toggle", true, text5, (Func<string>)(() => Text.Get("settings_quick_pin")), (Func<string>)(() => Text.Get("settings_quick_pin_tip")), false);
			ItemPinShortcut.SetEnabled(_quickPinToggle.Value);
			_quickPinToggle.OnChanged += delegate(bool enabled)
			{
				try
				{
					ItemPinShortcut.SetEnabled(enabled);
					QoLKeyBindings.SetQuickPinVisible(enabled);
				}
				catch
				{
				}
			};
			_quickBuySpace.OnChanged += delegate(bool enabled)
			{
				try
				{
					QoLKeyBindings.SetQuickBuyVisible(enabled);
				}
				catch
				{
				}
			};
			_quickSellSpace.OnChanged += delegate(bool enabled)
			{
				try
				{
					QoLKeyBindings.SetQuickSellVisible(enabled);
				}
				catch
				{
				}
			};
			_quickAppraisalSpace.OnChanged += delegate(bool enabled)
			{
				try
				{
					QoLKeyBindings.SetQuickAppraisalVisible(enabled);
				}
				catch
				{
				}
			};
			_quickConfrontSpace.OnChanged += delegate(bool enabled)
			{
				try
				{
					QoLKeyBindings.SetQuickConfrontVisible(enabled);
				}
				catch
				{
				}
			};
			string text6 = Text.Get("settings_section_machine");
			_machineAutoBattery = config.Define("machine_auto_battery", true, text6, (Func<string>)(() => Text.Get("settings_machine_auto_battery")), (Func<string>)(() => Text.Get("settings_machine_auto_battery_tip")), false);
			string text7 = Text.Get("settings_section_trash");
			_trashAutoPickup = config.Define("trash_auto_pickup", true, text7, (Func<string>)(() => Text.Get("settings_trash_auto_pickup")), (Func<string>)(() => Text.Get("settings_trash_auto_pickup_tip")), false);
			_trashBlackMarketOnly = config.Define("trash_black_market_only", true, text7, (Func<string>)(() => Text.Get("settings_black_market_only")), (Func<string>)(() => Text.Get("settings_black_market_only_tip")), false);
			try
			{
				QoLKeyBindings.RefreshVisibility();
			}
			catch
			{
			}
		}
		catch (Exception ex)
		{
			Log.Warn("settings init failed: " + ex.Message);
		}
	}

	private static string ToolName(string key, string fallback)
	{
		try
		{
			string localizedItem = LocHelper.GetLocalizedItem(key, Array.Empty<object>());
			if (!string.IsNullOrEmpty(localizedItem) && localizedItem != key)
			{
				return localizedItem;
			}
		}
		catch
		{
		}
		return fallback;
	}
}
