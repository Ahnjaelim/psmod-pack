using HarmonyLib;

namespace ShopExpansion
{
    // 대형 수조 툴팁: 게임 양식 기계 툴팁(MachineRAS.CreateTooltip)과 같고, 업그레이드 중 저장 공간(고철/금속 주괴) 줄만 뺀다.
    // (대형 수조는 크기가 고정이라 저장 공간 업그레이드가 의미 없음. 효율 업그레이드 줄은 남긴다)
    [HarmonyPatch(typeof(MachineRAS), nameof(MachineRAS.CreateTooltip))]
    internal static class TankTooltipTextPatch
    {
        private static bool Prefix(RichTextBuilder builder, GameItem item)
        {
            if (!AquaTank.Is(item)) return true;
            GameGridInventory tankGrid = MachineRAS.GetTankGrid(item);
            GameItem water = MachineRAS.GetWaterSlot(item)?.childItem;
            builder.AddLine(" ");
            if (water == null)
            {
                builder.AddLine(LocHelper.GetLocalizedMechanic("mech_ras_no_water_tank"), ignoreEmpty: true, RenderHandler.ColorPalette.LightRed);
            }
            else
            {
                int waterMl = AquacultureHelper.GetWaterMl(water);
                int needMl = AquacultureHelper.GetTotalWaterNeedMl(tankGrid);
                float waste = AquacultureHelper.GetWastePercent(water);
                RenderHandler.ColorPalette wc = (needMl == 0 || waterMl >= needMl) ? RenderHandler.ColorPalette.LightCyan
                    : (waterMl >= needMl * 0.5f ? RenderHandler.ColorPalette.Yellow : RenderHandler.ColorPalette.LightRed);
                builder.AddLine(LocHelper.GetLocalizedMechanic("mech_ras_water", waterMl, needMl), ignoreEmpty: true, wc);
                RenderHandler.ColorPalette pc = waste < 3f ? RenderHandler.ColorPalette.LightCyan
                    : (waste < 8f ? RenderHandler.ColorPalette.Yellow : RenderHandler.ColorPalette.LightRed);
                builder.AddLine(LocHelper.GetLocalizedMechanic("mech_ras_waste", waste.ToString("0.0")), ignoreEmpty: true, pc);
            }
            if (tankGrid != null)
            {
                int live = 0, dead = 0;
                foreach (GameItem c in tankGrid.childItems)
                {
                    if (AquacultureHelper.IsLiveStock(c)) live++;
                    else if (AquacultureHelper.IsCarcass(c)) dead++;
                }
                builder.AddLine(dead > 0 ? LocHelper.GetLocalizedMechanic("mech_ras_stock_dead", live, dead) : LocHelper.GetLocalizedMechanic("mech_ras_stock", live),
                    ignoreEmpty: true, dead > 0 ? RenderHandler.ColorPalette.Yellow : RenderHandler.ColorPalette.LightCyan);
                int eel = AquacultureHelper.GetTankPowerOutput(tankGrid);
                if (eel > 0) builder.AddLine(LocHelper.GetLocalizedMechanic("mech_ras_eel_power", eel), ignoreEmpty: true, RenderHandler.ColorPalette.LightBlue);
            }
            MachineFeedDispenser.CreateSlottedTooltip(builder, MachineRAS.GetDispenserSlot(item)?.childItem);
            int eff = item.GetTagReadonly("RAS_POWER_UPGRADE_COUNT_TAG").GetInt();
            builder.AddLine(" ");
            builder.AddLine(LocHelper.GetLocalizedMechanic("mech_fridge_tooltip_upgrade_header"), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
            builder.AddLine(LocHelper.GetLocalizedMechanic("mech_fridge_tooltip_upgrade_efficiency", LocHelper.GetLocalizedItem("item_common_electronic_name"), eff, 12),
                ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
            return false;
        }
    }
}
