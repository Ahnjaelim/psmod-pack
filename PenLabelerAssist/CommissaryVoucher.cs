using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PenLabelerAssist;

internal static class CommissaryVoucher
{
	private const string VoucherLabelName = "PenQoL_CommissaryVoucher";

	private const string GridName = "PenQoL_CommissaryQuotaGrid";

	private const int Columns = 4;

	private const int MaxItems = 8;

	private const float CellWidth = 64f;

	private const float CellHeight = 36f;

	private const float CellSpacing = 4f;

	private static readonly Dictionary<string, Sprite> IconCache = new Dictionary<string, Sprite>();

	public static void AppendToCard(ScrollRect scroll, AfterhourLocation location)
	{
		if (!ModSettings.ShowTransitVoucher)
		{
			return;
		}
		try
		{
			if ((Object)(object)scroll == (Object)null || (Object)(object)scroll.content == (Object)null || location == null || location.Id != "commissary")
			{
				return;
			}
			PlayerStore instance = PlayerStore.Instance;
			if (instance != null && instance.commissaryData != null)
			{
				TMP_FontAsset val = ResolveFont();
				if (!((Object)(object)val == (Object)null))
				{
					RectTransform content = scroll.content;
					AppendVoucherLabel((Transform)(object)content, val, instance.commissaryData.redeemedCredit);
					AppendQuotaGrid((Transform)(object)content, val, instance.commissaryData.commissaryItemDatas);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("commissary voucher card failed: " + ex.Message);
		}
	}

	private static void AppendVoucherLabel(Transform parent, TMP_FontAsset font, int balance)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		Transform val = parent.Find("PenQoL_CommissaryVoucher");
		TextMeshProUGUI val2 = (((Object)(object)val != (Object)null) ? ((Component)val).GetComponent<TextMeshProUGUI>() : null);
		if ((Object)(object)val2 == (Object)null)
		{
			GameObject val3 = new GameObject("PenQoL_CommissaryVoucher", new Type[1] { typeof(RectTransform) });
			val3.transform.SetParent(parent, false);
			val2 = val3.AddComponent<TextMeshProUGUI>();
			((TMP_Text)val2).font = font;
			((TMP_Text)val2).fontSize = 18f;
			((Graphic)val2).color = TransitStyle.EVENT_DESCRIPTION_TEXT;
			((TMP_Text)val2).alignment = (TextAlignmentOptions)514;
			((Graphic)val2).raycastTarget = false;
			((TMP_Text)val2).richText = true;
			LayoutElement obj = val3.AddComponent<LayoutElement>();
			obj.minHeight = 44f;
			obj.preferredHeight = 44f;
		}
		string text = Text.Get("commissary_voucher_balance", balance);
		string text2 = Text.Get("commissary_voucher_hint");
		((TMP_Text)val2).text = (string.IsNullOrEmpty(text2) ? text : (text + "\n" + text2));
	}

	private static void AppendQuotaGrid(Transform parent, TMP_FontAsset font, List<CommissaryItemData> items)
	{
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Expected O, but got Unknown
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		if (items == null || items.Count == 0)
		{
			return;
		}
		List<CommissaryItemData> list = new List<CommissaryItemData>();
		for (int i = 0; i < items.Count; i++)
		{
			if (items[i] != null && !string.IsNullOrEmpty(items[i].identifier) && items[i].StockCount > 0)
			{
				list.Add(items[i]);
			}
		}
		list.Sort(delegate(CommissaryItemData a, CommissaryItemData b)
		{
			int num6 = b.StockCount.CompareTo(a.StockCount);
			return (num6 != 0) ? num6 : string.Compare(a.identifier, b.identifier, StringComparison.Ordinal);
		});
		int num = Math.Min(8, list.Count);
		if (num <= 0)
		{
			Transform val = parent.Find("PenQoL_CommissaryQuotaGrid");
			if ((Object)(object)val != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)val).gameObject);
			}
			return;
		}
		Transform val2 = parent.Find("PenQoL_CommissaryQuotaGrid");
		GameObject val3;
		GridLayoutGroup val4;
		if ((Object)(object)val2 != (Object)null)
		{
			for (int num2 = val2.childCount - 1; num2 >= 0; num2--)
			{
				Object.Destroy((Object)(object)((Component)val2.GetChild(num2)).gameObject);
			}
			val3 = ((Component)val2).gameObject;
			val4 = val3.GetComponent<GridLayoutGroup>();
		}
		else
		{
			val3 = new GameObject("PenQoL_CommissaryQuotaGrid", new Type[1] { typeof(RectTransform) });
			val3.transform.SetParent(parent, false);
			val4 = val3.AddComponent<GridLayoutGroup>();
		}
		val4.cellSize = new Vector2(64f, 36f);
		val4.spacing = new Vector2(4f, 4f);
		val4.constraint = (GridLayoutGroup.Constraint)1;
		val4.constraintCount = 4;
		val4.startCorner = (GridLayoutGroup.Corner)0;
		val4.startAxis = (GridLayoutGroup.Axis)0;
		int num3 = (num + 4 - 1) / 4;
		float num4 = (float)num3 * 36f + (float)Math.Max(0, num3 - 1) * 4f;
		LayoutElement val5 = val3.GetComponent<LayoutElement>();
		if ((Object)(object)val5 == (Object)null)
		{
			val5 = val3.AddComponent<LayoutElement>();
		}
		val5.minHeight = num4;
		val5.preferredHeight = num4;
		for (int num5 = 0; num5 < num; num5++)
		{
			BuildCell(val3.transform, font, list[num5]);
		}
	}

	private static void BuildCell(Transform parent, TMP_FontAsset font, CommissaryItemData item)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject("Item_" + item.identifier, new Type[1] { typeof(RectTransform) });
		val.transform.SetParent(parent, false);
		bool flag = false;
		Sprite val2 = ResolveIcon(item.identifier);
		if ((Object)(object)val2 != (Object)null)
		{
			flag = true;
			GameObject val3 = new GameObject("Icon", new Type[1] { typeof(RectTransform) });
			val3.transform.SetParent(val.transform, false);
			Image obj = val3.AddComponent<Image>();
			obj.sprite = val2;
			obj.preserveAspect = true;
			((Graphic)obj).raycastTarget = false;
			RectTransform rectTransform = ((Graphic)obj).rectTransform;
			rectTransform.anchorMin = new Vector2(0f, 0.5f);
			rectTransform.anchorMax = new Vector2(0f, 0.5f);
			rectTransform.pivot = new Vector2(0f, 0.5f);
			rectTransform.anchoredPosition = new Vector2(2f, 0f);
			rectTransform.sizeDelta = new Vector2(24f, 24f);
		}
		GameObject val4 = new GameObject("Count", new Type[1] { typeof(RectTransform) });
		val4.transform.SetParent(val.transform, false);
		TextMeshProUGUI obj2 = val4.AddComponent<TextMeshProUGUI>();
		((TMP_Text)obj2).font = font;
		((TMP_Text)obj2).fontSize = 12f;
		((Graphic)obj2).color = TransitStyle.EVENT_EFFECT_TEXT;
		((TMP_Text)obj2).alignment = (TextAlignmentOptions)514;
		((Graphic)obj2).raycastTarget = false;
		((TMP_Text)obj2).enableWordWrapping = false;
		((TMP_Text)obj2).overflowMode = (TextOverflowModes)0;
		((TMP_Text)obj2).text = item.StockCount.ToString();
		RectTransform rectTransform2 = ((TMP_Text)obj2).rectTransform;
		rectTransform2.anchorMin = Vector2.zero;
		rectTransform2.anchorMax = Vector2.one;
		rectTransform2.offsetMin = new Vector2(flag ? 28f : 0f, 0f);
		rectTransform2.offsetMax = Vector2.zero;
	}

	private static Sprite ResolveIcon(string identifier)
	{
		if (string.IsNullOrEmpty(identifier))
		{
			return null;
		}
		if (IconCache.TryGetValue(identifier, out var value))
		{
			return value;
		}
		Sprite value2 = null;
		GameItem item = null;
		try
		{
			item = DirectoryMaster.Item(identifier, false);
			if (item != null)
			{
				// 모드 아이템 그림은 ModLoader 를 거쳐야 해서 건너뜀 (모드 스캐너가 ModLoader 접근을 막는다)
				if (!item.useSpriteFromMod)
				{
					value2 = RenderHandler.TryLoadFromAtlas(item.spriteAtlasPath, item.spritePath);
				}
			}
		}
		catch
		{
			value2 = null;
		}
		finally
		{
			if (item != null)
			{
				try
				{
					item.Destroy();
				}
				catch
				{
				}
			}
		}
		if ((Object)(object)value2 == (Object)null)
		{
			value2 = RenderHandler.unknownSprite;
		}
		IconCache[identifier] = value2;
		return value2;
	}

	private static TMP_FontAsset ResolveFont()
	{
		FontLoader instance = FontLoader.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return null;
		}
		string text = null;
		try
		{
			text = LocHelper.GetCurrentLocaleCode();
		}
		catch
		{
		}
		TMP_FontAsset val = ((!string.IsNullOrEmpty(text) && text.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) ? instance.simplifiedChineseFont : instance.notoSans);
		if ((Object)(object)val == (Object)null)
		{
			val = instance.notoSans;
		}
		if ((Object)(object)val == (Object)null)
		{
			val = instance.simplifiedChineseFont;
		}
		return val;
	}
}
