using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WaterTankFill
{
    // 물탱크(water_tank)는 게임에서 불투명 용기라 안의 액체가 안 보인다.
    // 모드 폴더 Textures/water_tank_{0,25,50,75,100}.png 를 채운 양에 맞춰 탱크 그림 위에 겹쳐 그린다.
    // 단계는 게임 투명 용기와 같은 SpriteHelper.MapCapacity 기준 (10 단계는 25 그림 사용).
    // 그림은 탱크 원본 그림과 같은 비율의 캔버스(왼쪽 위 기준)로 가정하고, 폭을 원본에 맞춰 크기를 맞춘다.
    [HarmonyPatch(typeof(WaterTankHelper), nameof(WaterTankHelper.Create))]
    internal static class CreatePatch
    {
        private const string ModId = "personal-qol-tweak";
        private static readonly string[] Levels = { "0", "25", "50", "75", "100" };
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static bool _loaded, _logged;

        private static void Postfix(GameItem __result)
        {
            if (__result == null) return;
            __result.getItemSpriteModifierForItemFunc = delegate (GameItem item)
            {
                try
                {
                    Sprite s = SpriteFor(WaterHelper.GetCurrentCapacity(item));
                    if (s == null) return null;
                    return new List<ItemSpriteModifier> { new ItemSpriteModifier(s, Vector2.zero) };
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[WaterTankFill] " + ex.Message);
                    return null;
                }
            };
        }

        private static Sprite SpriteFor(int percent)
        {
            Load();
            string level = SpriteHelper.MapCapacity(percent);
            if (level == "10") level = "25";
            return Cache.TryGetValue(level, out Sprite s) ? s : null;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            Sprite baseSprite = null;
            try { baseSprite = RenderHandler.LoadFromAtlas("Items/items_tool2", "water_tank"); } catch { }
            foreach (string lv in Levels)
            {
                try
                {
                    string path = "Textures/water_tank_" + lv + ".png";
                    if (!ModHelper.ModFileExists(ModId, path)) continue;
                    byte[] bytes = ModHelper.ReadModFileBytes(ModId, path);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(tex, bytes)) continue;
                    tex.filterMode = FilterMode.Point;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    float ppu = 100f;
                    if (baseSprite != null && baseSprite.rect.width > 0f)
                        ppu = baseSprite.pixelsPerUnit * tex.width / baseSprite.rect.width;
                    Cache[lv] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0f, 1f), ppu);
                    if (!_logged)
                    {
                        _logged = true;
                        Debug.Log("[WaterTankFill] tank sprite " + (baseSprite != null ? baseSprite.rect.size.ToString() : "?")
                                  + " ppu " + (baseSprite != null ? baseSprite.pixelsPerUnit.ToString() : "?")
                                  + ", overlay " + tex.width + "x" + tex.height + " ppu " + ppu);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[WaterTankFill] load " + lv + " failed: " + ex.Message);
                }
            }
        }
    }
}
