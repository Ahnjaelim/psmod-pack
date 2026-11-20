using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace PenLabelerAssist;

internal static class ChemBatch
{
	private sealed class Refs
	{
		public PixelWindow Window;

		public GameItem Machine;

		public GameSlotInventory Container;

		public GameGridInventory Output;

		public MachineryText Status;

		public MachineryButton OutputButton;

		public MachineryStack Control;

		public Action OriginalOutput;

		public Action<GameItem, GameInventory, SlotMarker> OriginalOnOneSecond;

		public Action<GameItem, GameInventory, SlotMarker> WrappedOnOneSecond;

		public MachinerySlider Slider;

		public MachineryButton MinusButton;

		public MachineryButton PlusButton;

		public MachineryText TargetLabel;

		public MachineryStack QuantityRow;

		public bool SliderShown;

		public bool RestoreToMax;

		// Quantity the player last picked with the slider or arrows (0 = none yet).
		// Kept across output clicks so the chosen batch size stays fixed.
		public int Preferred;

		public bool Installed;

		public bool RefreshScheduled;

		public bool LogScheduledTiming;

		public string LastRecipe;

		public int LastAvailable;

		public int LastValueWidth;

		public int LayoutWidth;
	}

	private const int StepperWidth = 10;

	private const int StepperHeight = 10;

	private const int RowGap = 4;

	private static readonly Dictionary<PixelWindow, Refs> Windows = new Dictionary<PixelWindow, Refs>();

	private static readonly Dictionary<MachineryButton, Action> Callbacks = new Dictionary<MachineryButton, Action>();

	private static readonly List<GameItem> NoIgnoredItems = new List<GameItem>();

	private static bool _batchTransferActive;

	private static GameItem _batchTransferContainer;

	private static int[] _batchTransferShadow;

	private static int _batchTransferConsumed;

