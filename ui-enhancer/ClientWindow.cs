using System;
using UnityEngine;
using UnityEngine.UI;

namespace UiEnhancer
{
    // 레이어 1: 윈도우 배경 (img/window.png)
    //  - 게임 거래창이 켜지면 화면 위에서 스르륵 내려오고, 꺼지면 올라간다 (게임에 열림/닫힘 이벤트가 없어서 매 프레임 확인)
    //  - 거래창과 같은 부모, 거래창 바로 앞 순서 → 항상 거래창 한 단계 뒤에 그려진다
    //  - 크기는 2560x1440 화면 기준 픽셀 (화면 높이에 비례), 픽셀아트 필터(PixelSize)
    // 숫자 설정은 Settings.cs. 거래창 쪽은 TabletDock.cs(자리/배율), TabletLayout.cs(배치), TabletStyle.cs(색/글자).
    internal partial class ClientWindow : MonoBehaviour
    {
        private static ClientWindow instance;
        private static bool createdOnce;
        private RectTransform rt;
        private Canvas hostCanvas;
        private Vector2 size;     // 화면 크기 (기준 픽셀)
        private float t;          // 0 = 위에 숨음, 1 = 다 내려옴
        private bool shown;

        // 없으면 만들고, 꺼져 있으면 켠다 (게임 불러올 때 / 일어날 때 / 셔터 열 때 / 가게 복귀 / 거래창 열 때 호출)
        internal static void Ensure()
        {
            if (instance != null)
            {
                // 잘 때 게임이 종이 UI 층의 오브젝트를 전부 끈다(CloseAllPaperUIForSleep) → 꺼져 있으면 다시 켠다
                if (!instance.gameObject.activeSelf)
                {
                    instance.gameObject.SetActive(true);
                    Debug.Log("[UiEnhancer] window was switched off, switched back on");
                }
                return;
            }
            // 지워진 오브젝트는 Unity 에서 null 로 취급 → 다시 만든다
            if (createdOnce) Debug.Log("[UiEnhancer] window was gone, recreating");
            createdOnce = true;
            Transform host = AdvCalendarUIManager.Instance != null && AdvCalendarUIManager.Instance.UIPanel != null
                ? AdvCalendarUIManager.Instance.UIPanel.transform.parent : null; // 처음 자리 (곧 거래창 옆으로 옮김)
            if (host == null) { Debug.LogWarning("[UiEnhancer] no store UI canvas"); return; }

            Texture2D src = Load("img/window.png");
            if (src == null) { Debug.LogWarning("[UiEnhancer] img/window.png not found"); return; }
            // 화면 크기 = 원래 × Scale, 그 크기에서 블록 하나가 PixelSize 픽셀이 되게 줄인다
            int lowW = Mathf.Max(1, Mathf.RoundToInt(src.width * Scale / PixelSize));
            int lowH = Mathf.Max(1, Mathf.RoundToInt(src.height * Scale / PixelSize));
            Texture2D tex = Pixelate(src, lowW, lowH);

            var go = new GameObject("VME_ClientWindow", typeof(RectTransform));
            go.transform.SetParent(host, false);
            go.transform.SetAsFirstSibling();
            var img = go.AddComponent<Image>();
            img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            img.raycastTarget = false; // 클릭은 그대로 아래로
            instance = go.AddComponent<ClientWindow>();
            instance.rt = (RectTransform)go.transform;
            instance.rt.anchorMin = instance.rt.anchorMax = new Vector2(0.5f, 1f); // 화면 위 가운데
            instance.rt.pivot = new Vector2(0.5f, 0f);                             // 그림 아래쪽 기준 (처음엔 화면 위 밖)
            instance.hostCanvas = host.GetComponentInParent<Canvas>()?.rootCanvas;
            instance.size = new Vector2(lowW * PixelSize, lowH * PixelSize);
            instance.Apply();
        }

        internal static void Remove()
        {
            if (instance != null) Destroy(instance.gameObject);
            instance = null;
        }

