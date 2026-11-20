using System;
using System.Collections.Generic;

namespace PenLabelerAssist;

internal static class BlackMarketFilter
{
	private static readonly HashSet<string> OutputIds = new HashSet<string>(StringComparer.Ordinal)
	{
		"black_injector", "blue_blood_bag", "pink_injector", "blood_bag", "salve", "surgery_tool", "common_medical", "common_electronic", "bandage_item", "hemostatic_bandage_item",
		"topical_bandage_item", "processed_meat", "processed_milk", "galaxy_blend", "cat_bar", "soda_red", "small_raw_meat", "birth_control_pill", "pack_condom", "kitchen_cleaver",
		"kitchen_knife", "system_capped_neural_core", "energy_credit_breeder", "scav_token", "smuggler_bay_mod", "large_purple_injector", "food_stamp", "energy_credit", "metal_ingot", "water_filter",
		"sec_box", "med_box", "eng_box", "sec_keycard", "eng_keycard", "med_keycard", "system_module_performance", "system_module_efficiancy", "system_module_quality", "system_module_overclock",
		"system_module_eco", "system_module_fineness"
	};

	private static bool _built;

	public static void Build()
	{
		_built = true;
	}

	public static bool Contains(string identifier)
	{
		if (string.IsNullOrEmpty(identifier))
		{
			return false;
		}
		if (!_built)
		{
			Build();
		}
		return OutputIds.Contains(identifier);
	}
}
