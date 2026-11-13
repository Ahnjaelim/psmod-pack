# Personal Mod Pack (Probably Stolen)

Personal-use mod pack for *Probably Stolen*: three mods merged into a single DLL so there is only one thing to version.

## Contents

- **QoL Tweaks** (`PenLabelerAssist/`): personal, buildable copy of the Workshop mod *Pen's QoL Tweaks* v1.1.5, with the strings translated to English.
  - Quick buy / sell / appraisal / confront keys
  - Tool box-select batching
  - Auto battery swap
  - Hydroponic auto replant
  - Auto document discard
  - Window follow / snap
  - Chem finisher batch slider. The chosen quantity now stays fixed after pressing output.
- **Better Keybinder** (`Merged/ComboKeyPresets.cs`):
  - Ctrl / Shift / Alt combos for Storage Coordinator presets and Toolbox slot keys.
  - Magnifier and Close-all-windows hotkeys (default Ctrl+Shift+W) in Options > Keybind.
- **Better Butcher Machine** (`Merged/BetterButcherMachine.cs`): adds a "Composite" cull mode to the Auto Butcher cage tab.
  - Cull by age OR weight.
  - Keep breeders.
  - Trim overcrowding lightest-first.

## Build

The project references the game's `Probably Stolen_Data/Managed` DLLs. Build with:

```
dotnet build -c Release
```

The build copies the DLL, `manifest.xml` and `Localization/` to `Mods/PersonalQoLTweaks/`. Close the game first, because the DLL is locked while it runs.

## Notes

- The mod ID is `personal-qol-tweak`.
- The game loader instantiates only one `IMod` per DLL. `PenLabelerAssistMod` is the entry point and calls the merged modules' enable/disable hooks.
- `[HarmonyPatch]` classes are applied by the loader's `PatchAll`.
- `<Capabilities/>` stays empty, because the in-game scanner refuses `reflection-external`.
