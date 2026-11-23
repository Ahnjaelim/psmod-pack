using System;
using UnityEngine;

namespace ShopExpansion
{
    // 이 모드의 새 아이템 종류를 게임 아이템 목록(DirectoryMaster)에 정식 등록한다.
    // 정식 등록이라 세이브에 아이템 id 로 저장되고, 불러올 때 이 공장 함수로 다시 만들어진다.
    //  - vse_aqua_feeder  : 수족관 전용 급이기 = 게임 급이기(livestock_feed_dispenser) 복사. 업그레이드 내용은 나중에.
    //  - vse_aqua_butcher : 수족관 전용 도축기 = 게임 자동 도축기(auto_butcher) 복사. 업그레이드 내용은 나중에.
    // 둘 다 FixedTag: 양식장 대형 수조에 붙박이 (사용자가 뺄 수 없음).
    internal class ShopItems : ItemDirectory
    {
        internal const string AquaFeeder = "vse_aqua_feeder";
        internal const string AquaButcher = "vse_aqua_butcher";
        internal const string FixedTag = "VSE_FIXED_FITTING_TAG";

        private static GameObject host;

        // 모드가 켜질 때 한 번 (세이브를 불러오기 전에 등록돼 있어야 한다)
        internal static void Register()
        {
            if (host != null) return;
            host = new GameObject("VSE_ShopItems");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ShopItems>(); // Directory.Awake → DirectoryMaster.AddDirectory → InitDirectory
        }

        protected override void InitDirectory()
        {
            Add(AquaFeeder, CreateAquaFeeder);
            Add(AquaButcher, CreateAquaButcher);
        }

        private static GameItem CreateAquaFeeder()
        {
            GameItem m = MachineFeedDispenser.CreateFeedDispenser();
            m.name = "수족관 전용 급이기";
            m.EnableTag(FixedTag);
            return m;
        }

        private static GameItem CreateAquaButcher()
        {
            GameItem m = MachineAutoButcher.CreateAutoButcher();
            m.name = "수족관 전용 도축기";
            m.EnableTag(FixedTag);
            return m;
        }

        internal static GameItem Spawn(string id)
        {
            try { return DirectoryMaster.Item(id); }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] spawn " + id + ": " + ex.Message); return null; }
        }
    }
}
