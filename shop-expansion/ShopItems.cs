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
        internal const string AquaWaterTank = "vse_aqua_water_tank";
        // 대형 수조 = 게임 양식 기계(ras) + 크기/필터 8칸 구조. 구조가 달라 세이브에서 같은 모양으로 다시 만들려면 자기 id 가 필요하다 (0.6.4)
        internal const string AquaTank = "vse_aqua_tank";
        internal const string RoomTable = "vse_room_table"; // 방 앞 내 테이블 (18x4 투명 격자, RoomTable.cs)
        internal const string AquaFreezer = "vse_aqua_freezer"; // 냉동고 (하이퍼테크 보관함 복제, 생선 부속물만, Freezers.cs)
        internal const string FreezerRack = "vse_freezer_rack"; // 오른쪽 작업면: 냉동고 거치대 (Freezers.cs)
        // 수조가 풀업 양식 기계 5대분이라 물탱크도 게임 것(12,000ml)의 5배
        internal const int AquaWaterTankMl = 60000;
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
            Add(AquaWaterTank, CreateAquaWaterTank);
            Add(AquaTank, CreateAquaTank);
            Add(RoomTable, ShopExpansion.RoomTable.Create);
            Add(AquaFreezer, Freezers.CreateFreezer);
            Add(FreezerRack, Freezers.CreateRack);
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
            DropCageTab(m);
            try { ShopExpansion.AquaButcher.BuildPage(m); } // 수조 탭 → "틸라피아" 물 관리 탭 (0.11.0)
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] aqua butcher page: " + ex.Message); }
            return m;
        }

        // 수족관 전용 도축기는 수조에만 꽂히니 "우리"(동물) 탭을 뺀다 (0.9.3). 남은 수조 탭이 0번이 되어 항상 선택된다.
        // 게임 창은 우리 탭 설정도 계속 읽고 쓰지만 화면에만 없을 뿐이라 무해하다.
        private static void DropCageTab(GameItem m)
        {
            try
            {
                if (m.contentWindow == null) return;
                foreach (TreeNodeRender t in m.contentWindow.handler.GetComponentsInChildren<TreeNodeRender>(true))
                {
                    if (!(t.element is MachineryTabs tabs) || tabs.count < 2) continue;
                    PixelElement cage = tabs.GetPage(0);
                    if (cage != null && tabs.Detach(cage)) tabs.Select(0);
                    return;
                }
                Debug.LogWarning("[ShopExpansion] aqua butcher: tabs not found");
            }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] aqua butcher cage tab: " + ex.Message); }
        }

        private static GameItem CreateAquaTank()
        {
            GameItem m = MachineRAS.CreateRAS();
            ShopExpansion.AquaTank.Structure(m);
            return m;
        }

        // 수족관 전용 물탱크 = 게임 RAS 물탱크(ras_water_tank) 복사, 용량만 키움 (빈 채로 꽂힌다, 게임 양식 기계와 같음)
        private static GameItem CreateAquaWaterTank()
        {
            GameItem m = MachineRAS.CreateWaterTank();
            m.name = "수족관 전용 물탱크";
            m.ModifyTag("LIQUID_CONTAINER_CAPACITY", s => s.Enable().SetInt(AquaWaterTankMl * 1000)); // 게임 단위: ml x 1000
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
