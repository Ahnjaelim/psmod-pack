using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ShopExpansion
{
    // 수족관 전용 도축기 = 틸라피아 물 관리 (0.11.0).
    //  - 틸라피아(일반/하수/이식용 모두)만 잡는다. 장어·철갑상어는 손으로 잡으니 건드리지 않는다.
    //  - 밤(과 [지금 도축])마다: 수조 물 필요량이 물탱크의 상한 %(기본 90, 슬라이더)를 넘으면 아래 순서로 상한 아래가 될 때까지 잡는다.
    //      1. 600g 넘은 틸라피아 (무거운 것부터)  2. 13일 넘은 틸라피아 (나이 많은 것부터)  3. 나머지 중 가장 작은 것부터
    //  - 이름 붙였거나 표시한 개체(게임 보호 규칙)는 안 잡는다. 죽은 틸라피아는 항상 손질한다. 치어 무리(그룹)는 개체가 아니라 그대로.
    //  - 창: 원래 수조 탭 대신 "틸라피아" 탭 [켜기 / 상한 슬라이더 / 지금 물 사용량 / 순서 안내]. 설정은 도축기 아이템 태그(세이브에 저장).
    internal static class AquaButcher
    {
        internal const string OnTag = "VSE_AB_WATER_ON_BOOL";
        internal const string PctTag = "VSE_AB_WATER_PCT_INT";
        internal const int DefaultPct = 90, MinPct = 50, MaxPct = 100;
        private const int HeavyGrams = 600, OldDays = 13;

        private static readonly HashSet<string> Tilapia = new HashSet<string> { "FISH_TILAPIA", "FISH_GUTTER_TILAPIA", "FISH_GRAFT_TILAPIA" };

        internal static bool IsOn(GameItem m) => !m.IsTag(OnTag) || m.GetTagReadonly(OnTag).GetBool();
        internal static int Pct(GameItem m) => m.IsTag(PctTag) ? Mathf.Clamp(m.GetTagReadonly(PctTag).GetInt(), MinPct, MaxPct) : DefaultPct;

        private static bool IsTilapia(GameItem f) => Tilapia.Contains(f.GetTagReadonly("FISH_SPECIES_TAG").GetString() ?? "");
        private static int Age(GameItem f) => f.GetTagReadonly("FISH_AGE_TAG").GetInt();

        // ---- 창: 원래 수조 탭을 "틸라피아" 탭으로 바꾼다 (ShopItems.CreateAquaButcher 에서, 우리 탭을 뺀 뒤) ----
        internal static void BuildPage(GameItem m)
        {
            MachineryTabs tabs = FindTabs(m);
            if (tabs == null) { Debug.LogWarning("[ShopExpansion] aqua butcher: tabs not found"); return; }
            if (!m.IsTag(OnTag)) m.ModifyTag(OnTag, s => s.Enable().SetBool(true));
            if (!m.IsTag(PctTag)) m.ModifyTag(PctTag, s => s.Enable().SetInt(DefaultPct));

            MachineryRadioButton on = null;
            MachineryText pctText = new MachineryText(PctCaption(DefaultPct), 6, HorizontalAlignmentOptions.Left, uppercase: false);
            MachinerySlider pct = new MachinerySlider(DefaultPct).SetRange(MinPct, MaxPct, 1f);
            MachineryText status = new MachineryText("", 6, HorizontalAlignmentOptions.Left, uppercase: false);
            on = new MachineryRadioButton("틸라피아 물 관리 도축", true, v =>
            {
                m.ModifyTag(OnTag, s => s.Enable().SetBool(v));
                pct.SetDisabled(!v);
            });
            pct.onChanged = v =>
            {
                int p = Mathf.RoundToInt(v);
                m.ModifyTag(PctTag, s => s.Enable().SetInt(p));
                pctText.SetText(PctCaption(p));
                status.SetText(StatusText(m));
            };
            MachineryStack page = MachineryStack.Vertical(
                on,
                pctText,
                pct,
                status,
                new MachineryText("잡는 순서", 6, HorizontalAlignmentOptions.Left, uppercase: false),
                new MachineryText("1. " + HeavyGrams + "g 넘은 틸라피아", 6, HorizontalAlignmentOptions.Left, uppercase: false),
                new MachineryText("2. " + OldDays + "일 넘은 틸라피아", 6, HorizontalAlignmentOptions.Left, uppercase: false),
                new MachineryText("3. 가장 작은 틸라피아부터", 6, HorizontalAlignmentOptions.Left, uppercase: false),
                new MachineryText("장어·철갑상어, 이름 붙인 개체는 안 잡음", 6, HorizontalAlignmentOptions.Left, uppercase: false));
            page.SetAlign(MachineryStack.Align.Start);

            int added = tabs.AddTab("틸라피아", page);
            PixelElement old = tabs.GetPage(0);
            if (added > 0 && old != null) tabs.Detach(old); // 원래 수조 탭 (무게/나이 규칙)은 뺀다
            tabs.Select(0);

            void Sync()
            {
                try
                {
                    bool o = IsOn(m);
                    int p = Pct(m);
                    on.SetOn(o);
                    pct.SetValue(p);
                    pct.SetDisabled(!o);
                    pctText.SetText(PctCaption(p));
                    status.SetText(StatusText(m));
                }
                catch (Exception ex) { Debug.LogWarning("[ShopExpansion] aqua butcher sync: " + ex.Message); }
            }
            m.onLoaded = (Action<GameItem>)Delegate.Combine(m.onLoaded, (Action<GameItem>)(_ => Sync()));
            if (m.contentWindow != null) m.contentWindow.openCallback = (Action)Delegate.Combine(m.contentWindow.openCallback, new Action(Sync));
            Sync();
        }

        private static MachineryTabs FindTabs(GameItem m)
        {
            if (m.contentWindow == null) return null;
            foreach (TreeNodeRender t in m.contentWindow.handler.GetComponentsInChildren<TreeNodeRender>(true))
                if (t.element is MachineryTabs tabs) return tabs;
            return null;
        }

        private static string PctCaption(int p) => "물 사용량 상한: " + p + "%";

        private static string StatusText(GameItem m)
        {
            if (!Water(m, out int water, out int need)) return "지금: 수조에 꽂혀 있지 않음";
            int now = water > 0 ? Mathf.RoundToInt(need * 100f / water) : 0;
            return "지금: " + now + "% (" + need.ToString("N0") + " / " + water.ToString("N0") + " ml)";
        }

        // 도축기가 꽂힌 수조의 물 양과 물 필요량
        private static bool Water(GameItem m, out int water, out int need)
        {
            water = need = 0;
            GameItem host = MachineAutoButcher.GetHost(m);
            if (host == null) return false;
            GameItem tank = MachineRAS.GetWaterSlot(host)?.childItem;
            GameGridInventory grid = MachineRAS.GetTankGrid(host);
            if (grid == null) return false;
            water = tank != null ? AquacultureHelper.GetWaterMl(tank) : 0;
            need = AquacultureHelper.GetTotalWaterNeedMl(grid);
            return true;
        }

        // ---- 도축 (MachineAutoButcher.RunTank 대신) ----
        internal static void Run(GameItem m, IEnumerable<GameItem> items, GameItem dispenser, GameGridInventory tray, ref int count)
        {
            List<GameItem> fish = items.Where(f => AquacultureHelper.IsFish(f) && IsTilapia(f)).ToList();
            // 죽은 틸라피아는 항상 손질 (물은 안 쓰지만 수조에 남지 않게)
            foreach (GameItem f in fish.Where(AquacultureHelper.IsDead).ToList())
            {
                int r = ButcherCalls.ProcessFish(m, f, tray, dispenser);
                if (r == 0) count++;
                else if (r == 1) return; // 출력 칸이 꽉 참
            }
            if (!Water(m, out int water, out int need)) return;
            int cap = water * Pct(m) / 100;
            if (need <= cap) return;

            List<GameItem> live = fish.Where(f => !AquacultureHelper.IsDead(f) && !MachineAutoButcher.IsProtected(f)).ToList();
            var order = new List<GameItem>();
            order.AddRange(live.Where(f => AquacultureHelper.GetWeightGrams(f) > HeavyGrams).OrderByDescending(AquacultureHelper.GetWeightGrams));
            order.AddRange(live.Where(f => !order.Contains(f) && Age(f) > OldDays).OrderByDescending(Age));
            order.AddRange(live.Where(f => !order.Contains(f)).OrderBy(AquacultureHelper.GetWeightGrams));

            int done = 0;
            foreach (GameItem f in order)
            {
                if (need <= cap) break;
                int w = AquacultureHelper.GetWaterNeedMl(f);
                int r = ButcherCalls.ProcessFish(m, f, tray, dispenser);
                if (r == 0) { count++; done++; need -= w; }
                else if (r == 1) { Debug.Log("[ShopExpansion] aqua butcher: tray full"); break; }
            }
            Debug.Log("[ShopExpansion] aqua butcher: culled " + done + " tilapia, water need " + need + " / cap " + cap + " (" + water + " ml)");
        }
    }

    internal static class ButcherCalls
    {
        // 게임 ProcessFish (결과: 0 = 도축함, 1 = 출력 칸 꽉 참, 2 = 건너뜀)
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(MachineAutoButcher), "ProcessFish")]
        public static int ProcessFish(GameItem machine, GameItem fish, GameGridInventory tray, GameItem dispenser)
        {
            throw new NotImplementedException("reverse patch stub");
        }
    }

    // 수족관 전용 도축기일 때만 게임 수조 도축 대신 틸라피아 물 관리. 꺼 두면 원래 처리(원래 수조 탭 설정 = 기본 끔).
    [HarmonyPatch(typeof(MachineAutoButcher), "RunTank")]
    internal static class AquaButcherRunPatch
    {
        private static bool Prefix(GameItem machine, IEnumerable<GameItem> items, GameItem dispenser, GameGridInventory tray, ref int count)
        {
            if (machine == null || machine.identifier != ShopItems.AquaButcher || !AquaButcher.IsOn(machine)) return true;
            try { AquaButcher.Run(machine, items, dispenser, tray, ref count); }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] aqua butcher run: " + ex); }
            return false;
        }
    }
}
