using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace MachineProgressMove
{
    // 진행형 기계(프린터, DNA 조작기, 수경재배기 등) 툴팁에서 진행 막대를 "진행 속도" 줄 바로 밑으로 옮기고,
    // 현재 진행 속도 기준으로 완료까지 며칠 남았는지 적는다.
    // 게임 규칙 (MachineProgressHelper.ContinueProgressTypeMachine): 밤마다 진행도 += 현재 진행 속도.
    // 원래 막대(각 기계 CreateMachineTooltip 안의 막대 + "현재/목표" 줄)는 이 툴팁에서 한 번만 건너뛴다.
    internal static class State
    {
        [ThreadStatic] internal static GameItem Moved;   // 이번 툴팁에서 막대를 옮긴 기계
        [ThreadStatic] internal static bool Suppress;    // 원래 막대를 그리는 함수 안인지
        [ThreadStatic] internal static bool SkipLine;    // 막대 뒤 "현재/목표" 줄을 건너뛸지

        internal static string L(string key, string en, params object[] args)
        {
            string s = PenLabelerAssist.Text.Get(key, args);
            if (!string.IsNullOrEmpty(s) && s != key && !s.Contains("{0}")) return s;
            try { return string.Format(string.IsNullOrEmpty(s) || s == key ? en : s, args); }
            catch { return en; }
        }
    }

    [HarmonyPatch(typeof(MachineryHelper), nameof(MachineryHelper.CreateMachineryTooltip))]
    internal static class InsertAfterSpeedPatch
    {
        private static void Postfix(RichTextBuilder builder, GameItem item)
        {
            State.Moved = null;
            try
            {
                if (builder == null || item == null || !item.IsTag("PROGRESS_TYPE_MACHINE_TAG") || item.IsTag("BROKEN_MACHINE_TAG")) return;
                if (!MachineProgressHelper.GetMachineState(item).Equals("STATE_WORKING")) return;

                var (cur, target) = MachineProgressHelper.GetProgress(item);
                if (target <= 0) return;
                int speed = item.GetTagReadonly("CURRENT_PROCESSING_SPEED_TAG").GetInt();

                // 넣을 줄을 임시 빌더에 만든다: [막대] 현재/목표 (n%) ⏎ 완료까지 N일 ⏎
                var tmp = new RichTextBuilder(builder.maxCharWidth);
                bool dna = item.IsTag("DNA_MANIPULATOR_TAG");
                if (dna)
                {
                    // DNA 조작기: "형질 추출 중 / 형질 이식 중" 상태를 막대 위로
                    string op = item.GetTagReadonly("DNA_OPERATION_STRING").GetString();
                    PlantTrait trait = PlantTraitRegistry.Get(item.GetTagReadonly("DNA_OPERATION_TRAIT_STRING").GetString());
                    string status = op == "apply" && trait != null ? PlantGeneticsText.StatusApplying(trait.displayName) : PlantGeneticsText.StatusExtracting();
                    tmp.AddLine(status, true, RenderHandler.ColorPalette.LightGreen);
                }
                float ratio = Mathf.Clamp01((float)cur / target);
                tmp.AddProgressBar(ratio, 10, RenderHandler.ColorPalette.LightGreen, RenderHandler.ColorPalette.VeryDarkGreen, null, " ");
                tmp.AddLine(cur + "/" + target + " (" + Mathf.FloorToInt(ratio * 100f) + "%)", true, RenderHandler.ColorPalette.LightGreen);
                int remaining = Math.Max(0, target - cur);
                if (speed <= 0)
                    tmp.AddLine(State.L("progress_eta_stopped", "Progress stopped (speed 0)"), true, RenderHandler.ColorPalette.LightOrange);
                else
                {
                    int nights = Math.Max(1, (remaining + speed - 1) / speed);
                    tmp.AddLine(nights == 1
                            ? State.L("progress_eta_tonight", "Finishes tonight")
                            : State.L("progress_eta_days", "Finishes in {0} days", nights),
                        true, RenderHandler.ColorPalette.LightCyan);
                }
                if (dna)
                    tmp.AddLine(PlantGeneticsText.SuccessChance(item.GetTagReadonly("DNA_OPERATION_SUCCESS_INT").GetInt()), true, RenderHandler.ColorPalette.LightCyan);
                tmp.NextLine();

                var main = Traverse.Create(builder).Field("slices").GetValue() as IList;
                var add = Traverse.Create(tmp).Field("slices").GetValue() as IList;
                if (main == null || add == null) return;

                string label = LocHelper.GetLocalizedMechanic("mech_machinery_progress_speed");
                int labelIdx = -1;
                for (int i = main.Count - 1; i >= 0; i--)
                {
                    if (Traverse.Create(main[i]).Field("text").GetValue() as string == label) { labelIdx = i; break; }
                }
                if (labelIdx < 0) return;

                int insertAt = -1;
                for (int i = labelIdx + 1; i < main.Count; i++)
                {
                    if (Traverse.Create(main[i]).Field("isNewLine").GetValue() is bool nl && nl) { insertAt = i + 1; break; }
                }
                if (insertAt < 0)
                {
                    builder.NextLine(); // 속도 줄이 마지막이면 미뤄 둔 줄바꿈을 먼저 넣는다
                    insertAt = main.Count;
                }
                for (int i = 0; i < add.Count; i++) main.Insert(insertAt + i, add[i]);
                State.Moved = item;
            }
            catch (Exception ex)
            {
                State.Moved = null;
                Debug.LogWarning("[MachineProgressMove] " + ex.Message);
            }
        }
    }

    // 원래 막대를 그리는 기계별 툴팁 함수들: 막대를 옮긴 기계면 그 안의 막대 + 다음 줄을 건너뛴다.
    [HarmonyPatch]
    internal static class SuppressOriginalPatch
    {
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MachineProgressHelper), nameof(MachineProgressHelper.CreateMachineTooltip));
            yield return AccessTools.Method(typeof(MachineHydroponic), nameof(MachineHydroponic.CreateMachineTooltip));
            yield return AccessTools.Method(typeof(MachineDnaManipulator), nameof(MachineDnaManipulator.CreateMachineTooltip));
        }

        private static void Prefix(GameItem item)
        {
            State.Suppress = item != null && ReferenceEquals(item, State.Moved);
            State.SkipLine = false;
        }

        private static void Finalizer()
        {
            State.Suppress = false;
            State.SkipLine = false;
        }
    }

    // DNA 조작기 작업 중: 상태/막대/성공 확률은 위로 옮겼으므로 원래 함수에서는 디스크 자동 배출 줄만 그린다.
    // (원래 함수는 작업 중이면 이 부분 뒤에 상태+막대+성공 확률을 그리고 바로 return 한다)
    [HarmonyPatch(typeof(MachineDnaManipulator), nameof(MachineDnaManipulator.CreateMachineTooltip))]
    [HarmonyPriority(Priority.First)]
    internal static class DnaWorkingPatch
    {
        private static bool Prefix(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (builder == null || item == null || !ReferenceEquals(item, State.Moved) || !item.IsTag("DNA_MANIPULATOR_TAG")) return true;
                if (!MachineProgressHelper.GetMachineState(item).Equals("STATE_WORKING")) return true;
                builder.AddLine(" ");
                bool eject = MachineDnaManipulator.IsEjectEnabled(item);
                builder.AddLine(eject ? PlantGeneticsText.EjectOn() : PlantGeneticsText.EjectOff(), true,
                    eject ? RenderHandler.ColorPalette.LightGreen : RenderHandler.ColorPalette.LightOrange);
                builder.AddLine(PlantGeneticsText.EjectHint(), true, RenderHandler.ColorPalette.Gray);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MachineProgressMove] " + ex.Message);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(RichTextBuilder), nameof(RichTextBuilder.AddLine))]
    internal static class SkipCountLinePatch
    {
        private static bool Prefix(RichTextBuilder __instance, ref RichTextBuilder __result)
        {
            if (!State.Suppress || !State.SkipLine) return true;
            State.SkipLine = false;
            __result = __instance;
            return false;
        }
    }
}
