using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AppraisalPresets
{
    // 감정 계산기: 감정 입력 창 오른쪽에 붙는 별도 패널.
    // 플레이어가 직접 보고 판단한 상태를 체크하면 게임의 GetCorrectValue 와 같은 식으로 값을 계산해서
    // 입력 칸에 바로 넣는다 (숨겨진 정답을 읽지 않음).
    //   의수:  AppHandHelper.GetCorrectValue   (기본 100, 최저 50, 세트 보너스 포함)
    //   립위치: RibwichHelper.GetCorrectValue   (기본 150, 최저 40, 개봉/가짜면 40)
    //   플라즈마: PlasmaHelper.GetCorrectValue   (기본 300, 최저 90, 씰 보너스 포함)
    // 패널은 appraisalPanel 의 자식이라 감정 창이 닫히면 같이 사라진다.
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
        internal static void Tick(InspectionUIManager ui, string typeId, bool itemChanged)
        {
            if (_failed) return;
            try
            {
                _ui = ui;
                if (_panel == null && !Build(ui)) return;
                Section sec = typeId != null && Sections.TryGetValue(typeId, out Section found) ? found : null;
                if (sec == null)
                {
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
                }
            }
            catch (Exception ex)
            {
                _failed = true;
                Debug.LogWarning("[AppraisalCalculator] disabled: " + ex.Message);
                if (_panel != null) _panel.SetActive(false);
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

        // ── UI 만들기: 게임의 추궁 체크박스(toggle1)를 복제해서 모양을 맞춘다 ──
        private static bool Build(InspectionUIManager ui)
        {
            if (ui.toggle1 == null)
            {
                _failed = true;
                Debug.LogWarning("[AppraisalCalculator] template toggle not found; feature off");
                return false;
            }
            _defs = new List<(string, Func<HashSet<string>, int>, Opt[])>();
            Define();

            var panelRt = (RectTransform)ui.appraisalPanel.transform;
            var templateRt = (RectTransform)ui.toggle1.transform;
            TMP_Text templateLabel = ui.toggle1.GetComponentInChildren<TMP_Text>(true);
            float width = Mathf.Max(templateRt.rect.width, 220f);
            float rowH = Mathf.Max(templateRt.rect.height, 16f);

            _panel = new GameObject(PanelName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            _panel.GetComponent<LayoutElement>().ignoreLayout = true;
            var rt = (RectTransform)_panel.transform;
            rt.SetParent(panelRt, false);
            // 감정 창 오른쪽 위 모서리 바깥에 붙인다
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(8f, 0f);

            Image bg = _panel.GetComponent<Image>();
            Image panelImg = ui.appraisalPanel.GetComponent<Image>();
            if (panelImg != null && panelImg.sprite != null)
            {
                bg.sprite = panelImg.sprite;
                bg.type = panelImg.type;
                bg.color = panelImg.color;
            }
            else
            {
                bg.color = new Color(0.11f, 0.09f, 0.16f, 0.96f);
            }
            bg.raycastTarget = true; // 뒤의 게임 화면이 클릭되지 않게

            var vlg = _panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 10);
            vlg.spacing = 2f;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            var fitter = _panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            MakeText(rt, templateLabel, L("calc_title", "Appraisal calculator"), width, rowH, bold: true);
            _result = MakeText(rt, templateLabel, "", width, rowH, bold: true);

            foreach (var def in _defs)
            {
                var sec = new Section { TypeId = def.type, Compute = def.fn };
                var holder = new GameObject("Sec_" + def.type, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                var hrt = (RectTransform)holder.transform;
                hrt.SetParent(rt, false);
                var hv = holder.GetComponent<VerticalLayoutGroup>();
                hv.spacing = 2f;
                hv.childControlWidth = false;
                hv.childControlHeight = false;
                hv.childForceExpandWidth = false;
                hv.childForceExpandHeight = false;
                var hf = holder.GetComponent<ContentSizeFitter>();
                hf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                hf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                sec.Root = holder;

                foreach (Opt o in def.opts)
                {
                    if (o.Group == "#")
                    {
                        TextMeshProUGUI h = MakeText(hrt, templateLabel, o.Label, width, rowH, bold: false);
                        h.alpha = 0.7f;
                        continue;
                    }
                    GameObject go = UnityEngine.Object.Instantiate(ui.toggle1.gameObject, hrt);
                    go.name = "Opt_" + o.Id;
                    go.SetActive(true);
                    var trt = (RectTransform)go.transform;
                    trt.sizeDelta = new Vector2(width, rowH);
                    Toggle t = go.GetComponent<Toggle>();
                    t.group = null;
                    t.interactable = true;
                    foreach (Graphic g in go.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = true;
                    t.onValueChanged = new Toggle.ToggleEvent(); // 프리팹의 추궁 이슈 리스너 제거
                    t.SetIsOnWithoutNotify(false);
                    TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
                    if (label != null)
                    {
                        string sign = o.Delta > 0 ? " (+" + o.Delta + ")" : o.Delta < 0 ? " (" + o.Delta + ")" : "";
                        label.text = o.Label + sign;
                        label.enableWordWrapping = false;
                    }
                    o.Toggle = t;
                    Section s = sec;
                    Opt opt = o;
                    t.onValueChanged.AddListener(on => OnToggle(s, opt, on));
                    sec.Opts.Add(o);
                }
                holder.SetActive(false);
                Sections[def.type] = sec;
            }
            // 감정 창 뒤쪽 형제(투명한 클릭 영역 등)가 패널을 덮어서 클릭이 안 먹던 문제:
            // 자체 Canvas(정렬 덮어쓰기) + GraphicRaycaster 로 맨 위 레이어에서 클릭을 받는다.
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

            _panel.SetActive(false);
            return true;
        }

        private static TextMeshProUGUI MakeText(RectTransform parent, TMP_Text template, string text, float width, float height, bool bold)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(width, height);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (template != null)
            {
                tmp.font = template.font;
                tmp.fontSize = template.fontSize;
                tmp.color = template.color;
            }
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.text = text;
            return tmp;
        }
    }
}
