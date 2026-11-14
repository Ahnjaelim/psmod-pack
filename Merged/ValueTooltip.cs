using System;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ValueTooltip
{
    // 아이템 기본 툴팁의 가치 줄을 정리한다.
    //   원본: "추정 가치: 15, 기본 가치: 25" / "구매가: 11"
    //   변경:  현재가: 15 (+4 이득)
    //          칸당 가치: 7.5
    //          구매가: 11 (기본가: 25)   <- 흐리게
    //
    // GameItemElement.GetTooltipBasic 실행 중에 LocHelper.GetLocalizedMechanic 결과를 바꿔치기한다.
    //   - "mech_tooltip_est_value" -> 위 블록 전체
    //   - "mech_tooltip_bought_at" -> 빈 문자열 (AddLine 이 빈 줄은 건너뜀; 구매가는 블록에 포함됨)
    // 흥정 중(단가/총가가 따로 나오는 경우)에는 est_value 를 안 부르므로 원본 그대로 둔다.
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.

    internal static class Ctx
    {
        [ThreadStatic] public static GameItemElement Item;
        [ThreadStatic] public static bool BoughtMerged;
    }

    [HarmonyPatch(typeof(GameItemElement), "GetTooltipBasic")]
    internal static class GetTooltipBasicPatch
    {
        private static void Prefix(GameItemElement __instance)
        {
            // 액체 이름 번역 오류("Translation Error 'liquid_...'")를 툴팁에 쓰이기 전에 복구
            BetterScannerUI.ScannerBars.RepairAllLiquidNames();
            Ctx.Item = __instance;
            Ctx.BoughtMerged = false;
        }

        private static void Finalizer()
        {
            Ctx.Item = null;
            Ctx.BoughtMerged = false;
        }
    }

    [HarmonyPatch(typeof(LocHelper), nameof(LocHelper.GetLocalizedMechanic))]
    internal static class GetLocalizedMechanicPatch
    {
        private static void Postfix(string key, object[] arguments, ref string __result)
        {
            GameItemElement item = Ctx.Item;
            if (item == null) return;
            try
            {
                if (key == "mech_tooltip_est_value")
                {
                    string block = ValueBlock.Build(item, arguments);
                    if (block != null)
                    {
                        __result = block;
                        Ctx.BoughtMerged = true;
                    }
                }
                else if (key == "mech_tooltip_bought_at" && Ctx.BoughtMerged)
                {
                    __result = "";
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ValueTooltip] failed: " + ex.Message);
            }
        }
    }

    internal static class ValueBlock
    {
        // 게임은 이 줄 바로 앞에 AddEmptyLine(14) 로 14px 공백 한 글자를 넣는데, 원래 첫 줄이 그 뒤에 이어 붙어 한 칸 들여써진다.
        // 블록을 줄바꿈으로 시작해서 14px 공백은 간격용 줄로만 남기고, 세 줄 모두 왼쪽 끝에서 시작하게 한다.
        // 게임의 크레딧 아이콘 (TMP 스프라이트 "c", tint=1 이라 글자색을 따라감). 가격 표시에 게임이 쓰는 것과 같은 아이콘.
        private static string Credit => GeneralHelper.CreditIcon();
        private const string MainColor = "#ffffff";
        private const string DimColor = "#8a8f94";
        private const string GoodColor = "#8fc46a";
        private const string BadColor = "#d9695f";

        public static string Build(GameItemElement item, object[] args)
        {
            if (args == null || args.Length < 2) return null;
            double est = ToDouble(args[0]);
            double baseValue = ToDouble(args[1]);
            int bought = 0;
            try { bought = item.GetTagReadonly("BOUGHT_PRICE_TAG").GetInt(); } catch { }
            int cells = CountCells(item);

            var sb = new StringBuilder();
            // 앞의 14px 간격 공백과 줄을 분리. 그대로 두면 간격이 너무 넓어서 이 줄바꿈은 높이 0 (14px 간격 줄만 남김).
            sb.Append("<line-height=0%>").Append('\n').Append("</line-height>");

            // 1) 현재가: 15 (+4 이득)
            sb.Append("<color=").Append(MainColor).Append('>')
              .Append(Credit).Append(" ").Append(T("value_current")).Append(": ").Append(Num(est)).Append("</color>");
            if (bought != 0)
            {
                double profit = est - bought;
                if (Math.Abs(profit) >= 0.5)
                {
                    bool gain = profit > 0;
                    sb.Append(" <color=").Append(gain ? GoodColor : BadColor).Append(">(")
                      .Append(T(gain ? "value_gain" : "value_loss", Signed(profit))).Append(")</color>");
                }
            }
            else if (baseValue > 0)
            {
                // 아직 안 산 물건: 기본가 대비 비싼지/싼지 (싸면 초록, 비싸면 빨강)
                double diff = est - baseValue;
                if (Math.Abs(diff) >= 0.5)
                {
                    bool cheap = diff < 0;
                    sb.Append(" <color=").Append(cheap ? GoodColor : BadColor).Append(">(")
                      .Append(T(cheap ? "value_vs_base_cheap" : "value_vs_base_pricey", Signed(diff))).Append(")</color>");
                }
            }

            // 2) 칸당 가치: 7.5
            if (cells > 0)
            {
                double per = est / cells;
                sb.Append('\n').Append("<color=").Append(MainColor).Append('>')
                  .Append(Credit).Append(" ").Append(T("value_per_cell")).Append(": ")
                  .Append(per >= 100 ? Num(per) : per.ToString("0.#", CultureInfo.InvariantCulture))
                  .Append("</color>");
            }

            // 3) 구매가: 11 (기본가: 25)   — 흐리게. 구매 기록이 없으면 기본가만.
            sb.Append('\n').Append("<color=").Append(DimColor).Append('>');
            if (bought != 0)
                sb.Append(Credit).Append(" ").Append(T("value_paid")).Append(": ").Append(Num(bought))
                  .Append(" (").Append(T("value_base")).Append(": ").Append(Num(baseValue)).Append(')');
            else
                sb.Append(Credit).Append(" ").Append(T("value_base")).Append(": ").Append(Num(baseValue));
            sb.Append("</color>");

            return sb.ToString();
        }

        private static int CountCells(GameItem item)
        {
            try
            {
                GridShape shape = item.shape ?? item.modifiedShape;
                if (shape == null) return 0;
                int n = 0;
                for (int x = 0; x < shape.width; x++)
                    for (int y = 0; y < shape.height; y++)
                        if (shape.GetLocal(x, y) != 0) n++;
                return n;
            }
            catch
            {
                return 0;
            }
        }

        private static string T(string key, params object[] args) => PenLabelerAssist.Text.Get(key, args);

        private static string Num(double v) => Math.Round(v).ToString("#,0", CultureInfo.InvariantCulture);

        private static string Signed(double v)
        {
            string s = Math.Abs(v) >= 100 || Math.Abs(v - Math.Round(v)) < 0.05
                ? Math.Round(v).ToString("#,0", CultureInfo.InvariantCulture)
                : v.ToString("0.#", CultureInfo.InvariantCulture);
            return v > 0 ? "+" + s : s;
        }

        private static double ToDouble(object o)
        {
            try { return Convert.ToDouble(o, CultureInfo.InvariantCulture); } catch { return 0; }
        }
    }
}
