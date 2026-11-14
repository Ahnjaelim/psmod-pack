using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AppraisalPresets
{
    // 감정 서비스 입력 창: 원래의 가격 +/- 버튼 줄 바로 밑에 "기본 N" / "최저 N" 버튼 줄을 붙인다.
    // 누르면 지금 보는 감정 품목 종류의 기본값/최저값이 입력 칸에 들어간다.
    //   의수 100 / 50, 립위치 150 / 40, 플라즈마 연료 300 / 90  (게임 GetCorrectValue 의 시작값 / 하한)
    // 버튼은 게임의 +/- 버튼 줄을 복제해서 모양을 맞춘다. 종류를 모르는(다른 모드가 추가한) 품목이면 줄을 숨긴다.
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.

    [HarmonyPatch(typeof(InspectionUIManager), nameof(InspectionUIManager.UpdateAppraisalPanel))]
    internal static class UpdateAppraisalPanelPatch
    {
        private const string RowName = "PSMP_AppraisalPresetRow";

        private static readonly Dictionary<string, int[]> Values = new Dictionary<string, int[]>
        {
            { "hand", new[] { 100, 50 } },
            { "ribwich", new[] { 150, 40 } },
            { "plasma_fuel", new[] { 300, 90 } },
        };

        private static GameObject _row;
        private static Button _baseBtn, _minBtn;
        private static GameItem _lastItem;
        private static bool _failed;

        // UpdateAppraisalPanel 은 패널이 열려 있는 동안 매 프레임 불린다 → 만들기는 한 번, 라벨은 아이템이 바뀔 때만.
        private static void Postfix(InspectionUIManager __instance)
        {
            if (_failed || __instance == null || __instance.appraisalPanel == null) return;
            try
            {
                if (!__instance.appraisalPanel.activeSelf)
                {
                    _lastItem = null;
                    return;
                }
                GameItem item = __instance.currentInspectingItem;
                bool changed = item != _lastItem;
                _lastItem = item;
                AppraisalCalculator.Tick(__instance, TypeIdOf(item), changed);

                if (_row == null && !Build(__instance)) return;
                if (!changed) return;

                int[] v = ValuesFor(item);
                _row.SetActive(v != null);
                if (v == null) return;
                SetLabel(_baseBtn, PenLabelerAssist.Text.Get("appraisal_btn_base", v[0]));
                SetLabel(_minBtn, PenLabelerAssist.Text.Get("appraisal_btn_min", v[1]));
            }
            catch (System.Exception ex)
            {
                _failed = true; // 한 번 실패하면 매 프레임 로그가 쌓이지 않게 끈다
                Debug.LogWarning("[AppraisalPresets] disabled: " + ex.Message);
                if (_row != null) _row.SetActive(false);
            }
        }

        private static string TypeIdOf(GameItem item)
        {
            if (item == null) return null;
            AppraisalItemType type = AppraisalItemRegistry.Find(item);
            return type?.id;
        }

        private static int[] ValuesFor(GameItem item)
        {
            string id = TypeIdOf(item);
            return id != null && Values.TryGetValue(id, out int[] v) ? v : null;
        }

        private static void Apply(InspectionUIManager ui, int pick)
        {
            int[] v = ValuesFor(ui.currentInspectingItem);
            if (v == null || ui.apprisalServiceInputField == null) return;
            int value = v[pick];
            ui.apprisalServiceInputField.text = value.ToString();
            ui.currentInspectingItem.ModifyTag("APPRAISAL_DEFAULT_VALUE", delegate (TagState state)
            {
                state.Enable().SetInt(value);
            });
        }

        // 게임의 가격 +/- 버튼(ChangeAppraisalServicePrice 에 연결된 버튼)을 찾아 그 줄을 복제한다.
        private static bool Build(InspectionUIManager ui)
        {
            Transform panel = ui.appraisalPanel.transform;
            var priceButtons = new List<Button>();
            foreach (Button b in panel.GetComponentsInChildren<Button>(true))
            {
                if (b == null || b.transform.parent != null && b.transform.parent.name == RowName) continue;
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                {
                    if (b.onClick.GetPersistentMethodName(i) == "ChangeAppraisalServicePrice")
                    {
                        priceButtons.Add(b);
                        break;
                    }
                }
            }
            if (priceButtons.Count == 0)
            {
                _failed = true;
                Debug.LogWarning("[AppraisalPresets] price buttons not found; feature off");
                return false;
            }

            Transform row = priceButtons[0].transform.parent;
            bool rowIsContainer = row != null && row != panel && AllUnder(priceButtons, row);

            if (rowIsContainer)
            {
                // 버튼 줄 전체를 복제해서 바로 밑 형제로. 부모가 세로 레이아웃이면 자동으로 밑에 붙는다.
                GameObject clone = Object.Instantiate(row.gameObject, row.parent);
                clone.name = RowName;
                clone.transform.SetSiblingIndex(row.GetSiblingIndex() + 1);
                if (row.parent.GetComponent<LayoutGroup>() == null)
                    MoveBelow((RectTransform)clone.transform, (RectTransform)row);

                // 복제본 안의 버튼 중 앞의 두 개만 쓰고 나머지 자식은 지운다.
                Button[] cloned = clone.GetComponentsInChildren<Button>(true);
                if (cloned.Length < 2)
                {
                    Object.Destroy(clone);
                    _failed = true;
                    return false;
                }
                _baseBtn = cloned[0];
                _minBtn = cloned[1];
                for (int i = clone.transform.childCount - 1; i >= 0; i--)
                {
                    Transform c = clone.transform.GetChild(i);
                    if (c != _baseBtn.transform && c != _minBtn.transform) Object.Destroy(c.gameObject);
                }
                _row = clone;
            }
            else
            {
                // 버튼이 패널에 바로 놓인 경우: 버튼 두 개를 각각 복제해서 원래 버튼 바로 밑에 둔다.
                if (priceButtons.Count < 2)
                {
                    _failed = true;
                    return false;
                }
                var holder = new GameObject(RowName, typeof(RectTransform));
                holder.AddComponent<LayoutElement>().ignoreLayout = true;
                var hrt = (RectTransform)holder.transform;
                hrt.SetParent(panel, false);
                hrt.anchorMin = Vector2.zero;
                hrt.anchorMax = Vector2.one;
                hrt.offsetMin = Vector2.zero;
                hrt.offsetMax = Vector2.zero;
                _baseBtn = CloneBelow(priceButtons[0], hrt);
                _minBtn = CloneBelow(priceButtons[priceButtons.Count - 1], hrt);
                _row = holder;
            }

            Hook(_baseBtn, () => Apply(ui, 0));
            Hook(_minBtn, () => Apply(ui, 1));
            return true;
        }

        private static bool AllUnder(List<Button> buttons, Transform row)
        {
            foreach (Button b in buttons)
                if (b.transform.parent != row) return false;
            return true;
        }

        private static Button CloneBelow(Button src, RectTransform holder)
        {
            var srcRt = (RectTransform)src.transform;
            GameObject go = Object.Instantiate(src.gameObject, srcRt.parent);
            var rt = (RectTransform)go.transform;
            rt.SetParent(holder, true); // 월드 위치 유지
            rt.position = srcRt.position;
            rt.anchoredPosition += new Vector2(0f, -(srcRt.rect.height + 2f));
            return go.GetComponent<Button>();
        }

        private static void MoveBelow(RectTransform clone, RectTransform row)
        {
            clone.anchorMin = row.anchorMin;
            clone.anchorMax = row.anchorMax;
            clone.pivot = row.pivot;
            clone.sizeDelta = row.sizeDelta;
            clone.anchoredPosition = row.anchoredPosition + new Vector2(0f, -(row.rect.height + 2f));
        }

        private static void Hook(Button b, UnityEngine.Events.UnityAction action)
        {
            // 프리팹의 영구 리스너(ChangeAppraisalServicePrice)는 RemoveAllListeners 로 안 지워지므로 이벤트를 통째로 교체
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(action);
        }

        private static void SetLabel(Button b, string text)
        {
            if (b == null) return;
            TMP_Text tmp = b.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = text;
                tmp.enableWordWrapping = false;
                tmp.enableAutoSizing = true; // "+10" 크기 버튼에 "기본 150" 이 들어가도록
                tmp.fontSizeMax = tmp.fontSize > 0 ? tmp.fontSize : 18f;
                tmp.fontSizeMin = 6f;
                return;
            }
            Text legacy = b.GetComponentInChildren<Text>(true);
            if (legacy != null)
            {
                legacy.text = text;
                legacy.resizeTextForBestFit = true;
            }
        }
    }
}
