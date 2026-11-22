using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UiEnhancer
{
    // 거래창 모양 (민트 + 흰색, 색/배율은 Settings.cs)
    //  - Restyle: 베이지 배경판은 투명, 진한 글자는 흰색 (새로 생긴 줄/글자도 매 프레임)
    //  - StyleTablet: 부품별 색/글꼴/크기, 숨길 부품
    // 바꾼 값은 원래 값을 처음 한 번 기억해 뒀다가 거래창이 닫히면 돌려놓는다 (Unstyle).
    internal partial class ClientWindow
    {
        private struct TextStyle { public TMP_FontAsset font; public float size; public TextAlignmentOptions align; public bool auto; }

        private readonly Dictionary<Graphic, Color> restyled = new Dictionary<Graphic, Color>();
        private readonly Dictionary<TMP_Text, TextStyle> textStyle = new Dictionary<TMP_Text, TextStyle>();
        private readonly Dictionary<TMP_Text, float> baseSize = new Dictionary<TMP_Text, float>();
        private readonly Dictionary<Transform, Vector3> hiddenObjects = new Dictionary<Transform, Vector3>();

        // 베이지 배경판 → 투명 (클릭 판정은 그대로), 진한 잉크색 글자 → 흰색 (색 있는 글자는 그대로)
        private void Restyle(RectTransform root)
        {
            foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null || restyled.ContainsKey(g)) continue;
                Color c = g.color;
                if (g is TMP_Text)
                {
                    if (Luma(c) < 0.45f) { restyled[g] = c; g.color = new Color(1f, 1f, 1f, c.a); }
                }
                else if (IsBeige(c))
                {
                    restyled[g] = c; g.color = new Color(c.r, c.g, c.b, 0f);
                }
            }
        }

        private void StyleTablet(NegotiationTabletFrame f, bool full)
        {
            if (f == null) return;
            TMP_FontAsset pixel = f.sectionHeader != null ? f.sectionHeader.font : null; // 게임 픽셀 제목 글꼴

            // 손님 이름: 민트, 크게 / 부제(유인 요인 등): 흐린 흰색
            SetText(f.clientName, Mint, pixel, NameTextScale, TextAlignmentOptions.TopLeft);
            SetText(f.clientSubtitle, SubtitleColor, null, 1f, TextAlignmentOptions.TopLeft);

            // 오른쪽 위 버튼(크기, 흥정 말풍선, 닫기): 숨김. 닫기는 Esc / Ctrl+W 로
            HideObject(f.resizeButton != null ? f.resizeButton.gameObject : null);
            HideObject(f.bargainButton != null ? f.bargainButton.gameObject : null);
            HideObject(f.closeButton != null ? f.closeButton.gameObject : null);

            // 칸 머리("판매"/"구매") 판과 글자, 위 구분선, 바탕 화살표, 칸 테두리: 숨김
            Hide(f.sectionHeaderPlate);
            Hide(f.sectionHeader);
            Hide(f.separatorTop);
            Hide(f.arrowsWatermark);
            Hide(f.frame);
            // 목록과 금액 줄 사이 선, 스크롤 막대
            SetColor(f.separatorBottom, BottomLineColor);
            if (f.scrollHandle != null) SetColor(f.scrollHandle, ScrollbarColor);

            // 금액 줄: 점선 숨김, 글자 흰색, "합계" 금액은 민트 큰 픽셀 숫자 (게임이 빨갛게 한 금액은 그대로)
            if (f.footer != null)
            {
                float totalSize = 0f; // 합계 숫자 크기 (손님 예산 숫자도 같은 글꼴, 같은 크기로)
                TMP_Text budget = null;
                foreach (Transform row in f.footer)
                {
                    if (row.name != "FooterRow") continue;
                    TMP_Text label = row.Find("Label")?.GetComponent<TMP_Text>();
                    TMP_Text value = row.Find("Value")?.GetComponent<TMP_Text>();
                    Hide(row.Find("Dots")?.GetComponent<Graphic>());
                    bool total = label != null && label.text != null && label.text.Contains("합계");
                    bool red = value != null && (restyled.TryGetValue(value, out Color ov) ? IsRed(ov) : IsRed(value.color));
                    SetText(label, SoftWhite);
                    if (total) { SetText(value, red ? value.color : Mint, pixel, TotalTextScale); if (value != null) totalSize = value.fontSize; }
                    else SetText(value, red ? value.color : SoftWhite);
                    if (label != null && label.text != null && label.text.Contains("예산")) budget = value;
                }
                if (budget != null && totalSize > 0f)
                {
                    if (pixel != null && budget.font != pixel) budget.font = pixel; // 합계와 같은 픽셀 글꼴
                    budget.enableAutoSizing = false;
                    if (Mathf.Abs(budget.fontSize - totalSize) > 0.01f) budget.fontSize = totalSize;
                }
            }

            // 목록 안: 소제목 줄(도매 할인 제안 등) 판/글자, 줄에 마우스 올렸을 때 상자(Frame/Border, Bar), 줄 사이 선
            if (full && f.content != null)
            {
                foreach (Transform row in f.content)
                {
                    if (row.name == "SubHeader")
                    {
                        SetColor(row.Find("Plate")?.GetComponent<Graphic>(), SubHeaderPlateColor);
                        SetColor(row.Find("Text")?.GetComponent<TMP_Text>(), Mint);
                        continue;
                    }
                    Transform frame = row.Find("Frame");
                    if (frame != null)
                    {
                        SetColor(frame.Find("Border")?.GetComponent<Graphic>(), Mint);
                        SetColor(frame.Find("Bar")?.GetComponent<Graphic>(), Mint);
                    }
                    Transform sep = row.Find("Visuals/Separator") ?? row.Find("Separator");
                    if (sep != null) SetColor(sep.GetComponent<Graphic>(), RowLineColor);
                }
                // 목록 글자 크기
                if (Mathf.Abs(ListTextScale - 1f) > 0.001f)
                    foreach (TMP_Text t in f.content.GetComponentsInChildren<TMP_Text>(true))
                        ScaleText(t, ListTextScale);
            }

            // 큰 버튼: 바탕 투명, 이중 테두리·글자 민트, 마우스 올리면 옅은 민트
            if (f.buttonHost != null)
            {
                foreach (TabletButton b in f.buttonHost.GetComponentsInChildren<TabletButton>(true))
                {
                    bool hover = false;
                    try { hover = Traverse.Create(b).Field("hovered").GetValue() is bool h && h && b.Interactable; } catch { }
                    Hide(b.background);
                    SetColor(b.face, hover ? ButtonHoverColor : new Color(0f, 0f, 0f, 0f));
                    SetColor(b.outer, Mint);
                    SetColor(b.inner, Mint);
                    SetText(b.label, Mint, pixel);
                }
            }
        }

        // ---- 도우미 (원래 값은 처음 한 번 기억) ----

        private void SetColor(Graphic g, Color c)
        {
            if (g == null) return;
            if (!restyled.ContainsKey(g)) restyled[g] = g.color;
            if (g.color != c) g.color = c;
        }

        // 원래 색 그대로 투명하게
        private void Hide(Graphic g)
        {
            if (g == null) return;
            Color c = restyled.TryGetValue(g, out Color o) ? o : g.color;
            SetColor(g, new Color(c.r, c.g, c.b, 0f));
        }

        private void RememberText(TMP_Text t)
        {
            if (!textStyle.ContainsKey(t))
                textStyle[t] = new TextStyle { font = t.font, size = t.fontSize, align = t.alignment, auto = t.enableAutoSizing };
        }

        // 색 + (글꼴, 원래 크기 × sizeMul, 정렬)
        private void SetText(TMP_Text t, Color c, TMP_FontAsset font = null, float sizeMul = 1f, TextAlignmentOptions? align = null)
        {
            if (t == null) return;
            RememberText(t);
            TextStyle o = textStyle[t];
            if (font != null && t.font != font) t.font = font;
            if (Mathf.Abs(sizeMul - 1f) > 0.001f)
            {
                t.enableAutoSizing = false;
                float size = o.size * sizeMul;
                if (Mathf.Abs(t.fontSize - size) > 0.01f) t.fontSize = size;
            }
            if (align.HasValue && t.alignment != align.Value) t.alignment = align.Value;
            SetColor(t, c);
        }

        // 글자 크기만 원래 크기 × mul. 칸에 맞춰 자동으로 줄이기(물건 이름/가격)는 끄고 직접 정한다 (안 그러면 칸에 막혀 안 커짐)
        private void ScaleText(TMP_Text t, float mul)
        {
            if (t == null) return;
            RememberText(t);
            TextStyle o = textStyle[t];
            if (!baseSize.ContainsKey(t)) baseSize[t] = o.auto ? t.fontSizeMax : o.size;
            float size = baseSize[t] * mul;
            if (t.enableAutoSizing) t.enableAutoSizing = false;
            if (Mathf.Abs(t.fontSize - size) > 0.01f) t.fontSize = size;
            if (t.overflowMode != TextOverflowModes.Overflow) t.overflowMode = TextOverflowModes.Overflow;
        }

        // 숨기기 = 크기 0 (게임이 버튼을 다시 켜도 안 보이고 안 눌림)
        private void HideObject(GameObject go)
        {
            if (go == null) return;
            Transform t = go.transform;
            if (!hiddenObjects.ContainsKey(t)) hiddenObjects[t] = t.localScale;
            if (t.localScale != Vector3.zero) t.localScale = Vector3.zero;
        }

        // 거래창이 닫힐 때: 배치/색/글자/숨긴 부품 전부 원래대로
        private void Unstyle()
        {
            Unplace();
            foreach (var kv in hiddenObjects)
                if (kv.Key != null) kv.Key.localScale = kv.Value;
            hiddenObjects.Clear();
            foreach (var kv in restyled)
                if (kv.Key != null) kv.Key.color = kv.Value;
            restyled.Clear();
            foreach (var kv in textStyle)
            {
                if (kv.Key == null) continue;
                kv.Key.font = kv.Value.font;
                kv.Key.fontSize = kv.Value.size;
                kv.Key.alignment = kv.Value.align;
                kv.Key.enableAutoSizing = kv.Value.auto;
            }
            textStyle.Clear();
            baseSize.Clear();
        }

        private static bool IsRed(Color c) => c.r > 0.6f && c.g < 0.45f && c.b < 0.45f;

        private static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        // 거래창 종이색 (밝은 노란 베이지). 흰색(아이템 그림 등)은 제외
        private static bool IsBeige(Color c)
            => c.a > 0.3f && c.r > 0.65f && c.g > 0.6f && c.b < c.r * 0.88f && c.b > 0.35f && c.r - c.b > 0.12f;
    }
}
