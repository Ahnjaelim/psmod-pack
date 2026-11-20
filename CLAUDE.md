# Notes for Claude Code (handoff)

Personal mods for the Unity game **Probably Stolen** (Steam). The user writes in Korean; answer in Korean.
All in-game text the mods show is Korean.

## Layout

This repo root is `<game>/ModSource/` (game = `C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen` on the main PC;
on another machine find the Steam install). Each folder is a separate mod:

| Folder | Mod name (ID) | Deployed to | Version |
|---|---|---|---|
| `PersonalQoLTweaks/` | 개인 편의성 패치 (`personal-qol-tweak`) | `Mods/PersonalQoLTweaks` | 2.26.0 |
| `map-extension/` | 지도 확장 (`map-extension`) | `Mods/MapExtension` | 0.40.2 |
| `korean-patch/` | 한국어 패치 (`korean-patch`) | `Mods/KoreanPatch` | 0.3.0 |

`map-extension/NOTES.md` is the detailed per-version design log (Korean). Read it before touching map-extension.

## Build / deploy

- `dotnet build -c Release` inside a mod folder. The csproj references `..\..\Probably Stolen_Data\Managed\*.dll`
  and a post-build step copies the DLL, `manifest.xml` and assets (`Map/**`, `Fonts/**`) into `<game>/Mods/<X>`.
