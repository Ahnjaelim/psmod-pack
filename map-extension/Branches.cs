using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace MapExtension
{
    // 분점 (방치형 시뮬레이션)
    //  1) 지도에서 부지를 산다.
    //  2) 본점에서 "분점" 버튼 → 분점별 가상 재고 창에 물건을 넣는다 (본점에서는 사라짐, 세이브 DB 에 저장).
    //  3) 매일 아침(PlayerStore.BeginDay) 위치별 손님이 와서 재고를 사 간다 → 판 돈이 들어오고 로그가 남는다.
    internal class BranchSite
    {
        public string Key;
        public string Name;
        public string Desc;
        public Vector2 Pos;          // 지도 위치 (0~1, 왼쪽 위 기준)
        public int Price;            // 개설 비용
        public int VisitorsMin, VisitorsMax;
        public float PriceMult = 1f; // 판매가 = 감정가 × 배율
        public (string type, int weight)[] Visitors; // 손님 종류 가중치 (BranchSim.Types 키)

        // 세이브 상태
        public bool Owned;
        public long Earned;          // 지금까지 번 돈
        public int LastIncome;       // 오늘 아침 매출
        public int Attractiveness;   // 분점 자체 매력도 (본점과 별개, 처음 0). 손님 수 배율 = 매력도/400 (0.6~1.8)
        public readonly List<string> Logs = new List<string>(); // 최근 로그 (새 것이 앞)
        public bool AutoRestock;                 // 매일 아침 자동 채우기
        public bool HoldOnPriceDrop;             // 가격 하락 이벤트 중인 물건은 안 팔기
        public int FilterMode;                   // 자동 채우기 필터: 0 전부(손님이 좋아하는 것) / 1 허용 목록만 / 2 차단 목록 제외 전부
        public readonly List<string> FilterIds = new List<string>(); // 허용/차단 목록 (아이템 ID)

        // 가상 재고 (게임 상자 + 인벤토리 창)
        public GameItem StockBox;
        public GameInventory StockInv;
    }

    // 분점 안: 수색처와 같은 무대(임시 배경) + 돌아가기 패널. 바닥은 창고처럼 물건이 그대로 남는다.
    internal sealed class BranchLocation : FarmLocation
    {
        public BranchSite Site;

        public override string Id => "vme_branch_" + Site.Key;
        public override bool IsDiscovered() => Site != null && Site.Owned; // 산 분점만 갈 수 있음
        protected override bool RefillsEnergy => false;

        public override string GetLeaveWarning() => "바닥에 둔 물건은 " + Name + "에 그대로 보관해 둔다.";

        public override void Arrive()
        {
            base.Arrive();
            try
            {
                StoreUIManager.Instance.Notify(Name + " · 재고 " + Branches.StockCount(Site) + "개 · 오늘 아침 매출 " + Site.LastIncome + " ₵");
            }
            catch
            {
            }
        }
    }

    internal static class Branches
    {
        internal static readonly List<BranchSite> All = new List<BranchSite>
        {
            new BranchSite
            {
                Key = "lower", Name = "하층민 구역 분점", Pos = new Vector2(0.17f, 0.74f),
                Desc = "골목 끝 빈 점포. 싸구려 물건이 꾸준히 돈다.",
                Price = 6000, VisitorsMin = 5, VisitorsMax = 9, PriceMult = 0.8f,
                Visitors = new[] { ("worker", 40), ("family", 25), ("rebel", 15), ("broker", 10), ("believer", 10) },
            },
            new BranchSite
            {
                Key = "downtown", Name = "시내 분점", Pos = new Vector2(0.56f, 0.46f),
                Desc = "정거장 가는 길목의 작은 가게. 오가는 사람이 많다.",
                Price = 15000, VisitorsMin = 3, VisitorsMax = 6, PriceMult = 1f,
                Visitors = new[] { ("worker", 25), ("tourist", 25), ("security", 20), ("tech", 20), ("broker", 10) },
            },
            new BranchSite
            {
                Key = "upper", Name = "상류층 구역 분점", Pos = new Vector2(0.76f, 0.31f),
                Desc = "고층 건물 1층의 진열장. 손님은 적지만 한 번에 크게 쓴다.",
                Price = 40000, VisitorsMin = 1, VisitorsMax = 3, PriceMult = 1.3f,
                Visitors = new[] { ("noble", 45), ("cartel", 25), ("believer", 15), ("tourist", 15) },
            },
        };

        // 자동 채우기 필터 통과? (0: 손님이 좋아하는 것 전부 / 1: 허용 목록만 / 2: 손님이 좋아하는 것 중 차단 목록 빼고)
        internal static bool PassFilter(BranchSite b, GameItem i, int score)
        {
            string id = i.identifier ?? "";
            switch (b.FilterMode)
            {
                case 1: return b.FilterIds.Contains(id);          // 직접 고른 건 손님 취향과 상관없이
                case 2: return score > 0 && !b.FilterIds.Contains(id);
                default: return score > 0;
            }
        }

        internal const int StockW = 17, StockH = 10; // 잭슨 박사의 대형 보관함(storage_bay_large)과 같은 크기
        private const int MaxLogs = 7;
        private const string LastPaidKey = "branch/last_paid_day";
        private const string NamesKey = "branch/names"; // 기록 속 아이템 이름 → ID ("이름\u001fID" 를 \u001e 로 이음). 기록 표 그림용
        private static int lastPaidDay = -1;

        private static string Key(BranchSite b) => "branch/" + b.Key;
        private static string StockKey(BranchSite b) => "branch/" + b.Key + "/stock";
        private static string LogKey(BranchSite b) => "branch/" + b.Key + "/log";
        private static string AutoKey(BranchSite b) => "branch/" + b.Key + "/auto"; // "자동 채우기 켬(1/0)|m필터모드|허용/차단 ID,ID"

        // ---- 분점 장소 (지도에서 들어가기) ----
        private static List<BranchLocation> locations;
        internal static List<BranchLocation> Locations
        {
            get
            {
                if (locations == null)
                {
                    locations = new List<BranchLocation>();
                    foreach (BranchSite b in All)
                        locations.Add(new BranchLocation { Site = b, Key = "branch_" + b.Key, Name = b.Name, Desc = b.Desc });
                }
                return locations;
            }
        }

        internal static BranchLocation LocationOf(BranchSite b)
        {
            foreach (BranchLocation l in Locations) if (l.Site == b) return l;
            return null;
        }

        internal static bool AnyOwned()
        {
            foreach (BranchSite b in All) if (b.Owned) return true;
            return false;
        }

        // ---- 가상 재고 ----
        internal static GameInventory Stock(BranchSite b)
        {
            if (b.StockInv != null) return b.StockInv;
            (PixelWindow win, GameInventory inv) = DirectoryUtils.CreateInventoryWindow(StockW, StockH);
            try { win.SetTitle(b.Name + " 재고"); } catch { }
            GameItem box = ItemDirectory.CreateEmptyItem("vme_branch_stock").SetContentWindow(win).SetSpriteAndShape("Items/items_container", "evidence_box");
            box.name = b.Name + " 재고";
            box.EnableTag("IS_OWNED_TAG"); // 내 물건을 넣을 수 있게 (GeneralHelper.MayOwnershipAcceptItem)
            b.StockBox = box;
            b.StockInv = inv;
            return inv;
        }

        internal static int StockCount(BranchSite b)
        {
            if (b.StockInv == null) return 0;
            int n = 0;
            foreach (GameItem i in b.StockInv.childItems) n += Math.Max(1, i.unitCount);
            return n;
        }

        internal static void OpenStock(BranchSite b)
        {
            Stock(b);
            PixelWindow w = b.StockBox?.contentWindow;
            if (w == null) return;
            w.Show();
            w.Center();
            try { AudioManager.Instance.Play2("click_vintage"); } catch { }
        }

        // ---- 재고 자동 채우기: 본점 인벤토리에서 그 분점 손님들이 좋아하는 물건을 재고가 찰 때까지 옮긴다 ----
        // 안 건드리는 것: 고정(핀)한 물건, 내 것이 아닌 물건, 상자/가방 같은 용기, 기계
        internal static int AutoFill(BranchSite b)
        {
            GameInventory stock = Stock(b);
            GameInventory inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement as GameInventory : null;
            if (stock == null || inv == null) return 0;
            var cands = new List<(GameItem item, int score, long value)>();
            // 본점 인벤토리 + 그 안의 보관함/상자/냉장고 속까지 (다른 기계 안은 안 건드림, 고정한 용기 안도 안 건드림)
            List<GameItem> all;
            try { all = GraphUtils.FindAllChildrenType<GameItem>(inv, null, Descend); }
            catch { all = new List<GameItem>(inv.childItems); }
            foreach (GameItem i in all)
            {
                if (i == null) continue;
                try
                {
                    if (ItemPinHelper.IsPinned(i) || !GeneralHelper.IsItemOwned(i)) continue;
                    if (i.contentWindow != null || i.IsGameItemType("MACHINE")) continue;
                }
                catch { continue; }
                if (BranchSim.IsContraband(i)) continue; // 밀수품은 분점에서 안 팔리니 안 옮김
                int score = BranchSim.Score(b, i);
                if (!PassFilter(b, i, score)) continue;   // 필터 (호퍼식)
                long v = 0;
                try { v = i.GetCurrentValue(false, true, true, false, 0L); } catch { }
                cands.Add((i, score, v));
            }
            cands.Sort((x, y) => y.score != x.score ? y.score.CompareTo(x.score) : y.value.CompareTo(x.value));
            int moved = 0, fails = 0;
            foreach (var c in cands)
            {
                if (GraphUtils.TryAcceptAll(stock, c.item) == 0) { moved++; fails = 0; }
                else if (++fails >= 6) break; // 재고 칸이 다 찼다
            }
            return moved;
        }

        // 이 용기 안까지 찾아볼까: 고정 안 한 상자/가방/보관함, 냉장고처럼 물건 담는 기계만 (작업 기계 안의 부품·재료는 빼지 않는다)
        private static bool Descend(GameItem c)
        {
            try
            {
                if (c == null || ItemPinHelper.IsPinned(c)) return false;
                if (c.IsGameItemType("MACHINE"))
                    return c.IsTag("CONTAINER_TAG") || c.IsGameItemType("STORAGE");
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void CloseAllStock()
        {
            foreach (BranchSite b in All)
                try { b.StockBox?.contentWindow?.Hide(); } catch { }
        }

        // ---- 지도 툴팁 ----
        internal static string Tooltip(BranchSite b)
        {
            if (b.Owned)
                return b.Desc + "\n<color=#9ad6cf>운영 중</color> · 재고 " + StockCount(b) + "개"
                       + "\n오늘 아침 매출: " + b.LastIncome + " ₵ · 지금까지 " + b.Earned + " ₵"
                       + "\n<color=#9a9aa8>재고는 본점의 \"분점\" 버튼에서 넣는다</color>";
            return b.Desc + "\n개설 비용: " + b.Price + " ₵ · 손님 하루 " + b.VisitorsMin + "~" + b.VisitorsMax + "명"
                   + "\n<color=#9a9aa8>클릭해서 분점 내기</color>";
        }

        // ---- 지도에서 클릭: 사기 ----
        internal static void Click(BranchSite b)
        {
            if (b.Owned) return; // 산 분점은 지도에서 장소처럼 선택/이동
            int cash = PlayerStore.Instance != null ? PlayerStore.Instance.GetCash() : 0;
            if (cash < b.Price)
            {
                Notify("돈이 모자란다. (필요 " + b.Price + " ₵, 가진 돈 " + cash + " ₵)");
                return;
            }
            try { EmporiumAudioPlayer.Instance.audioButton1.Play(); } catch { }
            string body = "<b>" + b.Name + "</b>\n\n" + b.Desc
                          + "\n개설 비용: <color=#ff5a5a>" + b.Price + " ₵</color>"
                          + "\n손님 하루 " + b.VisitorsMin + "~" + b.VisitorsMax + "명 · 판매가 감정가 ×" + b.PriceMult.ToString("0.0#");
            Dialogs.Confirm(body, "구매", "취소", () => Buy(b));
        }

        private static void Buy(BranchSite b)
        {
            if (b.Owned || PlayerStore.Instance == null) return;
            if (PlayerStore.Instance.GetCash() < b.Price) { Notify("돈이 모자란다."); return; }
            ModHelper.AddCredit(-b.Price);
            b.Owned = true;
            b.Earned = 0;
            b.LastIncome = 0;
            b.Logs.Clear();
            Notify(b.Name + "을(를) 열었다. 본점의 \"분점\" 버튼으로 재고를 넣자.");
            TransitMap.Refresh();
        }

        // ---- 매일 아침 (PlayerStore.BeginDay) ----
        internal static void OnDayStart()
        {
            int day = StoreStation.Instance != null ? StoreStation.Instance.dayCounter : 0;
            if (day == lastPaidDay) return; // 같은 날 두 번 안 돌림
            lastPaidDay = day;
            int total = 0, count = 0;
            foreach (BranchSite b in All)
            {
                if (!b.Owned) continue;
                int sales = 0, filled = 0;
                if (b.AutoRestock)
                {
                    try { filled = AutoFill(b); }
                    catch (Exception ex) { Debug.LogWarning("[MapExtension] auto restock " + b.Key + ": " + ex.Message); }
                }
                try { sales = BranchSim.RunDay(b, day, b.AutoRestock ? filled : -1); }
                catch (Exception ex) { Debug.LogWarning("[MapExtension] branch sim " + b.Key + ": " + ex); }
                b.LastIncome = sales;
                b.Earned += sales;
                total += sales;
                count++;
            }
            if (count == 0) return;
            if (total > 0) ModHelper.AddCredit(total, false);
            Notify("분점 매출 +" + total + " ₵" + (count > 1 ? " (" + count + "곳)" : ""));
            Debug.Log("[MapExtension] branch sales day " + day + ": +" + total);
        }

        internal static void AddLog(BranchSite b, string text)
        {
            b.Logs.Insert(0, text);
            while (b.Logs.Count > MaxLogs) b.Logs.RemoveAt(b.Logs.Count - 1);
        }

        // ---- 세이브 DB ----
        //  branch/<key>        = "1|번 돈|오늘 매출" (산 분점만)
        //  branch/<key>/stock  = 재고 (게임 가방 저장 형식)
        //  branch/<key>/log    = 최근 로그 (\u001e 로 구분)
        //  branch/last_paid_day
        internal static void Write()
        {
            foreach (BranchSite b in All)
            {
                if (!b.Owned)
                {
                    ModHelper.RemoveModData(MapExtensionMod.ModId, Key(b));
                    ModHelper.RemoveModData(MapExtensionMod.ModId, StockKey(b));
                    ModHelper.RemoveModData(MapExtensionMod.ModId, LogKey(b));
                    ModHelper.RemoveModData(MapExtensionMod.ModId, AutoKey(b));
                    continue;
                }
                ModHelper.SetModData(MapExtensionMod.ModId, Key(b), "1|" + b.Earned + "|" + b.LastIncome + "|" + b.Attractiveness);
                string stock = null;
                if (b.StockInv != null && b.StockInv.childItems.Count > 0)
                    stock = PlayerStore.Instance.EncodeSaveItem("save_bag", (PixelElement)b.StockInv, b.StockBox.contentWindow);
                if (string.IsNullOrEmpty(stock)) ModHelper.RemoveModData(MapExtensionMod.ModId, StockKey(b));
                else ModHelper.SetModData(MapExtensionMod.ModId, StockKey(b), stock);
                ModHelper.SetModData(MapExtensionMod.ModId, AutoKey(b), (b.AutoRestock ? "1" : "0") + "|m" + b.FilterMode + "|" + string.Join(",", b.FilterIds) + "|h" + (b.HoldOnPriceDrop ? "1" : "0"));
                if (b.Logs.Count == 0) ModHelper.RemoveModData(MapExtensionMod.ModId, LogKey(b));
                else ModHelper.SetModData(MapExtensionMod.ModId, LogKey(b), string.Join("\u001e", b.Logs));
            }
            ModHelper.SetModData(MapExtensionMod.ModId, LastPaidKey, lastPaidDay.ToString());

            // 지금 남은 기록에 나오는 이름만 저장 (기록은 7일치라 금방 작아진다)
            var used = new List<string>();
            foreach (BranchSite b in All)
                foreach (string l in b.Logs)
                    foreach (var kv in BranchSim.NameToId)
                        if (l.Contains(kv.Key) && !used.Contains(kv.Key)) used.Add(kv.Key);
            if (used.Count == 0) ModHelper.RemoveModData(MapExtensionMod.ModId, NamesKey);
            else ModHelper.SetModData(MapExtensionMod.ModId, NamesKey,
                string.Join("\u001e", used.Select(n => n + "\u001f" + BranchSim.NameToId[n])));
        }

        internal static void Read()
        {
            int owned = 0;
            foreach (BranchSite b in All)
            {
                b.Owned = false;
                b.Earned = 0;
                b.LastIncome = 0;
                b.Attractiveness = 0;
                b.Logs.Clear();
                b.AutoRestock = false;
                b.HoldOnPriceDrop = false;
                b.FilterMode = 0;
                b.FilterIds.Clear();
                ResetStock(b);
                string v = ModHelper.GetModData(MapExtensionMod.ModId, Key(b));
                if (string.IsNullOrEmpty(v)) continue;
                string[] p = v.Split('|');
                b.Owned = p.Length > 0 && p[0] == "1";
                if (p.Length > 1) long.TryParse(p[1], out b.Earned);
                if (p.Length > 2) int.TryParse(p[2], out b.LastIncome);
                if (p.Length > 3) int.TryParse(p[3], out b.Attractiveness); // 예전 세이브엔 없음 → 0
                if (!b.Owned) continue;
                owned++;
                string stock = ModHelper.GetModData(MapExtensionMod.ModId, StockKey(b));
                if (!string.IsNullOrEmpty(stock)) PlayerStore.Instance.DecodeSaveItem(stock, Stock(b));
                string auto = ModHelper.GetModData(MapExtensionMod.ModId, AutoKey(b));
                if (!string.IsNullOrEmpty(auto))
                {
                    string[] ap = auto.Split('|');
                    b.AutoRestock = ap[0] == "1";
                    // "1|m<모드>|id,id" (예전 "1|<종류 비트>" 는 모드 0 으로)
                    if (ap.Length > 1 && ap[1].StartsWith("m") && int.TryParse(ap[1].Substring(1), out int mode)) b.FilterMode = mode;
                    if (ap.Length > 2)
                        foreach (string id in ap[2].Split(','))
                            if (!string.IsNullOrEmpty(id) && !b.FilterIds.Contains(id)) b.FilterIds.Add(id);
                    if (ap.Length > 3) b.HoldOnPriceDrop = ap[3] == "h1";
                }
                string log = ModHelper.GetModData(MapExtensionMod.ModId, LogKey(b));
                if (!string.IsNullOrEmpty(log)) b.Logs.AddRange(log.Split('\u001e'));
            }
            string d = ModHelper.GetModData(MapExtensionMod.ModId, LastPaidKey);
            lastPaidDay = int.TryParse(d, out int day) ? day : -1;
            string names = ModHelper.GetModData(MapExtensionMod.ModId, NamesKey);
            if (!string.IsNullOrEmpty(names))
                foreach (string pair in names.Split('\u001e'))
                {
                    int i = pair.IndexOf('\u001f');
                    if (i > 0 && i < pair.Length - 1) BranchSim.NameToId[pair.Substring(0, i)] = pair.Substring(i + 1);
                }
            BranchMonitor.Prewarm(); // 기록 표 아이템 그림을 미리 (여러 프레임에 나눠)
            Debug.Log("[MapExtension] save db read branches: owned " + owned);
        }

        private static void ResetStock(BranchSite b)
        {
            if (b.StockInv == null) return;
            try
            {
                b.StockBox?.contentWindow?.Hide();
                foreach (GameItem i in new List<GameItem>(b.StockInv.childItems)) { b.StockInv.Expel(i); i.Destroy(); }
            }
            catch
            {
            }
        }

        private static void Notify(string text)
        {
            try { StoreUIManager.Instance.Notify(text); } catch { Debug.Log("[MapExtension] " + text); }
        }
    }

    // ---- 손님 시뮬레이션 ----
    internal static class BranchSim
    {
        internal class VisitorType
        {
            public string Name;
            public int BudgetMin, BudgetMax;
            public string[] Likes; // 게임 아이템 종류 (GameItem.GetGameItemType)
            public string Faction; // 게임 세력 (평판이 이 손님 빈도에 영향)
        }

        internal static readonly Dictionary<string, VisitorType> Types = new Dictionary<string, VisitorType>
        {
            { "worker",   new VisitorType { Name = "하층민 노동자", BudgetMin = 20,  BudgetMax = 120,  Likes = new[] { "FOOD", "PROCESSED_FOOD", "WATER", "WATER_RATION", "MEDICAL", "SUBSTANCE", "ALCOHOL", "HOUSEHOLD_GOOD" }, Faction = "FACTION_LOWER_LEVEL" } },
            { "family",   new VisitorType { Name = "하층민 가족",   BudgetMin = 30,  BudgetMax = 160,  Likes = new[] { "FOOD", "FRESH_FOOD", "PROCESSED_FOOD", "WATER", "MEDICAL", "TREAT", "HOUSEHOLD_GOOD" }, Faction = "FACTION_LOWER_LEVEL" } },
            { "rebel",    new VisitorType { Name = "혁명군 청년",   BudgetMin = 50,  BudgetMax = 300,  Likes = new[] { "WEAPON", "AMMUNITION", "MUNITION", "MEDICAL", "TOOL", "ALCOHOL", "SUBSTANCE" }, Faction = "FACTION_REVOLUTION" } },
            { "broker",   new VisitorType { Name = "암시장 중개인", BudgetMin = 100, BudgetMax = 600,  Likes = new[] { "NARCOTIC", "WEAPON", "FIREARM", "MODULE", "ORGAN", "SUBSTANCE" }, Faction = "FACTION_BLACK_MARKET" } },
            { "believer", new VisitorType { Name = "교회 신자",     BudgetMin = 40,  BudgetMax = 300,  Likes = new[] { "FOOD", "WATER", "MEDICAL", "ART", "HOUSEHOLD_GOOD" }, Faction = "FACTION_CHURCH" } },
            { "tourist",  new VisitorType { Name = "관광객",        BudgetMin = 60,  BudgetMax = 450,  Likes = new[] { "LUXURY_ITEM", "ART", "TREAT", "BREVAGE", "ALCOHOL", "HOUSEHOLD_GOOD" }, Faction = "FACTION_TOURIST" } },
            { "security", new VisitorType { Name = "보안대원",      BudgetMin = 80,  BudgetMax = 400,  Likes = new[] { "WEAPON", "FIREARM", "AMMUNITION", "ARMOR", "MEDICAL", "ALCOHOL", "SUBSTANCE" }, Faction = "FACTION_SECURITY" } },
            { "tech",     new VisitorType { Name = "기술자",        BudgetMin = 80,  BudgetMax = 500,  Likes = new[] { "TOOL", "MATERIAL", "BASIC_MATERIAL", "REFINED_MATERIAL", "MODULE", "ENERGY_CELL", "MACHINE" }, Faction = "FACTION_LOWER_LEVEL" } },
            { "noble",    new VisitorType { Name = "상류층 손님",   BudgetMin = 300, BudgetMax = 2500, Likes = new[] { "LUXURY_ITEM", "ART", "ALCOHOL", "TREAT", "FRESH_FOOD" }, Faction = "FACTION_UPPER_LEVEL" } },
            { "cartel",   new VisitorType { Name = "카르텔 간부",   BudgetMin = 400, BudgetMax = 3000, Likes = new[] { "WEAPON", "FIREARM", "NARCOTIC", "LUXURY_ITEM", "ARMOR", "ALCOHOL", "SUBSTANCE" }, Faction = "FACTION_CARTEL" } },
        };

        // 하루치: 손님이 와서 재고를 사 간다. 매출을 돌려준다.
        internal static int RunDay(BranchSite b, int day, int restocked = -1)
        {
            GameInventory stock = Branches.Stock(b);
            float attr = AttractivenessMult(b, out int attrValue);
            int visitors = Mathf.Max(0, Mathf.RoundToInt(UnityEngine.Random.Range(b.VisitorsMin, b.VisitorsMax + 1) * attr));
            var lines = new List<string>();
            int sales = 0, buyers = 0;
            var evCache = new Dictionary<GameItem, int>(); // 물건별 오늘 이벤트 % (하루 한 번 계산)

            for (int v = 0; v < visitors; v++)
            {
                VisitorType t = PickType(b);
                if (t == null) continue;
                int budget = UnityEngine.Random.Range(t.BudgetMin, t.BudgetMax + 1);
                int wallet = budget; // 기록용: 들고 온 돈
                int liked = 0, cheapest = int.MaxValue; // 기록용: 빈손 이유
                var bought = new List<string>();
                // 유행: 가격 상승 이벤트(+10% 이상) 물건은 종류가 안 맞아도 이 손님이 관심을 가질 수 있다 (확률 = 이벤트%/100, 최대 50%)
                var fad = new HashSet<GameItem>();
                foreach (GameItem i in stock.childItems)
                {
                    if (i == null || Likes(t, i)) continue;
                    int e = Ev(evCache, i);
                    if (e >= 10 && UnityEngine.Random.value < Mathf.Min(0.5f, e / 100f)) fad.Add(i);
                }
                for (int pick = 0; pick < 2 && budget > 0; pick++) // 한 손님이 최대 두 가지
                {
                    var cands = new List<(GameItem item, int unit, int ev, float w)>();
                    float wsum = 0f;
                    foreach (GameItem i in stock.childItems)
                    {
                        if (i == null || IsContraband(i)) continue;          // 밀수품은 분점에서 절대 안 팔림
                        if (!Likes(t, i) && !fad.Contains(i)) continue;
                        int e = Ev(evCache, i);
                        if (b.HoldOnPriceDrop && e < 0) continue;             // 가격 하락 이벤트 중이면 안 팖
                        int unit = UnitPrice(b, i);
                        if (unit <= 0) continue;
                        if (pick == 0) { liked++; if (unit < cheapest) cheapest = unit; }
                        // 가격 상승 이벤트 물건엔 돈을 그만큼 더 쓸 마음이 있다
                        int stretch = e > 0 ? Mathf.RoundToInt(budget * (1f + e / 100f)) : budget;
                        if (unit > stretch) continue;
                        // 이벤트 물건 선호: 상승이면 더 자주, 하락이면 덜 고른다
                        float w = Mathf.Clamp(1f + e / 50f, 0.3f, 4f);
                        cands.Add((i, unit, e, w));
                        wsum += w;
                    }
                    if (cands.Count == 0) break;
                    var c = cands[cands.Count - 1];
                    float r = UnityEngine.Random.Range(0f, wsum);
                    foreach (var x in cands) { if (r < x.w) { c = x; break; } r -= x.w; }
                    int spend = c.ev > 0 ? Mathf.RoundToInt(budget * (1f + c.ev / 100f)) : budget;
                    int have = Math.Max(1, c.item.unitCount);
                    int qty = Math.Min(have, Math.Min(spend / c.unit, UnityEngine.Random.Range(1, 4)));
                    if (qty <= 0) break;
                    string name = Name(c.item);
                    RememberId(name, c.item); // 기록 표에서 아이템 그림을 찾을 수 있게
                    bool isFad = fad.Contains(c.item);
                    int paid = c.unit * qty;
                    budget = Math.Max(0, budget - paid);
                    sales += paid;
                    if (qty >= have) { stock.Expel(c.item); c.item.Destroy(); }
                    else c.item.SetUnitCount(have - qty);
                    bought.Add(name + (qty > 1 ? " ×" + qty : "") + " (" + paid + " ₵"
                               + (c.ev != 0 ? ", 이벤트 " + (c.ev > 0 ? "+" : "") + c.ev + "%" : "")
                               + (isFad ? ", 유행" : "") + ")");
                }
                string who = " - " + t.Name + " (소지금 " + wallet + " ₵): ";
                if (bought.Count > 0) { buyers++; lines.Add(who + string.Join(", ", bought)); }
                else if (liked == 0) lines.Add(who + "둘러보고 그냥 감 (마음에 드는 게 없음)");
                else lines.Add(who + "둘러보고 그냥 감 (너무 비쌈: 제일 싼 게 " + cheapest + " ₵)");
            }

            var sb = new StringBuilder();
            sb.Append("[").Append(day).Append("일차] 손님 ").Append(visitors).Append("명 (매력도 ").Append(attrValue).Append(", ×").Append(attr.ToString("0.0#")).Append(") · 구매 ").Append(buyers)
              .Append("명 · 매출 ").Append(sales).Append(" ₵");
            if (stock.childItems.Count == 0) sb.Append(" · 재고 없음");
            if (restocked >= 0) sb.Append("\n - 아침 자동 채우기: ").Append(restocked).Append("개");
            foreach (string l in lines) sb.Append('\n').Append(l);
            Branches.AddLog(b, sb.ToString());
            return sales;
        }

        // 밀수품: 게임 밀수품 표시(CONTRABAND_ITEM_TAG) 또는 종류 CONTRABAND
        internal static bool IsContraband(GameItem i)
        {
            try { return ContrabandHelper.IsContraband(i) || i.IsGameItemType("CONTRABAND"); }
            catch { return false; }
        }

        // 이 분점 손님들이 이 물건을 얼마나 원하나 (좋아하는 손님 종류의 가중치 합)
        internal static int Score(BranchSite b, GameItem i)
        {
            float score = 0f;
            foreach (var w in b.Visitors)
                if (Types.TryGetValue(w.type, out VisitorType t) && Likes(t, i)) score += Weight(w.type, w.weight);
            return Mathf.RoundToInt(score);
        }

        // 손님 빈도 = 기본 가중치 × 그 세력 평판 배율
        internal static float Weight(string type, int baseWeight)
        {
            return Types.TryGetValue(type, out VisitorType t) ? baseWeight * RepMult(t.Faction) : baseWeight;
        }

        // 세력 평판(-200~200) → 1 + 평판/200, 0.25~2 배
        internal static float RepMult(string faction)
        {
            if (string.IsNullOrEmpty(faction)) return 1f;
            try
            {
                StoreReputation r = StoreReputation.GetStoreReputation(faction);
                if (r == null) return 1f;
                return Mathf.Clamp(1f + (float)r.GetReputationExact() / 200f, 0.25f, 2f);
            }
            catch
            {
                return 1f;
            }
        }

        // 분점 매력도 → 손님 수 배율 (매력도 / 400, 0.6~1.8). 게임 기준: 400 이상 2등급, 650 이상 3등급.
        // 예전엔 본점 매력도를 썼음. 지금은 분점마다 따로 (처음 0 → 최저 배율 0.6)
        internal static float AttractivenessMult(BranchSite b, out int value)
        {
            value = b != null ? b.Attractiveness + BranchRenovation.Bonus(b) : 0; // 기본 0 + 와일드 네트워크 분점 개보수
            return Mathf.Clamp(value / 400f, 0.6f, 1.8f);
        }

        private static VisitorType PickType(BranchSite b)
        {
            float sum = 0f;
            foreach (var w in b.Visitors) sum += Weight(w.type, w.weight);
            float r = UnityEngine.Random.Range(0f, sum);
            foreach (var w in b.Visitors)
            {
                float wt = Weight(w.type, w.weight);
                if (r < wt) return Types.TryGetValue(w.type, out VisitorType t) ? t : null;
                r -= wt;
            }
            return null;
        }

        // 모니터 표시용: 이 분점 손님 구성 (세력 평판 반영 비율 %)
        internal static string Mix(BranchSite b)
        {
            float sum = 0f;
            foreach (var w in b.Visitors) sum += Weight(w.type, w.weight);
            if (sum <= 0f) return "";
            var parts = new List<string>();
            foreach (var w in b.Visitors)
                if (Types.TryGetValue(w.type, out VisitorType t))
                    parts.Add(t.Name + " " + Mathf.RoundToInt(Weight(w.type, w.weight) * 100f / sum) + "%");
            return string.Join(" · ", parts);
        }

        internal static bool Likes(VisitorType t, GameItem i)
        {
            List<string> types;
            try { types = i.GetGameItemType(); } catch { return false; }
            if (types == null) return false;
            foreach (string like in t.Likes)
                if (types.Contains(like)) return true;
            return false;
        }

        private static int Ev(Dictionary<GameItem, int> cache, GameItem i)
        {
            if (!cache.TryGetValue(i, out int e)) { e = EventPercent(i); cache[i] = e; }
            return e;
        }

        // 오늘 이벤트(게임 시세 이벤트)로 이 물건 값이 몇 % 바뀌었나 (0 = 영향 없음)
        internal static int EventPercent(GameItem i)
        {
            try
            {
                long without = i.GetCurrentValue(true, true, false, false, 0L);
                long with = i.GetCurrentValue(true, true, true, false, 0L);
                if (without <= 0 || with == without) return 0;
                return (int)Math.Round((with - without) * 100.0 / without);
            }
            catch
            {
                return 0;
            }
        }

        // 판매가 = 게임 판매가(소매 마진 + 오늘 이벤트 시세 포함, 가게 진열가와 같은 계산) × 위치 배율
        internal static int UnitPrice(BranchSite b, GameItem i)
        {
            long v;
            try { v = i.GetCurrentValue(true, true, true, false, 0L); } catch { v = i.unitValue; }
            return (int)Math.Max(0, Math.Round(v * b.PriceMult));
        }

        // 기록에 남는 표시 이름 → 아이템 ID (이번 실행 동안만. 기록 표의 아이템 그림용)
        internal static readonly Dictionary<string, string> NameToId = new Dictionary<string, string>();

        internal static void RememberId(string name, GameItem i)
        {
            try
            {
                if (!string.IsNullOrEmpty(name) && i != null && !string.IsNullOrEmpty(i.identifier)) NameToId[name] = i.identifier;
            }
            catch
            {
            }
        }

        internal static string Name(GameItem i)
        {
            try { return i.GetDisplayName(true); } catch { return i.name; }
        }
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.BeginDay))]
    internal static class BeginDayPatch
    {
        private static void Postfix()
        {
            try { Branches.OnDayStart(); }
            catch (Exception ex) { Debug.LogWarning("[MapExtension] branch day: " + ex.Message); }
        }
    }
}