	public static bool TryBatchLiquidTransfer(GameItem container, GameItem chemicalItem, int volumeInPart)
	{
		try
		{
			if (!_batchTransferActive || volumeInPart <= 0)
			{
				return false;
			}
			if (container == null || chemicalItem == null)
			{
				return false;
			}
			if (_batchTransferContainer != null && container != _batchTransferContainer)
			{
				return false;
			}
			int[] array = _batchTransferShadow;
			if (array == null)
			{
				array = WaterHelper.GetLiquidParts(container);
			}
			int num = 0;
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] > 0)
				{
					num += array[i];
				}
			}
			if (num < volumeInPart)
			{
				return false;
			}
			int[] array2 = WaterHelper.SplitProportionally(array, volumeInPart);
			List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
			for (int j = 0; j < array2.Length && j < Liquid.Liquids.Count; j++)
			{
				if (array2[j] > 0)
				{
					list.Add(new KeyValuePair<string, int>(Liquid.Liquids[j].identifier, array2[j]));
				}
			}
			if (list.Count == 0)
			{
				return false;
			}
			if (_batchTransferShadow != null)
			{
				for (int k = 0; k < array.Length; k++)
				{
					array[k] = Mathf.Max(0, array[k] - array2[k]);
				}
				_batchTransferConsumed += volumeInPart;
			}
			ChemicalHelper.AddLiquidToChemFromList(chemicalItem, list);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static void Reset()
	{
		Windows.Clear();
		Callbacks.Clear();
	}

	public static void CaptureWindow((PixelWindow, GameGridInventory, GameSlotInventory, GameGridInventory, MachineryButton, MachineryButton, MachineryText, MachineryText, MachineryStack) result)
	{
		try
		{
			var (val, _, _, _, _, _, _, _, _) = result;
			if (val != null)
			{
				Windows[val] = new Refs
				{
					Window = val,
					Container = result.Item3,
					Output = result.Item4,
					OutputButton = result.Item6,
					Status = result.Rest.Item1,
					Control = result.Rest.Item2,
					TargetLabel = result.Item7
				};
				Log.Debug("chem batch: captured window");
			}
		}
		catch (Exception ex)
		{
			Log.Debug("chem batch capture failed: " + ex.Message);
		}
	}

	public static void CaptureCallback(MachineryButton button, Action callback)
	{
		if (button != null)
		{
			Callbacks[button] = callback;
		}
	}

	public static void Setup(GameItem machine)
	{
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Expected O, but got Unknown
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Expected O, but got Unknown
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Expected O, but got Unknown
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Expected O, but got Unknown
		try
		{
			if (machine == null || machine.contentWindow == null || !Windows.TryGetValue(machine.contentWindow, out var refs) || refs == null)
			{
				return;
			}
			refs.Machine = machine;
			if (!ModSettings.ChemBatch || refs.Installed || !Callbacks.TryGetValue(refs.OutputButton, out var value))
			{
				return;
			}
			refs.OriginalOutput = value;
			if (refs.Slider == null)
			{
				refs.Slider = new MachinerySlider(1f, 90);
				refs.Slider.SetRange(1f, 1f, 1f);
				refs.Slider.onChanged = delegate
				{
					try
					{
						refs.RestoreToMax = false;
						refs.Preferred = refs.Slider.GetInt();
						UpdateTargetLabel(refs.Machine, refs, refs.SliderShown);
					}
					catch
					{
					}
				};
				refs.MinusButton = new MachineryButton("<", (Action)delegate
				{
					StepValue(refs, -1);
				}, 10, 10);
				refs.PlusButton = new MachineryButton(">", (Action)delegate
				{
					StepValue(refs, 1);
				}, 10, 10);
				refs.QuantityRow = new MachineryStack(false, 4);
				refs.QuantityRow.Add((PixelElement)(object)refs.MinusButton);
				refs.QuantityRow.Add((PixelElement)(object)refs.Slider);
				refs.QuantityRow.Add((PixelElement)(object)refs.PlusButton);
				refs.QuantityRow.SetAlign((MachineryStack.Align)1);
			}
			FitQuantityRow(refs);
			refs.OutputButton.SetCallback((Action)delegate
			{
				GameItem val = ((refs.Container != null) ? refs.Container.childItem : null);
				int[] array = CaptureLiquid(val);
				int[] array2 = ((array != null) ? ((int[])array.Clone()) : null);
				GameItem val2 = null;
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				int num4 = 0;
				int num5 = 0;
				int num6 = 0;
				long ticks = 0L;
				long num7 = 0L;
				long num8 = 0L;
				long num9 = 0L;
				long num10 = 0L;
				long num11 = 0L;
				long num12 = 0L;
				long num13 = 0L;
				Stopwatch stopwatch = Stopwatch.StartNew();
				Stopwatch stopwatch2 = Stopwatch.StartNew();
				try
				{
					num3 = ((refs.Slider == null) ? 1 : refs.Slider.GetInt());
					string text = SelectedRecipeId(machine);
					num2 = ChemFinisherRecipe.GetRecipeById(text)?.requiredVolume ?? 0;
					stopwatch2.Restart();
					val2 = DirectoryMaster.Item(text, true);
					ticks = stopwatch2.ElapsedTicks;
					stopwatch2.Restart();
					int num14 = OutputCapacity(refs.Output, text, val2);
					num7 += stopwatch2.ElapsedTicks;
					num4++;
					for (int i = 0; i < num3; i++)
					{
						if (refs.OriginalOutput == null)
						{
							break;
						}
						if (!CanCraftShadow(machine, refs.Container, array2))
						{
							break;
						}
						stopwatch2.Restart();
						bool num15 = CanPlaceOneProduct(refs.Output, text, val2);
						long elapsedTicks = stopwatch2.ElapsedTicks;
						num8 += elapsedTicks;
						num5++;
						if (elapsedTicks > num9)
						{
							num9 = elapsedTicks;
						}
						if (!num15)
						{
							Log.Debug("chem batch: output slot blocked, skip without consuming");
							break;
						}
						if (num14 < 1)
						{
							stopwatch2.Restart();
							num14 = OutputCapacity(refs.Output, text, val2);
							num7 += stopwatch2.ElapsedTicks;
							num4++;
							if (num14 < 1)
							{
								break;
							}
						}
						int num16 = OutputUnits(refs.Output);
						int[] array3 = ((array2 != null) ? ((int[])array2.Clone()) : null);
						stopwatch2.Restart();
						bool batchTransferActive = _batchTransferActive;
						GameItem batchTransferContainer = _batchTransferContainer;
						int[] batchTransferShadow = _batchTransferShadow;
						int batchTransferConsumed = _batchTransferConsumed;
						_batchTransferActive = true;
						_batchTransferContainer = val;
						_batchTransferShadow = array2;
						_batchTransferConsumed = 0;
						int num17 = 0;
						try
						{
							refs.OriginalOutput();
						}
						finally
						{
							num17 = _batchTransferConsumed;
							_batchTransferActive = batchTransferActive;
							_batchTransferContainer = batchTransferContainer;
							_batchTransferShadow = batchTransferShadow;
							_batchTransferConsumed = batchTransferConsumed;
						}
						long elapsedTicks2 = stopwatch2.ElapsedTicks;
						num10 += elapsedTicks2;
						num6++;
						if (elapsedTicks2 > num11)
						{
							num11 = elapsedTicks2;
						}
						int num18 = OutputUnits(refs.Output) - num16;
						if (num18 <= 0)
						{
							if (array3 != null)
							{
								array2 = array3;
							}
							break;
						}
						if (num17 <= 0 && array2 != null)
						{
							DeductShadow(array2, num2);
						}
						num++;
						if (num18 == 1)
						{
							num14 = Mathf.Max(0, num14 - 1);
						}
						else
						{
							stopwatch2.Restart();
							num14 = OutputCapacity(refs.Output, text, val2);
							num7 += stopwatch2.ElapsedTicks;
							num4++;
						}
					}
				}
				catch (Exception ex2)
				{
					Log.Debug("chem batch output failed: " + ex2.Message);
				}
				finally
				{
					if (array2 != null)
					{
						if (!LiquidMatches(val, array2))
						{
							stopwatch2.Restart();
							RestoreLiquid(val, array2);
							num12 += stopwatch2.ElapsedTicks;
						}
						if (num > 0)
						{
							try
							{
								WaterFeatureHelper.OnLiquidContainerPourOutOf(val);
							}
							catch (Exception ex3)
							{
								Log.Debug("chem batch water update failed: " + ex3.Message);
							}
						}
					}
					else
					{
						if (!LiquidMatches(val, array))
						{
							stopwatch2.Restart();
							RestoreLiquid(val, array);
							num12 += stopwatch2.ElapsedTicks;
						}
						int num19 = num * Mathf.Max(0, num2);
						if (num19 > 0)
						{
							try
							{
								WaterHelper.RemoveAndReturn(val, num19);
							}
							catch (Exception ex4)
							{
								Log.Debug("chem batch consume failed: " + ex4.Message);
							}
						}
					}
					try
					{
						if (val2 != null)
						{
							val2.Destroy();
						}
					}
					catch
					{
					}
				}
				refs.RestoreToMax = ModSettings.ChemBatchDefaultMax && refs.Preferred <= 0;
				stopwatch2.Restart();
				Update(machine, refs);
				num13 = stopwatch2.ElapsedTicks;
				stopwatch.Stop();
				Log.Debug("chem batch timing req=" + num3 + " ok=" + num + " proto=" + Ms(ticks) + " cap=" + Ms(num7) + "/" + num4 + " pre=" + Ms(num8) + "/" + num5 + " preMax=" + Ms(num9) + " orig=" + Ms(num10) + "/" + num6 + " origMax=" + Ms(num11) + " restore=" + Ms(num12) + " update=" + Ms(num13) + " total=" + Ms(stopwatch.ElapsedTicks));
				refs.LogScheduledTiming = true;
				ScheduleRefresh(machine, refs);
			});
			refs.Installed = true;
			InstallOnOneSecond(refs, machine);
			Rebuild(refs, showSlider: false);
			Update(machine, refs);
			ScheduleRefresh(machine, refs);
			Log.Debug("chem batch: UI installed");
		}
		catch (Exception ex)
		{
			Log.Debug("chem batch setup failed: " + ex.Message);
		}
	}

	private static void StepValue(Refs refs, int delta)
	{
		try
		{
			if (refs != null && refs.Slider != null)
			{
				int num = Mathf.Max(1, Mathf.RoundToInt(refs.Slider.max));
				int num2 = Mathf.Clamp(refs.Slider.GetInt() + delta, 1, num);
				refs.Slider.SetInt(num2, true);
			}
		}
		catch
		{
		}
	}

	public static void SetEnabled(bool enabled)
	{
		try
		{
			List<Refs> list = new List<Refs>(Windows.Values);
			for (int i = 0; i < list.Count; i++)
			{
				Refs refs = list[i];
				if (refs != null)
				{
					if (!enabled)
					{
						Disable(refs);
					}
					else if (refs.Machine != null)
					{
						Setup(refs.Machine);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.Debug("chem batch toggle failed: " + ex.Message);
		}
	}

	private static void Disable(Refs refs)
	{
		try
		{
			if (refs != null && refs.Installed)
			{
				if (refs.OutputButton != null && refs.OriginalOutput != null)
				{
					refs.OutputButton.SetCallback(refs.OriginalOutput);
				}
				if (refs.SliderShown)
				{
					Rebuild(refs, showSlider: false);
				}
				UpdateTargetLabel(refs.Machine, refs, withValue: false);
				ApplyChemStatus(refs, outputFull: false);
				refs.SliderShown = false;
				refs.Installed = false;
				if (refs.Machine != null && refs.WrappedOnOneSecond != null && ((GameItemFunc)refs.Machine).onOneSecondItemFunc == refs.WrappedOnOneSecond)
				{
					((GameItemFunc)refs.Machine).onOneSecondItemFunc = refs.OriginalOnOneSecond;
				}
				refs.WrappedOnOneSecond = null;
				refs.OriginalOnOneSecond = null;
			}
		}
		catch
		{
		}
	}

	public static void OnCanOutput(GameItem machine, GameSlotInventory container)
	{
		try
		{
			if (machine != null && machine.contentWindow != null && Windows.TryGetValue(machine.contentWindow, out var value) && value != null && value.Slider != null)
			{
				if (!ModSettings.ChemBatch)
				{
					Disable(value);
					return;
				}
				Update(machine, value);
				ScheduleRefresh(machine, value);
			}
		}
		catch
		{
		}
	}

	private static bool CanCraft(GameItem machine, GameSlotInventory container)
	{
		try
		{
			if (machine == null || container == null || container.childItem == null)
			{
				return false;
			}
			string text = SelectedRecipeId(machine);
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			if (WaterHelper.IsEmpty(container.childItem))
			{
				return false;
			}
			ChemFinisherRecipe recipeById = ChemFinisherRecipe.GetRecipeById(text);
			return recipeById != null && recipeById.CanOutput(container.childItem);
		}
		catch
		{
			return false;
		}
	}

	private static bool CanCraftShadow(GameItem machine, GameSlotInventory container, int[] shadow)
	{
		if (shadow == null)
		{
			return CanCraft(machine, container);
		}
		try
		{
			if (machine == null || container == null || container.childItem == null)
			{
				return false;
			}
			string text = SelectedRecipeId(machine);
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			ChemFinisherRecipe recipeById = ChemFinisherRecipe.GetRecipeById(text);
			if (recipeById == null || recipeById.requiredVolume <= 0)
			{
				return false;
			}
			if (ShadowVolume(shadow) < recipeById.requiredVolume)
			{
				return false;
			}
			if (recipeById.isRequireDry && WaterHelper.IsWet(container.childItem))
			{
				return false;
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static int ShadowVolume(int[] shadow)
	{
		if (shadow == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < shadow.Length; i++)
		{
			if (shadow[i] > 0)
			{
				num += shadow[i];
			}
		}
		return num;
	}

	private static void DeductShadow(int[] shadow, int amount)
	{
		if (shadow != null && amount > 0 && ShadowVolume(shadow) > 0)
		{
			int[] array = WaterHelper.SplitProportionally(shadow, amount);
			for (int i = 0; i < shadow.Length && i < array.Length; i++)
			{
				shadow[i] = Mathf.Max(0, shadow[i] - array[i]);
			}
		}
	}

	private static string SelectedRecipeId(GameItem machine)
	{
		try
		{
			if (machine == null || !machine.IsTag("SELECTED_CHEM_ID_TAG"))
			{
				return null;
			}
			return machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString();
		}
		catch
		{
			return null;
		}
	}

	private static int MaxCraft(GameItem machine, Refs refs)
	{
		try
		{
			if (!CanCraft(machine, refs.Container))
			{
				return 0;
			}
			ChemFinisherRecipe recipeById = ChemFinisherRecipe.GetRecipeById(SelectedRecipeId(machine));
			if (recipeById == null || recipeById.requiredVolume <= 0)
			{
				return 0;
			}
			int num = WaterHelper.GetTotalVolume(refs.Container.childItem) / recipeById.requiredVolume;
			if (num <= 0)
			{
				return 0;
			}
			int num2 = OutputCapacity(refs.Output, recipeById.identifier);
			return Mathf.Min(num, num2);
		}
		catch
		{
			return 0;
		}
	}

	private static int OutputCapacity(GameGridInventory grid, string productId)
	{
		GameItem val = null;
		try
		{
			if (grid == null || grid.inventoryShape == null || string.IsNullOrEmpty(productId))
			{
				return 0;
			}
			val = DirectoryMaster.Item(productId, true);
			return OutputCapacity(grid, productId, val);
		}
		catch
		{
			return 0;
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

	private static int OutputCapacity(GameGridInventory grid, string productId, GameItem prototype)
	{
		try
		{
			if (grid == null || grid.inventoryShape == null || string.IsNullOrEmpty(productId) || prototype == null || prototype.modifiedShape == null)
			{
				return 0;
			}
			List<GameItem> childItems = ((GameInventory)grid).childItems;
			int num = 0;
			for (int i = 0; i < childItems.Count; i++)
			{
				GameItem val = childItems[i];
				if (val == null || val.identifier != productId)
				{
					continue;
				}
				try
				{
					int num2 = val.MaxNumAdd();
					if (num2 > 0)
					{
						num += Math.Min(num2, 9999);
					}
				}
				catch
				{
				}
			}
			bool[,] occupied = BuildOccupied(grid);
			int j = 0;
			for (int num3 = grid.inventoryShape.width * grid.inventoryShape.height; j < num3; j++)
			{
				if (!TryFindShadowPlacement(grid, prototype, occupied, out var found))
				{
					break;
				}
				MarkOccupied(occupied, found);
			}
			return num + j;
		}
		catch
		{
			return 0;
		}
	}

	private static bool[,] BuildOccupied(GameGridInventory grid)
	{
		int width = grid.inventoryShape.width;
		int height = grid.inventoryShape.height;
		bool[,] array = new bool[width, height];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				if (grid.inventoryShape.GetLocal(j, i) != 0)
				{
					array[j, i] = true;
				}
			}
		}
		List<GameItem> childItems = ((GameInventory)grid).childItems;
		for (int k = 0; k < childItems.Count; k++)
		{
			GameItem val = childItems[k];
			if (val == null || val.modifiedShape == null)
			{
				continue;
			}
			for (int l = 0; l < height; l++)
			{
				for (int m = 0; m < width; m++)
				{
					if (val.modifiedShape.Get(m, l) != 0)
					{
						array[m, l] = true;
					}
				}
			}
		}
		return array;
	}

	private static bool TryFindShadowPlacement(GameGridInventory grid, GameItem prototype, bool[,] occupied, out GridShape found)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		found = null;
		GridShape modifiedShape = prototype.modifiedShape;
		if (modifiedShape == null)
		{
			return false;
		}
		int width = grid.inventoryShape.width;
		int height = grid.inventoryShape.height;
		GridShapeBuilder val = new GridShapeBuilder(modifiedShape);
		for (int i = 0; i < 2; i++)
		{
			for (int j = 0; j < 4; j++)
			{
				int num = 4 - j;
				for (int k = 0; k < height; k++)
				{
					for (int l = 0; l < width; l++)
					{
						val.SetTransform(l, k, i != 0, num);
						if (ShadowFits(occupied, (GridShape)(object)val))
						{
							GridShape val2 = val.Build();
							if (((GameInventory)grid).CheckShapePlacement(prototype, val2, NoIgnoredItems))
							{
								found = val2;
								return true;
							}
						}
					}
				}
			}
		}
		return false;
	}

	private static bool ShadowFits(bool[,] occupied, GridShape shape)
	{
		int length = occupied.GetLength(0);
		int length2 = occupied.GetLength(1);
		for (int i = shape.minY; i <= shape.maxY; i++)
		{
			for (int j = shape.minX; j <= shape.maxX; j++)
			{
				if (shape.Get(j, i) != 0)
				{
					if (j < 0 || i < 0 || j >= length || i >= length2)
					{
						return false;
					}
					if (occupied[j, i])
					{
						return false;
					}
				}
			}
		}
		return true;
	}

	private static void MarkOccupied(bool[,] occupied, GridShape shape)
	{
		int length = occupied.GetLength(0);
		int length2 = occupied.GetLength(1);
		for (int i = shape.minY; i <= shape.maxY; i++)
		{
			for (int j = shape.minX; j <= shape.maxX; j++)
			{
				if (shape.Get(j, i) != 0 && j >= 0 && i >= 0 && j < length && i < length2)
				{
					occupied[j, i] = true;
				}
			}
		}
	}

	private static bool CanPlaceOneProduct(GameGridInventory grid, string productId)
	{
		GameItem val = null;
		try
		{
			if (grid == null || grid.inventoryShape == null || string.IsNullOrEmpty(productId))
			{
				return false;
			}
			val = DirectoryMaster.Item(productId, true);
			return CanPlaceOneProduct(grid, productId, val);
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

	private static bool CanPlaceOneProduct(GameGridInventory grid, string productId, GameItem prototype)
	{
		try
		{
			if (grid == null || grid.inventoryShape == null || string.IsNullOrEmpty(productId) || prototype == null || prototype.modifiedShape == null)
			{
				return false;
			}
			SlotMarker val = grid.TryFindOneValidInventorySlot(prototype, false);
			return val != null && val.numTransfer > 0;
		}
		catch
		{
			return false;
		}
	}

	private static int[] CaptureLiquid(GameItem container)
	{
		if (container == null)
		{
			return null;
		}
		try
		{
			return WaterHelper.GetLiquidParts(container);
		}
		catch
		{
			return null;
		}
	}

	private static bool LiquidMatches(GameItem container, int[] snapshot)
	{
		if (container == null || snapshot == null)
		{
			return true;
		}
		try
		{
			int[] liquidParts = WaterHelper.GetLiquidParts(container);
			int num = Mathf.Min(liquidParts.Length, snapshot.Length);
			for (int i = 0; i < num; i++)
			{
				if (liquidParts[i] != snapshot[i])
				{
					return false;
				}
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static void RestoreLiquid(GameItem container, int[] snapshot)
	{
		if (container == null || snapshot == null)
		{
			return;
		}
		try
		{
			for (int i = 0; i < Liquid.Liquids.Count && i < snapshot.Length; i++)
			{
				int value = snapshot[i];
				string currentPart = Liquid.Liquids[i].currentPart;
				container.ModifyTag(currentPart, (Action<TagState>)delegate(TagState state)
				{
					state.Enable().SetInt(value);
				}, false);
			}
		}
		catch
		{
		}
	}

	private static void InstallOnOneSecond(Refs refs, GameItem machine)
	{
		try
		{
			if (refs == null || machine == null || (refs.WrappedOnOneSecond != null && ((GameItemFunc)machine).onOneSecondItemFunc == refs.WrappedOnOneSecond))
			{
				return;
			}
			refs.OriginalOnOneSecond = ((GameItemFunc)machine).onOneSecondItemFunc;
			Action<GameItem, GameInventory, SlotMarker> original = refs.OriginalOnOneSecond;
			refs.WrappedOnOneSecond = delegate(GameItem item, GameInventory inventory, SlotMarker slot)
			{
				try
				{
					if (original != null)
					{
						original(item, inventory, slot);
					}
				}
				finally
				{
					try
					{
						if (ModSettings.ChemBatch && refs.Installed)
						{
							ApplyChemStatus(refs, IsOutputFull(machine, refs));
						}
					}
					catch
					{
					}
				}
			};
			((GameItemFunc)machine).onOneSecondItemFunc = refs.WrappedOnOneSecond;
		}
		catch
		{
		}
	}

	private static bool IsOutputFull(GameItem machine, Refs refs)
	{
		try
		{
			if (!CanCraft(machine, refs.Container))
			{
				return false;
			}
			string text = SelectedRecipeId(machine);
			ChemFinisherRecipe recipeById = ChemFinisherRecipe.GetRecipeById(text);
			if (recipeById == null || recipeById.requiredVolume <= 0)
			{
				return false;
			}
			if (WaterHelper.GetTotalVolume(refs.Container.childItem) / recipeById.requiredVolume <= 0)
			{
				return false;
			}
			return !CanPlaceOneProduct(refs.Output, text);
		}
		catch
		{
			return false;
		}
	}

	private static void ApplyChemStatus(Refs refs, bool outputFull)
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (refs != null && refs.Status != null)
			{
				if (outputFull)
				{
					string localizedMechanic = LocHelper.GetLocalizedMechanic("mech_turbo_booster_status", new object[1] { Text.Get("chem_batch_status_output_full") });
					refs.Status.SetText(localizedMechanic);
					refs.Status.SetColor((Color32)(RenderHandler.GetColorFromPalette((RenderHandler.ColorPalette)12615037)));
				}
				else
				{
					refs.Status.SetColor(MachineryStyle.BODY_TEXT);
				}
			}
		}
		catch
		{
		}
	}

	private static string Ms(long ticks)
	{
		try
		{
			return ((double)ticks * 1000.0 / (double)Stopwatch.Frequency).ToString("F2");
		}
		catch
		{
			return "?";
		}
	}

	private static int OutputUnits(GameGridInventory grid)
	{
		if (grid == null)
		{
			return 0;
		}
		List<GameItem> childItems = ((GameInventory)grid).childItems;
		int num = 0;
		for (int i = 0; i < childItems.Count; i++)
		{
			GameItem val = childItems[i];
			if (val != null)
			{
				num += val.unitCount;
			}
		}
		return num;
	}

	private static void ScheduleRefresh(GameItem machine, Refs refs)
	{
		if (refs == null || refs.RefreshScheduled)
		{
			return;
		}
		refs.RefreshScheduled = true;
		try
		{
			LetterboxManager.RunNextUpdate((Action)delegate
			{
				if (refs != null)
				{
					refs.RefreshScheduled = false;
					long timestamp = Stopwatch.GetTimestamp();
					Update(machine, refs);
					if (refs.LogScheduledTiming)
					{
						refs.LogScheduledTiming = false;
						Log.Debug("chem batch timing scheduled update=" + Ms(Stopwatch.GetTimestamp() - timestamp));
					}
				}
			});
		}
		catch
		{
			refs.RefreshScheduled = false;
		}
	}

	private static void FitQuantityRow(Refs refs)
	{
		try
		{
			if (refs == null || refs.Slider == null)
			{
				return;
			}
			int num = ((refs.Control != null && refs.Control.widthPixels > 0) ? refs.Control.widthPixels : ((refs.Status != null && refs.Status.widthPixels > 0) ? refs.Status.widthPixels : 0));
			if (num <= 0)
			{
				return;
			}
			if (refs.LayoutWidth <= 0)
			{
				refs.LayoutWidth = num;
			}
			int layoutWidth = refs.LayoutWidth;
			if (layoutWidth > 0 && refs.LastAvailable != layoutWidth)
			{
				refs.LastAvailable = layoutWidth;
				int num2 = 28;
				int width = Mathf.Max(8, layoutWidth - num2);
				refs.Slider.SetWidth(width);
				if (refs.QuantityRow != null)
				{
					refs.QuantityRow.ResizePixels(layoutWidth, refs.QuantityRow.heightPixels);
				}
			}
		}
		catch
		{
		}
	}

	private static void UpdateTargetLabel(GameItem machine, Refs refs, bool withValue)
	{
		try
		{
			if (refs == null || refs.TargetLabel == null)
			{
				return;
			}
			string text = SelectedRecipeId(machine);
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			ChemFinisherRecipe recipeById = ChemFinisherRecipe.GetRecipeById(text);
			if (recipeById != null && !string.IsNullOrEmpty(recipeById.displayName))
			{
				string text2 = recipeById.displayName;
				if (withValue && refs.Slider != null)
				{
					text2 = text2 + "  * " + refs.Slider.GetInt();
				}
				if (refs.TargetLabel.GetText() != text2)
				{
					refs.TargetLabel.SetText(text2);
				}
			}
		}
		catch
		{
		}
	}

	private static void Update(GameItem machine, Refs refs)
	{
		try
		{
			if (!ModSettings.ChemBatch)
			{
				Disable(refs);
				return;
			}
			int num = MaxCraft(machine, refs);
			bool flag = num >= 1;
			string text = SelectedRecipeId(machine);
			if (flag)
			{
				FitQuantityRow(refs);
				if (refs.Preferred > 0)
				{
					// Player picked a quantity: keep it, only clamped to what can be crafted now.
					refs.Slider.SetRange(1f, (float)num, 1f);
					refs.Slider.SetInt(Mathf.Clamp(refs.Preferred, 1, num), false);
					refs.RestoreToMax = false;
				}
				else if (!refs.SliderShown || !string.Equals(refs.LastRecipe, text))
				{
					refs.Slider.SetRange(1f, (float)num, 1f);
					refs.Slider.SetInt((!ModSettings.ChemBatchDefaultMax) ? 1 : num, false);
					refs.RestoreToMax = ModSettings.ChemBatchDefaultMax;
				}
				else
				{
					if (!ModSettings.ChemBatchDefaultMax)
					{
						refs.RestoreToMax = false;
					}
					if (refs.RestoreToMax)
					{
						refs.Slider.SetInt(num, false);
					}
					else
					{
						if (refs.Slider.GetInt() > num)
						{
							refs.Slider.SetInt(num, false);
						}
						if (refs.Slider.GetInt() < 1)
						{
							refs.Slider.SetInt(1, false);
						}
					}
					refs.Slider.SetRange(1f, (float)num, 1f);
				}
				refs.LastRecipe = text;
			}
			if (flag != refs.SliderShown)
			{
				Rebuild(refs, flag);
				refs.SliderShown = flag;
			}
			UpdateTargetLabel(machine, refs, flag);
		}
		catch (Exception ex)
		{
			Log.Debug("chem batch update failed: " + ex.Message);
		}
	}

	private static void Rebuild(Refs refs, bool showSlider)
	{
		if (refs == null || refs.Control == null)
		{
			return;
		}
		List<PixelElement> list = new List<PixelElement>(refs.Control.elements);
		for (int i = 0; i < list.Count; i++)
		{
			refs.Control.Detach(list[i]);
		}
		for (int j = 0; j < list.Count; j++)
		{
			PixelElement val = list[j];
			if ((object)val == refs.Status)
			{
				MachineryStack control = refs.Control;
				PixelElement obj;
				if (!showSlider || refs.QuantityRow == null)
				{
					PixelElement status = (PixelElement)(object)refs.Status;
					obj = status;
				}
				else
				{
					PixelElement status = (PixelElement)(object)refs.QuantityRow;
					obj = status;
				}
				control.Add(obj);
			}
			else if ((object)val == refs.QuantityRow)
			{
				if (!showSlider)
				{
					refs.Control.Add((PixelElement)(object)refs.Status);
				}
			}
			else
			{
				refs.Control.Add(val);
			}
		}
		refs.Control.Validate();
	}
}
