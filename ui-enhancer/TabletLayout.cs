using System.Collections.Generic;
using UnityEngine;

namespace UiEnhancer
{
    // 거래창 2단 배치 (거래창 안 좌표: 왼쪽 위 기준, x 오른쪽 / y 아래로 +, 숫자는 Settings.cs)
    //  왼쪽 칸: 손님 이름 / 부제 / 관심사     오른쪽 칸: 거래 목록 + 구분선 + 금액 줄     아래 가운데: 버튼 줄
    // 게임은 목록을 고칠 때마다 이 부품들을 원래 자리로 다시 놓으므로 매 프레임(게임 뒤에) 같은 값으로 덮어쓴다.
    // 원래 자리는 처음 한 번 기억해 뒀다가 거래창이 닫히면 돌려놓는다 (Unplace).
    internal partial class ClientWindow
    {
        private readonly Dictionary<RectTransform, (Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)> placed
            = new Dictionary<RectTransform, (Vector2, Vector2, Vector2, Vector2, Vector2)>();

        private void Remember(RectTransform r)
        {
            if (!placed.ContainsKey(r)) placed[r] = (r.anchorMin, r.anchorMax, r.pivot, r.anchoredPosition, r.sizeDelta);
        }

        // r 을 부모 왼쪽 위 기준 (x, y) 에 w x h 로. remember=false: 게임이 매번 새로 만드는 부품 (원래 자리 기억 안 함)
        private void Place(RectTransform r, float x, float y, float w, float h, bool remember = true)
        {
            if (r == null) return;
            if (remember) Remember(r);
            Vector2 tl = new Vector2(0f, 1f);
            if (r.anchorMin != tl) r.anchorMin = tl;
            if (r.anchorMax != tl) r.anchorMax = tl;
            if (r.pivot != tl) r.pivot = tl;
            var pos = new Vector2(x, -y);
            var size = new Vector2(w, h);
            if (r.anchoredPosition != pos) r.anchoredPosition = pos;
            if (r.sizeDelta != size) r.sizeDelta = size;
        }

        private void LayoutColumns(RectTransform root, NegotiationTabletFrame f, bool full)
        {
            if (f == null) return;
            Remember(root);
            var rootSize = new Vector2(TabletW, TabletH);
            if (root.sizeDelta != rootSize) root.sizeDelta = rootSize;

            // 왼쪽 칸
            Place(f.clientName != null ? f.clientName.rectTransform : null, LeftX, TopY, LeftW, 40f);
            Place(f.clientSubtitle != null ? f.clientSubtitle.rectTransform : null, LeftX, TopY + 48f, LeftW, 50f);
            if (f.clientSubtitle != null) f.clientSubtitle.enableWordWrapping = true;

            // 오른쪽 칸 = 게임 "구매/판매" 칸(Section) 통째로. 안쪽 목록/구분선/금액 줄은 칸 기준 좌표
            Place(f.section, RightX, TopY, RightW, ColumnH);
            // 목록은 스크롤 막대 자리만큼 좁히고, 막대는 칸 오른쪽 끝에 붙인다 (원래는 칸 밖 오른쪽에 떠 있음)
            float sbW = ScrollbarWidth(f);
            float listW = RightW - sbW - ScrollbarGap;
            Place(f.body, 0f, 0f, listW, ListH);
            PlaceScrollbar(f, sbW);
            if (full) WidenRows(f, listW);
            Place(f.separatorBottom != null ? f.separatorBottom.rectTransform : null, 0f, ListH + 8f, RightW, 2f);
            Place(f.footer, 0f, ListH + 22f, RightW, FooterH);
            // 관심사 줄(Section 안에 있음): 왼쪽 칸 이름/부제 아래로 (Section 기준이라 x 는 LeftX - RightX = 음수)
            Place(f.infoHost, LeftX - RightX, InfoY, LeftW, InfoH);
            LayoutInfo(f.infoHost);

            // 버튼 줄: 거래창 가운데
            Place(f.buttonHost, (TabletW - ButtonsW) * 0.5f, ButtonsY, ButtonsW, 42f);
        }

