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

        // 게임 가게 UI 캔버스(달력 모니터 창과 같은 부모)의 맨 뒤에 둔다 → 로딩 화면/메뉴/연출 등 게임 화면이 덮으면 같이 가려진다.
        // 크기는 1920x1080 기준 단위로 만들고, 화면 해상도와 캔버스 배율에 맞춰 localScale 로 맞춘다.
        private Canvas hostCanvas;

        internal static void Ensure()
        {
            if (instance != null) return;
            Transform host = AdvCalendarUIManager.Instance != null && AdvCalendarUIManager.Instance.UIPanel != null
                ? AdvCalendarUIManager.Instance.UIPanel.transform.parent : null;
            GameObject go;
            if (host != null)
            {
                go = new GameObject("VME_BranchPanel", typeof(RectTransform));
                go.transform.SetParent(host, false);
                go.transform.SetAsFirstSibling();
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
            bool show = InStore();
            if (button.activeSelf != show) button.SetActive(show);
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
