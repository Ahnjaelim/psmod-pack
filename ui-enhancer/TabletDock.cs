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

        private void DockTablet()
        {
            GameObject ui = NegociationUIManager.Instance != null ? NegociationUIManager.Instance.uiObject : null;
            if (ui == null || !ui.activeInHierarchy) { Undock(); return; }
            var prt = (RectTransform)ui.transform;
            if (docked != prt)
            {
                Undock();
                docked = prt;
                savedScale = prt.localScale;
                savedPos = prt.anchoredPosition;
            }
            NegotiationTabletFrame f = Frame();
            HideTabletArt(f);
            Restyle(prt);
            StyleTablet(f);
            LayoutColumns(prt, f);

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

        private void Undock()
        {
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