        // 관심사/제한 줄: 게임은 "라벨  태그 태그" 한 줄. 왼쪽 칸이 좁으니 라벨 아래에 태그를 칸 너비로 줄바꿈해 놓는다
        //   관심사
        //   [식품] [의약품] [공구]
        //   [잡화]
        // 게임이 만드는 순서: InfoLabel, Tag, Tag, ... (다음 묶음 InfoLabel, Tag ...). 흥정 문구는 InfoValue
        private void LayoutInfo(RectTransform host)
        {
            if (host == null) return;
            float y = 0f, bottom = 0f, x = 0f;
            bool first = true;
            foreach (Transform child in host)
            {
                var r = child as RectTransform;
                if (r == null || !child.gameObject.activeSelf || child.name == "Probe") continue; // Probe = 게임이 태그 너비 재고 지우는 임시 부품
                if (child.name == "InfoLabel" || child.name == "InfoValue")
                {
                    y = first ? 0f : bottom + InfoGroupGap;
                    first = false;
                    float h = 23f;
                    if (child.name == "InfoValue")
                    {
                        var t = child.GetComponent<TMPro.TMP_Text>();
                        if (t != null) { t.enableWordWrapping = true; h = Mathf.Max(23f, t.GetPreferredValues(t.text, LeftW, 0f).y); }
                    }
                    if (child.name == "InfoLabel") { ScaleText(child.GetComponent<TMPro.TMP_Text>(), InfoLabelScale); h = 23f * InfoLabelScale; }
                    Place(r, 0f, y, LeftW, h, false);
                    bottom = y + h;
                    y = bottom + InfoLabelGap; // 태그 첫 줄
                    x = 0f;
                }
                else if (child.name == "Tag")
                {
                    // 태그 통째로 배율 (판, 글자, 아이콘이 같이 커짐). 크기는 게임 값 그대로, 자리 계산만 배율만큼
                    var sc = new Vector3(InfoTagScale, InfoTagScale, 1f);
                    if (r.localScale != sc) r.localScale = sc;
                    float w = r.sizeDelta.x * InfoTagScale, h = 23f * InfoTagScale;
                    if (x > 0f && x + w > LeftW) { x = 0f; y += h + InfoLineGap; }
                    Place(r, x, y, r.sizeDelta.x, 23f, false);
                    x += w + InfoTagGap;
                    bottom = y + h;
                }
            }
        }

        // 목록 줄 너비: 게임은 줄/이름 칸/오른쪽 숫자/구분선/마우스 상자를 전부 폭 502 고정 좌표로 만든다 → 목록 너비 w 로 늘린다
        //  - 폭 502 인 부품 → w,  오른쪽 숫자("Right") → 오른쪽 끝 기준으로 밀기,  이름("Name") → 늘어난 만큼 넓게
        //  - 소제목 줄의 아이콘 → 가운데 기준이라 절반만 밀기
        // 원래 x/폭은 처음 본 값을 기억 (게임이 줄을 새로 만들면 새 부품이라 다시 기억)
        private const float GameRowW = 502f;
        private bool loggedRows, loggedIndent;
        private readonly Dictionary<RectTransform, Vector2> rowOrig = new Dictionary<RectTransform, Vector2>(); // (x, 폭)

        private void WidenRows(NegotiationTabletFrame f, float w)
        {
            if (f.content == null) return;
            if (rowOrig.Count > 3000) { var dead = new List<RectTransform>(); foreach (var k in rowOrig.Keys) if (k == null) dead.Add(k); foreach (var k in dead) rowOrig.Remove(k); }
            // 보이는 영역(viewport, 넘치면 자르는 곳): 목록 칸에 꽉 채우고 자르는 여백도 없앤다
            RectTransform vp = f.scroll != null ? f.scroll.viewport : null;
            if (vp != null && vp != f.body)
            {
                Remember(vp);
                if (vp.anchorMin != Vector2.zero) vp.anchorMin = Vector2.zero;
                if (vp.anchorMax != Vector2.one) vp.anchorMax = Vector2.one;
                if (vp.offsetMin != Vector2.zero) vp.offsetMin = Vector2.zero;
                if (vp.offsetMax != Vector2.zero) vp.offsetMax = Vector2.zero;
            }
            var mask = vp != null ? vp.GetComponent<UnityEngine.UI.RectMask2D>() : null;
            if (mask != null && mask.padding != Vector4.zero) mask.padding = Vector4.zero;
            if (!loggedRows && vp != null)
            {
                loggedRows = true;
                Debug.Log("[UiEnhancer] list: w=" + w + " body=" + f.body.rect.width + " viewport=" + vp.rect.width + " (" + vp.name + ", parent " + vp.parent.name + ") content=" + f.content.rect.width
                    + " mask=" + (mask != null ? mask.padding.ToString() : "none") + " scrollbar=" + scrollbarW);
            }
            // 줄 폭: 목록 안쪽 내용(content)이 보이는 영역 왼쪽에서 들여 놓인 만큼 빼고, RowInset 만큼 더 좁게
            // (안 그러면 줄 오른쪽 끝, 마우스 상자 테두리가 보이는 영역 밖으로 나가 잘린다)
            // 줄이 실제로 그려지는 왼쪽 위치를 직접 잰다 (content 안 정렬 설정이 줄을 밀 수 있음)
            float indent = 0f;
            if (vp != null)
            {
                foreach (Transform c in f.content)
                {
                    var cr = c as RectTransform;
                    if (cr == null || !c.gameObject.activeSelf) continue;
                    Vector3 cl = vp.InverseTransformPoint(cr.TransformPoint(new Vector3(cr.rect.xMin, 0f, 0f)));
                    indent = Mathf.Max(indent, cl.x - vp.rect.xMin);
                }
            }
            float rowW = w - indent - RowInset;
            float extra = rowW - GameRowW;
            if (!loggedIndent && f.content.childCount > 0)
            {
                loggedIndent = true;
                var first = (RectTransform)f.content.GetChild(0);
                string comps = "";
                foreach (Component comp in f.content.GetComponents<Component>()) comps += comp.GetType().Name + " ";
                Debug.Log("[UiEnhancer] list indent=" + indent + " rowW=" + rowW + " content comps: " + comps
                    + "| first row " + first.name + " pos=" + first.anchoredPosition + " size=" + first.sizeDelta + " aMin=" + first.anchorMin + " aMax=" + first.anchorMax + " pivot=" + first.pivot
                    + " | content pos=" + f.content.anchoredPosition + " size=" + f.content.sizeDelta + " aMin=" + f.content.anchorMin + " aMax=" + f.content.anchorMax + " pivot=" + f.content.pivot);
            }
            WidenOne(f.content, w - GameRowW, w, false);
            foreach (RectTransform r in f.content.GetComponentsInChildren<RectTransform>(true))
            {
                if (r == f.content) continue;
                bool inSubHeader = r.parent != null && r.parent.name == "SubHeader";
                WidenOne(r, extra, rowW, inSubHeader);
            }
        }

