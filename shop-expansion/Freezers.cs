using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShopExpansion
{
    // 양식장 오른쪽 작업면: 냉동고 거치대 (붙박이, 사용자가 옮길 수 없음).
    //  - 냉동고 = 정식 아이템 vse_aqua_freezer: 하이퍼테크 보관함 복제(탭 없이 16x30 한 칸), 그림은 게임 냉장고(Items/items_ship fridge).
    //    생선 부속물(게임 원래 아이템 10종)만 들어간다. 생선살은 원래 안 상하므로 냉장 기능은 없음(이름만 냉동고).
    //  - 거치대 = 정식 아이템 vse_freezer_rack: 투명 창 + 흐린 격자(냉동고 1칸 크기 x 5 세로). 냉동고만 올라가고 뺄 수 없다.
    //    숨김 인벤(room<n>/freezer-rack)에 두어 저장/밤 처리. 방에 있는 동안 오른쪽에 창을 띄운다 (작업 테이블과 같은 방식).
    //  - 개조 때 1개 기본, 다음 자리(점선 빈칸)를 눌러 추가: 5,000 / 7,500 / 11,250 / 16,875₵. 최대 5개.
    //  - 방 폐쇄 시 거치대와 냉동고, 안의 물건 모두 삭제.
    internal static class Freezers
    {
        internal const int Max = 5;
        private static readonly int[] AddCost = { 0, 5000, 7500, 11250, 16875 }; // [n] = n+1 번째 냉동고 값
        internal const int InnerW = 16, InnerH = 30;                              // 하이퍼테크 보관함 한 칸

        // 넣을 수 있는 것: 생선 손질/알 꺼내기로 나오는 게임 원래 아이템만 (다른 모드 아이템 제외)
        internal static readonly HashSet<string> Allowed = new HashSet<string>
        {
            "large_fish_fillet", "fish_fillet", "small_fish_fillet", "tiny_fish_fillet",
            "eel_fillet", "small_eel_fillet", "fish_scrap", "tilapia_skin_graft",
            "caviar", "sturgeon_roe",
        };

        // 거치대 위치 (1920x1080 기준): 왼쪽 위 모서리
        private const float RackLeft = 1500f, RackTop = 130f;

        private static string Key(int room) => "room" + room + "/freezer-rack";

        internal static bool UsedBy(RoomPurpose p) => p != null && p.Key == "aquaculture";

        internal static int CostOf(int index) => index >= 0 && index < AddCost.Length ? AddCost[index] : 0;

        // ---- 아이템 공장 ----
        internal static GameItem CreateFreezer()
        {
            var grid = new GameGridInventory(InnerW, InnerH);
            PixelWindow win = MachineHelper.CreateMachineryWindow(true, MachineHelper.Panel(null, MachineHelper.WrapInIntake(grid), "storage"));
            GameItem m = ItemDirectory.CreateEmptyItem().SetContentWindow(win).SetSpriteAndShape("Items/items_ship", "fridge");
            ContainerHelper.InitContainerItem(grid, m);
            // 생선 부속물만 (게임 보관함 기본 규칙: 내 물건, 내 보관함)
            grid.mayInventoryAddItemFunc = (item, inv) =>
                item != null && GeneralHelper.IsItemOwned(item) && Allowed.Contains(item.identifier ?? "");
            m.name = "냉동고";
            m.shortDescription = "생선살, 생선 자투리, 캐비어 같은 생선 부속물만 보관하는 냉동고.";
            m.EnableTag(ShopItems.FixedTag);
            m.EnableTag("IS_OWNED_TAG");
            try { win.SetTitle("냉동고"); } catch { }
            return m;
        }

        private static Vector2Int freezerShape = new Vector2Int(-1, -1);

        // 냉동고 한 개가 차지하는 칸 (게임 냉장고 그림 크기)
        private static Vector2Int FreezerShape()
        {
            if (freezerShape.x > 0) return freezerShape;
            freezerShape = new Vector2Int(3, 3);
            try
            {
                GameItem sample = ItemDirectory.CreateEmptyItem().SetSpriteAndShape("Items/items_ship", "fridge");
                if (sample.shape != null) freezerShape = new Vector2Int(sample.shape.width, sample.shape.height);
                sample.Destroy();
            }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] freezer shape: " + ex.Message); }
            return freezerShape;
        }

        private static bool moving;

        internal static GameItem CreateRack()
        {
            Vector2Int s = FreezerShape();
            var win = new PixelWindow();
            win.SetTransparency(0.99f);
            var grid = new GameGridInventory(s.x, s.y * Max);
            grid.SetTransparency(0.9f);
            win.Attach(grid);
            win.Validate();
            grid.mayInventoryAddItemFunc = (item, inv) => moving || (item != null && item.identifier == ShopItems.AquaFreezer);
            grid.mayInventoryRemoveItemFunc = (item, inv) => moving || item == null || !item.IsTag(ShopItems.FixedTag);
            GameItem box = ItemDirectory.CreateEmptyItem().SetContentWindow(win).SetSpriteAndShape("Items/items_container", "evidence_box");
            box.name = "냉동고 거치대";
            box.EnableTag("IS_OWNED_TAG");
            return box;
        }

        // ---- 방 ----
        private static GameItem Rack(int room) => HiddenStore.GetOrCreate(Key(room), () => ShopItems.Spawn(ShopItems.FreezerRack));

        private static GameInventory RackGrid(GameItem rack) => rack?.contentWindow?.childElement as GameInventory;

        internal static int Count(int room)
        {
            GameInventory g = RackGrid(HiddenStore.Find(Key(room)));
            if (g == null) return 0;
            int n = 0;
            foreach (GameItem i in g.childItems) if (i.identifier == ShopItems.AquaFreezer) n++;
            return n;
        }

        // 개조 직후/방에 들어올 때: 거치대 + 냉동고 1개 (없으면)
        internal static void Ensure(int room)
        {
            GameItem rack = Rack(room);
            if (rack != null && Count(room) == 0) AddOne(rack);
        }

        private static bool AddOne(GameItem rack)
        {
            GameInventory g = RackGrid(rack);
            GameItem f = ShopItems.Spawn(ShopItems.AquaFreezer);
            if (g == null || f == null) return false;
            f.EnableTag("IS_OWNED_TAG");
            moving = true;
            try { g.TryAcceptAll(f); }
            finally { moving = false; }
            if (!g.childItems.Contains(f)) { Debug.LogWarning("[ShopExpansion] freezer did not fit on the rack"); f.Destroy(); return false; }
            return true;
        }

        // 다음 냉동고 사기 (돈 확인 → 차감 → 추가)
        internal static bool TryBuy(int room)
        {
            int n = Count(room);
            if (n >= Max) { Rooms.Notify("냉동고는 " + Max + "개까지 놓을 수 있다."); return false; }
            int cost = CostOf(n);
            int cash = PlayerStore.Instance != null ? PlayerStore.Instance.playerCash : 0;
            if (cash < cost) { Rooms.Notify("비용이 부족하다. (필요 " + cost.ToString("N0") + "₵, 소지 " + cash.ToString("N0") + "₵)"); return false; }
            GameItem rack = Rack(room);
            if (rack == null || !AddOne(rack)) return false;
            if (cost > 0) ModHelper.AddCredit(-cost);
            Rooms.Notify("냉동고를 추가했다. (" + (n + 1) + "/" + Max + ", -" + cost.ToString("N0") + "₵)");
            return true;
        }

        private static PixelWindow shown;

        internal static void Show(int room)
        {
            HideAll();
            Ensure(room);
            PixelWindow w = Rack(room)?.contentWindow;
            if (w == null) return;
            try
            {
                w.Show(new Vector2(Screen.width * RackLeft / 1920f, Screen.height - Screen.height * RackTop / 1080f));
                shown = w;
            }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] freezer rack show: " + ex.Message); }
        }

        internal static void HideAll()
        {
            try { if (shown != null && shown.IsVisible()) shown.Hide(); } catch { }
            shown = null;
            // 열려 있던 냉동고 창도 닫는다
            foreach (string k in new[] { Key(1), Key(2), Key(3) })
            {
                GameInventory g = RackGrid(HiddenStore.Find(k));
                if (g == null) continue;
                foreach (GameItem f in g.childItems)
                    try { if (f.contentWindow != null && f.contentWindow.IsVisible()) f.contentWindow.Hide(); } catch { }
            }
        }

        internal static void Delete(int room)
        {
            HideAll();
            HiddenStore.Delete(Key(room));
        }

        // 다음 냉동고가 놓일 자리 (화면 픽셀, 왼쪽 아래 원점). 다 찼거나 거치대가 안 보이면 null
        internal static Rect? NextSlotScreenRect(int room)
        {
            if (shown == null || !shown.IsVisible()) return null;
            int n = Count(room);
            if (n >= Max) return null;
            Vector2Int s = FreezerShape();
            float sf = 1f, cell = 16f;
            try { sf = RenderHandler.current.windowUICanvas.scaleFactor; } catch { }
            try { cell = UISettings.current.gridSize; } catch { }
            float c = cell * sf;
            // 거치대 격자 왼쪽 위 = 창 내용 영역 왼쪽 위 (투명 창은 여백이 거의 없다)
            Vector3[] corners = new Vector3[4];
            RectTransform inner = (shown.childElement as GameInventory) is PixelElement pe && pe.handler != null ? pe.handler.rectTransform : shown.rectTransform;
            inner.GetWorldCorners(corners); // 0 왼쪽 아래, 1 왼쪽 위
            Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
            float w = s.x * c, h = s.y * c;
            return new Rect(topLeft.x, topLeft.y - (n + 1) * h, w, h);
        }
    }
}
