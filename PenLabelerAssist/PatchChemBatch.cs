using System;

namespace PenLabelerAssist;

internal static class PatchChemBatch
{
	private static void CallbackPrefix(MachineryButton __instance, Action callback)
	{
		try
		{
			ChemBatch.CaptureCallback(__instance, callback);
		}
		catch (Exception ex)
		{
			Log.Debug("chem batch callback capture failed: " + ex.Message);
		}
	}

	private static void CreatePostfix(ref (PixelWindow, GameGridInventory, GameSlotInventory, GameGridInventory, MachineryButton, MachineryButton, MachineryText, MachineryText, MachineryStack) __result)
	{
		try
		{
			ChemBatch.CaptureWindow(__result);
		}
		catch (Exception ex)
		{
			Log.Debug("chem batch create hook failed: " + ex.Message);
		}
	}

	private static void MachinePostfix(GameItem __result)
	{
		try
		{
			ChemBatch.Setup(__result);
		}
		catch (Exception ex)
		{
			Log.Debug("chem batch setup hook failed: " + ex.Message);
		}
	}

	private static void CanOutputPostfix(GameItem machine, GameSlotInventory containerSlot)
	{
		try
		{
			ChemBatch.OnCanOutput(machine, containerSlot);
		}
		catch (Exception ex)
		{
			Log.Debug("chem batch can-output hook failed: " + ex.Message);
		}
	}
}
