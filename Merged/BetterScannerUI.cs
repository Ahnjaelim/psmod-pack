using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterScannerUI
{
    // 워터 스캐너 / 화학 스캐너의 성분 목록을
    //   성분명(좌측 정렬) | 막대그래프(퍼센트를 막대 위에 겹쳐 씀) | 양(우측 정렬, 흐리게)
    // 형태로, 양 많은 순으로 다시 그린다.
    //
    // 원본 RefreshUI 가 contaminantTMP.text 를 "이름: 양 (xx.xx%)\n" 로 채운 뒤 Postfix 에서 덮어쓴다.
    // 막대는 폰트 글리프(█ 등)가 없을 수 있어서 텍스트가 아닌 UI Image 로 그린다.
    // 막대 색은 게임의 Liquid.color 를 사용.
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.

    [HarmonyPatch(typeof(WaterScannerUIManager), nameof(WaterScannerUIManager.RefreshUI))]
    internal static class WaterScannerRefreshPatch
    {
        private static void Postfix(WaterScannerUIManager __instance)
        {
            ScannerBars.Rebuild(__instance.contaminantTMP, __instance.currentWaterContainer);
        }
    }

    [HarmonyPatch(typeof(ChemScannerUIManager), nameof(ChemScannerUIManager.RefreshUI))]
    internal static class ChemScannerRefreshPatch
    {
        private static void Postfix(ChemScannerUIManager __instance)
        {
            ScannerBars.Rebuild(__instance.contaminantTMP, __instance.currentScannedItem);
        }
    }

    internal static class ScannerBars
    {
        private const string ContainerName = "BSUI_Bars";
        private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color FallbackFill = new Color32(0x67, 0xaa, 0xa5, 0xff);
        private const string AmountColor = "#9a95a8";

        private struct Entry
        {
            public string Name;
            public int Amount;
            public float Pct;
            public Color Color;
        }

        public static void Rebuild(TextMeshProUGUI tmp, GameItem item)
        {
            if (tmp == null) return;
            try
            {
                RebuildInner(tmp, item);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BetterScannerUI] rebuild failed: " + ex.Message);
                HideBars(tmp);
            }
        }

        private static void RebuildInner(TextMeshProUGUI tmp, GameItem item)
        {
            // 원본이 비워둔 경우(빈 용기 등)는 그대로 둔다.
            if (item == null || string.IsNullOrEmpty(tmp.text))
            {
                HideBars(tmp);
                return;
            }

            var entries = new List<Entry>();
            int total = 0;
            foreach (Liquid liquid in Liquid.Liquids)
                total += item.GetTagReadonly(liquid.currentPart).GetInt();
            if (total <= 0)
            {
                HideBars(tmp);
                return;
            }
            foreach (Liquid liquid in Liquid.Liquids)
            {
                int amount = item.GetTagReadonly(liquid.currentPart).GetInt();
                if (amount <= 0) continue;
                entries.Add(new Entry
                {
                    Name = ResolveName(liquid),
                    Amount = amount,
                    Pct = amount / (float)total * 100f,
                    Color = ParseColor(liquid.color),
                });
            }
            if (entries.Count == 0)
            {
                HideBars(tmp);
                return;
            }
            // 양 많은 순 (같으면 이름순)
            entries.Sort((a, b) =>
            {
                int c = b.Amount.CompareTo(a.Amount);
                return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
            });

            RectTransform rt = tmp.rectTransform;
            float width = rt.rect.width;
            float gap = tmp.fontSize * 0.6f;
            float inset = tmp.fontSize * 0.3f; // 막대 안 퍼센트 글자의 왼쪽 여백

            // 퍼센트는 막대 위에 겹쳐 쓰고(막대 왼쪽 안쪽), 오른쪽 열에는 흐린 양(uL / mL / L)만 둔다.
            var pctStrings = new string[entries.Count];
            var amtStrings = new string[entries.Count];
            float nameCol = 0f;
            float amtCol = 0f;
            float pctMax = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                pctStrings[i] = entries[i].Pct.ToString("F2", CultureInfo.InvariantCulture) + "%";
                amtStrings[i] = "<color=" + AmountColor + ">" + WaterScannerUIManager.FormatAmount(entries[i].Amount) + "</color>";
                nameCol = Mathf.Max(nameCol, tmp.GetPreferredValues(entries[i].Name).x);
                amtCol = Mathf.Max(amtCol, tmp.GetPreferredValues(amtStrings[i]).x);
                pctMax = Mathf.Max(pctMax, tmp.GetPreferredValues(pctStrings[i]).x);
            }

            if (width > 0f) nameCol = Mathf.Min(nameCol, width * 0.45f);
            float barStart = nameCol + gap;
            float barEnd = width - amtCol - gap;
            // 막대 안에 퍼센트가 들어갈 만큼 넓어야 겹쳐 그린다. 아니면 글자만 (이름  양  퍼센트).
            bool drawBars = width > 0f && barEnd - barStart >= pctMax + inset * 2f;

            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = false;
            var sb = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                sb.Append(entries[i].Name);
                if (drawBars)
                {
                    sb.Append("<pos=").Append(Pct(barStart + inset, width)).Append("%>").Append(pctStrings[i]);
                    float amtX = width - tmp.GetPreferredValues(amtStrings[i]).x;
                    sb.Append("<pos=").Append(Pct(amtX, width)).Append("%>").Append(amtStrings[i]);
                }
                else
                {
                    sb.Append("  ").Append(amtStrings[i]).Append("  ").Append(pctStrings[i]);
                }
                if (i < entries.Count - 1) sb.Append('\n');
            }
            tmp.text = sb.ToString();

            if (!drawBars)
            {
                HideBars(tmp);
                return;
            }

            tmp.ForceMeshUpdate();
            TMP_TextInfo info = tmp.textInfo;
            if (info == null || info.lineCount != entries.Count)
            {
                // 줄 수가 안 맞으면(예상 밖 레이아웃) 막대는 생략하고 텍스트만 보여준다.
                HideBars(tmp);
                return;
            }

            RectTransform container = GetContainer(tmp);
            if (container == null)
            {
                HideBars(tmp);
                return;
            }
            container.gameObject.SetActive(true);
            float left = rt.rect.xMin;
            float barWidth = barEnd - barStart;

            for (int i = 0; i < entries.Count; i++)
            {
                TMP_LineInfo line = info.lineInfo[i];
                float yMid = (line.ascender + line.descender) * 0.5f;
                // 글자가 올라가므로 막대를 줄 높이에 가깝게
                float h = Mathf.Max(2f, (line.ascender - line.descender) * 0.9f);

                Image track = GetBar(container, i * 2);
                Place(track.rectTransform, left + barStart, yMid, barWidth, h);
                track.color = TrackColor;

                Image fill = GetBar(container, i * 2 + 1);
                float fw = Mathf.Max(2f, barWidth * entries[i].Pct / 100f);
                Place(fill.rectTransform, left + barStart, yMid, fw, h);
                fill.color = entries[i].Color;
            }
            // 남는 막대 숨김
            for (int c = entries.Count * 2; c < container.childCount; c++)
                container.GetChild(c).gameObject.SetActive(false);
        }

        private static string Pct(float x, float width)
        {
            return Mathf.Clamp(x / width * 100f, 0f, 100f).ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string ContainerNameFor(TextMeshProUGUI tmp)
        {
            return ContainerName + "_" + tmp.GetInstanceID();
        }

        // 막대는 텍스트의 "형제"로, 텍스트 바로 앞에 둔다. UI 는 나중 형제가 위에 그려지므로
        // 이렇게 해야 퍼센트 글자가 막대 위에 보인다 (자식으로 두면 막대가 글자를 덮음).
        // 컨테이너는 텍스트와 같은 위치/크기를 따라가고, 막대는 피벗 기준 좌표를 쓴다.
        private static RectTransform GetContainer(TextMeshProUGUI tmp)
        {
            RectTransform t = tmp.rectTransform;
            Transform parent = t.parent;
            if (parent == null) return null;

            Transform existing = parent.Find(ContainerNameFor(tmp));
            RectTransform rt;
            if (existing != null)
            {
                rt = (RectTransform)existing;
            }
            else
            {
                var go = new GameObject(ContainerNameFor(tmp), typeof(RectTransform), typeof(LayoutElement));
                go.GetComponent<LayoutElement>().ignoreLayout = true; // 부모 레이아웃 그룹이 줄 세우지 않게
                rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
            }
            rt.anchorMin = t.anchorMin;
            rt.anchorMax = t.anchorMax;
            rt.pivot = t.pivot;
            rt.anchoredPosition = t.anchoredPosition;
            rt.sizeDelta = t.sizeDelta;
            rt.localRotation = t.localRotation;
            rt.localScale = t.localScale;
            // 텍스트 바로 앞 순서로
            int ti = t.GetSiblingIndex();
            int ci = rt.GetSiblingIndex();
            if (ci != ti - 1) rt.SetSiblingIndex(ci < ti ? ti - 1 : ti);
            return rt;
        }

        private static Image GetBar(RectTransform container, int index)
        {
            if (index < container.childCount)
            {
                var child = container.GetChild(index);
                child.gameObject.SetActive(true);
                return child.GetComponent<Image>();
            }
            var go = new GameObject("Bar" + index, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(container, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            var rt = img.rectTransform;
            // 컨테이너 피벗 = 텍스트 피벗 → 텍스트 로컬 좌표를 그대로 쓸 수 있다.
            rt.anchorMin = container.pivot;
            rt.anchorMax = container.pivot;
            rt.pivot = new Vector2(0f, 0.5f);
            return img;
        }

        // x, y 는 TMP 로컬 좌표(피벗 기준)
        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void HideBars(TextMeshProUGUI tmp)
        {
            Transform parent = tmp.transform.parent;
            Transform existing = parent != null ? parent.Find(ContainerNameFor(tmp)) : null;
            if (existing != null) existing.gameObject.SetActive(false);
        }

        // Liquid.displayName 은 게임 시작 시 한 번만 번역되어 저장된다.
        // 그 시점에 번역 테이블이 준비되지 않았으면 "Translation Error 'liquid_xxx' in Mechanic" 이 그대로 남으므로
        // 키를 뽑아 다시 번역하고, 성공하면 원본 필드도 고쳐 둔다 (다른 화면에도 반영).
        private static readonly Regex BrokenName = new Regex(@"Translation Error '([^']+)'", RegexOptions.Compiled);

        // 모든 액체 이름을 한 번에 복구한다. 다 고쳐지면 이후 호출은 바로 끝난다.
        // 스캐너뿐 아니라 동물 툴팁(체내 혈청) 등 다른 화면에도 액체 이름이 쓰이므로 툴팁 생성 시점마다 호출된다.
        private static bool _allRepaired;

        internal static void RepairAllLiquidNames()
        {
            if (_allRepaired) return;
            try
            {
                if (Liquid.Liquids == null || Liquid.Liquids.Count == 0) return;
                bool anyBroken = false;
                foreach (Liquid liquid in Liquid.Liquids)
                {
                    if (liquid == null || string.IsNullOrEmpty(liquid.displayName)) continue;
                    if (!BrokenName.IsMatch(liquid.displayName)) continue;
                    ResolveName(liquid);
                    if (BrokenName.IsMatch(liquid.displayName)) anyBroken = true;
                }
                _allRepaired = !anyBroken;
            }
            catch
            {
            }
        }

        internal static string ResolveName(Liquid liquid)
        {
            string name = liquid.displayName;
            if (string.IsNullOrEmpty(name)) return liquid.identifier ?? "?";
            Match m = BrokenName.Match(name);
            if (!m.Success) return name;

            string key = m.Groups[1].Value;
            // 1) 모드팩 자체 번역 (Localization/ko.csv). 한글 번역 모드 테이블에 액체 이름이 빠져 있어서 필요.
            try
            {
                string own = PenLabelerAssist.Text.Get(key);
                if (IsGood(own, key))
                {
                    liquid.displayName = own;
                    return own;
                }
            }
            catch
            {
            }
            // 2) 게임 번역 테이블에서 다시 찾기
            try
            {
                string again = LocHelper.RelocalizeIfMissing(name);
                if (IsGood(again, key))
                {
                    liquid.displayName = again;
                    return again;
                }
            }
            catch
            {
            }
            // 그래도 실패하면 최소한 키에서 읽을 만한 이름을 만든다 (liquid_heavy_metal -> heavy metal)
            return key.StartsWith("liquid_") ? key.Substring(7).Replace('_', ' ') : key;
        }

        private static bool IsGood(string text, string key)
        {
            return !string.IsNullOrEmpty(text) && text != key && !BrokenName.IsMatch(text);
        }

        private static Color ParseColor(string hex)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out Color c))
            {
                c.a = 1f;
                return c;
            }
            return FallbackFill;
        }
    }
}
