using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private const string LabelName = "GunsmithVisitLabel";

        // 날짜 칸 안쪽(이벤트 막대 아래)에 배경 없이 글자만 넣는다.
        private static void Postfix(CalendarDayElement __instance, int dayNumber)
        {
            try
            {
                if (__instance == null || __instance.layoutGroup == null) return;
                bool show = Gunsmith.VisitsOn(dayNumber) && Gunsmith.Unlocked;
                Transform parent = __instance.layoutGroup.transform;
                Transform existing = parent.Find(LabelName);
                if (!show)
                {
                    if (existing != null) existing.gameObject.SetActive(false);
                    return;
                }
                TextMeshProUGUI label = existing != null ? existing.GetComponent<TextMeshProUGUI>() : Create(__instance, parent);
                if (label == null) return;
                label.gameObject.SetActive(true);
                label.transform.SetAsLastSibling();
                ScriptFontSwap.Apply(label, Gunsmith.Name());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GunsmithCalendar] " + ex.Message);
            }
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
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = label.fontSize * 1.4f;
            le.minHeight = le.preferredHeight;
            return label;
        }
    }
}
