using System;
using HarmonyLib;
using UnityEngine;

namespace CategoryPills
{
    // 아이템 툴팁의 카테고리 " [생활용품] [필터]" 를 배경이 칠해진 박스(pill) 형태로 바꾼다.
    // TextMeshPro 에는 테두리(outline box) 태그가 없어서 <mark> 하이라이트 배경으로 박스를 만든다.
    // 원본 CommonTooltipHelper.CreateItemTypeTooltip 과 같은 데이터/색(일반 = 청록, 밀수품 = 빨강)을 쓰고,
    // 실패하면 원본을 그대로 실행한다.
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.

    [HarmonyPatch(typeof(CommonTooltipHelper), nameof(CommonTooltipHelper.CreateItemTypeTooltip))]
    internal static class CreateItemTypeTooltipPatch
    {
        // TMP 에는 </alpha> 닫는 태그가 없다 (글자로 찍혀 버림). <alpha=#FF> 로 되돌린다.
        // 양 끝 고정점: 1px 크기 투명 '.' (폭이 거의 0이라 여백에 영향 없음). 실제 안쪽 여백은 <space=4px> 로 좌우 똑같이.
        private const string Pad = "<size=1px><alpha=#00>.<alpha=#FF></size>";
        private const string Space = "<space=4px>";
        private const string NormalBg = "#3fb8b840";
        private const string ContrabandBg = "#d9695f40";

        private static bool Prefix(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (builder == null || item == null) return true;
                // 카테고리 위 여백 약 +5px: 5px 크기 공백 한 글자짜리 줄
                builder.AddLine("<size=5px> </size>");
                bool any = false;
                foreach (string type in item.GetGameItemType())
                {
                    bool contraband = type.Equals("CONTRABAND");
                    string name = TypeHelper.GetTypeDisplayName(type);
                    // TMP 의 <mark> 는 마지막 "보이는" 글자까지만 칠해서 끝의 공백이 빠진다(오른쪽 여백만 좁아짐).
                    // 양쪽 끝에 완전 투명한 '.' 을 둬서 좌우 여백을 똑같이 만든다.
                    string text = (any ? "  " : "") + "<mark=" + (contraband ? ContrabandBg : NormalBg) + ">"
                        + Pad + Space + name + Space + Pad + "</mark>";
                    builder.Add(text, contraband ? RenderHandler.ColorPalette.LightRed : RenderHandler.ColorPalette.LightCyan);
                    any = true;
                }
                builder.AddLine("");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CategoryPills] failed, using vanilla: " + ex.Message);
                return true;
            }
        }
    }
}
