using System;
using System.Runtime.CompilerServices;

namespace PenLabelerAssist;

internal static class WaterLabelSync
{
	private sealed class PurityBox
	{
		public int Value;
	}

	private const int UnknownPurity = int.MinValue;

	private static ConditionalWeakTable<GameItem, PurityBox> _lastPurity = new ConditionalWeakTable<GameItem, PurityBox>();

	public static void Reset()
	{
		_lastPurity = new ConditionalWeakTable<GameItem, PurityBox>();
	}

	public static int Prepare(GameItem item)
	{
		try
		{
			if (!ModSettings.WaterLabelSync)
			{
				return int.MinValue;
			}
			if (!IsWaterContainer(item))
			{
				return int.MinValue;
			}
			WaterHelper.GetWaterPurity(item);
			if (_lastPurity.TryGetValue(item, out var value))
			{
				return value.Value;
			}
			ItemFeature val = item.FindItemFeatureByCategory("CATEGORY_WATER_PURITY");
			if (val == null)
			{
				return int.MinValue;
			}
			ItemCondition realCondition = val.realCondition;
			if (realCondition != null && realCondition.category == "CATEGORY_WATER_PURITY" && realCondition.customValue > 0)
			{
				return realCondition.customValue;
			}
			if (val.isFeatureMatch)
			{
				return val.customIntValue1;
			}
			return int.MinValue;
		}
		catch
		{
			return int.MinValue;
		}
	}

	public static void Apply(GameItem item, int previousPurity)
	{
		try
		{
			if (!ModSettings.WaterLabelSync || !IsWaterContainer(item))
			{
				return;
			}
			int waterPurity = WaterHelper.GetWaterPurity(item);
			Remember(item, waterPurity);
			if (previousPurity == int.MinValue || previousPurity == waterPurity || WaterHelper.IsEmpty(item))
			{
				return;
			}
			ItemFeature val = item.FindItemFeatureByCategory("CATEGORY_WATER_PURITY");
			if (val != null)
			{
				ItemCondition conditionFromPurity = WaterFeatureHelper.GetConditionFromPurity(waterPurity);
				if (conditionFromPurity != null)
				{
					string text = WaterFeatureHelper.GetWaterDisplayName(waterPurity) + " (" + WaterFeatureHelper.WATER_VALUE_DISPLAY[WaterFeatureHelper.GetPurityArrayIndex(waterPurity)] + " Purity)";
					val.fakeCondition = conditionFromPurity;
					val.realCondition = conditionFromPurity;
					val.customIntValue1 = waterPurity;
					val.isFeatureMatch = true;
					val.SetPublicDisplay(text);
					val.SetActualDisplay(text);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Debug("water label sync failed: " + ex.Message);
		}
	}

	private static void Remember(GameItem item, int purity)
	{
		try
		{
			if (_lastPurity.TryGetValue(item, out var value))
			{
				value.Value = purity;
				return;
			}
			_lastPurity.Add(item, new PurityBox
			{
				Value = purity
			});
		}
		catch
		{
		}
	}

	private static bool IsWaterContainer(GameItem item)
	{
		try
		{
			return item != null && item.IsTag("LIQUID_CONTAINER_TAG");
		}
		catch
		{
			return false;
		}
	}
}
