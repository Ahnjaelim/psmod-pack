using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapExtension
{
    // 분점 모니터: 게임의 달력 모니터 창(AdvCalendarUIManager.UIPanel: [달력][야간 보고서][현황] 탭)을 복제해서
    // 탭 세 개를 분점 세 곳으로 바꾸고, 베이지 종이 안(탭 아래)에 요약 / [재고 넣기] / 최근 기록을 그린다.
    // 복제본의 게임 스크립트는 지우고 버튼은 새로 연결한다. 원래 달력 창은 건드리지 않는다.
    internal static class BranchMonitor
    {
        private static GameObject current;
        private static readonly List<Button> tabs = new List<Button>();
        private static Color normal, selected;
        private static int index;
        private static TextMeshProUGUI title, info, log;
        private static Button stockButton, fillButton;

        private static readonly Color Ink = new Color32(0x4a, 0x2e, 0x22, 0xff);
        private static readonly Color Red = new Color32(0x8a, 0x24, 0x16, 0xff);

        internal static bool IsOpen => current != null && current.activeInHierarchy;

        internal static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        internal static void Close()
        {
            if (current != null)
            {
                UnityEngine.Object.Destroy(current);
                try { EmporiumAudioPlayer.Instance.audioButton1.Play(); } catch { }
            }
            current = null;
            tabs.Clear();
        }

        internal static void Open()
        {
            try { Build(); }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] branch monitor: " + ex);
                if (current != null) UnityEngine.Object.Destroy(current);
                current = null;
            }
        }

        private static void Build()
        {
            Close();
            AdvCalendarUIManager mgr = AdvCalendarUIManager.Instance;
            GameObject src = mgr != null ? mgr.UIPanel : null;
            if (src == null) return;
            Transform s = src.transform;

            string pTab0 = PathFrom(mgr.calendarButton?.transform, s);
            string pTab1 = PathFrom(mgr.nightlyReportButton?.transform, s);
            string pTab2 = PathFrom(mgr.statusButton?.transform, s);
            string pSec0 = PathFrom(mgr.calendarSection?.transform, s);
            string pSec1 = PathFrom(mgr.nightlyReportSection?.transform, s);
            string pSec2 = PathFrom(mgr.statusSection?.transform, s);
            normal = mgr.normalColor;
            selected = mgr.selectedColor;
            TMP_Text bodySrc = mgr.detailDescriptionTMP;

            // 비활성 부모 밑에 복제 → 게임 스크립트 제거
            var holder = new GameObject("VME_Holder");
            holder.SetActive(false);
            GameObject go = UnityEngine.Object.Instantiate(src, holder.transform, false);
            go.name = "VME_BranchMonitor";
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string ns = mb.GetType().Namespace ?? "";
                if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro")) continue;
                UnityEngine.Object.DestroyImmediate(mb);
            }
            go.transform.SetParent(s.parent, false);
            UnityEngine.Object.Destroy(holder);
            go.transform.SetAsLastSibling();
            go.AddComponent<DragWindow>(); // 창 테두리를 잡고 끌어서 옮기기

            // 원래 내용 칸 셋은 숨긴다
            foreach (string p in new[] { pSec0, pSec1, pSec2 })
            {
                Transform t = Find(go.transform, p);
                if (t != null) t.gameObject.SetActive(false);
            }
            go.SetActive(true); // 크기 계산을 위해 먼저 켠다
            current = go;
            Canvas.ForceUpdateCanvases();

            // 탭 = 분점 세 곳
            tabs.Clear();
            var tabPaths = new[] { pTab0, pTab1, pTab2 };
            for (int i = 0; i < tabPaths.Length && i < Branches.All.Count; i++)
            {
                Transform t = Find(go.transform, tabPaths[i]);
                Button b = t != null ? t.GetComponent<Button>() : null;
                if (b == null) continue;
                int idx = i;
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener(() => { Click(); Select(idx); });
                TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = Branches.All[i].Name.Replace(" 분점", "");
                tabs.Add(b);
            }

            // 나머지 버튼(닫기 X) = 창 닫기
            foreach (Button b in go.GetComponentsInChildren<Button>(true))
            {
                if (tabs.Contains(b)) continue;
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener(Close);
            }

            // 내용 칸 = 베이지 종이 안쪽, 탭 아래.
            // 창 테두리와 종이가 그림 한 장이라 따로 못 찾는다 → 창 그림(가장 큰 이미지) 기준 비율로 종이 안쪽을 잡는다
            RectTransform frame = FindFrame(go);
            if (frame == null) { Close(); return; }
            BlockItemsBehind(frame.gameObject);
            BlockItemsBehind(go);
            var content = new GameObject("VME_Content", typeof(RectTransform));
            content.transform.SetParent(frame, false);
            content.transform.SetAsLastSibling();
            var crt = (RectTransform)content.transform;
            const float PaperX = 0.065f, PaperBottom = 0.098f, Inner = 0.03f; // 종이 여백 (창 크기 비율) + 안쪽 여백
            crt.anchorMin = new Vector2(PaperX + Inner, PaperBottom + Inner);
            crt.anchorMax = new Vector2(1f - PaperX - Inner, 1f);
            float top = frame.rect.height * 0.2f;
            if (tabs.Count > 0)
            {
                var c = new Vector3[4];
                ((RectTransform)tabs[0].transform).GetWorldCorners(c);
                float tabBottom = frame.InverseTransformPoint(c[0]).y;
                top = frame.rect.yMax - tabBottom + 28f;
            }
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = new Vector2(0f, -top);

            // 글자 (종이 위라 어두운 색). 제목은 탭 글꼴, 본문은 게임 설명 글꼴
            TMP_Text tabFont = tabs.Count > 0 ? tabs[0].GetComponentInChildren<TMP_Text>(true) : null;
            float baseSize = tabFont != null ? tabFont.fontSize : 24f;
            title = Text(crt, "Title", tabFont, Red, baseSize * 1.1f, 0f, 0f, 0.7f, 0.11f);
            info = Text(crt, "Info", bodySrc, Ink, baseSize * 0.72f, 0f, 0.11f, 0.75f, 0.30f);
            info.enableWordWrapping = true;
            log = Text(crt, "Log", bodySrc, Ink, baseSize * 0.66f, 0f, 0.47f, 1f, 1f);
            log.enableWordWrapping = true;
            log.overflowMode = TextOverflowModes.Truncate;

            // 요약과 기록 사이 구분선
            var line = new GameObject("Line", typeof(RectTransform));
            line.transform.SetParent(crt, false);
            var lrt = (RectTransform)line.transform;
            lrt.anchorMin = new Vector2(0f, 1f - 0.455f);
            lrt.anchorMax = new Vector2(1f, 1f - 0.455f);
            lrt.sizeDelta = new Vector2(0f, 2f);
            var limg = line.AddComponent<Image>();
            limg.color = new Color(Ink.r, Ink.g, Ink.b, 0.35f);
            limg.raycastTarget = false;

            // [재고 넣기]: 탭 버튼 하나를 복제해서 내용 칸 오른쪽 위
            stockButton = null;
            if (tabs.Count > 0)
            {
                GameObject sb = UnityEngine.Object.Instantiate(tabs[0].gameObject, crt, false);
                sb.name = "VME_StockButton";
                var brt = (RectTransform)sb.transform;
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 1f);
                brt.anchoredPosition = Vector2.zero;
                stockButton = sb.GetComponent<Button>();
                stockButton.onClick = new Button.ButtonClickedEvent();
                stockButton.onClick.AddListener(OpenStock);
                if (stockButton.image != null) stockButton.image.color = normal;
                TMP_Text sl = sb.GetComponentInChildren<TMP_Text>(true);
                if (sl != null) sl.text = "재고 넣기";
            }

            // [자동 채우기]: [재고 넣기] 아래
            fillButton = null;
            if (stockButton != null)
            {
                GameObject fb = UnityEngine.Object.Instantiate(stockButton.gameObject, crt, false);
                fb.name = "VME_FillButton";
                var frt = (RectTransform)fb.transform;
                frt.anchoredPosition = new Vector2(0f, -(((RectTransform)stockButton.transform).rect.height + 8f));
                fillButton = fb.GetComponent<Button>();
                fillButton.onClick = new Button.ButtonClickedEvent();
                fillButton.onClick.AddListener(AutoFill);
                TMP_Text fl = fb.GetComponentInChildren<TMP_Text>(true);
                if (fl != null) fl.text = "자동 채우기";
            }

            // [자동 채우기 설정] (호퍼식 설정 창) + 지금 설정 요약
            autoToggle = null;
            if (tabs.Count > 0)
            {
                autoToggle = SmallButton(tabs[0].gameObject, crt, "자동 채우기 설정", 0f, 0.32f, 0.28f, 0.40f, OpenRestockSettings);
                autoStatus = Text(crt, "AutoStatus", bodySrc, Ink, baseSize * 0.62f, 0.30f, 0.33f, 1f, 0.42f);
            }

            // 처음엔 산 분점 중 첫 번째 탭
            int first = 0;
            for (int i = 0; i < Branches.All.Count; i++) if (Branches.All[i].Owned) { first = i; break; }
            Click();
            Select(first);
        }

        private static void Click()
        {
            try { EmporiumAudioPlayer.Instance.audioButton1.Play(); } catch { }
        }

        private static void Select(int i)
        {
            index = i;
            for (int t = 0; t < tabs.Count; t++)
                if (tabs[t] != null && tabs[t].image != null) tabs[t].image.color = t == i ? selected : normal;
            if (i < 0 || i >= Branches.All.Count || title == null) return;
            BranchSite b = Branches.All[i];
            title.text = b.Name;
            if (!b.Owned)
            {
                info.text = b.Desc + "\n아직 열지 않은 분점. 외출 지도에서 " + b.Price + " ₵에 열 수 있다.";
                log.text = "";
                if (stockButton != null) stockButton.gameObject.SetActive(false);
                if (fillButton != null) fillButton.gameObject.SetActive(false);
                ShowRestockControls(null);
                return;
            }
            if (stockButton != null) stockButton.gameObject.SetActive(true);
            if (fillButton != null) fillButton.gameObject.SetActive(true);
            ShowRestockControls(b);
            float am = BranchSim.AttractivenessMult(out int av);
            info.text = "재고 " + Branches.StockCount(b) + "개 · 손님 하루 " + Mathf.RoundToInt(b.VisitorsMin * am) + "~" + Mathf.RoundToInt(b.VisitorsMax * am)
                        + "명 (본점 매력도 " + av + ") · 판매가 ×" + b.PriceMult.ToString("0.0#")
                        + "\n오늘 아침 매출 " + b.LastIncome + " ₵ · 지금까지 " + b.Earned + " ₵"
                        + "\n손님 구성: " + BranchSim.Mix(b);
            log.text = b.Logs.Count == 0
                ? "아직 기록이 없다. 재고를 넣으면 다음 날 아침부터 손님이 온다."
                : string.Join("\n\n", b.Logs);
        }

        // ---- 매일 자동 채우기 / 종류 필터 ----
        private static Button autoToggle;
        private static TextMeshProUGUI autoStatus;

        private static void ShowRestockControls(BranchSite b)
        {
            if (autoToggle != null) autoToggle.gameObject.SetActive(b != null);
            if (autoStatus == null) return;
            autoStatus.gameObject.SetActive(b != null);
            if (b == null) return;
            string mode = b.FilterMode == 1 ? "허용 목록 " + b.FilterIds.Count + "개만"
                        : b.FilterMode == 2 ? "손님이 좋아하는 것 (차단 " + b.FilterIds.Count + "개 제외)"
                        : "손님이 좋아하는 것 전부";
            autoStatus.text = "매일 자동 채우기: " + (b.AutoRestock ? "켜짐" : "꺼짐") + " · " + mode
                              + (b.HoldOnPriceDrop ? " · 가격 하락 땐 안 팖" : "");
        }

        // 설정 창: 모니터는 뒤에 켜 둔 채로 그 위에 연다
        private static void OpenRestockSettings()
        {
            if (index < 0 || index >= Branches.All.Count) return;
            BranchSite b = Branches.All[index];
            BranchRestockWindow.Open(b);
        }

        // 설정이 바뀌면 요약 줄 갱신
        internal static void Refresh()
        {
            if (current != null) Select(index);
        }

        // 아이템 고르기 중에는 가게 물건이 보이게 잠깐 숨긴다
        internal static void SetHidden(bool hidden)
        {
            if (current != null) current.SetActive(!hidden);
        }

        // 다른 창(설정 창)을 모니터보다 위 층으로
        internal static void RaiseAbove(GameObject other)
        {
            if (current == null || other == null) return;
            Canvas mc = current.GetComponentInParent<Canvas>()?.rootCanvas;
            Canvas oc = other.GetComponentInParent<Canvas>()?.rootCanvas;
            if (mc == null) return;
            if (oc == mc)
            {
                // 같은 캔버스: 모니터를 그 창보다 앞 순서로 (창이 위에 그려지게)
                Transform a = current.transform, o = other.transform;
                while (o.parent != null && o.parent != a.parent) o = o.parent;
                if (o.parent == a.parent) a.SetSiblingIndex(Mathf.Max(0, o.GetSiblingIndex() - 1));
                return;
            }
            int order = Mathf.Max(mc.sortingOrder, oc != null ? oc.sortingOrder : 0) + 10;
            Canvas c = other.GetComponent<Canvas>() ?? other.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingLayerID = mc.sortingLayerID;
            c.sortingOrder = order;
            if (other.GetComponent<GraphicRaycaster>() == null) other.AddComponent<GraphicRaycaster>();
        }

        // 탭 버튼을 복제한 작은 버튼 (내용 칸 비율 좌표, 위가 0)
        private static Button SmallButton(GameObject template, RectTransform parent, string label, float x0, float y0, float x1, float y1, Action click)
        {
            GameObject go = UnityEngine.Object.Instantiate(template, parent, false);
            go.name = "VME_Btn_" + label;
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = new Vector2(x0, 1f - y1);
            rt.anchorMax = new Vector2(x1, 1f - y0);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            Button b = go.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(() => click());
            TMP_Text t = go.GetComponentInChildren<TMP_Text>(true);
            if (t != null)
            {
                t.text = label;
                t.enableAutoSizing = true;
                t.fontSizeMax = t.fontSize * 0.75f;
                t.fontSizeMin = 6f;
                t.enableWordWrapping = false;
            }
            return b;
        }

        // [재고 넣기]: 모니터 위에 그 분점 재고 창을 연다 (아래 인벤토리에서 끌어 넣기, 모니터는 끌어서 옆으로 치울 수 있음)
        private static void OpenStock()
        {
            if (index < 0 || index >= Branches.All.Count) return;
            BranchSite b = Branches.All[index];
            Branches.OpenStock(b); // 모니터는 뒤에 켜 둔다
            try
            {
                GameObject w = b.StockBox?.contentWindow?.handler?.gameObject;
                RaiseAbove(w);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] stock raise: " + ex.Message);
            }
        }

        // [자동 채우기]: 본점 인벤토리에서 이 분점 손님이 좋아하는 물건을 재고로 (고정한 물건은 안 건드림)
        private static void AutoFill()
        {
            if (index < 0 || index >= Branches.All.Count) return;
            BranchSite b = Branches.All[index];
            int n = 0;
            try { n = Branches.AutoFill(b); }
            catch (Exception ex) { Debug.LogWarning("[MapExtension] auto fill: " + ex.Message); }
            Click();
            try
            {
                StoreUIManager.Instance.Notify(n > 0
                    ? b.Name + " 재고로 " + n + "개 옮겼다. (고정한 물건은 그대로)"
                    : "옮길 물건이 없다. (이 분점 손님이 좋아할 물건이 없거나 재고가 꽉 찼다)");
            }
            catch
            {
            }
            Select(index);
        }

        // 내용 칸 안에 비율(0~1, 위가 0)로 글자 칸
        private static TextMeshProUGUI Text(RectTransform parent, string name, TMP_Text style, Color color, float size, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (style != null) t.font = style.font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(x0, 1f - y1);
            rt.anchorMax = new Vector2(x1, 1f - y0);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }

        // 모니터 뒤 아이템의 툴팁/클릭/끌기를 막는다.
        // 게임은 마우스 아래 UI 를 전부 훑어 첫 "아이템 그림 조각"(TreeNodeRender)을 찾는데, 보통 UI 는 건너뛴다.
        // 아이템이 연결 안 된 빈 TreeNodeRender 를 모니터 그림에 붙이면 거기서 멈추고 "아이템 없음"이 된다.
        private static void BlockItemsBehind(GameObject g)
        {
            if (g == null) return;
            Graphic gr = g.GetComponent<Graphic>();
            if (gr == null) return;
            gr.raycastTarget = true;
            if (g.GetComponent<TreeNodeRender>() == null) g.AddComponent<TreeNodeRender>();
        }

        // 창 그림: 복제본 안에서 화면보다 작은 이미지 중 가장 큰 것 (화면 전체를 덮는 어둠 판 등은 제외)
        private static RectTransform FindFrame(GameObject window)
        {
            Canvas canvas = window.GetComponentInParent<Canvas>()?.rootCanvas;
            float screen = canvas != null ? Area((RectTransform)canvas.transform) : float.MaxValue;
            RectTransform best = null;
            float bestArea = 0f;
            foreach (Image img in window.GetComponentsInChildren<Image>(false))
            {
                float a = Area(img.rectTransform);
                if (a < screen * 0.95f && a > bestArea) { bestArea = a; best = img.rectTransform; }
            }
            return best;
        }

        private static float Area(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Mathf.Abs((c[2].x - c[0].x) * (c[2].y - c[0].y));
        }

        private static string PathFrom(Transform t, Transform root)
        {
            if (t == null) return null;
            if (t == root) return "";
            var parts = new List<string>();
            while (t != null && t != root) { parts.Insert(0, t.name); t = t.parent; }
            return t == root ? string.Join("/", parts) : null;
        }

        private static Transform Find(Transform root, string path)
        {
            if (path == null) return null;
            return path.Length == 0 ? root : root.Find(path);
        }
    }

    // 잡고 끌어서 창 옮기기 (버튼 위에서 시작해도 끌린다)
    // 게임 창 끌기와 같이 UIDragHandler.isDraggingAnyUI 를 켜서, 끄는 동안 아이템 범위 선택(네모)이 안 생기게 한다
    internal class DragWindow : MonoBehaviour, UnityEngine.EventSystems.IBeginDragHandler, UnityEngine.EventSystems.IDragHandler,
        UnityEngine.EventSystems.IEndDragHandler, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
    {
        private RectTransform rt;
        private Canvas canvas;
        private bool holding;

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
        {
            holding = true;
            UIDragHandler.isDraggingAnyUI = true;
        }

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => Release();
        public void OnEndDrag(UnityEngine.EventSystems.PointerEventData e) => Release();
        private void OnDisable() => Release();

        private void Release()
        {
            if (!holding) return;
            holding = false;
            UIDragHandler.isDraggingAnyUI = false;
        }

        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData e)
        {
            rt = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            UIDragHandler.isDraggingAnyUI = true;
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData e)
        {
            if (rt == null) return;
            float f = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            rt.anchoredPosition += e.delta / f;
        }
    }
}
