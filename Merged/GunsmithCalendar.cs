using System;
using HarmonyLib;
using UnityEngine;

namespace GunsmithCalendar
{
    // 은퇴한 총기 장인 방문일을 달력에 표시한다.
    // 게임 규칙 (StoreClientManager): 건스미스 시작이거나 "Gunsmith" 네트워크 업그레이드 해금 시, 날짜 % 7 == 4 인 날(0일 제외) 방문.
    //  - 큰 달력(일정 화면) 날짜 칸: 방문일에 "총기 장인" 표시
    //  - 벽 달력 화면/툴팁: CalendarTodayTooltip 이 Visit 줄을 덧붙인다
    internal static class Gunsmith
    {
        internal static bool Unlocked
        {
            get
            {
                try
                {
                    return (PlayerStore.Instance != null && PlayerStore.Instance.IsGunsmithStart()) || NetworkUpgrade.IsRetiredGunsmithUnlocked();
                }
                catch
                {
                    return false;
                }
            }
        }

        internal static bool VisitsOn(int day) => day > 0 && day % 7 == 4;

        internal static int NextVisit(int today)
        {
            for (int d = Math.Max(today, 1); d < today + 8; d++)
                if (VisitsOn(d)) return d;
            return -1;
        }

        internal static string Name() => PenLabelerAssist.Text.Get("gunsmith_cal_name");

        // 벽 달력 툴팁용 한 줄: "총기 장인: 오늘 방문" / "총기 장인: 11일 (3일 후)"
        internal static string SummaryLine(int today)
        {
            if (!Unlocked) return null;
            if (VisitsOn(today)) return PenLabelerAssist.Text.Get("gunsmith_cal_today");
            int next = NextVisit(today);
            return next < 0 ? null : PenLabelerAssist.Text.Get("gunsmith_cal_next", next, next - today);
        }
    }

    [HarmonyPatch(typeof(CalendarDayElement), nameof(CalendarDayElement.SetElement))]
    internal static class CalendarDayElementPatch
    {
        private static void Postfix(CalendarDayElement __instance, int dayNumber)
        {
            try
            {
                if (__instance == null || __instance.dayTMP == null || !Gunsmith.VisitsOn(dayNumber) || !Gunsmith.Unlocked) return;
                __instance.dayTMP.text = (__instance.dayTMP.text ?? "") + " <size=75%><color=#b07a3a>" + Gunsmith.Name() + "</color></size>";
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GunsmithCalendar] " + ex.Message);
            }
        }
    }
}
