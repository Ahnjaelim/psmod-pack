using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GunsmithCalendar
{
    // 정기 방문 손님을 달력에 표시한다. 조건은 게임 StoreClientManager 의 요일(날짜 % 7) 규칙을 그대로 옮겼다.
    // (0=일 1=월 2=화 3=수 4=목 5=금 6=토). 해금 여부는 "지금" 기준으로 판단한다.
    //  - 큰 달력(일정 화면) 날짜 칸 안쪽: 이벤트 아래에 배경 없이 손님 이름. 자리가 모자라면 "+N"
    //  - 날짜 칸에 마우스를 올리면 그날 이벤트 + 방문 손님 전체를 툴팁으로
    //  - 벽 달력 화면/툴팁: CalendarTodayTooltip 이 오늘 방문 손님을 덧붙인다
    internal static class Visitors
    {
        private sealed class Def
        {
            public string Key;
            public Func<int, bool> When;
        }

        private static PlayerStore PS => PlayerStore.Instance;
        private static bool Perk(string id) { try { return StoreReputation.IsPerkUnlocked(id); } catch { return false; } }
        private static bool Chemist() => PS.IsChemistStart() || NetworkUpgrade.IsRetiredChemistUnlocked();
        private static bool Cartel() => PS.startType == NewGameData.StartType.DRUG_DEALER && !PS.isCartelExited;

        private static readonly List<Def> Defs = new List<Def>
        {
            // 은퇴한 장인 계열
            new Def { Key = "retired_chemist",   When = d => d % 7 == 2 && Chemist() },
            new Def { Key = "retired_aquafarmer", When = d => d % 7 == 2 && NetworkUpgrade.IsAquacultureUnlocked() },
            new Def { Key = "retired_junker",    When = d => d % 7 == 3 && (PS.IsJunkerStart() || NetworkUpgrade.IsRuinedMachineUnlocked()) },
            new Def { Key = "retired_water",     When = d => d % 7 == 3 && (PS.IsWaterMerchantStart() || NetworkUpgrade.IsWaterTraderUnlocked()) },
            new Def { Key = "retired_gunsmith",  When = d => d % 7 == 4 && (PS.IsGunsmithStart() || NetworkUpgrade.IsRetiredGunsmithUnlocked()) },
            new Def { Key = "retired_farmer",    When = d => d % 7 == 5 && (PS.IsFarmerStart() || NetworkUpgrade.IsRetiredFarmerUnlocked()) },
            new Def { Key = "retired_winemaker", When = d => d % 7 == 5 && NetworkUpgrade.IsWinemakerUnlocked() },
            new Def { Key = "retired_rancher",   When = d => d % 7 == 5 && (PS.IsRancherStart() || NetworkUpgrade.IsRetiredRancherUnlocked()) },
            // 누구나
            new Def { Key = "supplier",          When = d => d % 7 == 0 },
            new Def { Key = "inventor",          When = d => d >= 12 && d % 7 == 5 && !Perk("LLAVOIDED") },
            new Def { Key = "flexi_buyer",       When = d => d % 7 == 6 || (d >= 50 && d % 7 == 3) },
            new Def { Key = "business_permit",   When = d => d >= 38 && d % 7 == 3 && !PS.hasBusinessPermit },
            // 평판 퍽
            new Def { Key = "sec_surplus",       When = d => d % 7 == 3 && Perk("SEC_SURPLUS") },
            new Def { Key = "sec_requisition",   When = d => d > 7 && d % 7 == 4 && Perk("SEC_REQUISITION") },
            new Def { Key = "ll_rare",           When = d => (d % 7 == 5 || d % 7 == 1) && Perk("LLRAREGOOD") },
            new Def { Key = "thief",             When = d => d % 7 == 1 && Perk("BM_RELIABLE") },
            new Def { Key = "rev_quartermaster", When = d => d % 7 == 0 && Perk("REV_QUARTERMASTER") },
            new Def { Key = "rev_raider",        When = d => d % 7 == 5 && Perk("REV_RAID_LOOT") },
            // 시작 직업/특성
            new Def { Key = "cartel_weekly",     When = d => d > 3 && d % 7 == 1 && Cartel() },
            new Def { Key = "cartel_checkin",    When = d => d > 3 && d % 7 == 4 && Cartel() },
            new Def { Key = "shady_farmer",      When = d => (d % 7 == 3 || d % 7 == 6) && Chemist() },
            new Def { Key = "dealer",            When = d => d > 7 && (d % 7 == 2 || d % 7 == 5) && Chemist() },
            new Def { Key = "blackmailer",       When = d => d % 7 == 1 && StartingPerk.IsBlackmail() },
            new Def { Key = "scalper",           When = d => d % 7 == 6 && StartingPerk.IsSentinel() },
        };

        internal static List<string> On(int day)
        {
            var list = new List<string>();
            if (day <= 0 || PS == null) return list;
            foreach (Def def in Defs)
            {
                bool ok;
                try { ok = def.When(day); } catch { ok = false; }
                if (ok) list.Add(PenLabelerAssist.Text.Get("visitor_" + def.Key));
            }
            return list;
        }

        // 벽 달력 툴팁용: 총기 장인이 해금돼 있고 오늘이 아니면 다음 방문일 한 줄
        internal static string GunsmithNextLine(int today)
        {
            try
            {
                if (PS == null || !(PS.IsGunsmithStart() || NetworkUpgrade.IsRetiredGunsmithUnlocked())) return null;
            }
            catch
            {
                return null;
            }
            if (today > 0 && today % 7 == 4) return null;
            for (int d = Math.Max(today + 1, 1); d < today + 8; d++)
                if (d % 7 == 4) return PenLabelerAssist.Text.Get("gunsmith_cal_next", d, d - today);
            return null;
        }
    }

    [HarmonyPatch(typeof(CalendarDayElement), nameof(CalendarDayElement.SetElement))]
    internal static class CalendarDayElementPatch
    {
        private const string LabelName = "VisitorLabel";

        private static void Postfix(CalendarDayElement __instance, int dayNumber, List<StoreEvent> events)
        {
            try
            {
                if (__instance == null || __instance.layoutGroup == null) return;
                List<string> visitors = Visitors.On(dayNumber);

                var hover = __instance.GetComponent<DayHover>() ?? __instance.gameObject.AddComponent<DayHover>();
                hover.Set(__instance, dayNumber, events, visitors);

                Transform parent = __instance.layoutGroup.transform;
                Transform existing = parent.Find(LabelName);
                if (visitors.Count == 0)
                {
                    if (existing != null) existing.gameObject.SetActive(false);
                    return;
                }
                TextMeshProUGUI label = existing != null ? existing.GetComponent<TextMeshProUGUI>() : Create(__instance, parent);
                if (label == null) return;

                int capacity = Capacity(__instance, label);
                var sb = new StringBuilder();
                int shown = visitors.Count <= capacity ? visitors.Count : Math.Max(0, capacity - 1);
                for (int i = 0; i < shown; i++)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(visitors[i]);
                }
                int rest = visitors.Count - shown;
                if (rest > 0)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append("+").Append(rest);
                }
                int lines = shown + (rest > 0 ? 1 : 0);

                label.gameObject.SetActive(true);
                label.transform.SetAsLastSibling();
                ScriptFontSwap.Apply(label, sb.ToString());
                var le = label.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.preferredHeight = LineHeight(label) * lines;
                    le.minHeight = le.preferredHeight;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[VisitorCalendar] " + ex.Message);
            }
        }

        private static float LineHeight(TextMeshProUGUI label) => label.fontSize * 1.3f;

        // 이벤트 막대가 차지하고 남은 높이에 들어가는 줄 수 (최소 1)
        private static int Capacity(CalendarDayElement day, TextMeshProUGUI label)
        {
            int activeEvents = 0;
            if (day.eventElements != null)
                foreach (CalendarEventElement e in day.eventElements)
                    if (e != null && e.gameObject.activeSelf) activeEvents++;

            var rt = day.layoutGroup.transform as RectTransform;
            float height = rt != null ? rt.rect.height : 0f;
            if (height <= 1f) return Math.Max(1, 4 - activeEvents);

            VerticalLayoutGroup lg = day.layoutGroup;
            float used = lg.padding.top + lg.padding.bottom;
            int children = 0;
            if (day.eventElements != null)
                foreach (CalendarEventElement e in day.eventElements)
                {
                    if (e == null || !e.gameObject.activeSelf) continue;
                    float h = LayoutUtility.GetPreferredHeight((RectTransform)e.transform);
                    used += h > 0f ? h : 38f;
                    children++;
                }
            used += lg.spacing * children;
            int n = (int)Math.Floor((height - used) / LineHeight(label));
            return Math.Max(1, n);
        }

        private static TextMeshProUGUI Create(CalendarDayElement day, Transform parent)
        {
            var go = new GameObject(LabelName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            TextMeshProUGUI src = day.dayTMP;
            if (src != null)
            {
                label.font = src.font;
                label.fontSharedMaterial = src.fontSharedMaterial;
                label.fontSize = Mathf.Max(8f, src.fontSize * 0.6f);
            }
            label.color = new Color32(0x6b, 0x45, 0x22, 0xff);
            label.alignment = TextAlignmentOptions.Top;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            go.AddComponent<LayoutElement>();
            return label;
        }
    }

    // 날짜 칸에 마우스를 올리면 달력 화면의 특성 툴팁 창을 빌려 그날 이벤트/방문 손님 전체를 보여준다.
    internal class DayHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private string title;
        private string body;
        private bool showing;

        internal void Set(CalendarDayElement day, int dayNumber, List<StoreEvent> events, List<string> visitors)
        {
            title = LocHelper.GetLocalizedMechanic("mech_day_label", dayNumber);
            var sb = new StringBuilder();
            var seen = new HashSet<StoreEvent>();
            if (events != null)
                foreach (StoreEvent ev in events)
                {
                    if (ev == null || !seen.Add(ev)) continue;
                    if (sb.Length == 0) sb.Append("<b>").Append(PenLabelerAssist.Text.Get("visitor_cal_events")).Append("</b>");
                    sb.Append("\n- ").Append(CalendarTodayTooltip.StoreCalendarTooltipPatch.SafeName(ev));
                }
            if (visitors != null && visitors.Count > 0)
            {
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append("<b>").Append(PenLabelerAssist.Text.Get("visitor_cal_visitors")).Append("</b>");
                foreach (string v in visitors) sb.Append("\n- ").Append(v);
            }
            body = sb.ToString();
            if (showing) Fill();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(body)) return;
            try
            {
                showing = true;
                Fill();
                PerkUIController.Instance.ShowTooltip();
                Follow();
            }
            catch
            {
                showing = false;
            }
        }

        public void OnPointerExit(PointerEventData eventData) => Hide();

        private void OnDisable() => Hide();

        private void Update()
        {
            if (showing) Follow();
        }

        private void Fill()
        {
            PerkUIController p = PerkUIController.Instance;
            if (p == null) return;
            if (p.startingPerkTooltipTitle != null) ScriptFontSwap.Apply(p.startingPerkTooltipTitle, title);
            if (p.startingPerkTooltipDescription != null) ScriptFontSwap.Apply(p.startingPerkTooltipDescription, body);
            if (p.startingPerkTooltipCost != null) p.startingPerkTooltipCost.text = "";
            if (p.startingPerkTooltipIncompatible != null) p.startingPerkTooltipIncompatible.text = "";
        }

        private static void Follow()
        {
            PerkUIController p = PerkUIController.Instance;
            if (p == null || p.startingPerkTooltip == null) return;
            Vector3 pos = UIHelper.GetTooltipPositionSimple();
            pos.z = p.startingPerkTooltip.transform.position.z;
            p.startingPerkTooltip.transform.position = pos;
        }

        private void Hide()
        {
            if (!showing) return;
            showing = false;
            try
            {
                PerkUIController.Instance?.HideTooltip();
            }
            catch
            {
            }
        }
    }
}
