// 원본: Korean Translation (korean_translation_rnin) by Gangmin(RNIN), Steam Workshop 3828896687, v1.0.10.
// 디컴파일해서 한국어 패치에 합침 (개인 사용). 네임스페이스/모드 ID/데이터 경로만 바꿈. Gugi 폰트 설정(ui_font_gugi)은 뺌 = 항상 꺼짐.
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Localization.Settings;

using Object = UnityEngine.Object;

namespace KoreanPatch.Translation;

public static class KoreanMod
{
	public const string ModId = KoreanPatch.KoreanPatchMod.ModId;

	public const string LocaleCode = "ko";

	internal static ModLog Log;

	internal static readonly Dictionary<string, Dictionary<long, string>> Data = new Dictionary<string, Dictionary<long, string>>();


	private static bool dataLoaded;

	internal static readonly Dictionary<string, string> Hardcoded = new Dictionary<string, string>();

	private static readonly List<KeyValuePair<Regex, string[]>> patterns = new List<KeyValuePair<Regex, string[]>>();

	public static void Init(ModManifest manifest)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		Log = new ModLog(manifest);
		GameObject val = new GameObject("KoreanTranslationRunner");
		Object.DontDestroyOnLoad((Object)(object)val);
		val.AddComponent<KoreanTableFiller>();
		try
		{
			LocalizationSettings.SelectedLocaleChanged += KoreanTableFiller.OnLocaleChanged;
		}
		catch (Exception ex)
		{
			Log.Warning((object)("Could not watch locale changes: " + ex.Message));
		}
	}

	public static void OnEnable()
	{
	}

	public static void OnDisable()
	{
	}

	internal static bool EnsureData()
	{
		if (dataLoaded)
		{
			return true;
		}
		string text;
		try
		{
			text = ModHelper.ReadModFileText(ModId, "data/ko.tsv");
		}
		catch (ArgumentException)
		{
			return false;
		}
		dataLoaded = true;
		if (text == null)
		{
			Log.Error((object)"data/ko.tsv is missing; Korean text will fall back to English.");
			return true;
		}
		ParseData(text);
		ParseHardcoded(ModHelper.ReadModFileText(ModId, "data/ko-hardcoded.tsv"));
		return true;
	}

	private static void ParseHardcoded(string text)
	{
		if (text == null)
		{
			return;
		}
		string[] array = text.Split('\n', StringSplitOptions.None);
		foreach (string text2 in array)
		{
			string text3 = text2.TrimEnd('\r');
			if (text3.Length == 0 || text3[0] == '#')
			{
				continue;
			}
			string[] array2 = text3.Split(new char[1] { '\t' }, 2);
			if (array2.Length == 2)
			{
				string text4 = Unescape(array2[0]);
				string text5 = Unescape(array2[1]);
				if (text4.IndexOf('*') < 0)
				{
					Hardcoded[text4] = text5;
					continue;
				}
				Regex key = new Regex("^" + Regex.Escape(text4).Replace("\\*", "(.*?)") + "$", RegexOptions.Singleline);
				patterns.Add(new KeyValuePair<Regex, string[]>(key, text5.Split('*', StringSplitOptions.None)));
			}
		}
		Log.Log((object)("Loaded " + Hardcoded.Count + " hard-coded lines and " + patterns.Count + " patterns"));
	}

	internal static string Translate(string s)
	{
		if (string.IsNullOrEmpty(s) || (Hardcoded.Count == 0 && patterns.Count == 0) || !KoreanTableFiller.KoreanSelected())
		{
			return s;
		}
		return TranslateRaw(s, 0);
	}

	private static string TranslateRaw(string s, int depth)
	{
		if (Hardcoded.TryGetValue(s, out var value))
		{
			return value;
		}
		if (depth > 3)
		{
			return s;
		}
		foreach (KeyValuePair<Regex, string[]> pattern in patterns)
		{
			Match match = pattern.Key.Match(s);
			if (!match.Success)
			{
				continue;
			}
			string[] value2 = pattern.Value;
			StringBuilder stringBuilder = new StringBuilder(value2[0]);
			for (int i = 1; i < value2.Length; i++)
			{
				if (i < match.Groups.Count)
				{
					stringBuilder.Append(TranslateRaw(match.Groups[i].Value, depth + 1));
				}
				stringBuilder.Append(value2[i]);
			}
			return stringBuilder.ToString();
		}
		return s;
	}

	private static void ParseData(string text)
	{
		int num = 0;
		int num2 = 0;
		string[] array = text.Split('\n', StringSplitOptions.None);
		foreach (string text2 in array)
		{
			string text3 = text2.TrimEnd('\r');
			if (text3.Length == 0 || text3[0] == '#')
			{
				continue;
			}
			string[] array2 = text3.Split(new char[1] { '\t' }, 3);
			if (array2.Length != 3 || !long.TryParse(array2[1], out var result))
			{
				num2++;
				continue;
			}
			if (!Data.TryGetValue(array2[0], out var value))
			{
				value = new Dictionary<long, string>();
				Data[array2[0]] = value;
			}
			value[result] = Unescape(array2[2]);
			num++;
		}
		Log.Log((object)("Loaded " + num + " Korean lines in " + Data.Count + " tables" + ((num2 > 0) ? (" (" + num2 + " bad lines skipped)") : "")));
	}

	private static string Unescape(string s)
	{
		if (s.IndexOf('\\') < 0)
		{
			return s;
		}
		StringBuilder stringBuilder = new StringBuilder(s.Length);
		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			if (c != '\\' || i + 1 >= s.Length)
			{
				stringBuilder.Append(c);
				continue;
			}
			char c2 = s[++i];
			switch (c2)
			{
			case 'n':
				stringBuilder.Append('\n');
				break;
			case 't':
				stringBuilder.Append('\t');
				break;
			case 'r':
				stringBuilder.Append('\r');
				break;
			default:
				stringBuilder.Append(c2);
				break;
			}
		}
		return stringBuilder.ToString();
	}
}
