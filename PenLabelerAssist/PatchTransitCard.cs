using System;
using UnityEngine.UI;

namespace PenLabelerAssist;

internal static class PatchTransitCard
{
	private static void Postfix(ScrollRect __result, AfterhourLocation __1)
	{
		try
		{
			CommissaryVoucher.AppendToCard(__result, __1);
		}
		catch (Exception ex)
		{
			Log.Warn("transit voucher hook failed: " + ex.Message);
		}
	}
}
