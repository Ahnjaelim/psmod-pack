using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CtrlClickSelect
{
    // Ctrl + 클릭: 윈도우 탐색기처럼 아이템을 하나씩 다중 선택에 넣고 뺀다.
    // 게임의 다중 선택(드래그 박스 선택)과 같은 목록/마무리(FinalizeBand)를 쓰므로 선택 후 끌어서 옮기기 등이 그대로 동작한다.
    // 같은 인벤토리 안에서만 모은다 (다른 인벤토리 아이템을 Ctrl + 클릭하면 새로 시작).
    // 게임 기본 "빠른 이동(Ctrl + 클릭)"은 Ctrl이 눌려 있는 동안 막는다. 빠른 이동을 쓰려면 옵션에서 다른 키로 바꾸면 된다.
    internal static class CtrlState
    {
        internal static bool Held => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        internal static bool QuickMoveIsCtrl
        {
            get
            {
                KeyCode k = KeyBindHelper.Get("quick_move_inventory");
                return k == KeyCode.LeftControl || k == KeyCode.RightControl;
            }
        }

        internal static bool Selectable(GameItemElement e)
        {
            return e != null && !e.IsDestroyed() && e.parentInventory != null && !e.isDebugMenu
                && e.MayRemove() && e.MaxNumRemove() >= 1 && !ItemPinHelper.IsPinned(e)
                && e.handler != null && e.handler.gameObject.activeInHierarchy;
        }
    }

    [HarmonyPatch(typeof(ItemMultiSelectHandler), "OnEventPress")]
    internal static class MultiSelectPressPatch
    {
        private static bool Prefix(ItemMultiSelectHandler __instance, ISet<KeyCode> __0)
        {
            try
            {
                if (__0 == null || !__0.Contains(__instance.key) || !CtrlState.Held || ItemPinHelper.BlocksClicks) return true;
                if (StoreUIManager.Instance != null && StoreUIManager.Instance.IsTyping()) return true;
                if (ItemMouseDragHandler.current != null && ItemMouseDragHandler.current.IsDraggingItem) return true;

                var tr = Traverse.Create(__instance);
                string state = tr.Field("state").GetValue()?.ToString();
                if (state != "Idle" && state != "Selected") return true;

                GameItemElement e = RenderHandler.RaycastElement<GameItemElement>(Input.mousePosition);
                if (!CtrlState.Selectable(e)) return true;

                var selected = tr.Field("selectedItems").GetValue() as List<GameItemElement>;
                if (selected == null) return true;

                if (selected.Contains(e))
                {
                    selected.Remove(e);
                    if (selected.Count == 0)
                    {
                        __instance.Cancel();
                        return false;
                    }
                }
                else
                {
                    if (selected.Count > 0 && selected[0].parentInventory != e.parentInventory)
                    {
                        __instance.Cancel();
                        selected = tr.Field("selectedItems").GetValue() as List<GameItemElement>;
                        if (selected == null) return false;
                    }
                    selected.Add(e);
                }
                SelectAll.Stubs.FinalizeBand(__instance);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CtrlClickSelect] " + ex.Message);
                return true;
            }
        }
    }

    // 빠른 이동: Ctrl(빠른 이동 키가 Ctrl일 때)이 눌린 채 마우스를 떼면 이동하지 않는다.
    [HarmonyPatch(typeof(ItemQuickTransferHandler), "OnEventRelease")]
    internal static class QuickTransferReleasePatch
    {
        private static void Prefix(ItemQuickTransferHandler __instance, ISet<KeyCode> __0)
        {
            try
            {
                if (__0 == null || !__0.Contains(__instance.key) || !CtrlState.Held || !CtrlState.QuickMoveIsCtrl) return;
                __0.Remove(__instance.key);
                Traverse.Create(__instance).Field("currentItem").SetValue(null);
            }
            catch
            {
            }
        }
    }

    // 더블클릭/한 번 클릭 사용: Ctrl + 클릭으로 고르는 중에는 아이템이 사용되지 않게 한다.
    [HarmonyPatch(typeof(ItemMouseDoubleClickHandler), "OnEventPress")]
    internal static class DoubleClickPressPatch
    {
        private static bool Prefix() => !CtrlState.Held;
    }

    [HarmonyPatch(typeof(ItemMouseDoubleClickHandler), "OnEventRelease")]
    internal static class DoubleClickReleasePatch
    {
        private static bool Prefix() => !CtrlState.Held;
    }
}
