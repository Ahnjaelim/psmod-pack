using System;
using System.Collections.Generic;
using System.Text;

namespace PenLabelerAssist;

internal static class SaveSanitizer
{
	private const int MaxLoggedIdentifiers = 12;

	public static void Filter(List<SaveItemNode> items)
	{
		if (items == null || items.Count == 0)
		{
			return;
		}
		try
		{
			if (!SaveDirectoriesReady())
			{
				Log.Warn("SaveSanitizer: item directories not ready; skip cleanup");
				return;
			}
			Dictionary<long, SaveItemNode> dictionary = new Dictionary<long, SaveItemNode>();
			for (int i = 0; i < items.Count; i++)
			{
				SaveItemNode val = items[i];
				if (val != null && !dictionary.ContainsKey(val.uuid))
				{
					dictionary.Add(val.uuid, val);
				}
			}
			SaveItemNode val2 = null;
			for (int j = 0; j < items.Count; j++)
			{
				if (items[j] != null)
				{
					val2 = items[j];
					break;
				}
			}
			if (val2 != null && !IsKnown(val2))
			{
				if (!DirectoryMaster.Has<GameItem>("save_bag"))
				{
					Log.Warn("SaveSanitizer: unknown root and save_bag missing; skip cleanup");
					return;
				}
				val2.identifier = "save_bag";
				val2.itemType = typeof(GameItem).FullName;
				Log.Warn("SaveSanitizer: unknown root replaced with save_bag");
			}
			HashSet<long> hashSet = new HashSet<long>();
			HashSet<string> hashSet2 = new HashSet<string>();
			for (int k = 0; k < items.Count; k++)
			{
				SaveItemNode val3 = items[k];
				if (val3 == null)
				{
					continue;
				}
				if (!IsKnown(val3) && !string.IsNullOrEmpty(val3.identifier))
				{
					hashSet2.Add(val3.identifier);
				}
				if (val3.childItems == null)
				{
					continue;
				}
				for (int l = 0; l < val3.childItems.Count; l++)
				{
					long num = val3.childItems[l];
					if (!dictionary.ContainsKey(num))
					{
						hashSet.Add(num);
					}
				}
			}
			if (hashSet.Count == 0 && hashSet2.Count == 0)
			{
				return;
			}
			for (int m = 0; m < items.Count; m++)
			{
				SaveItemNode val4 = items[m];
				if (val4 == null)
				{
					continue;
				}
				if (val4.childItems == null)
				{
					val4.childItems = new List<long>();
				}
				if (val4.childItemInventoryNode == null)
				{
					val4.childItemInventoryNode = new List<int>();
				}
				List<long> list = new List<long>();
				List<int> list2 = new List<int>();
				int num2 = Math.Min(val4.childItems.Count, val4.childItemInventoryNode.Count);
				for (int n = 0; n < num2; n++)
				{
					long item = val4.childItems[n];
					if (!hashSet.Contains(item))
					{
						list.Add(item);
						list2.Add(val4.childItemInventoryNode[n]);
					}
				}
				val4.childItems = list;
				val4.childItemInventoryNode = list2;
			}
			if (hashSet2.Count <= 0)
			{
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			int num3 = 0;
			foreach (string item2 in hashSet2)
			{
				if (num3 < 12)
				{
					if (num3 > 0)
					{
						stringBuilder.Append(", ");
					}
					stringBuilder.Append(item2);
					num3++;
					continue;
				}
				break;
			}
			Log.Debug("SaveSanitizer: preserved " + hashSet2.Count + " unknown item node(s)" + ((stringBuilder.Length > 0) ? (" [" + stringBuilder?.ToString() + "]") : ""));
		}
		catch (Exception ex)
		{
			Log.Warn("SaveSanitizer failed: " + ex.Message);
		}
	}

	private static bool SaveDirectoriesReady()
	{
		try
		{
			List<string> identifierList = DirectoryMaster.GetIdentifierList<GameItem>((string)null);
			if (identifierList == null || identifierList.Count == 0)
			{
				return false;
			}
			return DirectoryMaster.Has<GameItem>("save_bag");
		}
		catch
		{
			return false;
		}
	}

	private static bool IsKnown(SaveItemNode node)
	{
		try
		{
			if (node == null || string.IsNullOrEmpty(node.identifier))
			{
				return false;
			}
			if (node.itemType == typeof(GameCharacterItem).FullName)
			{
				List<string> identifierList = DirectoryMaster.GetIdentifierList<GameCharacterItem>((string)null);
				if (identifierList == null || identifierList.Count == 0)
				{
					return true;
				}
				return DirectoryMaster.Has<GameCharacterItem>(node.identifier);
			}
			return DirectoryMaster.Has<GameItem>(node.identifier);
		}
		catch
		{
			return true;
		}
	}
}
