using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShopExpansion
{
    // 가게 화면 오른쪽 벽 방 버튼 (세로) [가게] [방 1 · 온실] [방 2 · 빈 방] ...  (증축한 방이 있을 때, 가게 안에서만)
    // 방 안: 위쪽 가운데 방 이름 + [용도 변경]. 용도를 안 정한 방에 들어가면 용도 고르기 창이 바로 뜬다.
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
        private GameObject tankSpot, aquaTable;

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
                tankSpot = Hotspot(coverRt, "TankSpot", 435f, 370f, 1027f, 443f, () => AquaTank.Toggle(Rooms.Current));
                // 양식장 작업대: 수족관 전용 급이기 / 도축기 (메인 인벤 왼쪽 바닥)
                aquaTable = new GameObject("AquaTable", typeof(RectTransform));
                aquaTable.transform.SetParent(coverRt, false);
                Stretch((RectTransform)aquaTable.transform);
                FittingButton((RectTransform)aquaTable.transform, "급이기", "Items/items_husbandry", "animal_feeder", 150f, 60f, () => AquaTank.ToggleFitting(Rooms.Current, true));
                FittingButton((RectTransform)aquaTable.transform, "도축기", "Items/items_tool2", "gibber", 330f, 60f, () => AquaTank.ToggleFitting(Rooms.Current, false));
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
            aquaTable.SetActive(cp != null && cp.Key == "aquaculture");
            cover.SetActive(true);
            KeepBehindWindows();
        }
        private GameObject tabRow, roomHeader, picker;
        private TextMeshProUGUI headerText, pickerTitle;
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
            MakeButton(roomHeader.transform, "용도 변경", 960f - 60f, 1080f - 40f - 44f - 8f - 32f, 120f, 32f, 15f, () => OpenPicker(Rooms.Current));
            roomHeader.SetActive(false);

            BuildPicker();
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
                if (Rooms.Purpose(Rooms.Current) == null && !picker.activeSelf) OpenPicker(Rooms.Current); // 빈 방 → 용도부터
            }
            if (!inRoom && picker.activeSelf) picker.SetActive(false);
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
            Text(rt, "Hint", 20f, -44f, PickW - 40f, 22f, 14f, Dim, TextAlignmentOptions.Top).text = "";
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
            MakeButton(rt, "닫기", PickW / 2f - 60f, h - 46f, 120f, 32f, 15f, () => picker.SetActive(false), anchorTop: true);
            picker.SetActive(false);
        }

        private void OpenPicker(int room)
        {
            if (room <= 0) return;
            pickerRoom = room;
            ScriptFontSwap.Apply(pickerTitle, "방 " + room + " 용도 정하기");
            picker.SetActive(true);
            picker.transform.SetAsLastSibling();
        }

        private void Pick(RoomPurpose p)
        {
            Rooms.SetPurpose(pickerRoom, p);
            picker.SetActive(false);
            Rooms.Notify("방 " + pickerRoom + "을(를) " + p.Name + "(으)로 정했다.");
            try { AudioManager.Instance.Play2("click_vintage"); } catch { }
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
