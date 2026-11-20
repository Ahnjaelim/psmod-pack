using System;
using System.Collections.Generic;

namespace PenLabelerAssist;

internal static class HydroponicAutoReplant
{
	public static void OnGrowCycleEnd(GameItem machine)
	{
		try
		{
			if (!ModSettings.HydroponicAutoReplant || machine == null)
			{
				return;
			}
			GameSlotInventory inputSlot = MachineHydroponic.GetInputSlot(machine);
			if (inputSlot == null || inputSlot.childItem != null)
			{
				return;
			}
			GameGridInventory outputSlot = MachineHydroponic.GetOutputSlot(machine);
			if (outputSlot == null)
			{
				return;
			}
			List<GameItem> childItems = ((GameInventory)outputSlot).childItems;
			for (int i = 0; i < childItems.Count; i++)
			{
				GameItem val = childItems[i];
				if (val != null && val.IsTag("SEED") && !val.IsTag("CONTAINER_TAG") && GeneralHelper.IsItemOwned(val) && GraphUtils.TryAcceptOnce((GraphNodeStorage)(object)inputSlot, val, 1) == 0)
				{
					Log.Debug("hydroponic auto replant: " + val.identifier);
					break;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Debug("hydroponic auto replant failed: " + ex.Message);
		}
	}
}
