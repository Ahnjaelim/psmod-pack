using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClickAdvanceDialogue
{
    // 대화 중 화면 빈 곳을 왼쪽 클릭해도 대사를 넘긴다 (대사 넘기기 키와 같은 동작).
    // 게임은 말풍선을 눌렀을 때만 넘어가는데, 말풍선 밖(손님, 가게 배경 등)을 눌러도 넘어가게 한다.
    // 넘기지 않는 경우: 아이템/게임 창 위, 버튼 등 클릭을 받는 UI 위(말풍선 포함 → 게임이 직접 처리), 아이템 끌기 중,
    // 도구를 든 상태, 선택지가 떠 있을 때(OnAdvancePressed 가 알아서 무시).
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.
    [HarmonyPatch(typeof(StoreUIManager), "Update")]
    internal static class StoreUpdatePatch
    {
        private static readonly List<RaycastResult> hits = new List<RaycastResult>();

        private static void Postfix()
        {
            try
            {
                if (!Input.GetMouseButtonDown(0)) return;
                DialogUIManager store = DialogUIManager.Instance;
                DialoguePhoneUIManager phone = DialoguePhoneUIManager.Instance;
                bool talking = (store != null && store.currentDialog != null) || (phone != null && phone.currentDialog != null);
                if (!talking) return;
                if (UIDragHandler.isDraggingAnyUI) return;
                if (ItemSelectHandler.current != null && ItemSelectHandler.current.IsEquipped) return;
                Vector2 pos = Input.mousePosition;
                if (RenderHandler.RaycastElement<GameItem>(pos) != null) return;
                if (RenderHandler.RaycastFindParentElement<PixelWindow>(pos) != null) return;
                if (OverUiControl(pos)) return;

                if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.DismissAdvanceDialogHint();
                store?.OnAdvancePressed();
                phone?.OnAdvancePressed();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ClickAdvanceDialogue] " + ex.Message);
            }
        }

        // 버튼/토글/입력칸/말풍선 등 클릭을 직접 받는 UI 위인가
        private static bool OverUiControl(Vector2 pos)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            hits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = pos }, hits);
            foreach (RaycastResult r in hits)
            {
                GameObject g = r.gameObject;
                if (g == null) continue;
                if (g.GetComponentInParent<Selectable>() != null) return true;
                if (g.GetComponentInParent<DialogBubbleClickAdvance>() != null) return true;
                if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(g) != null) return true;
                if (ExecuteEvents.GetEventHandler<IPointerDownHandler>(g) != null) return true;
            }
            return false;
        }
    }
}
