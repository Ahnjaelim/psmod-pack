using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapExtension
{
    // 지도 확장 확인 창: 게임의 "외출 확인" 창(MapUIManager.confirmationPanel, 본문 + [예][아니요])을 복제해서
    // 본문/버튼 글자와 동작만 바꿔 쓴다. 복제본의 게임 스크립트는 지우고 버튼 동작은 새로 연결한다.
    // 외출 지도(TransitUI)보다 위 층으로 올려서 지도 위에서도 보인다. 실패하면 게임 기본 확인 창.
    internal static class Dialogs
    {
        private static GameObject current;
        private static bool dumped;

        internal static void Confirm(string body, string ok, string cancel, Action onOk, Action onCancel = null)
        {
            try
            {
                if (TryOpen(body, ok, cancel, onOk, onCancel)) return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] dialog: " + ex);
                Close();
            }
            GenericConfirmationUIManager gc = GenericConfirmationUIManager.Instance;
            if (gc == null) return;
            gc.OpenPanel(body, ok, cancel, onOk, onCancel);
            try
            {
                int order = MapOrder(out int layer);
                Lift(gc.confirmationDarkness, layer, order + 20);
                Lift(gc.panel, layer, order + 21);
            }
            catch
            {
            }
        }

        internal static void Close()
        {
            if (current != null) UnityEngine.Object.Destroy(current);
            current = null;
            try { GenericConfirmationUIManager.Instance?.confirmationDarkness?.SetActive(false); } catch { }
        }

        private static bool TryOpen(string body, string ok, string cancel, Action onOk, Action onCancel)
        {
            Close();
            GameObject src = MapUIManager.Instance != null ? MapUIManager.Instance.confirmationPanel : null;
            if (src == null) return false;

            // 띄울 곳: 외출 지도가 열려 있으면 지도 안(같은 화면 층), 아니면 원래 창 자리
            GameObject mapRoot = TransitUI.Root;
            bool onMap = mapRoot != null && mapRoot.activeInHierarchy;
            Transform host = onMap ? mapRoot.transform : src.transform.parent;
            Canvas srcCanvas = src.GetComponentInParent<Canvas>(true)?.rootCanvas;
            Canvas hostCanvas = host.GetComponentInParent<Canvas>(true)?.rootCanvas;
            float scale = srcCanvas != null && hostCanvas != null && hostCanvas.scaleFactor > 0f ? srcCanvas.scaleFactor / hostCanvas.scaleFactor : 1f;

            // 화면 전체를 덮는 층: 어두운 판(뒤 클릭 막기) + 창
            var layer = new GameObject("VME_ConfirmLayer", typeof(RectTransform));
            layer.transform.SetParent(host, false);
            layer.transform.SetAsLastSibling();
            var lrt = (RectTransform)layer.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(-4000f, -4000f); // 부모가 화면보다 작아도 화면 전체를 덮게
            lrt.offsetMax = new Vector2(4000f, 4000f);
            var dim = layer.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            dim.raycastTarget = true;

            // 비활성 부모 밑에 복제 → Awake/OnEnable 전에 게임 스크립트 제거
            var holder = new GameObject("VME_DialogHolder");
            holder.SetActive(false);
            GameObject go = UnityEngine.Object.Instantiate(src, holder.transform, false);
            go.name = "VME_Confirm";
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string ns = mb.GetType().Namespace ?? "";
                if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro")) continue;
                UnityEngine.Object.DestroyImmediate(mb);
            }
            go.transform.SetParent(layer.transform, false);
            UnityEngine.Object.Destroy(holder);
            var rt = (RectTransform)go.transform;
            var srt = (RectTransform)src.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = srt.rect.size;
            rt.localScale = new Vector3(scale, scale, 1f);
            go.SetActive(true);
            layer.SetActive(true);
            Canvas.ForceUpdateCanvases();

            // 버튼(Button) 두 개: 왼쪽 = 확인, 오른쪽 = 취소. 버튼 글자 = 버튼 영역 안에 있는 글자
            var buttons = new List<Button>(go.GetComponentsInChildren<Button>(true));
            buttons.RemoveAll(b => b.gameObject == go);
            buttons.Sort((a, b) => X(a.transform).CompareTo(X(b.transform)));
            var texts = new List<TMP_Text>(go.GetComponentsInChildren<TMP_Text>(true));
            var labelOf = new Dictionary<Button, TMP_Text>();
            foreach (Button b in buttons)
                foreach (TMP_Text t in texts)
                    if (!labelOf.ContainsValue(t) && Inside((RectTransform)b.transform, t.rectTransform)) { labelOf[b] = t; break; }
            TMP_Text bodyText = null;
            float best = -1f;
            foreach (TMP_Text t in texts)
            {
                if (labelOf.ContainsValue(t)) continue;
                float area = t.rectTransform.rect.width * t.rectTransform.rect.height;
                if (area > best) { best = area; bodyText = t; }
            }
            if (bodyText == null || buttons.Count < 2)
            {
                if (!dumped) { dumped = true; Debug.Log("[MapExtension] confirm clone: buttons " + buttons.Count + "\n" + Dump(go.transform, "")); }
                UnityEngine.Object.Destroy(layer);
                return false;
            }

            bodyText.text = body ?? "";
            Button okB = buttons[0], noB = buttons[buttons.Count - 1];
            Wire(okB, labelOf.TryGetValue(okB, out TMP_Text okT) ? okT : null, ok, () => { Close(); onOk?.Invoke(); });
            if (string.IsNullOrEmpty(cancel)) noB.gameObject.SetActive(false);
            else Wire(noB, labelOf.TryGetValue(noB, out TMP_Text noT) ? noT : null, cancel, () => { Close(); onCancel?.Invoke(); });

            if (!onMap)
            {
                // 가게 화면: 원래 창 층 그대로 (지도 없음)
                int order = MapOrder(out int sl);
                Lift(layer, sl, order + 21);
            }
            current = layer;
            return true;
        }

        // inner 의 가운데가 outer 영역 안에 있나 (월드 좌표)
        private static bool Inside(RectTransform outer, RectTransform inner)
        {
            var c = new Vector3[4];
            outer.GetWorldCorners(c);
            Vector3 p = inner.TransformPoint(inner.rect.center);
            return p.x >= c[0].x && p.x <= c[2].x && p.y >= c[0].y && p.y <= c[2].y;
        }

        private static void Wire(Button b, TMP_Text t, string label, Action click)
        {
            if (t == null) t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null)
            {
                t.text = label ?? "";
                // 원래 [예]/[아니요] 크기 버튼이라 긴 글자는 줄여서 넣는다
                t.enableAutoSizing = true;
                t.fontSizeMax = t.fontSize;
                t.fontSizeMin = Mathf.Max(6f, t.fontSize * 0.5f);
                t.enableWordWrapping = false;
            }
            b.onClick = new Button.ButtonClickedEvent(); // 복제된 원래 동작(외출 확인) 제거
            b.onClick.AddListener(() =>
            {
                try { EmporiumAudioPlayer.Instance.audioButton1.Play(); } catch { }
                click();
            });
            b.interactable = true;
        }

        // 외출 지도 캔버스 층 (지도 위로 올릴 기준)
        private static int MapOrder(out int layer)
        {
            int order = 0;
            layer = 0;
            GameObject map = TransitUI.Root;
            Canvas mc = map != null ? map.GetComponentInParent<Canvas>() : null;
            if (mc != null)
            {
                order = mc.rootCanvas.sortingOrder;
                layer = mc.rootCanvas.sortingLayerID;
                foreach (Canvas c in map.GetComponentsInChildren<Canvas>(true))
                    if (c.overrideSorting && c.sortingOrder > order) order = c.sortingOrder;
            }
            else
            {
                GameObject p = MapUIManager.Instance != null ? MapUIManager.Instance.confirmationPanel : null;
                Canvas own = p != null ? p.GetComponentInParent<Canvas>() : null;
                if (own != null) { order = own.rootCanvas.sortingOrder; layer = own.rootCanvas.sortingLayerID; }
            }
            return order;
        }

        private static void Lift(GameObject go, int layer, int order)
        {
            if (go == null) return;
            Canvas c = go.GetComponent<Canvas>() ?? go.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingLayerID = layer;
            c.sortingOrder = order;
            if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>(); // 버튼 클릭 받기
        }

        private static float X(Transform t) => t.position.x;

        private static string Dump(Transform t, string ind)
        {
            var sb = new StringBuilder();
            sb.Append(ind).Append(t.name).Append(" [");
            foreach (Component c in t.GetComponents<Component>()) sb.Append(c != null ? c.GetType().Name : "null").Append(' ');
            sb.Append("]\n");
            foreach (Transform ch in t) sb.Append(Dump(ch, ind + "  "));
            return sb.ToString();
        }
    }
}
