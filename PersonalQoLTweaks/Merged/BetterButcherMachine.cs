using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BetterButcherMachine
{
    // 자동 도축기 "우리" 탭에 "복합" 수확 모드를 추가하는 모드.
    //
    // 복합 모드 규칙 (우리 동물 대상, 이름/표시로 보호된 개체는 항상 제외):
    //   0. 사체, 병든/프리온 개체 도태 (기존 토글 그대로)
    //   1. 나이 범위에 든 개체는 도축 (번식용 유지 무시)
    //   2. 무게 범위에 든 개체는 도축 (번식용 유지 개체는 제외)
    //   3. 과밀 정리가 켜져 있으면 칸 사용률이 설정값 이하가 될 때까지 가벼운 개체부터 도축 (번식용 제외)
    //   "임신 개체 제외"가 켜져 있으면 임신한 개체는 1~3 모두에서 제외.
    // Merged into the Personal QoL Tweaks DLL (single IMod per DLL); patches are applied by PatchAll.
    internal static class BetterButcherMachineModule
    {
        public static void OnEnable() { Debug.Log("[BetterButcherMachine] enabled"); }
    }

    internal static class Settings
    {
        public const string COMPOSITE_BOOL = "BBM_CAGE_COMPOSITE_BOOL";
        public const string OVERCROWD_BOOL = "BBM_CAGE_OVERCROWD_BOOL";
        public const string OVERCROWD_PCT_INT = "BBM_CAGE_OVERCROWD_PCT_INT";

        public const int COMPOSITE_INDEX = 4;   // 수확 목록의 5번째 항목
        public const int FALLBACK_HARVEST = 3;  // 복합일 때 원래 수확 값은 "나이"로 저장 (모드 제거 시 대체 동작)
        public const int DEFAULT_PCT = 55;      // 게임의 과밀 1단계 기준 (55% 초과)
        public const string CAGE_CAPTION_KEY = "mech_auto_butcher_tab_cage";

        // 우리 탭 설정 태그 (게임 원본 값)
        public const string WEIGHT_MIN = "AUTO_BUTCHER_CAGE_WEIGHT_MIN_INT";
        public const string WEIGHT_MAX = "AUTO_BUTCHER_CAGE_WEIGHT_MAX_INT";
        public const string AGE_MIN = "AUTO_BUTCHER_CAGE_AGE_MIN_INT";
        public const string AGE_MAX = "AUTO_BUTCHER_CAGE_AGE_MAX_INT";
        public const string KEEP_FEMALES = "AUTO_BUTCHER_CAGE_KEEP_FEMALES_INT";
        public const string KEEP_MALES = "AUTO_BUTCHER_CAGE_KEEP_MALES_INT";
        public const string SPARE_PREGNANT = "AUTO_BUTCHER_CAGE_SPARE_PREGNANT_BOOL";
        public const string COUNT_PROTECTED = "AUTO_BUTCHER_CAGE_COUNT_PROTECTED_BOOL";
        public const string CULL_SICK = "AUTO_BUTCHER_CAGE_CULL_SICK_BOOL";
        public const string CULL_PRION = "AUTO_BUTCHER_CAGE_CULL_PRION_BOOL";
        public const int WEIGHT_SLIDER_MAX = 1000;
        public const int AGE_SLIDER_MAX = 30;

        public static bool IsComposite(GameItem m)
        {
            return m != null && m.GetTagReadonly(COMPOSITE_BOOL).GetBool();
        }

        public static void SetComposite(GameItem m, bool on)
        {
            m?.ModifyTag(COMPOSITE_BOOL, s => s.Enable().SetBool(on));
        }

        public static int OvercrowdPct(GameItem m)
        {
            return m != null && m.IsTag(OVERCROWD_PCT_INT) ? m.GetTagReadonly(OVERCROWD_PCT_INT).GetInt() : DEFAULT_PCT;
        }

        // 아직 설정한 적 없으면 기본 켜짐
        public static bool OvercrowdOn(GameItem m)
        {
            return m == null || !m.IsTag(OVERCROWD_PCT_INT) || m.GetTagReadonly(OVERCROWD_BOOL).GetBool();
        }

        public static void SetOvercrowd(GameItem m, bool on, int pct)
        {
            if (m == null) return;
            m.ModifyTag(OVERCROWD_BOOL, s => s.Enable().SetBool(on));
            m.ModifyTag(OVERCROWD_PCT_INT, s => s.Enable().SetInt(pct));
        }

        public static string OvercrowdCaption(int pct)
        {
            return "칸 사용률 " + pct + "% 이하 유지";
        }
    }

    // ════════════ UI ════════════

    internal class CageExtras
    {
        public Func<GameItem> machine;
        public MachineryRadioList harvest;
        public MachineryRangeSlider weight;
        public MachineryText weightValue;
        public MachineryRangeSlider age;
        public MachineryText ageValue;
        public MachineryRadioButton overcrowdToggle;
        public MachineryText overcrowdValue;
        public MachinerySlider overcrowdSlider;

        public bool CompositeSelected => harvest.selected == Settings.COMPOSITE_INDEX;

        public void ApplyEnabled()
        {
            bool comp = CompositeSelected;
            if (comp)
            {
                SetRow(weight, weightValue, true);
                SetRow(age, ageValue, true);
            }
            overcrowdToggle.SetDisabled(!comp);
            bool sliderOn = comp && overcrowdToggle.isOn;
            overcrowdSlider.SetDisabled(!sliderOn);
            overcrowdValue.SetColor(sliderOn ? MachineryStyle.BODY_TEXT : MachineryStyle.BUTTON_TEXT_DISABLED);
        }

        private static void SetRow(MachineryRangeSlider slider, MachineryText text, bool enabled)
        {
            slider.SetDisabled(!enabled);
            text.SetColor(enabled ? MachineryStyle.BODY_TEXT : MachineryStyle.BUTTON_TEXT_DISABLED);
        }

        public void Sync(GameItem m)
        {
            if (m == null) return;
            if (Settings.IsComposite(m) && harvest.selected != Settings.COMPOSITE_INDEX)
                harvest.Select(Settings.COMPOSITE_INDEX);
            overcrowdToggle.SetOn(Settings.OvercrowdOn(m));
            int pct = Settings.OvercrowdPct(m);
            overcrowdSlider.SetInt(pct);
            overcrowdValue.SetText(Settings.OvercrowdCaption(pct));
            ApplyEnabled();
        }
    }

    internal static class Ui
    {
        // PageControls 객체 → 우리 탭 추가 UI
        public static readonly Dictionary<object, CageExtras> Pages = new Dictionary<object, CageExtras>();
    }

    [HarmonyPatch(typeof(MachineAutoButcher), "BuildPage")]
    internal static class Patch_BuildPage
    {
        private static void Postfix(object __result)
        {
            Traverse page = Traverse.Create(__result);
            if (page.Field("spec").Field("captionKey").GetValue() as string != Settings.CAGE_CAPTION_KEY) return;

            CageExtras ex = new CageExtras
            {
                machine = page.Field("machine").GetValue() as Func<GameItem>,
                harvest = page.Field("harvest").GetValue() as MachineryRadioList,
                weight = page.Field("weight").GetValue() as MachineryRangeSlider,
                weightValue = page.Field("weightValue").GetValue() as MachineryText,
                age = page.Field("age").GetValue() as MachineryRangeSlider,
                ageValue = page.Field("ageValue").GetValue() as MachineryText,
            };
            MachineryStack root = page.Field("root").GetValue() as MachineryStack;
            if (ex.harvest == null || ex.weight == null || ex.age == null || root == null)
            {
                Debug.LogWarning("[BetterButcherMachine] auto butcher page layout changed, UI not added");
                return;
            }

            // 1) 수확 목록에 "복합" 추가
            MachineryRadioButton compositeButton = new MachineryRadioButton("복합 (나이 또는 무게)", false);
            ex.harvest.group.Add(compositeButton);
            ex.harvest.Add(compositeButton);

            Action<int> original = ex.harvest.group.onChanged;
            ex.harvest.group.onChanged = index =>
            {
                GameItem m = ex.machine?.Invoke();
                if (index == Settings.COMPOSITE_INDEX)
                {
                    Settings.SetComposite(m, true);
                    if (m != null && !m.IsTag(Settings.OVERCROWD_PCT_INT))
                        Settings.SetOvercrowd(m, true, Settings.DEFAULT_PCT);
                    original?.Invoke(Settings.FALLBACK_HARVEST); // 원래 수확 값은 "나이"로 저장
                }
                else
                {
                    Settings.SetComposite(m, false);
                    original?.Invoke(index);
                }
                ex.ApplyEnabled();
            };

            // 2) 과밀 정리 행
            ex.overcrowdValue = new MachineryText(Settings.OvercrowdCaption(Settings.DEFAULT_PCT), 6, HorizontalAlignmentOptions.Left, uppercase: false);
            ex.overcrowdSlider = new MachinerySlider(Settings.DEFAULT_PCT).SetRange(30f, 90f, 5f);
            ex.overcrowdToggle = new MachineryRadioButton("과밀 정리 (가벼운 개체부터)", true, on =>
            {
                GameItem m = ex.machine?.Invoke();
                Settings.SetOvercrowd(m, on, Settings.OvercrowdPct(m));
                ex.ApplyEnabled();
            });
            ex.overcrowdSlider.onChanged = value =>
            {
                int pct = Mathf.RoundToInt(value);
                GameItem m = ex.machine?.Invoke();
                Settings.SetOvercrowd(m, Settings.OvercrowdOn(m), pct);
                ex.overcrowdValue.SetText(Settings.OvercrowdCaption(pct));
            };

            // 나이 슬라이더 바로 아래에 끼워 넣기
            List<PixelElement> after = root.elements.SkipWhile(e => e != ex.age).Skip(1).ToList();
            foreach (PixelElement e in after) root.Detach(e);
            root.Add(ex.overcrowdToggle);
            root.Add(ex.overcrowdValue);
            root.Add(ex.overcrowdSlider);
            foreach (PixelElement e in after) root.Add(e);

            Ui.Pages[__result] = ex;
            ex.ApplyEnabled();
        }
    }

    [HarmonyPatch(typeof(MachineAutoButcher), "RefreshEnabled")]
    internal static class Patch_RefreshEnabled
    {
        private static void Postfix(object page)
        {
            if (page != null && Ui.Pages.TryGetValue(page, out CageExtras ex)) ex.ApplyEnabled();
        }
    }

    [HarmonyPatch(typeof(MachineAutoButcher), "SyncPage")]
    internal static class Patch_SyncPage
    {
        private static void Postfix(GameItem machine, object page)
        {
            if (page != null && Ui.Pages.TryGetValue(page, out CageExtras ex)) ex.Sync(machine);
        }
    }

    // 설정 복사/붙여넣기에 모드 값 포함
    [HarmonyPatch(typeof(MachineAutoButcher), nameof(MachineAutoButcher.CopySettings))]
    internal static class Patch_CopySettings
    {
        private static void Postfix(GameItem machine, Dictionary<string, int> ___clipboardInts, Dictionary<string, bool> ___clipboardBools)
        {
            if (machine == null || ___clipboardInts == null || ___clipboardBools == null) return;
            ___clipboardBools[Settings.COMPOSITE_BOOL] = Settings.IsComposite(machine);
            ___clipboardBools[Settings.OVERCROWD_BOOL] = Settings.OvercrowdOn(machine);
            ___clipboardInts[Settings.OVERCROWD_PCT_INT] = Settings.OvercrowdPct(machine);
        }
    }

    // ════════════ 도축 로직 ════════════

    [HarmonyPatch]
    internal static class ButcherCalls
    {
        // 원래 반환형은 private enum Outcome (0 = Butchered, 1 = TrayFull, 2 = Skipped)
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(MachineAutoButcher), "ProcessAnimal")]
        public static int ProcessAnimal(GameItem machine, GameItem animal, GameGridInventory tray)
        {
            throw new NotImplementedException("reverse patch stub");
        }
    }

    [HarmonyPatch(typeof(MachineAutoButcher), "RunCage")]
    internal static class Patch_RunCage
    {
        private static bool Prefix(GameItem machine, IEnumerable<GameItem> items, GameGridInventory tray, ref int count)
        {
            if (!Settings.IsComposite(machine)) return true; // 기존 모드는 원래 로직
            try
            {
                count += CompositeRules.Run(machine, items, tray);
            }
            catch (Exception e)
            {
                Debug.LogError("[BetterButcherMachine] composite run failed: " + e);
            }
            return false;
        }
    }

    internal static class CompositeRules
    {
        private static bool InRange(float value, int low, int high, int sliderMax)
        {
            return value >= low && (high >= sliderMax || value <= high);
        }

        private static GameGridInventory CageGrid(GameItem machine)
        {
            GameItem host = MachineAutoButcher.GetHost(machine);
            if (host != null && host.IsTag("CAGE_TAG")) return AnimalCage.GetCageInventory(host);
            return machine.parentInventory as GameGridInventory;
        }

        public static int Run(GameItem machine, IEnumerable<GameItem> items, GameGridInventory tray)
        {
            int wLow = machine.GetTagReadonly(Settings.WEIGHT_MIN).GetInt();
            int wHigh = machine.GetTagReadonly(Settings.WEIGHT_MAX).GetInt();
            int aLow = machine.GetTagReadonly(Settings.AGE_MIN).GetInt();
            int aHigh = machine.GetTagReadonly(Settings.AGE_MAX).GetInt();
            int keepFemales = machine.GetTagReadonly(Settings.KEEP_FEMALES).GetInt();
            int keepMales = machine.GetTagReadonly(Settings.KEEP_MALES).GetInt();
            bool sparePregnant = machine.GetTagReadonly(Settings.SPARE_PREGNANT).GetBool();
            bool countProtected = machine.GetTagReadonly(Settings.COUNT_PROTECTED).GetBool();
            bool cullSick = machine.GetTagReadonly(Settings.CULL_SICK).GetBool();
            bool cullPrion = machine.GetTagReadonly(Settings.CULL_PRION).GetBool();

            List<GameItem> animals = items.Where(a => a.IsTag("ANIMAL_ITEM_TAG")).ToList();
            List<GameItem> order = new List<GameItem>();
            HashSet<GameItem> culled = new HashSet<GameItem>();
            void Cull(GameItem a) { if (culled.Add(a)) order.Add(a); }

            bool Spared(GameItem a) => MachineAutoButcher.IsProtected(a) || (sparePregnant && a.IsTag("ANIMAL_PREGNANT_TAG"));
            int Age(GameItem a) => a.GetTagReadonly("ANIMAL_AGE_TAG").GetInt();
            float Weight(GameItem a) => a.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat();
            string Sex(GameItem a) => a.GetTagReadonly("ANIMAL_SEXE_TAG").GetString();

            // 0. 사체 / 병든 / 프리온
            foreach (GameItem a in animals)
            {
                if (MachineAutoButcher.IsProtected(a)) continue;
                if (HusbandryHelper.IsAnimalDead(a)
                    || (cullSick && a.IsTag("ANIMAL_DISEASED_TAG"))
                    || (cullPrion && a.IsTag("ANIMAL_PRION_TAG")))
                    Cull(a);
            }

            List<GameItem> live = animals.Where(a => !HusbandryHelper.IsAnimalDead(a)).ToList();

            // 1. 나이 (번식용 유지 무시)
            foreach (GameItem a in live)
            {
                if (!culled.Contains(a) && !Spared(a) && InRange(Age(a), aLow, aHigh, Settings.AGE_SLIDER_MAX))
                    Cull(a);
            }

            // 번식용 선정: 종별로, 남은 개체 중 번식 가능한 성체를 어린 순으로 (부족하면 곧 성체가 될 개체를 나이 많은 순으로)
            HashSet<GameItem> keepers = new HashSet<GameItem>();
            foreach (IGrouping<string, GameItem> species in live.Where(a => !culled.Contains(a))
                         .GroupBy(a => a.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString()))
            {
                PickKeepers(species.ToList(), "FEMALE", keepFemales, countProtected, keepers, Age, Sex);
                PickKeepers(species.ToList(), "MALE", keepMales, countProtected, keepers, Age, Sex);
            }

            // 2. 무게 (번식용 제외)
            foreach (GameItem a in live)
            {
                if (!culled.Contains(a) && !keepers.Contains(a) && !Spared(a)
                    && InRange(Weight(a), wLow, wHigh, Settings.WEIGHT_SLIDER_MAX))
                    Cull(a);
            }

            // 3. 과밀 정리: 가벼운 개체부터 (번식용 제외)
            GameGridInventory grid = CageGrid(machine);
            if (Settings.OvercrowdOn(machine) && grid?.inventoryShape != null)
            {
                int totalCells = grid.inventoryShape.width * grid.inventoryShape.height;
                float limit = totalCells * Settings.OvercrowdPct(machine) / 100f;
                int occupied = animals.Where(a => !culled.Contains(a)).Sum(a => a.GetItemSize());
                foreach (GameItem a in live.Where(a => !culled.Contains(a) && !keepers.Contains(a) && !Spared(a))
                                           .OrderBy(Weight).ToList())
                {
                    if (occupied <= limit) break;
                    Cull(a);
                    occupied -= a.GetItemSize();
                }
            }

            int done = 0;
            foreach (GameItem a in order)
            {
                if (ButcherCalls.ProcessAnimal(machine, a, tray) == 0) done++;
            }
            return done;
        }

        private static void PickKeepers(List<GameItem> group, string sex, int quota, bool countProtected,
                                        HashSet<GameItem> keepers, Func<GameItem, int> age, Func<GameItem, string> sexOf)
        {
            List<GameItem> sameSex = group.Where(a => sexOf(a) == sex).ToList();
            int kept = countProtected ? sameSex.Count(MachineAutoButcher.IsProtected) : 0;
            IEnumerable<GameItem> breedable = sameSex.Where(a => !MachineAutoButcher.IsProtected(a) && !a.IsTag("ANIMAL_NEUTERED_TAG"));
            IEnumerable<GameItem> ordered = breedable.Where(HusbandryHelper.IsMature).OrderBy(age)
                .Concat(breedable.Where(a => !HusbandryHelper.IsMature(a)).OrderByDescending(age));
            foreach (GameItem a in ordered)
            {
                if (kept >= quota) break;
                keepers.Add(a);
                kept++;
            }
        }
    }
}
