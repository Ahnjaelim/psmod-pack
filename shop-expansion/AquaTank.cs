using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ShopExpansion
{
    // 양식장 방의 붙박이 대형 수조 = 게임의 진짜 순환 여과 양식 시스템(ras) 아이템을 숨김 인벤(HiddenStore)에 넣어 둔 것.
    //  - 수조 크기: 게임 수조 확장 공식 (8 + 가로 확장) × (4 + 세로 확장) 을 이용해 하이퍼테크 보관함 한 칸(16 × 30)을 눕힌 가로형 30 × 16 으로.
    //  - 전력: 풀업 기계(1/일) 4대분 = 4/일 고정 (게임 공식대로면 확장만큼 크게 늘어난다).
    //  - 창 배치: 왼쪽 세로 줄 [전원 / 물 / 필터] + 오른쪽 큰 수조. 급이기·도축 칸은 숨김 (나중에 방 밖 장치로 연결 예정)
    //  - 성장/먹이/오염/번식은 게임 기계 처리 그대로 (밤마다 HiddenStore.Night).
    internal static class AquaTank
    {
        internal const string Tag = "VSE_ROOM_TANK_TAG";
        internal const int W = 30, H = 16;   // 하이퍼테크 보관함 한 칸(16x30)을 눕힌 가로형 (세로 30칸은 화면에 안 들어감)
        internal const int Power = 4;        // 하루 전력

        private static string Key(int room) => "room" + room + "/aqua-tank";

        private static readonly HashSet<MachineryPanelRow> rows = new HashSet<MachineryPanelRow>();

        internal static bool Is(GameItem m) => m != null && m.IsTag(Tag);

        internal static GameItem Get(int room) => HiddenStore.Find(Key(room));

        private static GameItem Ensure(int room)
        {
            GameItem m = HiddenStore.GetOrCreate(Key(room), () => ItemSpawner.Spawn("ras"));
            if (m != null) Setup(m);
            return m;
        }

        // 만들 때와 불러온 뒤마다: 표시, 크기, 전력, 이름, 창 배치 등록
        internal static void Setup(GameItem m)
        {
            m.ModifyTag(Tag, s => s.Enable());
            m.ModifyTag("RAS_WIDTH_UPGRADE_COUNT_TAG", s => s.Enable().SetInt(W - 8));
            m.ModifyTag("RAS_HEIGHT_UPGRADE_COUNT_TAG", s => s.Enable().SetInt(H - 4));
            m.EnableTag("IS_OWNED_TAG");
            m.name = "대형 수조";
            EnsureFittings(m);
            try { MachineRAS.ApplyTankSize(m); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] tank size: " + ex.Message); }
            try { MachineRAS.UpdatePowerUsage(m); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] tank power: " + ex.Message); }
            if (m.contentWindow?.childElement is MachineryPanelRow row)
            {
                // 급이기·도축 칸은 창에서 숨긴다 (칸 자체는 그대로 있어 저장/밤 처리는 영향 없음). 나중에 방 밖 장치로 연결 예정
                foreach (string r in HiddenRoles)
                {
                    MachineryPanel hp = row.GetPanel(r);
                    if (hp != null && !hp.isHidden) hp.SetHidden(true);
                }
                rows.Add(row);
                try { row.Validate(); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] tank layout: " + ex.Message); }
                try { m.contentWindow.Validate(); } catch { }
            }
        }

        internal static void AfterLoad()
        {
            ResetBench();
            rows.Clear();
            lockedSlots.Clear();
            foreach (GameItem m in HiddenStore.Machines().ToList())
                if (Is(m)) Setup(m);
        }

        // 양식장으로 정했을 때: 수조 + 전용 급이기/도축기를 미리 만들어 둔다
        internal static void Prepare(int room) => Ensure(room);

        // 수조의 숨긴 급이기/도축 칸에 수족관 전용 기계를 꽂고, 빼지 못하게 막는다
        private static readonly HashSet<GameInventory> lockedSlots = new HashSet<GameInventory>();

        private static void EnsureFittings(GameItem m)
        {
            Fit(m, MachineRAS.GetDispenserSlot(m), ShopItems.AquaFeeder);
            Fit(m, MachineRAS.GetButcherSlot(m), ShopItems.AquaButcher);
        }

        private static void Fit(GameItem m, GameSlotInventory slot, string id)
        {
            if (slot == null) { Debug.LogWarning("[ShopExpansion] tank slot for " + id + " not found"); return; }
            if (slot.childItem == null && !Benched(m)) // 작업대에 꺼내 둔 동안은 칸이 비어 있는 게 정상 (새로 만들면 둘이 된다)
            {
                GameItem f = ShopItems.Spawn(id);
                if (f != null)
                {
                    f.EnableTag("IS_OWNED_TAG");
                    slot.TryAcceptAll(f);
                    if (slot.childItem != f) Debug.LogWarning("[ShopExpansion] could not fit " + id + " into the tank");
                }
            }
            if (lockedSlots.Add(slot))
            {
                var prev = slot.mayInventoryRemoveItemFunc;
                slot.mayInventoryRemoveItemFunc = (item, inv) =>
                    (moving || item == null || !item.IsTag(ShopItems.FixedTag)) && (prev == null || prev(item, inv));
            }
        }

        // ---- 작업대: 양식장에 있는 동안 급이기/도축기를 바닥의 진짜 게임 인벤 창에 꺼내 둔다 ----
        // 게임 아이템 그대로라 크기/툴팁/물건 끌어다 넣기/더블클릭 창 열기가 원래 기계와 똑같다.
        // 방을 나갈 때, 저장할 때, 밤 처리 전에는 수조 칸에 다시 꽂는다 (수조가 칸에 꽂힌 기계로 먹이 주기/도축을 하기 때문)
        private static PixelWindow benchWin;
        private static GameInventory benchInv;
        private static GameItem benchHolder;
        private static int benchRoom = -1;
        private static bool moving;

        private static bool Benched(GameItem tank) => benchRoom > 0 && tank != null && Get(benchRoom) == tank;

        private static void EnsureBench(GameItem f, GameItem b)
        {
            if (benchWin != null) return;
            int w = f.shape.width + 1 + b.shape.width;
            int h = Mathf.Max(f.shape.height, b.shape.height);
            (benchWin, benchInv) = DirectoryUtils.CreateInventoryWindow(w, h, false);
            try { benchWin.SetTitle("작업대"); } catch { }
            // 주운 물건이 내 소유라 소유 표시 없는 칸에는 못 들어간다 → 창 주인 아이템도 내 소유로 (HiddenStore 와 같은 방식)
            benchHolder = ItemDirectory.CreateEmptyItem("vse_bench_holder").SetContentWindow(benchWin).SetSpriteAndShape("Items/items_container", "evidence_box");
            benchHolder.name = "작업대";
            benchHolder.EnableTag("IS_OWNED_TAG");
            // 붙박이 기계만 올라가고, 끌어서 뺄 수 없다
            benchInv.mayInventoryAddItemFunc = (item, inv) => item != null && item.IsTag(ShopItems.FixedTag);
            benchInv.mayInventoryRemoveItemFunc = (item, inv) => moving || item == null || !item.IsTag(ShopItems.FixedTag);
        }

        // 양식장 방에 들어왔을 때 (배경을 바꿀 때마다 불림)
        internal static void ShowBench(int room)
        {
            if (benchRoom == room) { Place(); return; }
            StowBench();
            GameItem m = Ensure(room);
            if (m == null) return;
            GameSlotInventory fs = MachineRAS.GetDispenserSlot(m), bs = MachineRAS.GetButcherSlot(m);
            GameItem f = fs?.childItem, b = bs?.childItem;
            if (f == null || b == null) { Debug.LogWarning("[ShopExpansion] bench: fittings missing"); return; }
            EnsureBench(f, b);
            moving = true;
            try
            {
                foreach (GameItem i in new[] { f, b })
                {
                    i.parentInventory?.Expel(i);
                    if (GraphUtils.TryAcceptAll(benchInv, i) != 0)
                    {
                        Debug.LogWarning("[ShopExpansion] bench: " + i.name + " did not fit, back into the tank");
                        (i == f ? fs : bs).TryAcceptAll(i);
                    }
                }
            }
            finally { moving = false; }
            benchRoom = room;
            Place();
        }

        // 메인 인벤 왼쪽 바닥 (1920x1080 기준 왼쪽 위 = x 150, 아래에서 y 400)
        private static void Place()
        {
            if (benchWin == null) return;
            try { benchWin.Show(new Vector2(Screen.width * 150f / 1920f, Screen.height * 400f / 1080f)); }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] bench show: " + ex.Message); }
        }

        // 작업대의 기계를 수조 칸으로 되돌린다 (방을 나갈 때 / 저장 / 밤)
        internal static void StowBench()
        {
            if (benchRoom <= 0 || benchInv == null) { benchRoom = -1; return; }
            GameItem m = Get(benchRoom);
            moving = true;
            try
            {
                foreach (GameItem i in new List<GameItem>(benchInv.childItems))
                {
                    try { i.contentWindow?.Hide(); } catch { }
                    GameSlotInventory slot = m == null ? null : i.IsTag("FEED_DISPENSER_TAG") ? MachineRAS.GetDispenserSlot(m) : MachineRAS.GetButcherSlot(m);
                    if (slot == null || slot.childItem != null) { Debug.LogWarning("[ShopExpansion] bench: no slot for " + i.name); continue; }
                    benchInv.Expel(i);
                    slot.TryAcceptAll(i);
                    if (slot.childItem != i) { Debug.LogWarning("[ShopExpansion] bench: " + i.name + " did not go back into the tank"); GraphUtils.TryAcceptAll(benchInv, i); }
                }
            }
            finally { moving = false; }
            try { benchWin?.Hide(); } catch { }
            benchRoom = -1;
        }

        // 세이브를 새로 불러올 때: 작업대에 남은 옛 세이브 기계는 버린다 (불러온 세이브의 기계는 수조 칸에 있다)
        internal static void ResetBench()
        {
            if (benchInv != null)
                foreach (GameItem i in new List<GameItem>(benchInv.childItems))
                {
                    try { i.contentWindow?.Hide(); benchInv.Expel(i); i.Destroy(); } catch { }
                }
            try { benchWin?.Hide(); } catch { }
            benchRoom = -1;
        }

        private static int savedBenchRoom = -1;

        // 저장 직전: 칸으로 되돌려 같이 저장 / 저장 뒤: 다시 작업대로
        internal static void BeforeSave() { savedBenchRoom = benchRoom; StowBench(); }
        internal static void AfterSave() { if (savedBenchRoom > 0 && Rooms.Current == savedBenchRoom) ShowBench(savedBenchRoom); savedBenchRoom = -1; }

        // 작업대의 급이기/도축기를 누르면: 그 기계 창 열기 / 다시 누르면 닫기
        internal static void ToggleFitting(int room, bool feeder)
        {
            GameItem m = Ensure(room);
            if (m == null) return;
            GameItem f = feeder ? MachineRAS.GetDispenserSlot(m)?.childItem : MachineRAS.GetButcherSlot(m)?.childItem;
            PixelWindow w = f?.contentWindow;
            if (w == null) return;
            try { AudioManager.Instance.Play2("click_vintage"); } catch { }
            if (w.IsVisible()) { w.Hide(); return; }
            w.Show();
            w.Center();
        }

        // 방의 수조를 누르면: 창 열기 / 다시 누르면 닫기
        internal static void Toggle(int room)
        {
            GameItem m = Ensure(room);
            PixelWindow w = m?.contentWindow;
            if (w == null) return;
            try { AudioManager.Instance.Play2("click_vintage"); } catch { }
            if (w.IsVisible()) { w.Hide(); return; }
            w.Show();
            w.Center();
        }

        // 가게로 돌아가거나 다른 방으로 갈 때 수조 창은 닫는다
        internal static void CloseAll()
        {
            StowBench();
            foreach (GameItem m in HiddenStore.Machines())
            {
                if (!Is(m)) continue;
                try { if (m.contentWindow != null && m.contentWindow.IsVisible()) m.contentWindow.Hide(); } catch { }
                foreach (GameItem f in new[] { MachineRAS.GetDispenserSlot(m)?.childItem, MachineRAS.GetButcherSlot(m)?.childItem })
                {
                    try { if (f?.contentWindow != null && f.contentWindow.IsVisible()) f.contentWindow.Hide(); } catch { }
                }
            }
        }

        // ---- 창 배치: 왼쪽 세로 줄 + 오른쪽 수조 ----
        private static bool layingOut;
        private static readonly string[] LeftRoles = { "power", "water", "filters", "dispenser", "butcher" };
        private static readonly string[] HiddenRoles = { "dispenser", "butcher" };

        private static int frameSeen = -1, callsThisFrame;
        private static bool warnedLoop;

        internal static bool Owns(MachineryPanelRow row) => row != null && rows.Contains(row);

        // 게임 배치(가로 한 줄) 대신 이것만 돈다 (TankLayoutPatch Prefix). 게임 배치와 번갈아 돌면 서로 다시 불러 멈췄다 (0.3.1)
        internal static void Relayout(MachineryPanelRow row)
        {
            if (layingOut) return;
            // 안전장치: 한 프레임에 너무 많이 불리면 그만 (무한 반복 방지)
            if (Time.frameCount != frameSeen) { frameSeen = Time.frameCount; callsThisFrame = 0; }
            if (++callsThisFrame > 30)
            {
                if (!warnedLoop) { warnedLoop = true; Debug.LogWarning("[ShopExpansion] tank relayout called too often in one frame; skipping"); }
                return;
            }
            layingOut = true;
            try
            {
                MachineryPanel tank = row.GetPanel("tank");
                var left = new List<MachineryPanel>();
                foreach (string r in LeftRoles)
                {
                    MachineryPanel p = row.GetPanel(r);
                    if (p != null && !p.isHidden) left.Add(p);
                }
                if (tank == null || left.Count == 0) return;

                // 각 칸의 원래 크기 (행 배치 때 늘어난 크기를 되돌린다)
                // 칸 안쪽(MachineryIntake 등)도 요청 크기를 기억하고 있어서, 안쪽부터 0 으로 풀어야 원래 크기로 돌아온다 (0.3.2)
                foreach (MachineryPanel p in left) ShrinkToNatural(p);
                ShrinkToNatural(tank);
                int colW = left.Max(p => p.widthPixels);
                int y = 0;
                var sepYs = new List<int>();
                foreach (MachineryPanel p in left)
                {
                    int h = p.heightPixels;
                    p.ResizePixels(colW, h);
                    p.handler.rectTransform.anchoredPosition = new Vector2(0f, -y);
                    y += h;
                    sepYs.Add(y);
                    y += 1;
                }
                int leftH = y - 1;
                sepYs.RemoveAt(sepYs.Count - 1);
                int tankH = Mathf.Max(tank.heightPixels, leftH);
                // 왼쪽 줄이 더 짧으면 마지막 칸을 늘려 맞춘다
                if (leftH < tankH)
                {
                    MachineryPanel last = left[left.Count - 1];
                    last.ResizePixels(colW, last.heightPixels + (tankH - leftH));
                }
                tank.ResizePixels(tank.widthPixels, tankH);
                int tankX = colW + 1;
                tank.handler.rectTransform.anchoredPosition = new Vector2(tankX, 0f);
                int width = tankX + tank.widthPixels;

                // 구분선: 게임이 그린 것(가로 한 줄 배치용)은 끄고 새로
                HideSeparators(row);
                int idx = 0;
                PlaceSeparator(row, idx++, colW, 0, 1, tankH - 4, -1, -1, tankH);   // 세로 (왼쪽 줄 | 수조)
                foreach (int sy in sepYs) PlaceSeparator(row, idx++, 4, sy, Mathf.Max(0, colW - 8), 1, 0, colW, -1); // 왼쪽 줄 칸 사이

                Traverse.Create(row).Property("widthPixels").SetValue(width);
                Traverse.Create(row).Property("heightPixels").SetValue(tankH);
                MachineryStyle.Place(row.inner, 0, 0, width, tankH);
                row.handler.parent?.FindParentElement<GraphNodeStorage>()?.Validate();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ShopExpansion] tank relayout: " + ex);
            }
            finally
            {
                layingOut = false;
            }
        }

        private static void ShrinkToNatural(MachineryPanel p)
        {
            MachineryContainerUtils.TryResize(p.content, 0, 0);
            p.ResizePixels(0, 0);
        }

        private static void HideSeparators(MachineryPanelRow row)
        {
            foreach (string f in new[] { "separators", "separatorBackings" })
            {
                if (Traverse.Create(row).Field(f).GetValue() is System.Collections.IList list)
                    foreach (object o in list)
                        if (o is TreeNodeRender t && t.image != null) t.image.enabled = false;
            }
        }

        private static void PlaceSeparator(MachineryPanelRow row, int index, int x, int y, int w, int h, int backingX, int backingW, int backingH)
        {
            Traverse.Create(row).Method("PlaceSeparator", index, x, y, w, h, backingX, backingW, backingH).GetValue();
        }
    }

    // 대형 수조 창: 게임 배치(가로 한 줄)를 건너뛰고 우리 배치만
    [HarmonyPatch(typeof(MachineryPanelRow), nameof(MachineryPanelRow.Validate))]
    internal static class TankLayoutPatch
    {
        private static bool Prefix(MachineryPanelRow __instance)
        {
            if (!AquaTank.Owns(__instance)) return true;
            AquaTank.Relayout(__instance);
            return false;
        }
    }

    // 대형 수조 전력: 게임 공식(확장만큼 증가) 대신 고정값
    [HarmonyPatch(typeof(MachineRAS), nameof(MachineRAS.UpdatePowerUsage))]
    internal static class TankPowerPatch
    {
        private static void Postfix(GameItem ras)
        {
            if (!AquaTank.Is(ras)) return;
            ras.ModifyTag("machinery_drawn_power", s => s.Enable().SetInt(AquaTank.Power));
        }
    }
}
