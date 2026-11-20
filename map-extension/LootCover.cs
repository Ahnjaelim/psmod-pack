using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MapExtension
{
    // 상자 안 아이템 칸 위에 씌우는 빗금 덮개(타르코프식). 클릭하면 벗겨져서 아이템이 보인다.
    // 클릭(PointerClick)만 덮개가 받고, 누르기/끌기는 부모(아이템 TreeNodeRender)로 넘어가서 덮인 채로도 옮길 수 있다.
    internal class LootCover : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly List<LootCover> all = new List<LootCover>();
        private static readonly Color CoverColor = new Color32(0x2c, 0x27, 0x38, 0xff);
        private static readonly Color CoverHover = new Color32(0x3c, 0x36, 0x4c, 0xff);

        public GameItem item;
        private Image img;
        private Image bar;
        private bool revealing;

        // 안 깐 물건 표시 (아이템 태그 → 세이브에 같이 저장됨)
        internal const string HiddenTag = "VME_UNREVEALED_TAG";

        // IsTag 는 modifiedState 를 보는데 EnableTag(skipValidate) 는 state 만 바꾸므로 state 를 직접 본다
        internal static bool IsHidden(GameItem i)
        {
            try { return i != null && i.state != null && i.state.IsTag(HiddenTag); }
            catch { return false; }
        }

        internal static void EnsureCovers(IEnumerable<GameItem> items)
        {
            if (items == null) return;
            foreach (GameItem i in new List<GameItem>(items))
                if (IsHidden(i)) Attach(i);
        }

        internal static void Attach(GameItem item)
        {
            if (!(item is GameItemElement e) || e.middle == null) return;
            item.EnableTag(HiddenTag, true);
            if (e.middle.transform.Find("VME_Cover") != null) return; // 이미 덮여 있음
            var go = new GameObject("VME_Cover", typeof(RectTransform));
            go.transform.SetParent(e.middle.transform, false);
            go.transform.SetAsLastSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(1f, 1f);
            rt.offsetMax = new Vector2(-1f, -1f);
            // 타르코프식 빗금: 작은 빗금 무늬를 바둑판(Tiled)으로 깐다
            var img = go.AddComponent<Image>();
            img.sprite = Stripes();
            img.type = Image.Type.Tiled;
            img.color = Color.white;
            img.raycastTarget = true;

            // 공개 진행 막대 (덮개 아래쪽, 처음엔 0)
            var bgo = new GameObject("Bar", typeof(RectTransform));
            bgo.transform.SetParent(go.transform, false);
            var brt = (RectTransform)bgo.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(0f, 0f);
            brt.pivot = new Vector2(0f, 0f);
            brt.anchoredPosition = new Vector2(2f, 2f);
            brt.sizeDelta = new Vector2(0f, 3f);
            var bar = bgo.AddComponent<Image>();
            bar.color = new Color32(0x9a, 0xd6, 0xcf, 0xff);
            bar.raycastTarget = false;

            var c = go.AddComponent<LootCover>();
            c.bar = bar;
            c.item = item;
            c.img = img;
            all.Add(c);
        }

        internal static void RemoveAll()
        {
            foreach (LootCover c in all)
                if (c != null) Destroy(c.gameObject);
            all.Clear();
        }

        // keep 에 든 아이템(상자 안에 남은 것)의 덮개만 남기고 나머지 정리
        internal static void RemoveOutside(HashSet<GameItem> keep)
        {
            for (int i = all.Count - 1; i >= 0; i--)
            {
                LootCover c = all[i];
                if (c == null) { all.RemoveAt(i); continue; }
                if (keep != null && c.item != null && keep.Contains(c.item)) continue;
                try { c.item?.DisableTag(HiddenTag, true); } catch { }
                Destroy(c.gameObject);
                all.RemoveAt(i);
            }
        }

        public void OnPointerEnter(PointerEventData e) { if (img != null) img.color = new Color(1.25f, 1.25f, 1.3f, 1f); }
        public void OnPointerExit(PointerEventData e) { if (img != null) img.color = Color.white; }

        // 8x8 빗금 무늬 (어두운 바탕 + 대각선 2px)
        private static Sprite stripes;
        private static Sprite Stripes()
        {
            if (stripes != null) return stripes;
            const int n = 8;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            Color bg = new Color32(0x26, 0x25, 0x2b, 0xff), line = new Color32(0x3a, 0x39, 0x41, 0xff);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    tex.SetPixel(x, y, ((x + y) % n) < 2 ? line : bg);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            stripes = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return stripes;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || revealing) return;
            StartCoroutine(RevealSlowly());
        }

        // 잠깐 뜸 들이기: 막대가 차오르고 "…" 이 깜빡인 뒤 공개. 비싼 물건일수록 조금 더 걸린다.
        private IEnumerator RevealSlowly()
        {
            revealing = true;
            long v = 0;
            try { v = item != null ? item.unitValue : 0; } catch { }
            float dur = v >= 1500 ? 1.3f : v >= 600 ? 1.05f : v >= 200 ? 0.85f : 0.7f;
            var rt = (RectTransform)transform;
            float full = Mathf.Max(rt.rect.width - 4f, 1f);
            try { AudioManager.Instance.Play2("click"); } catch { }
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                if (bar != null) bar.rectTransform.sizeDelta = new Vector2(full * Mathf.Clamp01(t / dur), 3f);
                yield return null;
            }
            Reveal();
        }

        private void Reveal()
        {
            all.Remove(this);
            try { item?.DisableTag(HiddenTag, true); } catch { }
            Color rc = RarityColor(item);
            bool good = rc.r > 0.9f && rc.g > 0.7f; // 금색
            try { AudioManager.Instance.Play2(good ? "click_vintage" : "debris"); } catch { }
            try
            {
                string name = item != null ? item.name : "?";
                StoreUIManager.Instance.Notify("<color=#" + ColorUtility.ToHtmlStringRGB(rc) + ">" + name + "</color>");
            }
            catch
            {
            }
            Destroy(gameObject);
        }

        // 단가로 등급 색
        internal static Color RarityColor(GameItem item)
        {
            long v = 0;
            try { v = item != null ? item.unitValue : 0; } catch { }
            if (v >= 1500) return new Color32(0xff, 0xc8, 0x3d, 0xff); // 금
            if (v >= 600) return new Color32(0xc0, 0x7c, 0xff, 0xff);  // 보라
            if (v >= 200) return new Color32(0x5a, 0xa8, 0xff, 0xff);  // 파랑
            if (v >= 50) return new Color32(0x6c, 0xd0, 0x6c, 0xff);   // 초록
            return new Color32(0xb0, 0xb0, 0xb8, 0xff);                 // 회색
        }
    }

    // 안 깐 물건은 툴팁(이름·가격·설명)이 뜨지 않게
    [HarmonyLib.HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.HasTooltip))]
    internal static class HiddenTooltipPatch
    {
        private static void Postfix(GameItemElement __instance, ref bool __result)
        {
            try
            {
                if (__result && LootCover.IsHidden(__instance)) __result = false;
            }
            catch
            {
            }
        }
    }
}
