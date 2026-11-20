using System;
using UnityEngine;

namespace MapExtension
{
    // Harmony 패치는 모드 로더가 이 어셈블리에 PatchAll 로 적용한다.
    public class MapExtensionMod : IMod
    {
        internal const string ModId = "map-extension";

        public void Init(ModManifest manifest)
        {
            Debug.Log("[MapExtension] init v" + (manifest != null ? manifest.ModVersion : "?"));
        }

        public void OnEnable()
        {
            RetiredMerchants.Register();
            FarmLocations.Register();
            Roaming.Register();
            BranchRenovation.Register();
            ModHook.OnGameLoadedLate += OnGameLoaded;
        }

        private static void OnGameLoaded()
        {
            try { BranchPanel.Ensure(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] branch panel: " + ex.Message); }
            try { OutingHud.Ensure(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] hud: " + ex.Message); }
            try { SaveDb.Read(); }
            catch (Exception ex) { Debug.LogWarning("[MapExtension] save db read: " + ex.Message); }
        }

        public void OnDisable()
        {
            ModHook.OnGameLoadedLate -= OnGameLoaded;
            Roaming.Unregister();
            BranchRenovation.Unregister();
            OutingHud.Remove();
            BranchPanel.Remove();
            RetiredMerchants.Unregister();
            try
            {
                TransitMap.Destroy();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] disable: " + ex.Message);
            }
        }
    }
}
