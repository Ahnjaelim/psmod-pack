using System;
using UnityEngine;

namespace PenLabelerAssist;

internal static class PatchChemRecipeClose
{
	private static void Postfix(ChemFinisherUIManager __instance)
	{
		try
		{
			if (!((Object)(object)__instance == (Object)null) && ModSettings.ChemCloseRecipe && !((Object)(object)__instance.UIPanel == (Object)null) && __instance.UIPanel.activeSelf)
			{
				__instance.HideUI();
			}
		}
		catch (Exception ex)
		{
			Log.Debug("chem recipe close failed: " + ex.Message);
		}
	}
}