- `MSB3021/MSB3027` = the game is running and the DLL is locked. Compile still succeeded; tell the user, deploy after they close the game.
- Verify a deploy with `md5sum bin/Release/netstandard2.1/X.dll ../../Mods/X/X.dll`.
- Bump `<ModVersion>` in the mod's `manifest.xml` on every change, and append a line to `map-extension/NOTES.md` for map-extension changes.
- "반영 좀/패치 좀" = build + deploy. Commit/push only when the user asks ("커밋하고 푸시 좀"). Remote: github.com/Ahnjaelim/psmod-pack (one repo for all mods, user's choice; can be split later with `git subtree split`).

## Hard rules

- New folders/files: English kebab-case names, even when the user asks in Korean.
- **Never build anything that approves mods automatically** (ModLoader.ApproveAndEnable etc.). Mod approval is the player's.
- The game's **mod scanner** refuses DLLs that:
  - reference guard types: `ModLoader`, `ModTrust*`, `ModFingerprint`, `ModScanner`, `ModCapability`, `ModWorkshop`, `SteamManager`, `PaperBookRegistry`, `ModReflect`;
  - use `System.IO.File/Directory` directly (→ "files" capability). Use `ModHelper.GetModFolder / ReadModFileBytes / ModFileExists`;
  - call `Traverse.GetValue<T>()` / AccessTools generics with a **System** type argument (→ "reflection-external"). Use non-generic `GetValue()` + `as` cast. Generics with game types are fine.
- Mod save data: `ModHelper.SetModData/GetModData/RemoveModData(modId, key, string)` → stored in the save (`playerStore.modData`), saved/rolled back with the save.
- Files are a mix of LF and CRLF. When patching with Python, normalize `\r\n` and write back with the original newline; Python on Windows writes CRLF by default. Avoid `sed` replacements containing `\U` (uppercases the rest).

## Decompiled game code

Not in the repo. Regenerate with `ilspycmd -p -o <scratch>/decomp2 "<game>/Probably Stolen_Data/Managed/Assembly-CSharp.dll"` and grep it.
Useful anchors: `MapUIManager` (outing travel/return), `TransitUI` (outing map window), `AfterhourLocation`, `ScavHelper`,
`PlayerStore` (`BeginDay`, `SaveGame`, `EncodeSaveItem/DecodeSaveItem`, `GetCurrentStoreAttractiveness`), `StoreReputation`,
`CustomUIManager/CustomUIBuilder` (game's own window builder), `HopperHelper` (reference for the filter window),
`AdvCalendarUIManager` (calendar monitor window we clone), `RenderHandler.RaycastElement`, `ItemPinHelper.BlocksClicks`.

## map-extension architecture (0.30.4)

- `TransitMap.cs` — replaces the outing card list with a city map (`Map/map_background.png`). Markers scaled 0.7, max y 0.86
  (bottom strip has buttons + HUD). Icons are black silhouettes `Map/icon_*.png`, whitened on load and tinted via `Image.color`.
  Marker kinds: vanilla/mod locations, retired merchants, factions (info only), 19 "미확인 장소" (`UnknownSpots`, future search
  locations, alpha 0.2), branches, home (내 전당포). District labels (상류층/하층민 구역). Travel button narrowed + a cloned
  [돌아가기] button (go home, no confirm).
- `FarmLocation.cs` — search locations (pharmacy only for now). Hotspots → confirm → loot box window with Tarkov-style
  stripe covers (`LootCover.cs`, tag `VME_UNREVEALED_TAG`, read via `item.state` not `IsTag`). Floor items persist per location.
  `TestEnergy` = 0 (real energy) since 0.40.1. Placeholder background `Map/bg_placeholder.png`.
- `Roaming.cs` — leaving a location opens the map directly (inside the return blackscreen) instead of the shop; keeps the carried
  backpack; returning never consumes the nightly outing; energy refills once per night (`ScavHelper.ResetScavenging` prefix).
- `OutingHud.cs` — bottom-left HUD while outside / map open: cash, HP = (10 − woundState) × 10, energy.
- `Dialogs.cs` — confirm popups = clone of the game's go-outside confirm panel; placed inside the map canvas when the map is open.
- `RetiredMerchants.cs` — retired merchant houses (Jackson-style stage, visit weekday, vanilla portrait per save).
- Branches (분점), idle simulation:
  - `Branches.cs` — 3 sites (lower/downtown/upper). Each has a virtual 17×10 stock inventory filled from the main shop.
    Every morning (`PlayerStore.BeginDay` postfix) `BranchSim.RunDay` spawns visitors. Visitor count = range × branch attractiveness/400 (own value per branch, starts 0; was the main shop value before 0.36)
    (clamped 0.6–1.8). 10 visitor types, weighted per site and multiplied by faction reputation `1 + rep/200` (0.25–2).
    Each type has a budget and liked item types. Price = `GetCurrentValue(retail markup + events) × site multiplier`.
    Contraband never sells. Optional "hold on price-drop event". Logs keep the last 7 days.
  - Auto-fill searches the shop inventory including containers and fridges. It skips other machines, pinned items,
    contraband and containers themselves. Filter modes: all liked / allow list / block list.
  - `BranchMonitor.cs` — clone of the calendar monitor window, web-app layout: top tabs = [전체] + branches (4th tab cloned), left sidebar pages 재고/판매 기록/손님/통계/설정, right = toolbar + scroll table; draggable; blocks item raycasts behind it with an
    empty `TreeNodeRender`; sets `UIDragHandler.isDraggingAnyUI` while dragging.
  - `BranchRestockWindow.cs` — hopper-style settings window (CustomUIBuilder): daily auto-fill, price-drop hold, filter mode,
    item list. `UseSearch = false` → pick items by clicking them in the shop (blocks game clicks via `ItemPinHelper.BlocksClicks`
    postfix); `true` → name search (`ItemIndex`).
  - `BranchPanel.cs` — the bottom-left [분점] button, placed in the item window canvas just before `RenderHandler.windowRootObject` (behind shop items/windows, like the game background UI).
  - `BranchLocation` — owned branches are also enterable outing locations (floor works as storage).
- `SaveDb.cs` — all mod state in the save: `farm/<loc>/floor`, `farm/<loc>/box/<spot>`, `branch/<key>`,
  `branch/<key>/stock|log|auto`, `branch/last_paid_day`. Written in a `PlayerStore.SaveGame` prefix, read on `OnGameLoadedLate`.

## korean-patch (0.3.0)

- `translation/` + `data/ko.tsv`, `data/ko-hardcoded.tsv` = the Korean Translation workshop mod (3828896687, v1.0.10, by Gangmin(RNIN)),
  decompiled and merged for personal use (namespace `KoreanPatch.Translation`, mod id/data paths changed). The user unsubscribed the original.
  It is someone else's work with no stated license: ask before pushing it to the public repo.
- `data/mod-translations/corppresence-ko.csv` = Korean for the CorpPresence workshop mod (3828913695). The build copies it into that
  mod's folder as `Localization/ko.csv` (the game's ModLoader loads `<mod>/Localization/<locale>.csv`; CorpPresence reads it via
  `LoadedMod.Localization`). Only keyed strings; unkeyed `L.T(zh,en)` text stays English. Rebuild after a CorpPresence update.
- `KoreanPatchMod.cs` = Mulmaru font for display/sci-fi roles (runs after the translation's font patch).

## Open items / plans

- Energy (scav attempts) balance is deferred by the user: leave as is for now.
- Search-location unlock chain (0.38): `FarmLocationData.cs` = 19 locations on the `TransitMap.UnknownSpots` positions (placeholder
  background), `StartOpen` = pharmacy + hardware, `Chain` = what each location's telescope unlocks. Telescope spot appears once every
  spot was searched once; unlock state = afterhour counter `vme_farm_<key>`/`unlocked`. Undiscovered ones show as "미확인 장소".
- Ideas not done: branch visitor budget bonus / tier from attractiveness, contraband raids.
- Nothing has been play-tested since ~0.29; the user reports issues with screenshots.
