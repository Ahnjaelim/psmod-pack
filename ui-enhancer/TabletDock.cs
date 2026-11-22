using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace UiEnhancer
{
    // 레이어 2: 게임 거래창(NegotiationTablet)을 윈도우가 "다 내려온 자리"에 고정하고 배율을 맞춘다.
    // 윈도우 애니메이션과 상관없이 고정: 거래창 위쪽 = 윈도우 위쪽 + TabletTopOffset, 좌우 가운데.
    // 배율은 윈도우 회색 칸 높이 / 게임 원래 거래창 높이(FitRefH) × TabletScale.
    // 거래창이 닫히면 원래 위치/크기/모양으로 전부 돌려놓는다 (Undock).
    internal partial class ClientWindow
    {
        private RectTransform docked;
        private Vector3 savedScale;
        private Vector2 savedPos;
        private Image hiddenArt;

        // 게임 거래창 부품 묶음 (이름, 목록, 금액 줄, 버튼 등)
        private static NegotiationTabletFrame Frame()
            => Traverse.Create(NegociationUIManager.Instance).Field("frame").GetValue() as NegotiationTabletFrame;

        private static bool dirty = true;
        private float nextFullPass;
        private const float FullPassInterval = 0.25f; // 초. 게임이 알리지 않고 바꾼 부분도 이 간격 안에 따라잡는다

        internal static void MarkDirty() => dirty = true;

        internal static bool IsInfoHost(RectTransform r)
        {
            try { return NegociationUIManager.Instance != null && Frame()?.infoHost == r; } catch { return false; }
        }

        private void DockTablet()
        {
            GameObject ui = NegociationUIManager.Instance != null ? NegociationUIManager.Instance.uiObject : null;
            if (ui == null || !ui.activeInHierarchy) { Undock(); return; }
            var prt = (RectTransform)ui.transform;
            if (docked != prt)
            {
                Undock();
                docked = prt;
                dirty = true;
                savedScale = prt.localScale;
                savedPos = prt.anchoredPosition;
            }
            NegotiationTabletFrame f = Frame();
            // 무거운 일(거래창 부품 전체 훑기: 색, 글자 크기, 목록 줄 폭)은 내용이 바뀐 프레임 + FullPassInterval 마다만
            bool full = dirty || Time.unscaledTime >= nextFullPass;
            if (full) { dirty = false; nextFullPass = Time.unscaledTime + FullPassInterval; }
            HideTabletArt(f);
            if (full) Restyle(prt);
            StyleTablet(f, full);
            LayoutColumns(prt, f, full);
            DrawDebugBorder(prt);

            // 윈도우가 다 내려왔을 때의 회색 칸 (월드 좌표)
            var c = new Vector3[4];
            Vector2 animPos = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(0f, DownY());
            rt.GetWorldCorners(c); // 0 왼아래, 2 오른위
            rt.anchoredPosition = animPos;
            float left = Mathf.Lerp(c[0].x, c[2].x, InnerLeft), right = Mathf.Lerp(c[0].x, c[2].x, InnerRight);
            float top = Mathf.Lerp(c[2].y, c[0].y, InnerTop), bottom = Mathf.Lerp(c[2].y, c[0].y, InnerBottom);
            float innerH = top - bottom;
            if (right - left <= 0f || innerH <= 0f) return;

            // 배율: 회색 칸 높이 기준 (게임 원래 높이 FitRefH 로 고정 → TabletH 를 바꿔도 글자 크기는 그대로)
            var pc = new Vector3[4];
            prt.GetWorldCorners(pc);
            float sx = Mathf.Abs(prt.localScale.x) > 1e-5f ? prt.localScale.x : 1f;
            float unitH = (pc[2].y - pc[0].y) / sx;
            if (unitH <= 0f) return;
            float unitPerLocal = unitH / Mathf.Max(1f, prt.rect.height); // 배율 1일 때 거래창 1단위의 월드 길이
            float scale = innerH / (FitRefH * unitPerLocal) * InnerPad * TabletScale;
            if (Mathf.Abs(prt.localScale.x - scale) > 1e-4f) prt.localScale = new Vector3(scale, scale, 1f);

            // 자리: 거래창 위쪽 = 윈도우 위쪽 + TabletTopOffset, 좌우 가운데
            Vector3 topCenter = prt.TransformPoint(new Vector3(prt.rect.center.x, prt.rect.yMax, 0f));
            float down = prt.TransformVector(new Vector3(0f, TabletTopOffset, 0f)).y; // 거래창 단위 → 월드
            Vector3 want = new Vector3((left + right) * 0.5f, c[2].y - down, topCenter.z);
            prt.position += want - topCenter;
        }

        // 거래창 태블릿 배경 그림(베이지 판 + 금속 모서리)을 숨긴다. 내용(글자/칸/버튼)은 그대로
        private void HideTabletArt(NegotiationTabletFrame f)
        {
            if (hiddenArt != null) { if (hiddenArt.enabled) hiddenArt.enabled = false; return; }
            Image art = f?.art;
            if (art == null) return;
            hiddenArt = art;
            art.enabled = false;
        }

        // 디버그용 빨간 테두리 (Settings.DebugBorder): 거래창 전체, 왼쪽 칸, 오른쪽 칸
        private GameObject debugBorder;

        private void DrawDebugBorder(RectTransform root)
        {
            if (!DebugBorder) return;
            if (debugBorder != null && debugBorder.transform.parent == root) return;
            if (debugBorder != null) Destroy(debugBorder);
            debugBorder = new GameObject("VME_DebugBorder", typeof(RectTransform));
            var holder = (RectTransform)debugBorder.transform;
            holder.SetParent(root, false);
            holder.anchorMin = Vector2.zero; holder.anchorMax = Vector2.one;
            holder.offsetMin = holder.offsetMax = Vector2.zero;
            Box(holder, null);                                              // 거래창 전체 (크기 따라감)
            Box(holder, new Rect(LeftX, TopY, LeftW, ColumnH));             // 왼쪽 칸
            Box(holder, new Rect(RightX, TopY, RightW, ColumnH));           // 오른쪽 칸
        }

        // area = 거래창 왼쪽 위 기준 (x, y 아래로 +, w, h). null 이면 부모 전체
        private static void Box(RectTransform parent, Rect? area)
        {
            var go = new GameObject("Box", typeof(RectTransform));
            var box = (RectTransform)go.transform;
            box.SetParent(parent, false);
            if (area.HasValue)
            {
                Rect a = area.Value;
                box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 1f);
                box.anchoredPosition = new Vector2(a.x, -a.y);
                box.sizeDelta = new Vector2(a.width, a.height);
            }
            else
            {
                box.anchorMin = Vector2.zero; box.anchorMax = Vector2.one;
                box.offsetMin = box.offsetMax = Vector2.zero;
            }
            const float w = 3f;
            Line(box, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -w), Vector2.zero); // 위
            Line(box, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, w));  // 아래
            Line(box, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(w, 0f));  // 왼쪽
            Line(box, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-w, 0f), Vector2.zero); // 오른쪽
        }

        private static void Line(RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = oMin; r.offsetMax = oMax;
            var img = go.AddComponent<Image>();
            img.color = Color.red;
            img.raycastTarget = false;
        }

        private void Undock()
        {
            if (debugBorder != null) Destroy(debugBorder);
            debugBorder = null;
            Unstyle();
            if (hiddenArt != null) hiddenArt.enabled = true;
            hiddenArt = null;
            if (docked == null) return;
            try
            {
                docked.localScale = savedScale;
                docked.anchoredPosition = savedPos;
            }
            catch
            {
            }
            docked = null;
        }
    }
}
