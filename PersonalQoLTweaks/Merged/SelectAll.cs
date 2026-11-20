using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SelectAll
{
    // Ctrl + A (키는 옵션에서 변경): 마우스가 올라가 있는 인벤토리의 아이템을 전부 다중 선택한다.
    // 게임의 드래그 박스 선택(ItemMultiSelectHandler)과 같은 목록/같은 마무리(FinalizeBand)를 쓰므로
    // 선택 후 끌어서 옮기기, 일괄 고정 등은 박스로 선택했을 때와 똑같이 동작한다.
    // 박스 선택과 같은 조건: 꺼낼 수 있고 고정(핀)되지 않은 아이템만.
    [HarmonyPatch(typeof(StoreUIManager), "Update")]
    internal static class SelectAllPatch
    {
        internal const string BindingId = "psmp_select_all";

        private static void Postfix(StoreUIManager __instance)
        {
            try
            {
                if (!(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) return;
                KeyCode key = KeyBindHelper.Get(BindingId);
                if (key == KeyCode.None || !Input.GetKeyDown(key)) return;
                if (__instance == null || __instance.IsTyping() || ItemPinHelper.BlocksClicks) return;
                if (ItemMouseDragHandler.current != null && ItemMouseDragHandler.current.IsDraggingItem) return;
                ItemMultiSelectHandler handler = ItemMultiSelectHandler.current;
                if (handler == null || handler.IsGroupDragging) return;

                GameInventory inv = RenderHandler.RaycastFindParentElement<GameInventory>(Input.mousePosition);
                if (inv == null) return;

                var items = new List<GameItemElement>();
                foreach (GameItem gi in inv.childItems)
                {
                    if (gi is GameItemElement e && !e.IsDestroyed() && e.parentInventory != null && !e.isDebugMenu
                        && e.MayRemove() && e.MaxNumRemove() >= 1 && !ItemPinHelper.IsPinned(e)
                        && e.handler != null && e.handler.gameObject.activeInHierarchy && !items.Contains(e))
                        items.Add(e);
                }
                if (items.Count == 0) return;

                handler.Cancel(); // 기존 선택 해제
                var selected = Traverse.Create(handler).Field("selectedItems").GetValue() as List<GameItemElement>;
                if (selected == null) return;
                selected.AddRange(items);
                Stubs.FinalizeBand(handler);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SelectAll] " + ex.Message);
            }
        }
    }

    [HarmonyPatch]
    internal static class Stubs
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(ItemMultiSelectHandler), "FinalizeBand")]
        public static void FinalizeBand(ItemMultiSelectHandler instance)
        {
            throw new NotImplementedException("stub");
        }
    }
}
