# Notes for Claude Code (handoff)

Personal mods for the Unity game **Probably Stolen** (Steam). The user writes in Korean; answer in Korean.
All in-game text the mods show is Korean.

## Layout

This repo root is `<game>/ModSource/` (game = `C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen` on the main PC;
on another machine find the Steam install). Each folder is a separate mod:

| Folder | Mod name (ID) | Deployed to | Version |
|---|---|---|---|
| `PersonalQoLTweaks/` | 개인 편의성 패치 (`personal-qol-tweak`) | `Mods/PersonalQoLTweaks` | 2.27.5 |
| `map-extension/` | 지도 확장 (`map-extension`) | `Mods/MapExtension` | 0.43.4 |
| `korean-patch/` | 한국어 패치 (`korean-patch`) | `Mods/KoreanPatch` | 0.3.0 |
| `ui-enhancer/` | UI 개선 (`ui-enhancer`) | `Mods/UiEnhancer` | 0.10.21 |
| `shop-expansion/` | 가게 증축 (`shop-expansion`) | `Mods/ShopExpansion` | 0.9.1 |

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
- Register network upgrades (`ModHelper.AddNetworkUpgrade`) in `OnGameLoadedLate`, not `OnEnable`: at OnEnable `PlayerStore.Instance` builds a new PlayerStore and throws NRE.
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
  Someone else's work with no stated license; the user decided to keep it in the repo (personal use only, committed in a0a46e9).
- `data/mod-translations/corppresence-ko.csv` = Korean for the CorpPresence workshop mod (3828913695). The build copies it into that
  mod's folder as `Localization/ko.csv` (the game's ModLoader loads `<mod>/Localization/<locale>.csv`; CorpPresence reads it via
  `LoadedMod.Localization`). Only keyed strings; unkeyed `L.T(zh,en)` text stays English. Rebuild after a CorpPresence update.
