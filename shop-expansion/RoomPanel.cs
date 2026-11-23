using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShopExpansion
{
    // 가게 화면 오른쪽 벽 방 버튼 (세로) [가게] [방 1 · 온실] [방 2 · 빈 방] ...  (증축한 방이 있을 때, 가게 안에서만)
    // 방 안: 위쪽 가운데 방 이름 + [개조](빈 방) / [폐쇄](개조된 방). 빈 방에 처음 들어가면 용도 고르기 창이 뜬다.
    // 개조된 방은 용도를 바꿀 수 없고, 폐쇄(모든 아이템 삭제, 확인 창) 후 다시 개조한다.
    // 게임 아이템 창 캔버스의 창 루트 바로 앞에 둔다 (지도 확장 분점 버튼과 같은 층: 가게 물건/창 뒤, 배경 위).
    internal class RoomPanel : MonoBehaviour
    {
        // 방 버튼: 오른쪽 벽(포스터 오른쪽 빈 곳)에 위에서 아래로 세로 줄. 1920x1080 기준, TabTop = 화면 위에서 첫 버튼 윗변까지
        private const float TabW = 170f, TabH = 36f, TabGap = 6f, TabRight = 16f, TabTop = 250f;
        private static readonly Color Edge = new Color32(0x8b, 0xc4, 0xbe, 0xff);
        private static readonly Color BtnBg = new Color32(0x30, 0x2a, 0x3c, 0xff);
        private static readonly Color BtnOn = new Color32(0x3f, 0x6f, 0x6a, 0xff);
        private static readonly Color PanelBg = new Color32(0x24, 0x1f, 0x2e, 0xf2);
        private static readonly Color Title = new Color32(0x9a, 0xd6, 0xcf, 0xff);
        private static readonly Color Body = new Color32(0xe2, 0xe2, 0xe6, 0xff);
        private static readonly Color Dim = new Color32(0x9a, 0x9a, 0xa8, 0xff);

        private static RoomPanel instance;
        private Canvas hostCanvas;
        internal static Canvas HostCanvas => instance != null ? instance.hostCanvas : null;

        // 방 배경 덮개: 창 루트 바로 앞(메인 인벤 뒤), 화면 전체. 그림이 없으면 어두운 단색.
        private GameObject cover;
        private RectTransform coverRt, coverImgRt;
        private Image coverImg;
        // 방 배경 위 누를 수 있는 자리 (덮개 안 = 메인 인벤/기계 창보다 뒤라 창을 가리지 않는다)
        private GameObject tankSpot, aquaTable, fishLayer;

        // 화면 좌표가 지금 보이는 수조 자리 안인가 (TankDrop: 끌어다 놓기)
        internal static bool IsOverTankSpot(Vector2 screen)
        {
            GameObject s = instance != null ? instance.tankSpot : null;
            if (s == null || !s.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)s.transform, screen, null);
        }

        internal static void SetCover(Sprite sp)
        {
            if (instance != null) instance.SetCoverNow(sp);
        }

        private void SetCoverNow(Sprite sp)
        {
            if (!Rooms.InRoom)
            {
                if (cover != null) cover.SetActive(false);
                return;
            }
            if (cover == null)
            {
                cover = new GameObject("VSE_RoomCover", typeof(RectTransform));
                cover.transform.SetParent(transform.parent, false);
                coverRt = (RectTransform)cover.transform;
                coverRt.anchorMin = coverRt.anchorMax = coverRt.pivot = Vector2.zero;
                coverRt.anchoredPosition = Vector2.zero;
                coverRt.sizeDelta = new Vector2(1920f, 1080f);
                var back = cover.AddComponent<Image>();
                back.color = new Color32(0x1c, 0x1a, 0x26, 0xff);
                back.raycastTarget = true; // 아래 층 클릭 막기
                var ig = new GameObject("Image", typeof(RectTransform));
                ig.transform.SetParent(coverRt, false);
                coverImgRt = (RectTransform)ig.transform;
                coverImgRt.anchorMin = coverImgRt.anchorMax = coverImgRt.pivot = new Vector2(0.5f, 0.5f);
                coverImgRt.anchoredPosition = Vector2.zero;
                coverImg = ig.AddComponent<Image>();
                coverImg.raycastTarget = false;
                // 양식장 배경의 큰 유리 수조 자리 (1920x1080 기준, 아래 왼쪽 원점)
                // 수조 안에서 헤엄치는 물고기 (수조 자리보다 먼저 = 아래에 그린다)
                fishLayer = new GameObject("FishSwim", typeof(RectTransform));
                fishLayer.transform.SetParent(coverRt, false);
                var flr = (RectTransform)fishLayer.transform;
                flr.anchorMin = flr.anchorMax = flr.pivot = Vector2.zero;
                flr.anchoredPosition = Vector2.zero;
                flr.sizeDelta = new Vector2(1920f, 1080f);
                fishLayer.AddComponent<FishSwim>();
                tankSpot = Hotspot(coverRt, "TankSpot", 435f, 370f, 1027f, 443f, () => AquaTank.Toggle(Rooms.Current));
                tankSpot.AddComponent<TankHover>(); // 마우스를 올리면 대형 수조 툴팁 (TankTooltip.cs)
                if (DebugTankBorder) tankBorder = DebugBorder((RectTransform)tankSpot.transform, BorderIdle, 3f);
                // 양식장 작업대: 수족관 전용 급이기 / 도축기 (메인 인벤 왼쪽 바닥)
                aquaTable = new GameObject("AquaTable", typeof(RectTransform));
                aquaTable.transform.SetParent(coverRt, false);
                Stretch((RectTransform)aquaTable.transform);
                // 급이기/도축기 그림 버튼은 0.5.0 부터 작업대 창(진짜 게임 아이템, AquaTank.ShowBench)으로 바뀜
            }
            coverImg.sprite = sp;
            coverImg.enabled = sp != null;
            if (sp != null)
            {
                // 화면을 꽉 채우고(cover) 넘치는 쪽은 잘린다
                float a = sp.rect.width / sp.rect.height;
                coverImgRt.sizeDelta = a > 1920f / 1080f ? new Vector2(1080f * a, 1080f) : new Vector2(1920f, 1920f / a);
            }
            cover.transform.localScale = transform.localScale;
            RoomPurpose cp = Rooms.Purpose(Rooms.Current);
            tankSpot.SetActive(cp != null && cp.Key == "aquaculture");
            fishLayer.SetActive(cp != null && cp.Key == "aquaculture");
            aquaTable.SetActive(cp != null && cp.Key == "aquaculture");
            cover.SetActive(true);
            KeepBehindWindows();
        }
        private GameObject tabRow, roomHeader, picker;
        private TextMeshProUGUI headerText, pickerTitle, pickerHint;
        private RoomPurpose pendingPurpose;
        private readonly List<(int room, GameObject go, Image bg, TextMeshProUGUI label)> tabs = new List<(int, GameObject, Image, TextMeshProUGUI)>();
        private int pickerRoom;
        private int shownBuilt = -1;

        internal static void Ensure()
        {
            if (instance != null) return;
            Transform host = null;
            int siblingIndex = 0;
            try
            {
                GameObject wr = RenderHandler.current != null ? RenderHandler.current.windowRootObject : null;
                if (wr != null && wr.transform.parent != null)
                {
                    host = wr.transform.parent;
                    siblingIndex = wr.transform.GetSiblingIndex();
                }
            }
            catch { host = null; }
            if (host == null)
            {
                Debug.LogWarning("[ShopExpansion] no window canvas for the room buttons");
                return;
            }
            var go = new GameObject("VSE_RoomPanel", typeof(RectTransform));
            go.transform.SetParent(host, false);
            go.transform.SetSiblingIndex(siblingIndex);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1920f, 1080f);
            instance = go.AddComponent<RoomPanel>();
            instance.hostCanvas = host.GetComponentInParent<Canvas>()?.rootCanvas;
            instance.Build();
            instance.FitScale();
        }

        internal static void Remove()
        {
            if (instance != null && instance.cover != null) Destroy(instance.cover);
            if (instance != null) Destroy(instance.gameObject);
            instance = null;
        }

        internal static void Refresh()
        {
            if (instance != null) instance.RefreshNow();
        }

        private void Build()
        {
            tabRow = new GameObject("Tabs", typeof(RectTransform));
            tabRow.transform.SetParent(transform, false);
            Stretch((RectTransform)tabRow.transform);

            // 방 안 머리말: 방 이름 + [용도 변경]
            roomHeader = new GameObject("Header", typeof(RectTransform));
            roomHeader.transform.SetParent(transform, false);
            Stretch((RectTransform)roomHeader.transform);
            headerText = Text((RectTransform)roomHeader.transform, "Name", 960f - 300f, 1080f - 40f, 600f, 44f, 30f, Title, TextAlignmentOptions.Center);
            RectTransform hb = MakeButton(roomHeader.transform, "개조", 960f - 60f, 1080f - 40f - 44f - 8f - 32f, 120f, 32f, 15f, HeaderAction);
            headerButtonLabel = hb.Find("Label").GetComponent<TextMeshProUGUI>();
            roomHeader.SetActive(false);

            BuildPicker();
            BuildCloseConfirm();
            BuildFreezerGhost();
            RefreshNow();
        }

        private void BuildTabs(int built)
        {
            foreach (var t in tabs) if (t.go != null) Destroy(t.go);
            tabs.Clear();
            shownBuilt = built;
            for (int i = 0; i <= built; i++)
            {
                int room = i;
                RectTransform b = MakeButton(tabRow.transform, "", 1920f - TabRight - TabW, 1080f - TabTop - TabH - i * (TabH + TabGap), TabW, TabH, 15f, () => Rooms.GoTo(room));
                tabs.Add((room, b.gameObject, b.Find("Bg").GetComponent<Image>(), b.Find("Label").GetComponent<TextMeshProUGUI>()));
            }
        }

        private void RefreshNow()
        {
            int built = Rooms.Built;
            if (built != shownBuilt) BuildTabs(built);
            foreach (var t in tabs)
            {
                string label = t.room == 0 ? "가게" : "방 " + t.room + " · " + Rooms.RoomName(t.room);
                if (t.label.text != label) ScriptFontSwap.Apply(t.label, label);
                t.bg.color = t.room == Rooms.Current ? BtnOn : BtnBg;
            }
            bool inRoom = Rooms.InRoom;
            if (roomHeader.activeSelf != inRoom) roomHeader.SetActive(inRoom);
            if (inRoom)
            {
                string name = "방 " + Rooms.Current + " · " + Rooms.RoomName(Rooms.Current);
                if (headerText.text != name) ScriptFontSwap.Apply(headerText, name);
                bool empty = Rooms.Purpose(Rooms.Current) == null;
                string hl = empty ? "개조" : "폐쇄";
                if (headerButtonLabel != null && headerButtonLabel.text != hl) ScriptFontSwap.Apply(headerButtonLabel, hl);
                // 빈 방에 들어오면 한 번만 용도 고르기 (닫으면 다시 안 뜬다, 다른 방에 갔다 오면 다시)
                if (empty && autoPickedRoom != Rooms.Current && !picker.activeSelf) { autoPickedRoom = Rooms.Current; OpenPicker(Rooms.Current); }
                if (!empty && picker.activeSelf) picker.SetActive(false);
            }
            if (!inRoom && picker.activeSelf) picker.SetActive(false);
            if (!inRoom) autoPickedRoom = -1;
            if (!inRoom && closeConfirm.activeSelf) closeConfirm.SetActive(false);
        }

        // ---- 용도 고르기 창 ----
        private const float PickW = 560f, PickBtnW = 255f, PickBtnH = 64f;

        private void BuildPicker()
        {
            picker = new GameObject("Picker", typeof(RectTransform));
            picker.transform.SetParent(transform, false);
            var rt = (RectTransform)picker.transform;
            int rows = (Rooms.Purposes.Length + 1) / 2;
            float h = 70f + rows * (PickBtnH + 10f) + 56f;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(PickW, h);
            Box(rt);
            pickerTitle = Text(rt, "Title", 20f, -16f, PickW - 40f, 34f, 22f, Title, TextAlignmentOptions.Top);
            pickerHint = Text(rt, "Hint", 20f, -44f, PickW - 40f, 22f, 14f, Dim, TextAlignmentOptions.Top);
            for (int i = 0; i < Rooms.Purposes.Length; i++)
            {
                RoomPurpose p = Rooms.Purposes[i];
                float x = 20f + (i % 2) * (PickBtnW + 10f);
                float y = 70f + (i / 2) * (PickBtnH + 10f);
                RectTransform b = MakeButton(rt, "", x, y, PickBtnW, PickBtnH, 18f, () => Pick(p), anchorTop: true);
                var label = b.Find("Label").GetComponent<TextMeshProUGUI>();
                label.richText = true;
                label.enableWordWrapping = true;
                ScriptFontSwap.Apply(label, p.Name + "\n<size=13><color=#9a9aa8>" + p.Desc + "</color></size>");
            }
            // 고른 뒤 [개조하기] 로 돈을 낸다 (잘못 눌러 돈이 나가지 않게 두 단계)
            MakeButton(rt, "개조하기", PickW / 2f - 130f, h - 46f, 120f, 32f, 15f, ConfirmPick, anchorTop: true);
            MakeButton(rt, "닫기", PickW / 2f + 10f, h - 46f, 120f, 32f, 15f, () => picker.SetActive(false), anchorTop: true);
            picker.SetActive(false);
        }

        // ---- [개조] / [폐쇄] ----
        private TextMeshProUGUI headerButtonLabel;
        private int autoPickedRoom = -1;
        private GameObject closeConfirm;
        private TextMeshProUGUI closeConfirmBody;
        private int closeRoom;

        private void HeaderAction()
        {
            int room = Rooms.Current;
            if (room <= 0) return;
            if (Rooms.Purpose(room) == null) OpenPicker(room);
            else OpenCloseConfirm(room);
        }

        private const float ConfirmW = 520f, ConfirmH = 210f;

        private void BuildCloseConfirm()
        {
            closeConfirm = new GameObject("CloseConfirm", typeof(RectTransform));
            closeConfirm.transform.SetParent(transform, false);
            var rt = (RectTransform)closeConfirm.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(ConfirmW, ConfirmH);
            Box(rt);
            confirmTitle = Text(rt, "Title", 20f, -16f, ConfirmW - 40f, 34f, 22f, Title, TextAlignmentOptions.Top);
            closeConfirmBody = Text(rt, "Body", 24f, -56f, ConfirmW - 48f, 90f, 16f, Body, TextAlignmentOptions.Top);
            closeConfirmBody.enableWordWrapping = true;
            closeConfirmBody.richText = true;
            RectTransform ok = MakeButton(rt, "확인", ConfirmW / 2f - 130f, ConfirmH - 46f, 120f, 32f, 15f, ConfirmClose, anchorTop: true);
            confirmOkLabel = ok.Find("Label").GetComponent<TextMeshProUGUI>();
            MakeButton(rt, "취소", ConfirmW / 2f + 10f, ConfirmH - 46f, 120f, 32f, 15f, () => closeConfirm.SetActive(false), anchorTop: true);
            closeConfirm.SetActive(false);
        }

        private void OpenCloseConfirm(int room)
        {
            RoomPurpose p = Rooms.Purpose(room);
            if (p == null) return;
            closeRoom = room;
            AskConfirm("방 폐쇄",
                "방 " + room + "(" + p.Name + ")을(를) 폐쇄할까요?\n"
                + "<color=#ff8a6b>" + (p.Key == "aquaculture" ? "수조의 물고기를 포함한 " : "") + "방 안의 모든 아이템이 사라지고 빈 방이 됩니다.</color>\n"
                + "<color=#9a9aa8>폐쇄한 뒤 다시 개조할 수 있습니다 (개조 비용 " + Rooms.PurposeCost.ToString("N0") + "₵).</color>",
                "폐쇄", () =>
                {
                    Rooms.CloseRoom(closeRoom);
                    autoPickedRoom = closeRoom; // 폐쇄 직후 고르기 창이 바로 뜨지 않게
                    RefreshNow();
                });
        }

        // 확인 창 (폐쇄, 냉동고 추가 등 공용)
        private TextMeshProUGUI confirmTitle, confirmOkLabel;
        private Action confirmAction;

        private void AskConfirm(string title, string body, string ok, Action onOk)
        {
            ScriptFontSwap.Apply(confirmTitle, title);
            ScriptFontSwap.Apply(confirmOkLabel, ok);
            ScriptFontSwap.Apply(closeConfirmBody, body);
            confirmAction = onOk;
            if (picker.activeSelf) picker.SetActive(false);
            closeConfirm.SetActive(true);
            closeConfirm.transform.SetAsLastSibling();
            try { AudioManager.Instance.Play2("click_vintage"); } catch { }
        }

        private void ConfirmClose()
        {
            closeConfirm.SetActive(false);
            Action a = confirmAction;
            confirmAction = null;
            try { a?.Invoke(); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] confirm: " + ex.Message); }
        }

        // ---- 냉동고 추가 자리: 다음 냉동고가 놓일 빈칸 (점선 느낌 테두리 + "+ 냉동고 추가 / 가격") ----
        private GameObject freezerGhost;
        private TextMeshProUGUI freezerGhostLabel;

        private void BuildFreezerGhost()
        {
            freezerGhost = new GameObject("FreezerAddSlot", typeof(RectTransform));
            freezerGhost.transform.SetParent(transform, false);
            var rt = (RectTransform)freezerGhost.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            var img = freezerGhost.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.06f);
            var b = freezerGhost.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 3f); // 0.06 → 0.18
            colors.pressedColor = new Color(1f, 1f, 1f, 4f);
            b.colors = colors;
            b.onClick.AddListener(AskAddFreezer);
            DebugBorder(rt, new Color(0.8f, 0.9f, 1f, 0.55f), 2f);
            var lgo = new GameObject("Label", typeof(RectTransform));
            lgo.transform.SetParent(rt, false);
            freezerGhostLabel = lgo.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = FontLoader.Instance != null ? (FontLoader.Instance.notoSans_Normal ?? FontLoader.Instance.notoSans) : null;
            if (font != null) freezerGhostLabel.font = font;
            freezerGhostLabel.fontSize = 13f;
            freezerGhostLabel.color = new Color(1f, 1f, 1f, 0.75f);
            freezerGhostLabel.alignment = TextAlignmentOptions.Center;
            freezerGhostLabel.enableWordWrapping = true;
            freezerGhostLabel.raycastTarget = false;
            Stretch(freezerGhostLabel.rectTransform);
            freezerGhost.SetActive(false);
        }

        private void UpdateFreezerGhost()
        {
            if (freezerGhost == null) return;
            Rect? r = null;
            int room = Rooms.Current;
            if (room > 0 && !closeConfirm.activeSelf && !picker.activeSelf) r = Freezers.NextSlotScreenRect(room);
            if (r == null) { if (freezerGhost.activeSelf) freezerGhost.SetActive(false); return; }
            float k = Screen.height / 1080f; // 화면 픽셀 → 이 패널 단위
            var rt = (RectTransform)freezerGhost.transform;
            rt.anchoredPosition = new Vector2(r.Value.x / k, r.Value.y / k);
            rt.sizeDelta = new Vector2(r.Value.width / k, r.Value.height / k);
            string label = "+ 냉동고 추가\n" + Freezers.CostOf(Freezers.Count(room)).ToString("N0") + "₵";
            if (freezerGhostLabel.text != label) ScriptFontSwap.Apply(freezerGhostLabel, label);
            if (!freezerGhost.activeSelf) freezerGhost.SetActive(true);
        }

        private void AskAddFreezer()
        {
            int room = Rooms.Current;
            if (room <= 0) return;
            int n = Freezers.Count(room);
            if (n >= Freezers.Max) return;
            int cost = Freezers.CostOf(n);
            int cash = PlayerStore.Instance != null ? PlayerStore.Instance.playerCash : 0;
            if (cash < cost) { Rooms.Notify("비용이 부족하다. (필요 " + cost.ToString("N0") + "₵, 소지 " + cash.ToString("N0") + "₵)"); return; }
            AskConfirm("냉동고 추가",
                "냉동고를 하나 더 놓을까요? (" + (n + 1) + "/" + Freezers.Max + ")\n"
                + "비용 " + cost.ToString("N0") + "₵ (소지 " + cash.ToString("N0") + "₵)\n"
                + "<color=#9a9aa8>생선 부속물만 넣을 수 있는 " + Freezers.InnerW + "x" + Freezers.InnerH + " 보관함입니다.</color>",
                "추가", () => Freezers.TryBuy(room));
        }

        private void OpenPicker(int room)
        {
            if (room <= 0) return;
            pickerRoom = room;
            ScriptFontSwap.Apply(pickerTitle, "방 " + room + " 용도 정하기");
            pendingPurpose = null;
            UpdateHint();
            picker.SetActive(true);
            picker.transform.SetAsLastSibling();
        }

        private void Pick(RoomPurpose p)
        {
            pendingPurpose = p;
            try { AudioManager.Instance.Play2("click_vintage"); } catch { }
            UpdateHint();
        }

        private void UpdateHint()
        {
            int cash = PlayerStore.Instance != null ? PlayerStore.Instance.playerCash : 0;
            string cost = "개조 비용 " + Rooms.PurposeCost.ToString("N0") + "₵ (소지 " + cash.ToString("N0") + "₵)";
            string text = pendingPurpose == null
                ? "용도를 고르고 [개조하기]를 누르세요. " + cost
                : "<color=#ffcf6b>" + pendingPurpose.Name + "</color>(으)로 개조 · " + cost;
            pickerHint.richText = true;
            ScriptFontSwap.Apply(pickerHint, text);
        }

        private void ConfirmPick()
        {
            if (pendingPurpose == null) { Rooms.Notify("먼저 용도를 고르세요."); return; }
            RoomPurpose p = pendingPurpose;
            if (!Rooms.TryBuyPurpose(pickerRoom, p)) { UpdateHint(); return; }
            picker.SetActive(false);
            pendingPurpose = null;
            Rooms.Notify("방 " + pickerRoom + "을(를) " + p.Name + "(으)로 개조했다. (-" + Rooms.PurposeCost.ToString("N0") + "₵)");
            RefreshNow();
        }

        // ---- 표시 조건 ----
        private static bool InStore()
        {
            try
            {
                if (PlayerStore.Instance == null || Rooms.Built <= 0) return false;
                MapUIManager map = MapUIManager.Instance;
                if (map != null && (map.isOutside || map.IsInTransit)) return false;
                return true;
            }
            catch { return false; }
        }

        private float nextRefresh;

        private void Update()
        {
            UpdateTankBorder();
            UpdateFreezerGhost();
            FitScale();
            KeepBehindWindows();
            bool show = InStore();
            if (tabRow.activeSelf != show) tabRow.SetActive(show);
            if (!show)
            {
                if (roomHeader.activeSelf) roomHeader.SetActive(false);
                if (picker.activeSelf) picker.SetActive(false);
                return;
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.5f;
                RefreshNow();
            }
        }

        // 가게: 항상 창 루트 바로 앞(= 가게 물건/창 뒤, 배경 층).
        // 방 안: 창 루트 바로 뒤(= 창들 위). 가게 창은 다 숨겼고, 안 그러면 투명한 게임 창이 방 UI 클릭을 가로챈다.
        private void KeepBehindWindows()
        {
            try
            {
                GameObject wr = RenderHandler.current != null ? RenderHandler.current.windowRootObject : null;
                if (wr == null || wr.transform.parent != transform.parent) return;
                if (cover != null && cover.activeSelf)
                {
                    PlaceBefore(cover.transform, wr.transform); // 덮개: 메인 인벤 바로 뒤
                    if (cover.transform.localScale != transform.localScale) cover.transform.localScale = transform.localScale;
                }
                if (Rooms.InRoom) PlaceAfter(transform, wr.transform);
                else PlaceBefore(transform, wr.transform);
            }
            catch { }
        }

        private static void PlaceBefore(Transform t, Transform wr)
        {
            int wi = wr.GetSiblingIndex(), ti = t.GetSiblingIndex();
            if (ti != wi - 1) t.SetSiblingIndex(ti < wi ? wi - 1 : wi);
        }

        private static void PlaceAfter(Transform t, Transform wr)
        {
            int wi = wr.GetSiblingIndex(), ti = t.GetSiblingIndex();
            if (ti != wi + 1) t.SetSiblingIndex(ti < wi ? wi : wi + 1);
        }

        // 1920x1080 기준 단위 → 이 캔버스 단위
        private void FitScale()
        {
            if (hostCanvas == null) return;
            float sf = hostCanvas.scaleFactor > 0f ? hostCanvas.scaleFactor : 1f;
            float k = (Screen.height / 1080f) / sf;
            if (Mathf.Abs(transform.localScale.x - k) > 0.0001f) transform.localScale = new Vector3(k, k, 1f);
        }

        // 작업대 위 기계: 게임 아이템 그림 + 아래 이름. (x, y) = 왼쪽 아래, 1920x1080 기준
        private static void FittingButton(RectTransform parent, string label, string atlas, string sprite, float x, float y, Action click)
        {
            const float W = 150f, H = 150f;
            GameObject go = Hotspot(parent, "Fitting_" + label, x, y, W, H + 26f, click);
            Image bg = go.GetComponent<Image>();
            bg.color = new Color32(0x24, 0x1f, 0x2e, 0xb0);
            var b = go.GetComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.5f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            b.colors = colors;
            var ig = new GameObject("Icon", typeof(RectTransform));
            ig.transform.SetParent(go.transform, false);
            var irt = (RectTransform)ig.transform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = new Vector2(0f, -8f);
            irt.sizeDelta = new Vector2(W - 20f, H - 20f);
            var img = ig.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            try { img.sprite = RenderHandler.LoadFromAtlas(atlas, sprite); } catch { }
            var t = Text((RectTransform)go.transform, "Label", 0f, -(H - 4f), W, 26f, 16f, Body, TextAlignmentOptions.Center);
            ScriptFontSwap.Apply(t, label);
        }

        // 투명한 누름 자리: 마우스를 올리면 살짝 밝아진다
        // 디버그: 수조 누름 자리에 빨간 테두리 (자리 맞추기용, 다 맞추면 false)
        private const bool DebugTankBorder = true;

        private static readonly Color BorderIdle = new Color(1f, 0.15f, 0.15f, 1f);   // 빨강: 평소
        private static readonly Color BorderDrop = new Color(1f, 0.85f, 0.1f, 1f);    // 노랑: 수조에 놓을 수 있는 물건을 끌고 있을 때
        private List<Image> tankBorder;

        // 끌고 있는 아이템을 수조에 놓을 수 있으면(전자 부품 업그레이드, 필터, 물고기 등) 테두리를 노랗게
        private void UpdateTankBorder()
        {
            if (tankBorder == null || tankSpot == null || !tankSpot.activeInHierarchy) return;
            bool can = false;
            try
            {
                ItemMouseDragHandler d = ItemMouseDragHandler.current;
                GameItem item = d != null && d.IsDraggingItem ? d.GetCurrentItem() : null;
                GameItem tank = item != null ? AquaTank.Get(Rooms.Current) : null;
                if (tank != null && item != tank)
                    can = (item.MayTarget(tank) && item.CanTarget(tank))
                          || (GeneralHelper.MayPlayerInsertInto(tank) && tank.MayHaveValidInventorySlot(item));
            }
            catch { can = false; }
            Color c = can ? BorderDrop : BorderIdle;
            foreach (Image i in tankBorder) if (i != null && i.color != c) i.color = c;
        }

        private static List<Image> DebugBorder(RectTransform rt, Color color, float t)
        {
            var list = new List<Image>();
            void Edge(string n, Vector2 aMin, Vector2 aMax, Vector2 size)
            {
                var go = new GameObject("DebugBorder_" + n, typeof(RectTransform));
                go.transform.SetParent(rt, false);
                var e = (RectTransform)go.transform;
                e.anchorMin = aMin;
                e.anchorMax = aMax;
                e.pivot = new Vector2(0.5f, 0.5f);
                e.sizeDelta = size;
                e.anchoredPosition = Vector2.zero;
                var img = go.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
                list.Add(img);
            }
            Edge("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, t));
            Edge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, t));
            Edge("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(t, 0f));
            Edge("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(t, 0f));
            return list;
        }

        private static GameObject Hotspot(RectTransform parent, string name, float x, float y, float w, float h, Action click)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.15f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            b.colors = colors;
            b.onClick.AddListener(() =>
            {
                try { click(); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] hotspot " + name + ": " + ex.Message); }
            });
            return go;
        }

        // ---- 작은 UI 도우미 ----
        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void Box(RectTransform rt)
        {
            var edge = rt.gameObject.AddComponent<Image>();
            edge.color = Edge;
            var bg = new GameObject("Bg", typeof(RectTransform));
            bg.transform.SetParent(rt, false);
            var brt = (RectTransform)bg.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(2f, 2f);
            brt.offsetMax = new Vector2(-2f, -2f);
            var img = bg.AddComponent<Image>();
            img.color = PanelBg;
            img.raycastTarget = false;
        }

        // (x, y) = 왼쪽 위 기준 (부모 왼쪽 위에서 오른쪽/아래로)
        private static TextMeshProUGUI Text(RectTransform parent, string name, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = FontLoader.Instance != null ? (FontLoader.Instance.notoSans_Normal ?? FontLoader.Instance.notoSans) : null;
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            bool fromBottom = parent.anchorMax == Vector2.one && parent.sizeDelta == Vector2.zero; // 화면 전체 층이면 아래 기준 좌표
            if (fromBottom)
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(x, y - h);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(x, y);
            }
            rt.sizeDelta = new Vector2(w, h);
            return t;
        }

        // 기본: (x, y) = 왼쪽 아래 기준. anchorTop: 부모 왼쪽 위에서 (x, 아래로 y)
        private static RectTransform MakeButton(Transform parent, string label, float x, float y, float w, float h, float size, Action click, bool anchorTop = false)
        {
            var go = new GameObject("Btn", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            if (anchorTop)
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(x, -y);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(x, y);
            }
            rt.sizeDelta = new Vector2(w, h);
            var edge = go.AddComponent<Image>();
            edge.color = Edge;
            var inner = new GameObject("Bg", typeof(RectTransform));
            inner.transform.SetParent(rt, false);
            var irt = (RectTransform)inner.transform;
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(2f, 2f);
            irt.offsetMax = new Vector2(-2f, -2f);
            var bg = inner.AddComponent<Image>();
            bg.color = BtnBg;
            bg.raycastTarget = false;

            var lgo = new GameObject("Label", typeof(RectTransform));
            lgo.transform.SetParent(rt, false);
            var t = lgo.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = FontLoader.Instance != null ? (FontLoader.Instance.notoSans_Normal ?? FontLoader.Instance.notoSans) : null;
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = Body;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            Stretch(t.rectTransform);
            ScriptFontSwap.Apply(t, label);

            var b = go.AddComponent<Button>();
            b.targetGraphic = bg;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.5f, 1f);
            b.colors = colors;
            b.onClick.AddListener(() =>
            {
                try { click(); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] room panel: " + ex.Message); }
            });
            return rt;
        }
    }
}
