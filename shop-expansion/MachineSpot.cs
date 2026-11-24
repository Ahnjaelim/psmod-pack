using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShopExpansion
{
    // 방 배경 위에 얹은 기계 그림 (급이기 / 도축기 / 냉동고, 0.10.0).
    // 진짜 기계는 숨김 인벤(수조 칸, 냉동고 거치대)에 그대로 있고, 그림은 그 기계를 가리킨다.
    //  - 누르기: 기계 창 열기/닫기 (RoomPanel 의 버튼)
    //  - 끌어다 놓기: 그 기계 위에 놓은 것으로 게임이 처리 (TankDropPatch)
    //  - 마우스 올리기: 그 기계의 게임 툴팁 (TankTooltipPatch)
    internal class MachineSpot : MonoBehaviour
    {
        internal Func<GameItem> Target;

        private static readonly List<MachineSpot> all = new List<MachineSpot>();

        private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        private void OnDisable() => all.Remove(this);

        // 화면 좌표 아래 보이는 기계 그림의 기계 (없으면 null)
        internal static GameItem At(Vector2 screen)
        {
            for (int i = all.Count - 1; i >= 0; i--)
            {
                MachineSpot s = all[i];
                if (s == null || !s.gameObject.activeInHierarchy) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)s.transform, screen, null)) continue;
                try { return s.Target?.Invoke(); } catch { return null; }
            }
            return null;
        }
    }
}
