using System;
using UnityEngine;

namespace ShopExpansion
{
    // 가게 증축 모드: 조 와일드 네트워크 엔드 콘텐츠 탭에서 방을 사고(RoomUpgrades), 가게 화면 버튼으로 방을 오간다(Rooms, RoomPanel).
    // Harmony 패치는 모드 로더가 이 어셈블리에 PatchAll 로 적용한다.
    public class ShopExpansionMod : IMod
    {
        internal const string ModId = "shop-expansion";

        public void Init(ModManifest manifest)
        {
            Debug.Log("[ShopExpansion] init v" + (manifest != null ? manifest.ModVersion : "?"));
            // 새 아이템 종류 등록 (세이브를 불러오기 전에)
            try { ShopItems.Register(); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] items: " + ex.Message); }
        }

        public void OnEnable()
        {
            // 업그레이드 등록은 세이브를 불러온 뒤에 (모드가 켜질 때는 PlayerStore 가 아직 없어 만들다 오류가 난다)
            ModHook.OnGameLoadedLate += OnGameLoaded;
        }

        private static void OnGameLoaded()
        {
            try
            {
                RoomUpgrades.Register();
                Rooms.ResetOnLoad();
                HiddenStore.Read();
                AquaTank.AfterLoad();
                RoomPanel.Ensure();
            }
            catch (Exception ex) { Debug.LogWarning("[ShopExpansion] load: " + ex.Message); }
        }

        public void OnDisable()
        {
            ModHook.OnGameLoadedLate -= OnGameLoaded;
            try { Rooms.ReturnToShop(); } catch { }
            RoomPanel.Remove();
            RoomUpgrades.Unregister();
        }
    }

    // 외출 등으로 가게를 나가기 전에 (게임이 열린 창을 기억하기 전에) 가게 화면부터 되돌린다
    [HarmonyLib.HarmonyPatch(typeof(EmporiumEntry), nameof(EmporiumEntry.OnLeaveStore))]
    internal static class LeaveStoreReturnPatch
    {
        private static void Prefix()
        {
            try { Rooms.ReturnToShop(); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] leaving store: " + ex.Message); }
        }
    }
}
