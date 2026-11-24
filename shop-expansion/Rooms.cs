using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShopExpansion
{
    // 용도 하나: 이름 + 배경 파일 키 (img/room-<Key>.png, 없으면 img/empty-room.png, 그것도 없으면 img/bg_placeholder.png)
    internal class RoomPurpose
    {
        public string Key, Name, Desc;
        public RoomPurpose(string key, string name, string desc) { Key = key; Name = name; Desc = desc; }
    }

    // 증축한 방 (최대 3개). 방 수 = 산 증축 업그레이드 수 (게임 네트워크 업그레이드 저장에 남는다).
    // 방 용도 = 모드 데이터 "room/<n>/purpose" (세이브에 저장).
    // 방으로 가기 = 가게 화면(배경, 기계, 진열대, 카운터, 열린 창)을 숨기고 방 배경을 띄운다. 메인 인벤토리만 그대로.
    internal static class Rooms
    {
        internal const int Max = 3;

        internal static readonly RoomPurpose[] Purposes =
        {
            new RoomPurpose("aquaculture", "양식장", "물고기와 수산물을 기르는 방"),
            new RoomPurpose("breeding", "사육장", "동물을 기르는 방"),
            new RoomPurpose("aging", "숙성고", "술, 치즈, 고기를 숙성시키는 방"),
            new RoomPurpose("greenhouse", "온실", "식물을 기르는 방"),
            new RoomPurpose("workshop", "작업실", "물건을 고치고 만드는 방"),
            new RoomPurpose("storage", "창고", "물건을 쌓아 두는 방"),
        };

        // 지금 있는 곳: 0 = 가게, 1~3 = 방
        internal static int Current { get; private set; }

        internal static bool InRoom => Current > 0;

        // ---- 증축 / 용도 ----
        internal static int Built
        {
            get
            {
                int n = 0;
                for (int i = 1; i <= Max; i++)
                {
                    try { if (ModHelper.IsNetworkUpgradeUnlocked(RoomUpgrades.Id(i))) n = i; } catch { }
                }
                return n;
            }
        }

        internal static RoomPurpose Purpose(int room)
        {
            string key = ModHelper.GetModData(ShopExpansionMod.ModId, "room/" + room + "/purpose", "");
            return Purposes.FirstOrDefault(p => p.Key == key);
        }

        internal static void SetPurpose(int room, RoomPurpose p)
        {
            ModHelper.SetModData(ShopExpansionMod.ModId, "room/" + room + "/purpose", p != null ? p.Key : "");
            // 양식장: 대형 수조 + 수족관 전용 급이기/도축기 미리 배치
            if (p != null && p.Key == "aquaculture")
            {
                try { AquaTank.Prepare(room); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] prepare tank: " + ex.Message); }
            }
            if (Current == room) ShowBackground(room);
        }

        // 방 개조(용도 정하기/바꾸기) 비용. 하이퍼테크 보관함(약 15,000₵)보다 비싸게 (사용자 요청, 0.6.0)
        internal const int PurposeCost = 20000;

        // 돈을 내고 용도를 정한다. 성공하면 true
        internal static bool TryBuyPurpose(int room, RoomPurpose p)
        {
            if (p == null) return false;
            RoomPurpose cur = Purpose(room);
            // 한 번 개조한 방은 용도를 바꿀 수 없다. 폐쇄(빈 방) 후 다시 개조 (0.8.0)
            if (cur != null) { Notify("이미 " + cur.Name + "(으)로 개조된 방이다. 바꾸려면 먼저 폐쇄하자."); return false; }
            int cash = PlayerStore.Instance != null ? PlayerStore.Instance.playerCash : 0;
            if (cash < PurposeCost)
            {
                Notify("개조 비용이 부족하다. (필요 " + PurposeCost.ToString("N0") + "₵, 소지 " + cash.ToString("N0") + "₵)");
                return false;
            }
            ModHelper.AddCredit(-PurposeCost);
            SetPurpose(room, p);
            return true;
        }

        // 방 폐쇄: 그 용도의 붙박이 기계와 안의 모든 아이템을 없애고 빈 방으로
        internal static void CloseRoom(int room)
        {
            RoomPurpose cur = Purpose(room);
            if (cur == null) return;
            try
            {
                if (cur.Key == "aquaculture") AquaTank.Dismantle(room);
                RoomTable.Delete(room); // 테이블 위 물건도 같이 사라진다
                Freezers.Delete(room);  // 냉동고와 안의 물건도
            }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] close room " + room + ": " + ex); }
            SetPurpose(room, null);
            SetCustomName(room, null); // 폐쇄하면 붙인 이름도 지운다 (다시 개조하면 새 용도 이름)
            Notify("방 " + room + "(" + cur.Name + ")을(를) 폐쇄했다. 빈 방이 되었다.");
            try { AudioManager.Instance.Play2("close_door"); } catch { }
        }

        // 방 이름: 사용자가 붙인 이름 → 용도 이름 → "빈 방"
        internal static string RoomName(int room)
        {
            string custom = CustomName(room);
            if (!string.IsNullOrEmpty(custom)) return custom;
            RoomPurpose p = Purpose(room);
            return p != null ? p.Name : "빈 방";
        }

        // ---- 방 이름 바꾸기 (0.10.8): 세이브의 모드 데이터 room/<n>/name ----
        internal const int NameMaxLength = 12;

        internal static string CustomName(int room)
        {
            try { return (ModHelper.GetModData(ShopExpansionMod.ModId, "room/" + room + "/name", "") ?? "").Trim(); }
            catch { return ""; }
        }

        internal static void SetCustomName(int room, string name)
        {
            name = (name ?? "").Replace("\n", " ").Trim();
            if (name.Length > NameMaxLength) name = name.Substring(0, NameMaxLength);
            if (name.Length == 0) ModHelper.RemoveModData(ShopExpansionMod.ModId, "room/" + room + "/name");
            else ModHelper.SetModData(ShopExpansionMod.ModId, "room/" + room + "/name", name);
        }

        // 게임의 이름 붙이기 창(보관함 탭 이름 바꾸기와 같은 창). 확인 = 그 이름, 초기화 = 용도 이름으로
        internal static void OpenRename(int room, Action changed)
        {
            if (room <= 0) return;
            if (LabelerBlueUIManager.Instance == null) { Notify("이름 바꾸기 창을 열 수 없다."); return; }
            LabelerBlueUIManager.Instance.OpenUI(CustomName(room),
                text => { SetCustomName(room, text); changed?.Invoke(); },
                () => { SetCustomName(room, null); changed?.Invoke(); },
                NameMaxLength);
        }

        // ---- 이동 ----
        internal static bool CanMove(out string why)
        {
            why = null;
            try
            {
                if (NegociationUIManager.Instance != null && NegociationUIManager.Instance.uiObject != null
                    && NegociationUIManager.Instance.uiObject.activeSelf)
                {
                    why = "손님을 응대하는 중에는 자리를 비울 수 없다.";
                    return false;
                }
                MapUIManager map = MapUIManager.Instance;
                if (map != null && (map.isOutside || map.IsInTransit)) { why = "지금은 이동할 수 없다."; return false; }
            }
            catch { }
            return true;
        }

        internal static void GoTo(int room)
        {
            if (room == Current) return;
            if (room < 0 || room > Built) return;
            if (!CanMove(out string why)) { Notify(why); return; }
            try { AudioManager.Instance.Play2("close_door"); } catch { }
            if (room == 0)
            {
                ReturnToShop();
                return;
            }
            if (Current == 0) HideShop();
            AquaTank.CloseAll();
            Current = room;
            ShowBackground(room);
            RoomPanel.Refresh();
        }

        // 가게로 (손님 도착, 하루 끝, 외출 등에서도 부른다. 이미 가게면 아무것도 안 함)
        internal static void ReturnToShop()
        {
            if (Current == 0) return;
            Current = 0;
            AquaTank.CloseAll();
            RoomTable.HideAll();
            Freezers.HideAll();
            RoomPanel.SetCover(null);
            RestoreShop();
            RoomPanel.Refresh();
        }

        // 세이브를 새로 불러오면 숨긴 목록은 옛 장면 것이라 버린다
        internal static void ResetOnLoad()
        {
            Current = 0;
            hiddenObjects.Clear();
            hiddenWindows.Clear();
        }

        // ---- 가게 화면 숨기기 / 되돌리기 ----
        private static readonly List<GameObject> hiddenObjects = new List<GameObject>();
        private static readonly List<PixelWindow> hiddenWindows = new List<PixelWindow>();

        private static void HideShop()
        {
            hiddenObjects.Clear();
            hiddenWindows.Clear();
            EmporiumEntry e = EmporiumEntry.Instance;
            if (e == null) return;
            PixelWindow main = e.invWindow;

            // 떠 있는 창 (상자/기계 등 열어 둔 창) → 닫아 두었다가 돌아오면 다시 연다
            try
            {
                if (WindowsHandler.current != null && WindowsHandler.current.visibleWindows != null)
                {
                    foreach (PixelWindow w in WindowsHandler.current.visibleWindows.ToList())
                    {
                        if (w == null || w == main || !w.canMove || w.handler == null) continue;
                        try { w.Hide(); hiddenWindows.Add(w); } catch { }
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] hide windows: " + ex.Message); }

            // 가게 고정 창 (쓰레기통, 수도, 진열장, 카운터, 뒤 선반 ...) + 가게 배경/기계
            var objs = new List<GameObject>();
            void Add(PixelWindow w)
            {
                try { if (w != null && w != main && w.handler != null) objs.Add(w.rectTransform.gameObject); } catch { }
            }
            Add(e.trashInvWindow);
            Add(e.faucetWindow);
            Add(e.drainInvWindow);
            Add(e.cassettePlayerWindow);
            Add(e.showcaseWindow);
            Add(e.docInvWindow);
            Add(e.frontInvWindow);
            Add(e.backInvWindow);
            Add(e.vendingFountainWindow);
            Add(e.vendingMachineWindow);
            Add(e.hiddenWindow);
            // 손님 자리(가운데 문)에 있는 투명한 창들: 안 숨기면 방 UI 클릭을 가로챈다 (0.2.4)
            Add(e.responseWindow);
            Add(e.responseWindowClosable);
            Add(e.bazarLeftInvWindow);
            Add(e.hirelingWindow);
            objs.Add(e.storeInteractables);
            objs.Add(e.storePhysical);
            // 벽 달력 글씨, 소지금, 손님 수 (게임이 외출할 때 숨기는 것과 같음)
            try
            {
                StoreUIManager ui = StoreUIManager.Instance;
                if (ui != null)
                {
                    if (ui.storeCalendar != null && ui.storeCalendar.dayTMP != null) objs.Add(ui.storeCalendar.dayTMP.gameObject);
                    if (ui.storeCreditDisplay != null && ui.storeCreditDisplay.creditDisplayText != null) objs.Add(ui.storeCreditDisplay.creditDisplayText.gameObject);
                    objs.Add(ui.clientCounter);
                }
            }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] hide store texts: " + ex.Message); }
            // 가게 셔터 + 셔터 차단막 (덮개보다 위 층에 그려진다. 게임도 외출할 때 차단막 부모를 끈다)
            try
            {
                StoreShutterButton sh = StoreShutterButton.Instance;
                if (sh != null)
                {
                    if (sh.shutterGameObject != null) objs.Add(sh.shutterGameObject);
                    if (sh.blockerGameObject != null && sh.blockerGameObject.transform.parent != null) objs.Add(sh.blockerGameObject.transform.parent.gameObject);
                }
            }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] hide shutter: " + ex.Message); }
            // 아이템 창 캔버스에서 창 루트보다 뒤(아래 층)에 있는 것 전부 (게임 배경 UI, 다른 모드 버튼 — 예: 지도 확장 분점 버튼).
            // 덮개로 가려지지만, 분점 버튼처럼 매 프레임 창 루트 바로 앞으로 오는 것이 덮개 자리를 다투므로 아예 끈다.
            try
            {
                GameObject wr = RenderHandler.current != null ? RenderHandler.current.windowRootObject : null;
                if (wr != null && wr.transform.parent != null)
                {
                    foreach (Transform t in wr.transform.parent)
                    {
                        if (t == wr.transform) break;
                        if (t.name.StartsWith("VSE_")) continue;
                        objs.Add(t.gameObject);
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] hide back layer: " + ex.Message); }
            foreach (GameObject go in objs)
            {
                if (go == null || !go.activeSelf) continue;
                go.SetActive(false);
                hiddenObjects.Add(go);
            }
        }

        private static void RestoreShop()
        {
            foreach (GameObject go in hiddenObjects)
            {
                try { if (go != null) go.SetActive(true); } catch { }
            }
            hiddenObjects.Clear();
            foreach (PixelWindow w in hiddenWindows)
            {
                try { if (w != null && w.handler != null && !w.IsVisible()) w.Show(); } catch { }
            }
            hiddenWindows.Clear();
        }

        // ---- 방 배경: 화면 전체를 덮는 UI 그림 (RoomPanel 이 메인 인벤 창 루트 바로 뒤에 깐다) ----
        // → 그보다 아래 층의 UI(게임, 다른 모드 전부)가 가려진다. 위 층(메뉴, 알림, 툴팁)은 그대로.
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<Texture2D, Sprite> sprites = new Dictionary<Texture2D, Sprite>();
        private static bool canvasesLogged;

        private static void ShowBackground(int room)
        {
            RoomPurpose p = Purpose(room);
            Texture2D tex = LoadTexture(p != null ? "img/room-" + p.Key + ".png" : null);
            Sprite sp = null;
            if (tex != null && !sprites.TryGetValue(tex, out sp))
            {
                sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sprites[tex] = sp;
            }
            RoomPanel.SetCover(sp);
            // 급이기/도축기는 0.10.0 부터 배경 위 그림(RoomPanel, MachineSpot)으로만 보이고 기계는 수조 칸에 그대로 둔다.
            // 예전(작업대 창) 방식으로 꺼내 둔 게 있으면 수조로 되돌린다
            try { AquaTank.StowBench(); }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] bench: " + ex.Message); }
            // 내 테이블 (가게 카운터처럼 물건 올려 두는 곳)
            try
            {
                if (RoomTable.UsedBy(p)) RoomTable.Show(room);
                else RoomTable.HideAll();
                if (Freezers.UsedBy(p)) Freezers.Show(room); // 오른쪽 작업면: 냉동고 거치대
                else Freezers.HideAll();
            }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] table: " + ex.Message); }
            LogCanvasesOnce();
        }

        // 진단: 방 배경보다 위에 그려지는 캔버스를 찾으려고, 처음 방에 들어갈 때 켜진 최상위 캔버스를 기록한다
        private static void LogCanvasesOnce()
        {
            if (canvasesLogged) return;
            canvasesLogged = true;
            try
            {
                Canvas host = RoomPanel.HostCanvas;
                foreach (Canvas c in UnityEngine.Object.FindObjectsOfType<Canvas>())
                {
                    if (c == null || !c.isRootCanvas || !c.gameObject.activeInHierarchy) continue;
                    var kids = new List<string>();
                    foreach (Transform t in c.transform) if (t.gameObject.activeSelf) kids.Add(t.name);
                    Debug.Log("[ShopExpansion] canvas '" + c.name + "' mode=" + c.renderMode + " layer=" + c.sortingLayerName + " order=" + c.sortingOrder
                              + (c == host ? " (room cover here)" : "") + " active children: " + string.Join(", ", kids.Take(25)));
                }
            }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] canvas log: " + ex.Message); }
        }

        private static Texture2D LoadTexture(string path)
        {
            // 용도 배경 → 빈 방 그림(img/empty-room.png) → 임시 배경
            var tries = new List<string>();
            if (!string.IsNullOrEmpty(path)) tries.Add(path);
            tries.Add("img/empty-room.png");
            tries.Add("img/bg_placeholder.png");
            foreach (string f in tries)
            {
                if (textures.TryGetValue(f, out Texture2D cached)) return cached;
                try
                {
                    if (!ModHelper.ModFileExists(ShopExpansionMod.ModId, f)) continue;
                    byte[] bytes = ModHelper.ReadModFileBytes(ShopExpansionMod.ModId, f);
                    if (bytes == null || bytes.Length == 0) continue;
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(bytes)) continue;
                    tex.filterMode = FilterMode.Point;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    textures[f] = tex;
                    return tex;
                }
                catch (Exception ex) { Debug.LogWarning("[ShopExpansion] background " + f + ": " + ex.Message); }
            }
            return null;
        }

        internal static void Notify(string text)
        {
            try { StoreUIManager.Instance.Notify(text); } catch { }
        }
    }

    // 손님이 오면(거래창 열림) 가게로 돌아간다
    [HarmonyLib.HarmonyPatch(typeof(NegociationUIManager), nameof(NegociationUIManager.OpenUI))]
    internal static class NegotiationReturnPatch
    {
        private static void Prefix()
        {
            try { Rooms.ReturnToShop(); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] return on customer: " + ex.Message); }
        }
    }

    // 하루 정산 화면이 뜨면 가게로
    [HarmonyLib.HarmonyPatch(typeof(EndOfDayUIManager), nameof(EndOfDayUIManager.OpenUI))]
    internal static class EndOfDayReturnPatch
    {
        private static void Prefix()
        {
            try { Rooms.ReturnToShop(); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] return on end of day: " + ex.Message); }
        }
    }
}
