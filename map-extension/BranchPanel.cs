using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MapExtension
{
    // 본점 화면 왼쪽 아래 "분점" 버튼 → 분점 모니터(BranchMonitor). (아래 목록 패널은 예전 방식, 지금은 안 씀)
    // 예전: 분점 목록 (재고 수 / 오늘 매출 / [재고] [로그]) + 선택한 분점의 최근 로그.
    // [재고] 은 게임 인벤토리 창(분점 가상 재고)을 연다. 산 분점이 있을 때, 가게 안(외출/지도 아님)에서만 보인다.
    internal class BranchPanel : MonoBehaviour
    {
        private const float Margin = 20f, BtnW = 110f, BtnH = 36f, PanelW = 620f, RowH = 34f, LogH = 300f;
        private static readonly Color Bg = new Color32(0x24, 0x1f, 0x2e, 0xf2);
        private static readonly Color Edge = new Color32(0x8b, 0xc4, 0xbe, 0xff);
        private static readonly Color Title = new Color32(0x9a, 0xd6, 0xcf, 0xff);
        private static readonly Color Body = new Color32(0xe2, 0xe2, 0xe6, 0xff);
        private static readonly Color Dim = new Color32(0x9a, 0x9a, 0xa8, 0xff);
        private static readonly Color BtnBg = new Color32(0x30, 0x2a, 0x3c, 0xff);

        private static BranchPanel instance;
        private GameObject button, panel;
        private RectTransform panelRt, rowsRt;
        private TextMeshProUGUI logTitle, logBody;
        private BranchSite selected;
        private float nextRefresh;
        private readonly List<(BranchSite site, TextMeshProUGUI info)> infos = new List<(BranchSite, TextMeshProUGUI)>();
        private int shownLogs = -1;
        // [분점] 옆 분점별 [재고] 바로가기 (산 분점만, 왼쪽부터 차례로)
        private const float StockBtnW = 84f, StockBtnH = 87f; // 상자 그림 버튼 (Map/img_box.png, 486x502)
        private static Sprite boxSprite;
        private readonly List<(BranchSite site, GameObject btn)> stockButtons = new List<(BranchSite, GameObject)>();

        // 게임 아이템 창 캔버스의 창 루트 바로 앞에 둔다 → 가게 물건/창보다 뒤(배경 층), 로딩 화면/메뉴가 덮으면 같이 가려진다.
        // 크기는 1920x1080 기준 단위로 만들고, 화면 해상도와 캔버스 배율에 맞춰 localScale 로 맞춘다.
        private Canvas hostCanvas;

        internal static void Ensure()
        {
            if (instance != null) return;
            // 1순위: 게임 아이템 창 캔버스(windowUICanvas)에서 창 루트(windowRootObject) 바로 앞 → 가게 물건/창 뒤, 배경 위.
            // 2순위: 가게 UI 캔버스(달력 모니터 창 부모) 맨 뒤.
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
            if (host == null && AdvCalendarUIManager.Instance != null && AdvCalendarUIManager.Instance.UIPanel != null)
                host = AdvCalendarUIManager.Instance.UIPanel.transform.parent;
            GameObject go;
            if (host != null)
            {
                go = new GameObject("VME_BranchPanel", typeof(RectTransform));
                go.transform.SetParent(host, false);
                go.transform.SetSiblingIndex(siblingIndex);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(1920f, 1080f);
                instance = go.AddComponent<BranchPanel>();
                instance.hostCanvas = host.GetComponentInParent<Canvas>()?.rootCanvas;
            }
            else
            {
                // 예비: 따로 오버레이 캔버스
                go = new GameObject("VME_BranchPanel");
                DontDestroyOnLoad(go);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 390;
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;
                go.AddComponent<GraphicRaycaster>();
                instance = go.AddComponent<BranchPanel>();
            }
            instance.Build();
            instance.FitScale();
        }

        // 게임이 형제 순서를 바꿔도(ESC 메뉴, 창 열고 닫기 등) 항상 창 루트 바로 앞에 있게
        private void KeepBehindWindows()
        {
            try
            {
                GameObject wr = RenderHandler.current != null ? RenderHandler.current.windowRootObject : null;
                if (wr == null || wr.transform.parent != transform.parent) return;
                int wi = wr.transform.GetSiblingIndex();
                int mi = transform.GetSiblingIndex();
                if (mi != wi - 1) transform.SetSiblingIndex(mi < wi ? wi - 1 : wi);
            }
            catch { }
        }

        // 1920x1080 기준 단위 → 이 캔버스 단위
        private void FitScale()
        {
            if (hostCanvas == null) return;
            float sf = hostCanvas.scaleFactor > 0f ? hostCanvas.scaleFactor : 1f;
            float k = (Screen.height / 1080f) / sf;
            if (Mathf.Abs(transform.localScale.x - k) > 0.0001f) transform.localScale = new Vector3(k, k, 1f);
        }

        internal static void Remove()
        {
            if (instance != null) Destroy(instance.gameObject);
            instance = null;
        }

        private void Build()
        {
            button = MakeButton(transform, "분점", Margin, Margin, BtnW, BtnH, 17f, Toggle).gameObject;
            foreach (BranchSite site in Branches.All)
            {
                BranchSite s = site;
                string shortName = s.Name.EndsWith(" 분점") ? s.Name.Substring(0, s.Name.Length - 3) : s.Name;
                GameObject b = BoxButton(transform, shortName, () => OpenStockQuick(s));
                b.SetActive(false);
                stockButtons.Add((s, b));
            }

            panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = Vector2.zero;
            panelRt.anchoredPosition = new Vector2(Margin, Margin + BtnH + 8f);
            Box(panelRt);

            var t = Text(panelRt, "Title", 16f, -10f, PanelW - 60f, 30f, 20f, Title, TextAlignmentOptions.TopLeft);
            ScriptFontSwap.Apply(t, "분점");
            MakeButton(panelRt, "X", 0f, 0f, 28f, 28f, 16f, () => Show(false), topRight: true);

            var rows = new GameObject("Rows", typeof(RectTransform));
            rows.transform.SetParent(panelRt, false);
            rowsRt = (RectTransform)rows.transform;
            rowsRt.anchorMin = rowsRt.anchorMax = rowsRt.pivot = new Vector2(0f, 1f);
            rowsRt.anchoredPosition = new Vector2(16f, -46f);
            rowsRt.sizeDelta = new Vector2(PanelW - 32f, 0f);

            logTitle = Text(panelRt, "LogTitle", 16f, 0f, PanelW - 32f, 26f, 16f, Title, TextAlignmentOptions.TopLeft);
            logBody = Text(panelRt, "LogBody", 16f, 0f, PanelW - 32f, LogH, 14f, Body, TextAlignmentOptions.TopLeft);
            logBody.enableWordWrapping = true;
            logBody.overflowMode = TextOverflowModes.Truncate;
            panel.SetActive(false);
            button.SetActive(false);
        }

        // [분점] 버튼 → 분점 모니터 (게임 달력 모니터 창 복제, BranchMonitor). 예전 오버레이 목록 패널은 안 씀
        private void Toggle() => BranchMonitor.Toggle();

        // 분점 재고 창 바로 열기 (모니터가 열려 있으면 그 위로)
        private static void OpenStockQuick(BranchSite site)
        {
            // 이미 열려 있으면 닫는다 (버튼 다시 누르기 = 토글)
            try
            {
                PixelWindow w = site.StockBox?.contentWindow;
                if (w != null && w.IsVisible())
                {
                    w.Hide();
                    try { AudioManager.Instance.Play2("click_vintage"); } catch { }
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] stock toggle: " + ex.Message);
            }
            Branches.OpenStock(site);
            try
            {
                if (BranchMonitor.IsOpen) BranchMonitor.RaiseAbove(site.StockBox?.contentWindow?.handler?.gameObject);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] stock quick raise: " + ex.Message);
            }
        }

        // 상자 그림 버튼 + 아래쪽에 분점 이름 (그림을 못 읽으면 글자 버튼)
        private GameObject BoxButton(Transform parent, string label, Action click)
        {
            Sprite sp = BoxSprite();
            if (sp == null) return MakeButton(parent, label + " 재고", 0f, Margin, 150f, BtnH, 15f, click).gameObject;
            var go = new GameObject("Box_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(0f, Margin);
            rt.sizeDelta = new Vector2(StockBtnW, StockBtnH);
            var img = go.AddComponent<Image>();
            img.sprite = sp;
            img.preserveAspect = true;
            go.AddComponent<HoverName>().Text = label + " 분점 재고"; // 이름은 마우스 올리면 툴팁으로
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            b.colors = colors;
            b.onClick.AddListener(() =>
            {
                try { click(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] branch panel: " + ex.Message); }
            });
            go.SetActive(false);
            return go;
        }

        private static Sprite BoxSprite()
        {
            if (boxSprite != null) return boxSprite;
            try
            {
                byte[] bytes = ModHelper.ReadModFileBytes(MapExtensionMod.ModId, "Map/img_box.png");
                if (bytes == null || bytes.Length == 0) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes)) return null;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.name = "img_box";
                boxSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] img_box: " + ex.Message);
            }
            return boxSprite;
        }

        private void UpdateStockButtons(bool show)
        {
            float x = Margin + BtnW + 8f;
            foreach (var (site, b) in stockButtons)
            {
                bool on = show && site.Owned;
                if (b.activeSelf != on) b.SetActive(on);
                if (!on) continue;
                var rt = (RectTransform)b.transform;
                if (Mathf.Abs(rt.anchoredPosition.x - x) > 0.01f) rt.anchoredPosition = new Vector2(x, Margin);
                x += StockBtnW + 6f;
            }
        }

        private void Show(bool on)
        {
            try { EmporiumAudioPlayer.Instance.audioButton1.Play(); } catch { }
            panel.SetActive(on);
            if (on) Rebuild();
            else Branches.CloseAllStock();
        }

        // 가게 안에 있고 (외출/지도 아님), 산 분점이 있을 때만
        private static bool InStore()
        {
            try
            {
                if (PlayerStore.Instance == null || !Branches.AnyOwned()) return false;
                MapUIManager map = MapUIManager.Instance;
                if (map != null && (map.isOutside || map.IsInTransit)) return false;
                GameObject root = TransitUI.Root;
                if (root != null && root.activeInHierarchy) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void Update()
        {
            FitScale();
            KeepBehindWindows();
            BranchMonitor.TickOverlay();
            bool show = InStore();
            if (button.activeSelf != show) button.SetActive(show);
            UpdateStockButtons(show);
            if (!show && panel.activeSelf) { panel.SetActive(false); Branches.CloseAllStock(); }
            if (panel.activeSelf && Time.unscaledTime >= nextRefresh) RefreshInfo(); // 재고 수 갱신 (버튼은 그대로)
        }

        private void RefreshInfo()
        {
            nextRefresh = Time.unscaledTime + 0.5f;
            foreach (var (site, info) in infos)
            {
                if (info == null) continue;
                string s = "재고 " + Branches.StockCount(site) + "개 · 오늘 " + site.LastIncome + " ₵";
                if (info.text != s) ScriptFontSwap.Apply(info, s);
            }
            if (selected != null && selected.Logs.Count != shownLogs) Rebuild(); // 새 로그
        }

        private void Rebuild()
        {
            nextRefresh = Time.unscaledTime + 0.5f;
            infos.Clear();
            shownLogs = selected != null ? selected.Logs.Count : -1;
            foreach (Transform c in rowsRt) Destroy(c.gameObject);
            var owned = new List<BranchSite>();
            foreach (BranchSite b in Branches.All) if (b.Owned) owned.Add(b);
            if (selected == null || !selected.Owned) selected = owned.Count > 0 ? owned[0] : null;
            shownLogs = selected != null ? selected.Logs.Count : -1;

            float y = 0f;
            foreach (BranchSite b in owned)
            {
                BranchSite site = b;
                var row = new GameObject("Row_" + b.Key, typeof(RectTransform));
                row.transform.SetParent(rowsRt, false);
                var rrt = (RectTransform)row.transform;
                rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0f, 1f);
                rrt.anchoredPosition = new Vector2(0f, -y);
                rrt.sizeDelta = new Vector2(PanelW - 32f, RowH);
                var name = Text(rrt, "Name", 0f, -4f, 230f, RowH, 16f, b == selected ? Title : Body, TextAlignmentOptions.TopLeft);
                ScriptFontSwap.Apply(name, b.Name);
                var info = Text(rrt, "Info", 230f, -6f, 200f, RowH, 14f, Dim, TextAlignmentOptions.TopLeft);
                ScriptFontSwap.Apply(info, "재고 " + Branches.StockCount(b) + "개 · 오늘 " + b.LastIncome + " ₵");
                infos.Add((b, info));
                MakeButton(rrt, "재고", PanelW - 32f - 150f, 0f, 70f, 28f, 14f, () => Branches.OpenStock(site), anchorTop: true);
                MakeButton(rrt, "로그", PanelW - 32f - 74f, 0f, 70f, 28f, 14f, () => { selected = site; Rebuild(); }, anchorTop: true);
                y += RowH + 4f;
            }

            float logTop = 46f + y + 8f;
            logTitle.rectTransform.anchoredPosition = new Vector2(16f, -logTop);
            logBody.rectTransform.anchoredPosition = new Vector2(16f, -(logTop + 28f));
            ScriptFontSwap.Apply(logTitle, selected != null ? selected.Name + " 최근 기록" : "");
            string body = selected == null ? "" :
                selected.Logs.Count == 0 ? "아직 기록이 없다. 재고를 넣으면 다음 날 아침부터 손님이 온다." : string.Join("\n\n", selected.Logs);
            ScriptFontSwap.Apply(logBody, body);
            panelRt.sizeDelta = new Vector2(PanelW, logTop + 28f + LogH + 12f);
        }

        // ---- 작은 UI 도우미 ----
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
            img.color = Bg;
            img.raycastTarget = false;
        }

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
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return t;
        }

        private static RectTransform MakeButton(Transform parent, string label, float x, float y, float w, float h, float size, Action click,
                                                bool topRight = false, bool anchorTop = false)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            if (topRight)
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one;
                rt.anchoredPosition = new Vector2(-8f, -8f);
            }
            else if (anchorTop)
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
            var t = Text(rt, "Label", 0f, 0f, w, h, size, Body, TextAlignmentOptions.Center);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            ScriptFontSwap.Apply(t, label);
            var b = go.AddComponent<Button>();
            b.targetGraphic = bg;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.5f, 1f);
            b.colors = colors;
            b.onClick.AddListener(() =>
            {
                try { click(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] branch panel: " + ex.Message); }
            });
            return rt;
        }
    }
}
