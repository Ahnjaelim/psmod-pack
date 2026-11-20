using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapExtension
{
    // 외출 HUD: 화면 왼쪽 아래 고정. 지도가 열려 있거나 어느 장소에 있든 체력/기력 바 두 줄.
    //  - 체력 = (10 - 부상 수치 woundState) × 10 (0~100). 40 이하 = 중상(외출 불가)
    //  - 기력 = 남은 수색 횟수 (ScavHelper)
    // 게임 UI 와 따로 맨 위 오버레이 캔버스(1920x1080 기준 크기)에 그린다.
    internal class OutingHud : MonoBehaviour
    {
        private const float W = 320f, H = 22f, Gap = 6f, Margin = 20f, Font = 15f;
        private static readonly Color Bg = new Color32(0x24, 0x1f, 0x2e, 0xf0);
        private static readonly Color Edge = new Color32(0x8b, 0xc4, 0xbe, 0xff);
        private static readonly Color HpFull = new Color32(0x6c, 0xd0, 0x6c, 0xff);
        private static readonly Color EnFull = new Color32(0x4d, 0xf0, 0xe0, 0xff);
        private static readonly Color Low = new Color32(0xff, 0x5a, 0x5a, 0xff);

        private static OutingHud instance;

        private GameObject panel;
        private Bar hp, energy;
        private TextMeshProUGUI cash;
        private string lastHp, lastEn, lastCash;

        private class Bar
        {
            public RectTransform fill;
            public Image fillImg;
            public TextMeshProUGUI text;
        }

        internal static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("VME_OutingHud");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            instance = go.AddComponent<OutingHud>();
            instance.BuildUi();
        }

        internal static void Remove()
        {
            if (instance != null) Destroy(instance.gameObject);
            instance = null;
        }

        private void BuildUi()
        {
            panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = prt.pivot = Vector2.zero; // 왼쪽 아래
            prt.anchoredPosition = new Vector2(Margin, Margin);
            prt.sizeDelta = new Vector2(W, H * 2f + Gap);
            hp = MakeBar(prt, "Hp", H + Gap, HpFull);     // 위: 체력
            // 맨 위: 소지금
            var cgo = new GameObject("Cash", typeof(RectTransform));
            cgo.transform.SetParent(prt, false);
            cash = cgo.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset cf = FontLoader.Instance != null ? (FontLoader.Instance.notoSans_Normal ?? FontLoader.Instance.notoSans) : null;
            if (cf != null) cash.font = cf;
            cash.fontSize = Font + 3f;
            cash.color = new Color32(0xff, 0xd8, 0x4a, 0xff);
            cash.alignment = TextAlignmentOptions.BottomLeft;
            cash.enableWordWrapping = false;
            cash.overflowMode = TextOverflowModes.Overflow;
            cash.raycastTarget = false;
            var crt = cash.rectTransform;
            crt.anchorMin = crt.anchorMax = crt.pivot = Vector2.zero;
            crt.anchoredPosition = new Vector2(2f, (H + Gap) * 2f);
            crt.sizeDelta = new Vector2(W, 26f);
            energy = MakeBar(prt, "Energy", 0f, EnFull);   // 아래: 기력
            panel.SetActive(false);
        }

        private static Bar MakeBar(RectTransform parent, string name, float y, Color full)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(W, H);
            var edge = go.AddComponent<Image>();
            edge.color = Edge;
            edge.raycastTarget = false;
            Inner(rt, "Bg", Bg);
            Image fill = Inner(rt, "Fill", full);

            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(rt, false);
            var t = tgo.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = FontLoader.Instance != null ? (FontLoader.Instance.notoSans_Normal ?? FontLoader.Instance.notoSans) : null;
            if (font != null) t.font = font;
            t.fontSize = Font;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.color = Color.white;
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            return new Bar { fill = fill.rectTransform, fillImg = fill, text = t };
        }

        private static Image Inner(RectTransform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(2f, 2f);
            rt.sizeDelta = new Vector2(W - 4f, H - 4f);
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        // 외출 중 = 어느 장소에 있거나, 외출 지도가 열려 있을 때
        private static bool Outing()
        {
            try
            {
                MapUIManager map = MapUIManager.Instance;
                if (map != null && map.isOutside) return true;
                GameObject root = TransitUI.Root;
                return root != null && root.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        private void Update()
        {
            bool show = PlayerStore.Instance != null && Outing();
            if (panel.activeSelf != show) panel.SetActive(show);
            if (!show) return;
            Place();
            try
            {
                // 소지금
                string cs = "소지금 " + PlayerStore.Instance.GetCash().ToString("N0") + " ₵";
                if (cs != lastCash)
                {
                    lastCash = cs;
                    ScriptFontSwap.Apply(cash, cs);
                }
                // 체력
                HealthData hd = PlayerStore.Instance.healthData;
                int h = Mathf.Clamp((10 - hd.woundState) * 10, 0, 100);
                string hs = "체력 " + h + " / 100" + (h <= 40 ? " (중상)" : (h < 100 && !hd.isWoundStable ? " (불안정)" : ""));
                if (hs != lastHp)
                {
                    lastHp = hs;
                    Set(hp, h / 100f, HpFull, hs, 0.3f);
                }
                // 기력
                int now = ScavHelper.GetScavTimeLeft();
                int max = FarmLocation.TestEnergy > 0 ? FarmLocation.TestEnergy : ScavHelper.GetMaxScavAttempts();
                string es = hd.isWoundedFresh ? "기력 " + now + " / " + max + " (부상: 수색 불가)" : "기력 " + now + " / " + max;
                if (es != lastEn)
                {
                    lastEn = es;
                    Set(energy, max > 0 ? (float)now / max : 0f, EnFull, es, 0f);
                }
            }
            catch
            {
            }
        }

        // 지도가 열려 있으면 지도 그림 영역 안 왼쪽 아래, 아니면 화면 왼쪽 아래
        private void Place()
        {
            var prt = (RectTransform)panel.transform;
            Vector2 pos = new Vector2(Margin, Margin);
            try
            {
                RectTransform area = TransitMap.MapArea;
                if (area != null)
                {
                    var c = new Vector3[4];
                    area.GetWorldCorners(c);
                    Canvas ac = area.GetComponentInParent<Canvas>()?.rootCanvas;
                    Camera cam = ac != null && ac.renderMode != RenderMode.ScreenSpaceOverlay ? ac.worldCamera : null;
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
                    var crt = (RectTransform)transform;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(crt, screen, null, out Vector2 local))
                        pos = local - crt.rect.min + new Vector2(16f, 16f);
                }
            }
            catch
            {
            }
            if ((prt.anchoredPosition - pos).sqrMagnitude > 0.25f) prt.anchoredPosition = pos;
        }

        private static void Set(Bar b, float r, Color full, string text, float lowStart)
        {
            r = Mathf.Clamp01(r);
            b.fill.sizeDelta = new Vector2((W - 4f) * r, H - 4f);
            b.fillImg.color = Color.Lerp(Low, full, Mathf.Clamp01((r - lowStart) / 0.5f));
            ScriptFontSwap.Apply(b.text, text);
        }
    }
}