- `KoreanPatchMod.cs` = Mulmaru font for display/sci-fi roles (runs after the translation's font patch).

## shop-expansion (0.9.1)

- Side rooms (옆방), up to 3, bought as Wild network Advanced-tab upgrades `VSE_SHOP_ROOM_1..3` (`RoomUpgrades.cs`).
- `Rooms.cs` — entering a room hides the shop scene (same objects the game hides in `EmporiumEntry.OnLeaveStore`, minus `invWindow`)
  plus open item windows, and shows a full-screen UI cover image just before `RenderHandler.windowRootObject` (hides every UI layered below the main inventory). Returns to the shop automatically on
  customer (`NegociationUIManager.OpenUI`), end of day, `OnLeaveStore`, save load. Purpose per room in mod data `room/<n>/purpose`.
- `RoomPanel.cs` — room tab buttons on the right wall (vertical, right of the poster), room header, purpose picker.
- `HiddenStore.cs` — mod-private hidden inventories, one real game machine per key, saved as mod data `hidden/<key>` (EncodeSaveItem), ticked nightly in a `PlayerStore.EndNight` postfix (Early/normal/Late), never added to `FindAllItem`.
- `AquaTank.cs` — aquaculture room tank = registered item `vse_aqua_tank` (game `ras` + 8 filter slots: the 4x1 filter grid arrays are extended via Traverse and the new slots added to the TickTank closure list; old `ras` tanks are migrated on load) in HiddenStore, 30x16 tank (wide) via the RAS size-upgrade tags, power fixed 100/day (= 4 RAS with full size upgrades and no efficiency upgrades, 25/day each), window laid out (left column + tank) by a `MachineryPanelRow.Validate` prefix that replaces the game layout for this row (a postfix ping-ponged with the game layout and froze the game) (panel order untouched = save-safe).
- `ShopItems.cs` — the mod's own item ids registered in `DirectoryMaster` via an `ItemDirectory` MonoBehaviour added in `Init` (so saves reload them through our factories): `vse_aqua_feeder`, `vse_aqua_butcher`, `vse_aqua_water_tank` (60,000 ml) (copies of the game feeder/auto butcher, fixed into the tank, not removable).
- `shop-expansion/NOTES.md` = per-version log.

### shop-expansion handoff (2026-11-23)

What the user wants (their design, keep to it):
- Shop "옆방": buy an empty room (Wild network Advanced tab, max 3), then pay 20,000₵ to renovate it into a use (no changing afterwards;
  the only way back is 폐쇄 = confirm dialog, deletes the room machines and every item in them, room becomes empty, then renovate again). Uses: 양식장 / 사육장 / 숙성고 / 온실 (+ 작업실 / 창고 I added).
  Move between shop and rooms any time while in the shop. In a room only the main inventory stays; every other UI (game + other mods) is hidden.
- Each use gets built-in machines that are **real game items kept in the hidden inventory** (`HiddenStore`), so game rules apply unchanged.
  The user expects many more such "items that don't exist in the game" for later room types; reuse HiddenStore + ShopItems for them.
- 양식장 (done first): one big built-in tank instead of juggling 3–4 RAS machines. Tank = 30x16 (wide; user asked for wide, the 16x30 tall
  version did not fit the screen), = 5 fully upgraded RAS (12x8). Power fixed 100/day (user: 4 RAS fully size-upgraded, NO efficiency upgrades = 4 x 25).
  Window: left column power / water / filters, right the tank. Feeder and butcher slots are hidden in the window; instead
  "수족관 전용 급이기 / 도축기" (`vse_aqua_feeder` / `vse_aqua_butcher`) are auto-placed into those slots when the room becomes 양식장,
  cannot be removed, and open from two buttons on the room floor ("작업대", left of the main inventory).

Status (0.9.1, end of 2026-11-23 laptop session):
- User confirmed in game: rooms/UI hiding, tank window (30x16, 8 filters), fish growing and saving, power/efficiency drag,
  swimming fish + bubbles + depth tint, fish tooltips, glass text, workbench, renovation cost, close-room flow.
  User: "기능적으론 거의 완성". Freezers (0.9.1) and the room table (0.9.0) were deployed at the end, user said "베리굿".
- 양식장 contents: `vse_aqua_tank` (power 100/day, -8 per efficiency upgrade, min 4; water tank `vse_aqua_water_tank` 60,000 ml),
  `vse_aqua_feeder` / `vse_aqua_butcher` on the left "설비 거치대" (desktop's bench), `vse_room_table` 18x4 in the middle (RoomTable.cs,
  for sturgeon egg surgery), `vse_freezer_rack` on the right with up to 5 `vse_aqua_freezer` (Freezers.cs: hypertech-bay clone 16x30, no tabs,
  game fridge sprite, only 10 vanilla fish byproduct ids, no cooling; add slot below the last freezer: 5,000/7,500/11,250/16,875₵).
- Background effects in FishSwim.cs (fish = real items in the tank, fry/roe skipped, species speeds, depth tint, bubbles, glass text = own
  lines built from tank state, not the tooltip). Tank tooltip on hover is switched off (`TankTooltipPatch.ShowTankTooltip = false`); fish tooltips on.
- Costs: room expansion 10,000 x1.2 per step; renovation (pick a use) 20,000₵; a renovated room cannot change use, only 폐쇄 (confirm, deletes everything).

Naming the user agreed to: "작업면" = any surface on the background where items go; "설비 거치대" = fixed machines (feeder/butcher);
"작업 테이블" = the player's own table; "냉동고" = fish byproduct storage on the right 작업면.

Pending decisions (ask the user, don't guess):
- Backgrounds are placeholders; the user will bring new art and then adjust coordinates (tank area 435,370 1027x443, table, rack, bench).
  Turn off the red debug border (`RoomPanel.DebugTankBorder`) after that.
- Upgrade content of the aqua feeder / butcher. Whether the water tank should start full.
- 사육장 next: "almost the same as the aquarium". Game `animal_cage`: 10x3 → 10x8 via ingots, no power/water/filters, has cage wear/escape.
  Asked the user: size (25x16 or 30x16), keep wear or not, dedicated feeder/butcher copies, background animal movement. No answer yet.

Rule (user, 2026-11-23): every machine/item this mod adds must be its **own registered item id** in `ShopItems` (factory = game
factory + our changes), never a vanilla item modified after spawning. Saves recreate items from the id, so vanilla-id items lose
structural changes on reload (the tank was a modified `ras` until 0.6.4 and had to be migrated to `vse_aqua_tank`).

Gotchas learned here:
- Register network upgrades and touch `PlayerStore` only after save load (`OnGameLoadedLate`); at `OnEnable` it throws NRE.
- Machine windows: never re-layout in a `MachineryPanelRow.Validate` postfix (froze the game); replace it in a prefix for our row only,
  and shrink panel contents with `MachineryContainerUtils.TryResize(content, 0, 0)` before `ResizePixels(0, 0)` (intakes remember old sizes).
- In a room the `RoomPanel` sits after `windowRootObject` (else invisible game windows over the customer area eat clicks); the cover
  and its hotspots sit before it (so opened machine windows are on top).
- Rooms hide `StoreShutterButton.shutterGameObject` + the blocker parent explicitly (the shutter draws above the cover).

## Open items / plans

- Energy (scav attempts) balance is deferred by the user: leave as is for now.
- Search-location unlock chain (0.38): `FarmLocationData.cs` = 19 locations on the `TransitMap.UnknownSpots` positions (placeholder
  background), `StartOpen` = pharmacy + hardware, `Chain` = what each location's telescope unlocks. Telescope spot appears once every
  spot was searched once; unlock state = afterhour counter `vme_farm_<key>`/`unlocked`. Undiscovered ones show as "미확인 장소".
- Ideas not done: branch visitor budget bonus / tier from attractiveness, contraband raids.
- Nothing has been play-tested since ~0.29; the user reports issues with screenshots.
