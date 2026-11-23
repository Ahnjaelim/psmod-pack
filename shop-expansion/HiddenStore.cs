using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShopExpansion
{
    // 모드 전용 숨김 인벤: 방에 붙박이로 들어가는 기계(진짜 게임 아이템)를 이름표(key)마다 하나씩 보관한다.
    //  - 플레이어에게는 안 보인다. 방에서 그 자리를 누르면 기계의 창(contentWindow)만 연다.
    //  - 저장: 게임 가방 저장 형식(EncodeSaveItem) → 모드 데이터 "hidden/<key>" (세이브에 같이 저장/되돌림).
    //  - 밤: 게임 EndNight 가 가게 아이템에 돌리는 기계 처리 3단계(Early / 보통 / Late)를 숨김 인벤 아이템에도 똑같이 돌린다.
    //  - 게임의 "가게 아이템 전체(FindAllItem)"에는 안 넣는다 (손님 요청, 가게 가치 등에 잡히지 않게).
    internal static class HiddenStore
    {
        private const int HolderW = 12, HolderH = 12; // 기계 하나 들어갈 크기면 충분
        private const string Prefix = "hidden/";

        private class Holder
        {
            public PixelWindow Win;
            public GameInventory Inv;
            public GameItem Box;
        }

        private static readonly Dictionary<string, Holder> holders = new Dictionary<string, Holder>();

        private static Holder Get(string key)
        {
            if (holders.TryGetValue(key, out Holder h)) return h;
            (PixelWindow win, GameInventory inv) = DirectoryUtils.CreateInventoryWindow(HolderW, HolderH);
            GameItem box = ItemDirectory.CreateEmptyItem("vse_hidden_holder").SetContentWindow(win).SetSpriteAndShape("Items/items_container", "evidence_box");
            box.name = "숨김 보관 " + key;
            box.EnableTag("IS_OWNED_TAG");
            h = new Holder { Win = win, Inv = inv, Box = box };
            holders[key] = h;
            return h;
        }

        // 이름표의 기계 (없으면 null)
        internal static GameItem Find(string key)
        {
            return holders.TryGetValue(key, out Holder h) ? h.Inv.childItems.FirstOrDefault() : null;
        }

        // 이름표의 기계, 없으면 create() 로 만들어 넣는다
        internal static GameItem GetOrCreate(string key, Func<GameItem> create)
        {
            Holder h = Get(key);
            GameItem item = h.Inv.childItems.FirstOrDefault();
            if (item != null) return item;
            item = create();
            if (item == null) return null;
            item.EnableTag("IS_OWNED_TAG");
            h.Inv.TryAcceptAll(item);
            if (!h.Inv.childItems.Contains(item)) Debug.LogWarning("[ShopExpansion] hidden store '" + key + "': item did not fit");
            return item;
        }

        // 숨김 인벤의 기계들 (맨 위 단계만)
        internal static IEnumerable<GameItem> Machines()
        {
            foreach (Holder h in holders.Values)
                foreach (GameItem i in h.Inv.childItems)
                    yield return i;
        }

        // 숨김 인벤 안 모든 아이템 (기계 안의 물고기, 배터리 등까지)
        private static List<GameItem> AllItems()
        {
            var list = new List<GameItem>();
            foreach (Holder h in holders.Values)
            {
                try { list.AddRange(GraphUtils.FindAllChildrenType<GameItem>(h.Win)); }
                catch (Exception ex) { Debug.LogWarning("[ShopExpansion] hidden store items: " + ex.Message); }
            }
            return list;
        }

        // ---- 밤 (게임 PlayerStore.EndNight 와 같은 순서) ----
        internal static void Night()
        {
            List<GameItem> items = AllItems();
            foreach (GameItem i in items)
            {
                try { i.onCycleEndEarlySlotItemFunc?.Invoke(i, i.parentInventory, i.GetIncompleteSlotInInventory()); }
                catch (Exception ex) { Debug.LogWarning("[ShopExpansion] night early " + i.name + ": " + ex.Message); }
            }
            items = AllItems();
            foreach (GameItem i in items)
            {
                try { i.onCycleEndSlotItemFunc?.Invoke(i, i.parentInventory, i.GetIncompleteSlotInInventory()); }
                catch (Exception ex) { Debug.LogWarning("[ShopExpansion] night " + i.name + ": " + ex.Message); }
            }
            items = AllItems();
            foreach (GameItem i in items)
            {
                try { i.onCycleEndLateSlotItemFunc?.Invoke(i, i.parentInventory, i.GetIncompleteSlotInInventory()); }
                catch (Exception ex) { Debug.LogWarning("[ShopExpansion] night late " + i.name + ": " + ex.Message); }
            }
        }

        // ---- 저장 ----
        internal static void Write()
        {
            foreach (var kv in holders)
            {
                try
                {
                    Holder h = kv.Value;
                    if (h.Inv.childItems.Count == 0) { ModHelper.RemoveModData(ShopExpansionMod.ModId, Prefix + kv.Key); continue; }
                    string json = PlayerStore.Instance.EncodeSaveItem("save_bag", (PixelElement)h.Inv, h.Win);
                    if (!string.IsNullOrEmpty(json)) ModHelper.SetModData(ShopExpansionMod.ModId, Prefix + kv.Key, json);
                }
                catch (Exception ex) { Debug.LogWarning("[ShopExpansion] hidden store write " + kv.Key + ": " + ex.Message); }
            }
        }

        // 세이브를 불러온 직후: 비우고 다시 채운다 (다른 세이브와 섞이지 않게)
        internal static void Read()
        {
            foreach (Holder h in holders.Values)
            {
                try
                {
                    foreach (GameItem i in new List<GameItem>(h.Inv.childItems))
                    {
                        try { i.contentWindow?.Hide(); } catch { }
                        h.Inv.Expel(i);
                        i.Destroy();
                    }
                }
                catch { }
            }
            int n = 0;
            foreach (string k in ModHelper.GetModDataKeys(ShopExpansionMod.ModId) ?? new List<string>())
            {
                if (!k.StartsWith(Prefix)) continue;
                string key = k.Substring(Prefix.Length);
                try
                {
                    string json = ModHelper.GetModData(ShopExpansionMod.ModId, k);
                    if (string.IsNullOrEmpty(json)) continue;
                    PlayerStore.Instance.DecodeSaveItem(json, Get(key).Inv);
                    n++;
                }
                catch (Exception ex) { Debug.LogWarning("[ShopExpansion] hidden store read " + key + ": " + ex.Message); }
            }
            Debug.Log("[ShopExpansion] hidden store: " + n + " loaded");
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SaveGame))]
    internal static class HiddenStoreSavePatch
    {
        private static void Prefix()
        {
            try { HiddenStore.Write(); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] hidden store save: " + ex.Message); }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.EndNight))]
    internal static class HiddenStoreNightPatch
    {
        private static void Postfix()
        {
            try { HiddenStore.Night(); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] hidden store night: " + ex.Message); }
        }
    }
}
