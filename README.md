# Probably Stolen personal mods

Source for my personal mods for *Probably Stolen*. Each folder is a separate mod with its own `manifest.xml`; `dotnet build -c Release` inside a folder builds it and copies it to the game's `Mods/` folder.

| Folder | Mod | Mods/ folder |
|---|---|---|
| `PersonalQoLTweaks/` | 개인 편의성 패치 (`personal-qol-tweak`) | `Mods/PersonalQoLTweaks` |
| `map-extension/` | 지도 확장 (`map-extension`) — outing map, scavenging locations | `Mods/MapExtension` |
| `korean-patch/` | 한국어 패치 (`korean-patch`) — Korean translation (merged from Gangmin(RNIN)'s Korean Translation mod) + Mulmaru font for display roles | `Mods/KoreanPatch` |
| `ui-enhancer/` | UI 개선 (`ui-enhancer`) — UI improvements (customer trade window) | `Mods/UiEnhancer` |
| `shop-expansion/` | 가게 증축 (`shop-expansion`) — shop expansion (new, empty skeleton) | `Mods/ShopExpansion` |

`korean-patch/Fonts/Mulmaru.*` is © Mushsooni, licensed under the SIL Open Font License 1.1 (`korean-patch/Fonts/OFL.txt`).
