using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PenLabelerAssist;

internal static class QoLKeyBindings
{
	public const string TabLeftId = "pen_qol_tab_left";

	public const string TabRightId = "pen_qol_tab_right";

	public const string FollowModifierId = "pen_qol_window_follow_modifier";

	public const string SnapModifierId = "pen_qol_window_snap_modifier";

	public const string BoxSelectModifierId = "pen_qol_boxselect_modifier";

	public const string MultiSelectToggleId = "pen_qol_multi_select_toggle";

	public const string QuickBuyId = "pen_qol_quick_buy";

	public const string QuickSellId = "pen_qol_quick_sell";

	public const string QuickAppraisalId = "pen_qol_quick_appraisal";

	public const string QuickConfrontId = "pen_qol_quick_confront";

	public const string QuickPinId = "pen_qol_quick_pin";

	public const string ForceSaveId = "pen_qol_force_save";

	private static bool _registered;

	public static KeyCode TabLeft => KeyBindHelper.Get("pen_qol_tab_left");

	public static KeyCode TabRight => KeyBindHelper.Get("pen_qol_tab_right");

	public static KeyCode FollowModifier => KeyBindHelper.Get("pen_qol_window_follow_modifier");

	public static KeyCode SnapModifier => KeyBindHelper.Get("pen_qol_window_snap_modifier");

	public static KeyCode BoxSelectModifier => KeyBindHelper.Get("pen_qol_boxselect_modifier");

	public static KeyCode MultiSelectToggle => KeyBindHelper.Get("pen_qol_multi_select_toggle");

	public static KeyCode QuickBuy => KeyBindHelper.Get("pen_qol_quick_buy");

	public static KeyCode QuickSell => KeyBindHelper.Get("pen_qol_quick_sell");

	public static KeyCode QuickAppraisal => KeyBindHelper.Get("pen_qol_quick_appraisal");

	public static KeyCode QuickConfront => KeyBindHelper.Get("pen_qol_quick_confront");

	public static KeyCode QuickPin => KeyBindHelper.Get("pen_qol_quick_pin");

	public static KeyCode ForceSave => KeyBindHelper.Get("pen_qol_force_save");

