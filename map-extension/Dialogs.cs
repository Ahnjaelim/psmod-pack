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
        private static readonly List<GameObject> layers = new List<GameObject>(); // 혹시 여러 개 떠도 Close 가 전부 지운다

        internal static bool IsOpen => current != null;
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

        // 원본 창의 글자 폰트(게임이 언어에 맞춰 바꿔 둔 것)를 복제본 같은 자리 글자에 그대로.
        // 폰트를 바꿔 주는 게임 스크립트는 복제본에서 지워지므로, 안 하면 영어 폰트 + 한글 대체 글꼴이 섞여 보인다.
        private static void CopyFonts(Transform src, Transform dst)
        {
            try
            {
                TMP_Text s = src.GetComponent<TMP_Text>(), d = dst.GetComponent<TMP_Text>();
                if (s != null && d != null)
                {
                    d.font = s.font;
                    d.fontSharedMaterial = s.fontSharedMaterial;
                }
                int n = Mathf.Min(src.childCount, dst.childCount);
                for (int i = 0; i < n; i++) CopyFonts(src.GetChild(i), dst.GetChild(i));
            }
            catch
            {
            }
        }

        internal static void Close()
        {
            if (current != null || layers.Count > 0) Probe.After(0.3f);
            if (current != null) UnityEngine.Object.Destroy(current);
            current = null;
            foreach (GameObject l in layers) if (l != null) UnityEngine.Object.Destroy(l);
            layers.Clear();
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
                // 번역 컴포넌트(UnityEngine.Localization)도 지운다: 남겨 두면 켜질 때 버튼 글자를 "No" 등 원래 문구로 되돌린다
                if ((ns.StartsWith("UnityEngine") && !ns.StartsWith("UnityEngine.Localization")) || ns.StartsWith("TMPro")) continue;
                UnityEngine.Object.DestroyImmediate(mb);
            }
            CopyFonts(src.transform, go.transform);
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

            SetText(bodyText, body ?? "");
            Button okB = buttons[0], noB = buttons[buttons.Count - 1];
            Debug.Log("[MapExtension] dialog open: buttons " + buttons.Count + ", host " + host.name + (onMap ? " (map)" : ""));
            Wire(okB, labelOf.TryGetValue(okB, out TMP_Text okT) ? okT : null, ok, () => { Debug.Log("[MapExtension] dialog ok"); Close(); onOk?.Invoke(); });
            if (string.IsNullOrEmpty(cancel)) noB.gameObject.SetActive(false);
            else Wire(noB, labelOf.TryGetValue(noB, out TMP_Text noT) ? noT : null, cancel, () => { Debug.Log("[MapExtension] dialog cancel"); Close(); onCancel?.Invoke(); });

            // 버튼이 3개 이상인 창(닫기 X 등): 나머지 버튼에 남은 원래 동작(복제본 창만 숨기기 → 어두운 판만 남아 화면이 막힘)을 지우고 "취소"로.
            // 글자는 안 바꾼다 (X 같은 것이 "취소"로 바뀌지 않게)
            for (int bi = 1; bi < buttons.Count - 1; bi++)
            {
                Button extra = buttons[bi];
                Debug.Log("[MapExtension] dialog extra button: " + extra.name + " → cancel");
                extra.onClick = new Button.ButtonClickedEvent();
                extra.onClick.AddListener(() =>
                {
                    Debug.Log("[MapExtension] dialog cancel (extra button)");
                    try { EmporiumAudioPlayer.Instance.audioButton1.Play(); } catch { }
                    Close();
                    onCancel?.Invoke();
                });
                extra.interactable = true;
            }

            if (!onMap)
            {
                // 가게 화면: 원래 창 층 그대로 (지도 없음)
                int order = MapOrder(out int sl);
                Lift(layer, sl, order + 21);
            }
            current = layer;
            layers.Add(layer);
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
            // 버튼 안 글자 칸을 전부 바꾼다 (켜진 칸/꺼진 칸이 따로 있는 버튼이 있어서 하나만 바꾸면 "No" 가 남는다)
            var all = new List<TMP_Text>(b.GetComponentsInChildren<TMP_Text>(true));
            if (t != null && !all.Contains(t)) all.Add(t);
            foreach (TMP_Text x in all)
            {
                SetText(x, label ?? "");
                // 원래 [예]/[아니요] 크기 버튼이라 긴 글자는 줄여서 넣는다
                x.enableAutoSizing = true;
                x.fontSizeMax = x.fontSize;
                x.fontSizeMin = Mathf.Max(6f, x.fontSize * 0.5f);
                x.enableWordWrapping = false;
            }
            b.onClick = new Button.ButtonClickedEvent(); // 복제된 원래 동작(외출 확인) 제거
            b.onClick.AddListener(() =>
            {
                try { EmporiumAudioPlayer.Instance.audioButton1.Play(); } catch { }
                click();
            });
            b.interactable = true;
        }

        // 글자 + 그 글자에 맞는 폰트 (게임 ScriptFontSwap: 한글이면 게임 한국어 폰트).
        // 복제본은 원래 창의 폰트 교체 스크립트가 지워져서 영어 폰트 + 한글 대체 글꼴이 섞여 보인다.
        private static void SetText(TMP_Text t, string text)
        {
            if (t == null) return;
            if (t is TextMeshProUGUI u)
            {
                try { ScriptFontSwap.Apply(u, text); return; } catch { }
            }
            t.text = text;
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

        // 진단: 창을 닫고 나서도 화면을 덮고 클릭을 막는 판이 남았나 → 로그 (VME_ 판이면 지움)
        private class Probe : MonoBehaviour
        {
            private float at;
            private static Probe inst;

            internal static void After(float sec)
            {
                if (inst == null)
                {
                    var go = new GameObject("VME_DialogProbe");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    inst = go.AddComponent<Probe>();
                }
                inst.at = Time.unscaledTime + sec;
                inst.enabled = true;
            }

            private void Update()
            {
                if (Time.unscaledTime < at) return;
                enabled = false;
                try { Scan(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] probe: " + ex.Message); }
            }

            private static void Scan()
            {
                float sw = Screen.width, sh = Screen.height;
                int n = 0;
                foreach (Graphic g in UnityEngine.Object.FindObjectsOfType<Graphic>())
                {
                    if (!g.isActiveAndEnabled || !g.raycastTarget || g.color.a < 0.2f) continue;
                    var c = new Vector3[4];
                    g.rectTransform.GetWorldCorners(c);
                    Canvas cv = g.canvas;
                    Camera cam = cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay ? cv.worldCamera : null;
                    Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), z = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
                    if (a.x > sw * 0.1f || a.y > sh * 0.1f || z.x < sw * 0.9f || z.y < sh * 0.9f) continue; // 화면 거의 전체를 덮는 것만
                    string path = g.name;
                    for (Transform t = g.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
                    Debug.Log("[MapExtension] probe: full-screen blocker " + path + " color " + g.color + " canvas " + (cv != null ? cv.rootCanvas.name + "#" + cv.sortingOrder : "-"));
                    n++;
                    if (path.Contains("VME_ConfirmLayer") || path.Contains("VME_Confirm")) { Transform r = g.transform; while (r.parent != null && !r.name.StartsWith("VME_ConfirmLayer")) r = r.parent; UnityEngine.Object.Destroy(r.gameObject); Debug.Log("[MapExtension] probe: removed leftover dialog layer"); }
                }
                Debug.Log("[MapExtension] probe: " + n + " full-screen blocker(s) after dialog close");
            }
        }

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
