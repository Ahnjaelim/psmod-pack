using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapExtension
{
    // 분점 자동 채우기 설정 창 (게임 호퍼 설정 창과 같은 모양: CustomUIManager, 기계 스타일)
    //  [■] 매일 자동 채우기
    //  필터: 전부(손님이 좋아하는 것) / 허용 목록만 / 차단 목록 제외 전부
    //  검색 입력칸 → 치는 대로 결과 (누르면 목록에 추가) / 목록 칸 (누르면 빠짐)
    // 호퍼는 아이템을 직접 클릭해서 추가하지만, 여기선 이름 검색으로 추가한다.
    internal static class BranchRestockWindow
    {
        private const string WindowId = "vme_branch_restock";
        // 목록에 아이템 넣는 방법: true = 이름 검색 / false = 호퍼처럼 [아이템 추가] 누르고 가게의 물건을 직접 클릭
        private const bool UseSearch = false;
        private static BranchSite site;
        private static RectTransform resultHost, listHost;
        private static string query = "";

        private static readonly string[] ModeNames = { "전부 (이 분점 손님이 좋아하는 것)", "허용 목록만", "차단 목록 제외 전부" };

        internal static void Open(BranchSite b)
        {
            if (CustomUIManager.Instance == null || b == null) return;
            Window()?.Close();
            site = b;
            query = "";
            if (UseSearch) ItemIndex.EnsureBuilding();

            CustomUIBuilder ui = CustomUIManager.Instance.CreateWindow(WindowId, b.Name + " · 자동 채우기", "paper", null, CustomUIManager.Style.Machinery)
                .SetSize(700f, 760f).SetDraggable(true).SetCloseOnEscape()
                .OnClose(() => { resultHost = null; listHost = null; StopPick(); BranchMonitor.Refresh(); });

            ui.AddToggle("매일 자동 채우기 (아침에 손님이 오기 전)", b.AutoRestock, on => { b.AutoRestock = on; Click(); BranchMonitor.Refresh(); });
            ui.AddToggle("가격 하락 이벤트 중인 물건은 안 팔기", b.HoldOnPriceDrop, on => { b.HoldOnPriceDrop = on; Click(); BranchMonitor.Refresh(); })
              .WithTooltip("켜면 오늘 시세 이벤트로 값이 내린 물건은 손님이 와도 안 판다. 이벤트가 끝나면 다시 판다.");
            ui.AddLabel("<color=#9a9aa8>본점 인벤토리에서 옮긴다. 고정한 물건, 밀수품, 가방·상자, 기계는 안 옮긴다.</color>");
            ui.AddSeparator();
            ui.BeginRow(6f, TextAnchor.MiddleLeft);
            ui.AddLabel("필터");
            ui.AddFlexibleSpace();
            if (!UseSearch)
            {
                ui.AddButton(Picking ? "고르기 끝" : "아이템 추가", TogglePick, "vme_add_items")
                  .WithTooltip("누른 뒤 가게에 있는 물건을 클릭하면 목록에 추가된다. 우클릭이나 Esc로 끝.");
            }
            ui.End();
            for (int m = 0; m < ModeNames.Length; m++)
            {
                int mode = m;
                ui.AddToggle(ModeNames[m], b.FilterMode == mode, on =>
                {
                    CustomUIWindow w = Window();
                    if (!on) { w?.Get(ModeId(mode))?.SetBool(true); return; } // 라디오: 끄기는 안 됨
                    Click();
                    for (int i = 0; i < ModeNames.Length; i++)
                        if (i != mode) w?.Get(ModeId(i))?.SetBool(false);
                    b.FilterMode = mode;
                    Rebuild();
                }, ModeId(m));
            }
            if (UseSearch)
            {
                ui.AddInput("아이템 이름으로 검색해서 목록에 추가", "", null, "vme_search", q => { query = q ?? ""; RebuildResults(); });
                ui.BeginScroll(150f);
                resultHost = ui.CurrentContainer;
                ui.End();
            }
            ui.AddLabel("목록 (누르면 빠진다)");
            ui.BeginScrollFlexible(196f);
            listHost = ui.CurrentContainer;
            ui.End();
            ui.Center();
            CustomUIWindow shown = ui.Show();
            try { BranchMonitor.RaiseAbove(shown?.Rect?.gameObject); } catch (Exception ex) { Debug.LogWarning("[MapExtension] raise: " + ex.Message); }
            Rebuild();
        }

        // ---- 호퍼식 고르기 ----
        internal static bool Picking { get; private set; }
        private static PickRunner runner;

        private static void TogglePick()
        {
            Click();
            if (Picking) StopPick();
            else
            {
                if (runner == null)
                {
                    var go = new GameObject("VME_PickRunner");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    runner = go.AddComponent<PickRunner>();
                }
                Picking = true;
                runner.Begin();
                BranchMonitor.SetHidden(true); // 가게 물건이 보이게
                Window()?.Get("vme_add_items")?.SetText("고르기 끝");
            }
        }

        internal static void StopPick()
        {
            if (!Picking) return;
            Picking = false;
            BranchMonitor.SetHidden(false);
            try { TooltipManager._instance?.HideTooltip(); } catch { }
            Window()?.Get("vme_add_items")?.SetText("아이템 추가");
        }

        internal static bool PickVerdict(GameItem hit, out string message)
        {
            if (hit == null) { message = "목록에 넣을 물건을 클릭 (우클릭/Esc: 끝)"; return false; }
            string name = hit.GetDisplayName(true);
            if (site == null || string.IsNullOrEmpty(hit.identifier) || !GeneralHelper.IsItemOwned(hit))
            {
                message = name + ": 넣을 수 없다"; return false;
            }
            if (site.FilterIds.Contains(hit.identifier)) { message = name + ": 이미 목록에 있다"; return false; }
            message = "클릭: " + name + " 추가";
            return true;
        }

        internal static void AddPicked(GameItem hit)
        {
            if (site == null || site.FilterIds.Contains(hit.identifier)) return;
            site.FilterIds.Add(hit.identifier);
            try { EmporiumAudioPlayer.Instance?.PlayUnMatched(); } catch { }
            RebuildList();
        }

        private static string ModeId(int m) => "vme_mode_" + m;

        private static CustomUIWindow Window()
        {
            CustomUIWindow w = CustomUIManager.Instance != null ? CustomUIManager.Instance.GetWindow(WindowId) : null;
            return w != null && w.IsAlive ? w : null;
        }

        private static void Click()
        {
            try { EmporiumAudioPlayer.Instance.audioButton1.Play(); } catch { }
        }

        private static void Rebuild()
        {
            RebuildResults();
            RebuildList();
        }

        // 검색 결과: 이름에 검색어가 들어간 아이템 (최대 24개)
        private static void RebuildResults()
        {
            CustomUIWindow w = Window();
            if (w != null && site != null && !UseSearch)
            {
                w.Get("vme_add_items")?.SetVisible(site.FilterMode != 0);
                listHost?.GetComponentInParent<ScrollRect>(true)?.gameObject.SetActive(site.FilterMode != 0);
                if (site.FilterMode == 0) StopPick();
            }
            if (w == null || resultHost == null || site == null) return;
            CustomUIBuilder.ClearContainer(resultHost);
            var ui = new CustomUIBuilder(CustomUIManager.Instance, w, resultHost);
            bool useList = site.FilterMode != 0;
            w.Get("vme_search")?.SetVisible(useList);
            resultHost.GetComponentInParent<ScrollRect>(true)?.gameObject.SetActive(useList);
            listHost?.GetComponentInParent<ScrollRect>(true)?.gameObject.SetActive(useList);
            if (!useList) { CustomUIBuilder.RebuildLayout(w.Rect); return; }

            string q = query.Trim();
            if (q.Length == 0)
            {
                ui.AddLabel("<color=#9a9aa8>검색어를 입력하면 여기 결과가 나온다.</color>");
            }
            else if (!ItemIndex.Ready)
            {
                ui.AddLabel("<color=#9a9aa8>아이템 목록 준비 중… (" + ItemIndex.Progress + ")</color>");
                CustomUIManager.Instance.StartCoroutine(RetryWhenReady());
            }
            else
            {
                List<string> hits = ItemIndex.Search(q, 24);
                if (hits.Count == 0) ui.AddLabel("<color=#9a9aa8>찾는 아이템이 없다.</color>");
                else
                {
                    ui.BeginGrid(3, 200f, 64f);
                    foreach (string id in hits)
                    {
                        string iid = id;
                        bool inList = site.FilterIds.Contains(iid);
                        AddChip(ui, iid, inList ? "이미 목록에 있다" : "목록에 추가", () =>
                        {
                            if (!site.FilterIds.Contains(iid)) site.FilterIds.Add(iid);
                            Click();
                            RebuildList();
                            RebuildResults();
                        }, inList);
                    }
                    ui.End();
                }
            }
            CustomUIBuilder.RebuildLayout(w.Rect);
        }

        private static IEnumerator RetryWhenReady()
        {
            while (!ItemIndex.Ready) yield return null;
            RebuildResults();
        }

        private static void RebuildList()
        {
            CustomUIWindow w = Window();
            if (w == null || listHost == null || site == null) return;
            CustomUIBuilder.ClearContainer(listHost);
            if (site.FilterMode == 0) { CustomUIBuilder.RebuildLayout(w.Rect); return; }
            var ui = new CustomUIBuilder(CustomUIManager.Instance, w, listHost);
            if (site.FilterIds.Count == 0)
            {
                ui.AddLabel(site.FilterMode == 1 ? "허용 목록이 비어 있다. 위에서 검색해서 추가하자." : "차단 목록이 비어 있다. 손님이 좋아하는 건 전부 옮긴다.");
            }
            else
            {
                ui.BeginGrid(3, 200f, 92f);
                foreach (string id in new List<string>(site.FilterIds))
                {
                    string iid = id;
                    AddChip(ui, iid, "목록에서 빼기", () =>
                    {
                        site.FilterIds.Remove(iid);
                        Click();
                        RebuildList();
                        RebuildResults();
                    }, false);
                }
                ui.End();
            }
            CustomUIBuilder.RebuildLayout(w.Rect);
        }

        // 호퍼 목록 칸과 같은 모양 (그림 + 이름, 누르면 동작)
        private static void AddChip(CustomUIBuilder ui, string id, string tooltip, Action onClick, bool dim)
        {
            (string name, Sprite sprite) = ItemIndex.Describe(id);
            ui.AddImage((Sprite)null, 200f, 64f).WithTooltip(tooltip);
            CustomUIElement plate = ui.Last;
            ui.AddImage(sprite, 32f, 32f);
            CustomUIElement icon = ui.Last;
            if (sprite == null) icon?.SetImageColor(new Color(1f, 1f, 1f, 0.15f));
            ui.AddLabel(dim ? "<color=#8a8a96>" + name + "</color>" : name);
            CustomUIElement label = ui.Last;
            if (label?.text != null)
            {
                label.text.horizontalAlignment = HorizontalAlignmentOptions.Center;
                label.text.enableWordWrapping = true;
            }
            if (plate?.root == null) return;
            try
            {
                MachineStorageCoordinator.SetupChipPlate(plate, onClick);
                if (plate.image != null)
                {
                    plate.image.sprite = MachineryStyle.PlateNotched;
                    plate.image.type = Image.Type.Sliced;
                    plate.image.pixelsPerUnitMultiplier = 0.5f;
                    plate.image.preserveAspect = false;
                }
                MachineStorageCoordinator.NestInPlate(icon, plate);
                MachineStorageCoordinator.NestInPlate(label, plate);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] chip: " + ex.Message);
            }
        }
    }

    // ---- 호퍼식 고르기: [아이템 추가] → 가게 물건을 클릭하면 그 종류(ID)가 목록에 ----
    internal class PickRunner : MonoBehaviour
    {
        private int startedFrame;
        internal void Begin() => startedFrame = Time.frameCount;

        private void Update()
        {
            if (!BranchRestockWindow.Picking) return;
            if (Time.frameCount == startedFrame) return; // [아이템 추가] 누른 그 클릭은 무시
            if (Input.GetMouseButtonDown(1) || KeyBindHelper.GetBackDown()) { BranchRestockWindow.StopPick(); return; }
            GameItem hit = null;
            try
            {
                if (StoreUIManager.Instance == null || !StoreUIManager.Instance.hoveringStoreUI)
                    hit = RenderHandler.RaycastElement<GameItem>(Input.mousePosition);
            }
            catch
            {
            }
            bool ok = BranchRestockWindow.PickVerdict(hit, out string message);
            try
            {
                // 물건 위에 있을 때만 안내 (빈 곳에서는 툴팁 없음)
                if (hit != null) TooltipManager._instance?.SetAndShowTooltip(message);
                else TooltipManager._instance?.HideTooltip();
            }
            catch
            {
            }
            if (Input.GetMouseButtonDown(0) && hit != null)
            {
                if (ok) BranchRestockWindow.AddPicked(hit);
                else try { EmporiumAudioPlayer.Instance?.audioNegativeTwoBeep.Play(); } catch { }
            }
        }
    }

    // 고르는 동안 게임의 아이템 클릭(집기/사용/더블클릭/다중선택)을 막는다 (게임 핀 모드와 같은 방식)
    [HarmonyLib.HarmonyPatch(typeof(ItemPinHelper), nameof(ItemPinHelper.BlocksClicks), HarmonyLib.MethodType.Getter)]
    internal static class PickBlocksClicksPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (BranchRestockWindow.Picking) __result = true;
        }
    }

    // 전체 아이템 이름표 (검색용). 아이템을 잠깐 만들어 이름을 읽고 지운다 (게임 호퍼 목록과 같은 방식).
    // 처음 한 번만, 멈칫하지 않게 여러 프레임에 나눠 만든다.
    internal static class ItemIndex
    {
        private static List<(string id, string name)> entries;
        private static bool building;
        private static int done, total;
        private static readonly Dictionary<string, (string, Sprite)> describeCache = new Dictionary<string, (string, Sprite)>();

        internal static bool Ready => entries != null && !building;
        internal static string Progress => total > 0 ? (done * 100 / total) + "%" : "…";

        internal static void EnsureBuilding()
        {
            if (entries != null || building || CustomUIManager.Instance == null) return;
            building = true;
            CustomUIManager.Instance.StartCoroutine(Build());
        }

        private static IEnumerator Build()
        {
            var list = new List<(string, string)>();
            List<string> ids = null;
            try { ids = DirectoryMaster.GetIdentifierList<GameItem>(); }
            catch (Exception ex) { Debug.LogWarning("[MapExtension] item index: " + ex.Message); }
            ids = ids ?? new List<string>();
            total = ids.Count;
            done = 0;
            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id)) { done++; continue; }
                string name = id;
                try
                {
                    GameItem g = DirectoryMaster.Item(id);
                    if (g != null)
                    {
                        name = g.GetDisplayName();
                        GraphUtils.DestroyIfUnplaced(g);
                    }
                }
                catch
                {
                }
                list.Add((id, name ?? id));
                done++;
                if (done % 40 == 0) yield return null;
            }
            entries = list;
            building = false;
            byName = null;
            try { BranchMonitor.Refresh(); } catch { } // 기록 표 아이템 그림 다시 그리기
            Debug.Log("[MapExtension] item index: " + list.Count + " items");
        }

        // 표시 이름이 정확히 같은 아이템 ID (첫 번째). 목록이 아직 없으면 null
        private static Dictionary<string, string> byName;

        internal static string IdByName(string name)
        {
            if (entries == null || string.IsNullOrEmpty(name)) return null;
            if (byName == null)
            {
                byName = new Dictionary<string, string>();
                foreach (var (id, n) in entries)
                    if (!string.IsNullOrEmpty(n) && !byName.ContainsKey(n)) byName[n] = id;
            }
            return byName.TryGetValue(name, out string r) ? r : null;
        }

        // 이름(또는 ID)에 검색어가 들어간 것. 이름이 검색어로 시작하는 것 먼저
        internal static List<string> Search(string q, int max)
        {
            var starts = new List<string>();
            var contains = new List<string>();
            if (entries == null) return starts;
            string lq = q.ToLowerInvariant();
            foreach (var (id, name) in entries)
            {
                string ln = name.ToLowerInvariant();
                if (ln.StartsWith(lq)) starts.Add(id);
                else if (ln.Contains(lq) || id.Contains(lq)) contains.Add(id);
                if (starts.Count >= max) break;
            }
            starts.AddRange(contains);
            if (starts.Count > max) starts.RemoveRange(max, starts.Count - max);
            return starts;
        }

        internal static (string name, Sprite sprite) Describe(string id)
        {
            if (describeCache.TryGetValue(id, out (string, Sprite) v)) return v;
            string name = id;
            Sprite sprite = null;
            try
            {
                GameItem g = DirectoryMaster.Item(id);
                if (g != null)
                {
                    name = g.GetDisplayName();
                    sprite = (g as GameItemElement)?.ResolveSpriteByName(g.spritePath);
                    GraphUtils.DestroyIfUnplaced(g);
                }
            }
            catch
            {
            }
            describeCache[id] = (name, sprite);
            return (name, sprite);
        }
    }
}