	public static bool IsBoundKeyDown(KeyCode key)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		if ((int)key != 0)
		{
			return Input.GetKeyDown(key);
		}
		return false;
	}

	public static bool IsFollowModifierDown()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		return IsDown(FollowModifier);
	}

	public static bool IsSnapModifierDown()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		return IsDown(SnapModifier);
	}

	public static bool IsBoxSelectModifierDown()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		KeyCode boxSelectModifier = BoxSelectModifier;
		if (IsAlt(boxSelectModifier))
		{
			if (!Input.GetKey((KeyCode)308))
			{
				return Input.GetKey((KeyCode)307);
			}
			return true;
		}
		return IsDown(boxSelectModifier);
	}

	public static bool IsMultiSelectToggleDown()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		return IsDown(MultiSelectToggle);
	}

	public static bool SnapConflictsWithBoxSelect()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		KeyCode boxSelectModifier = BoxSelectModifier;
		KeyCode snapModifier = SnapModifier;
		if ((int)boxSelectModifier == 0 || (int)snapModifier == 0)
		{
			return false;
		}
		if (boxSelectModifier == snapModifier)
		{
			return true;
		}
		if (IsAlt(boxSelectModifier))
		{
			return IsAlt(snapModifier);
		}
		return false;
	}

	private static bool IsAlt(KeyCode key)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Invalid comparison between Unknown and I4
		if ((int)key != 308)
		{
			return (int)key == 307;
		}
		return true;
	}

	private static bool IsDown(KeyCode key)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Invalid comparison between Unknown and I4
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected I4, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		if ((int)key == 0 || (int)key == 323)
		{
			return false;
		}
		return ((int)key - 324) switch
		{
			0 => Input.GetMouseButton(1), 
			1 => Input.GetMouseButton(2), 
			2 => Input.GetMouseButton(3), 
			3 => Input.GetMouseButton(4), 
			4 => Input.GetMouseButton(5), 
			5 => Input.GetMouseButton(6), 
			_ => Input.GetKey(key), 
		};
	}

	public static void Register()
	{
		if (_registered)
		{
			return;
		}
		_registered = true;
		try
		{
			EnsureBinding("pen_qol_tab_left", (KeyCode)276, "keybind_tab_left");
			EnsureBinding("pen_qol_tab_right", (KeyCode)275, "keybind_tab_right");
			EnsureBinding("pen_qol_window_follow_modifier", (KeyCode)306, "keybind_follow_modifier");
			EnsureBinding("pen_qol_window_snap_modifier", (KeyCode)304, "keybind_snap_modifier");
			EnsureBinding("pen_qol_boxselect_modifier", (KeyCode)308, "keybind_boxselect_modifier");
			EnsureBinding("pen_qol_multi_select_toggle", (KeyCode)304, "keybind_multi_select_toggle");
			EnsureBinding("pen_qol_quick_buy", (KeyCode)32, "keybind_quick_buy");
			EnsureBinding("pen_qol_quick_sell", (KeyCode)13, "keybind_quick_sell");
			EnsureBinding("pen_qol_quick_appraisal", (KeyCode)32, "keybind_quick_appraisal");
			EnsureBinding("pen_qol_quick_confront", (KeyCode)32, "keybind_quick_confront");
			EnsureBinding("pen_qol_quick_pin", (KeyCode)116, "keybind_quick_pin");
			EnsureBinding("psmp_select_all", (KeyCode)97, "keybind_select_all");
			EnsureBinding("pen_qol_force_save", (KeyCode)286, "keybind_force_save");
			ReloadSavedBindings();
			MigrateFollowBindingConflict();
			MigrateQuickSellToEnter();
			GameOptionRegistry.EnsureVanilla();
			EnsureOption("pen_qol_tab_left", "keybind_tab_left", "keybind_tab_left_tip");
			EnsureOption("pen_qol_tab_right", "keybind_tab_right", "keybind_tab_right_tip");
			EnsureOption("pen_qol_window_follow_modifier", "keybind_follow_modifier", "keybind_follow_modifier_tip");
			EnsureOption("pen_qol_window_snap_modifier", "keybind_snap_modifier", "keybind_snap_modifier_tip");
			EnsureOption("pen_qol_boxselect_modifier", "keybind_boxselect_modifier", "keybind_boxselect_modifier_tip");
			EnsureOption("pen_qol_multi_select_toggle", "keybind_multi_select_toggle", "keybind_multi_select_toggle_tip");
			EnsureOption("pen_qol_quick_buy", "keybind_quick_buy", "keybind_quick_buy_tip");
			EnsureOption("pen_qol_quick_sell", "keybind_quick_sell", "keybind_quick_sell_tip");
			EnsureOption("pen_qol_quick_appraisal", "keybind_quick_appraisal", "keybind_quick_appraisal_tip");
			EnsureOption("pen_qol_quick_confront", "keybind_quick_confront", "keybind_quick_confront_tip");
			EnsureOption("pen_qol_quick_pin", "keybind_quick_pin", "keybind_quick_pin_tip");
			EnsureOption("psmp_select_all", "keybind_select_all", "keybind_select_all_tip");
			EnsureOption("pen_qol_force_save", "keybind_force_save", "keybind_force_save_tip");
			QoLTabInputHandler.RefreshKeys();
			Log.Debug("QoL keybindings registered");
		}
		catch (Exception ex)
		{
			Log.Warn("QoL keybindings registration failed: " + ex.Message);
		}
	}


	// 빠른 판매("물건 N개 판매" 버튼) 기본 키를 Space -> Enter 로 바꿨다. 예전 기본값(Space)이 그대로면 한 번만 Enter 로 옮긴다.
	// 사용자가 직접 다른 키로 바꿔 둔 경우는 건드리지 않는다.
	private static void MigrateQuickSellToEnter()
	{
		try
		{
			const string flag = "psmp_quick_sell_enter_migrated";
			if (PlayerPrefs.GetInt(flag, 0) != 0)
			{
				return;
			}
			PlayerPrefs.SetInt(flag, 1);
			PlayerPrefs.Save();
			if (KeyBindHelper.Get("pen_qol_quick_sell") == (KeyCode)32)
			{
				KeyBindHelper.Set("pen_qol_quick_sell", (KeyCode)13);
				KeyBindHelper.Save();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("quick sell key migration failed: " + ex.Message);
		}
	}
	private static void EnsureBinding(string id, KeyCode defaultKey, string labelKey)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		if (KeyBindHelper.Find(id) == null)
		{
			KeyBindHelper.Bindings.Add(new KeyBindHelper.Binding
			{
				id = id,
				defaultKey = defaultKey,
				label = () => Text.Get(labelKey)
			});
		}
	}

	private static void ReloadSavedBindings()
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			FieldInfo fieldInfo = AccessTools.Field(typeof(KeyBindHelper), "loaded");
			if (!(fieldInfo == null))
			{
				fieldInfo.SetValue(null, false);
				KeyBindHelper.Get("pen_qol_tab_left");
			}
		}
		catch (Exception ex)
		{
			Log.Debug("keybind reload failed: " + ex.Message);
		}
	}

	private static void EnsureOption(string id, string labelKey, string tipKey)
	{
		if (GameOptionRegistry.FindSection("keybind") == null)
		{
			return;
		}
		string text = GameOptionRegistry.KeyBindOptionId(id);
		GameOptionRegistry.Entry val = GameOptionRegistry.Find(text);
		if (val != null)
		{
			val.tooltip = () => Tip(tipKey);
			val.setKey = delegate(KeyCode value)
			{
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				SetBinding(id, value);
			};
			return;
		}
		GameOptionRegistry.Add("keybind", GameOptionRegistry.KeyBind(text, (Func<string>)(() => Text.Get(labelKey)), (Func<KeyCode>)(() => KeyBindHelper.Get(id)), (Action<KeyCode>)delegate(KeyCode value)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			SetBinding(id, value);
		}, (Func<string>)(() => Tip(tipKey))));
	}

	private static void SetBinding(string id, KeyCode value)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		KeyBindHelper.Set(id, value);
		if (id == "pen_qol_tab_left" || id == "pen_qol_tab_right")
		{
			QoLTabInputHandler.RefreshKeys();
		}
	}

	private static void MigrateFollowBindingConflict()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Invalid comparison between Unknown and I4
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Invalid comparison between Unknown and I4
		try
		{
			if ((int)KeyBindHelper.Get("pen_qol_window_follow_modifier") == 304 && (int)KeyBindHelper.Get("pen_qol_multi_select_toggle") == 304)
			{
				SetBinding("pen_qol_window_follow_modifier", (KeyCode)306);
				Log.Info("window follow binding migrated: LeftShift -> LeftControl (multi-select conflict)");
			}
		}
		catch (Exception ex)
		{
			Log.Debug("follow binding migration failed: " + ex.Message);
		}
	}

	public static void RefreshVisibility()
	{
		SetFollowVisible(ModSettings.WindowFollow);
		SetSnapVisible(ModSettings.WindowSnap);
		SetAltBoxSelectVisible(ModSettings.WindowAltBoxSelect);
		SetMultiSelectVisible(ModSettings.MultiSelectToggle);
		SetQuickBuyVisible(ModSettings.QuickBuy);
		SetQuickSellVisible(ModSettings.QuickSell);
		SetQuickAppraisalVisible(ModSettings.QuickAppraisal);
		SetQuickConfrontVisible(ModSettings.QuickConfront);
		SetQuickPinVisible(ModSettings.QuickPin);
	}

	public static void SetFollowVisible(bool visible)
	{
		SetOptionVisible("pen_qol_window_follow_modifier", visible, "keybind_follow_modifier", "keybind_follow_modifier_tip");
	}

	public static void SetSnapVisible(bool visible)
	{
		SetOptionVisible("pen_qol_window_snap_modifier", visible, "keybind_snap_modifier", "keybind_snap_modifier_tip");
	}

	public static void SetAltBoxSelectVisible(bool visible)
	{
		SetOptionVisible("pen_qol_boxselect_modifier", visible, "keybind_boxselect_modifier", "keybind_boxselect_modifier_tip");
	}

	public static void SetMultiSelectVisible(bool visible)
	{
		SetOptionVisible("pen_qol_multi_select_toggle", visible, "keybind_multi_select_toggle", "keybind_multi_select_toggle_tip");
	}

	public static void SetQuickBuyVisible(bool visible)
	{
		SetOptionVisible("pen_qol_quick_buy", visible, "keybind_quick_buy", "keybind_quick_buy_tip");
	}

	public static void SetQuickSellVisible(bool visible)
	{
		SetOptionVisible("pen_qol_quick_sell", visible, "keybind_quick_sell", "keybind_quick_sell_tip");
	}

	public static void SetQuickAppraisalVisible(bool visible)
	{
		SetOptionVisible("pen_qol_quick_appraisal", visible, "keybind_quick_appraisal", "keybind_quick_appraisal_tip");
	}

	public static void SetQuickConfrontVisible(bool visible)
	{
		SetOptionVisible("pen_qol_quick_confront", visible, "keybind_quick_confront", "keybind_quick_confront_tip");
	}

	public static void SetQuickPinVisible(bool visible)
	{
		SetOptionVisible("pen_qol_quick_pin", visible, "keybind_quick_pin", "keybind_quick_pin_tip");
	}

	private static void SetOptionVisible(string id, bool visible, string labelKey, string tipKey)
	{
		if (GameOptionRegistry.FindSection("keybind") != null)
		{
			if (!visible)
			{
				GameOptionRegistry.Remove(GameOptionRegistry.KeyBindOptionId(id));
			}
			else
			{
				EnsureOption(id, labelKey, tipKey);
			}
		}
	}

	private static string Tip(string tipKey)
	{
		try
		{
			return Text.Get(tipKey) + "\n" + string.Format(Text.Get("keybind_source"), Text.Get("mod_name"));
		}
		catch
		{
			return Text.Get(tipKey);
		}
	}
}
