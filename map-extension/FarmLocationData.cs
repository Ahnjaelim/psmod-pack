using System.Collections.Generic;
using UnityEngine;

namespace MapExtension
{
    // 수색처 19곳 (지도의 "미확인 장소" 자리 = TransitMap.UnknownSpots 와 같은 key). 배경은 전부 임시 배경.
    // 해금: 시작은 약국 + 철물점. 장소의 뒤질 곳을 전부 한 번 이상 뒤지면 망원경 지점이 생기고, 쓰면 Unlocks 장소가 열린다.
    // 전리품 표: packedFoodTable, junkTable, householdTable, materialTable, medicalTable, toolTable,
    //           makeshiftWeaponTable, allModuleTable, accessCardTable
    internal static class FarmLocationData
    {
        internal static readonly HashSet<string> StartOpen = new HashSet<string> { "pharmacy", "hardware" };

        // ---- 초반 수색처 밸런스: 큰 배낭(8x9 = 72칸) + 기본 기력(하룻밤 5) 기준으로 이틀은 털어야 다 털리게 ----
        //  장소 전체 기력 합 = EarlyEnergy (망원경 제외), 처음 뒤질 때 기력 1당 아이템 3~4개 → 장소당 약 30~40개 (배낭 두 번 분량)
        internal static readonly HashSet<string> Early = new HashSet<string>
        {
            "pharmacy", "hardware",                                                    // 1단계 (시작)
            "convenience", "apartment", "laundry", "clinic", "radio_shop", "sewer",    // 2단계
        };
        internal const int EarlyEnergy = 10, ItemsPerEnergyMin = 3, ItemsPerEnergyMax = 4;
        internal const float EarlyMaxEmpty = 5f;

