using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PenLabelerAssist;

internal static class Assist
{
	private sealed class Memory
	{
		public string ItemId;

		public string ItemName;

		public Dictionary<string, string> Labels = new Dictionary<string, string>();
	}

	private sealed class BlueBatchPlan
	{
		public string ItemId;

		public List<GameItemElement> Items = new List<GameItemElement>();

		public GameItemElement Representative;
	}

	private sealed class BatchPlan
	{
		public string Category;

		public List<GameItemElement> Items = new List<GameItemElement>();

		public GameItemElement Representative;

		public bool FromSelection;
	}

	private sealed class PanelSession
	{
		public GameItem Item;

		public Dictionary<string, string> Baseline = new Dictionary<string, string>();

		public bool BaselineReady;

		public bool LiveApplied;

		public BatchPlan Batch;

		public int OpenFrame;
	}

	private static Memory _memory;

	private static Memory _lastCommitted;

	private static PanelSession _session;

	private static BatchPlan _pendingBatch;

	private static BatchPlan _queuedBatch;

	private static BlueBatchPlan _blueBatch;

	private static BlueBatchPlan _pendingBlueBatch;

	private static BlueBatchPlan _queuedBlueBatch;

	private static GameItem _blueCurrentItem;

	private static string _blueMemoryItemId;

	private static string _blueMemoryText;

	private static GameItem _armedTool;

	private static int _armedFrame = -1;

	private static int _bandTransparentFrame = -1;

	private static bool _qolSelectionOwned;

	private static bool _qolBandStarted;

	private static bool _gameReady;

	private static Dictionary<string, List<ItemCondition>> _conditionLookup;

	private static bool _conditionLookupFailed;

	public static int ArmedFrame => _armedFrame;

	public static bool QolSelectionOwned => _qolSelectionOwned;

	public static void SetGameReady(bool ready)
	{
		_gameReady = ready;
		if (!ready)
		{
			_conditionLookup = null;
			_conditionLookupFailed = false;
		}
	}

	private static Dictionary<string, List<ItemCondition>> GetConditionLookup()
	{
		if (!_gameReady || _conditionLookupFailed)
		{
			return null;
		}
		if (_conditionLookup != null)
		{
			return _conditionLookup;
		}
		try
		{
			_conditionLookup = ItemConditionList.conditionLookup;
		}
		catch (Exception ex)
		{
			_conditionLookupFailed = true;
			Log.Warn("condition list unavailable: " + ex.Message);
		}
		return _conditionLookup;
	}

	public static bool IsLabeler(GameItem item)
	{
		try
		{
			return item != null && item.IsTag("LABELER_TAG");
		}
		catch
		{
			return false;
		}
	}

	public static bool IsBlueLabeler(GameItem item)
	{
		try
		{
			return item != null && item.IsTag("LABELER_BLUE");
		}
		catch
		{
			return false;
		}
	}

	public static bool IsDirectTargetTool(GameItem item)
	{
		try
		{
			if (item == null)
			{
				return false;
			}
			if (item.IsID("screwdriver") || item.IsID("welder") || item.IsID("wire_cutter") || item.IsID("surgery_tool") || item.IsID("uv_filter"))
			{
				return true;
			}
			return item.IsTag("BLADE_ITEM_TAG");
		}
		catch
		{
			return false;
		}
	}

	public static bool IsBatchTool(GameItem item)
	{
		return IsBatchEnabled(item);
	}

	private static bool IsNoProductDirectTool(GameItem item)
	{
		if (item != null)
		{
			return item.IsID("uv_filter");
		}
		return false;
	}

	public static bool IsBatchEnabled(GameItem item)
	{
		try
		{
			if (BatchToolCompat.IsExcludedFromBoxSelect(item))
			{
				return false;
			}
			if (IsBlueLabeler(item))
			{
				return ModSettings.BatchBlueLabeler;
			}
			if (IsLabeler(item))
			{
				return ModSettings.BatchLabeler;
			}
			if (IsAquaScan(item))
			{
				return ModSettings.BatchAquaScan;
			}
			if (IsLiquidContainerTool(item))
			{
				return ModSettings.BatchLiquidContainer;
			}
			if (item == null)
			{
				return false;
			}
			if (item.IsID("screwdriver"))
			{
				return ModSettings.BatchScrewdriver;
			}
			if (item.IsID("welder"))
			{
				return ModSettings.BatchWelder;
			}
			if (item.IsID("wire_cutter"))
			{
				return ModSettings.BatchWireCutter;
			}
			if (item.IsID("surgery_tool"))
			{
				return ModSettings.BatchSurgery;
			}
			if (item.IsID("uv_filter"))
			{
				return ModSettings.BatchUvFilter;
			}
			if (item.IsTag("BLADE_ITEM_TAG"))
			{
				return ModSettings.BatchBlade;
			}
		}
		catch
		{
		}
		return false;
	}

	public static bool IsAquaScan(GameItem item)
	{
		try
		{
			return item != null && item.IsID("aquascan");
		}
		catch
		{
			return false;
		}
	}

	public static bool IsLiquidContainerTool(GameItem item)
	{
		try
		{
			return item != null && item.IsTag("LIQUID_CONTAINER_TAG");
		}
		catch
		{
			return false;
		}
	}

	public static GameItem EquippedLabeler()
	{
		try
		{
			ItemSelectHandler current = ItemSelectHandler.current;
			if ((Object)(object)current == (Object)null)
			{
				return null;
			}
			GameItem equippedItem = current.GetEquippedItem();
			return IsLabeler(equippedItem) ? equippedItem : null;
		}
		catch
		{
			return null;
		}
	}

	public static GameItem EquippedBlueLabeler()
	{
		try
		{
			ItemSelectHandler current = ItemSelectHandler.current;
			if ((Object)(object)current == (Object)null)
			{
				return null;
			}
			GameItem equippedItem = current.GetEquippedItem();
			return IsBlueLabeler(equippedItem) ? equippedItem : null;
		}
		catch
		{
			return null;
		}
	}

	public static GameItem EquippedBatchTool()
	{
		try
		{
			ItemSelectHandler current = ItemSelectHandler.current;
			if ((Object)(object)current == (Object)null)
			{
				return null;
			}
			GameItem equippedItem = current.GetEquippedItem();
			return IsBatchTool(equippedItem) ? equippedItem : null;
		}
		catch
		{
			return null;
		}
	}

	public static void ArmForBand()
	{
		try
		{
			_armedTool = EquippedBatchTool();
			_armedFrame = Time.frameCount;
			if (_armedTool != null)
			{
				MarkQolBandStart();
			}
		}
		catch
		{
			_armedTool = null;
			_armedFrame = Time.frameCount;
		}
	}

	public static void MarkQolBandStart()
	{
		_qolBandStarted = true;
		_qolSelectionOwned = true;
	}

	public static void MarkQolSelectionOwned()
	{
		_qolSelectionOwned = true;
	}

	public static void ClearQolSelectionOwnership()
	{
		_qolSelectionOwned = false;
		_qolBandStarted = false;
	}

	public static void ArmWith(GameItem tool)
	{
		_armedTool = tool;
		_armedFrame = Time.frameCount;
		MarkQolBandStart();
	}

