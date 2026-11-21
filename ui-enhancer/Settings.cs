using UnityEngine;

namespace UiEnhancer
{
    // ===== 조정용 숫자 모음 =====
    // 여기 숫자만 바꾸고 게임을 끈 뒤 `dotnet build -c Release` 하면 반영된다.
    internal partial class ClientWindow
    {
        // ---- 윈도우 (레이어 1: img/window.png 배경) ----
        private const float Scale = 0.57f;        // 윈도우 크기 (원래 그림 대비)
        private const int PixelSize = 2;          // 픽셀아트 블록 크기 (2560x1440 화면 픽셀). 1 이면 필터 없음
        private const float TopMargin = 40f;      // 다 내려왔을 때 화면 위에서 떨어진 거리 (기준 픽셀)
        private const float DownTime = 0.45f;     // 내려오는 시간 (초)
        private const float UpTime = 0.3f;        // 올라가는 시간 (초)
        private const float RefHeight = 1440f;    // 기준 화면 높이: 그림 픽셀 = 이 높이일 때 1:1 (바꾸지 말 것)

        // 윈도우 그림에서 회색 칸 위치 (그림 크기 대비 비율, 위가 0). 그림을 바꾸면 맞춰 줄 것
        private const float InnerLeft = 0.036f, InnerRight = 0.964f, InnerTop = 0.040f, InnerBottom = 0.855f;

        // ---- 거래창 (레이어 2: 게임 거래창) 크기/위치 ----
        private const float TabletScale = 1.4f;   // 회색 칸 높이에 맞춘 크기에서 더 키우기 (글자/칸/버튼 전부)
        private const float InnerPad = 0.97f;     // 회색 칸에 맞출 때 여유
        private const float FitRefH = 818f;       // 배율 계산 기준 높이 (게임 원래 거래창 높이, 바꾸지 말 것)
        private const float TabletTopOffset = 20f; // 거래창 위쪽 = 윈도우 위쪽에서 이만큼 아래 (거래창 단위)

        // ---- 거래창 2단 배치 (거래창 안 좌표: 왼쪽 위 기준, x 오른쪽 / y 아래로 +) ----
        private const float TabletW = 920f, TabletH = 610f;   // 거래창 전체 (게임 원래 644 x 818)
        private const float LeftX = 24f, LeftW = 276f;        // 왼쪽 칸: 손님 이름 / 부제 / 관심사
        private const float RightX = 360f, RightW = 570f;     // 오른쪽 칸: 거래 목록 + 금액 줄
        private const float TopY = 40f;                       // 두 칸 위쪽
        private const float ListH = 400f, FooterH = 70f;      // 목록 높이, 금액 줄 높이
        private const float ButtonsW = 562f;                  // 버튼 줄 너비 (게임 원래 562 → 버튼은 그 안 가운데)
        private const float ButtonsY = TabletH - 55f;         // 버튼 줄 위치 = 거래창 바닥에서 55 위

        // ---- 거래창 글자 ----
        private const float NameTextScale = 1.35f;  // 손님 이름 글자 배율
        private const float TotalTextScale = 1.5f;  // 합계 금액 글자 배율
        private const float ListTextScale = 1.2f;   // 거래 목록 글자 배율 (물건 이름, 가격, 소제목)

        // ---- 거래창 색 ----
        private static readonly Color Mint = new Color32(0x2b, 0xc4, 0xb0, 0xff);
        private static readonly Color SoftWhite = new Color32(0xe6, 0xe6, 0xe6, 0xff);
        private static readonly Color SubtitleColor = new Color(SoftWhite.r, SoftWhite.g, SoftWhite.b, 0.7f); // 부제(유인 요인 등)
        private static readonly Color BottomLineColor = new Color(Mint.r, Mint.g, Mint.b, 0f);                 // 목록과 금액 줄 사이 선
        private static readonly Color ScrollbarColor = new Color(Mint.r, Mint.g, Mint.b, 0.3f);                // 목록 스크롤 막대
        private static readonly Color SubHeaderPlateColor = new Color(Mint.r, Mint.g, Mint.b, 0.15f);          // 소제목 줄 판 (도매 할인 제안 등)
        private static readonly Color RowLineColor = new Color(1f, 1f, 1f, 0f);                                // 목록 줄 사이 선
        private static readonly Color ButtonHoverColor = new Color(Mint.r, Mint.g, Mint.b, 0.18f);             // 버튼에 마우스 올렸을 때 바탕
    }
}
