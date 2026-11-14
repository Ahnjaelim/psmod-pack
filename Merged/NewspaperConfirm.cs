using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewspaperConfirm
{
    // 신문 UI 하단에 "확인" 버튼을 추가한다. 누르면 읽던 신문 아이템을 휴지통에 넣고 UI 를 닫는다.
    //
    // 신문 아이템은 GameItem.ActivateSlotItem -> onActivateSlotItemFunc -> NewsUIManager.ToggleUI 로 열리는데
    // ToggleUI 는 어떤 아이템인지 모르므로, ActivateSlotItem Prefix 에서 신문 아이템을 기억해 둔다.
    // 아이템이 아닌 경로(카운터의 신문 오브젝트 클릭 등)로 열면 옮길 대상이 없으니 버튼을 숨긴다.
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.

    [HarmonyPatch(typeof(GameItem), nameof(GameItem.ActivateSlotItem))]
    internal static class ActivateSlotItemPatch
    {
        private static void Prefix(GameItem __instance)
        {
            try
            {
                if (__instance != null && __instance.identifier == NewspaperTrash.NewspaperId)
                    NewspaperTrash.Pending = __instance;
            }
            catch
            {
            }
        }

        private static void Finalizer()
        {
            NewspaperTrash.Pending = null;
        }
    }

    [HarmonyPatch(typeof(NewsUIManager), nameof(NewsUIManager.ToggleUI))]
    internal static class ToggleUIPatch
    {
        private static void Postfix(NewsUIManager __instance)
        {
            try
            {
                bool open = __instance.uiGameObject != null && __instance.uiGameObject.activeSelf;
                NewspaperTrash.Current = open ? NewspaperTrash.Pending : null;
                NewspaperTrash.Pending = null;
                NewspaperTrash.RefreshButton(__instance);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NewspaperConfirm] toggle failed: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(NewsUIManager), nameof(NewsUIManager.CloseUI))]
    internal static class CloseUIPatch
    {
        private static void Postfix()
        {
            NewspaperTrash.Current = null;
        }
    }

    internal static class NewspaperTrash
    {
        public const string NewspaperId = "newspaper";
        private const string ButtonName = "NPC_ConfirmButton";

        public static GameItem Pending;
        public static GameItem Current;

        public static void RefreshButton(NewsUIManager ui)
        {
            if (ui == null || ui.uiGameObject == null) return;
            GameObject button = FindOrCreateButton(ui);
            bool show = ui.uiGameObject.activeSelf && Current != null && Current.parentInventory != null && !IsInTrash(Current);
            button.SetActive(show);
            if (show) button.transform.SetAsLastSibling();
        }

        private static GameObject FindOrCreateButton(NewsUIManager ui)
        {
            Transform root = ui.uiGameObject.transform;
            Transform existing = root.Find(ButtonName);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 24f);
            rt.sizeDelta = new Vector2(180f, 48f);

            var img = go.GetComponent<Image>();
            img.color = new Color32(0x2a, 0x24, 0x33, 0xee);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock colors = btn.colors;
            colors.highlightedColor = new Color(0.85f, 0.95f, 0.95f, 1f);
            colors.pressedColor = new Color(0.7f, 0.8f, 0.8f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => OnConfirm(ui));

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var lrt = (RectTransform)labelGo.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            // 신문 UI 의 기존 텍스트에서 폰트를 빌려 온다 (한글 폰트 포함 여부도 따라감).
            TextMeshProUGUI sample = root.GetComponentInChildren<TextMeshProUGUI>(true);
            if (sample != null && sample != label)
            {
                label.font = sample.font;
                label.fontSharedMaterial = sample.fontSharedMaterial;
            }
            label.fontSize = 26f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color32(0x8c, 0xc5, 0xbf, 0xff);
            label.raycastTarget = false;
            label.text = PenLabelerAssist.Text.Get("news_confirm_button");

            return go;
        }

        private static void OnConfirm(NewsUIManager ui)
        {
            GameItem item = Current;
            try
            {
                if (item != null && MoveToTrash(item))
                {
                    Current = null;
                    ui.CloseUI();
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NewspaperConfirm] move to trash failed: " + ex.GetType().Name + ": " + ex.Message);
            }
            // 휴지통에 못 넣었으면 UI 는 그대로 두고 버튼만 갱신
            RefreshButton(ui);
        }

        private static GameInventory GetTrash()
        {
            EmporiumEntry entry = EmporiumEntry.Instance;
            if (entry == null) return null;
            try
            {
                if (entry.trashcan?.contentWindow?.child is GameInventory inv) return inv;
            }
            catch
            {
            }
            return entry.trashcanInvElement;
        }

        private static bool IsInTrash(GameItem item)
        {
            GameInventory trash = GetTrash();
            return trash != null && item.parentInventory == trash;
        }

        // AutoDiscard 와 같은 방식: 빈 칸 확인 -> 원래 위치에서 빼기 -> 휴지통에 넣기 -> 실패 시 되돌리기
        private static bool MoveToTrash(GameItem item)
        {
            GameInventory trash = GetTrash();
            GameInventory source = item.parentInventory;
            if (trash == null || source == null || source == trash) return false;

            SlotMarker slot = trash.TryFindOneValidInventorySlot(item);
            if (slot == null || slot.numTransfer <= 0)
            {
                Debug.Log("[NewspaperConfirm] trash is full");
                return false;
            }

            bool expelled = source.Expel(item);
            if (!expelled && item.parentInventory != null) return false;

            int left = GraphUtils.TryAcceptAll(trash, item);
            if (item.parentInventory == trash || left == 0) return true;

            source.UncheckedAccept(item);
            Debug.LogWarning("[NewspaperConfirm] trash rejected the newspaper; rolled back");
            return false;
        }
    }
}
