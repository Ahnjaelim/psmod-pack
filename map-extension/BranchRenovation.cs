using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapExtension
{
    // 조 와일드 네트워크 "Advanced"(엔드 콘텐츠) 탭: 분점 개보수 I~III (본점 "가게 개보수"와 같은 방식, 단계마다 매력도 +150).
    // 산 단계는 게임 네트워크 업그레이드 저장에 남으므로, 매력도 보너스는 산 업그레이드 수로 계산한다 (따로 저장 안 함).
    internal static class BranchRenovation
    {
        internal const int PerLevel = 150;            // 본점 개보수와 같음 (NetworkUpgradeList.RENOVATION_ATTRACTIVENESS)
        private static readonly int[] BaseCost = { 500, 750, 1000 }; // 본점 개보수 비용
        private static readonly string[] Roman = { "I", "II", "III" };
        private static readonly List<string> registered = new List<string>();

        internal static string Id(BranchSite b, int level) => "VME_BRANCH_RENOVATION_" + b.Key.ToUpperInvariant() + (level + 1);

        // 분점 개설 비용에 비례 (하층민 6000 → 본점과 같은 500/750/1000)
        private static int Cost(BranchSite b, int level)
        {
            float k = Mathf.Max(1f, b.Price / 6000f);
            return Mathf.RoundToInt(BaseCost[level] * k / 50f) * 50;
        }

        internal static void Register()
        {
            foreach (BranchSite site in Branches.All)
            {
                BranchSite b = site;
                for (int lv = 0; lv < 3; lv++)
                {
                    int level = lv;
                    string id = Id(b, level);
                    try
                    {
                        ModHelper.AddNetworkUpgrade(MapExtensionMod.ModId, id, Cost(b, level), "Store",
                            () => b.Name + " 개보수 " + Roman[level],
                            () => "와일드가 업자를 보내 " + b.Name + "을(를) " + (level == 0 ? "소규모로" : level == 1 ? "대규모로" : "전면") + " 개보수합니다. "
                                  + "분점 매력도가 " + PerLevel + "점 오릅니다 (분점 손님 수 = 매력도/400배, 0.6~1.8)."
                                  + (b.Owned ? "" : "\n아직 분점을 열지 않았다. 열어야 효과가 있다."),
                            () => { Notify(b.Name + " 개보수 " + Roman[level] + " 완료: 분점 매력도 +" + PerLevel); BranchMonitor.Refresh(); },
                            tab: "Advanced",
                            prerequisites: level > 0 ? new[] { Id(b, level - 1) } : null,
                            subtitleLabel: () => "분점");
                        registered.Add(id);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[MapExtension] renovation upgrade " + id + ": " + ex.Message);
                    }
                }
            }
        }

        internal static void Unregister()
        {
            try { ModHelper.RemoveNetworkUpgrades(MapExtensionMod.ModId); } catch { }
            registered.Clear();
        }

        // 산 개보수 단계 수 × 150
        internal static int Bonus(BranchSite b)
        {
            if (b == null) return 0;
            int n = 0;
            for (int lv = 0; lv < 3; lv++)
            {
                try { if (ModHelper.IsNetworkUpgradeUnlocked(Id(b, lv))) n++; } catch { }
            }
            return n * PerLevel;
        }

        private static void Notify(string text)
        {
            try { StoreUIManager.Instance.Notify(text); } catch { }
        }
    }
}