        internal static void ApplyEarlyBalance(FarmLocation l)
        {
            if (!Early.Contains(l.Key)) return;
            var spots = l.Spots.FindAll(s => !s.Telescope);
            if (spots.Count == 0) return;
            // 원래 비용 비율대로 합이 EarlyEnergy 가 되게 (최소 1)
            int sum = 0;
            foreach (FarmSpot s in spots) sum += Mathf.Max(1, s.Cost);
            int given = 0;
            for (int i = 0; i < spots.Count; i++)
            {
                FarmSpot s = spots[i];
                int c = i == spots.Count - 1 ? EarlyEnergy - given : Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, s.Cost) * EarlyEnergy / (float)sum));
                c = Mathf.Max(1, c);
                s.Cost = c;
                given += c;
                s.MinItems = c * ItemsPerEnergyMin;
                s.MaxItems = c * ItemsPerEnergyMax;
                s.Empty = Mathf.Min(s.Empty, EarlyMaxEmpty);
            }
        }

        // 해금 연결 (망원경으로 보이는 곳)
        internal static readonly Dictionary<string, string[]> Chain = new Dictionary<string, string[]>
        {
            { "pharmacy",     new[] { "clinic", "laundry" } },
            { "hardware",     new[] { "radio_shop", "apartment" } },
            { "clinic",       new[] { "convenience", "soup_kitchen" } },
            { "laundry",      new[] { "sewer" } },
            { "radio_shop",   new[] { "garage", "lost_found" } },
            { "apartment",    new[] { "school" } },
            { "convenience",  new[] { "loading_dock" } },
            { "soup_kitchen", new[] { "church" } },
            { "sewer",        new[] { "factory", "rebel_hideout" } },
            { "garage",       new[] { "checkpoint" } },
            { "lost_found",   new[] { "cartel_storage" } },
            { "school",       new[] { "hospital" } },
            { "loading_dock", new[] { "cartel_storage" } },
            { "factory",      new[] { "checkpoint" } },
            { "church",       new[] { "mansion" } },
        };

        private static FarmSpot S(string key, string name, float x, float y, int cost, string desc, string[] tables,
                                  float empty = 20f, float minor = 0f, float major = 0f, int min = 1, int max = 1,
                                  string[] items = null, float itemChance = 60f, int refill = 3)
        {
            return new FarmSpot
            {
                Key = key, Name = name, Area = new Rect(x, y, 0f, 0f), Cost = cost, Desc = desc, Tables = tables,
                Empty = empty, MinorWound = minor, MajorWound = major, MinItems = min, MaxItems = max,
                Items = items, ItemChance = itemChance, RefillDays = refill,
            };
        }

        private static FarmSpot Locked(FarmSpot s, string[] needs, string text, bool consume)
        {
            s.NeedsAny = needs;
            s.NeedsText = text;
            s.ConsumesTool = consume;
            return s;
        }

        private static readonly string[] Crowbar = { "crowbar" };
        private const string CrowbarText = "꽉 막혀 있다. 지렛대(crowbar)가 외출 가방에 있어야 한다.";

        private static FarmLocation L(string key, string name, string desc, params FarmSpot[] spots)
        {
            return new FarmLocation { Key = key, Name = name, Desc = desc, Spots = new List<FarmSpot>(spots) };
        }

        internal static List<FarmLocation> Create()
        {
            var list = new List<FarmLocation>
            {
                // ---- 1단계 ----
                L("hardware", "버려진 철물점", "간판 글자가 반쯤 떨어진 철물점. 공구는 다 털렸다지만 창고 쪽은 아무도 안 들어가 봤다.",
                    S("rack", "공구 진열대", 0.20f, 0.58f, 2, "구부러진 걸이에 공구 몇 개가 아직 걸려 있다.", new[] { "toolTable", "materialTable" },
                        empty: 20f, minor: 4f, items: new[] { "screwdriver:20", "wire_cutter:12", "box_cutter:14", "flashlight:10", "wire:20", "nuts_metal:20" }, itemChance: 55f),
                    S("bins", "부품 서랍장", 0.46f, 0.52f, 1, "작은 서랍이 수십 개. 대부분 비었지만 나사 몇 개쯤은.", new[] { "materialTable", "junkTable" },
                        empty: 25f, min: 1, max: 2, items: new[] { "nuts_metal:30", "scrap_metal:25", "wire:20", "common_electronic:10" }, itemChance: 50f, refill: 2),
                    S("counter", "계산대", 0.70f, 0.48f, 1, "금전 등록기는 뜯겨 나갔다. 아래 칸이 남아 있다.", new[] { "householdTable", "junkTable" },
                        empty: 30f, items: new[] { "lottery_ticket_old:15", "postit:20", "marking_pen:15", "plastic_bag:20" }, itemChance: 40f),
                    Locked(S("storeroom", "뒤쪽 창고", 0.84f, 0.66f, 3, "쇠사슬로 감긴 창고 문. 안에 상자가 쌓여 있는 게 틈새로 보인다.",
                        new[] { "toolTable", "materialTable", "allModuleTable" }, empty: 8f, minor: 6f, min: 2, max: 3,
                        items: new[] { "toolbox:6", "welder:3", "metal_ingot:15", "scrap_metal:25" }, itemChance: 45f, refill: 6), Crowbar, CrowbarText, false)),

                // ---- 2단계 ----
                L("convenience", "편의점", "불 꺼진 24시 편의점. 유통기한은 지났어도 포장 음식은 아직 먹을 만하다.",
                    S("shelves", "식품 진열대", 0.22f, 0.55f, 1, "쓰러진 진열대 아래 깔린 봉지들.", new[] { "packedFoodTable" },
                        empty: 15f, min: 1, max: 3, items: new[] { "cup_noodle:25", "li_eat_snackbar:20", "processed_cheese:12", "processed_meat:10" }, itemChance: 50f, refill: 2),
                    S("fridge", "음료 냉장고", 0.48f, 0.62f, 1, "문이 열린 채 꺼진 냉장고. 미지근한 병이 몇 개 남았다.", new[] { "packedFoodTable" },
                        empty: 20f, min: 1, max: 2, items: new[] { "small_bottled_water:25", "bottled_water:15", "soda_red:20", "energy_drink:15", "red_beer:10", "processed_juice:12" }, itemChance: 75f),
                    S("register", "카운터", 0.72f, 0.50f, 1, "카운터 뒤 담배 진열장. 거의 비었다.", new[] { "householdTable", "junkTable" },
                        empty: 30f, items: new[] { "packet_red_cigarette:15", "lottery_ticket:10", "scratcher:10", "pack_condom:10", "plastic_bag:20" }, itemChance: 55f),
                    S("backroom", "창고 상자", 0.86f, 0.70f, 2, "뒤쪽 창고에 쌓인 상자. 위쪽 상자가 불안하게 기울어 있다.", new[] { "packedFoodTable", "householdTable" },
                        empty: 15f, minor: 8f, min: 1, max: 3, refill: 4)),

                L("apartment", "빈 아파트", "주민이 한꺼번에 빠져나간 낡은 아파트. 문 몇 개는 아직 잠겨 있다.",
                    S("kitchen", "부엌 찬장", 0.18f, 0.55f, 1, "찬장 문이 떨어져 나갔다. 안쪽 깊숙이 뭔가 있을지도.", new[] { "packedFoodTable", "householdTable" },
                        empty: 20f, min: 1, max: 2, items: new[] { "kitchen_knife:10", "cup_noodle:20", "paper_towel:15" }, itemChance: 40f),
                    S("wardrobe", "옷장", 0.42f, 0.66f, 1, "옷걸이만 남은 옷장. 바닥에 가방이 하나 굴러다닌다.", new[] { "householdTable" },
                        empty: 25f, items: new[] { "kids_backpack:8", "fanny_pack:10", "satchel:6", "kotton_fabric:20" }, itemChance: 45f),
                    S("bathroom", "욕실 수납장", 0.62f, 0.50f, 1, "거울 달린 수납장. 깨진 유리 조심.", new[] { "householdTable", "medicalTable" },
                        empty: 20f, minor: 5f, items: new[] { "toothpaste:20", "shampoo:20", "toilet_paper:20", "skincare_cream:6", "bandage_item:15" }, itemChance: 60f),
                    Locked(S("locked", "잠긴 집", 0.84f, 0.62f, 3, "문 틈으로 사람이 살던 흔적이 그대로 보인다. 급하게 떠난 모양이다.",
                        new[] { "householdTable", "packedFoodTable", "materialTable" }, empty: 8f, min: 2, max: 3,
                        items: new[] { "wine_bottle:8", "lottery_ticket:8", "gamblers_coin:2", "deal_maker:3" }, itemChance: 30f, refill: 6), Crowbar, CrowbarText, false)),

                L("laundry", "세탁소", "세제 냄새가 아직 배어 있는 동네 세탁소. 찾아가지 않은 옷이 가득하다.",
                    S("machines", "세탁기 줄", 0.25f, 0.52f, 1, "멈춘 세탁기 안에 잊힌 빨래. 주머니를 뒤져 보자.", new[] { "householdTable", "junkTable" },
                        empty: 25f, min: 1, max: 2, items: new[] { "kotton_fabric:25", "lottery_ticket_old:10", "food_stamp:8", "pack_condom:6" }, itemChance: 55f, refill: 2),
                    S("rack", "보관 옷걸이", 0.52f, 0.66f, 1, "번호표가 붙은 옷들. 몇 년째 아무도 안 찾아갔다.", new[] { "householdTable" },
                        empty: 20f, items: new[] { "kotton_fabric:30", "kotton_fiber:15", "satchel:5" }, itemChance: 60f),
                    S("counter", "접수대", 0.76f, 0.48f, 1, "장부와 잔돈 통. 잔돈은 없다.", new[] { "junkTable", "householdTable" },
                        empty: 30f, items: new[] { "postit:20", "marking_pen:15", "handnote:6" }, itemChance: 40f),
                    S("boiler", "보일러실", 0.86f, 0.72f, 2, "뜨거운 배관이 지나가는 좁은 방. 공구 상자가 걸려 있다.", new[] { "toolTable", "materialTable" },
                        empty: 15f, minor: 10f, major: 2f, items: new[] { "wire:20", "faucet:10", "drain:10", "screwdriver:10" }, itemChance: 50f, refill: 5)),

                L("clinic", "동네 의원", "간판만 남은 작은 의원. 진료 기록이 바닥에 흩어져 있다.",
                    S("reception", "접수처", 0.20f, 0.50f, 1, "대기실 의자와 접수 창구.", new[] { "householdTable", "medicalTable" },
                        empty: 25f, items: new[] { "bandage_item:25", "caffeine_pill:15", "birth_control_pill:10", "dossier:4" }, itemChance: 50f),
                    S("exam", "진료실 서랍", 0.45f, 0.62f, 2, "진료 침대 옆 서랍장. 소독약 냄새.", new[] { "medicalTable" },
                        empty: 15f, min: 1, max: 2, items: new[] { "rubbing_alcohol:20", "topical_bandage_item:15", "hemostatic_bandage_item:8", "surgery_tool:5", "smelling_salt:10" }, itemChance: 65f),
                    S("fridge", "약품 냉장고", 0.68f, 0.55f, 2, "전기가 끊긴 약품 냉장고. 앰플 몇 개가 굴러다닌다.", new[] { "medicalTable" },
                        empty: 25f, minor: 4f, items: new[] { "pink_injector:8", "pink_alt_injector:5", "black_injector:3", "blood_bag:6", "med_bottle_red:10" }, itemChance: 70f, refill: 5),
                    Locked(S("records", "원장실", 0.86f, 0.70f, 3, "의무부 카드 리더기가 달린 원장실 문.", new[] { "medicalTable", "allModuleTable" },
                        empty: 5f, min: 2, max: 3, items: new[] { "large_purple_injector:4", "pure_white_injector:3", "phagimycin_pill:12", "xray_scanner:2" }, itemChance: 50f, refill: 7),
                        new[] { "med_keycard", "cmd_keycard" }, "의무부 키카드 잠금장치다. 의무부 키카드(또는 사령부 키카드)가 외출 가방에 있어야 한다.", true)),

                L("radio_shop", "전파사", "고장 난 라디오와 텔레비전이 천장까지 쌓인 전파사.",
                    S("pile", "고물 더미", 0.20f, 0.58f, 1, "분해하다 만 기계들. 쓸 만한 부품을 골라내자.", new[] { "junkTable", "materialTable" },
                        empty: 15f, minor: 4f, min: 1, max: 2, items: new[] { "common_electronic:30", "wire:25", "scrap_metal:20", "rare_electronic:4" }, itemChance: 60f, refill: 2),
                    S("bench", "수리 작업대", 0.46f, 0.50f, 2, "인두와 돋보기가 놓인 작업대.", new[] { "toolTable", "materialTable" },
                        empty: 15f, minor: 6f, items: new[] { "magnifier:8", "screwdriver:15", "wire_cutter:10", "current_converter:6", "recharger:5" }, itemChance: 55f),
                    S("shelf", "부품 선반", 0.70f, 0.62f, 1, "번호 붙은 부품 상자들.", new[] { "materialTable", "allModuleTable" },
                        empty: 20f, items: new[] { "common_electronic:25", "blank_module:5", "node_small:6" }, itemChance: 55f, refill: 4),
                    S("radio", "무전기 구석", 0.86f, 0.72f, 2, "벽에 붙은 낡은 무전기. 누군가 최근까지 쓴 흔적이 있다.", new[] { "materialTable" },
                        empty: 30f, items: new[] { "republic_transmitter:2", "rev_codeword:3", "rare_electronic:10", "aug_chip:3" }, itemChance: 60f, refill: 6)),

                L("sewer", "하수구 입구", "하층 아래로 이어지는 하수구 입구. 냄새가 지독하고, 뭔가 기어 다닌다.",
                    S("grate", "배수구 철망", 0.22f, 0.50f, 1, "철망에 걸린 쓰레기들.", new[] { "junkTable" },
                        empty: 25f, minor: 6f, min: 1, max: 2, items: new[] { "junk:30", "glass_shard:15", "rat:10", "feces_small:10" }, itemChance: 55f, refill: 1),
                    S("nest", "쥐 둥지", 0.46f, 0.66f, 2, "쥐들이 모아 둔 반짝이는 것들. 물리지 않게.", new[] { "junkTable", "householdTable" },
                        empty: 15f, minor: 14f, major: 2f, items: new[] { "rat:25", "gamblers_coin:3", "lottery_ticket_old:10", "nuts_metal:15" }, itemChance: 60f),
                    S("pipe", "물 고인 관로", 0.70f, 0.55f, 2, "고인 물에 물고기가 산다. 하수에 적응한 녀석들.", new[] { "junkTable" },
                        empty: 25f, minor: 8f, items: new[] { "gutter_tilapia:25", "gutter_tilapia_fry:20", "gutterflow_pellet:15", "eel:6" }, itemChance: 80f),
                    S("cache", "숨겨진 짐", 0.88f, 0.72f, 3, "벽 틈에 비닐로 싼 꾸러미. 누군가 여기 숨겼다.", new[] { "makeshiftWeaponTable", "accessCardTable", "materialTable" },
                        empty: 20f, minor: 10f, major: 3f, items: new[] { "pipe_weapon:10", "dream_dust:8", "fanta_pill:8", "handmade_pistol:3", "small_pistol_ammo:10" }, itemChance: 50f, refill: 6)),

                // ---- 3단계 ----
                L("lost_found", "정거장 분실물 보관소", "정거장 사람들이 잃어버린 물건이 다 모이는 곳. 주인이 찾아간 적은 거의 없다.",
                    S("bins", "분실물 바구니", 0.20f, 0.55f, 1, "온갖 잡동사니가 섞인 바구니.", new[] { "householdTable", "junkTable" },
                        empty: 15f, min: 1, max: 3, items: new[] { "kids_backpack:6", "flashlight:6", "lottery_ticket:8", "laser_toy:6", "marking_pen:10" }, itemChance: 45f, refill: 2),
                    S("lockers", "보관함", 0.45f, 0.62f, 2, "번호 자물쇠가 망가진 사물함들.", new[] { "householdTable", "accessCardTable" },
                        empty: 20f, min: 1, max: 2, items: new[] { "blank_keycard:8", "wanted_paper:4", "cigarette_color:8", "gamblers_coin:2" }, itemChance: 40f),
                    S("desk", "직원 책상", 0.68f, 0.50f, 1, "서류 더미 아래 서랍.", new[] { "junkTable" },
                        empty: 25f, items: new[] { "postit:15", "procurement_slip:6", "slip:10", "food_stamp:10" }, itemChance: 55f),
                    Locked(S("vault", "귀중품 보관실", 0.86f, 0.70f, 3, "보급부 카드로 여는 귀중품 보관실.", new[] { "accessCardTable", "allModuleTable", "householdTable" },
                        empty: 5f, min: 2, max: 3, items: new[] { "luxury_supply_crate:3", "deal_maker:5", "liquid_luck:4", "energy_credit:6" }, itemChance: 55f, refill: 7),
                        new[] { "sup_keycard", "cmd_keycard" }, "보급부 키카드 잠금장치다. 보급부 키카드(또는 사령부 키카드)가 외출 가방에 있어야 한다.", true)),

                L("loading_dock", "화물 하역장", "화물선이 들어오던 하역장. 크레인이 멈춘 채 녹슬고 있다.",
                    S("crates", "부서진 화물 상자", 0.22f, 0.52f, 2, "떨어지며 깨진 화물 상자들.", new[] { "materialTable", "packedFoodTable" },
                        empty: 15f, minor: 8f, min: 1, max: 3, refill: 3),
                    S("container", "열린 컨테이너", 0.48f, 0.64f, 2, "문이 열린 컨테이너. 안쪽은 어둡다.", new[] { "materialTable", "toolTable", "householdTable" },
                        empty: 15f, minor: 6f, major: 2f, min: 1, max: 2, items: new[] { "metal_ingot:15", "kotton_fiber:15", "plasma_fuel:6", "printer_plastic:10" }, itemChance: 40f),
                    S("office", "하역 사무소", 0.72f, 0.50f, 1, "하역 일지와 서류함.", new[] { "junkTable", "accessCardTable" },
                        empty: 25f, items: new[] { "procurement_slip:10", "expedition_log:4", "sup_keycard:4", "slip:10" }, itemChance: 45f),
                    Locked(S("bonded", "보세 창고", 0.88f, 0.72f, 3, "세관 봉인이 붙은 창고. 지렛대로 봉인을 뜯을 수 있다.", new[] { "allModuleTable", "materialTable", "accessCardTable" },
                        empty: 8f, minor: 5f, min: 2, max: 3, items: new[] { "luxury_supply_crate:4", "hypertech_electronic:3", "rare_ore:10", "caviar:6" }, itemChance: 40f, refill: 7), Crowbar, CrowbarText, false)),

                L("factory", "폐공장", "기계 소리가 멈춘 지 오래인 공장. 컨베이어 위에 만들다 만 물건들이 그대로다.",
                    S("line", "생산 라인", 0.20f, 0.55f, 2, "멈춘 컨베이어. 부품이 그대로 걸려 있다.", new[] { "materialTable", "allModuleTable" },
                        empty: 15f, minor: 8f, min: 1, max: 2, items: new[] { "gun_part:8", "internal_springs:8", "trigger_group:5", "scrap_metal:20" }, itemChance: 45f),
                    S("furnace", "용광로", 0.44f, 0.66f, 3, "식은 용광로 바닥에 굳은 금속 덩어리.", new[] { "materialTable" },
                        empty: 20f, minor: 10f, major: 4f, items: new[] { "metal_ingot:30", "common_ore:20", "rare_ore:8", "flux_agent:8" }, itemChance: 75f, refill: 5),
                    S("tools", "공구실", 0.68f, 0.50f, 2, "공구가 걸려 있던 벽. 몇 개는 남았다.", new[] { "toolTable", "makeshiftWeaponTable" },
                        empty: 15f, minor: 5f, items: new[] { "hatchet:6", "crowbar:6", "welder:4", "toolbox:5", "wire_cutter:10" }, itemChance: 50f),
                    Locked(S("control", "통제실", 0.86f, 0.70f, 3, "기술부 카드 리더기가 달린 통제실.", new[] { "allModuleTable", "materialTable" },
                        empty: 5f, min: 2, max: 3, items: new[] { "system_module_performance:4", "system_module_efficiancy:4", "system_module_quality:4", "system_capped_neural_core:2", "powerblock:4" }, itemChance: 55f, refill: 7),
                        new[] { "eng_keycard", "cmd_keycard" }, "기술부 키카드 잠금장치다. 기술부 키카드(또는 사령부 키카드)가 외출 가방에 있어야 한다.", true)),

                L("garage", "수리소", "기름때 찌든 차량 수리소. 리프트에 올려 둔 차가 그대로 있다.",
                    S("lift", "리프트 차량", 0.22f, 0.58f, 2, "뜯다 만 차. 부품을 빼낼 수 있다.", new[] { "materialTable", "toolTable" },
                        empty: 15f, minor: 10f, major: 2f, min: 1, max: 2, items: new[] { "scrap_metal:25", "wire:15", "common_electronic:15", "current_converter:5" }, itemChance: 50f),
                    S("cabinet", "공구함", 0.48f, 0.50f, 1, "빨간 공구함. 서랍 몇 개가 잠겨 있다.", new[] { "toolTable" },
                        empty: 20f, items: new[] { "screwdriver:20", "wire_cutter:15", "box_cutter:10", "crowbar:6", "toolbox:5" }, itemChance: 55f),
                    S("drums", "기름통", 0.72f, 0.64f, 1, "드럼통 몇 개. 아직 뭔가 남아 있다.", new[] { "materialTable" },
                        empty: 30f, minor: 4f, items: new[] { "plasma_fuel:15", "common_chemical:20", "restricted_chemical:5" }, itemChance: 70f),
                    S("office", "사장실", 0.86f, 0.42f, 1, "벽에 핀업 달력, 책상엔 영수증.", new[] { "junkTable", "householdTable" },
                        empty: 25f, items: new[] { "nudka:8", "packet_red_cigarette:10", "lottery_ticket:8", "red_beer:10" }, itemChance: 50f)),

                L("school", "폐교", "아이들 그림이 아직 벽에 붙어 있는 폐교. 조용하다.",
                    S("classroom", "교실 사물함", 0.20f, 0.55f, 1, "이름표 붙은 사물함들.", new[] { "householdTable", "junkTable" },
                        empty: 20f, min: 1, max: 2, items: new[] { "kids_backpack:10", "kids_backpack_large:4", "marking_pen:15", "laser_toy:6", "li_eat_snackbar:10" }, itemChance: 50f, refill: 2),
                    S("lab", "과학실", 0.45f, 0.64f, 2, "깨진 비커와 약품장.", new[] { "materialTable", "medicalTable" },
                        empty: 20f, minor: 8f, items: new[] { "common_chemical:20", "restricted_chemical:6", "5ml_dropper:10", "25ml_cylinder:8", "heating_plate:3" }, itemChance: 60f),
                    S("cafeteria", "급식실", 0.70f, 0.50f, 1, "급식실 창고. 통조림이 남아 있을지도.", new[] { "packedFoodTable" },
                        empty: 15f, min: 1, max: 3, items: new[] { "processed_milk:15", "nutrient_tablet:10", "cup_noodle:15" }, itemChance: 45f, refill: 3),
                    S("nurse", "보건실", 0.86f, 0.70f, 1, "작은 보건실. 약통이 열려 있다.", new[] { "medicalTable" },
                        empty: 20f, items: new[] { "bandage_item:25", "topical_bandage_item:15", "caffeine_pill:10", "salve:10" }, itemChance: 65f, refill: 4)),

                L("soup_kitchen", "무료 급식소", "교회가 운영하던 무료 급식소. 배급표가 바닥에 흩어져 있다.",
                    S("pantry", "식료품 창고", 0.22f, 0.55f, 1, "기부받은 식료품이 쌓여 있던 창고.", new[] { "packedFoodTable" },
                        empty: 15f, min: 1, max: 3, items: new[] { "water_ration:20", "water_ration_small:20", "nutrient_tablet:15", "food_stamp:15" }, itemChance: 55f, refill: 2),
                    S("kitchen", "조리대", 0.48f, 0.64f, 1, "큰 솥과 국자. 아직 따뜻한 것 같기도.", new[] { "packedFoodTable", "householdTable" },
                        empty: 20f, minor: 4f, items: new[] { "kitchen_knife:10", "kitchen_cleaver:6", "meat_scrap:15", "nutrifruit:15" }, itemChance: 50f),
                    S("donations", "기부함", 0.72f, 0.50f, 1, "자물쇠가 부서진 기부함.", new[] { "householdTable", "junkTable" },
                        empty: 25f, items: new[] { "food_stamp:20", "lottery_ticket_old:10", "handnote:8", "gamblers_coin:2" }, itemChance: 55f),
                    S("clinic", "구호 천막", 0.86f, 0.70f, 2, "한쪽에 쳐 둔 구호 천막. 상비약 상자.", new[] { "medicalTable" },
                        empty: 20f, min: 1, max: 2, items: new[] { "bandage_item:25", "salve:15", "rubbing_alcohol:15", "water_tablet:15" }, itemChance: 60f, refill: 4)),

                L("church", "무너진 교회", "지붕이 반쯤 내려앉은 교회. 스테인드글라스 조각이 바닥에 반짝인다.",
                    S("pews", "신도석", 0.20f, 0.55f, 1, "기도 책과 헌금 봉투가 남은 신도석.", new[] { "householdTable", "junkTable" },
                        empty: 25f, min: 1, max: 2, items: new[] { "handnote:10", "postit:10", "glass_shard:15" }, itemChance: 45f, refill: 2),
                    S("altar", "제단", 0.46f, 0.66f, 2, "먼지 쌓인 제단. 성물은 이미 누가 가져갔다.", new[] { "householdTable" },
                        empty: 20f, items: new[] { "wine_bottle:15", "black_lamp:4", "gamblers_coin:3" }, itemChance: 55f),
                    S("rubble", "무너진 지붕 잔해", 0.70f, 0.48f, 2, "무너진 지붕 아래. 파내다 다칠 수 있다.", new[] { "materialTable", "junkTable" },
                        empty: 20f, minor: 14f, major: 4f, min: 1, max: 2, items: new[] { "scrap_metal:25", "metal_ingot:10", "glass_shard:20" }, itemChance: 50f),
                    Locked(S("crypt", "지하 납골당", 0.86f, 0.74f, 3, "녹슨 쇠문으로 막힌 납골당. 소문으로는 신부가 뭔가 숨겨 뒀다.", new[] { "householdTable", "allModuleTable", "medicalTable" },
                        empty: 8f, minor: 6f, min: 2, max: 3, items: new[] { "liquid_luck:5", "wine_bottle:15", "dossier:5", "luxury_supply_crate:2" }, itemChance: 45f, refill: 7), Crowbar, CrowbarText, false)),

                // ---- 4단계 (끝: 망원경 없음) ----
                L("checkpoint", "보안대 검문소 폐허", "습격으로 무너진 보안대 검문소. 무기고가 털린 뒤에도 뭔가 남았을지 모른다.",
                    S("booth", "검문 부스", 0.20f, 0.55f, 2, "총알 자국투성이 부스.", new[] { "makeshiftWeaponTable", "accessCardTable" },
                        empty: 20f, minor: 8f, items: new[] { "small_pistol_ammo:20", "small_pistol_ammo_nl:15", "stun_baton:6", "sec_slip:8" }, itemChance: 55f),
                    S("barracks", "숙소", 0.45f, 0.64f, 2, "간이침대와 개인 사물함.", new[] { "householdTable", "medicalTable" },
                        empty: 15f, min: 1, max: 2, items: new[] { "backpack_medium_military:4", "energy_drink:15", "hemostatic_bandage_item:10", "intel_sec:4" }, itemChance: 50f),
                    S("wreck", "불탄 차량", 0.70f, 0.50f, 2, "불에 탄 순찰차. 트렁크가 열려 있다.", new[] { "materialTable", "makeshiftWeaponTable" },
                        empty: 20f, minor: 10f, major: 4f, items: new[] { "flashbang_grenade:6", "smoke_grenade:6", "heavy_pistol_ammo:10", "scrap_metal:20" }, itemChance: 50f),
                    Locked(S("armory", "무기고", 0.86f, 0.72f, 4, "보안부 카드로 잠긴 무기고. 습격 때도 이 문은 못 뚫었다.", new[] { "makeshiftWeaponTable", "accessCardTable", "allModuleTable" },
                        empty: 0f, min: 2, max: 4, items: new[] { "revolver:4", "shotgun:3", "smg:2", "heavy_pistol_ammo_p:12", "stun_gun:5", "c4:1" }, itemChance: 60f, refill: 7),
                        new[] { "sec_keycard", "cmd_keycard" }, "보안부 키카드 잠금장치다. 보안부 키카드(또는 사령부 키카드)가 외출 가방에 있어야 한다.", true)),

                L("rebel_hideout", "혁명군 은신처 흔적", "급하게 버리고 떠난 혁명군 은신처. 벽에 구호가 남아 있다.",
                    S("bunks", "간이 침상", 0.20f, 0.55f, 1, "담요와 개인 짐.", new[] { "householdTable", "packedFoodTable" },
                        empty: 20f, min: 1, max: 2, items: new[] { "nutrient_tablet:15", "water_ration_small:15", "rev_codeword:5", "handnote:10" }, itemChance: 50f, refill: 3),
                    S("workbench", "무기 작업대", 0.45f, 0.62f, 2, "수제 총을 만들던 작업대.", new[] { "makeshiftWeaponTable", "materialTable" },
                        empty: 15f, minor: 8f, items: new[] { "handmade_pistol_frame:8", "heavy_handmade_pistol_frame:4", "gun_part:10", "printer_chip_handmade_pistol_frame:3", "pipe_weapon:8" }, itemChance: 55f),
                    S("medic", "응급 처치 구석", 0.70f, 0.50f, 1, "피 묻은 붕대 더미 옆 약 상자.", new[] { "medicalTable" },
                        empty: 20f, items: new[] { "hemostatic_bandage_item:15", "bandage_item:20", "phagimycin_pill:8", "blood_bag:6" }, itemChance: 60f, refill: 4),
                    Locked(S("stash", "숨긴 무기 상자", 0.86f, 0.72f, 3, "바닥 판자 아래 못질된 상자.", new[] { "makeshiftWeaponTable", "accessCardTable" },
                        empty: 5f, minor: 4f, min: 2, max: 3, items: new[] { "handmade_pistol:6", "heavy_handmade_pistol:3", "c4:1", "republic_transmitter:2", "small_pistol_ammo_p:10" }, itemChance: 55f, refill: 7), Crowbar, CrowbarText, false)),

                L("cartel_storage", "카르텔 창고", "카르텔이 쓰던 창고. 지키는 사람이 사라진 지금이 기회다.",
                    S("boxes", "포장 상자", 0.22f, 0.55f, 2, "'과일'이라고 적힌 상자. 과일은 아닐 것이다.", new[] { "householdTable", "materialTable" },
                        empty: 15f, minor: 6f, min: 1, max: 3, items: new[] { "dream_dust:12", "fanta_pill:12", "nightmare_dust:4", "zerochew:8" }, itemChance: 50f),
                    S("lab", "간이 실험실", 0.46f, 0.66f, 3, "약품 냄새가 진동하는 간이 실험실. 유독 가스 조심.", new[] { "materialTable", "medicalTable" },
                        empty: 15f, minor: 12f, major: 5f, items: new[] { "restricted_chemical:15", "common_chemical:20", "advanced_flux_agent:4", "evaporator:2" }, itemChance: 60f),
                    S("office", "회계 사무실", 0.70f, 0.50f, 1, "장부와 금고 잔해.", new[] { "accessCardTable", "junkTable" },
                        empty: 20f, items: new[] { "energy_credit:10", "deal_maker:4", "dossier:6", "gamblers_coin:3" }, itemChance: 55f),
                    Locked(S("vault", "현금 금고", 0.86f, 0.72f, 4, "벽에 박힌 금고. 지렛대로 경첩을 뜯어낼 수 있을지도.", new[] { "accessCardTable", "allModuleTable" },
                        empty: 5f, minor: 8f, major: 3f, min: 2, max: 3, items: new[] { "energy_credit_hyp:3", "energy_credit_ext:4", "luxury_supply_crate:4", "liquid_luck:4", "smg:2" }, itemChance: 65f, refill: 7), Crowbar, CrowbarText, false)),

                L("mansion", "상류층 저택 빈집", "상류층 구역의 빈 저택. 주인은 더 높은 곳으로 이사 갔다.",
                    S("salon", "응접실", 0.20f, 0.55f, 2, "고급 가구와 장식품.", new[] { "householdTable" },
                        empty: 15f, min: 1, max: 2, items: new[] { "black_lamp:6", "wine_bottle:15", "caviar:8", "luxury_supply_crate:3" }, itemChance: 55f),
                    S("cellar", "와인 저장고", 0.45f, 0.66f, 2, "먼지 앉은 와인 병이 줄지어 있다.", new[] { "householdTable", "packedFoodTable" },
                        empty: 10f, min: 1, max: 3, items: new[] { "wine_bottle:40", "wine_rack:3", "wine_superyeast:4", "nudka:10" }, itemChance: 80f, refill: 5),
                    S("dressing", "드레스룸", 0.70f, 0.50f, 1, "고급 옷과 화장품.", new[] { "householdTable" },
                        empty: 15f, items: new[] { "skincare_cream:15", "neuroactive_perfume:4", "pheromone_perfume:3", "kotton_fabric:15" }, itemChance: 60f),
                    Locked(S("study", "서재 금고", 0.86f, 0.72f, 4, "사령부 키카드로만 열리는 서재 금고.", new[] { "accessCardTable", "allModuleTable", "householdTable" },
                        empty: 0f, min: 2, max: 4, items: new[] { "luxury_supply_crate:6", "hypertech_electronic:4", "energy_credit_hyp:3", "liquid_luck:5", "aug_chip:4" }, itemChance: 65f, refill: 7),
                        new[] { "cmd_keycard" }, "사령부 키카드 잠금장치다. 사령부 키카드가 외출 가방에 있어야 한다.", true)),

                L("hospital", "폐병원", "환자들이 한꺼번에 옮겨 간 뒤 버려진 병원. 복도 끝에서 바람 소리가 난다.",
                    S("ward", "병동", 0.20f, 0.55f, 2, "빈 병상과 링거 걸이.", new[] { "medicalTable", "householdTable" },
                        empty: 15f, min: 1, max: 2, items: new[] { "bandage_item:20", "blood_bag:10", "med_bottle_blue:8", "med_bottle_violet:6" }, itemChance: 60f),
                    S("pharmacy", "원내 약국", 0.45f, 0.64f, 2, "약품 선반이 쓰러져 있다.", new[] { "medicalTable" },
                        empty: 10f, minor: 6f, min: 1, max: 3, items: new[] { "phagimycin_pill:15", "pink_injector:10", "black_injector:5", "large_purple_injector:3", "zyanide_pill:2" }, itemChance: 65f, refill: 4),
                    S("surgery", "수술실", 0.70f, 0.50f, 3, "수술 도구가 흩어진 수술실. 날카로운 것 투성이.", new[] { "medicalTable", "materialTable" },
                        empty: 15f, minor: 12f, major: 3f, items: new[] { "surgery_tool:15", "xray_scanner:3", "aug_scanner:3", "hand:2" }, itemChance: 55f),
                    Locked(S("morgue", "연구 병동", 0.86f, 0.72f, 4, "연구부 카드로 잠긴 격리 연구 병동.", new[] { "medicalTable", "allModuleTable", "accessCardTable" },
                        empty: 0f, minor: 6f, major: 4f, min: 2, max: 4, items: new[] { "pure_white_injector:5", "serum_red:4", "serum_green:4", "mind_reprogrammer:1", "desequencer:2" }, itemChance: 60f, refill: 7),
                        new[] { "sci_keycard", "cmd_keycard" }, "연구부 키카드 잠금장치다. 연구부 키카드(또는 사령부 키카드)가 외출 가방에 있어야 한다.", true)),
            };
            return list;
        }

        // 망원경 지점 (해금 연결이 있는 장소에만 붙인다)
        internal static FarmSpot Telescope(string key)
        {
            return new FarmSpot
            {
                Key = "telescope", Name = "망원경", Area = new Rect(0.92f, 0.86f, 0f, 0f), Cost = 2, Telescope = true,
                Unlocks = Chain.TryGetValue(key, out string[] u) ? u : new string[0],
                Desc = "높은 곳에 걸린 낡은 망원경. 여기서 내려다보면 근처에 들어갈 만한 곳이 보일 것이다.",
                RefillDays = 0,
            };
        }
    }
}
