using System;
using HarmonyLib;
using UnityEngine;

namespace ForceSave
{
    // 강제 저장 (기본 F5): 원래는 잠을 자고 아침이 될 때만 저장되는데, 아무 때나 지금 상태를 저장한다.
    // 게임 저장 함수(PlayerStore.SaveGame)를 그대로 부른다. 불러오면 저장한 상태(아침/영업 중) 그대로 이어진다.
    // 저장이 꼬일 수 있는 때는 막는다:
    //  - 외출 중 / 이동 중 (외출 가방 아이템이 저장 대상이 아님)
    //  - 손님 응대 중 (카운터 위 손님 물건, 손님 상태는 저장되지 않음)
    //  - 영업 마감 후 밤(CLOSED) (게임이 이 상태로 저장하는 일이 없어서 불러온 뒤 동작이 보장되지 않음)
    //  - 게임 오버 화면
    // 영업 중 저장 → 불러오면 오늘 남은 손님 대기열은 비어 있다(대기열은 저장되지 않음).
    // 게임은 원래 아침 상태로만 불러오므로 셔터가 항상 닫힌 모습으로 시작한다.
    // 영업 중(OPEN)에 강제 저장한 세이브를 불러오면 셔터를 열린 모습으로 맞춘다 (닫힌 채면 셔터 버튼이 반대로 동작해 보임).
    internal static class LoadFix
    {
        internal static void OnGameLoadedLate()
        {
            try
            {
                if (PlayerStore.Instance == null || PlayerStore.Instance.storeState != PlayerStore.StoreState.OPEN) return;
                StoreShutterButton shutter = StoreShutterButton.Instance;
                if (shutter == null) return;
                var closed = Traverse.Create(shutter).Field("isClosed");
                if (closed.GetValue() is bool b && b)
                {
                    closed.SetValue(false);
                    Traverse.Create(shutter).Field("elapsedTime").SetValue(0f);
                    Debug.Log("[ForceSave] loaded an OPEN-state save: shutter set to open");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ForceSave] load fix: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(MapUIManager), "Update")]
    internal static class ForceSaveKeyPatch
    {
        private static float lastSave = -10f;

        private static void Postfix()
        {
            try
            {
                if (!PenLabelerAssist.QoLKeyBindings.IsBoundKeyDown(PenLabelerAssist.QoLKeyBindings.ForceSave)) return;
                if (Time.unscaledTime - lastSave < 1f) return;
                PlayerStore store = PlayerStore.Instance;
                if (store == null) return;

                string blocked = BlockReason(store);
                if (blocked != null)
                {
                    Notify(PenLabelerAssist.Text.Get(blocked));
                    return;
                }
                lastSave = Time.unscaledTime;
                store.SaveGame();
                Notify(PenLabelerAssist.Text.Get("force_save_done", StoreStation.Instance != null ? StoreStation.Instance.dayCounter : 0));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ForceSave] " + ex);
                Notify(PenLabelerAssist.Text.Get("force_save_failed"));
            }
        }

        private static string BlockReason(PlayerStore store)
        {
            try
            {
                if (GameOverUIManager.Instance != null && GameOverUIManager.Instance.uiPanel != null && GameOverUIManager.Instance.uiPanel.activeSelf)
                    return "force_save_blocked_gameover";
            }
            catch
            {
            }
            MapUIManager map = MapUIManager.Instance;
            if (map != null && (map.IsOutside || map.IsInTransit)) return "force_save_blocked_outside";
            if (store.storeState == PlayerStore.StoreState.CLOSED) return "force_save_blocked_night";
            // isClientArrived 는 손님이 떠나도 다음 손님이 올 때까지 true 로 남으므로 쓰지 않는다.
            // currentClientInstance 는 손님을 보낼 때(DismissCurrentClient) null 이 된다.
            if (store.storeState == PlayerStore.StoreState.OPEN && (store.currentClientInstance != null || ClientWalking()))
                return "force_save_blocked_client";
            return null;
        }

        private static bool ClientWalking()
        {
            try
            {
                return StoreClientMono.Instance != null && StoreClientMono.Instance.IsMoving();
            }
            catch
            {
                return false;
            }
        }

        private static void Notify(string text)
        {
            try
            {
                StoreUIManager.Instance?.Notify(text);
            }
            catch
            {
                Debug.Log("[ForceSave] " + text);
            }
        }
    }
}
