using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AppraisalPresets
{
    // 감정 계산기: 감정 입력 창 안, 기본/최저 버튼 줄 밑에 붙는 체크박스 블록 (카테고리별 가로 나열).
    // 플레이어가 직접 보고 판단한 상태를 체크하면 게임의 GetCorrectValue 와 같은 식으로 값을 계산해서
    // 입력 칸에 바로 넣는다 (숨겨진 정답을 읽지 않음).
    //   의수:  AppHandHelper.GetCorrectValue   (기본 100, 최저 50, 세트 보너스 포함)
    //   립위치: RibwichHelper.GetCorrectValue   (기본 150, 최저 40, 개봉/가짜면 40)
    //   플라즈마: PlasmaHelper.GetCorrectValue   (기본 300, 최저 90, 씰 보너스 포함)
    // 블록은 appraisalPanel 안에 있어서 감정 창이 닫히면 같이 사라진다.
    internal static class AppraisalCalculator
    {
        private const string PanelName = "PSMP_AppraisalCalc";

        private sealed class Opt
        {
            public string Id, Group, Label;
            public int Delta;
            public Toggle Toggle;
        }

        private sealed class Section
        {
            public string TypeId;
            public GameObject Root;
            public readonly List<Opt> Opts = new List<Opt>();
            public Func<HashSet<string>, int> Compute;
        }

        private static GameObject _panel;
        private static TextMeshProUGUI _result;
        private static readonly Dictionary<string, Section> Sections = new Dictionary<string, Section>();
        private static Section _active;
        private static InspectionUIManager _ui;
        private static bool _failed, _syncing;

        // ── 계산식 (게임 코드와 같은 순서/조건) ──
        private static int Hand(HashSet<string> s)
        {
            int v = 100;
            if (s.Contains("gold")) v += 200; else if (s.Contains("carbon")) v += 50;
            if (s.Contains("neural")) v += 120; else if (s.Contains("emg")) v += 45; else if (s.Contains("unspec")) v -= 60;
            if (s.Contains("wear")) v -= 35; else if (s.Contains("damage")) v -= 85;
            if (s.Contains("armored")) v += 50;
            if (s.Contains("bladed")) v += 80;
            bool pristine = !s.Contains("wear") && !s.Contains("damage");
            if (s.Contains("carbon") && (s.Contains("emg") || s.Contains("neural")) && pristine && s.Contains("armored") && s.Contains("bladed")) v += 100;
            if (s.Contains("gold") && s.Contains("neural") && pristine) v += 200;
            return Math.Max(v, 50);
        }

        private static int Ribwich(HashSet<string> s)
        {
            if (s.Contains("opened") || s.Contains("fake")) return 40;
            int v = 150;
            if (s.Contains("meat")) v += 800;
            if (s.Contains("deluxe")) v += 300;
            if (s.Contains("edge_month")) v += 50;
            if (s.Contains("first_year")) v += 80;
            if (s.Contains("fac_high")) v += 50; else if (s.Contains("fac_low")) v -= 50;
            return Math.Max(v, 40);
        }

        private static int Plasma(HashSet<string> s)
        {
            int v = 300;
            bool wear = s.Contains("wear"), heavy = s.Contains("wear_heavy"), crack = s.Contains("crack"),
                 screw = s.Contains("screw"), reinforced = s.Contains("reinforced"),
                 poor = s.Contains("color_poor"), high = s.Contains("color_high"), std = !poor && !high;
            if (wear) v -= 45; else if (heavy) v -= 90;
            if (crack) v -= 150;
            if (screw) v -= 30;
            if (reinforced) v += 60;
            if (poor) v -= 150; else if (high) v += 150;
            bool noWear = !wear && !heavy;
            if (s.Contains("seal_ll") && (std || poor)) v += 60;
            else if (s.Contains("seal_zhonghua") && noWear && !crack && !screw && std) v += 120;
            else if (s.Contains("seal_republic") && !crack && std) v += 120;
            else if (s.Contains("seal_ignition") && !reinforced && noWear && !crack && !screw && high) v += 750;
            return Math.Max(v, 90);
        }

        private static void Define()
        {
            Add("hand", Hand,
                H("calc_h_material", "Material"),
                O("gold", "material", "calc_gold", "Gold plated", 200),
                O("carbon", "material", "calc_carbon", "Carbon composite", 50),
                H("calc_h_interface", "Interface"),
                O("neural", "iface", "calc_neural", "Neural", 120),
                O("emg", "iface", "calc_emg", "EMG+", 45),
                O("unspec", "iface", "calc_unspec", "Unspecified", -60),
                H("calc_h_condition", "Condition"),
                O("wear", "cond", "calc_wear", "Wear", -35),
                O("damage", "cond", "calc_damage", "Damage", -85),
                H("calc_h_addons", "Add-ons"),
                O("armored", null, "calc_armored", "Armored", 50),
                O("bladed", null, "calc_bladed", "Bladed", 80));
            Add("ribwich", Ribwich,
                H("calc_h_state", "State"),
                O("opened", null, "calc_opened", "Opened (= 40)", 0),
                O("fake", null, "calc_fake", "Counterfeit (= 40)", 0),
                H("calc_h_contents", "Contents"),
                O("meat", null, "calc_meat", "Double meat", 800),
                O("deluxe", null, "calc_deluxe", "Deluxe (bun + pickles)", 300),
                H("calc_h_production", "Production"),
                O("edge_month", null, "calc_edge_month", "First / last production month", 50),
                O("first_year", null, "calc_first_year", "First production year (425-429)", 80),
                H("calc_h_facility", "Facility"),
                O("fac_high", "fac", "calc_fac_high", "High-quality facility", 50),
                O("fac_low", "fac", "calc_fac_low", "Low-quality facility", -50));
            Add("plasma_fuel", Plasma,
                H("calc_h_casing", "Casing"),
                O("wear", "wear", "calc_wear", "Wear", -45),
                O("wear_heavy", "wear", "calc_wear_heavy", "Heavy wear", -90),
                O("crack", null, "calc_crack", "Crack", -150),
                O("screw", null, "calc_screw", "Uncovered screw", -30),
                O("reinforced", null, "calc_reinforced", "Reinforced", 60),
                H("calc_h_color", "Color"),
                O("color_poor", "color", "calc_color_poor", "Poor color", -150),
                O("color_high", "color", "calc_color_high", "High-quality color", 150),
                H("calc_h_seal", "Seal (bonus if conditions met)"),
                O("seal_ll", "seal", "calc_seal_ll", "LL", 60),
                O("seal_zhonghua", "seal", "calc_seal_zhonghua", "Zhonghua", 120),
                O("seal_republic", "seal", "calc_seal_republic", "Republic", 120),
                O("seal_ignition", "seal", "calc_seal_ignition", "Ignition Lab", 750));
        }

        // 정의용 작은 헬퍼: Group == "#" 이면 소제목 줄
        private static Opt H(string key, string en) => new Opt { Group = "#", Label = L(key, en) };
        private static Opt O(string id, string group, string key, string en, int delta) =>
            new Opt { Id = id, Group = group, Label = L(key, en), Delta = delta };

        private static string L(string key, string en)
        {
            string s = PenLabelerAssist.Text.Get(key);
            return string.IsNullOrEmpty(s) || s == key ? en : s;
        }

        private static List<(string type, Func<HashSet<string>, int> fn, Opt[] opts)> _defs;

        private static void Add(string type, Func<HashSet<string>, int> fn, params Opt[] opts)
        {
            _defs.Add((type, fn, opts));
        }

        // ── 매 프레임 (UpdateAppraisalPanel postfix) ──
        internal static void Tick(InspectionUIManager ui, string typeId, bool itemChanged, GameObject anchorRow, float refFont)
        {
            if (_failed) return;
            try
            {
                _ui = ui;
                if (_panel == null && !Build(ui, anchorRow, refFont)) return;
                Section sec = typeId != null && Sections.TryGetValue(typeId, out Section found) ? found : null;
                if (sec == null)
                {
                    if (_panel.activeSelf || _active != null) RestoreLayout();
                    _panel.SetActive(false);
                    _active = null;
                    return;
                }
                if (!_panel.activeSelf) _panel.SetActive(true);
                if (sec != _active || itemChanged)
                {
                    foreach (Section other in Sections.Values) other.Root.SetActive(other == sec);
                    _active = sec;
                    _syncing = true;
                    foreach (Opt o in sec.Opts) if (o.Toggle != null) o.Toggle.SetIsOnWithoutNotify(false);
                    _syncing = false;
                    ShowResult(sec, apply: false);
                    FitLayout();
                }
            }
            catch (Exception ex)
            {
                _failed = true;
                Debug.LogWarning("[AppraisalCalculator] disabled: " + ex.Message);
                try { RestoreLayout(); } catch { }
                if (_panel != null) _panel.SetActive(false);
            }
        }

        // ── 감정 창 늘리기 ──
        // 감정 창에는 자동 레이아웃이 없어서 블록이 "감정가 보내기" 버튼을 덮는다.
        // 블록 높이만큼 창을 아래로 늘리고(위쪽 모서리는 고정), 보내기 버튼을 블록 바로 밑으로 옮긴다.
        private static RectTransform _panelRt, _sendRt;
        private static Vector2 _panelSize0, _panelPos0, _sendPos0;
        private static bool _layoutSaved;

        private static void SaveLayout(InspectionUIManager ui)
        {
            _panelRt = (RectTransform)ui.appraisalPanel.transform;
            _sendRt = null;
            foreach (Button b in ui.appraisalPanel.GetComponentsInChildren<Button>(true))
            {
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                {
                    if (b.onClick.GetPersistentMethodName(i) == "OnAppraisalServiceEstimated")
                    {
                        _sendRt = (RectTransform)b.transform;
                        break;
                    }
                }
                if (_sendRt != null) break;
            }
            _panelSize0 = _panelRt.sizeDelta;
            _panelPos0 = _panelRt.anchoredPosition;
            if (_sendRt != null) _sendPos0 = _sendRt.anchoredPosition;
            _layoutSaved = true;
        }

        private static void RestoreLayout()
        {
            if (!_layoutSaved) return;
            if (_panelRt != null)
            {
                _panelRt.sizeDelta = _panelSize0;
                _panelRt.anchoredPosition = _panelPos0;
            }
            if (_sendRt != null) _sendRt.anchoredPosition = _sendPos0;
        }

        private static void FitLayout()
        {
            if (!_layoutSaved || _panelRt == null || _panel == null) return;
            RestoreLayout();
            var rt = (RectTransform)_panel.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            float h = LayoutUtility.GetPreferredHeight(rt);
            if (h <= 0f) h = rt.rect.height;
            float gap = 6f;
            float d = h + gap;

            // 창 높이 +d, 위쪽 모서리 고정 (피벗만큼 아래로 이동)
            _panelRt.sizeDelta = _panelSize0 + new Vector2(0f, d);
            _panelRt.anchoredPosition = _panelPos0 - new Vector2(0f, d * (1f - _panelRt.pivot.y));

            if (_sendRt == null) return;
            // 보내기 버튼 윗변을 블록 아랫변 - gap 으로 (월드 좌표로 맞춘 뒤 로컬로)
            Canvas.ForceUpdateCanvases();
            var blockCorners = new Vector3[4];
            var sendCorners = new Vector3[4];
            rt.GetWorldCorners(blockCorners);
            _sendRt.GetWorldCorners(sendCorners);
            float scaleY = _panelRt.lossyScale.y != 0f ? _panelRt.lossyScale.y : 1f;
            float blockBottom = blockCorners[0].y;
            float sendTop = sendCorners[1].y;
            float shiftWorld = (blockBottom - gap * scaleY) - sendTop;
            if (shiftWorld < 0f) // 겹칠 때만 내린다
            {
                Vector3 p = _sendRt.position;
                p.y += shiftWorld;
                _sendRt.position = p;
            }
        }

        private static void OnToggle(Section sec, Opt opt, bool on)
        {
            if (_syncing) return;
            if (on && opt.Group != null)
            {
                _syncing = true;
                foreach (Opt o in sec.Opts)
                    if (o != opt && o.Group == opt.Group && o.Toggle != null) o.Toggle.SetIsOnWithoutNotify(false);
                _syncing = false;
            }
            ShowResult(sec, apply: true);
        }

        private static void ShowResult(Section sec, bool apply)
        {
            var set = new HashSet<string>();
            foreach (Opt o in sec.Opts) if (o.Toggle != null && o.Toggle.isOn) set.Add(o.Id);
            int value = sec.Compute(set);
            if (_result != null) _result.text = string.Format(L("calc_result", "Estimate: {0}"), value);
            if (!apply || _ui == null || _ui.apprisalServiceInputField == null || _ui.currentInspectingItem == null) return;
            _ui.apprisalServiceInputField.text = value.ToString();
            _ui.currentInspectingItem.ModifyTag("APPRAISAL_DEFAULT_VALUE", delegate (TagState state)
            {
                state.Enable().SetInt(value);
            });
        }

        // ── UI 만들기 ──
        // 감정 창 안, 기본/최저 버튼 줄 바로 밑에 붙인다:
        //   계산값: N
        //   [카테고리]
        //   ㅁ내용 ㅁ내용 ㅁ내용      (가로로 나열, 넘치면 다음 줄)
        // 체크박스는 게임의 추궁 체크박스(toggle1)를 복제하고, 글자 크기는 게임 +/- 버튼 글자에 맞춘다.
        private static bool Build(InspectionUIManager ui, GameObject anchorRow, float refFont)
        {
            if (ui.toggle1 == null || anchorRow == null)
            {
                _failed = true;
                Debug.LogWarning("[AppraisalCalculator] template toggle / anchor row not found; feature off");
                return false;
            }
            _defs = new List<(string, Func<HashSet<string>, int>, Opt[])>();
            Define();

            var rowRt = (RectTransform)anchorRow.transform;
            var parent = (RectTransform)rowRt.parent;
            TMP_Text templateLabel = ui.toggle1.GetComponentInChildren<TMP_Text>(true);
            float font = refFont > 0f ? refFont : (templateLabel != null ? templateLabel.fontSize * 1.4f : 18f);
            float avail = Mathf.Max(rowRt.rect.width, ((RectTransform)ui.appraisalPanel.transform).rect.width - 24f, 200f);
            bool parentLayout = parent.GetComponent<LayoutGroup>() != null;

            _panel = new GameObject(PanelName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            var rt = (RectTransform)_panel.transform;
            rt.SetParent(parent, false);
            rt.SetSiblingIndex(rowRt.GetSiblingIndex() + 1);
            var le = _panel.GetComponent<LayoutElement>();
            if (!parentLayout)
            {
                // 레이아웃 그룹이 없으면 버튼 줄 바로 밑 좌표에 직접 놓는다
                le.ignoreLayout = true;
                rt.anchorMin = rowRt.anchorMin;
                rt.anchorMax = rowRt.anchorMin;
                rt.pivot = new Vector2(0f, 1f);
                Vector2 rowBottomLeft = rowRt.anchoredPosition + new Vector2(-rowRt.pivot.x * rowRt.rect.width, -rowRt.pivot.y * rowRt.rect.height);
                rt.anchoredPosition = rowBottomLeft + new Vector2(0f, -4f);
            }

            var vlg = _panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 4, 4);
            vlg.spacing = 2f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            var fitter = _panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            float lineH = font * 1.35f;
            _result = MakeText(rt, templateLabel, "", avail, lineH, font, bold: true);

            foreach (var def in _defs)
            {
                var sec = new Section { TypeId = def.type, Compute = def.fn };
                GameObject holder = MakeStack(rt, "Sec_" + def.type, vertical: true, spacing: 1f);
                sec.Root = holder;
                var hrt = (RectTransform)holder.transform;
                RectTransform line = null;
                float used = 0f;
                float hgap = Mathf.Round(font * 0.9f);

                foreach (Opt o in def.opts)
                {
                    if (o.Group == "#")
                    {
                        TextMeshProUGUI h = MakeText(hrt, templateLabel, "[" + o.Label + "]", avail, lineH, font * 0.9f, bold: false);
                        h.alpha = 0.75f;
                        line = null;
                        continue;
                    }
                    Toggle t = MakeToggle(ui, o, font, out float w);
                    if (line == null || used + w > avail)
                    {
                        line = (RectTransform)MakeStack(hrt, "Line", vertical: false, spacing: hgap).transform;
                        used = 0f;
                    }
                    t.transform.SetParent(line, false);
                    o.Toggle = t;
                    used += w + hgap;
                    Section s = sec;
                    Opt opt = o;
                    t.onValueChanged.AddListener(on => OnToggle(s, opt, on));
                    sec.Opts.Add(o);
                }
                holder.SetActive(false);
                Sections[def.type] = sec;
            }

            // 감정 창 형제 요소가 클릭을 가로채지 않게 자체 Canvas 로 맨 위에서 클릭을 받는다 (2.9.1 수정 유지).
            Canvas parentCanvas = ui.appraisalPanel.GetComponentInParent<Canvas>();
            Canvas own = _panel.AddComponent<Canvas>();
            own.overrideSorting = true;
            if (parentCanvas != null)
            {
                Canvas root = parentCanvas.rootCanvas;
                own.sortingLayerID = root.sortingLayerID;
                own.sortingOrder = Mathf.Max(parentCanvas.sortingOrder, root.sortingOrder) + 50;
            }
            else
            {
                own.sortingOrder = 500;
            }
            _panel.AddComponent<GraphicRaycaster>();

            SaveLayout(ui);
            _panel.SetActive(false);
            return true;
        }

        private static Toggle MakeToggle(InspectionUIManager ui, Opt o, float font, out float width)
        {
            GameObject go = UnityEngine.Object.Instantiate(ui.toggle1.gameObject);
            go.name = "Opt_" + o.Id;
            go.SetActive(true);
            Toggle t = go.GetComponent<Toggle>();
            t.group = null;
            t.interactable = true;
            t.onValueChanged = new Toggle.ToggleEvent(); // 프리팹의 추궁 이슈 리스너 제거
            t.SetIsOnWithoutNotify(false);
            foreach (Graphic g in go.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = true;

            float box = Mathf.Round(font * 0.9f);
            float gap = Mathf.Round(font * 0.3f);
            if (go.transform.childCount > 0)
            {
                // child(0) = 체크박스 (InspectionUIManager 도 GetChild(1) 을 라벨로 씀)
                var cb = (RectTransform)go.transform.GetChild(0);
                cb.anchorMin = new Vector2(0f, 0.5f);
                cb.anchorMax = new Vector2(0f, 0.5f);
                cb.pivot = new Vector2(0f, 0.5f);
                cb.anchoredPosition = Vector2.zero;
                cb.sizeDelta = new Vector2(box, box);
            }
            string sign = o.Delta > 0 ? " +" + o.Delta : o.Delta < 0 ? " " + o.Delta : "";
            string text = o.Label + sign;
            float labelW = 0f;
            TMP_Text label = go.transform.childCount > 1 ? go.transform.GetChild(1).GetComponent<TMP_Text>() : go.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.enableAutoSizing = false;
                label.fontSize = font;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Overflow;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.text = text;
                RectTransform lrt = label.rectTransform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 1f);
                lrt.pivot = new Vector2(0f, 0.5f);
                lrt.offsetMin = new Vector2(box + gap, 0f);
                lrt.offsetMax = Vector2.zero;
                labelW = label.GetPreferredValues(text).x;
            }
            width = box + gap + labelW + 2f;
            float height = Mathf.Max(box, font * 1.3f);
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, height);
            return t;
        }

        private static GameObject MakeStack(RectTransform parent, string name, bool vertical, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ContentSizeFitter));
            HorizontalOrVerticalLayoutGroup g;
            if (vertical) g = go.AddComponent<VerticalLayoutGroup>();
            else g = go.AddComponent<HorizontalLayoutGroup>();
            ((RectTransform)go.transform).SetParent(parent, false);
            g.spacing = spacing;
            g.childAlignment = vertical ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            g.childControlWidth = false;
            g.childControlHeight = false;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;
            var f = go.GetComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        private static TextMeshProUGUI MakeText(RectTransform parent, TMP_Text template, string text, float width, float height, float size, bool bold)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(width, height);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (template != null)
            {
                tmp.font = template.font;
                tmp.color = template.color;
            }
            tmp.fontSize = size;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.text = text;
            return tmp;
        }
    }
}
