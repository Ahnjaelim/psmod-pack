using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace GunsmithExtension
{
    // Vanilla Gunsmith Extension: 바닐라 그림만 써서 3D 프린터로 만들 수 있는 총기 부품/프레임을 늘린다.
    //  - 부품 칩: 바닐라 프린터로 못 만들던 총기 부품(총열, 보정기, 손잡이, 개머리판, SMG 탄창 등) 칩 16종
    //  - 프레임: SMG / 리볼버 / 샷건 프레임(+ CNC 프레임 칩). 프레임에 총기 부품을 끼우면 총 완성 (바닐라 수제 권총 프레임과 같은 방식)
    //  - 판매: 은퇴한 총기 장인(retired_gunsmith)이 칩을 늘어놓을 때 확률로 같이 판다.
    // 아이템 등록: 게임의 모드 아이템 디렉터리(ModHook.OnModItemDirectoryInit). ID는 모두 "vge_" 접두사.
    // 주의: 이 아이템이 있는 세이브는 모드를 빼면 해당 아이템을 불러오지 못한다.
    internal static class Gunsmith
    {
        private const string Metal = "Metal Printing Module";
        private const string Cnc = "CNC Module";

        private sealed class PartChip
        {
            public string Id, Target, Sprite;
            public int Count, Price, Progress, Chance;
            public (string id, int count) First;
            public (string id, int count)? Second;
            public bool NeedsMetal;
        }

        private sealed class FrameDef
        {
            public string Id, ChipId, Gun, SpriteSheet, Sprite;
            public int Contraband, Value, ChipPrice, Progress, Chance;
            public (string id, int count) First;
            public (string id, int count)? Second;
        }

        private static PartChip P(string target, int count, int price, int progress, string sprite, (string, int) first, (string, int)? second = null, bool metal = false, int chance = 25)
            => new PartChip { Id = "vge_chip_" + target, Target = target, Count = count, Price = price, Progress = progress, Sprite = sprite, First = first, Second = second, NeedsMetal = metal, Chance = chance };

        private static readonly List<PartChip> Parts = new List<PartChip>
        {
            P("grip",              2, 200, 20, "printer_chip2", ("printer_plastic", 2)),
            P("grip_gel",          2, 300, 30, "printer_chip2", ("printer_plastic", 2)),
            P("stock",             2, 250, 30, "printer_chip2", ("printer_plastic", 3)),
            P("short_stock",       2, 200, 24, "printer_chip2", ("printer_plastic", 2)),
            P("internal_springs",  4, 200, 20, "printer_chip2", ("scrap_metal", 2)),
            P("ammo_display",      2, 300, 30, "printer_chip2", ("printer_plastic", 1), ("common_electronic", 1)),
            P("bipod",             2, 250, 24, "printer_chip4", ("scrap_metal", 3)),
            P("laser2",            1, 400, 36, "printer_chip4", ("scrap_metal", 2), ("common_electronic", 2), chance: 20),
            P("compensator",       2, 300, 30, "printer_chip4", ("scrap_metal", 4), metal: true),
            P("trigger_group",     2, 300, 30, "printer_chip4", ("scrap_metal", 3), metal: true),
            P("barrel",            2, 350, 36, "printer_chip4", ("metal_ingot", 1), metal: true, chance: 20),
            P("match_barrel",      1, 400, 48, "printer_chip4", ("metal_ingot", 1), metal: true, chance: 15),
            P("silencer2",         1, 400, 48, "printer_chip4", ("metal_ingot", 1), ("scrap_metal", 2), metal: true, chance: 15),
            P("mag_10mm_smg",      2, 300, 36, "printer_chip3", ("scrap_metal", 3), metal: true, chance: 20),
            P("mag_10mm_smg_high", 2, 350, 48, "printer_chip3", ("scrap_metal", 4), metal: true, chance: 15),
            P("mag_10mm_smg_box",  1, 400, 60, "printer_chip3", ("scrap_metal", 4), metal: true, chance: 10),
        };

        private static readonly List<FrameDef> Frames = new List<FrameDef>
        {
            new FrameDef { Id = "vge_frame_revolver", ChipId = "vge_chip_frame_revolver", Gun = "revolver", SpriteSheet = "Items/items_weapons_ranged_energy", Sprite = "revolver",
                Contraband = 2, Value = 100, ChipPrice = 500, Progress = 80, First = ("metal_ingot", 1), Second = ("scrap_metal", 2), Chance = 12 },
            new FrameDef { Id = "vge_frame_smg", ChipId = "vge_chip_frame_smg", Gun = "smg", SpriteSheet = "Items/items_weapons_ranged_antique", Sprite = "heavy_smg",
                Contraband = 3, Value = 120, ChipPrice = 600, Progress = 90, First = ("metal_ingot", 2), Chance = 10 },
            new FrameDef { Id = "vge_frame_shotgun", ChipId = "vge_chip_frame_shotgun", Gun = "shotgun", SpriteSheet = "Items/items_weapons_ranged_antique", Sprite = "shotgun",
                Contraband = 3, Value = 130, ChipPrice = 650, Progress = 100, First = ("metal_ingot", 2), Second = ("scrap_metal", 1), Chance = 10 },
        };

        private static bool hooked;

        internal static void Enable()
        {
            if (!hooked)
            {
                hooked = true;
                ModHook.OnModItemDirectoryInit += Register;
            }
            // 모드가 디렉터리보다 늦게 켜진 경우: 이미 있는 디렉터리에 바로 등록
            try
            {
                var dir = UnityEngine.Object.FindObjectOfType<ModItemDirectory>();
                if (dir != null && dir.factoryDictionary != null) Register(dir);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GunsmithExtension] " + ex.Message);
            }
        }

        private static void Register(ModItemDirectory dir)
        {
            if (dir == null) return;
            foreach (PartChip p in Parts)
            {
                PartChip c = p;
                if (!dir.Has(c.Id)) dir.Add(c.Id, () => CreatePartChip(c));
            }
            foreach (FrameDef f in Frames)
            {
                FrameDef c = f;
                if (!dir.Has(c.Id)) dir.Add(c.Id, () => CreateFrame(c));
                if (!dir.Has(c.ChipId)) dir.Add(c.ChipId, () => CreateFrameChip(c));
            }
        }

        private static GameItem CreatePartChip(PartChip p)
        {
            GameItem chip = ItemDirectory.CreateEmptyItem(p.Id).SetSpriteAndShape("Items/items_tool", p.Sprite);
            chip.name = L("vge_chip_name", "3D Printer Chip (x{0} {1})", p.Count, GeneralHelper.GetDisplayNameFromID(p.Target));
            chip.shortDescription = LocHelper.GetLocalizedItem("item_printer_chip_desc");
            chip.unitValue = p.Price;
            chip.EnableTag("PRINTER_CHIP_TAG");
            SetRequirements(chip, p.First, p.Second);
            MachineProgressHelper.InitProgressSourceItem(chip, p.Progress, p.Target, true, p.Count, p.NeedsMetal ? Metal : null);
            AudioHelper.InitSmallModule(chip);
            return chip;
        }

        private static GameItem CreateFrame(FrameDef f)
        {
            GameItem frame = ItemDirectory.CreateEmptyItem(f.Id).SetSpriteAndShape(f.SpriteSheet, f.Sprite);
            frame.name = L("vge_frame_name", "{0} Frame", GeneralHelper.GetDisplayNameFromID(f.Gun));
            frame.EnableManufacturer("spacer");
            frame.SetValue(f.Value);
            GunFrameHelper.InitFrame(frame, f.Gun, f.Contraband);
            return frame;
        }

        private static GameItem CreateFrameChip(FrameDef f)
        {
            GameItem chip = ItemDirectory.CreateEmptyItem(f.ChipId).SetSpriteAndShape("Items/items_tool", "printer_chip_pistol");
            chip.name = L("vge_frame_chip_name", "{0} Frame 3D Printer Chip", GeneralHelper.GetDisplayNameFromID(f.Gun));
            chip.shortDescription = LocHelper.GetLocalizedItem("item_printer_chip_desc");
            chip.unitValue = f.ChipPrice;
            chip.EnableTag("PRINTER_CHIP_TAG");
            chip.EnableTag("PRINTER_FRAME_CHIP_TAG");
            SetRequirements(chip, f.First, f.Second);
            MachineProgressHelper.InitProgressSourceItem(chip, f.Progress, f.Id, true, 1, Metal);
            MachineProgressHelper.AddRequiredMachineTag(chip, Cnc);
            AudioHelper.InitSmallModule(chip);
            return chip;
        }

        private static void SetRequirements(GameItem chip, (string id, int count) first, (string id, int count)? second)
        {
            if (second.HasValue)
                MachineProgressHelper.SetDualAdditionalRequiredItem(chip, (itemID: first.id, count: first.count), (itemID: second.Value.id, count: second.Value.count));
            else
                MachineProgressHelper.SetSingleAdditionalRequiredItem(chip, first.id, first.count);
        }

        // 은퇴한 총기 장인이 바닐라 칩을 늘어놓을 때(항상 나오는 개량 내부 스프링 칩 직후) 확장 칩을 확률로 추가.
        internal static void AddToGunsmithTable(PlayerStore store)
        {
            foreach (PartChip p in Parts)
                if (RNG.Roll(p.Chance)) store.AddDirectSellingItemToTable(p.Id);
            foreach (FrameDef f in Frames)
                if (RNG.Roll(f.Chance)) store.AddDirectSellingItemToTable(f.ChipId);
        }

        private static string L(string key, string en, params object[] args)
        {
            string s = PenLabelerAssist.Text.Get(key, args);
            if (!string.IsNullOrEmpty(s) && s != key && !s.Contains("{0}")) return s;
            try { return string.Format(string.IsNullOrEmpty(s) || s == key ? en : s, args); }
            catch { return en; }
        }
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.AddDirectSellingItemToTable), new[] { typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(int) })]
    internal static class GunsmithTablePatch
    {
        private static void Postfix(PlayerStore __instance, string identifier)
        {
            if (identifier != "printer_chip_internal_springs_improved") return;
            try
            {
                Gunsmith.AddToGunsmithTable(__instance);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GunsmithExtension] " + ex.Message);
            }
        }
    }
}
