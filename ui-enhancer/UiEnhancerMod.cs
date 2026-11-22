using UnityEngine;

namespace UiEnhancer
{
    // UI 개선 모드: 손님 거래창 꾸미기 (ClientWindow.cs 와 Tablet*.cs, 숫자 설정은 Settings.cs).
    // Harmony 패치는 모드 로더가 이 어셈블리에 PatchAll 로 적용한다.
    // 거래창이 열릴 때마다 윈도우가 있는지 확인 (지워졌으면 다시 만든다)
    [HarmonyLib.HarmonyPatch(typeof(NegociationUIManager), nameof(NegociationUIManager.OpenUI))]
    internal static class OpenTabletPatch
    {
        private static void Postfix() => UiEnhancerMod.EnsureWindow();
    }

    // 게임이 거래창 내용(목록, 관심사 등)을 새로 만들 때 부르는 함수.
    //  - 모드에 "내용이 바뀌었다" 고 알린다 → 이번 프레임에 거래창 전체를 다시 꾸민다 (평소엔 가끔만, 무거워서)
    //  - 관심사 줄: 옛 부품은 Destroy 해도 프레임 끝에야 지워져 옛/새가 한 프레임 같이 배치된다 → 바로 꺼 둔다 (꺼진 건 배치에서 건너뜀)
    [HarmonyLib.HarmonyPatch(typeof(TabletUI), nameof(TabletUI.ClearChildren))]
    internal static class ClearChildrenPatch
    {
        private static void Prefix(RectTransform container)
        {
            if (container == null) return;
            ClientWindow.MarkDirty();
            if (container.name != "InfoHost" && !ClientWindow.IsInfoHost(container)) return;
            foreach (Transform c in container) c.gameObject.SetActive(false);
        }
    }

    public class UiEnhancerMod : IMod
    {
        internal const string ModId = "ui-enhancer";

        public void Init(ModManifest manifest)
        {
            Debug.Log("[UiEnhancer] init v" + (manifest != null ? manifest.ModVersion : "?"));
        }

        public void OnEnable()
        {
            ModHook.OnGameLoadedLate += OnGameLoaded;
            // 밤/아침에 거래창 부모가 정리되면서 윈도우가 같이 지워질 수 있다 → 일어날 때, 셔터 열 때, 가게로 돌아올 때 다시 만든다
            ModHook.OnWakingUpLate += EnsureWindow;
            ModHook.OnShutterOpenedLate += EnsureWindow;
            ModHook.OnReturningStoreLate += EnsureWindow;
        }

        internal static void EnsureWindow()
        {
            try { ClientWindow.Ensure(); }
            catch (System.Exception ex) { Debug.LogWarning("[UiEnhancer] client window: " + ex.Message); }
        }

        private static void OnGameLoaded()
        {
            try { ClientWindow.Ensure(); }
            catch (System.Exception ex) { Debug.LogWarning("[UiEnhancer] client window: " + ex.Message); }
        }

        public void OnDisable()
        {
            ModHook.OnGameLoadedLate -= OnGameLoaded;
            ModHook.OnWakingUpLate -= EnsureWindow;
            ModHook.OnShutterOpenedLate -= EnsureWindow;
            ModHook.OnReturningStoreLate -= EnsureWindow;
            ClientWindow.Remove();
        }
    }
}