        private void WidenOne(RectTransform r, float extra, float w, bool inSubHeader)
        {
            if (r == null || r.anchorMin != r.anchorMax) return; // 늘어나는 앵커(부모 따라감)는 건드리지 않음
            if (!rowOrig.TryGetValue(r, out Vector2 o)) { o = new Vector2(r.anchoredPosition.x, r.sizeDelta.x); rowOrig[r] = o; }
            Vector2 pos = r.anchoredPosition, size = r.sizeDelta;
            if (Mathf.Abs(o.y - GameRowW) < 0.5f) size.x = w;
            else if (r.name == "Right") pos.x = o.x + extra - RowRightPad;
            else if (r.name == "Name") size.x = o.y + extra;
            else if (inSubHeader) pos.x = o.x + extra * 0.5f;
            if (r.anchoredPosition != pos) r.anchoredPosition = pos;
            if (r.sizeDelta != size) r.sizeDelta = size;
        }

        private float scrollbarW = -1f; // 게임 원래 스크롤 막대 너비 (처음 한 번 잰다)

        private float ScrollbarWidth(NegotiationTabletFrame f)
        {
            var sb = f.scrollbar != null ? f.scrollbar.transform as RectTransform : null;
            if (sb == null) return 0f;
            if (scrollbarW < 0f) scrollbarW = sb.rect.width;
            return scrollbarW;
        }

        // 스크롤 막대: 오른쪽 칸 오른쪽 끝, 목록과 같은 높이. 막대의 부모가 무엇이든 칸 좌표를 그 부모 좌표로 바꿔서 놓는다
        private void PlaceScrollbar(NegotiationTabletFrame f, float w)
        {
            var sb = f.scrollbar != null ? f.scrollbar.transform as RectTransform : null;
            var parent = sb != null ? sb.parent as RectTransform : null;
            if (sb == null || parent == null || f.section == null) return;
            // 칸 안 (RightW - w, 0) 지점 = 막대 왼쪽 위
            Vector3 world = f.section.TransformPoint(new Vector3(f.section.rect.xMin + RightW - w, f.section.rect.yMax, 0f));
            Vector3 local = parent.InverseTransformPoint(world);
            float x = local.x - parent.rect.xMin, y = parent.rect.yMax - local.y;
            Place(sb, x, y, w, ListH);
        }

        private void Unplace()
        {
            foreach (var kv in placed)
            {
                if (kv.Key == null) continue;
                var o = kv.Value;
                kv.Key.anchorMin = o.aMin; kv.Key.anchorMax = o.aMax; kv.Key.pivot = o.pivot;
                kv.Key.anchoredPosition = o.pos; kv.Key.sizeDelta = o.size;
            }
            placed.Clear();
            // 목록 줄은 거래창을 닫아도 그대로 남아 다음에 다시 쓰인다 → 원래 x/폭으로 돌려놔야 다음에 또 밀리지 않는다
            foreach (var kv in rowOrig)
            {
                if (kv.Key == null) continue;
                var r = kv.Key;
                r.anchoredPosition = new Vector2(kv.Value.x, r.anchoredPosition.y);
                r.sizeDelta = new Vector2(kv.Value.y, r.sizeDelta.y);
            }
            rowOrig.Clear();
        }
    }
}
