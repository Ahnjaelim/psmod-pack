using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PinOverlayFade
{
    // 고정(핀)한 아이템 위에 깔리는 빗금(PinOverlay)을 옅게 한다.
    // 원래는 GameItemElement.PlacePinOverlayNode 가 image.color 를 흰색(불투명)으로 정해 두므로, 끝난 뒤 알파만 낮춘다.
    // Merged into the Personal QoL Tweaks DLL; patches are applied by the loader's PatchAll.
    [HarmonyPatch(typeof(GameItemElement), "PlacePinOverlayNode")]
    internal static class PlacePinOverlayNodePatch
    {
        internal const float Alpha = 0.4f;

        private static void Postfix(List<TreeNodeRender> pool, int index)
        {
            try
            {
                if (pool == null || index < 0 || index >= pool.Count) return;
                TreeNodeRender n = pool[index];
                if (n == null || n.image == null) return;
                Color c = n.image.color;
                if (c.a != Alpha)
                {
                    c.a = Alpha;
                    n.image.color = c;
                }
            }
            catch
            {
            }
        }
    }
}
