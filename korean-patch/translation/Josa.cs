// 원본: Korean Translation (korean_translation_rnin) by Gangmin(RNIN), Steam Workshop 3828896687, v1.0.10.
// 디컴파일해서 한국어 패치에 합침 (개인 사용). 네임스페이스/모드 ID/데이터 경로만 바꿈.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace KoreanPatch.Translation;

internal static class Josa
{
	private static readonly Regex Dual = new Regex("을\\(를\\)|를\\(을\\)|이\\(가\\)|가\\(이\\)|은\\(는\\)|는\\(은\\)|와\\(과\\)|과\\(와\\)|\\(으\\)로|\\(이\\)나|\\(이\\)랑|\\(이\\)라는|\\(이\\)라고");

	private static readonly Regex TrailingWord = new Regex("([A-Za-z]+)$");

	private static readonly Dictionary<string, int> Words = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
	{
		{ "tab", 1 },
		{ "ctrl", 2 },
		{ "control", 2 },
		{ "space", 0 },
		{ "enter", 0 },
		{ "shift", 0 },
		{ "alt", 0 },
		{ "esc", 0 },
		{ "escape", 0 },
		{ "home", 1 },
		{ "end", 1 },
		{ "insert", 0 },
		{ "delete", 0 },
		{ "backspace", 0 },
		{ "up", 1 },
		{ "down", 1 },
		{ "left", 0 },
		{ "right", 0 },
		{ "mouse", 0 },
		{ "kcal", 2 },
		{ "kg", 0 },
		{ "ml", 2 },
		{ "kj", 0 },
		{ "vip", 1 },
		{ "ai", 0 },
		{ "dna", 0 },
		{ "cnc", 0 }
	};

	internal static string Fix(string s)
	{
		if (string.IsNullOrEmpty(s) || s.IndexOf('(') < 0 || !KoreanTableFiller.KoreanSelected())
		{
			return s;
		}
		return Dual.Replace(s, (Match m) => Pick(m.Value, FinalSound(s, m.Index)));
	}

	private static string Pick(string dual, int fin)
	{
		if (fin < 0)
		{
			return dual;
		}
		bool flag = fin > 0;
		switch (dual)
		{
		case "을(를)":
		case "를(을)":
			if (!flag)
			{
				return "를";
			}
			return "을";
		case "이(가)":
		case "가(이)":
			if (!flag)
			{
				return "가";
			}
			return "이";
		case "은(는)":
		case "는(은)":
			if (!flag)
			{
				return "는";
			}
			return "은";
		case "와(과)":
		case "과(와)":
			if (!flag)
			{
				return "와";
			}
			return "과";
		case "(으)로":
			if (fin != 1)
			{
				return "로";
			}
			return "으로";
		default:
			if (!flag)
			{
				return dual.Substring(3);
			}
			return dual.Replace("(", "").Replace(")", "");
		}
	}

	private static int FinalSound(string s, int end)
	{
		int num = end - 1;
		while (num >= 0)
		{
			switch (s[num])
			{
			case '>':
			{
				int num2 = s.LastIndexOf('<', num);
				if (num2 < 0)
				{
					return -1;
				}
				num = num2 - 1;
				continue;
			}
			case '"':
			case '\'':
			case '’':
			case '”':
			case '」':
			case '』':
				num--;
				continue;
			}
			break;
		}
		if (num < 0)
		{
			return -1;
		}
		char c = s[num];
		if (c >= '가' && c <= '힣')
		{
			return ((c - 44032) % 28) switch
			{
				8 => 2, 
				0 => 0, 
				_ => 1, 
			};
		}
		if (c >= '0' && c <= '9')
		{
			return "1201001220"[c - 48] - 48;
		}
		if (c == '%')
		{
			return 0;
		}
		Match match = TrailingWord.Match(s.Substring(0, num + 1));
		if (!match.Success)
		{
			return -1;
		}
		if (Words.TryGetValue(match.Value, out var value))
		{
			return value;
		}
		if (match.Value.Length == 1 || match.Value.ToUpperInvariant() == match.Value)
		{
			switch (char.ToUpperInvariant(match.Value[match.Value.Length - 1]))
			{
			default:
				return 0;
			case 'M':
			case 'N':
				return 1;
			case 'L':
			case 'R':
				return 2;
			}
		}
		return -1;
	}
}
