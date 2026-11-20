using System;
using HarmonyLib;
using UnityEngine;

namespace MapExtension
{
    // 지도 확장 세이브 DB: 게임이 모드용으로 열어 둔 세이브 저장소(ModHelper.SetModData → playerStore.modData)에 문자열로 저장.
    // 세이브와 같이 저장되고 같이 되돌아가므로, 예전 세이브를 불러와도 물건이 복사되지 않는다.
    //  - farm/<장소>/floor        : 그 장소 바닥에 두고 온 물건
    //  - farm/<장소>/box/<물건>   : 그 물건(약장 등) 상자에 남긴 물건 (안 깐 건 VME_UNREVEALED_TAG 태그로 표시)
    //  - branch/<분점>            : "1|번 돈|오늘 수입" (산 분점만), branch/last_paid_day : 마지막으로 수입 준 날
    // 아이템 ↔ 문자열은 게임이 인벤토리를 저장할 때 쓰는 함수(PlayerStore.EncodeSaveItem / DecodeSaveItem) 그대로.
    // 뒤진 날짜(고갈)는 원래부터 외출 장소 카운터로 세이브에 저장된다.
    internal static class SaveDb
    {
        private static string FloorKey(FarmLocation loc) => "farm/" + loc.Key + "/floor";
        private static string BoxKey(FarmLocation loc, FarmSpot s) => "farm/" + loc.Key + "/box/" + s.Key;

        // 게임이 세이브 파일을 쓰기 직전 (잠잘 때 / F5 강제 저장 모두 PlayerStore.SaveGame)
        internal static void Write()
        {
            try { Branches.Write(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] save db write branches: " + ex.Message); }
            foreach (FarmLocation loc in FarmLocations.All)
            {
                try
                {
                    Put(FloorKey(loc), loc.SaveFloor());
                    foreach (FarmHotspot h in loc.Hotspots())
                        Put(BoxKey(loc, h.spot), h.SaveBox());
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[MapExtension] save db write " + loc.Id + ": " + ex.Message);
                }
            }
        }

        // 세이브를 불러온 직후: 메모리 상태를 비우고 DB 에서 다시 채운다 (다른 세이브를 불러와도 섞이지 않게)
        internal static void Read()
        {
            try { Branches.Read(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] save db read branches: " + ex.Message); }
            foreach (FarmLocation loc in FarmLocations.All)
            {
                try
                {
                    loc.LoadFloor(ModHelper.GetModData(MapExtensionMod.ModId, FloorKey(loc)));
                    int boxes = 0;
                    foreach (FarmHotspot h in loc.Hotspots())
                    {
                        string json = ModHelper.GetModData(MapExtensionMod.ModId, BoxKey(loc, h.spot));
                        h.LoadBox(json);
                        if (!string.IsNullOrEmpty(json)) boxes++;
                    }
                    Debug.Log("[MapExtension] save db read " + loc.Id + ": boxes " + boxes);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[MapExtension] save db read " + loc.Id + ": " + ex.Message);
                }
            }
        }

        private static void Put(string key, string json)
        {
            if (string.IsNullOrEmpty(json)) ModHelper.RemoveModData(MapExtensionMod.ModId, key);
            else ModHelper.SetModData(MapExtensionMod.ModId, key, json);
        }
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SaveGame))]
    internal static class SaveGamePatch
    {
        private static void Prefix()
        {
            try { SaveDb.Write(); }
            catch (Exception ex) { Debug.LogWarning("[MapExtension] save db: " + ex.Message); }
        }
    }
}
