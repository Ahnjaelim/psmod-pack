// 원본: Korean Translation (korean_translation_rnin) by Gangmin(RNIN), Steam Workshop 3828896687, v1.0.10.
// 디컴파일해서 한국어 패치에 합침 (개인 사용). 네임스페이스/모드 ID/데이터 경로만 바꿈.
using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

using Object = UnityEngine.Object;

namespace KoreanPatch.Translation;

public class KoreanTableFiller : MonoBehaviour
{
	private static readonly Dictionary<string, int> filled = new Dictionary<string, int>();

	private static bool refreshPending;

	private static bool handFontFixed;

	private static TMP_FontAsset globalFallback;

	internal static bool KoreanSelected()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (!LocalizationSettings.InitializationOperation.IsDone)
		{
			return false;
		}
		Locale selectedLocale = LocalizationSettings.SelectedLocale;
		if ((Object)(object)selectedLocale != (Object)null)
		{
			LocaleIdentifier identifier = selectedLocale.Identifier;
			return identifier.Code == "ko";
		}
		return false;
	}

	private static void FixHandwrittenFont()
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		if (handFontFixed || (Object)(object)FontLoader.Instance == (Object)null)
		{
			return;
		}
		handFontFixed = true;
		try
		{
			TMP_FontAsset val = (TMP_FontAsset)AccessTools.Field(typeof(FontLoader), "koreanHandwrittenFont").GetValue(FontLoader.Instance);
			if (!((Object)(object)val == (Object)null) && !val.isMultiAtlasTexturesEnabled)
			{
				val.isMultiAtlasTexturesEnabled = true;
				string text = default(string);
				val.TryAddCharacters("추출캡하합해혼환필.,!?-:0123456789", out text, false);
				KoreanMod.Log.Log((object)("Handwritten font: multi-atlas enabled" + (string.IsNullOrEmpty(text) ? "" : (", still missing: " + text))));
			}
		}
		catch (Exception ex)
		{
			KoreanMod.Log.Warning((object)("Could not fix the handwritten font: " + ex.Message));
		}
	}

	private static void UpdateGlobalFallback(bool korean)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		if (korean == ((Object)(object)globalFallback != (Object)null) || (Object)(object)FontLoader.Instance == (Object)null)
		{
			return;
		}
		try
		{
			List<TMP_FontAsset> fallbackFontAssets = TMP_Settings.fallbackFontAssets;
			if (fallbackFontAssets == null)
			{
				return;
			}
			if (korean)
			{
				globalFallback = (TMP_FontAsset)AccessTools.Field(typeof(FontLoader), "koreanFont").GetValue(FontLoader.Instance);
				if ((Object)(object)globalFallback != (Object)null && !fallbackFontAssets.Contains(globalFallback))
				{
					fallbackFontAssets.Add(globalFallback);
					KoreanMod.Log.Log((object)("Added " + ((Object)globalFallback).name + " as a global fallback font"));
				}
			}
			else
			{
				fallbackFontAssets.Remove(globalFallback);
				globalFallback = null;
			}
		}
		catch (Exception ex)
		{
			KoreanMod.Log.Warning((object)("Could not update the fallback font: " + ex.Message));
			globalFallback = null;
		}
	}

	private void Start()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		FillNow();
		try
		{
			AsyncOperationHandle<LocalizationSettings> initializationOperation = LocalizationSettings.InitializationOperation;
			if (!initializationOperation.IsDone)
			{
				initializationOperation.Completed += delegate
				{
					FillNow();
				};
			}
		}
		catch (Exception ex)
		{
			KoreanMod.Log.Warning((object)("Could not watch Localization start-up: " + ex.Message));
		}
	}

	private static void FillNow()
	{
		try
		{
			if (!KoreanMod.EnsureData() || !KoreanSelected())
			{
				return;
			}
			Locale selectedLocale = LocalizationSettings.SelectedLocale;
			foreach (string key in KoreanMod.Data.Keys)
			{
				if (Ensure(key, selectedLocale))
				{
					refreshPending = true;
				}
			}
		}
		catch (Exception ex)
		{
			KoreanMod.Log.Warning((object)("Early fill failed: " + ex.Message));
		}
	}

	private void Update()
	{
		FixHandwrittenFont();
		UpdateGlobalFallback(KoreanSelected());
		if (!KoreanMod.EnsureData())
		{
			return;
		}
		if (!KoreanSelected())
		{
			if (filled.Count > 0)
			{
				filled.Clear();
			}
			return;
		}
		Locale selectedLocale = LocalizationSettings.SelectedLocale;
		bool flag = false;
		foreach (string key in KoreanMod.Data.Keys)
		{
			if (Ensure(key, selectedLocale))
			{
				flag = true;
			}
		}
		if (flag || refreshPending)
		{
			refreshPending = false;
			RefreshTexts();
		}
	}

	internal static void EnsureBeforeRead(string table)
	{
		if (table != null && KoreanMod.EnsureData() && KoreanMod.Data.ContainsKey(table) && KoreanSelected() && Ensure(table, LocalizationSettings.SelectedLocale))
		{
			refreshPending = true;
		}
	}

	private static bool Ensure(string name, Locale ko)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		StringTable table;
		try
		{
			table = ((LocalizedDatabase<StringTable, StringTableEntry>)(object)LocalizationSettings.StringDatabase).GetTable((TableReference)name, ko);
		}
		catch (Exception ex)
		{
			KoreanMod.Log.Warning((object)("Could not load table " + name + ": " + ex.Message));
			return false;
		}
		if ((Object)(object)table == (Object)null)
		{
			return false;
		}
		int instanceID = ((Object)table).GetInstanceID();
		if (filled.TryGetValue(name, out var value) && value == instanceID)
		{
			return false;
		}
		filled[name] = instanceID;
		Fill(name, table);
		return true;
	}

	private static void Fill(string name, StringTable ko)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		Locale locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier("en"));
		StringTable val = (((Object)(object)locale != (Object)null) ? ((LocalizedDatabase<StringTable, StringTableEntry>)(object)LocalizationSettings.StringDatabase).GetTable((TableReference)name, locale) : null);
		Dictionary<long, string> dictionary = KoreanMod.Data[name];
		int num = 0;
		int num2 = 0;
		if ((Object)(object)val == (Object)null)
		{
			foreach (KeyValuePair<long, string> item in dictionary)
			{
				Put(ko, item.Key, item.Value, smart: false);
			}
			num = dictionary.Count;
		}
		else
		{
			foreach (StringTableEntry value2 in ((DetailedLocalizationTable<StringTableEntry>)(object)val).Values)
			{
				if (dictionary.TryGetValue(value2.KeyId, out var value))
				{
					num++;
				}
				else
				{
					value = value2.Value;
					num2++;
				}
				Put(ko, value2.KeyId, value, value2.IsSmart);
				if (name == "Mechanic" && value != value2.Value && !string.IsNullOrEmpty(value2.Value) && !KoreanMod.Hardcoded.ContainsKey(value2.Value))
				{
					var entry = ((LocalizationTable)val).SharedData.GetEntry(value2.KeyId);
					if (entry != null && entry.Key.StartsWith("mech_condition_"))
					{
						KoreanMod.Hardcoded[value2.Value] = value;
					}
				}
			}
		}
		KoreanMod.Log.Log((object)("Filled " + name + ": " + num + " Korean, " + num2 + " English fallback"));
	}

	private static void Put(StringTable table, long keyId, string text, bool smart)
	{
		StringTableEntry val = ((DetailedLocalizationTable<StringTableEntry>)(object)table).GetEntry(keyId) ?? ((DetailedLocalizationTable<StringTableEntry>)(object)table).AddEntry(keyId, text);
		val.Value = text;
		val.IsSmart = smart;
	}

	internal static void OnLocaleChanged(Locale locale)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)locale == (Object)null)
		{
			return;
		}
		LocaleIdentifier identifier = locale.Identifier;
		if (identifier.Code != "ko" || !KoreanMod.EnsureData())
		{
			return;
		}
		bool flag = false;
		foreach (string key in KoreanMod.Data.Keys)
		{
			if (Ensure(key, locale))
			{
				flag = true;
			}
		}
		if (flag)
		{
			refreshPending = true;
		}
	}

	private static void RefreshTexts()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		LocalizeStringEvent[] array = Object.FindObjectsOfType<LocalizeStringEvent>();
		foreach (LocalizeStringEvent val in array)
		{
			try
			{
				LocalizedString stringReference = val.StringReference;
				if (stringReference != null && !((LocalizedReference)stringReference).IsEmpty)
				{
					TableEntryReference tableEntryReference = ((LocalizedReference)stringReference).TableEntryReference;
					((LocalizedReference)stringReference).TableEntryReference = default(TableEntryReference);
					((LocalizedReference)stringReference).TableEntryReference = tableEntryReference;
				}
			}
			catch (Exception)
			{
			}
		}
	}
}
