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
        // 불러오기가 완전히 끝난 뒤(몇 프레임 뒤)에 할 일.
        // 다른 모드(예: CorpPresence)도 불러오기 끝 무렵에 자기 상태를 붙이므로, 손님 목록은 그 다음에 다시 만들어야
        // 그 모드 손님(물건 올리기 등)이 제대로 동작한다.
        private static System.Collections.IEnumerator ShowFrontCounterLater()
        {
            for (int i = 0; i < 3; i++) yield return null;
            try { QueueKeeper.Restore(); }
            catch (Exception ex) { Debug.LogWarning("[ForceSave] queue restore: " + ex.Message); }
            PlayerStore ps = PlayerStore.Instance;
            if (ps == null || ps.storeState != PlayerStore.StoreState.OPEN) yield break;
            MapUIManager map = MapUIManager.Instance;
            if (map != null && map.IsOutside) yield break;
            EmporiumEntry.Instance?.ShowFrontArea();
            Debug.Log("[ForceSave] front counter shown");
        }

        internal static void OnGameLoadedLate()
        {
            try
            {
                if (PlayerStore.Instance == null || PlayerStore.Instance.storeState != PlayerStore.StoreState.OPEN) return;
                // 손님 물건이 올라오는 앞 카운터: 게임은 불러오기가 끝난 "뒤에" 이 창을 숨기고(EmporiumEntry 초기화 끝의 frontInvWindow.Hide)
                // 아침에 셔터를 열 때만 다시 보여 준다(StoreShutterButton.OpenShutter → ShowFrontArea). 그래서 몇 프레임 뒤에 보여 준다.
                try { ModHelper.StartCoroutine(PenLabelerAssist.Ids.ModId, ShowFrontCounterLater()); }
                catch (Exception ex) { Debug.LogWarning("[ForceSave] front counter: " + ex.Message); }
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

    // 영업 중 강제 저장의 손님 대기열 보존.
    // 게임은 그날 손님 목록을 아침에 셔터를 열 때만 만들고(PlayerStore.OnShutterOpened → GenerateClient) 세이브에는 넣지 않는다.
    // 그래서 영업 중 저장을 불러오면 손님이 0명이 된다.
    // 저장할 때 남은 손님 ID 를 모드 저장칸에 적어 두고, 불러왔을 때 대기열이 비어 있으면 같은 손님을 같은 순서로 다시 만든다.
    // ID 로 못 만드는 손님이 있으면 그날 손님 목록을 새로 뽑아 저장 때 남은 수만큼만 남긴다.
    internal static class QueueKeeper
    {
        private const string Key = "force_save_queue"; // "날짜|남은 수|id,id,..."

        internal static void Record(PlayerStore store)
        {
            try
            {
                if (store.storeState != PlayerStore.StoreState.OPEN)
                {
                    ModHelper.RemoveModData(PenLabelerAssist.Ids.ModId, Key); // 아침/밤 저장은 해당 없음
                    return;
                }
                var ids = new System.Collections.Generic.List<string>();
                foreach (StoreClient c in store.storeClientManager.clientStack)
                    if (c != null) ids.Add(c.identifier ?? "");
                int day = StoreStation.Instance != null ? StoreStation.Instance.dayCounter : -1;
                ModHelper.SetModData(PenLabelerAssist.Ids.ModId, Key, day + "|" + ids.Count + "|" + string.Join(",", ids));
                Debug.Log("[ForceSave] queue saved: " + ids.Count + " clients");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ForceSave] queue record: " + ex.Message);
            }
        }

        internal static void Restore()
        {
            PlayerStore store = PlayerStore.Instance;
            if (store == null || store.storeState != PlayerStore.StoreState.OPEN) return;
            StoreClientManager mgr = store.storeClientManager;
            if (mgr == null || mgr.clientStack.Count > 0 || store.currentClientInstance != null) return;
            string v = ModHelper.GetModData(PenLabelerAssist.Ids.ModId, Key);
            int day = StoreStation.Instance != null ? StoreStation.Instance.dayCounter : -2;
            string[] p = string.IsNullOrEmpty(v) ? new string[0] : v.Split('|');
            if (p.Length < 2 || !int.TryParse(p[0], out int savedDay) || savedDay != day || !int.TryParse(p[1], out int count))
            {
                // 기록이 없는 영업 중 세이브 (이 기능 전에 저장한 것): 그날 손님 목록을 통째로 다시 뽑는다
                mgr.GenerateClient();
                store.HandleFutureClientQueue();
                mgr.clientStack.RemoveAll(c => c == null);
                Debug.Log("[ForceSave] no queue record: regenerated " + mgr.clientStack.Count + " clients");
                return;
            }
            if (count <= 0) return;

            var ids = p.Length > 2 ? p[2].Split(',') : new string[0];
            bool allKnown = ids.Length == count;
            foreach (string id in ids)
                if (string.IsNullOrEmpty(id) || !StoreClientListDict.storeClientDict.ContainsKey(id)) { allKnown = false; break; }

            if (allKnown)
            {
                foreach (string id in ids)
                {
                    StoreClient c = StoreClientListDict.CreateStoreClient(id);
                    if (c != null) mgr.clientStack.Add(c);
                }
                Debug.Log("[ForceSave] queue restored by id: " + mgr.clientStack.Count + " clients");
            }
            else
            {
                // 예비: 그날 손님 목록을 새로 뽑고 남은 수만큼만
                mgr.GenerateClient();
                store.HandleFutureClientQueue();
                mgr.clientStack.RemoveAll(c => c == null);
                if (mgr.clientStack.Count > count) mgr.clientStack.RemoveRange(count, mgr.clientStack.Count - count);
                Debug.Log("[ForceSave] queue regenerated: " + mgr.clientStack.Count + " clients (saved " + count + ")");
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
                QueueKeeper.Record(store); // 손님 대기열은 세이브에 안 들어가서 따로 적어 둔다
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