	public static void MarkBandTransparent()
	{
		_bandTransparentFrame = Time.frameCount;
	}

	public static bool ShouldKeepItemSelectTransparent()
	{
		try
		{
			if (_armedTool == null)
			{
				return false;
			}
			if (Time.frameCount == _bandTransparentFrame)
			{
				return true;
			}
			ItemMultiSelectHandler current = ItemMultiSelectHandler.current;
			return (Object)(object)current != (Object)null && current.HasSelection && (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0));
		}
		catch
		{
			return false;
		}
	}

	public static string DisplayName(GameItem item)
	{
		try
		{
			if (item == null)
			{
				return "null";
			}
			string displayName = item.GetDisplayName(true);
			if (!string.IsNullOrEmpty(displayName))
			{
				return displayName;
			}
		}
		catch
		{
		}
		return SafeId(item);
	}

	public static void OnBandFinalized()
	{
		GameItem armedTool = _armedTool;
		bool qolBandStarted = _qolBandStarted;
		_armedTool = null;
		_qolBandStarted = false;
		List<GameItemElement> selected = Selection.GetSelected();
		if (armedTool != null || qolBandStarted)
		{
			Log.Debug("labeler batch: band tool=" + DisplayName(armedTool) + " qolBand=" + qolBandStarted + " selected=" + selected.Count + " selectionOwned=" + QolSelectionOwned);
		}
		if (armedTool != null)
		{
			MarkQolSelectionOwned();
		}
		else if (!qolBandStarted)
		{
			ClearQolSelectionOwnership();
		}
		if (armedTool == null || !IsBatchEnabled(armedTool))
		{
			return;
		}
		try
		{
			if (IsAquaScan(armedTool))
			{
				ExecuteAquaScanBatch(selected);
			}
			else if (IsLiquidContainerTool(armedTool))
			{
				ExecuteLiquidContainerBatch(armedTool, selected);
			}
			else if (IsBlueLabeler(armedTool))
			{
				QueueBlueBatch(selected);
			}
			else if (IsDirectTargetTool(armedTool))
			{
				ExecuteDirectBatch(armedTool, selected);
			}
			else
			{
				if (!IsLabeler(armedTool))
				{
					return;
				}
				LabelerUIManager instance = LabelerUIManager.Instance;
				if ((Object)(object)instance == (Object)null || (Object)(object)instance.panel == (Object)null || instance.panel.activeSelf)
				{
					return;
				}
				GameItemElement val = (GameItemElement)(object)((armedTool is GameItemElement) ? armedTool : null);
				if (val == null || !val.IsDestroyed())
				{
					BatchPlan batchPlan = PickPlan(selected);
					if (batchPlan != null && batchPlan.Items.Count != 0)
					{
						_queuedBatch = batchPlan;
						Log.Debug("batch planned: " + batchPlan.Category + " x" + batchPlan.Items.Count);
						LetterboxManager.RunNextUpdate((Action)OpenQueuedBatch);
					}
				}
			}
		}
		catch (Exception ex)
		{
			_queuedBatch = null;
			Log.Warn("batch plan failed: " + ex.Message);
		}
	}

	private static void ExecuteDirectBatch(GameItem tool, List<GameItemElement> selected)
	{
		if (tool == null || selected == null || selected.Count == 0)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		bool flag = false;
		try
		{
			for (int i = 0; i < selected.Count; i++)
			{
				GameItemElement val = selected[i];
				if (val == null || val.IsDestroyed())
				{
					num2++;
					continue;
				}
				if (((GameItem)val).parentInventory == null)
				{
					num2++;
					continue;
				}
				if ((object)val == tool)
				{
					num2++;
					continue;
				}
				if (!GeneralHelper.IsItemEffectivelyOwned((GameItem)(object)val))
				{
					num2++;
					continue;
				}
				bool flag2 = false;
				bool flag3 = false;
				try
				{
					flag2 = tool.MayTarget((GameItem)(object)val);
					flag3 = flag2 && tool.CanTarget((GameItem)(object)val);
				}
				catch
				{
				}
				if (!flag2 || !flag3)
				{
					num2++;
					continue;
				}
				if (!CanApplyDirectWithCapacity(tool, (GameItem)(object)val, out var reason))
				{
					flag = true;
					Log.Warn("direct batch stopped (strict): " + SafeId((GameItem)(object)val) + " " + reason);
					break;
				}
				try
				{
					ItemBehaviourManager.current.Target(tool, (GameItem)(object)val);
					if (tool.MayTarget((GameItem)(object)val) && tool.CanTarget((GameItem)(object)val))
					{
						tool.Target((GameItem)(object)val);
					}
					num++;
				}
				catch (Exception ex)
				{
					num2++;
					Log.Warn("direct batch target failed: " + SafeId((GameItem)(object)val) + " " + ex.Message);
				}
			}
		}
		catch (Exception ex2)
		{
			Log.Warn("direct batch failed: " + ex2.Message);
		}
		Log.Debug("direct batch: tool=" + DisplayName(tool) + " applied=" + num + " skipped=" + num2 + (flag ? " stopped=strict" : ""));
		if (num <= 0)
		{
			return;
		}
		try
		{
			PlayFeedback();
		}
		catch
		{
		}
	}

	private static void ExecuteAquaScanBatch(List<GameItemElement> selected)
	{
		if (selected == null || selected.Count == 0)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		try
		{
			for (int i = 0; i < selected.Count; i++)
			{
				GameItemElement val = selected[i];
				if (val == null || val.IsDestroyed())
				{
					num2++;
					continue;
				}
				if (((GameItem)val).parentInventory == null)
				{
					num2++;
					continue;
				}
				bool flag = false;
				try
				{
					flag = WaterScannerUIManager.CanMatch((GameItem)(object)val);
				}
				catch
				{
				}
				if (!flag)
				{
					num2++;
					continue;
				}
				try
				{
					WaterFeatureHelper.OnWaterLabelUpdated((GameItem)(object)val);
					num++;
				}
				catch (Exception ex)
				{
					num2++;
					Log.Warn("aquascan label failed: " + SafeId((GameItem)(object)val) + " " + ex.Message);
				}
			}
		}
		catch (Exception ex2)
		{
			Log.Warn("aquascan batch failed: " + ex2.Message);
		}
		Log.Debug("aquascan batch: applied=" + num + " skipped=" + num2);
		if (num <= 0)
		{
			return;
		}
		try
		{
			AudioManager.Instance.Play3("scanner_activate");
		}
		catch
		{
		}
	}

	private static void ExecuteLiquidContainerBatch(GameItem tool, List<GameItemElement> selected)
	{
		if (tool == null || selected == null || selected.Count == 0)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		try
		{
			for (int i = 0; i < selected.Count; i++)
			{
				GameItemElement val = selected[i];
				if (val == null || val.IsDestroyed())
				{
					num2++;
					continue;
				}
				if (((GameItem)val).parentInventory == null)
				{
					num2++;
					continue;
				}
				if ((object)val == tool)
				{
					num2++;
					continue;
				}
				if (!GeneralHelper.IsItemEffectivelyOwned((GameItem)(object)val))
				{
					num2++;
					continue;
				}
				bool flag = false;
				bool flag2 = false;
				try
				{
					flag = tool.MayTarget((GameItem)(object)val);
					flag2 = flag && tool.CanTarget((GameItem)(object)val);
				}
				catch
				{
				}
				if (!flag || !flag2)
				{
					num2++;
					continue;
				}
				int num3 = SafeVolume(tool);
				int num4 = SafeVolume((GameItem)(object)val);
				try
				{
					ItemBehaviourManager.current.Target(tool, (GameItem)(object)val);
					if (tool.MayTarget((GameItem)(object)val) && tool.CanTarget((GameItem)(object)val))
					{
						tool.Target((GameItem)(object)val);
					}
					int num5 = SafeVolume(tool);
					int num6 = SafeVolume((GameItem)(object)val);
					if (num5 != num3 || num6 != num4)
					{
						num++;
					}
					else
					{
						num2++;
					}
				}
				catch (Exception ex)
				{
					num2++;
					Log.Warn("liquid batch target failed: " + SafeId((GameItem)(object)val) + " " + ex.Message);
				}
			}
		}
		catch (Exception ex2)
		{
			Log.Warn("liquid batch failed: " + ex2.Message);
		}
		Log.Debug("liquid batch: tool=" + DisplayName(tool) + " applied=" + num + " skipped=" + num2);
		if (num <= 0)
		{
			return;
		}
		try
		{
			PlayFeedback();
		}
		catch
		{
		}
	}

	private static int SafeVolume(GameItem item)
	{
		try
		{
			return WaterHelper.GetTotalVolume(item);
		}
		catch
		{
			return 0;
		}
	}

	private static bool CanApplyDirectWithCapacity(GameItem tool, GameItem target, out string reason)
	{
		reason = "";
		try
		{
			if (IsNoProductDirectTool(tool))
			{
				return true;
			}
			if (tool.IsID("surgery_tool"))
			{
				reason = "surgery outputs are variable";
				return false;
			}
			if (tool.IsTag("BLADE_ITEM_TAG"))
			{
				if (target.IsTag("ANIMAL_ITEM_TAG"))
				{
					if (!target.IsTag("ANIMAL_IS_DEAD_TAG"))
					{
						return true;
					}
					reason = "butcher outputs are variable";
					return false;
				}
				if (target.IsTag("FISH_ITEM_TAG"))
				{
					if (!AquacultureHelper.IsDead(target))
					{
						return true;
					}
					reason = "fillet outputs are variable";
					return false;
				}
				return true;
			}
			if (!TryDirectProducts(tool, target, out var products, out reason))
			{
				return false;
			}
			if (products == null || products.Count == 0)
			{
				return true;
			}
			GameInventory parentInventory = target.parentInventory;
			GameGridInventory val = (GameGridInventory)(object)((parentInventory is GameGridInventory) ? parentInventory : null);
			if (val == null)
			{
				reason = "product target is not a grid inventory";
				return false;
			}
			int num = FreeCells(val);
			int num2 = ProductCells(products);
			if (num2 < 0)
			{
				reason = "could not measure products";
				return false;
			}
			if (num < num2)
			{
				reason = "free cells " + num + " < products " + num2;
				return false;
			}
			if (!CanPlaceAllProducts(val, products))
			{
				reason = "products cannot all be packed (free=" + num + " need=" + num2 + ")";
				return false;
			}
			return true;
		}
		catch (Exception ex)
		{
			reason = "capacity check failed: " + ex.Message;
			return false;
		}
	}

	private static bool TryDirectProducts(GameItem tool, GameItem target, out List<string> products, out string reason)
	{
		products = new List<string>();
		reason = "";
		try
		{
			if (tool.IsID("screwdriver"))
			{
				if (target.IsTag("ADV_GUN_TAG"))
				{
					reason = "gun contents/products are variable";
					return false;
				}
				if (target.IsID("system_module_ruined"))
				{
					products.Add("common_electronic");
					products.Add("printer_plastic");
					products.Add("junk");
					return true;
				}
				if (target.IsTag("BROKEN_MACHINE_TAG"))
				{
					int num = target.GetTagReadonly("BROKEN_MACHINE_SCREW_COUNT").GetInt();
					for (int i = 0; i < num; i++)
					{
						products.Add("nuts_metal");
					}
					return true;
				}
				return true;
			}
			if (tool.IsID("welder") && target.IsTag("BROKEN_MACHINE_TAG"))
			{
				int num2 = target.GetTagReadonly("BROKEN_MACHINE_WELD_COUNT").GetInt();
				for (int j = 0; j < num2; j++)
				{
					products.Add("scrap_metal");
				}
				return true;
			}
			if (tool.IsID("wire_cutter") && target.IsTag("BROKEN_MACHINE_TAG"))
			{
				int num3 = target.GetTagReadonly("BROKEN_MACHINE_WIRE_COUNT").GetInt();
				for (int k = 0; k < num3; k++)
				{
					products.Add("wire");
				}
				return true;
			}
			return true;
		}
		catch (Exception ex)
		{
			reason = "product estimate failed: " + ex.Message;
			return false;
		}
	}

	private static bool CanPlaceAllProducts(GameGridInventory grid, List<string> ids)
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		if (grid == null || grid.inventoryShape == null)
		{
			return false;
		}
		int width = grid.inventoryShape.width;
		int height = grid.inventoryShape.height;
		bool[,] reserved = new bool[width, height];
		for (int i = 0; i < ids.Count; i++)
		{
			GameItem val = null;
			try
			{
				val = DirectoryMaster.Item(ids[i], true);
				if (val == null || val.modifiedShape == null)
				{
					return false;
				}
				GridShape modifiedShape = val.modifiedShape;
				bool flag = false;
				for (int j = 0; j + modifiedShape.height <= height; j++)
				{
					if (flag)
					{
						break;
					}
					for (int k = 0; k + modifiedShape.width <= width; k++)
					{
						if (ShadowFits(reserved, modifiedShape, k, j))
						{
							GridShape val2 = new GridShapeBuilder(modifiedShape).SetPosition(k, j).Build();
							SlotMarker val3 = ((GameInventory)grid).TryInventorySlot(val, 1, new Vector2(float.NegativeInfinity, float.NegativeInfinity), val2, (TagSystem)null);
							if (val3 != null && val3.numTransfer > 0)
							{
								MarkShadow(reserved, modifiedShape, k, j);
								flag = true;
								break;
							}
						}
					}
				}
				if (!flag)
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			finally
			{
				try
				{
					if (val != null)
					{
						val.Destroy();
					}
				}
				catch
				{
				}
			}
		}
		return true;
	}

	private static bool ShadowFits(bool[,] reserved, GridShape shape, int x, int y)
	{
		for (int i = 0; i < shape.height; i++)
		{
			for (int j = 0; j < shape.width; j++)
			{
				if (shape.Get(j, i) != 0)
				{
					int num = x + j;
					int num2 = y + i;
					if (num < 0 || num2 < 0 || num >= reserved.GetLength(0) || num2 >= reserved.GetLength(1) || reserved[num, num2])
					{
						return false;
					}
				}
			}
		}
		return true;
	}

	private static void MarkShadow(bool[,] reserved, GridShape shape, int x, int y)
	{
		for (int i = 0; i < shape.height; i++)
		{
			for (int j = 0; j < shape.width; j++)
			{
				if (shape.Get(j, i) != 0)
				{
					int num = x + j;
					int num2 = y + i;
					if (num >= 0 && num2 >= 0 && num < reserved.GetLength(0) && num2 < reserved.GetLength(1))
					{
						reserved[num, num2] = true;
					}
				}
			}
		}
	}

	private static int ProductCells(List<string> ids)
	{
		int num = 0;
		for (int i = 0; i < ids.Count; i++)
		{
			GameItem val = null;
			try
			{
				val = DirectoryMaster.Item(ids[i], true);
				if (val == null || val.modifiedShape == null)
				{
					return -1;
				}
				num += ShapeCells(val.modifiedShape);
			}
			catch
			{
				return -1;
			}
			finally
			{
				try
				{
					if (val != null)
					{
						val.Destroy();
					}
				}
				catch
				{
				}
			}
		}
		return num;
	}

	private static int ShapeCells(GridShape shape)
	{
		if (shape == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < shape.height; i++)
		{
			for (int j = 0; j < shape.width; j++)
			{
				if (shape.Get(j, i) != 0)
				{
					num++;
				}
			}
		}
		return num;
	}

	private static int FreeCells(GameGridInventory grid)
	{
		if (grid == null || grid.inventoryShape == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < grid.inventoryShape.height; i++)
		{
			for (int j = 0; j < grid.inventoryShape.width; j++)
			{
				if (grid.inventoryShape.Get(j, i) != 0)
				{
					continue;
				}
				bool flag = false;
				for (int k = 0; k < ((GameInventory)grid).childItems.Count; k++)
				{
					GameItem val = ((GameInventory)grid).childItems[k];
					GridShape val2 = ((val != null) ? val.modifiedShape : null);
					if (val2 == null)
					{
						continue;
					}
					try
					{
						if (val2.Get(j, i) != 0)
						{
							flag = true;
							break;
						}
					}
					catch
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					num++;
				}
			}
		}
		return num;
	}

	private static void OpenQueuedBatch()
	{
		BatchPlan queuedBatch = _queuedBatch;
		_queuedBatch = null;
		if (queuedBatch == null)
		{
			return;
		}
		try
		{
			LabelerUIManager instance = LabelerUIManager.Instance;
			if (!((Object)(object)instance == (Object)null) && !((Object)(object)instance.panel == (Object)null) && !instance.panel.activeSelf)
			{
				GameItemElement representative = queuedBatch.Representative;
				if (representative != null && !representative.IsDestroyed() && ((GameItem)representative).parentInventory != null && GeneralHelper.IsItemEffectivelyOwned((GameItem)(object)representative) && LabelerHelper.GetEditableLabelCount((GameItem)(object)representative) > 0)
				{
					_pendingBatch = queuedBatch;
					instance.OpenUI((GameItem)(object)representative);
					_pendingBatch = null;
					Log.Debug("batch panel opened: " + queuedBatch.Category + " x" + queuedBatch.Items.Count);
				}
			}
		}
		catch (Exception ex)
		{
			_pendingBatch = null;
			Log.Warn("batch open failed: " + ex.Message);
		}
	}

	private static void QueueBlueBatch(List<GameItemElement> selected)
	{
		if (!ModSettings.BatchBlueLabeler)
		{
			return;
		}
		BlueBatchPlan blueBatchPlan = PickBlueBatch(selected);
		if (blueBatchPlan != null)
		{
			LabelerBlueUIManager instance = LabelerBlueUIManager.Instance;
			if (!((Object)(object)instance == (Object)null) && !((Object)(object)instance.panel == (Object)null) && !instance.panel.activeSelf)
			{
				_queuedBlueBatch = blueBatchPlan;
				LetterboxManager.RunNextUpdate((Action)OpenQueuedBlueBatch);
				Log.Debug("blue batch planned: " + blueBatchPlan.ItemId + " x" + blueBatchPlan.Items.Count);
			}
		}
	}

	private static void OpenQueuedBlueBatch()
	{
		BlueBatchPlan queuedBlueBatch = _queuedBlueBatch;
		_queuedBlueBatch = null;
		if (queuedBlueBatch == null)
		{
			return;
		}
		try
		{
			LabelerBlueUIManager instance = LabelerBlueUIManager.Instance;
			if (!((Object)(object)instance == (Object)null) && !((Object)(object)instance.panel == (Object)null) && !instance.panel.activeSelf)
			{
				GameItemElement representative = queuedBlueBatch.Representative;
				if (representative != null && !representative.IsDestroyed() && ((GameItem)representative).parentInventory != null && GeneralHelper.IsItemEffectivelyOwned((GameItem)(object)representative) && !(((GameItem)representative).identifier == "dossier"))
				{
					_pendingBlueBatch = queuedBlueBatch;
					instance.OpenUI((GameItem)(object)representative);
					_pendingBlueBatch = null;
					Log.Debug("blue batch panel opened: " + queuedBlueBatch.ItemId + " x" + queuedBlueBatch.Items.Count);
				}
			}
		}
		catch (Exception ex)
		{
			_pendingBlueBatch = null;
			Log.Warn("blue batch open failed: " + ex.Message);
		}
	}

	private static BlueBatchPlan PickBlueBatch(List<GameItemElement> selected)
	{
		if (selected == null || selected.Count <= 1)
		{
			return null;
		}
		Dictionary<string, List<GameItemElement>> dictionary = new Dictionary<string, List<GameItemElement>>();
		List<string> list = new List<string>();
		for (int i = 0; i < selected.Count; i++)
		{
			GameItemElement val = selected[i];
			if (val == null || val.IsDestroyed() || ((GameItem)val).parentInventory == null || !GeneralHelper.IsItemEffectivelyOwned((GameItem)(object)val))
			{
				continue;
			}
			string identifier = ((GameItem)val).identifier;
			if (!string.IsNullOrEmpty(identifier) && !(identifier == "dossier"))
			{
				if (!dictionary.TryGetValue(identifier, out var value))
				{
					value = (dictionary[identifier] = new List<GameItemElement>());
					list.Add(identifier);
				}
				value.Add(val);
			}
		}
		string text = null;
		int num = 0;
		for (int j = 0; j < list.Count; j++)
		{
			List<GameItemElement> list3 = dictionary[list[j]];
			if (list3.Count > num)
			{
				text = list[j];
				num = list3.Count;
			}
		}
		if (text == null || num <= 1)
		{
			return null;
		}
		List<GameItemElement> list4 = dictionary[text];
		BlueBatchPlan blueBatchPlan = new BlueBatchPlan
		{
			ItemId = text,
			Representative = list4[0]
		};
		HashSet<GameItemElement> hashSet = new HashSet<GameItemElement>();
		for (int k = 0; k < list4.Count; k++)
		{
			if (hashSet.Add(list4[k]))
			{
				blueBatchPlan.Items.Add(list4[k]);
			}
		}
		return blueBatchPlan;
	}

	private static void ApplyBlueBatch(BlueBatchPlan batch, string text)
	{
		if (batch == null || batch.Items == null)
		{
			return;
		}
		int num = 0;
		for (int i = 0; i < batch.Items.Count; i++)
		{
			GameItemElement val = batch.Items[i];
			if (val == null || val.IsDestroyed() || ((GameItem)val).parentInventory == null || !GeneralHelper.IsItemEffectivelyOwned((GameItem)(object)val) || ((GameItem)val).identifier != batch.ItemId)
			{
				continue;
			}
			try
			{
				if (string.IsNullOrEmpty(text))
				{
					GeneralHelper.RemoveCustomName((GameItem)(object)val);
				}
				else
				{
					GeneralHelper.SetCustomName((GameItem)(object)val, text);
				}
				RefreshBlueTitle((GameItem)(object)val);
				num++;
			}
			catch (Exception ex)
			{
				Log.Warn("blue batch apply failed on " + SafeId((GameItem)(object)val) + ": " + ex.Message);
			}
		}
		Log.Debug("blue batch apply: " + batch.ItemId + " x" + num);
	}

	private static void RefreshBlueTitle(GameItem item)
	{
		try
		{
			if (item != null && item.contentWindow != null && item.contentWindow.IsVisible())
			{
				item.contentWindow.SetTitle(item.GetDisplayName(true));
			}
		}
		catch
		{
		}
	}

	private static void RememberBlue(string itemId, string text)
	{
		if (!ModSettings.BlueLabelerMemory || string.IsNullOrEmpty(itemId))
		{
			return;
		}
		if (string.IsNullOrEmpty(text))
		{
			if (_blueMemoryItemId == itemId)
			{
				_blueMemoryItemId = null;
				_blueMemoryText = null;
			}
		}
		else
		{
			_blueMemoryItemId = itemId;
			_blueMemoryText = text;
		}
	}

	public static void ClearBlueMemory()
	{
		_blueMemoryItemId = null;
		_blueMemoryText = null;
	}

	public static void OnBluePanelOpening(GameItem item)
	{
		if (!ModSettings.BatchBlueLabeler && !ModSettings.BlueLabelerMemory)
		{
			_pendingBlueBatch = null;
			_blueBatch = null;
			_blueCurrentItem = null;
		}
		else
		{
			_blueCurrentItem = item;
			BlueBatchPlan pendingBlueBatch = _pendingBlueBatch;
			_pendingBlueBatch = null;
			_blueBatch = ((pendingBlueBatch != null && (object)pendingBlueBatch.Representative == item) ? pendingBlueBatch : null);
		}
	}

	public static void OnBluePanelOpened(LabelerBlueUIManager mgr, GameItem item)
	{
		try
		{
			if (ModSettings.BlueLabelerMemory && !((Object)(object)mgr == (Object)null) && !((Object)(object)mgr.inputField == (Object)null) && item != null && !GeneralHelper.HasCustomName(item) && !string.IsNullOrEmpty(_blueMemoryText) && !(_blueMemoryItemId != item.identifier))
			{
				mgr.inputField.text = _blueMemoryText;
				mgr.inputField.caretPosition = _blueMemoryText.Length;
			}
		}
		catch (Exception ex)
		{
			Log.Debug("blue memory prefill failed: " + ex.Message);
		}
	}

	public static void OnBlueEditEnd(LabelerBlueUIManager mgr)
	{
		try
		{
			if ((ModSettings.BatchBlueLabeler || ModSettings.BlueLabelerMemory) && !((Object)(object)mgr == (Object)null) && !((Object)(object)mgr.inputField == (Object)null))
			{
				string text = mgr.inputField.text;
				BlueBatchPlan blueBatch = _blueBatch;
				_blueBatch = null;
				if (blueBatch != null)
				{
					ApplyBlueBatch(blueBatch, text);
				}
				GameItem blueCurrentItem = _blueCurrentItem;
				if (blueCurrentItem != null)
				{
					RememberBlue(blueCurrentItem.identifier, text);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("blue edit hook failed: " + ex.Message);
		}
	}

	public static void OnBlueClear(LabelerBlueUIManager mgr)
	{
		try
		{
			if (ModSettings.BatchBlueLabeler || ModSettings.BlueLabelerMemory)
			{
				BlueBatchPlan blueBatch = _blueBatch;
				_blueBatch = null;
				if (blueBatch != null)
				{
					ApplyBlueBatch(blueBatch, null);
				}
				GameItem blueCurrentItem = _blueCurrentItem;
				if (blueCurrentItem != null && _blueMemoryItemId == blueCurrentItem.identifier)
				{
					ClearBlueMemory();
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("blue clear hook failed: " + ex.Message);
		}
	}

	public static void OnBlueClosing()
	{
		_blueBatch = null;
		_blueCurrentItem = null;
	}

	public static void OnBlueExternalOpen()
	{
		_blueBatch = null;
		_pendingBlueBatch = null;
		_blueCurrentItem = null;
	}

	private static BatchPlan PickSelectionBatch(GameItem item)
	{
		if (!QolSelectionOwned)
		{
			return null;
		}
		List<GameItemElement> selected = Selection.GetSelected();
		if (selected == null || selected.Count <= 1)
		{
			return null;
		}
		GameItemElement val = (GameItemElement)(object)((item is GameItemElement) ? item : null);
		if (val == null || !selected.Contains(val))
		{
			return null;
		}
		return new BatchPlan
		{
			Category = null,
			Items = selected,
			Representative = val,
			FromSelection = true
		};
	}

	private static List<GameItemElement> RefreshBatchItems(BatchPlan batch)
	{
		List<GameItemElement> list = new List<GameItemElement>();
		if (batch == null)
		{
			return list;
		}
		List<GameItemElement> items = batch.Items;
		if (items != null)
		{
			for (int i = 0; i < items.Count; i++)
			{
				GameItemElement val = items[i];
				if (val != null && !val.IsDestroyed() && ((GameItem)val).parentInventory != null && (string.IsNullOrEmpty(batch.Category) || GetEditableCategories(val).Contains(batch.Category)) && !list.Contains(val))
				{
					list.Add(val);
				}
			}
		}
		List<GameItemElement> selected = Selection.GetSelected();
		Log.Debug("labeler batch: refresh category=" + (string.IsNullOrEmpty(batch.Category) ? "*" : batch.Category) + " snapshot=" + (items?.Count ?? 0) + " live=" + selected.Count + " kept=" + list.Count);
		return list;
	}

	private static BatchPlan PickPlan(List<GameItemElement> selected)
	{
		if (selected == null || selected.Count == 0)
		{
			return null;
		}
		Dictionary<string, List<GameItemElement>> dictionary = new Dictionary<string, List<GameItemElement>>();
		List<string> list = new List<string>();
		Dictionary<GameItemElement, List<string>> dictionary2 = new Dictionary<GameItemElement, List<string>>();
		for (int i = 0; i < selected.Count; i++)
		{
			GameItemElement val = selected[i];
			if (val == null || val.IsDestroyed() || ((GameItem)val).parentInventory == null || !GeneralHelper.IsItemEffectivelyOwned((GameItem)(object)val))
			{
				continue;
			}
			List<string> editableCategories = GetEditableCategories(val);
			if (editableCategories.Count == 0)
			{
				continue;
			}
			dictionary2[val] = editableCategories;
			for (int j = 0; j < editableCategories.Count; j++)
			{
				string text = editableCategories[j];
				if (!dictionary.TryGetValue(text, out var value))
				{
					value = (dictionary[text] = new List<GameItemElement>());
					list.Add(text);
				}
				value.Add(val);
			}
		}
		string text2 = null;
		int num = 0;
		for (int k = 0; k < list.Count; k++)
		{
			List<GameItemElement> list3 = dictionary[list[k]];
			if (list3.Count > num)
			{
				num = list3.Count;
				text2 = list[k];
			}
		}
		if (text2 == null)
		{
			return null;
		}
		List<GameItemElement> list4 = dictionary[text2];
		GameItemElement val2 = null;
		int num2 = int.MaxValue;
		for (int l = 0; l < list4.Count; l++)
		{
			GameItemElement val3 = list4[l];
			if (dictionary2.TryGetValue(val3, out var value2) && value2.Count < num2)
			{
				num2 = value2.Count;
				val2 = val3;
			}
		}
		if (val2 == null)
		{
			val2 = list4[0];
		}
		if (val2 == null || string.IsNullOrEmpty(((GameItem)val2).identifier))
		{
			return null;
		}
		Log.Debug("labeler batch: plan category=" + text2 + " items=" + list4.Count + " rep=" + DisplayName((GameItem)(object)val2));
		return new BatchPlan
		{
			Category = text2,
			Items = list4,
			Representative = val2
		};
	}

	private static List<string> GetEditableCategories(GameItemElement element)
	{
		List<string> list = new List<string>();
		if (element == null || ((GameItem)element).itemFeatures == null)
		{
			return list;
		}
		HashSet<string> hashSet = new HashSet<string>();
		for (int i = 0; i < ((GameItem)element).itemFeatures.Count; i++)
		{
			ItemFeature val = ((GameItem)element).itemFeatures[i];
			if (val == null)
			{
				continue;
			}
			try
			{
				if (LabelerHelper.IsEditableLabelFeature(val))
				{
					string category = val.category;
					if (!string.IsNullOrEmpty(category) && hashSet.Add(category))
					{
						list.Add(category);
					}
				}
			}
			catch
			{
			}
		}
		return list;
	}

	public static void OnPanelOpening(LabelerUIManager mgr, GameItem item)
	{
		try
		{
			if ((!ModSettings.BatchLabeler && !ModSettings.LabelerMemory) || (Object)(object)mgr == (Object)null || item == null)
			{
				return;
			}
			if ((Object)(object)mgr.panel != (Object)null && mgr.panel.activeSelf && _session != null && _session.Item == item)
			{
				_session.OpenFrame = Time.frameCount;
				return;
			}
			if (_session != null)
			{
				CommitSession(mgr);
			}
			BatchPlan batchPlan = _pendingBatch;
			_pendingBatch = null;
			if (batchPlan == null)
			{
				batchPlan = PickSelectionBatch(item);
			}
			_session = new PanelSession
			{
				Item = item,
				Baseline = CaptureBaseline(item),
				BaselineReady = false,
				Batch = batchPlan,
				OpenFrame = Time.frameCount
			};
		}
		catch (Exception ex)
		{
			_pendingBatch = null;
			Log.Warn("panel open hook failed: " + ex.Message);
		}
	}

	public static void OnPanelClosing(LabelerUIManager mgr)
	{
		try
		{
			if (!((Object)(object)mgr == (Object)null) && !((Object)(object)mgr.panel == (Object)null) && mgr.panel.activeSelf)
			{
				CommitSession(mgr);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("panel close hook failed: " + ex.Message);
		}
	}

	public static void OnPanelOpened(LabelerUIManager mgr, GameItem item)
	{
		try
		{
			if ((ModSettings.BatchLabeler || ModSettings.LabelerMemory) && !((Object)(object)mgr == (Object)null) && !((Object)(object)mgr.panel == (Object)null) && mgr.panel.activeSelf)
			{
				PanelSession session = _session;
				if (session != null && session.Item == item && !session.BaselineReady)
				{
					EnsureUiBaseline(mgr, session);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("labeler opened hook failed: " + ex.Message);
		}
	}

	public static void OnDropdownChanged(LabelerElement element)
	{
		try
		{
			if ((Object)(object)element == (Object)null || !_gameReady || (!ModSettings.BatchLabeler && !ModSettings.LabelerMemory))
			{
				return;
			}
			PanelSession session = _session;
			if (session == null || Time.frameCount == session.OpenFrame || !_session.BaselineReady)
			{
				return;
			}
			LabelerUIManager instance = LabelerUIManager.Instance;
			if ((Object)(object)instance == (Object)null || (Object)(object)instance.panel == (Object)null || !instance.panel.activeSelf)
			{
				return;
			}
			if (ModSettings.BatchLabeler && session.Batch != null)
			{
				string category = element.GetCategory();
				string selectedConditionIdentifier = element.GetSelectedConditionIdentifier();
				if ((string.IsNullOrEmpty(session.Batch.Category) || session.Batch.Category == category) && !string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(selectedConditionIdentifier))
				{
					session.Batch.Items = RefreshBatchItems(session.Batch);
					Dictionary<string, string> diff = new Dictionary<string, string> { { category, selectedConditionIdentifier } };
					ApplyMany(session.Batch.Items, diff, "live");
					session.LiveApplied = true;
				}
			}
			if (ModSettings.LabelerMemory)
			{
				Dictionary<string, string> dictionary = ComputeDiff(instance, session.Baseline, (session.Batch != null) ? session.Batch.Category : null);
				if (dictionary.Count > 0)
				{
					_memory = BuildMemory(session.Item, dictionary);
				}
				else
				{
					_memory = _lastCommitted;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("live memory failed: " + ex.Message);
		}
	}

	private static bool EnsureUiBaseline(LabelerUIManager mgr, PanelSession session)
	{
		if (session == null || session.BaselineReady)
		{
			return true;
		}
		if ((Object)(object)mgr == (Object)null || (Object)(object)mgr.panel == (Object)null)
		{
			return false;
		}
		Dictionary<string, string> dictionary = ReadRows(mgr);
		if (dictionary.Count == 0)
		{
			return false;
		}
		session.Baseline = dictionary;
		session.BaselineReady = true;
		return true;
	}

	private static Dictionary<string, string> ComputeDiff(LabelerUIManager mgr, Dictionary<string, string> baseline, string onlyCategory)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		if (baseline == null)
		{
			return dictionary;
		}
		if (!_gameReady)
		{
			return dictionary;
		}
		try
		{
			foreach (KeyValuePair<string, string> item in ReadRows(mgr))
			{
				if ((string.IsNullOrEmpty(onlyCategory) || !(item.Key != onlyCategory)) && baseline.TryGetValue(item.Key, out var value) && !(value == item.Value))
				{
					dictionary[item.Key] = item.Value;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("diff failed: " + ex.Message);
		}
		return dictionary;
	}

	private static Dictionary<string, string> ComputeBatchTargetDiff(LabelerUIManager mgr, PanelSession session)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		if (session == null || session.Batch == null)
		{
			return dictionary;
		}
		foreach (KeyValuePair<string, string> item in ReadRows(mgr))
		{
			if ((string.IsNullOrEmpty(session.Batch.Category) || !(item.Key != session.Batch.Category)) && BatchLabelDiffers(session.Batch, session.Item, item.Key, item.Value))
			{
				dictionary[item.Key] = item.Value;
			}
		}
		return dictionary;
	}

	private static bool BatchLabelDiffers(BatchPlan batch, GameItem current, string category, string targetId)
	{
		if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(targetId))
		{
			return false;
		}
		if (ItemLabelDiffers(current, category, targetId))
		{
			return true;
		}
		if (batch == null || batch.Items == null)
		{
			return false;
		}
		for (int i = 0; i < batch.Items.Count; i++)
		{
			if (ItemLabelDiffers((GameItem)(object)batch.Items[i], category, targetId))
			{
				return true;
			}
		}
		return false;
	}

	private static bool ItemLabelDiffers(GameItem item, string category, string targetId)
	{
		if (item == null || item.itemFeatures == null)
		{
			return false;
		}
		for (int i = 0; i < item.itemFeatures.Count; i++)
		{
			ItemFeature val = item.itemFeatures[i];
			if (val == null || val.category != category)
			{
				continue;
			}
			try
			{
				if (LabelerHelper.IsEditableLabelFeature(val))
				{
					ItemCondition currentItemCondition = val.GetCurrentItemCondition();
					if (currentItemCondition != null)
					{
						return currentItemCondition.identifier != targetId;
					}
				}
			}
			catch
			{
			}
		}
		return false;
	}

	private static Memory BuildMemory(GameItem item, Dictionary<string, string> diff)
	{
		if (item == null || string.IsNullOrEmpty(item.identifier))
		{
			return null;
		}
		if (diff == null || diff.Count == 0)
		{
			return null;
		}
		return new Memory
		{
			ItemId = item.identifier,
			ItemName = SafeDisplayName(item),
			Labels = new Dictionary<string, string>(diff)
		};
	}

	private static void CommitSession(LabelerUIManager mgr)
	{
		PanelSession session = _session;
		_session = null;
		if (session == null)
		{
			return;
		}
		if (!EnsureUiBaseline(mgr, session))
		{
			if (ModSettings.LabelerMemory)
			{
				_memory = _lastCommitted;
			}
			return;
		}
		if (session.Batch != null)
		{
			if (session.Batch.FromSelection && !QolSelectionOwned)
			{
				session.Batch = null;
			}
			else
			{
				session.Batch.Items = RefreshBatchItems(session.Batch);
			}
		}
		Dictionary<string, string> dictionary = ComputeDiff(mgr, session.Baseline, (session.Batch != null) ? session.Batch.Category : null);
		if (dictionary.Count == 0 && session.Batch != null)
		{
			dictionary = ComputeBatchTargetDiff(mgr, session);
		}
		if (session.Batch != null)
		{
			Log.Debug("labeler batch: commit item=" + DisplayName(session.Item) + " items=" + session.Batch.Items.Count + " category=" + (session.Batch.Category ?? "*") + " diff=" + dictionary.Count);
		}
		if (dictionary.Count == 0)
		{
			if (ModSettings.LabelerMemory)
			{
				_memory = _lastCommitted;
			}
			return;
		}
		if (ModSettings.LabelerMemory)
		{
			Memory memory = BuildMemory(session.Item, dictionary);
			if (memory != null)
			{
				_memory = memory;
				_lastCommitted = memory;
				Log.Debug("memory: " + memory.ItemId + " / " + dictionary.Count + " label(s)");
			}
		}
		if (session.Batch != null && !session.LiveApplied)
		{
			try
			{
				ApplyTo(session.Item, dictionary);
			}
			catch (Exception ex)
			{
				Log.Warn("batch representative apply failed: " + ex.Message);
			}
			ApplyMany(session.Batch.Items, dictionary, "batch");
		}
	}

	private static Dictionary<string, string> CaptureBaseline(GameItem item)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		if (item == null || item.itemFeatures == null)
		{
			return dictionary;
		}
		for (int i = 0; i < item.itemFeatures.Count; i++)
		{
			ItemFeature val = item.itemFeatures[i];
			if (val == null)
			{
				continue;
			}
			try
			{
				if (LabelerHelper.IsEditableLabelFeature(val))
				{
					ItemCondition currentItemCondition = val.GetCurrentItemCondition();
					if (currentItemCondition != null && !string.IsNullOrEmpty(currentItemCondition.identifier))
					{
						dictionary[val.category] = currentItemCondition.identifier;
					}
				}
			}
			catch
			{
			}
		}
		return dictionary;
	}

	private static Dictionary<string, string> ReadRows(LabelerUIManager mgr)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		if ((Object)(object)mgr == (Object)null || (Object)(object)mgr.verticalLayoutGroup == (Object)null)
		{
			return dictionary;
		}
		Transform transform = mgr.verticalLayoutGroup.transform;
		for (int i = 0; i < transform.childCount; i++)
		{
			Transform child = transform.GetChild(i);
			if ((Object)(object)child == (Object)null)
			{
				continue;
			}
			LabelerElement component = ((Component)child).GetComponent<LabelerElement>();
			if ((Object)(object)component == (Object)null)
			{
				continue;
			}
			try
			{
				string category = component.GetCategory();
				string selectedConditionIdentifier = component.GetSelectedConditionIdentifier();
				if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(selectedConditionIdentifier))
				{
					dictionary[category] = selectedConditionIdentifier;
				}
			}
			catch (Exception ex)
			{
				Log.Warn("read row failed: " + ex.Message);
			}
		}
		return dictionary;
	}

	private static void ApplyMany(List<GameItemElement> items, Dictionary<string, string> diff, string context)
	{
		if (items == null || diff == null || diff.Count == 0)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < items.Count; i++)
		{
			GameItemElement val = items[i];
			if (val != null && !val.IsDestroyed() && GeneralHelper.IsItemEffectivelyOwned((GameItem)(object)val))
			{
				int num3 = ApplyTo((GameItem)(object)val, diff);
				if (num3 > 0)
				{
					num += num3;
					num2++;
				}
			}
		}
		if (num > 0)
		{
			PlayFeedback();
			Log.Debug(context + ": " + num2 + " item(s), " + num + " label(s)");
		}
		else
		{
			Log.Debug(context + ": nothing to change");
		}
	}

	public static int ApplyTo(GameItem item, Dictionary<string, string> diff)
	{
		if (item == null || item.itemFeatures == null)
		{
			return 0;
		}
		if (diff == null || diff.Count == 0)
		{
			return 0;
		}
		if (!_gameReady)
		{
			return 0;
		}
		Dictionary<string, List<ItemCondition>> conditionLookup = GetConditionLookup();
		if (conditionLookup == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < item.itemFeatures.Count; i++)
		{
			ItemFeature val = item.itemFeatures[i];
			if (val == null)
			{
				continue;
			}
			try
			{
				if (!LabelerHelper.IsEditableLabelFeature(val) || !diff.TryGetValue(val.category, out var value) || string.IsNullOrEmpty(value) || !conditionLookup.TryGetValue(val.category, out var value2) || value2 == null)
				{
					continue;
				}
				int num2 = -1;
				for (int j = 0; j < value2.Count; j++)
				{
					if (value2[j] != null && value2[j].identifier == value)
					{
						num2 = j;
						break;
					}
				}
				if (num2 >= 0 && (val.IsExposed() || val.fakeCondition == null || !(val.fakeCondition.identifier == value)))
				{
					val.fakeCondition = value2[num2];
					val.SetExposed(false);
					WaterFeatureHelper.OnWaterLabelUpdated(val, num2);
					num++;
				}
			}
			catch (Exception ex)
			{
				Log.Warn("apply label failed on " + SafeId(item) + ": " + ex.Message);
			}
		}
		return num;
	}

	public static bool TryQuickApply(GameItem hovered, out int changed)
	{
		changed = 0;
		try
		{
			if (!_gameReady)
			{
				return false;
			}
			if (!ModSettings.LabelerMemory)
			{
				return false;
			}
			if (hovered == null)
			{
				return false;
			}
			GameItemElement val = (GameItemElement)(object)((hovered is GameItemElement) ? hovered : null);
			if (val != null && val.IsDestroyed())
			{
				return false;
			}
			if (_memory == null || _memory.Labels == null || _memory.Labels.Count == 0)
			{
				return false;
			}
			if (!GeneralHelper.IsItemEffectivelyOwned(hovered))
			{
				return false;
			}
			if (!HasSharedLabelCategory(hovered, _memory.Labels))
			{
				return false;
			}
			changed = ApplyTo(hovered, _memory.Labels);
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn("quick apply failed: " + ex.Message);
			return false;
		}
	}

	public static bool TryQuickApplyBlue(GameItem hovered, out int changed)
	{
		changed = 0;
		try
		{
			if (!ModSettings.BlueLabelerMemory)
			{
				return false;
			}
			if (hovered == null)
			{
				return false;
			}
			GameItemElement val = (GameItemElement)(object)((hovered is GameItemElement) ? hovered : null);
			if (val != null && val.IsDestroyed())
			{
				return false;
			}
			if (!GeneralHelper.IsItemEffectivelyOwned(hovered))
			{
				return false;
			}
			if (string.IsNullOrEmpty(_blueMemoryText))
			{
				return false;
			}
			if (_blueMemoryItemId != hovered.identifier)
			{
				return false;
			}
			GeneralHelper.SetCustomName(hovered, _blueMemoryText);
			RefreshBlueTitle(hovered);
			changed = 1;
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn("blue quick apply failed: " + ex.Message);
			return false;
		}
	}

	private static bool HasSharedLabelCategory(GameItem item, Dictionary<string, string> labels)
	{
		if (item == null || item.itemFeatures == null || labels == null)
		{
			return false;
		}
		for (int i = 0; i < item.itemFeatures.Count; i++)
		{
			ItemFeature val = item.itemFeatures[i];
			if (val == null || string.IsNullOrEmpty(val.category))
			{
				continue;
			}
			try
			{
				if (!LabelerHelper.IsEditableLabelFeature(val) || !labels.ContainsKey(val.category))
				{
					continue;
				}
				return true;
			}
			catch
			{
			}
		}
		return false;
	}

	public static void AppendMemoryTooltip(RichTextBuilder builder, GameItem item)
	{
		try
		{
			if (builder == null || item == null || !_gameReady || !ModSettings.LabelerMemory || !IsLabeler(item))
			{
				return;
			}
			Memory memory = _memory;
			if (memory == null || memory.Labels == null || memory.Labels.Count == 0)
			{
				return;
			}
			string value = Text.Get("tooltip_sep");
			StringBuilder stringBuilder = new StringBuilder();
			foreach (KeyValuePair<string, string> label in memory.Labels)
			{
				string text = CategoryDisplay(label.Key);
				string text2 = ConditionDisplay(label.Key, label.Value);
				if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(text2))
				{
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append(value);
					}
					stringBuilder.Append(Text.Get("tooltip_label", text, text2));
				}
			}
			if (stringBuilder.Length != 0)
			{
				builder.AddLine(Text.Get("tooltip_memory", stringBuilder.ToString()), true, (RenderHandler.ColorPalette)4367018, false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
			}
		}
		catch (Exception ex)
		{
			Log.Warn("memory tooltip failed: " + ex.Message);
		}
	}

	private static string CategoryDisplay(string category)
	{
		if (string.IsNullOrEmpty(category))
		{
			return null;
		}
		if (!_gameReady)
		{
			return category;
		}
		try
		{
			if (ItemFeatureList.CategoryNames != null && ItemFeatureList.CategoryNames.TryGetValue(category, out var value) && !string.IsNullOrEmpty(value))
			{
				return value;
			}
		}
		catch
		{
		}
		return category;
	}

	private static string ConditionDisplay(string category, string conditionId)
	{
		if (string.IsNullOrEmpty(conditionId))
		{
			return null;
		}
		if (!_gameReady)
		{
			return conditionId;
		}
		try
		{
			Dictionary<string, List<ItemCondition>> conditionLookup = GetConditionLookup();
			if (conditionLookup != null && conditionLookup.TryGetValue(category, out var value) && value != null)
			{
				for (int i = 0; i < value.Count; i++)
				{
					ItemCondition val = value[i];
					if (val != null && val.identifier == conditionId)
					{
						return string.IsNullOrEmpty(val.display) ? conditionId : val.display;
					}
				}
			}
		}
		catch
		{
		}
		return conditionId;
	}

	private static string SafeDisplayName(GameItem item)
	{
		try
		{
			string displayName = item.GetDisplayName(true);
			if (!string.IsNullOrEmpty(displayName))
			{
				return displayName;
			}
		}
		catch
		{
		}
		try
		{
			return item.name;
		}
		catch
		{
		}
		return null;
	}

	public static void PlayFeedback()
	{
		try
		{
			EmporiumAudioPlayer instance = EmporiumAudioPlayer.Instance;
			if ((Object)(object)instance != (Object)null && (Object)(object)instance.audioLabeler != (Object)null)
			{
				instance.audioLabeler.Play();
			}
		}
		catch
		{
		}
		try
		{
			if ((Object)(object)AchievementTracker.Instance != (Object)null)
			{
				AchievementTracker.Instance.CheckLowerLevelsSpecial();
			}
		}
		catch
		{
		}
	}

	public static void ResetTransient()
	{
		_session = null;
		_pendingBatch = null;
		_queuedBatch = null;
		_blueBatch = null;
		_pendingBlueBatch = null;
		_queuedBlueBatch = null;
		_blueCurrentItem = null;
		_armedTool = null;
		_armedFrame = -1;
		_bandTransparentFrame = -1;
		_qolSelectionOwned = false;
		_qolBandStarted = false;
	}

	public static void ClearMemory()
	{
		_memory = null;
		_lastCommitted = null;
	}

	public static void Dispose()
	{
		ResetTransient();
		_memory = null;
		_lastCommitted = null;
		SetGameReady(ready: false);
	}

	private static string SafeId(GameItem item)
	{
		try
		{
			return (item != null) ? item.identifier : "null";
		}
		catch
		{
			return "?";
		}
	}
}
