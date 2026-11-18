using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace CalendarTodayTooltip
{
    // 가게 벽 달력(StoreCalendar)에 마우스를 올렸을 때 나오는 "달력 확인" 툴팁 아래에
    // 오늘 진행 중인 이벤트를 정리해서 보여준다.
    //   • 이벤트 이름 (~끝나는 날)
    //       가격 영향 (+20% 무기 등)
    // 달력 화면과 같은 기준(StoreEventManager.GetActiveEventOnDay: 알려진 이벤트만, 장식용 제외)을 쓴다.
    // GetTooltip 은 마우스를 올린 동안 매 프레임 불릴 수 있어서 결과를 잠깐 캐시한다.
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.

    // 벽 달력 화면(dayTMP)에도 오늘 이벤트 이름을 바로 띄운다. 원본 Update 가 매 프레임 텍스트를 덮어쓰므로 Postfix 에서 덧붙인다.
    // 화면이 작아서 자동 글자 크기(최대 = 원래 크기)를 켜고, 이벤트 문자열은 1초마다만 다시 만든다.
    [HarmonyPatch(typeof(StoreCalendar), "Update")]
    internal static class StoreCalendarScreenPatch
    {
        private static string _cached;
        private static float _cachedAt = -10f;
        private static int _cachedDay = -1;
        private static bool _sizingSet;

        private static void Postfix(StoreCalendar __instance)
        {
            try
            {
                var tmp = __instance.dayTMP;
                if (tmp == null || StoreStation.Instance == null) return;
                int today = StoreStation.Instance.dayCounter;
                if (_cached == null || _cachedDay != today || Time.unscaledTime - _cachedAt > 1f)
                {
                    _cached = BuildScreenLines(today);
                    _cachedDay = today;
                    _cachedAt = Time.unscaledTime;
                }
                if (string.IsNullOrEmpty(_cached)) return;

                if (!_sizingSet)
                {
                    float max = tmp.fontSize;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMax = max;
                    tmp.fontSizeMin = Mathf.Max(6f, max * 0.4f);
                    _sizingSet = true;
                }
                string baseText = tmp.text ?? "";
                tmp.text = baseText.TrimEnd('\n') + "\n" + _cached;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CalendarTodayScreen] failed: " + ex.Message);
            }
        }

        private static string BuildScreenLines(int today)
        {
            StoreEventManager manager = StoreStation.Instance?.storeEventManager;
            List<StoreEvent> events = manager?.GetActiveEventOnDay(today);
            List<string> visitors = GunsmithCalendar.Visitors.On(today);
            if ((events == null || events.Count == 0) && visitors.Count == 0) return null;
            var sb = new StringBuilder("<size=80%>");
            var seen = new HashSet<StoreEvent>();
            bool first = true;
            foreach (string v in visitors)
            {
                if (!first) sb.Append('\n');
                sb.Append("- ").Append(v);
                first = false;
            }
            if (events == null) events = new List<StoreEvent>();
            foreach (StoreEvent ev in events)
            {
                if (ev == null || !seen.Add(ev)) continue;
                if (!first) sb.Append('\n');
                sb.Append("- ").Append(StoreCalendarTooltipPatch.SafeName(ev));
                first = false;
            }
            sb.Append("</size>");
            return first ? null : sb.ToString();
        }
    }

    [HarmonyPatch(typeof(StoreCalendar), nameof(StoreCalendar.GetTooltip))]
    internal static class StoreCalendarTooltipPatch
    {
        private static string _cached;
        private static float _cachedAt = -10f;
        private static int _cachedDay = -1;

        private static void Postfix(ref string __result)
        {
            try
            {
                int today = StoreStation.Instance != null ? StoreStation.Instance.dayCounter : -1;
                if (today < 0) return;
                if (_cached == null || _cachedDay != today || Time.unscaledTime - _cachedAt > 1f)
                {
                    _cached = BuildSummary(today);
                    _cachedDay = today;
                    _cachedAt = Time.unscaledTime;
                }
                if (!string.IsNullOrEmpty(_cached))
                    __result = (__result ?? "") + "\n" + _cached;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CalendarTodayTooltip] failed: " + ex.Message);
            }
        }

        private static string BuildSummary(int today)
        {
            StoreEventManager manager = StoreStation.Instance?.storeEventManager;
            if (manager == null) return null;

            List<StoreEvent> events = manager.GetActiveEventOnDay(today);
            var sb = new StringBuilder();
            sb.Append("<size=85%>");
            sb.Append("<b>").Append(PenLabelerAssist.Text.Get("calendar_today_header")).Append("</b>");

            List<string> visitors = GunsmithCalendar.Visitors.On(today);
            if (visitors.Count > 0)
                sb.Append('\n').Append("<color=#b07a3a>").Append(PenLabelerAssist.Text.Get("visitor_cal_today", string.Join(", ", visitors.ToArray()))).Append("</color>");
            string gunsmithLine = GunsmithCalendar.Visitors.GunsmithNextLine(today);
            if (!string.IsNullOrEmpty(gunsmithLine))
                sb.Append('\n').Append("<color=#b07a3a>").Append(gunsmithLine).Append("</color>");

            if (events == null || events.Count == 0)
            {
                sb.Append('\n').Append(PenLabelerAssist.Text.Get("calendar_today_none"));
                sb.Append("</size>");
                return sb.ToString();
            }

            var seen = new HashSet<StoreEvent>();
            foreach (StoreEvent ev in events)
            {
                if (ev == null || !seen.Add(ev)) continue;

                string name = SafeName(ev);
                sb.Append('\n').Append("- ").Append(name);

                int lastDay = LastDay(manager, ev, today);
                if (lastDay > today)
                    sb.Append(" <color=#8a8a8a>(").Append(PenLabelerAssist.Text.Get("calendar_today_until", lastDay)).Append(")</color>");

                string effect = SafeEffect(ev);
                if (!string.IsNullOrEmpty(effect))
                {
                    foreach (string line in effect.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string t = line.Trim();
                        if (t.Length == 0) continue;
                        string color = t.StartsWith("+") ? "#c0504d" : (t.StartsWith("-") ? "#7a9a5a" : "#aaaaaa");
                        sb.Append('\n').Append("    <color=").Append(color).Append('>').Append(t).Append("</color>");
                    }
                }
            }
            sb.Append("</size>");
            return sb.ToString();
        }

        // 이벤트가 끝나는 날. startDay 가 있으면 그걸로 계산하고, 아직 시작 전(예약 이벤트)이면 하루씩 확인한다.
        private static int LastDay(StoreEventManager manager, StoreEvent ev, int today)
        {
            if (ev.startDay >= 0 && ev.totalDuration > 0)
                return ev.startDay + ev.totalDuration - 1;

            int last = today;
            int max = Math.Max(ev.totalDuration, 1);
            for (int d = today + 1; d < today + max; d++)
            {
                List<StoreEvent> list = manager.GetActiveEventOnDay(d);
                if (list == null || !list.Contains(ev)) break;
                last = d;
            }
            return last;
        }

        internal static string SafeName(StoreEvent ev)
        {
            try
            {
                string n = ev.GetDisplayName();
                n = LocHelper.RelocalizeIfMissing(n);
                return string.IsNullOrEmpty(n) ? "?" : n;
            }
            catch
            {
                return "?";
            }
        }

        private static string SafeEffect(StoreEvent ev)
        {
            try
            {
                return ev.negociationDatas != null && ev.negociationDatas.Count > 0 ? ev.GetEventPriceEffect() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
