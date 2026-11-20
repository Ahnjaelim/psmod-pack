using System;
using UnityEngine;

namespace PenLabelerAssist;

internal static class PatchNegotiationTablet
{
	private static void CreateWindowPostfix(string id, string layerId, CustomUIManager __instance)
	{
		try
		{
			if (!((Object)(object)__instance == (Object)null) && !(id != "negotiation_tablet") && !(layerId != "paper"))
			{
				CustomUIWindow window = __instance.GetWindow(id);
				if (window != null && !((Object)(object)window.root == (Object)null) && !((Object)(object)window.root.GetComponent<DrJacksonTabletPlacement>() != (Object)null))
				{
					window.root.AddComponent<DrJacksonTabletPlacement>();
				}
			}
		}
		catch (Exception ex)
		{
			Log.Debug("negotiation tablet placement hook failed: " + ex.Message);
		}
	}
}