        private static Texture2D Load(string path)
        {
            byte[] bytes = ModHelper.ReadModFileBytes(UiEnhancerMod.ModId, path);
            if (bytes == null || bytes.Length == 0) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) return null;
            tex.filterMode = FilterMode.Point; // 픽셀 그림
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        // 블록 가운데 픽셀 색으로 줄인다 (색이 섞이지 않아 픽셀아트 느낌 유지)
        private static Texture2D Pixelate(Texture2D src, int w, int h)
        {
            Color32[] from = src.GetPixels32();
            var to = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = Mathf.Min(src.width - 1, (int)((x + 0.5f) * src.width / w));
                    int sy = Mathf.Min(src.height - 1, (int)((y + 0.5f) * src.height / h));
                    to[y * w + x] = from[sy * src.width + sx];
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(to);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return tex;
        }

        // 거래창이 켜져 있으면 내려오고, 꺼지면 올라간다 (외출 중은 제외)
        private static bool TabletOpen()
        {
            try
            {
                MapUIManager map = MapUIManager.Instance;
                if (map != null && map.isOutside) return false;
                GameObject ui = NegociationUIManager.Instance != null ? NegociationUIManager.Instance.uiObject : null;
                return ui != null && ui.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        // 게임이 이번 프레임에 거래창을 고친(버튼 다시 만들기, 위치 바꾸기) 다음에 적용해야 깜빡이지 않는다
        private void LateUpdate()
        {
            try { StickToTablet(); } catch (Exception ex) { Debug.LogWarning("[UiEnhancer] stick: " + ex.Message); }
            shown = TabletOpen();
            float target = shown ? 1f : 0f;
            if (!Mathf.Approximately(t, target))
            {
                float speed = 1f / (shown ? DownTime : UpTime);
                t = Mathf.MoveTowards(t, target, speed * Time.unscaledDeltaTime);
            }
            Apply();
            try { DockTablet(); } catch (Exception ex) { Debug.LogWarning("[UiEnhancer] dock: " + ex.Message); Undock(); }
        }

        // 윈도우는 거래창과 붙어 다닌다: 거래창과 같은 부모, 거래창 바로 앞 순서(= 한 단계 뒤에 그려짐)
        private void StickToTablet()
        {
            GameObject ui = NegociationUIManager.Instance != null ? NegociationUIManager.Instance.uiObject : null;
            if (ui == null) return;
            Transform tab = ui.transform, parent = tab.parent;
            if (parent == null) return;
            if (transform.parent != parent)
            {
                transform.SetParent(parent, false);
                hostCanvas = parent.GetComponentInParent<Canvas>()?.rootCanvas;
                Debug.Log("[UiEnhancer] window moved next to the trade window (" + parent.name + ")");
            }
            int mine = transform.GetSiblingIndex(), tabIndex = tab.GetSiblingIndex();
            if (mine != tabIndex - 1)
                transform.SetSiblingIndex(mine < tabIndex ? tabIndex - 1 : tabIndex); // 내가 앞에 있으면 거래창 번호가 하나 당겨지는 것 고려
        }

        // 기준 픽셀 1 = 캔버스 몇 단위인지
        private float CanvasUnit()
        {
            float sf = hostCanvas != null && hostCanvas.scaleFactor > 0f ? hostCanvas.scaleFactor : 1f;
            return (Screen.height / RefHeight) / sf;
        }

        // 다 내려왔을 때 윈도우 위치 (캔버스 단위)
        private float DownY() => -(TopMargin + size.y) * CanvasUnit();

        private void Apply()
        {
            if (rt == null) return;
            rt.sizeDelta = size * CanvasUnit();
            // 숨음: 그림 아래쪽이 화면 위 끝(0) / 다 내려옴: 그림 위쪽이 화면 위에서 TopMargin 아래
            float e = shown ? EaseOut(t) : EaseIn(t);
            rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, DownY(), e));
        }

        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x); // 내려올 때: 빠르게 → 천천히
        private static float EaseIn(float x) => 1f - (1f - x) * (1f - x);             // 올라갈 때

        private void OnDestroy() => Undock();
    }
}
