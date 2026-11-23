using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShopExpansion
{
    // 방 앞쪽 "내 테이블": 가게 카운터처럼 물건을 올려 두고 작업하는 투명 격자 (예: 철갑상어를 올려 알 꺼내기 수술).
    //  - 정식 아이템 vse_room_table(18x4 격자 창을 가진 보관함)을 숨김 인벤(이름표 room<n>/table)에 두고, 방에 있는 동안 그 창을 고정 위치에 띄운다.
    //  - 올려 둔 물건은 숨김 인벤 저장으로 세이브에 남고, 밤 처리도 가게 물건처럼 돈다.
    //  - 방을 나가면 창만 숨긴다. 방을 폐쇄하면 테이블과 위의 물건도 지운다.
    //  - 지금은 양식장에만.
    internal static class RoomTable
    {
        internal const int W = 18, H = 4;                 // 가게 앞 카운터와 같은 크기
        private const float TopFrom1080 = 560f;           // 화면 위에서 테이블 윗변까지 (1080 기준). 메인 인벤 바로 위
        private static string Key(int room) => "room" + room + "/table";

        internal static bool UsedBy(RoomPurpose p) => p != null && p.Key == "aquaculture";

        private static GameItem Ensure(int room) =>
            HiddenStore.GetOrCreate(Key(room), () => ShopItems.Spawn(ShopItems.RoomTable));

        internal static void Show(int room)
        {
            HideAll();
            GameItem t = Ensure(room);
            PixelWindow w = t?.contentWindow;
            if (w == null) return;
            try
            {
                // 가운데 정렬: 격자 폭(칸 x 게임 칸 크기 x 캔버스 배율)
                float sf = 1f, cell = 16f;
                try { sf = RenderHandler.current.windowUICanvas.scaleFactor; } catch { }
                try { cell = UISettings.current.gridSize; } catch { }
                float widthPx = W * cell * sf;
                float x = Screen.width / 2f - widthPx / 2f;
                float y = Screen.height - Screen.height * TopFrom1080 / 1080f;
                w.Show(new Vector2(x, y));
                shown = w;
            }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] table show: " + ex.Message); }
        }

        private static PixelWindow shown;

        internal static void HideAll()
        {
            try { if (shown != null && shown.IsVisible()) shown.Hide(); } catch { }
            shown = null;
        }

        // 방 폐쇄: 테이블과 위의 물건 삭제
        internal static void Delete(int room)
        {
            HideAll();
            HiddenStore.Delete(Key(room));
        }

        // vse_room_table 공장: 손님 카운터처럼 투명 창 + 흐린 격자, 내 물건만
        internal static GameItem Create()
        {
            var win = new PixelWindow();
            win.SetTransparency(0.99f);
            var grid = new GameGridInventory(W, H);
            grid.SetTransparency(0.85f);
            win.Attach(grid);
            win.Validate();
            GameItem box = ItemDirectory.CreateEmptyItem().SetContentWindow(win).SetSpriteAndShape("Items/items_container", "evidence_box");
            box.name = "작업 테이블";
            box.EnableTag("IS_OWNED_TAG"); // 내 물건을 올릴 수 있게
            return box;
        }
    }
}
