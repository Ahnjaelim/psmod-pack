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
                tankSpot = Hotspot(coverRt, "TankSpot", 274f, 270f, 1380f, 724f, () => AquaTank.Toggle(Rooms.Current));
                tankSpot.AddComponent<TankHover>(); // 마우스를 올리면 대형 수조 툴팁 (TankTooltip.cs)
                if (DebugTankBorder) tankBorder = DebugBorder((RectTransform)tankSpot.transform, BorderIdle, 3f);
                // 양식장 바닥 위 기계 그림: 급이기 / 도축기 (왼쪽), 냉동고 (오른쪽, 가진 개수만큼 + 추가 자리) (0.10.0)
                // 진짜 기계는 숨김 인벤에 그대로. 누르면 창, 끌어다 놓으면 그 기계에 넣기, 올리면 툴팁 (MachineSpot)
                aquaTable = new GameObject("AquaTable", typeof(RectTransform));
                aquaTable.transform.SetParent(coverRt, false);
                var at = (RectTransform)aquaTable.transform;
                Stretch(at);
                MachineArt(at, "Feeder", "img/fitting-feeder.png", FeederX, ArtY,
                    () => AquaTank.Fitting(Rooms.Current, true), () => AquaTank.ToggleFitting(Rooms.Current, true));
                MachineArt(at, "Butcher", "img/fitting-butcher.png", ButcherX, ArtY,
                    () => AquaTank.Fitting(Rooms.Current, false), () => AquaTank.ToggleFitting(Rooms.Current, false));
                for (int i = 0; i < Freezers.Max; i++)
                {
                    int idx = i;
                    freezerSpots[i] = MachineArt(at, "Freezer" + i, "img/fitting-freezer.png", 0f, ArtY,
                        () => Freezers.At(Rooms.Current, idx), () => Freezers.Toggle(Rooms.Current, idx));
                    ((RectTransform)freezerSpots[i].transform).anchoredPosition = FreezerPos(i);
                    freezerSpots[i].SetActive(false);
                }
                BuildFreezerGhost(at);
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
        // 방 이름 글씨: 게임의 강조(장식) 역할 폰트 Lubrifont (한국어면 한국어 패치가 물마루로 바꿔 줌, 0.10.6).
        // ScriptFontSwap 은 한글이면 기본 한글 폰트로 덮어쓰므로 거치지 않는다. 폰트를 못 구하면 ScriptFontSwap 으로.
        private static void SetEmphasis(TextMeshProUGUI t, string text)
        {
            TMP_FontAsset f = null;
            try
            {
                var locale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
                if (FontLoader.Instance != null && locale != null) f = FontLoader.Instance.GetFontByRole(FontLoader.FontRole.Lubrifont, locale);
            }
            catch { f = null; }
            if (f == null) { ScriptFontSwap.Apply(t, text); return; }
            ScriptFontSwap s = t.GetComponent<ScriptFontSwap>();
            if (s != null) Destroy(s); // 예전에 붙은 게 있으면 폰트를 되돌리지 않게
            t.font = f;
            t.text = text;
        }

        // 액자 안 버튼: 윈도우 98 스타일 (0.10.9). 회색 면 + 2겹 입체 테두리(위·왼쪽 밝게, 아래·오른쪽 어둡게), 검은 글씨.
        // 누르고 있는 동안은 테두리가 뒤집히고 글씨가 1px 내려간다 (Win98Press)
        private const float NameBtnGap = 6f;
        internal static readonly Color W98Face = new Color32(0xc0, 0xc0, 0xc0, 0xff);
        internal static readonly Color W98White = Color.white;
        internal static readonly Color W98Light = new Color32(0xdf, 0xdf, 0xdf, 0xff);
        internal static readonly Color W98Dark = new Color32(0x80, 0x80, 0x80, 0xff);
        internal static readonly Color W98Black = Color.black;

        private static RectTransform FrameButton(RectTransform parent, string label, float x, float y, float w, float h, Action click)
        {
            var go = new GameObject("Btn", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            var hit = go.AddComponent<Image>(); // 누름 판정용 (투명)
            hit.color = new Color(1f, 1f, 1f, 0f);
            // 겹 순서: 바깥 아래·오른쪽 / 바깥 위·왼쪽 / 안쪽 아래·오른쪽 / 안쪽 위·왼쪽 / 면
            Image Layer(string n, float l, float bt, float r, float t, Color c)
            {
                var s = new GameObject(n, typeof(RectTransform));
                s.transform.SetParent(rt, false);
                var srt = (RectTransform)s.transform;
                srt.anchorMin = Vector2.zero;
                srt.anchorMax = Vector2.one;
                srt.offsetMin = new Vector2(l, bt);
                srt.offsetMax = new Vector2(-r, -t);
                var img = s.AddComponent<Image>();
                img.color = c;
                img.raycastTarget = false;
                return img;
            }
            const float px = 1.5f; // 1920 기준 1.5 = 2560 화면에서 2px 정도
            var press = go.AddComponent<Win98Press>();
            press.OuterBR = Layer("OuterBR", 0f, 0f, 0f, 0f, W98Black);
            press.OuterTL = Layer("OuterTL", 0f, px, px, 0f, W98White);
            press.InnerBR = Layer("InnerBR", px, px, px, px, W98Dark);
            press.InnerTL = Layer("InnerTL", px, px * 2f, px * 2f, px, W98Light);
            Layer("Face", px * 2f, px * 2f, px * 2f, px * 2f, W98Face);

            var lgo = new GameObject("Label", typeof(RectTransform));
            lgo.transform.SetParent(rt, false);
            var t = lgo.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = FontLoader.Instance != null ? (FontLoader.Instance.notoSans_Normal ?? FontLoader.Instance.notoSans) : null;
            if (font != null) t.font = font;
            t.fontSize = 13f;
            t.color = W98Black;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            Stretch(t.rectTransform);
            ScriptFontSwap.Apply(t, label);
            press.Label = t.rectTransform;

            var b = go.AddComponent<Button>();
            b.targetGraphic = hit;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() =>
            {
                try { AudioManager.Instance.Play2("click_vintage"); } catch { }
                try { click(); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] room panel: " + ex.Message); }
            });
            return rt;
        }

        // 방 이름 액자: 크기 = 원본 x NameFrameScale, 화면 위에서 NameFrameTop 아래 (1920x1080 기준)
        private const float NameFrameScale = 0.75f, NameFrameTop = -8f, NameFontMax = 28f; // 0.10.7: 20 위로
        private const float NameBtnW = 100f, NameBtnH = 26f; // 액자 안 [개조]/[폐쇄] 버튼
        private const float NameInnerX = 36f / 308f, NameInnerW = 238f / 308f, NameInnerTop = 29f / 171f, NameInnerH = 114f / 171f;
        private static readonly Color NameInk = new Color32(0x2e, 0x34, 0x40, 0xff); // 크림색 칸 위 어두운 글씨

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
            // 방 이름 액자 (img/room-name-frame.png, 사용자 그림 방이름.png, 0.10.5): 이름은 안쪽 크림색 칸에 어두운 글씨로
            var hrt = (RectTransform)roomHeader.transform;
            float nameBottom = 1080f - 40f - 44f; // 액자가 없을 때 (예전 그대로)
            RectTransform hb;
            Sprite frame = LoadArt("img/room-name-frame.png");
            if (frame != null)
            {
                float fw = frame.rect.width * NameFrameScale, fh = frame.rect.height * NameFrameScale;
                float fx = 960f - fw / 2f, fy = 1080f - NameFrameTop - fh; // 왼쪽 아래
                var fgo = new GameObject("NameFrame", typeof(RectTransform));
                fgo.transform.SetParent(hrt, false);
                var frt = (RectTransform)fgo.transform;
                frt.anchorMin = frt.anchorMax = frt.pivot = Vector2.zero;
                frt.anchoredPosition = new Vector2(fx, fy);
                frt.sizeDelta = new Vector2(fw, fh);
                var fimg = fgo.AddComponent<Image>();
                fimg.sprite = frame;
                fimg.raycastTarget = false;
                // 안쪽 칸 = 원본 그림 기준 비율 (308x171 중 x 36~273, y 29~142)
                float ix = fx + fw * NameInnerX, iw = fw * NameInnerW;
                float itop = fy + fh - fh * NameInnerTop, ih = fh * NameInnerH;
                // 안쪽 칸: 위 = 방 이름, 아래 = [개조]/[폐쇄] 버튼 (0.10.7)
                const float pad = 6f;
                float nameH = ih - pad * 2f - NameBtnH - 4f;
                headerText = Text(hrt, "Name", ix + pad, itop - pad, iw - pad * 2f, nameH, NameFontMax, NameInk, TextAlignmentOptions.Center);
                headerText.enableAutoSizing = true;
                headerText.fontSizeMin = 12f;
                headerText.fontSizeMax = NameFontMax;
                // 버튼 두 개 나란히: [이름 변경] [개조/폐쇄] (0.10.8, 액자와 같은 금속 느낌)
                float bw = (iw - pad * 2f - NameBtnGap) / 2f, by = itop - ih + pad;
                FrameButton(hrt, "이름 변경", ix + pad, by, bw, NameBtnH, () => Rooms.OpenRename(Rooms.Current, RefreshNow));
                hb = FrameButton(hrt, "개조", ix + pad + bw + NameBtnGap, by, bw, NameBtnH, HeaderAction);
            }
            else
            {
                headerText = Text(hrt, "Name", 960f - 300f, 1080f - 40f, 600f, 44f, 30f, Title, TextAlignmentOptions.Center);
                hb = MakeButton(roomHeader.transform, "개조", 960f - 60f, nameBottom - 8f - 32f, 120f, 32f, 15f, HeaderAction);
            }
            headerButtonLabel = hb.Find("Label").GetComponent<TextMeshProUGUI>();
            roomHeader.SetActive(false);

            BuildPicker();
            BuildCloseConfirm();
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
                if (headerText.text != name) SetEmphasis(headerText, name);
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

        // ---- 바닥 위 기계 그림 자리 (1920x1080 기준, 아래 왼쪽 원점. 그림 크기 = 원본 x ArtScale) ----
        private const float ArtScale = 0.75f;             // 그림 원본(2560 배경 기준) → 1920 기준
        private const float ArtY = 130f;                  // 그림 아랫변 높이 (창 아래 벽에 붙게, 0.10.2)
        private const float FeederX = 140f, ButcherX = 315f; // 0.10.4: 가운데로 100
        private const float FreezerRight = 1780f, FreezerGap = 10f; // 냉동고: 오른쪽 끝에서 왼쪽으로 한 줄
        private static float freezerW = 150f, freezerH = 171f;
        private readonly GameObject[] freezerSpots = new GameObject[Freezers.Max];

        private static Vector2 FreezerPos(int i) => new Vector2(FreezerRight - (i + 1) * freezerW - i * FreezerGap, ArtY);

        private static readonly Dictionary<string, Sprite> art = new Dictionary<string, Sprite>();

        private static Sprite LoadArt(string file)
        {
            if (art.TryGetValue(file, out Sprite s)) return s;
            s = null;
            try
            {
                if (ModHelper.ModFileExists(ShopExpansionMod.ModId, file))
                {
                    byte[] bytes = ModHelper.ReadModFileBytes(ShopExpansionMod.ModId, file);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (bytes != null && tex.LoadImage(bytes))
                    {
                        tex.filterMode = FilterMode.Point;
                        tex.wrapMode = TextureWrapMode.Clamp;
                        s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
                else Debug.LogWarning("[ShopExpansion] art missing: " + file);
            }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] art " + file + ": " + ex.Message); }
            art[file] = s;
            return s;
        }

        // 기계 그림 버튼: 평소 살짝 어둡게, 마우스를 올리면 원래 밝기. 투명한 곳은 누르지 않는다
        private static GameObject MachineArt(RectTransform parent, string name, string file, float x, float y, Func<GameItem> target, Action click)
        {
            Sprite sp = LoadArt(file);
            float w = sp != null ? sp.rect.width * ArtScale : 150f, h = sp != null ? sp.rect.height * ArtScale : 150f;
            if (file.EndsWith("freezer.png")) { freezerW = w; freezerH = h; }
            GameObject go = Hotspot(parent, name, x, y, w, h, click);
            Image img = go.GetComponent<Image>();
            var b = go.GetComponent<Button>();
            var colors = b.colors;
            if (sp != null)
            {
                img.sprite = sp;
                img.preserveAspect = true;
                img.color = Color.white; // Hotspot 은 투명(알파 0)으로 만든다 → 버튼 밝기는 이 색에 곱해지니 흰색으로 (0.10.1)
                try { img.alphaHitTestMinimumThreshold = 0.1f; } catch { }
                colors.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                colors.selectedColor = colors.normalColor;
                b.colors = colors;
            }
            else
            {
                img.color = new Color32(0x24, 0x1f, 0x2e, 0xb0); // 그림이 없으면 어두운 칸
            }
            go.AddComponent<MachineSpot>().Target = target;
            return go;
        }

        // ---- 냉동고 추가 자리: 다음 냉동고가 놓일 곳에 흐린 냉동고 그림 + "+ 냉동고 추가 / 가격" ----
        private GameObject freezerGhost;
        private TextMeshProUGUI freezerGhostLabel;

        private void BuildFreezerGhost(RectTransform parent)
        {
            freezerGhost = new GameObject("FreezerAddSlot", typeof(RectTransform));
            freezerGhost.transform.SetParent(parent, false);
            var rt = (RectTransform)freezerGhost.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(freezerW, freezerH);
            var img = freezerGhost.AddComponent<Image>();
            Sprite sp = LoadArt("img/fitting-freezer.png");
            var b = freezerGhost.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            if (sp != null)
            {
                img.sprite = sp;
                img.preserveAspect = true;
                img.color = Color.white;
                colors.normalColor = new Color(1f, 1f, 1f, 0.22f);
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.45f);
                colors.pressedColor = new Color(1f, 1f, 1f, 0.6f);
                colors.selectedColor = colors.normalColor;
            }
            else
            {
                img.color = new Color(1f, 1f, 1f, 0.06f);
                colors.highlightedColor = new Color(1f, 1f, 1f, 3f); // 0.06 → 0.18
                colors.pressedColor = new Color(1f, 1f, 1f, 4f);
                DebugBorder(rt, new Color(0.8f, 0.9f, 1f, 0.55f), 2f);
            }
            b.colors = colors;
            b.onClick.AddListener(AskAddFreezer);
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
            int room = Rooms.Current;
            bool on = room > 0 && aquaTable != null && aquaTable.activeInHierarchy;
            int n = on ? Freezers.Count(room) : 0;
            // 가진 냉동고만큼 그림을 켠다
            for (int i = 0; i < freezerSpots.Length; i++)
            {
                bool a = on && i < n;
                if (freezerSpots[i] != null && freezerSpots[i].activeSelf != a) freezerSpots[i].SetActive(a);
            }
            if (!on || n >= Freezers.Max || closeConfirm.activeSelf || picker.activeSelf)
            {
                if (freezerGhost.activeSelf) freezerGhost.SetActive(false);
                return;
            }
            var rt = (RectTransform)freezerGhost.transform;
            Vector2 pos = FreezerPos(n);
            if (rt.anchoredPosition != pos) rt.anchoredPosition = pos;
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
        private const bool DebugTankBorder = false;

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

    // 윈도우 98 버튼 눌림: 누르고 있는 동안 입체 테두리를 뒤집고 글씨를 오른쪽 아래로 1px
    internal class Win98Press : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        internal Image OuterBR, OuterTL, InnerBR, InnerTL;
        internal RectTransform Label;
        private bool down;

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) => Set(true);
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => Set(false);
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => Set(false);
        private void OnDisable() => Set(false);

        private void Set(bool d)
        {
            if (down == d) return;
            down = d;
            if (OuterBR != null) OuterBR.color = d ? RoomPanel.W98White : RoomPanel.W98Black;
            if (OuterTL != null) OuterTL.color = d ? RoomPanel.W98Black : RoomPanel.W98White;
            if (InnerBR != null) InnerBR.color = d ? RoomPanel.W98Light : RoomPanel.W98Dark;
            if (InnerTL != null) InnerTL.color = d ? RoomPanel.W98Dark : RoomPanel.W98Light;
            if (Label != null) Label.anchoredPosition = d ? new Vector2(1f, -1f) : Vector2.zero;
        }
    }
}
