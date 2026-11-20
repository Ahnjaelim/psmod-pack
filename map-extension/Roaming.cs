using System;
using HarmonyLib;
using UnityEngine;

namespace MapExtension
{
    // 싸돌아다니기: 한 장소에서 "돌아가기"를 누르면 가게 대신 지도가 바로 열린다.
    // 지도에서 다른 장소로 가거나, "내 전당포"를 골라야 진짜로 가게에 남는다.
    // 또 가게로 돌아와도 오늘 밤 외출 횟수(visitLeftTonight)를 쓰지 않아서 문으로 다시 나갈 수도 있다.
    internal static class Roaming
    {
        // ---- 기력은 하룻밤에 한 번만 채운다 (고철처리장/수색처 도착 시 ResetScavenging) ----
        private static int energyRefillDay = -1;

        private static int Today => StoreStation.Instance != null ? StoreStation.Instance.dayCounter : -1;

        internal static bool EnergyNotRefilledTonight() => energyRefillDay != Today;

        // ScavHelper.ResetScavenging Prefix: 오늘 이미 채웠으면 건너뛴다
        internal static bool AllowEnergyRefill()
        {
            int d = Today;
            if (energyRefillDay == d) return false;
            energyRefillDay = d;
            return true;
        }

        internal static void Register()
        {
            ModHook.OnAfterhourLeaveEarly += OnLeaveEarly;
            ModHook.OnAfterhourLeaveLate += OnLeaveLate;
        }

        internal static void Unregister()
        {
            ModHook.OnAfterhourLeaveEarly -= OnLeaveEarly;
            ModHook.OnAfterhourLeaveLate -= OnLeaveLate;
        }

        // 들고 나간 배낭: 게임은 나갈 때 고른 배낭을 외출 가방 칸에 넣고 selectedBackpack 을 비운다.
        // 원래는 문에서 매번 다시 고르지만, 싸돌아다니기는 그 단계를 건너뛰므로 기억해 뒀다가 다시 지정한다.
        private static GameItem carriedBackpack;

        private static void OnLeaveEarly(string locationId)
        {
            try { carriedBackpack = EmporiumEntry.Instance?.afterhourPocketSlotInvBackpack?.childItem; }
            catch { carriedBackpack = null; }
        }

        // 장소를 떠나는 암전 한가운데 (MapUIManager.ReturnNow 의 마지막 단계, 가게 복귀 처리 직후).
        // 화면이 밝아지기 전에 지도를 바로 연다 → 가게는 안 보이고 장소 → 지도로 넘어간다.
        // (MapUIManager.OpenUI 안쪽 동작과 같음: 암전만 새로 안 걸 뿐)
        private static void OnLeaveLate(string locationId)
        {
            try
            {
                MapUIManager map = MapUIManager.Instance;
                if (map == null || map.isOutside) return;
                if (PlayerStore.Instance == null || PlayerStore.Instance.storeState != PlayerStore.StoreState.CLOSED) return;
                if (HealthData.IsSeriouslyWounded()) return; // 크게 다치면 그냥 가게에 남는다
                // 다음 장소에서도 같은 배낭 (가게로 돌아오며 본점 인벤토리로 들어간 그 배낭)
                if (carriedBackpack != null && carriedBackpack.parentInventory != null && EmporiumEntry.Instance.selectedBackpack == null)
                    EmporiumEntry.Instance.selectedBackpack = carriedBackpack;
                carriedBackpack = null;
                TransitUI.Open(map.transitPrefab);
                if (TransitUI.Root != null) map.mapPanel = TransitUI.Root;
                EmporiumEntry.Instance.OnLeaveStore();
                StoreShutterButton.Instance.ForceImmediateClose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] roaming: " + ex.Message);
            }
        }
    }

    // 장소에서 가게로 돌아올 때 외출 횟수를 줄이지 않는다 (지도의 "내 전당포"는 원래도 안 줄임)
    [HarmonyPatch(typeof(EmporiumEntry), nameof(EmporiumEntry.OnArriveStore))]
    internal static class KeepVisitPatch
    {
        private static void Postfix(bool fromMap)
        {
            try
            {
                if (!fromMap && PlayerStore.Instance != null && PlayerStore.Instance.visitLeftTonight < 1)
                    PlayerStore.Instance.visitLeftTonight = 1;
            }
            catch
            {
            }
        }
    }

    [HarmonyPatch(typeof(ScavHelper), nameof(ScavHelper.ResetScavenging))]
    internal static class EnergyOncePatch
    {
        private static bool Prefix()
        {
            try { return Roaming.AllowEnergyRefill(); }
            catch { return true; }
        }
    }
}
