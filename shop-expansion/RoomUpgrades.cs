using System;
using UnityEngine;

namespace ShopExpansion
{
    // 조 와일드 네트워크 "Advanced"(엔드 콘텐츠) 탭: 가게 증축 I~III (방 1개씩, 앞 단계를 사야 다음 단계).
    // 산 단계는 게임 네트워크 업그레이드 저장에 남는다 → 방 수 = 산 단계 수 (따로 저장 안 함).
    internal static class RoomUpgrades
    {
        private static readonly int[] Cost = { 3000, 6000, 10000 };
        private static readonly string[] Roman = { "I", "II", "III" };

        internal static string Id(int room) => "VSE_SHOP_ROOM_" + room;

        internal static void Register()
        {
            for (int i = 1; i <= Rooms.Max; i++)
            {
                int room = i;
                try
                {
                    ModHelper.AddNetworkUpgrade(ShopExpansionMod.ModId, Id(room), Cost[room - 1], "Store",
                        () => "가게 증축 " + Roman[room - 1],
                        () => "와일드가 업자를 보내 가게 옆에 빈 방을 하나 " + (room == 1 ? "" : "더 ") + "만들어 줍니다 (방 " + room + "/" + Rooms.Max + ").\n"
                              + "빈 방에 들어가면 양식장, 사육장, 숙성고, 온실 같은 용도를 정할 수 있습니다.\n"
                              + "가게 화면 왼쪽 아래 방 버튼으로 언제든 오갈 수 있습니다.",
                        () =>
                        {
                            Rooms.Notify("가게 증축 " + Roman[room - 1] + " 완료: 방 " + room + "이(가) 생겼다. 왼쪽 아래 [방 " + room + "] 버튼으로 들어가 용도를 정하자.");
                            RoomPanel.Refresh();
                        },
                        tab: "Advanced",
                        prerequisites: room > 1 ? new[] { Id(room - 1) } : null,
                        subtitleLabel: () => "증축");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ShopExpansion] room upgrade " + room + ": " + ex);
                }
            }
        }

        internal static void Unregister()
        {
            try { ModHelper.RemoveNetworkUpgrades(ShopExpansionMod.ModId); } catch { }
        }
    }
}
