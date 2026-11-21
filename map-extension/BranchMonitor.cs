using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapExtension
{
    // 분점 모니터: 게임의 달력 모니터 창(AdvCalendarUIManager.UIPanel: [달력][야간 보고서][현황] 탭)을 복제한 "웹앱식" 관리 창.
    //  - 위 탭 = [전체] + 분점 세 곳 (원래 탭 셋 + 하나 복제, 같은 폭으로 다시 배치)
    //  - 왼쪽 사이드바 = 재고 / 판매 기록 / 손님 / 통계 / 설정 + 아래 요약
    //  - 오른쪽 = 제목 + 도구 버튼, 상태 줄, 표 머리줄, 스크롤 표
    // 복제본의 게임 스크립트는 지우고 버튼은 새로 연결한다. 원래 달력 창은 건드리지 않는다.
    internal static class BranchMonitor
    {
        private static GameObject current;
        private static readonly List<Button> tabs = new List<Button>(); // [0] = 전체, [1..] = Branches.All
        private static readonly List<Win98Button> menu = new List<Win98Button>();
        private static Color normal, selected;
        private static int index;   // -1 = 전체, 0.. = Branches.All
        private static int page;    // 0 재고 1 판매 기록 2 손님 3 통계 4 설정
        private static int sortMode;
        private static bool emptyOnly;
        private static int logDay = -1; // 판매 기록에서 보는 날 (-1 = 가장 최근)

        private static GameObject template;   // 버튼 복제 원본 (탭)
        private static TMP_Text bodyFont, titleFont;
        private static float baseSize;
        private static RectTransform sideMenu, toolbar, header, body;
        private static ScrollRect scroll;
        private static TextMeshProUGUI sideTitle, sideSummary, pageTitle, status;

        private static readonly Color Ink = new Color32(0x4a, 0x2e, 0x22, 0xff);
        private static readonly Color Red = new Color32(0x8a, 0x24, 0x16, 0xff);
        private static readonly Color Green = new Color32(0x2f, 0x6b, 0x2a, 0xff);

        private static readonly string[] Pages = { "재고", "판매 기록", "손님", "통계", "설정" };
        private static readonly string[] SortNames = { "가격", "이름", "수량", "이벤트" };

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
            menu.Clear();
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
            bodyFont = mgr.detailDescriptionTMP;

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

            foreach (string p in new[] { pSec0, pSec1, pSec2 })
            {
                Transform t = Find(go.transform, p);
                if (t != null) t.gameObject.SetActive(false);
            }
            go.SetActive(true); // 크기 계산을 위해 먼저 켠다
            current = go;
            Canvas.ForceUpdateCanvases();

            // ---- 위 탭: [전체] + 분점 세 곳 ----
            var branchTabs = new List<Button>();
            foreach (string p in new[] { pTab0, pTab1, pTab2 })
            {
                Transform t = Find(go.transform, p);
                Button b = t != null ? t.GetComponent<Button>() : null;
                if (b != null) branchTabs.Add(b);
            }
            if (branchTabs.Count == 0) { Close(); return; }
            template = branchTabs[0].gameObject;
            titleFont = template.GetComponentInChildren<TMP_Text>(true);
            baseSize = titleFont != null ? titleFont.fontSize : 24f;

            Button allTab = UnityEngine.Object.Instantiate(template, template.transform.parent, false).GetComponent<Button>();
            allTab.name = "VME_TabAll";
            allTab.transform.SetSiblingIndex(template.transform.GetSiblingIndex());
            tabs.Clear();
            tabs.Add(allTab);
            tabs.AddRange(branchTabs);
            LayoutTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                int bi = i - 1;
                Button b = tabs[i];
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener(() => { Click(); SelectBranch(bi); });
                TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
                if (label == null) continue;
                label.text = bi < 0 ? "전체" : bi < Branches.All.Count ? Branches.All[bi].Name.Replace(" 분점", "") : "";
                label.enableAutoSizing = true;
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = 8f;
                label.enableWordWrapping = false;
                if (bi >= 0 && bi < Branches.All.Count && !Branches.All[bi].Owned) label.alpha = 0.45f; // 안 산 분점
            }

            // 나머지 버튼(닫기 X) = 창 닫기
            foreach (Button b in go.GetComponentsInChildren<Button>(true))
            {
                if (tabs.Contains(b)) continue;
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener(Close);
            }

            // ---- 내용 칸 = 베이지 종이 안쪽, 탭 아래 ----
            RectTransform frame = FindFrame(go);
            if (frame == null) { Close(); return; }
            BlockItemsBehind(frame.gameObject);
            BlockItemsBehind(go);
            RectTransform crt = Box(frame, "VME_Content", 0f, 0f, 1f, 1f);
            const float PaperX = 0.065f, PaperBottom = 0.098f, Inner = 0.03f; // 종이 여백 (창 크기 비율) + 안쪽 여백
            crt.anchorMin = new Vector2(PaperX + Inner, PaperBottom + Inner);
            crt.anchorMax = new Vector2(1f - PaperX - Inner, 1f);
            var c = new Vector3[4];
            ((RectTransform)tabs[0].transform).GetWorldCorners(c);
            float top = frame.rect.yMax - frame.InverseTransformPoint(c[0]).y + 24f;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = new Vector2(0f, -top);

            // ---- 왼쪽 사이드바 ----
            const float SideW = 0.2f;
            RectTransform side = Box(crt, "Side", 0f, 0f, SideW, 1f);
            sideTitle = Text(side, "SideTitle", titleFont, Red, baseSize * 0.85f, 0f, 0f, 1f, 0.09f);
            sideTitle.enableAutoSizing = true; sideTitle.fontSizeMax = baseSize * 0.85f; sideTitle.fontSizeMin = 8f;
            Line(side, 0f, 0.1f, 1f, 0.1f);
            sideMenu = Box(side, "Menu", 0f, 0.12f, 1f, 0.72f);
            menu.Clear();
            for (int i = 0; i < Pages.Length; i++)
            {
                int pi = i;
                // 높이 = 도구 버튼과 같게 (도구 버튼: 본문 높이 0.085 × 0.84, 사이드 메뉴 칸은 본문 높이의 0.6)
                const float H = 0.085f * 0.84f / 0.6f, Step = H + 0.035f;
                float y0 = i * Step;
                Win98Button mb = Win98Button.Create(sideMenu, Pages[i], bodyFont, baseSize * 0.55f, 0f, y0, 1f, y0 + H, () => { Click(); page = pi; Render(); });
                menu.Add(mb);
            }
            Line(side, 0f, 0.74f, 1f, 0.74f);
            sideSummary = Text(side, "Summary", bodyFont, Ink, baseSize * 0.55f, 0f, 0.76f, 1f, 1f);
            sideSummary.enableWordWrapping = true;
            // 세로 구분선
            Line(crt, SideW + 0.015f, 0f, SideW + 0.015f, 1f, vertical: true);

            // ---- 오른쪽 본문 ----
            RectTransform main = Box(crt, "Main", SideW + 0.035f, 0f, 1f, 1f);
            pageTitle = Text(main, "PageTitle", titleFont, Red, baseSize * 0.95f, 0f, 0f, 0.4f, 0.09f);
            toolbar = Box(main, "Toolbar", 0.3f, 0f, 1f, 0.085f);
            status = Text(main, "Status", bodyFont, Ink, baseSize * 0.55f, 0f, 0.1f, 1f, 0.155f);
            status.enableAutoSizing = true; status.fontSizeMax = baseSize * 0.55f; status.fontSizeMin = 8f;
            header = Box(main, "Header", 0f, 0.16f, 1f, 0.215f);
            Line(main, 0f, 0.218f, 1f, 0.218f);
            BuildScroll(Box(main, "Viewport", 0f, 0.225f, 1f, 1f));

            // 처음엔 산 분점 중 첫 번째
            int first = -1;
            for (int i = 0; i < Branches.All.Count; i++) if (Branches.All[i].Owned) { first = i; break; }
            Click();
            index = first;
            page = 0;
            Render();
        }

        // 탭 네 개를 원래 탭 세 개가 차지하던 폭에 같은 너비로 다시 놓는다 (레이아웃 그룹이 있으면 그쪽에 맡김)
        private static void LayoutTabs()
        {
            Transform parent = tabs[0].transform.parent;
            if (parent.GetComponent<HorizontalOrVerticalLayoutGroup>() != null) return;
            for (int i = 1; i < tabs.Count; i++) if (tabs[i].transform.parent != parent) return;
            Rect r0 = LocalRect((RectTransform)tabs[1].transform);
            Rect rL = LocalRect((RectTransform)tabs[tabs.Count - 1].transform);
            float gap = tabs.Count > 2 ? LocalRect((RectTransform)tabs[2].transform).xMin - r0.xMax : 4f;
            if (gap < 0f || gap > r0.width) gap = 4f;
            float left = r0.xMin, right = rL.xMax;
            int n = tabs.Count;
            float w = (right - left - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                var rt = (RectTransform)tabs[i].transform;
                if (i == 0) // 복제본: 원래 첫 탭 자리에서 시작
                {
                    rt.anchorMin = ((RectTransform)tabs[1].transform).anchorMin;
                    rt.anchorMax = ((RectTransform)tabs[1].transform).anchorMax;
                    rt.pivot = ((RectTransform)tabs[1].transform).pivot;
                    rt.sizeDelta = ((RectTransform)tabs[1].transform).sizeDelta;
                    rt.anchoredPosition = ((RectTransform)tabs[1].transform).anchoredPosition;
                }
                SetX(rt, left + i * (w + gap), w);
            }
        }

        private static Rect LocalRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            Vector3 a = rt.parent.InverseTransformPoint(c[0]);
            Vector3 b = rt.parent.InverseTransformPoint(c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static void SetX(RectTransform rt, float left, float width)
        {
            Rect r = LocalRect(rt);
            float sx = Mathf.Abs(rt.localScale.x) > 0.0001f ? Mathf.Abs(rt.localScale.x) : 1f;
            float dw = width - r.width;
            rt.sizeDelta += new Vector2(dw / sx, 0f);
            float newLeft = r.xMin - rt.pivot.x * dw;
            rt.anchoredPosition += new Vector2(left - newLeft, 0f);
        }

        private static void BuildScroll(RectTransform viewport)
        {
            var vimg = viewport.gameObject.AddComponent<Image>();
            vimg.color = new Color(0f, 0f, 0f, 0f); // 휠/끌기 받는 투명 판
            viewport.gameObject.AddComponent<RectMask2D>();
            body = Box(viewport, "Rows", 0f, 0f, 1f, 0f);
            body.anchorMin = new Vector2(0f, 1f);
            body.anchorMax = new Vector2(1f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.offsetMin = body.offsetMax = Vector2.zero;
            var vl = body.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childControlHeight = true;
            vl.childControlWidth = true;
            vl.childForceExpandHeight = false;
            vl.childForceExpandWidth = true;
            vl.spacing = 0f;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = body;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = baseSize * 1.5f;
        }

        private static void Click()
        {
            try { EmporiumAudioPlayer.Instance.audioButton1.Play(); } catch { }
        }

        private static void SelectBranch(int bi)
        {
            index = bi;
            Render();
        }

        // 설정이 바뀌면 다시 그림
        internal static void Refresh()
        {
            if (current != null) Render();
        }

        private static BranchSite Site => index >= 0 && index < Branches.All.Count ? Branches.All[index] : null;

        private static IEnumerable<BranchSite> Owned => Branches.All.Where(b => b.Owned);

        // ---- 그리기 ----
        private static void Render()
        {
            if (current == null || body == null) return;
            for (int t = 0; t < tabs.Count; t++)
                if (tabs[t] != null && tabs[t].image != null) tabs[t].image.color = t - 1 == index ? selected : normal;
            for (int m = 0; m < menu.Count; m++)
                if (menu[m] != null) menu[m].Latched = m == page;
            Clear(toolbar);
            Clear(header);
            Clear(body);

            BranchSite b = Site;
            sideTitle.text = b != null ? b.Name : "전체 분점";
            sideSummary.text = Summary(b);
            pageTitle.text = Pages[page];

            if (b != null && !b.Owned)
            {
                sideMenu.gameObject.SetActive(false);
                pageTitle.text = b.Name;
                status.text = "아직 열지 않은 분점";
                Para(b.Desc + "\n\n외출 지도에서 " + N(b.Price) + " ₵에 열 수 있다.\n손님 하루 " + b.VisitorsMin + "~" + b.VisitorsMax
                     + "명 · 판매가 ×" + b.PriceMult.ToString("0.0#") + "\n손님 구성: " + BranchSim.Mix(b));
                ScrollTop();
                return;
            }
            sideMenu.gameObject.SetActive(true);
            if (b == null && !Owned.Any())
            {
                status.text = "";
                Para("아직 연 분점이 없다. 외출 지도에서 분점 자리를 눌러 열 수 있다.");
                ScrollTop();
                return;
            }

            switch (page)
            {
                case 0: RenderStock(b); break;
                case 1: RenderLog(b); break;
                case 2: RenderVisitors(b); break;
                case 3: RenderStats(b); break;
                default: RenderSettings(b); break;
            }
            ScrollTop();
        }

        private static string Summary(BranchSite b)
        {
            if (b != null && !b.Owned) return "미개설\n개설 " + N(b.Price) + " ₵";
            var list = b != null ? new List<BranchSite> { b } : Owned.ToList();
            int stock = list.Sum(x => Branches.StockCount(x));
            int today = list.Sum(x => x.LastIncome);
            long total = list.Sum(x => x.Earned);
            return (b == null ? "운영 " + list.Count + "곳\n" : "") + "재고 " + N(stock) + "개\n오늘 아침 " + N(today) + " ₵\n누적 " + N(total) + " ₵";
        }

        // ---- 재고 ----
        private struct StockRow
        {
            public BranchSite Site;
            public GameItem Item;
            public string Name, Type, Liked;
            public int Qty, Price, Ev;
        }

        private static void RenderStock(BranchSite b)
        {
            ToolButton(0, "정렬: " + SortNames[sortMode], () => { Click(); sortMode = (sortMode + 1) % SortNames.Length; Render(); });
            if (b != null)
            {
                ToolButton(1, "자동 채우기", AutoFill);
                ToolButton(2, "재고 넣기", OpenStock);
            }

            var rows = new List<StockRow>();
            foreach (BranchSite site in b != null ? new List<BranchSite> { b } : Owned.ToList())
            {
                GameInventory inv = Branches.Stock(site);
                foreach (GameItem i in inv.childItems)
                {
                    if (i == null) continue;
                    var r = new StockRow { Site = site, Item = i, Name = BranchSim.Name(i), Qty = Math.Max(1, i.unitCount) };
                    BranchSim.RememberId(r.Name, i);
                    try { r.Price = BranchSim.UnitPrice(site, i); } catch { }
                    try { r.Ev = BranchSim.EventPercent(i); } catch { }
                    r.Type = TypeName(i);
                    r.Liked = BranchSim.IsContraband(i) ? "안 팔림 (밀수품)" : LikedBy(site, i);
                    rows.Add(r);
                }
            }
            int slots = rows.Count;
            int count = rows.Sum(r => r.Qty);
            // 같은 물건(분점·이름·판매가·이벤트가 같음)은 한 줄로 묶어 수량을 더한다
            rows = rows.GroupBy(r => (r.Site.Key, r.Name, r.Price, r.Ev))
                       .Select(g => { StockRow f = g.First(); f.Qty = g.Sum(x => x.Qty); return f; })
                       .ToList();
            IEnumerable<StockRow> SortRows(IEnumerable<StockRow> list)
            {
                switch (sortMode)
                {
                    case 1: return list.OrderBy(r => r.Name);
                    case 2: return list.OrderByDescending(r => r.Qty).ThenBy(r => r.Name);
                    case 3: return list.OrderByDescending(r => r.Ev).ThenByDescending(r => r.Price);
                    default: return list.OrderByDescending(r => r.Price).ThenBy(r => r.Name);
                }
            }
            // 종류별 묶음 (묶음 순서도 정렬 기준을 따른다: 가격=재고 가치, 이름=종류 이름, 수량=개수, 이벤트=최대 이벤트)
            var groups = rows.GroupBy(r => r.Type).ToList();
            switch (sortMode)
            {
                case 1: groups = groups.OrderBy(g => g.Key).ToList(); break;
                case 2: groups = groups.OrderByDescending(g => g.Sum(r => r.Qty)).ToList(); break;
                case 3: groups = groups.OrderByDescending(g => g.Max(r => r.Ev)).ToList(); break;
                default: groups = groups.OrderByDescending(g => g.Sum(r => (long)r.Price * r.Qty)).ToList(); break;
            }

            if (b != null)
            {
                float am = BranchSim.AttractivenessMult(b, out int av);
                status.text = "물건 " + count + "개 · " + rows.Count + "가지 · " + slots + "칸 · 손님 하루 " + Mathf.RoundToInt(b.VisitorsMin * am) + "~" + Mathf.RoundToInt(b.VisitorsMax * am)
                              + "명 (매력도 " + av + ") · 판매가 ×" + b.PriceMult.ToString("0.0#");
            }
            else status.text = "분점 " + Owned.Count() + "곳 재고 합계 " + count + "개 · " + rows.Count + "가지 · " + slots + "칸";

            bool all = b == null;
            float[] cols = all
                ? new[] { 0f, 0.07f, 0.36f, 0.49f, 0.57f, 0.69f, 0.78f, 1f }
                : new[] { 0f, 0.07f, 0.42f, 0.52f, 0.65f, 0.75f, 1f };
            var heads = all
                ? new[] { "", "이름", "분점", ">재고", ">판매가", ">이벤트", "좋아하는 손님" }
                : new[] { "", "이름", ">재고", ">판매가", ">이벤트", "좋아하는 손님" };
            HeaderRow(cols, heads);
            if (rows.Count == 0) { Para(b != null ? "재고가 비어 있다. [재고 넣기] 또는 [자동 채우기]로 채우자." : "모든 분점 재고가 비어 있다."); return; }

            foreach (var g in groups)
            {
                int gq = g.Sum(r => r.Qty);
                long gv = g.Sum(r => (long)r.Price * r.Qty);
                Para("<b><color=#8a2416>" + g.Key + "</color></b>  " + g.Count() + "가지 · " + N(gq) + "개 · 재고 가치 " + N(gv) + " ₵");
                int n = 0;
                foreach (StockRow r in SortRows(g))
                {
                    RectTransform row = Row(n++);
                    Icon(row, cols[0], cols[1], r.Item);
                    int k = 1;
                    Cell(row, cols[k], cols[++k], r.Name);
                    if (all) Cell(row, cols[k], cols[++k], r.Site.Name.Replace(" 분점", ""));
                    NumCell(row, cols[k], cols[++k], N(r.Qty) + "개");
                    NumCell(row, cols[k], cols[++k], N(r.Price) + " ₵");
                    NumCell(row, cols[k], cols[++k], r.Ev == 0 ? "–" : (r.Ev > 0 ? "+" : "") + r.Ev + "%", r.Ev > 0 ? Green : r.Ev < 0 ? Red : Ink);
                    Cell(row, cols[k], cols[++k], r.Liked, r.Liked.Length == 0 || r.Liked.StartsWith("안 팔림") || r.Liked == "없음" ? Red : Ink);
                }
            }
        }

        // 이 분점 손님 중 이 물건 종류를 좋아하는 손님 (짧은 이름, 셋 이상이면 "N종")
        private static string LikedBy(BranchSite site, GameItem i)
        {
            var who = new List<string>();
            foreach (var w in site.Visitors)
                if (BranchSim.Types.TryGetValue(w.type, out BranchSim.VisitorType t) && BranchSim.Likes(t, i)) who.Add(ShortName(w.type));
            if (who.Count == 0) return "없음";
            return who.Count > 2 ? who.Count + "종 (" + who[0] + " 외)" : string.Join(", ", who);
        }

        // ---- 판매 기록 ----
        private static void RenderLog(BranchSite b)
        {
            ToolButton(0, emptyOnly ? "빈손만 보는 중" : "전체 보는 중", () => { Click(); emptyOnly = !emptyOnly; Render(); });
            var entries = new List<(int day, string site, string text)>();
            foreach (BranchSite site in b != null ? new List<BranchSite> { b } : Owned.ToList())
                foreach (string l in site.Logs) entries.Add((DayOf(l), site.Name, l));
            entries = entries.OrderByDescending(e => e.day).ToList();

            // 하루씩 넘겨 보기: [<] N일차 [>]  (< = 이전 날, > = 다음 날)
            List<int> days = entries.Select(e => e.day).Distinct().OrderBy(d => d).ToList();
            int di = days.IndexOf(logDay);
            if (di < 0) { di = days.Count - 1; logDay = di >= 0 ? days[di] : -1; }
            int prevDay = di > 0 ? days[di - 1] : -1, nextDay = di >= 0 && di < days.Count - 1 ? days[di + 1] : -1;
            Win98Button.Create(toolbar, "<", bodyFont, baseSize * 0.6f, 0.15f, 0.08f, 0.24f, 0.92f,
                () => { if (prevDay >= 0) { Click(); logDay = prevDay; Render(); } });
            TextMeshProUGUI dayLabel = Text(toolbar, "Day", titleFont, Red, baseSize * 0.75f, 0.24f, 0f, 0.55f, 1f);
            dayLabel.alignment = TextAlignmentOptions.Center;
            dayLabel.text = logDay >= 0 ? logDay + "일차" + (nextDay < 0 ? " (최근)" : "") : "–";
            dayLabel.enableAutoSizing = true; dayLabel.fontSizeMax = baseSize * 0.75f; dayLabel.fontSizeMin = 8f;
            Win98Button.Create(toolbar, ">", bodyFont, baseSize * 0.6f, 0.55f, 0.08f, 0.64f, 0.92f,
                () => { if (nextDay >= 0) { Click(); logDay = nextDay; Render(); } });
            entries = entries.Where(e => e.day == logDay).ToList();

            status.text = "최근 7일 중 " + (di + 1) + "/" + days.Count + "번째 날 · " + (emptyOnly ? "빈손으로 간 손님만" : "전부") + (b == null ? " · 분점 " + Owned.Count() + "곳 합침" : "");
            float[] cols = { 0f, 0.2f, 0.31f, 0.66f, 0.77f, 1f };
            HeaderRow(cols, new[] { "손님", ">소지금", "산 것", ">금액", "비고" });
            if (entries.Count == 0) { Para("아직 기록이 없다. 재고를 넣으면 다음 날 아침부터 손님이 온다."); return; }
            Color dim = new Color(Ink.r, Ink.g, Ink.b, 0.55f);
            foreach (var e in entries)
            {
                string[] lines = e.text.Split('\n');
                string head = Commas(lines[0]); // 쉼표는 색 태그(#8a2416) 붙이기 전에
                if (b == null) head = "<color=#8a2416>" + e.site.Replace(" 분점", "") + "</color>  " + head;
                Para("<b>" + head + "</b>");
                int n = 0, shown = 0;
                for (int i = 1; i < lines.Length; i++)
                {
                    LogLine l = ParseLine(lines[i]);
                    if (l == null) continue;
                    if (emptyOnly && (l.Info || l.Bought)) continue;
                    RectTransform row = Row(n++);
                    shown++;
                    if (l.Info) { Cell(row, cols[0], cols[5], "<i>" + l.Who + "</i>", dim); continue; }
                    Color c = l.Bought ? Ink : dim;
                    Cell(row, cols[0], cols[1], l.Who, c);
                    NumCell(row, cols[1], cols[2], l.Wallet >= 0 ? N(l.Wallet) + " ₵" : "–", c);
                    if (l.Bought && l.Parts.Count > 0) ItemsCell(row, cols[2], cols[3], l, c);
                    else Cell(row, cols[2], cols[3], l.Bought ? l.Items : "둘러보고 그냥 감", c);
                    NumCell(row, cols[3], cols[4], l.Bought ? N(l.Paid) + " ₵" : "–", l.Bought ? Green : c);
                    Cell(row, cols[4], cols[5], l.Note, l.Bought ? Ink : Red);
                }
                if (emptyOnly && shown == 0) Cell(Row(0), cols[0], cols[5], "빈손으로 간 손님 없음", dim);
            }
        }

        private class LogLine
        {
            public bool Info, Bought;
            public string Who = "", Items = "", Note = "";
            public int Wallet = -1, Paid;
            public readonly List<(string name, int qty)> Parts = new List<(string, int)>();
        }

        // 산 것 칸: 아이템 그림(+ 수량)을 가로로. 그림을 못 찾은 건 이름 글자로
        private static void ItemsCell(RectTransform row, float x0, float x1, LogLine l, Color color)
        {
            RectTransform box = Box(row, "Items", x0, 0f, x1, 1f);
            box.offsetMax = new Vector2(-4f, 0f);
            var hl = box.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.spacing = 6f;
            float h = RowHeight * 0.86f;
            foreach (var (name, qty) in l.Parts)
            {
                Sprite sp = ItemSprite(name);
                if (sp != null)
                {
                    var ig = new GameObject("Icon", typeof(RectTransform));
                    ig.transform.SetParent(box, false);
                    var img = ig.AddComponent<Image>();
                    img.sprite = sp;
                    img.preserveAspect = true;
                    img.raycastTarget = true; // 마우스 올리면 이름 (HoverName)
                    ig.AddComponent<HoverName>().Text = name + (qty > 1 ? " ×" + qty : "");
                    float aspect = sp.rect.height > 0 ? Mathf.Clamp(sp.rect.width / sp.rect.height, 0.5f, 2.5f) : 1f;
                    var le = ig.AddComponent<LayoutElement>();
                    le.preferredHeight = h;
                    le.preferredWidth = h * aspect;
                    if (qty > 1) Label(box, "×" + qty, color);
                }
                else Label(box, name + (qty > 1 ? " ×" + qty : ""), color);
            }
        }

        private static void Label(RectTransform parent, string text, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (bodyFont != null) t.font = bodyFont.font;
            t.fontSize = baseSize * 0.5f;
            t.color = color;
            t.enableWordWrapping = false;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.raycastTarget = false;
            t.text = text;
        }

        // 기록 속 이름 → 그림: 이번 실행에서 본 이름(판매/재고) → 아이템 목록 이름 (뒤 괄호 "(보통)" 같은 건 떼고도 시도)
        private static readonly Dictionary<string, Sprite> spriteByName = new Dictionary<string, Sprite>();

        // 세이브 불러올 때: 기록에 나오는 아이템 그림을 미리 찾아 둔다 (탭 열 때 멈칫하지 않게).
        // ID 를 아는 이름 먼저 몇 개씩 프레임마다, 모르는 이름이 있으면 전체 아이템 목록을 만든 뒤 마저.
        private static Coroutine prewarm;

        internal static void Prewarm()
        {
            try
            {
                if (CustomUIManager.Instance == null) return;
                if (prewarm != null) CustomUIManager.Instance.StopCoroutine(prewarm);
                prewarm = CustomUIManager.Instance.StartCoroutine(PrewarmRoutine());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] prewarm: " + ex.Message);
            }
        }

        private static IEnumerator PrewarmRoutine()
        {
            yield return null;
            var names = new List<string>();
            foreach (BranchSite b in Branches.All)
                foreach (string log in b.Logs)
                    foreach (string line in log.Split('\n'))
                    {
                        LogLine l = null;
                        try { l = ParseLine(line); } catch { }
                        if (l == null) continue;
                        foreach (var (name, _) in l.Parts)
                            if (!names.Contains(name)) names.Add(name);
                    }
            var unknown = new List<string>();
            int n = 0;
            foreach (string name in names)
            {
                if (spriteByName.ContainsKey(name)) continue;
                string bare = Regex.Replace(name, @"\s*\([^()]*\)\s*$", "");
                if (!BranchSim.NameToId.ContainsKey(name) && !BranchSim.NameToId.ContainsKey(bare)) { unknown.Add(name); continue; }
                ItemSprite(name);
                if (++n % 4 == 0) yield return null;
            }
            if (unknown.Count > 0)
            {
                ItemIndex.EnsureBuilding();
                while (!ItemIndex.Ready) yield return null;
                foreach (string name in unknown)
                {
                    ItemSprite(name);
                    if (++n % 4 == 0) yield return null;
                }
            }
            prewarm = null;
            Debug.Log("[MapExtension] log icons ready: " + names.Count + " names (" + unknown.Count + " via item index)");
        }

        private static Sprite ItemSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (spriteByName.TryGetValue(name, out Sprite cached)) return cached;
            string id = null;
            if (!BranchSim.NameToId.TryGetValue(name, out id))
            {
                id = ItemIndex.IdByName(name);
                if (id == null)
                {
                    string bare = Regex.Replace(name, @"\s*\([^()]*\)\s*$", "");
                    if (bare != name) id = BranchSim.NameToId.TryGetValue(bare, out string b2) ? b2 : ItemIndex.IdByName(bare);
                }
            }
            Sprite sp = null;
            if (id != null)
            {
                BranchSim.NameToId[name] = id; // 세이브에 저장 → 다음엔 전체 아이템 목록 없이 바로
                try { sp = ItemIndex.Describe(id).sprite; } catch { }
            }
            if (sp != null || ItemIndex.Ready) spriteByName[name] = sp; // 목록이 다 만들어지기 전엔 못 찾아도 기억하지 않음
            return sp;
        }

        // 기록 한 줄 → 표 한 줄. 예전 형식(소지금 없음)도 읽는다.
        //  " - 아침 자동 채우기: 2개"
        //  " - 하층민 노동자 (소지금 87 ₵): 휴지 ×2 (64 ₵, 이벤트 +35%, 유행), 라이터 (20 ₵)"
        //  " - 하층민 가족 (소지금 140 ₵): 둘러보고 그냥 감 (너무 비쌈: 제일 싼 게 210 ₵)"
        private static LogLine ParseLine(string line)
        {
            if (!line.StartsWith(" - ")) return null;
            string s = line.Substring(3);
            var l = new LogLine();
            if (s.StartsWith("아침 자동 채우기")) { l.Info = true; l.Who = s; return l; }
            Match m = Regex.Match(s, @"^(.+?) \(소지금 (\d+) ₵\): (.*)$");
            string rest;
            if (m.Success) { l.Who = m.Groups[1].Value; l.Wallet = int.Parse(m.Groups[2].Value); rest = m.Groups[3].Value; }
            else
            {
                int c = s.IndexOf(": ", StringComparison.Ordinal);
                if (c < 0) { l.Info = true; l.Who = s; return l; }
                l.Who = s.Substring(0, c);
                rest = s.Substring(c + 2);
            }
            if (rest.StartsWith("둘러보고 그냥 감"))
            {
                Match r = Regex.Match(rest, @"\((.*)\)\s*$");
                l.Note = r.Success ? r.Groups[1].Value : "";
                return l;
            }
            l.Bought = true;
            var items = new List<string>();
            var notes = new List<string>();
            foreach (string seg in Regex.Split(rest, @"\), "))
            {
                Match mi = Regex.Match(seg, @"^(.+?)(?: ×(\d+))? \((\d+) ₵(.*?)\)?$");
                if (!mi.Success) { items.Add(seg); continue; }
                items.Add(mi.Groups[1].Value + (mi.Groups[2].Success ? " ×" + mi.Groups[2].Value : ""));
                l.Parts.Add((mi.Groups[1].Value, mi.Groups[2].Success ? int.Parse(mi.Groups[2].Value) : 1));
                l.Paid += int.Parse(mi.Groups[3].Value);
                string extra = mi.Groups[4].Value.Trim().TrimStart(',').Trim();
                if (extra.Length > 0 && !notes.Contains(extra)) notes.Add(extra);
            }
            l.Items = string.Join(", ", items);
            l.Note = string.Join(" / ", notes);
            return l;
        }

        private static int DayOf(string log)
        {
            Match m = Regex.Match(log, @"\[(\d+)일차\]");
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        // ---- 손님 ----
        private static void RenderVisitors(BranchSite b)
        {
            if (b != null)
            {
                float am = BranchSim.AttractivenessMult(b, out int av);
                status.text = "분점 매력도 " + av + " → 손님 수 ×" + am.ToString("0.0#") + " · 비율은 세력 평판 반영";
            }
            else status.text = "분점 매력도: " + string.Join(", ", Owned.Select(x => x.Name.Replace(" 분점", "") + " " + (x.Attractiveness + BranchRenovation.Bonus(x)))) + " · 비율은 세력 평판 반영";
            bool all = b == null;
            float[] cols = all
                ? new[] { 0f, 0.16f, 0.3f, 0.38f, 0.52f, 0.6f, 1f }
                : new[] { 0f, 0.2f, 0.29f, 0.45f, 0.54f, 1f };
            var heads = all
                ? new[] { "분점", "손님", ">비율", ">소지금", ">평판", "좋아하는 것" }
                : new[] { "손님", ">비율", ">소지금", ">평판", "좋아하는 것" };
            HeaderRow(cols, heads);
            int n = 0;
            foreach (BranchSite site in all ? Owned.ToList() : new List<BranchSite> { b })
            {
                float sum = site.Visitors.Sum(w => BranchSim.Weight(w.type, w.weight));
                foreach (var w in site.Visitors.OrderByDescending(w => BranchSim.Weight(w.type, w.weight)))
                {
                    if (!BranchSim.Types.TryGetValue(w.type, out BranchSim.VisitorType t)) continue;
                    RectTransform row = Row(n++);
                    int k = 0;
                    if (all) Cell(row, cols[k], cols[++k], site.Name.Replace(" 분점", ""));
                    Cell(row, cols[k], cols[++k], t.Name);
                    NumCell(row, cols[k], cols[++k], sum > 0f ? Mathf.RoundToInt(BranchSim.Weight(w.type, w.weight) * 100f / sum) + "%" : "–");
                    NumCell(row, cols[k], cols[++k], N(t.BudgetMin) + "~" + N(t.BudgetMax) + " ₵");
                    float rep = BranchSim.RepMult(t.Faction);
                    NumCell(row, cols[k], cols[++k], "×" + rep.ToString("0.0#"), rep > 1.01f ? Green : rep < 0.99f ? Red : Ink);
                    Cell(row, cols[k], cols[++k], string.Join(", ", t.Likes.Select(TypeKo).Distinct()));
                }
            }
        }

        // ---- 통계 ----
        private static void RenderStats(BranchSite b)
        {
            var sites = b != null ? new List<BranchSite> { b } : Owned.ToList();
            var byDay = new SortedDictionary<int, int>();
            var items = new Dictionary<string, (int qty, int money)>();
            int buyers = 0, visitors = 0;
            foreach (BranchSite site in sites)
                foreach (string log in site.Logs)
                {
                    int day = DayOf(log);
                    Match m = Regex.Match(log, @"매출 (\d+) ₵");
                    int sales = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                    byDay[day] = (byDay.TryGetValue(day, out int v) ? v : 0) + sales;
                    Match mv = Regex.Match(log, @"손님 (\d+)명");
                    if (mv.Success) visitors += int.Parse(mv.Groups[1].Value);
                    Match mb = Regex.Match(log, @"구매 (\d+)명");
                    if (mb.Success) buyers += int.Parse(mb.Groups[1].Value);
                    foreach (string line in log.Split('\n'))
                    {
                        if (!line.StartsWith(" - ") || line.Contains("그냥 감") || line.Contains("자동 채우기")) continue;
                        int cut = line.IndexOf("): ", StringComparison.Ordinal);
                        string part = cut >= 0 && line.Contains("(소지금") ? line.Substring(cut + 3) : line.Substring(line.IndexOf(": ", StringComparison.Ordinal) + 2);
                        foreach (string seg in Regex.Split(part, @"\), "))
                        {
                            Match mi = Regex.Match(seg, @"^(.+?)(?: ×(\d+))? \((\d+) ₵");
                            if (!mi.Success) continue;
                            string name = mi.Groups[1].Value.Trim();
                            int q = mi.Groups[2].Success ? int.Parse(mi.Groups[2].Value) : 1;
                            int paid = int.Parse(mi.Groups[3].Value);
                            items[name] = items.TryGetValue(name, out var cur) ? (cur.qty + q, cur.money + paid) : (q, paid);
                        }
                    }
                }
            int week = byDay.Values.Sum();
            status.text = "최근 7일 매출 " + N(week) + " ₵ · 손님 " + N(visitors) + "명 중 " + N(buyers) + "명 구매"
                          + (visitors > 0 ? " (" + Mathf.RoundToInt(buyers * 100f / visitors) + "%)" : "") + " · 누적 " + N(sites.Sum(x => x.Earned)) + " ₵";
            if (byDay.Count == 0) { Para("아직 기록이 없다."); return; }

            // 날짜별 매출 막대
            Para("<b>날짜별 매출</b>");
            float rowH = baseSize * 1.25f;
            var chart = new GameObject("Chart", typeof(RectTransform));
            chart.transform.SetParent(body, false);
            chart.AddComponent<LayoutElement>().preferredHeight = rowH * 6f;
            var crt = (RectTransform)chart.transform;
            int max = Mathf.Max(1, byDay.Values.Max());
            var days = byDay.Keys.ToList();
            int nd = days.Count;
            for (int i = 0; i < nd; i++)
            {
                float x0 = (float)i / nd + 0.015f, x1 = (float)(i + 1) / nd - 0.015f;
                int v = byDay[days[i]];
                float h = Mathf.Lerp(0.02f, 0.62f, v / (float)max);
                RectTransform bar = Box(crt, "Bar", x0 + 0.02f, 0.82f - h, x1 - 0.02f, 0.82f);
                var img = bar.gameObject.AddComponent<Image>();
                img.color = new Color(Red.r, Red.g, Red.b, 0.75f);
                img.raycastTarget = false;
                Cell(crt, x0, x1, N(v) + " ₵", Ink, 0.82f - h - 0.16f, 0.82f - h, TextAlignmentOptions.Bottom);
                Cell(crt, x0, x1, days[i] + "일차", Ink, 0.84f, 1f, TextAlignmentOptions.Top);
            }

            // 많이 팔린 물건
            Para("\n<b>많이 팔린 물건</b> (최근 7일)");
            if (items.Count == 0) { Para("아직 팔린 물건이 없다."); return; }
            float[] cols = { 0f, 0.08f, 0.55f, 0.72f, 1f };
            int n = 0;
            foreach (var kv in items.OrderByDescending(kv => kv.Value.money).Take(10))
            {
                RectTransform row = Row(n);
                Cell(row, cols[0], cols[1], (++n) + ".");
                Cell(row, cols[1], cols[2], kv.Key);
                NumCell(row, cols[2], cols[3], N(kv.Value.qty) + "개");
                NumCell(row, cols[3], cols[4], N(kv.Value.money) + " ₵");
            }
        }

        // ---- 설정 ----
        private static void RenderSettings(BranchSite b)
        {
            status.text = b != null ? "자동 채우기와 판매 규칙" : "분점별 설정 요약 (바꾸려면 위에서 분점을 고른다)";
            if (b != null) ToolButton(0, "설정 바꾸기", OpenRestockSettings);
            foreach (BranchSite site in b != null ? new List<BranchSite> { b } : Owned.ToList())
            {
                string mode = site.FilterMode == 1 ? "허용 목록 " + site.FilterIds.Count + "개만"
                            : site.FilterMode == 2 ? "손님이 좋아하는 것 (차단 " + site.FilterIds.Count + "개 제외)"
                            : "손님이 좋아하는 것 전부";
                var sb = new System.Text.StringBuilder();
                sb.Append("<b>").Append(site.Name).Append("</b>\n");
                sb.Append(" - 매일 아침 자동 채우기: ").Append(site.AutoRestock ? "켜짐" : "꺼짐").Append('\n');
                sb.Append(" - 채울 물건: ").Append(mode).Append('\n');
                sb.Append(" - 가격 하락 이벤트 중인 물건: ").Append(site.HoldOnPriceDrop ? "안 팖" : "그냥 팖").Append('\n');
                sb.Append(" - 밀수품: 안 팖 (재고에 넣어 둘 수는 있음)");
                if (site.FilterIds.Count > 0)
                {
                    var names = site.FilterIds.Take(12).Select(id => ItemIndex.Describe(id).name);
                    sb.Append("\n - 목록: ").Append(string.Join(", ", names)).Append(site.FilterIds.Count > 12 ? " 외 " + (site.FilterIds.Count - 12) + "개" : "");
                }
                Para(sb.ToString());
            }
        }

        private static void OpenRestockSettings()
        {
            BranchSite b = Site;
            if (b == null) return;
            BranchRestockWindow.Open(b);
        }

        // ---- 이름표 ----
        private static readonly Dictionary<string, string> TypeNames = new Dictionary<string, string>
        {
            { "FOOD", "음식" }, { "FRESH_FOOD", "신선식품" }, { "PROCESSED_FOOD", "가공식품" }, { "WATER", "물" }, { "WATER_RATION", "배급수" },
            { "MEDICAL", "의약품" }, { "SUBSTANCE", "약물" }, { "ALCOHOL", "술" }, { "TREAT", "간식" }, { "BREVAGE", "음료" },
            { "WEAPON", "무기" }, { "FIREARM", "총기" }, { "AMMUNITION", "탄약" }, { "MUNITION", "군수품" }, { "ARMOR", "방어구" },
            { "TOOL", "도구" }, { "MATERIAL", "재료" }, { "BASIC_MATERIAL", "기초 재료" }, { "REFINED_MATERIAL", "정제 재료" },
            { "MODULE", "모듈" }, { "ENERGY_CELL", "에너지 셀" }, { "MACHINE", "기계" }, { "NARCOTIC", "마약" }, { "ORGAN", "장기" },
            { "ART", "예술품" }, { "LUXURY_ITEM", "사치품" }, { "CONTRABAND", "밀수품" },
        };

        // 종류 이름: 게임 번역(TypeHelper.GetTypeDisplayName = mechanic "type_<종류>") 우선, 없으면 내 이름표, 그것도 없으면 "기타"
        private static readonly Dictionary<string, string> typeCache = new Dictionary<string, string>();

        private static string TypeKo(string t)
        {
            if (string.IsNullOrEmpty(t)) return "기타";
            if (typeCache.TryGetValue(t, out string cached)) return cached;
            string name = null;
            try
            {
                string g = TypeHelper.GetTypeDisplayName(t);
                if (!string.IsNullOrEmpty(g) && g.IndexOf("type_", StringComparison.OrdinalIgnoreCase) < 0
                    && g.IndexOf("Translation Error", StringComparison.OrdinalIgnoreCase) < 0 && g != t)
                    name = g;
            }
            catch
            {
            }
            if (name == null) name = TypeNames.TryGetValue(t, out string k) ? k : "기타";
            typeCache[t] = name;
            return name;
        }

        private static string TypeName(GameItem i)
        {
            List<string> types;
            try { types = i.GetGameItemType(); } catch { return "기타"; }
            if (types == null || types.Count == 0) return "기타";
            foreach (string t in types) if (t != "CONTRABAND") return TypeKo(t);
            return TypeKo(types[0]);
        }

        private static readonly Dictionary<string, string> ShortNames = new Dictionary<string, string>
        {
            { "worker", "노동자" }, { "family", "가족" }, { "rebel", "혁명군" }, { "broker", "중개인" }, { "believer", "신자" },
            { "tourist", "관광객" }, { "security", "보안대" }, { "tech", "기술자" }, { "noble", "상류층" }, { "cartel", "카르텔" },
        };

        private static string ShortName(string type) => ShortNames.TryGetValue(type, out string s) ? s : type;

        // ---- UI 조각 ----
        private static void Clear(RectTransform rt)
        {
            if (rt == null) return;
            for (int i = rt.childCount - 1; i >= 0; i--)
            {
                GameObject g = rt.GetChild(i).gameObject;
                g.SetActive(false);
                g.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(g);
            }
        }

        private static void ScrollTop()
        {
            Canvas.ForceUpdateCanvases();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        // 도구 버튼: 오른쪽부터 slot 0, 1, 2
        private static void ToolButton(int slot, string label, Action click)
        {
            float x1 = 1f - slot * 0.34f, x0 = x1 - 0.32f;
            Win98Button.Create(toolbar, label, bodyFont, baseSize * 0.55f, x0, 0.08f, x1, 0.92f, click);
        }

        private static float RowHeight => baseSize * 1.25f;

        private static RectTransform Row(int n)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(body, false);
            go.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            if (n % 2 == 1)
            {
                var img = go.AddComponent<Image>();
                img.color = new Color(Ink.r, Ink.g, Ink.b, 0.07f);
                img.raycastTarget = false;
            }
            return (RectTransform)go.transform;
        }

        private static void HeaderRow(float[] cols, string[] heads)
        {
            for (int i = 0; i < heads.Length && i + 1 < cols.Length; i++)
            {
                if (heads[i].Length == 0) continue;
                bool right = heads[i].StartsWith(">"); // ">" = 숫자 열 (오른쪽 정렬)
                string h = right ? heads[i].Substring(1) : heads[i];
                TextMeshProUGUI t = Cell(header, cols[i], cols[i + 1], "<b>" + h + "</b>", Red, 0f, 1f, right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
                if (right) t.rectTransform.offsetMax = new Vector2(-NumPad, 0f);
            }
        }

        private const float NumPad = 12f; // 숫자 열 오른쪽 여백 (다음 열 글자와 안 붙게)

        // 숫자 열: 오른쪽 정렬
        private static TextMeshProUGUI NumCell(RectTransform row, float x0, float x1, string text, Color? color = null)
        {
            TextMeshProUGUI t = Cell(row, x0, x1, text, color, 0f, 1f, TextAlignmentOptions.MidlineRight);
            t.rectTransform.offsetMax = new Vector2(-NumPad, 0f);
            return t;
        }

        // 천 단위 쉼표
        private static string N(long v) => v.ToString("#,0");

        // 기록 글 속 네 자리 이상 숫자에 쉼표
        private static string Commas(string text) => Regex.Replace(text, @"\d{4,}", m => N(long.Parse(m.Value)));

        private static TextMeshProUGUI Cell(RectTransform row, float x0, float x1, string text, Color? color = null,
                                            float y0 = 0f, float y1 = 1f, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            TextMeshProUGUI t = Text(row, "Cell", bodyFont, color ?? Ink, baseSize * 0.55f, x0, y0, x1, y1);
            t.alignment = align;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableAutoSizing = true;
            t.fontSizeMax = baseSize * 0.55f;
            t.fontSizeMin = baseSize * 0.42f;
            t.rectTransform.offsetMax = new Vector2(-4f, 0f);
            t.text = text;
            return t;
        }

        // 본문에 글 한 덩어리 (줄바꿈, 높이는 글 길이에 맞춤)
        private static void Para(string text)
        {
            var go = new GameObject("Para", typeof(RectTransform));
            go.transform.SetParent(body, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (bodyFont != null) t.font = bodyFont.font;
            t.fontSize = baseSize * 0.55f;
            t.color = Ink;
            t.richText = true;
            t.enableWordWrapping = true;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.raycastTarget = false;
            t.margin = new Vector4(0f, 4f, 4f, 8f);
            t.text = text;
        }

        private static void Icon(RectTransform row, float x0, float x1, GameItem item)
        {
            Sprite sp = null;
            try { sp = (item as GameItemElement)?.ResolveSpriteByName(item.spritePath); } catch { }
            if (sp == null) return;
            RectTransform rt = Box(row, "Icon", x0, 0.08f, x1, 0.92f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        // 부모 안에 비율(0~1, 위가 0)로 빈 칸
        private static RectTransform Box(RectTransform parent, string name, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(x0, 1f - y1);
            rt.anchorMax = new Vector2(x1, 1f - y0);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static void Line(RectTransform parent, float x0, float y0, float x1, float y1, bool vertical = false)
        {
            RectTransform rt = Box(parent, "Line", x0, y0, x1, y1);
            if (vertical) { rt.offsetMin = new Vector2(-1f, 0f); rt.offsetMax = new Vector2(1f, 0f); }
            else { rt.offsetMin = new Vector2(0f, -1f); rt.offsetMax = new Vector2(0f, 1f); }
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(Ink.r, Ink.g, Ink.b, 0.35f);
            img.raycastTarget = false;
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
            // 모니터 바로 위 한 칸 (너무 높이 올리면 게임 오버레이 층보다 위로 가서 끄는 아이템이 창 뒤에 숨는다)
            int order = mc.sortingOrder + 1;
            Canvas c = other.GetComponent<Canvas>() ?? other.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingLayerID = mc.sortingLayerID;
            c.sortingOrder = order;
            if (other.GetComponent<GraphicRaycaster>() == null) other.AddComponent<GraphicRaycaster>();
            LogOrders(mc, oc, c);
            if (!raised.Contains(other)) raised.Add(other);
            RaiseOverlay(c.sortingLayerID, order + 1);
        }

        // 끌고 있는 아이템/툴팁은 게임 오버레이 층(RenderHandler.OverlayCanvas)에 그려진다.
        // 올린 창보다 오버레이가 아래일 때만, 그 창이 열려 있는 동안만 오버레이를 그 위로 올리고, 다 닫히면 원래대로 되돌린다.
        // (0.31.1~0.35.0 은 한 번 올리면 게임 끌 때까지 그대로 → 오버레이가 아이템보다 먼저 마우스를 받아 아이템 끌기가 안 됨)
        // 주의: 오버레이 안에 Canvas 를 끼우면 안 된다 — 우클릭/정렬 메뉴(ItemContextHandler)가 오버레이 루트 캔버스의
        // GraphicRaycaster 로만 클릭을 찾는다 (0.30.5~0.31.0 버그).
        private static readonly List<GameObject> raised = new List<GameObject>();
        private static bool overlayRaised;
        private static int overlayLayer, overlayOrder;
        private static bool loggedOrders;

        private static void LogOrders(Canvas mc, Canvas oc, Canvas w)
        {
            if (loggedOrders) return;
            loggedOrders = true;
            try
            {
                Canvas ov = RenderHandler.current?.OverlayCanvas;
                Debug.Log("[MapExtension] canvas orders: monitor " + mc.sortingLayerName + "/" + mc.sortingOrder
                          + ", window " + (oc != null ? oc.sortingLayerName + "/" + oc.sortingOrder : "-")
                          + ", raised " + w.sortingLayerName + "/" + w.sortingOrder
                          + ", overlay " + (ov != null ? ov.sortingLayerName + "/" + ov.sortingOrder : "-"));
            }
            catch
            {
            }
        }

        private static bool Below(int layerA, int orderA, int layerB, int orderB)
        {
            int va = SortingLayer.GetLayerValueFromID(layerA), vb = SortingLayer.GetLayerValueFromID(layerB);
            return va < vb || (va == vb && orderA < orderB);
        }

        private static void RaiseOverlay(int layer, int minOrder)
        {
            try
            {
                GameObject ov = RenderHandler.current?.overlayRoot?.gameObject;
                if (ov != null)
                {
                    Canvas own = ov.GetComponent<Canvas>();
                    if (own != null && !own.isRootCanvas)
                    {
                        GraphicRaycaster gr = ov.GetComponent<GraphicRaycaster>();
                        if (gr != null) UnityEngine.Object.Destroy(gr);
                        UnityEngine.Object.Destroy(own);
                    }
                }
                Canvas root = RenderHandler.current?.OverlayCanvas;
                if (root == null) return;
                if (!overlayRaised && !Below(root.sortingLayerID, root.sortingOrder, layer, minOrder)) return; // 이미 위
                if (!overlayRaised)
                {
                    overlayLayer = root.sortingLayerID;
                    overlayOrder = root.sortingOrder;
                    overlayRaised = true;
                }
                root.sortingLayerID = layer;
                root.sortingOrder = Mathf.Max(root.sortingOrder, minOrder);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] overlay raise: " + ex.Message);
            }
        }

        // 매 프레임 (BranchPanel.Update): 올린 창이 다 닫혔으면 오버레이 순서를 원래대로
        internal static void TickOverlay()
        {
            if (!overlayRaised) return;
            raised.RemoveAll(g => g == null || !g.activeInHierarchy);
            if (raised.Count > 0) return;
            try
            {
                Canvas root = RenderHandler.current?.OverlayCanvas;
                if (root != null)
                {
                    root.sortingLayerID = overlayLayer;
                    root.sortingOrder = overlayOrder;
                }
            }
            catch
            {
            }
            overlayRaised = false;
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
            Render();
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

    // 윈도우 98 스타일 버튼: 회색 면 + 2단 입체 테두리 (왼쪽 위 흰/밝은 회색, 오른쪽 아래 검정/어두운 회색).
    // 누르는 동안과 Latched(선택된 메뉴)일 때는 테두리를 뒤집어 들어간 모양 + 글자 1칸 아래로.
    internal class Win98Button : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler,
        UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        private static readonly Color Face = new Color32(0xa8, 0xae, 0xa5, 0xff);   // 회녹색 (사용자 지정)
        private static readonly Color FaceOn = new Color32(0xc4, 0xca, 0xc1, 0xff); // 선택된 메뉴: 같은 색 조금 밝게
        private static readonly Color White = new Color32(0xff, 0xff, 0xff, 0xff);
        private static readonly Color Light = new Color32(0xdf, 0xdf, 0xdf, 0xff);
        private static readonly Color Dark = new Color32(0x80, 0x80, 0x80, 0xff);
        private static readonly Color Black = new Color32(0x0a, 0x0a, 0x0a, 0xff);
        private const float B = 1.5f; // 테두리 한 줄 두께 (캔버스 단위)

        private Image outerBR, outerTL, innerBR, innerTL, face;
        private TextMeshProUGUI label;
        private Vector2 labelPos;
        private bool down, latched;

        internal bool Latched
        {
            get => latched;
            set { latched = value; Paint(); }
        }

        internal static Win98Button Create(RectTransform parent, string text, TMP_Text font, float size,
                                           float x0, float y0, float x1, float y1, Action click)
        {
            var go = new GameObject("W98_" + text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(x0, 1f - y1);
            rt.anchorMax = new Vector2(x1, 1f - y0);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var w = go.AddComponent<Win98Button>();
            w.outerBR = go.AddComponent<Image>();               // 버튼 클릭을 받는 판
            w.outerTL = w.Layer("OuterTL", new Vector2(0f, B), new Vector2(-B, 0f));
            w.innerBR = w.Layer("InnerBR", new Vector2(B, B), new Vector2(-B, -B));
            w.innerTL = w.Layer("InnerTL", new Vector2(B, 2f * B), new Vector2(-2f * B, -B));
            w.face = w.Layer("Face", new Vector2(2f * B, 2f * B), new Vector2(-2f * B, -2f * B));

            var tgo = new GameObject("Label", typeof(RectTransform));
            tgo.transform.SetParent(rt, false);
            var t = tgo.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font.font;
            t.text = text;
            t.color = Black;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = size * 0.6f;
            t.raycastTarget = false;
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(4f * B, 2f * B);
            trt.offsetMax = new Vector2(-4f * B, -2f * B);
            w.label = t;
            w.labelPos = trt.anchoredPosition;

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = w.outerBR;
            btn.onClick.AddListener(() => click());
            w.Paint();
            return w;
        }

        private Image Layer(string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = min;
            rt.offsetMax = max;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private void Paint()
        {
            if (face == null) return;
            bool sunk = down || latched;
            outerBR.color = sunk ? White : Black;
            outerTL.color = sunk ? Black : White;
            innerBR.color = sunk ? Light : Dark;
            innerTL.color = sunk ? Dark : Light;
            face.color = latched ? FaceOn : Face;
            label.fontStyle = latched ? FontStyles.Bold : FontStyles.Normal;
            label.rectTransform.anchoredPosition = labelPos + (sunk ? new Vector2(B, -B) : Vector2.zero);
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { down = true; Paint(); }
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { down = false; Paint(); }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (down) { down = false; Paint(); } }
    }

    // 모니터 안 작은 이름 툴팁 (아이템 그림에 마우스를 올리면 마우스 옆에).
    // 게임 툴팁(OverlayHandler)은 모니터 뒤 아이템 툴팁을 막는 처리와 엉키므로 모니터 안에 따로 그린다.
    internal class HoverName : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        internal string Text;
        private static RectTransform tip;
        private static TextMeshProUGUI tipText;
        private static HoverName owner;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
        {
            owner = this;
            Ensure();
            if (tip == null) return;
            tipText.text = Text;
            tip.gameObject.SetActive(true);
            tip.SetAsLastSibling();
            Follow();
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => Hide(this);

        private void OnDisable() => Hide(this);

        private void Update()
        {
            if (owner == this) Follow();
        }

        private static void Hide(HoverName who)
        {
            if (owner != who) return;
            owner = null;
            if (tip != null) tip.gameObject.SetActive(false);
        }

        // 툴팁 판은 이 그림이 든 창(가장 위 Canvas 아래 모니터 루트)에 하나만 만든다
        private void Ensure()
        {
            Transform root = transform;
            while (root.parent != null && root.parent.GetComponent<Canvas>() == null) root = root.parent;
            if (tip != null && tip.parent == root) return;
            if (tip != null) Destroy(tip.gameObject);
            var go = new GameObject("VME_HoverName", typeof(RectTransform));
            go.transform.SetParent(root, false);
            tip = (RectTransform)go.transform;
            tip.anchorMin = tip.anchorMax = new Vector2(0.5f, 0.5f);
            tip.pivot = new Vector2(0f, 0f);
            var bg = go.AddComponent<Image>();
            bg.color = new Color32(0x1f, 0x1a, 0x24, 0xee);
            bg.raycastTarget = false;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var hl = go.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(8, 8, 4, 4);
            hl.childControlWidth = hl.childControlHeight = true;
            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(tip, false);
            tipText = tgo.AddComponent<TextMeshProUGUI>();
            TMP_Text style = GetComponentInParent<Canvas>()?.GetComponentInChildren<TMP_Text>(true);
            if (style != null) tipText.font = style.font;
            tipText.fontSize = 16f;
            tipText.color = new Color32(0xf2, 0xee, 0xe6, 0xff);
            tipText.enableWordWrapping = false;
            tipText.raycastTarget = false;
            // 자기 Canvas 로 맨 위에 (분점 버튼처럼 가게 물건 뒤 층에 있어도 툴팁은 가려지지 않게. 클릭은 안 받음)
            Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            var own = go.AddComponent<Canvas>();
            own.overrideSorting = true;
            if (rootCanvas != null) { own.sortingLayerID = rootCanvas.sortingLayerID; own.sortingOrder = rootCanvas.sortingOrder + 100; }
            go.SetActive(false);
        }

        private static void Follow()
        {
            if (tip == null) return;
            var parent = (RectTransform)tip.parent;
            Canvas c = parent.GetComponentInParent<Canvas>()?.rootCanvas;
            Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Input.mousePosition, cam, out Vector2 p))
                tip.anchoredPosition = p - parent.rect.center + new Vector2(14f, 10f);
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
