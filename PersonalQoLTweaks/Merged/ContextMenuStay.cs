using HarmonyLib;
using UnityEngine;

namespace ContextMenuStay
{
    // 우클릭 메뉴(아이템 메뉴/정렬 메뉴)는 마우스 밑에 "ContextMenuNode" 태그 오브젝트가 없으면 바로 닫힌다.
    // 다른 모드(Smart Inventory Arrange 등)가 정렬 메뉴에 줄을 끼워 넣으면 새 줄/줄 사이가 태그가 없어서
    // 마우스를 내리는 순간 메뉴가 사라진다 → 마우스가 열린 메뉴 창 사각형 안에 있으면 메뉴 위로 친다.
    [HarmonyPatch(typeof(ItemContextHandler), nameof(ItemContextHandler.FindContextMenuNode))]
    internal static class FindContextMenuNodePatch
    {
        private static void Postfix(ItemContextHandler __instance, Vector2 eventDataPosition, Canvas canvas, ref GameObject __result)
        {
            if (__result != null) return;
            try
            {
                Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                var tr = Traverse.Create(__instance);
                __result = Hit(tr.Field("sortWindow").GetValue() as PixelWindow, eventDataPosition, cam)
                        ?? Hit(tr.Field("contextWindow").GetValue() as PixelWindow, eventDataPosition, cam);
            }
            catch
            {
            }
        }

        private static GameObject Hit(PixelWindow window, Vector2 pos, Camera cam)
        {
            if (window == null || window.handler == null) return null;
            GameObject go = window.handler.gameObject;
            if (!go.activeInHierarchy) return null;
            foreach (RectTransform rt in go.GetComponentsInChildren<RectTransform>(false))
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, pos, cam)) return go;
            }
            return null;
        }
    }
}
