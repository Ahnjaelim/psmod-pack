using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapExtension
{
    // 파밍 장소 (터미너스 스타일): 화면의 물건(핫스팟)을 클릭 → 확인 창(남은 기력 → 수색 후) → 수색.
    //  - 시간: 쓰레기장과 같은 "남은 수색 시간"(PlayerStore.scavengingAttempts)을 물건마다 정한 만큼 쓴다.
    //  - 부상/불운: 물건마다 확률. 행운이면 부상을 피한다 (쓰레기장과 같은 규칙).
    //  - 전리품: 게임 전리품 표(medicalTable 등)에서 굴려 외출 가방(바닥)에 넣는다.
    //  - 고갈: 뒤진 물건은 RefillDays 동안 비어 있다. 상태는 세이브(외출 장소 카운터)에 저장.
    //  - 배경: 아직 그림이 없어서 화면을 어두운 판으로 덮고 물건은 네모로 표시 (테스트용).
    internal sealed class FarmSpot
    {
        public string Key, Name, Desc;
        public Rect Area;            // 화면 비율 (0~1, 왼쪽 아래 기준)
        public int Cost = 1;         // 수색 시간
        public float MinorWound;     // %
        public float MajorWound;     // %
        public float Empty;          // 빈손 확률 %
        public string[] Tables;      // 전리품 표 (하나 고름)
        public string[] Items;       // 직접 지정 아이템 "id:가중치" (예: "bandage_item:30")
        public float ItemChance = 70f; // 한 번 굴릴 때 Items 에서 뽑을 확률 % (Tables 가 없으면 항상)
        public int MinItems = 1, MaxItems = 1;
        public string[] NeedsAny;    // 이 중 하나가 외출 가방에 있어야 함 (예: crowbar, lockpicks)
        public string NeedsText;
        public bool ConsumesTool;    // 열 때 NeedsAny 중 하나를 1개 쓴다 (일회용 키카드 등, 앞에 적힌 것부터)
        public int RefillDays = 3;
        public bool Telescope;       // 망원경 지점: 장소를 다 뒤져야 나타나고, 쓰면 Unlocks 장소가 열린다
        public string[] Unlocks;     // FarmLocation.Key 목록
    }

    internal class FarmLocation : AfterhourLocation
    {
        public string Key, Name, Desc;
        public List<FarmSpot> Spots = new List<FarmSpot>();
        protected virtual bool RefillsEnergy => true; // 도착하면 기력 채움 (분점은 아님)
        public Color BgColor = new Color32(0x1c, 0x1a, 0x26, 0xff);
        public string BackgroundFile; // Map/ 아래 PNG (없으면 임시 배경)
        internal const string PlaceholderBackground = "bg_placeholder.png";

        // 테스트용 기력 (0 = 끔). 출시 전에 0 으로!
        internal const int TestEnergy = 0;

        private Texture2D LoadBackground()
        {
            // 배경이 없거나 아직 안 그린 장소는 임시 배경(Map/bg_placeholder.png)
            byte[] bytes = string.IsNullOrEmpty(BackgroundFile) ? null : ModHelper.ReadModFileBytes(MapExtensionMod.ModId, "Map/" + BackgroundFile);
            if (bytes == null || bytes.Length == 0) bytes = ModHelper.ReadModFileBytes(MapExtensionMod.ModId, "Map/" + PlaceholderBackground);
            if (bytes == null || bytes.Length == 0) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) return null;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        public override string Id => "vme_farm_" + Key;
        public override string Source => MapExtensionMod.ModId;
        public override string GetTitle() => Name;
        // 시작 장소(약국, 철물점) 또는 다른 장소의 망원경으로 찾은 곳만
        public override bool IsDiscovered() => FarmLocationData.StartOpen.Contains(Key) || ModHelper.GetAfterhourCounter(Id, "unlocked", 0) == 1;

        internal static void SetUnlocked(string key) => ModHelper.SetAfterhourCounter("vme_farm_" + key, "unlocked", 1);
        internal static bool IsUnlocked(string key) => FarmLocationData.StartOpen.Contains(key) || ModHelper.GetAfterhourCounter("vme_farm_" + key, "unlocked", 0) == 1;

        // 망원경 말고 뒤질 곳을 전부 한 번 이상 뒤졌나
        internal virtual bool AllCleared()
        {
            foreach (FarmSpot s in Spots)
                if (!s.Telescope && ModHelper.GetAfterhourCounter(Id, "searched_" + s.Key, -1000) <= -1000) return false;
            return true;
        }

        internal int ClearedCount()
        {
            int n = 0;
            foreach (FarmSpot s in Spots)
                if (!s.Telescope && ModHelper.GetAfterhourCounter(Id, "searched_" + s.Key, -1000) > -1000) n++;
            return n;
        }

        internal int SearchSpotCount()
        {
            int n = 0;
            foreach (FarmSpot s in Spots) if (!s.Telescope) n++;
            return n;
        }

        internal void RefreshAll()
        {
            if (stage == null) return;
            foreach (FarmHotspot h in stage.GetComponentsInChildren<FarmHotspot>(true)) h.Refresh();
        }

        private GameObject stage;
        private float hsLabelOffset;
        private GameObject labelLayer;
        internal bool Busy;

        public override GameObject StageObject
        {
            get
            {
                if (stage == null) BuildStage();
                return stage;
            }
        }

        // ---- 무대: 카메라 화면을 덮는 판 + 물건 네모들 ----
        private void BuildStage()
        {
            stage = new GameObject("VME_Farm_" + Key);
            stage.SetActive(false);
            Camera cam = Camera.main;
            float h = cam != null && cam.orthographic ? cam.orthographicSize * 2f : 10.8f;
            float w = h * (cam != null ? cam.aspect : 16f / 9f);
            Vector3 c = cam != null ? cam.transform.position : Vector3.zero;
            stage.transform.position = new Vector3(c.x, c.y, 0f);

            var bg = new GameObject("Background").AddComponent<SpriteRenderer>();
            bg.transform.SetParent(stage.transform, false);
            bg.sortingOrder = BackgroundSortingOrder;
            // 배경 그림: 화면을 꽉 채우고(cover) 넘치는 쪽만 잘린다. 물건 위치는 그림 기준 좌표 → imgW/imgH 로 환산
            float imgW = w, imgH = h;
            Texture2D tex = LoadBackground();
            if (tex != null)
            {
                float s = Mathf.Max(w / tex.width, h / tex.height); // 월드 단위 / 픽셀
                bg.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f / s);
                imgW = tex.width * s;
                imgH = tex.height * s;
                BgColor = Color.white;
            }
            else
            {
                bg.sprite = SolidSprite(BgColor);
                bg.transform.localScale = new Vector3(w, h, 1f); // 1x1 유닛 스프라이트를 화면 크기로
            }

            // 지도 마커처럼: 작은 네모 + 아래 이름표 (1080 기준 네모 24px, 글자 20px)
            float px = h / 1080f;
            float sq = 24f * px, labelH = 30f * px, labelW = 220f * px;
            TMP_FontAsset font = FontLoader.Instance != null ? (FontLoader.Instance.notoSans_Normal ?? FontLoader.Instance.notoSans) : null;

            foreach (FarmSpot s in Spots)
            {
                var go = new GameObject("Spot_" + s.Key);
                go.transform.SetParent(stage.transform, false);
                // Area 의 가운데 = 네모 위치 (배경 그림 비율, 왼쪽 아래 기준)
                go.transform.localPosition = new Vector3((s.Area.center.x - 0.5f) * imgW, (s.Area.center.y - 0.5f) * imgH, 0f);

                var square = new GameObject("Square").AddComponent<SpriteRenderer>();
                square.transform.SetParent(go.transform, false);
                // 뒤질 곳 = 상자 아이콘 (Map/icon_box.png, 없으면 네모)
                Sprite icon = TransitMap.Icon("box");
                square.sprite = icon ?? SolidSprite(Color.white);
                square.sortingOrder = BackgroundSortingOrder + 1;
                float isz = icon != null ? IconSize * px : sq;
                square.transform.localScale = new Vector3(isz, isz, 1f);

                hsLabelOffset = -(sq * 0.5f + labelH * 0.5f + 2f * px); // 이름표: 네모 아래 (월드 단위)

                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(Mathf.Max(sq, labelW * 0.6f), sq + labelH);
                col.offset = new Vector2(0f, -labelH * 0.5f);
                var sr = square;
                var inter = go.AddComponent<Interactable>();
                inter.isControlable = false;
                var hs = go.AddComponent<FarmHotspot>();
                hs.owner = this;
                hs.spot = s;
                hs.sr = sr;
                go.AddComponent<TooltipContainer>();
                hs.Refresh();
            }
            BuildExtra(stage, imgW, imgH, px);
        }

        // 하위 장소(격자 수색 GridFarmLocation 등)가 무대에 더 깔 것. imgW/imgH = 배경 그림의 월드 크기, px = 1080 기준 1px 의 월드 길이
        protected virtual void BuildExtra(GameObject stage, float imgW, float imgH, float px) { }

        // 하위 장소가 UI 층(이름표 층, 모든 창 뒤)에 더 그릴 것. toLocal = 월드 좌표 → 이 층 로컬 좌표 (실패 null)
        protected virtual void BuildOverlay(RectTransform layer, Func<Vector3, Vector2?> toLocal) { }

        private const float IconSize = 30f; // 1080 기준 px

        private static Sprite solid;
        private static Sprite SolidSprite(Color _)
        {
            if (solid != null) return solid;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            solid = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f); // 1x1 유닛
            return solid;
        }

        public override void Arrive()
        {
            // 쓰레기장과 같은 규칙: 도착할 때 수색 시간을 새로 채운다. 단 하룻밤 한 번만 (싸돌아다니며 다시 채우기 방지)
            if (RefillsEnergy && Roaming.EnergyNotRefilledTonight())
            {
                ScavHelper.ResetScavenging();
                // 테스트용: 기력을 TestEnergy 로 채운다 (0 이면 끔 = 게임 기본 5)
                if (TestEnergy > 0) PlayerStore.Instance.scavengingAttempts = ScavHelper.GetMaxScavAttempts() - TestEnergy;
            }
            if (stage != null)
            {
                var bg = stage.transform.Find("Background")?.GetComponent<SpriteRenderer>();
                if (bg != null) bg.color = BgColor;
                foreach (FarmHotspot h in stage.GetComponentsInChildren<FarmHotspot>(true)) h.Refresh();
            }
            try { BuildLabels(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] labels: " + ex.Message); }
            try { RestoreFloor(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] restore floor: " + ex.Message); }
        }

        // 이름표는 게임 UI 층(외출 가방 창과 같은 부모)의 맨 뒤에 그린다.
        // 월드(장면)는 픽셀 퍼펙트 카메라로 저해상도 렌더 후 확대돼서, 월드에 그린 글자는 뭉개진다.
        private void BuildLabels()
        {
            if (labelLayer != null) UnityEngine.Object.Destroy(labelLayer);
            labelLayer = null;
            PixelWindow bag = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.afterhourWindow : null;
            Transform parent = bag != null && bag.handler != null ? bag.handler.transform.parent : null;
            if (parent == null || stage == null) return;
            Canvas root = parent.GetComponentInParent<Canvas>()?.rootCanvas;
            Camera uiCam = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            Camera cam = Camera.main;

            labelLayer = new GameObject("VME_FarmLabels", typeof(RectTransform));
            labelLayer.transform.SetParent(parent, false);
            labelLayer.transform.SetSiblingIndex(0); // 모든 창 뒤
            var lrt = (RectTransform)labelLayer.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;

            TMP_FontAsset font = FontLoader.Instance != null ? (FontLoader.Instance.notoSans_Normal ?? FontLoader.Instance.notoSans) : null;
            if (cam == null) return;
            // 월드 좌표 → 이 층의 로컬 좌표
            bool ToLocal(Vector3 world, out Vector2 local)
            {
                Vector2 screen;
                if (cam.targetTexture == null) screen = cam.WorldToScreenPoint(world);
                else
                {
                    Vector3 v = cam.WorldToViewportPoint(world);
                    screen = new Vector2(v.x * Screen.width, v.y * Screen.height);
                }
                return RectTransformUtility.ScreenPointToLocalPointInRectangle(lrt, screen, uiCam, out local);
            }
            Sprite boxIcon = TransitMap.Icon("box");
            foreach (FarmHotspot h in stage.GetComponentsInChildren<FarmHotspot>(true))
                if (h.sr != null) h.sr.enabled = true; // UI 아이콘 못 만들면 월드 아이콘이 대신 보이게

            foreach (FarmHotspot h in stage.GetComponentsInChildren<FarmHotspot>(true))
            {
                // 뒤질 곳 아이콘도 UI 층에 (월드에 그리면 픽셀 퍼펙트 카메라 때문에 거칠다)
                if (boxIcon != null && h.sr != null)
                {
                    float wsz = h.sr.transform.lossyScale.y; // 월드 아이콘 크기 그대로
                    Vector3 c0 = h.sr.transform.position;
                    if (ToLocal(c0, out Vector2 ic) && ToLocal(c0 + new Vector3(0f, wsz, 0f), out Vector2 itop))
                    {
                        float usz = Mathf.Abs(itop.y - ic.y);
                        var igo = new GameObject("Icon_" + h.spot.Key, typeof(RectTransform));
                        igo.transform.SetParent(lrt, false);
                        var img = igo.AddComponent<Image>();
                        img.sprite = boxIcon;
                        img.preserveAspect = true;
                        img.raycastTarget = false;
                        var irt = img.rectTransform;
                        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
                        irt.sizeDelta = new Vector2(usz, usz);
                        irt.localPosition = ic;
                        h.icon = img;
                        h.sr.enabled = false; // 클릭은 월드 콜라이더가 그대로 받는다
                    }
                }

                if (!ToLocal(h.transform.position + new Vector3(0f, hsLabelOffset, 0f), out Vector2 local)) continue;

                var lgo = new GameObject("Label_" + h.spot.Key, typeof(RectTransform));
                lgo.transform.SetParent(lrt, false);
                var label = lgo.AddComponent<TextMeshProUGUI>();
                if (font != null) label.font = font;
                label.fontSize = 7.5f;
                label.alignment = TextAlignmentOptions.Center;
                label.enableWordWrapping = false;
                label.raycastTarget = false;
                var rt = label.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(220f, 30f);
                rt.localPosition = local;
                ScriptFontSwap.Apply(label, h.spot.Name); // 지도와 같은 산세리프 / 한국어 폰트
                h.label = label;
                h.Refresh();
            }
            BuildOverlay(lrt, w => ToLocal(w, out Vector2 l) ? (Vector2?)l : null);
        }

        // ---- 바닥에 둔 물건: 이 장소에 그대로 남긴다 ----
        // 원래 게임은 돌아갈 때 바닥(외출 가방의 "바닥" 칸) 물건을 버린다.
        // Leave 는 바닥 비우기(TransferAfterhourToInv)보다 먼저 불리므로, 여기서 보관함으로 옮겨 두고 다음 도착 때 바닥에 다시 깐다.
        // 보관함은 메모리에만 있다 (게임을 끄면 사라짐).
        private GameInventory floorStash;
        private PixelWindow floorStashWindow;

        private GameInventory FloorStash()
        {
            if (floorStash == null)
            {
                (PixelWindow win, GameInventory inv) = DirectoryUtils.CreateInventoryWindow(30, 20, false);
                floorStash = inv;
                floorStashWindow = win;
            }
            return floorStash;
        }

        // ---- 세이브 DB (SaveDb.cs) ----
        internal string SaveFloor()
        {
            if (floorStash == null || floorStash.childItems.Count == 0) return null;
            return PlayerStore.Instance.EncodeSaveItem("save_bag", (PixelElement)floorStash, floorStashWindow);
        }

        internal void LoadFloor(string json)
        {
            ResetFloor();
            if (!string.IsNullOrEmpty(json)) PlayerStore.Instance.DecodeSaveItem(json, FloorStash());
        }

        internal void ResetFloor()
        {
            if (floorStash == null) return;
            foreach (GameItem i in new List<GameItem>(floorStash.childItems)) { floorStash.Expel(i); i.Destroy(); }
        }

        internal IEnumerable<FarmHotspot> Hotspots()
        {
            GameObject st = StageObject; // 아직 안 만들었으면 만든다 (비활성)
            return st != null ? st.GetComponentsInChildren<FarmHotspot>(true) : new FarmHotspot[0];
        }

        private void StashFloor()
        {
            GameGridInventory floor = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.afterhourInventory : null;
            if (floor == null) return;
            int n = 0;
            foreach (GameItem item in new List<GameItem>(floor.childItems))
            {
                item.CloseContentWindow(includeChildren: true);
                floor.Expel(item);
                if (GraphUtils.TryAcceptAll(FloorStash(), item) == 0) n++;
                else GraphUtils.TryAcceptAll(floor, item); // 보관 실패하면 원래대로 (게임 규칙대로 버려짐)
            }
            if (n > 0) Debug.Log("[MapExtension] " + Id + ": left " + n + " item(s) on the floor");
        }

        private void RestoreFloor()
        {
            if (floorStash == null || floorStash.childItems.Count == 0) return;
            GameGridInventory floor = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.afterhourInventory : null;
            if (floor == null) return;
            foreach (GameItem item in new List<GameItem>(floorStash.childItems))
            {
                floorStash.Expel(item);
                if (GraphUtils.TryAcceptAll(floor, item) != 0) GraphUtils.TryAcceptAll(floorStash, item); // 바닥이 꽉 차면 보관함에 남김
            }
        }

        public override string GetLeaveWarning() => "바닥에 둔 물건은 " + Name + "에 그대로 남겨 둔다. 다음에 오면 그대로 있다.";

        public override void Leave()
        {
            try { StashFloor(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] stash floor: " + ex.Message); }
            if (labelLayer != null) UnityEngine.Object.Destroy(labelLayer);
            labelLayer = null;
            if (stage == null) return;
            foreach (FarmHotspot h in stage.GetComponentsInChildren<FarmHotspot>(true)) h.Cleanup();
        }

        // 패널: 잭슨 박사네처럼 게임의 "돌아가기" 패널만 (설명/기력 창 없음)
        public override GameObject PanelObject => MapUIManager.Instance != null ? MapUIManager.Instance.UIUpgradeMerchantPanel : null;

        // 기력/체력 바는 OutingHud (화면 왼쪽 아래, 외출 중 어디서나)
        internal void RefreshPanel() { }


        // ---- 고갈 상태 (세이브에 저장) ----
        internal int Today => StoreStation.Instance != null ? StoreStation.Instance.dayCounter : 0;

        // ---- 남은 물건 (%): 한 번도 안 뒤졌으면 100%. 뒤지면 0% → QuietDays 동안 그대로 → 하루 RegrowPerDay% 씩, 최대 RegrowCap%.
        //      다시 차는 건 잡동사니/싼 물건뿐 (RollRegrowth). 뒤질 수 있는 최소 = MinSearchable%.
        internal const int QuietDays = 7, RegrowPerDay = 4, RegrowCap = 40, MinSearchable = 5;

        internal int LastSearched(FarmSpot s) => ModHelper.GetAfterhourCounter(Id, "searched_" + s.Key, -1000);

        internal bool EverSearched(FarmSpot s) => LastSearched(s) > -1000;

        internal int FillPercent(FarmSpot s)
        {
            int last = LastSearched(s);
            if (last <= -1000) return 100;
            int d = Today - last;
            if (d <= QuietDays) return 0;
            return Mathf.Min(RegrowCap, (d - QuietDays) * RegrowPerDay);
        }

        // 뒤질 수 있을 만큼(MinSearchable%) 찰 때까지 남은 날 (0 = 지금 뒤질 수 있음)
        internal int DaysUntilRefill(FarmSpot s)
        {
            if (FillPercent(s) >= MinSearchable) return 0;
            int last = LastSearched(s);
            int need = QuietDays + Mathf.CeilToInt(MinSearchable / (float)RegrowPerDay);
            int left = last + need - Today;
            return left > 0 ? left : 1;
        }

        internal void MarkSearched(FarmSpot s) => ModHelper.SetAfterhourCounter(Id, "searched_" + s.Key, Today);

        // NeedsAny 중 외출 가방에 있는 첫 번째 아이템
        private GameItem FindTool(FarmSpot s)
        {
            if (s.NeedsAny == null) return null;
            foreach (string id in s.NeedsAny)
            {
                GameItem g = null;
                try { g = EmporiumEntry.Instance.GetAfterhourItemById(id); } catch { }
                if (g != null) return g;
            }
            return null;
        }

        internal string ToolName(FarmSpot s)
        {
            GameItem g = FindTool(s);
            if (g == null) return null;
            try { return g.GetDisplayName(); } catch { return g.identifier; }
        }

        // 일회용 도구 1개 쓰기 (여러 개 겹친 거면 개수만 줄임)
        internal void ConsumeTool(FarmSpot s)
        {
            GameItem g = FindTool(s);
            if (g == null) return;
            try
            {
                string name = ToolName(s);
                if (g.unitCount > 1) g.SetUnitCount(g.unitCount - 1);
                else
                {
                    g.parentInventory?.Expel(g);
                    g.Destroy();
                }
                try { StoreUIManager.Instance.Notify(name + " 1개를 썼다."); } catch { }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] consume tool: " + ex.Message);
            }
        }

        internal bool HasTool(FarmSpot s)
        {
            if (s.NeedsAny == null || s.NeedsAny.Length == 0) return true;
            foreach (string id in s.NeedsAny)
                if (EmporiumEntry.Instance.IsAfterhourHaveOwnedItems(id)) return true;
            return false;
        }
    }

    // 화면 속 뒤질 수 있는 물건
    internal class FarmHotspot : TooltipInterface, IClickable
    {
        public FarmLocation owner;
        public FarmSpot spot;
        public SpriteRenderer sr;
        public TMP_Text label;
        public Image icon; // UI 층 아이콘 (있으면 월드 sr 대신)

        private static readonly Color Ready = new Color32(0x4d, 0xf0, 0xe0, 0xff);   // 지도 마커와 같은 청록
        private static readonly Color Empty = new Color32(0x3a, 0x38, 0x46, 0xff);   // 이미 뒤짐
        private static readonly Color Locked = new Color32(0xff, 0x9f, 0x43, 0xff);  // 도구 필요

        internal void Refresh()
        {
            if (sr == null || owner == null) return;
            if (spot.Telescope)
            {
                bool show = owner.AllCleared();
                if (gameObject.activeSelf != show) gameObject.SetActive(show);
                if (icon != null) icon.gameObject.SetActive(show);
                if (label != null) label.gameObject.SetActive(show);
                bool more = TelescopeTargetsLeft() > 0;
                sr.color = more ? Locked : Empty;
                if (icon != null) icon.color = sr.color;
                if (label != null) label.color = more ? Color.white : new Color32(0x80, 0x80, 0x88, 0xff);
                return;
            }
            bool empty = owner.DaysUntilRefill(spot) > 0;
            sr.color = empty ? Empty : (owner.HasTool(spot) ? Ready : Locked);
            if (icon != null) icon.color = sr.color;
            if (label != null) label.color = empty ? new Color32(0x80, 0x80, 0x88, 0xff) : Color.white;
        }

        public override bool IsTooltipEnabled() => owner != null;

        private int TelescopeTargetsLeft()
        {
            int n = 0;
            if (spot.Unlocks != null) foreach (string k in spot.Unlocks) if (!FarmLocation.IsUnlocked(k)) n++;
            return n;
        }

        private static string LocName(string key)
        {
            foreach (FarmLocation l in FarmLocations.All) if (l.Key == key) return l.Name;
            return key;
        }

        public override string GetTooltip()
        {
            if (spot.Telescope)
                return TelescopeTargetsLeft() > 0
                    ? spot.Name + " (기력 " + spot.Cost + ")\n<color=#ffcf6b>근처에 들어갈 만한 곳이 보일 것 같다.</color>"
                    : spot.Name + "\n<color=#9a9aa8>이미 다 살펴봤다. 보이는 곳은 지도에 표시해 뒀다.</color>";
            int left = owner.DaysUntilRefill(spot);
            int fill = owner.FillPercent(spot);
            string s = spot.Name + " (기력 " + spot.Cost + ")\n" + FillText(fill);
            if (left > 0) return s + "\n<color=#9a9aa8>텅 비었다. " + left + "일은 지나야 뭔가 조금 쌓인다. (클릭: 상자 열기)</color>";
            if (owner.EverSearched(spot)) s += "\n<color=#9a9aa8>이미 털린 곳. 잡동사니나 싼 물건만 조금 쌓였다.</color>";
            if (!owner.HasTool(spot)) return s + "\n<color=#ff8a6b>" + spot.NeedsText + "</color>";
            return s;
        }

        // "남은 물건 약 N%" (색: 많음 청록 / 조금 노랑 / 없음 회색)
        internal static string FillText(int fill)
        {
            string c = fill >= 60 ? "#4df0e0" : fill >= FarmLocation.MinSearchable ? "#ffcf6b" : "#80808a";
            return "<color=" + c + ">남은 물건 약 " + fill + "%</color>";
        }

        public void OnClick()
        {
            if (owner == null || owner.Busy) return;
            Refresh();
            if (spot.Telescope) { UseTelescope(); return; }
            // 이번 외출에서 이미 연 상자에 물건이 남아 있으면 다시 연다
            // 이미 뒤진 곳: 다시 채워지기 전이면 그 상자를 다시 연다 (남긴 물건 그대로, 없으면 빈 상자)
            // 이미 뒤진 곳은 테스트 모드여도 수색 창 없이 상자만 연다
            const bool testRefill = false;
            if (owner.DaysUntilRefill(spot) > 0 && !testRefill)
            {
                if (box == null) box = CreateSearchBox(spot.Name); // 게임을 다시 켠 뒤 등: 빈 상자
                OpenBox();
                return;
            }
            // 다시 채워졌으면 지난 상자는 버리고 새로 뒤진다
            DiscardBox();
            int left = owner.DaysUntilRefill(spot);
            if (left > 0 && !testRefill) { Notify("텅 비었다. " + left + "일은 지나야 뭔가 쌓인다."); return; }
            if (!owner.HasTool(spot)) { Notify(spot.NeedsText); return; }
            if (PlayerStore.Instance.healthData.isWoundedFresh) { Notify(LocHelper.GetLocalizedMechanic("afterhour_attempts_wounded")); return; }

            int now = ScavHelper.GetScavTimeLeft();
            if (now < spot.Cost) { Notify("기력이 부족하다. (필요 " + spot.Cost + ", 남은 기력 " + now + ")"); return; }
            int after = now - spot.Cost;
            try { EmporiumAudioPlayer.Instance.audioButton1.Play(); } catch { }
            string body = "<b>수색</b>\n\n" + spot.Desc + "\n" + FillText(owner.FillPercent(spot))
                          + "\n남은 기력: " + now + " → 수색 후: <color=#ff5a5a>" + after + "</color>";
            string tool = spot.ConsumesTool ? owner.ToolName(spot) : null;
            if (tool != null) body += "\n<color=#ffcf6b>" + tool + " 1개를 쓴다.</color>";
            if (spot.MinorWound + spot.MajorWound > 0f)
                body += "\n<color=#9a9aa8>부상 위험: 경상 " + spot.MinorWound.ToString("0.#") + "% / 중상 " + spot.MajorWound.ToString("0.#") + "%</color>";
            Dialogs.Confirm(body, "수색", "취소", () => StartCoroutine(Search()));
        }

        // 망원경: 기력을 쓰고 연결된 장소를 해금
        private void UseTelescope()
        {
            if (!owner.AllCleared()) return;
            var targets = new List<string>();
            if (spot.Unlocks != null) foreach (string k in spot.Unlocks) if (!FarmLocation.IsUnlocked(k)) targets.Add(k);
            if (targets.Count == 0) { Notify("이미 다 살펴봤다."); return; }
            int now = ScavHelper.GetScavTimeLeft();
            if (now < spot.Cost) { Notify("기력이 부족하다. (필요 " + spot.Cost + ", 남은 기력 " + now + ")"); return; }
            try { EmporiumAudioPlayer.Instance.audioButton1.Play(); } catch { }
            string body = "<b>망원경</b>\n\n" + spot.Desc + "\n남은 기력: " + now + " → <color=#ff5a5a>" + (now - spot.Cost) + "</color>";
            Dialogs.Confirm(body, "살펴보기", "취소", () =>
            {
                PlayerStore.Instance.scavengingAttempts += spot.Cost;
                var names = new List<string>();
                foreach (string k in targets) { FarmLocation.SetUnlocked(k); names.Add(LocName(k)); }
                Notify("새 장소를 찾았다: " + string.Join(", ", names) + " (외출 지도에 표시됨)");
                TryPlay("click_vintage");
                owner.RefreshAll();
            });
        }

        private IEnumerator Search()
        {
            owner.Busy = true;
            Notify(spot.Name + " 수색 중…");
            try { AudioManager.Instance.Play2("debris"); } catch { }
            yield return new WaitForSeconds(1.2f);
            try
            {
                Resolve();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] search: " + ex);
            }
            owner.Busy = false;
            owner.RefreshAll(); // 다 뒤졌으면 망원경이 나타난다
            if (owner.AllCleared() && owner.Spots.Exists(x => x.Telescope))
                Notify(owner.Name + "을(를) 다 뒤졌다. 망원경으로 주변을 살펴볼 수 있다.");
            owner.RefreshPanel();
        }

        private void Resolve()
        {
            PlayerStore ps = PlayerStore.Instance;
            ps.scavengingAttempts += spot.Cost;
            int fill = owner.FillPercent(spot);          // 뒤지기 전 남은 양
            bool regrowth = owner.EverSearched(spot);    // 이미 털린 곳 → 다시 쌓인 잡동사니만
            owner.MarkSearched(spot);
            if (spot.ConsumesTool) owner.ConsumeTool(spot);

            if (spot.MinorWound > 0f && RNG.Roll(spot.MinorWound))
            {
                if (HealthData.RollLuck()) Notify(LocHelper.GetLocalizedMechanic("mech_scav_luck_saved"));
                else { HealthData.ReceiveMinorWound(); TryPlay("debris_impact2"); return; }
            }
            else if (spot.MajorWound > 0f && RNG.Roll(spot.MajorWound))
            {
                if (HealthData.RollLuck()) Notify(LocHelper.GetLocalizedMechanic("mech_scav_luck_saved"));
                else { HealthData.ReceiveMajorWound(); TryPlay("debris_impact"); return; }
            }

            // 다시 쌓인 곳: 빈손 확률이 높고(남은 양이 적을수록), 1~2개, 잡동사니/싼 물건만
            float emptyChance = regrowth ? Mathf.Min(90f, spot.Empty + (RegrowCapF - fill) * 1.2f) : spot.Empty;
            if ((emptyChance > 0f && RNG.Roll(emptyChance)) || HealthData.RollBadLuck())
            {
                Notify("아무것도 없었다.");
                return;
            }

            int n = regrowth ? (fill >= 25 && RNG.Roll(50f) ? 2 : 1) : UnityEngine.Random.Range(spot.MinItems, spot.MaxItems + 1);
            var found = new List<GameItem>();
            for (int i = 0; i < n; i++)
            {
                GameItem item = regrowth ? RollRegrowth() : RollOne();
                if (item != null) found.Add(item);
            }
            if (found.Count == 0)
            {
                Notify("아무것도 없었다.");
                return;
            }
            // 잠금 없는 수색용 상자(CreateSearchBox)에 담아 그 인벤토리 창을 띄운다.
            // 원하는 것만 끌어서 가방에 넣고, 나머지는 남겨 둔다 (이번 외출 동안은 다시 클릭하면 다시 열림).
            try
            {
                box = CreateSearchBox(spot.Name);
                // 상자 안 인벤토리에 직접 넣는다. 안 들어간 물건(반환값 = 남은 개수 > 0)만 외출 가방으로 + 로그
                int inBox = 0, lost = 0;
                foreach (GameItem item in found)
                {
                    if (boxInv != null && GraphUtils.TryAcceptAll(boxInv, item) == 0) inBox++;
                    else { lost++; GraphUtils.TryAcceptAll(EmporiumEntry.Instance.afterhourInventory, item); }
                }
                Debug.Log("[MapExtension] " + spot.Key + ": rolled " + found.Count + ", in box " + inBox + ", to bag " + lost);
                OpenBox();
                foreach (GameItem item in found) LootCover.Attach(item);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] loot box: " + ex.Message);
                foreach (GameItem item in found) GraphUtils.TryAcceptAll(EmporiumEntry.Instance.afterhourInventory, item);
            }
        }

        // ---- 이번 외출에서 연 상자 ----
        private GameItem box;

        // 잠금 없는 수색용 상자: 게임이 상자를 만드는 함수(CreateInventoryWindow + SetContentWindow) 그대로, 그림은 증거물 상자
        private GameInventory boxInv; // 상자 안 인벤토리 (물건은 여기에 직접 넣는다)

        private GameItem CreateSearchBox(string title)
        {
            (PixelWindow win, GameInventory inv) = DirectoryUtils.CreateInventoryWindow(8, 8); // 한 번에 10개 넘게 나올 수 있어서 넉넉히
            try { win.SetTitle(title); } catch { } // 창 제목 = 뒤진 곳 이름 (예: 조제실 카운터)
            GameItem b = ItemDirectory.CreateEmptyItem("vme_search_box").SetContentWindow(win).SetSpriteAndShape("Items/items_container", "evidence_box");
            b.name = title;
            // 주운 물건은 "내 소유"라 소유 표시 없는 상자에는 못 들어간다(GeneralHelper.MayOwnershipAcceptItem) → 상자도 내 소유로
            b.EnableTag("IS_OWNED_TAG");
            boxInv = inv;
            return b;
        }

        private bool BoxHasItems()
        {
            try { return box != null && boxInv != null && boxInv.childItems.Count > 0; }
            catch { return false; }
        }

        private void OpenBox()
        {
            if (box == null || box.contentWindow == null) return;
            box.contentWindow.Show();
            box.contentWindow.Center();
            LootCover.EnsureCovers(boxInv != null ? boxInv.childItems : null); // 불러온 상자: 안 깐 물건에 빗금 다시
            try { AudioManager.Instance.Play2("click_vintage"); } catch { }
        }

        // 장소를 떠날 때: 남긴 물건은 사라진다. 아직 안 깐 "?" 덮개도 정리.
        // 장소를 떠날 때: 상자 창만 닫는다. 남긴 물건은 다시 채워질 때까지 상자에 그대로 (게임을 끄면 사라짐).
        // 상자 밖으로 가져간 아이템의 "?" 덮개는 정리, 상자 안에 남은 건 덮개 유지.
        internal void Cleanup()
        {
            try { box?.contentWindow?.Hide(); } catch { }
            LootCover.RemoveOutside(BoxItems());
        }

        private void DiscardBox()
        {
            if (box == null) return;
            try { box.contentWindow?.Hide(); box.Destroy(); } catch { }
            box = null;
            boxInv = null;
        }

        // ---- 세이브 DB (SaveDb.cs) ----
        internal string SaveBox()
        {
            if (box == null || boxInv == null || boxInv.childItems.Count == 0) return null;
            return PlayerStore.Instance.EncodeSaveItem("save_bag", (PixelElement)boxInv, box.contentWindow);
        }

        internal void LoadBox(string json)
        {
            ResetBox();
            if (string.IsNullOrEmpty(json)) return;
            box = CreateSearchBox(spot.Name);
            PlayerStore.Instance.DecodeSaveItem(json, boxInv);
        }

        internal void ResetBox()
        {
            if (box == null) return;
            try
            {
                if (boxInv != null)
                    foreach (GameItem i in new List<GameItem>(boxInv.childItems)) { boxInv.Expel(i); i.Destroy(); }
            }
            catch
            {
            }
            DiscardBox();
        }

        internal HashSet<GameItem> BoxItems()
        {
            var set = new HashSet<GameItem>();
            try
            {
                if (box != null && boxInv != null)
                    foreach (GameItem i in boxInv.childItems) set.Add(i);
            }
            catch
            {
            }
            return set;
        }

        // 한 개 굴리기: Items(가중치) 또는 게임 전리품 표
        private const float RegrowCapF = FarmLocation.RegrowCap;

        // 다시 쌓인 물건: 잡동사니 표 위주 + 생활용품 조금
        private static GameItem RollRegrowth()
        {
            return ItemSpawner.SpawnFromTable(RNG.Roll(75f) ? "junkTable" : "householdTable");
        }

        private GameItem RollOne() => RollSpot(spot);

        // 그 수색 지점 정의대로 한 개 굴리기 (격자 수색 칸도 같이 씀)
        internal static GameItem RollSpot(FarmSpot spot)
        {
            bool hasTables = spot.Tables != null && spot.Tables.Length > 0;
            if (spot.Items != null && spot.Items.Length > 0 && (!hasTables || RNG.Roll(spot.ItemChance)))
            {
                int total = 0;
                foreach (string e in spot.Items) total += Weight(e);
                int r = UnityEngine.Random.Range(0, Mathf.Max(total, 1));
                foreach (string e in spot.Items)
                {
                    r -= Weight(e);
                    if (r < 0) return ItemSpawner.Spawn(e.Split(':')[0]);
                }
            }
            if (!hasTables) return null;
            return ItemSpawner.SpawnFromTable(spot.Tables[UnityEngine.Random.Range(0, spot.Tables.Length)]);
        }

        private static int Weight(string entry)
        {
            int i = entry.IndexOf(':');
            return i > 0 && int.TryParse(entry.Substring(i + 1), out int w) ? Mathf.Max(w, 0) : 1;
        }

        private static void TryPlay(string sfx)
        {
            try { AudioManager.Instance.Play2(sfx); } catch { }
        }

        private static void Notify(string text)
        {
            try { StoreUIManager.Instance.Notify(text); } catch { Debug.Log("[MapExtension] " + text); }
        }
    }

    internal static class FarmLocations
    {
        internal static readonly List<FarmLocation> All = new List<FarmLocation>
        {
            // 예전 약국 (클릭 수색). 격자 수색 테스트 동안 주석 처리 → GridFarmData.Pharmacy() 로 교체
            // new FarmLocation
            // {
            //     Key = "pharmacy", Name = "버려진 약국", BackgroundFile = "bg_farm_pharmacy.png",
            //     Desc = "셔터가 반쯤 뜯겨 나간 동네 약국. 쓸 만한 건 다 털렸다고들 하지만, 정말 그럴까.",
            //     Spots = new List<FarmSpot>
            //     {
            //         new FarmSpot { Key = "cabinet", Name = "약장", Area = new Rect(0.18f, 0.60f, 0f, 0f), Cost = 2,
            //             Desc = "유리문이 깨진 약장. 선반 안쪽에 뭔가 남아 있을지도.", Tables = new[] { "medicalTable" }, MinItems = 1, MaxItems = 3, MinorWound = 3f, Empty = 15f,
            //             Items = new[] { "bandage_item:30", "topical_bandage_item:20", "hemostatic_bandage_item:10", "rubbing_alcohol:20", "salve:12", "smelling_salt:8", "phagimycin_pill:6", "pink_injector:3" }, ItemChance = 75f },
            //         new FarmSpot { Key = "counter", Name = "조제실 카운터", Area = new Rect(0.53f, 0.57f, 0f, 0f), Cost = 1,
            //             Desc = "안쪽 조제실 카운터. 서랍이 반쯤 열려 있다.", Tables = new[] { "householdTable", "junkTable" }, MinItems = 1, MaxItems = 2, Empty = 25f,
            //             Items = new[] { "caffeine_pill:25", "phagimycin_pill:15", "unlicensed_phagimycin_pill:8", "birth_control_pill:12", "bandage_item:15", "chem_scanner:2", "surgery_tool:3" }, ItemChance = 55f },
            //         new FarmSpot { Key = "shelf", Name = "벽면 선반", Area = new Rect(0.78f, 0.66f, 0f, 0f), Cost = 2,
            //             Desc = "천장까지 닿는 벽면 선반. 위쪽 칸은 기울어져 있어 잘못 건드리면 쏟아질 것 같다.", Tables = new[] { "packedFoodTable", "materialTable", "medicalTable" }, MinItems = 1, MaxItems = 3, MinorWound = 10f, MajorWound = 2f, Empty = 15f,
            //             Items = new[] { "bandage_item:20", "topical_bandage_item:15", "rubbing_alcohol:20", "salve:10", "blood_bag:4" }, ItemChance = 40f },
            //         new FarmSpot { Key = "trash", Name = "진열대", Area = new Rect(0.38f, 0.50f, 0f, 0f), Cost = 1,
            //             Desc = "가운데 진열대. 남은 건 대부분 빈 상자지만 가끔 뭔가 굴러다닌다.", Tables = new[] { "junkTable" }, MinorWound = 2f, Empty = 30f, RefillDays = 1 },
            //         new FarmSpot { Key = "safe", Name = "지하실 문", Area = new Rect(0.33f, 0.72f, 0f, 0f), Cost = 3,
            //             Desc = "지하실 표지판 아래 잠긴 문. 약사가 귀한 물건을 숨겨 뒀다는 소문이 있다.", Tables = new[] { "medicalTable", "toolTable", "allModuleTable" }, MinItems = 2, MaxItems = 3, MinorWound = 3f, Empty = 5f,
            //             NeedsAny = new[] { "med_keycard", "cmd_keycard" }, ConsumesTool = true, NeedsText = "의무부 키카드 잠금장치다. 의무부 키카드(또는 사령부 키카드)가 외출 가방에 있어야 한다.", RefillDays = 7 },
            //     },
            // },
            GridFarmData.Pharmacy(),
        };

        private static bool dataAdded;

        internal static void Register()
        {
            // 새 수색처 19곳 + 해금 연결이 있는 장소에 망원경 (한 번만)
            if (!dataAdded)
            {
                dataAdded = true;
                foreach (FarmLocation l in FarmLocationData.Create())
                    if (!All.Exists(x => x.Key == l.Key)) All.Add(l);
                foreach (FarmLocation l in All)
                {
                    if (l is BranchLocation) continue;
                    FarmLocationData.ApplyEarlyBalance(l); // 초반 수색처: 기력 합 10, 아이템 많이
                    if (FarmLocationData.Chain.ContainsKey(l.Key) && !l.Spots.Exists(s => s.Telescope))
                        l.Spots.Add(FarmLocationData.Telescope(l.Key));
                }
            }
            // 분점도 같은 구조의 장소 (세이브 DB 바닥 저장도 같이 하도록 목록에 넣는다)
            foreach (BranchLocation b in Branches.Locations)
                if (!All.Contains(b)) All.Add(b);
            foreach (FarmLocation loc in All)
            {
                try { ModHelper.AddAfterhourLocation(loc); }
                catch (Exception ex) { Debug.LogWarning("[MapExtension] register " + loc.Id + ": " + ex.Message); }
            }
        }
    }
}
