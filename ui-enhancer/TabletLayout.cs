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

        // r 을 부모 왼쪽 위 기준 (x, y) 에 w x h 로
        private void Place(RectTransform r, float x, float y, float w, float h)
        {
            if (r == null) return;
            Remember(r);
            Vector2 tl = new Vector2(0f, 1f);
            if (r.anchorMin != tl) r.anchorMin = tl;
            if (r.anchorMax != tl) r.anchorMax = tl;
            if (r.pivot != tl) r.pivot = tl;
            var pos = new Vector2(x, -y);
            var size = new Vector2(w, h);
            if (r.anchoredPosition != pos) r.anchoredPosition = pos;
            if (r.sizeDelta != size) r.sizeDelta = size;
        }

        private void LayoutColumns(RectTransform root, NegotiationTabletFrame f)
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
            Place(f.section, RightX, TopY, RightW, ListH + FooterH + 30f);
            Place(f.body, 0f, 0f, RightW, ListH);
            Place(f.separatorBottom != null ? f.separatorBottom.rectTransform : null, 0f, ListH + 8f, RightW, 2f);
            Place(f.footer, 0f, ListH + 22f, RightW, FooterH);
            // 관심사 줄(Section 안에 있음): 왼쪽 칸 이름/부제 아래로 (Section 기준이라 왼쪽으로 빼는 만큼 음수)
            Place(f.infoHost, LeftX - RightX - 24f, 110f, LeftW + 24f, 40f);

            // 버튼 줄: 거래창 가운데
            Place(f.buttonHost, (TabletW - ButtonsW) * 0.5f, ButtonsY, ButtonsW, 42f);
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
        }
    }
}
