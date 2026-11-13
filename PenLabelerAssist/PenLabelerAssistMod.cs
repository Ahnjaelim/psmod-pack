using System;

namespace PenLabelerAssist;

public class PenLabelerAssistMod : IMod
{
	public void Init(ModManifest manifest)
	{
		Log.Init(manifest);
		try
		{
			Log.Info("init " + ((manifest != null) ? manifest.ID : "personal-qol-tweak") + " v" + ((manifest != null) ? manifest.ModVersion : "?"));
		}
		catch
		{
		}
		try
		{
			QoLKeyBindings.Register();
		}
		catch (Exception ex)
		{
			Log.Warn("keybind register failed: " + ex.Message);
		}
		try
		{
			ModSettings.Initialize((manifest != null) ? manifest.ID : "personal-qol-tweak");
		}
		catch (Exception ex2)
		{
			Log.Warn("mod settings init failed: " + ex2.Message);
		}
	}

	public void OnEnable()
	{
		try
		{
			ComboKeyPresets.ComboKeyPresetsModule.OnEnable();
		}
		catch (Exception ex0)
		{
			Log.Warn("keybinder enable failed: " + ex0.Message);
		}
		try
		{
			BetterButcherMachine.BetterButcherMachineModule.OnEnable();
		}
		catch (Exception ex1)
		{
			Log.Warn("butcher enable failed: " + ex1.Message);
		}
		try
		{
			ModHook.OnGameLoadedNormal += OnGameLoaded;
			ModHook.OnGameLoadedLate += OnGameLoadedLate;
			ModHook.OnDifficultyApplied += OnDifficultyApplied;
			ModHook.OnShutterOpenedLate += OnShutterOpenedLate;
			ModHook.OnWakingUpLate += OnWakingUpLate;
			ModHook.OnCreateTooltipLate += OnTooltipLate;
			try
			{
				if (PlayerStore.Instance != null)
				{
					Assist.SetGameReady(ready: true);
					Patches.ApplyCore();
					YeastBioreactor.ApplyToExisting();
					TabSwitcher.Ensure();
					BlackMarketFilter.Build();
					TrashAutoPickup.ScanOnLoad();
				}
			}
			catch
			{
			}
			Log.Info("enabled");
		}
		catch (Exception ex)
		{
			Log.Error("enable failed: " + ex.Message);
		}
	}

	public void OnDisable()
	{
		try
		{
			ComboKeyPresets.ComboKeyPresetsModule.OnDisable();
		}
		catch
		{
		}
		try
		{
			ModHook.OnGameLoadedNormal -= OnGameLoaded;
		}
		catch
		{
		}
		try
		{
			ModHook.OnGameLoadedLate -= OnGameLoadedLate;
		}
		catch
		{
		}
		try
		{
			ModHook.OnDifficultyApplied -= OnDifficultyApplied;
		}
		catch
		{
		}
		try
		{
			ModHook.OnShutterOpenedLate -= OnShutterOpenedLate;
		}
		catch
		{
		}
		try
		{
			ModHook.OnWakingUpLate -= OnWakingUpLate;
		}
		catch
		{
		}
		try
		{
			ModHook.OnCreateTooltipLate -= OnTooltipLate;
		}
		catch
		{
		}
		try
		{
			Patches.Remove();
		}
		catch
		{
		}
		try
		{
			Assist.Dispose();
		}
		catch
		{
		}
		try
		{
			WindowFollow.Reset();
		}
		catch
		{
		}
		try
		{
			WindowSnap.Reset();
		}
		catch
		{
		}
		try
		{
			MachineBatteryAutoSwap.Reset();
		}
		catch
		{
		}
		try
		{
			ChemBatch.Reset();
		}
		catch
		{
		}
		try
		{
			WaterLabelSync.Reset();
		}
		catch
		{
		}
		try
		{
			TabSwitcher.Dispose();
		}
		catch
		{
		}
		Log.Info("disabled");
	}

	private void OnGameLoaded()
	{
		try
		{
			Assist.ResetTransient();
		}
		catch
		{
		}
		try
		{
			WindowFollow.Reset();
		}
		catch
		{
		}
		try
		{
			WindowSnap.Reset();
		}
		catch
		{
		}
		try
		{
			MachineBatteryAutoSwap.Reset();
		}
		catch
		{
		}
		try
		{
			ChemBatch.Reset();
		}
		catch
		{
		}
		try
		{
			WaterLabelSync.Reset();
		}
		catch
		{
		}
	}

	private void OnGameLoadedLate()
	{
		try
		{
			Assist.SetGameReady(ready: true);
			Patches.ApplyCore();
			YeastBioreactor.ApplyToExisting();
			AutoDiscard.ScanExisting();
			TabSwitcher.Ensure();
			BlackMarketFilter.Build();
			TrashAutoPickup.ScanOnLoad();
		}
		catch (Exception ex)
		{
			Log.Error("deferred patch apply failed: " + ex.Message);
		}
	}

	private void OnDifficultyApplied(string difficultyId)
	{
		try
		{
			Assist.SetGameReady(ready: true);
			Patches.ApplyCore();
			YeastBioreactor.ApplyToExisting();
			TabSwitcher.Ensure();
			BlackMarketFilter.Build();
		}
		catch (Exception ex)
		{
			Log.Error("patch apply on difficulty failed: " + ex.Message);
		}
	}

	private void OnTooltipLate(RichTextBuilder builder, GameItem item)
	{
		try
		{
			Assist.AppendMemoryTooltip(builder, item);
		}
		catch
		{
		}
	}

	private void OnShutterOpenedLate()
	{
		try
		{
			AutoDiscard.ScanExisting();
			TabSwitcher.Ensure();
		}
		catch (Exception ex)
		{
			Log.Warn("shutter scan failed: " + ex.Message);
		}
	}

	private void OnWakingUpLate()
	{
		try
		{
			TrashAutoPickup.Scan();
		}
		catch (Exception ex)
		{
			Log.Warn("trash pickup day-start scan failed: " + ex.Message);
		}
	}
}
