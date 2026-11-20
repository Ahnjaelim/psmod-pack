using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapExtension
{
    // 은퇴한 상인 8명의 집을 외출 장소로 등록한다.
    //  - 그 상인이 가게에 오는 요일(날짜 % 7)에만, 그리고 해금(시작 직업 / 네트워크 업그레이드)돼 있을 때만 갈 수 있다.
    //  - 화면은 잭슨 박사와 똑같다(무대, 패널, 카운터). 손님만 그 상인으로 바꾸고, 카운터(판매 재고)는 비워 둔다.
    internal sealed class RetiredMerchantLocation : AfterhourLocation
    {
        public string Key;          // "gunsmith" 등
        public string NameKey;      // 게임 손님 이름 키 (name_retired_gunsmith)
        public string FallbackName; // 키가 없을 때
        public string Desc;         // 지도 툴팁 설명
        public int Weekday;         // 날짜 % 7 (0=일 … 6=토)
        public Func<bool> Unlocked;
        public Func<string> Sprite; // 초상화 이름 (게임의 그 상인 초상화 함수)

        private string cachedSprite;

        public override string Id => "vme_retired_" + Key;
        public override string Source => MapExtensionMod.ModId;

        public override GameObject StageObject => MapUIManager.Instance != null ? MapUIManager.Instance.upgradeStoreScene : null;
        public override GameObject PanelObject => MapUIManager.Instance != null ? MapUIManager.Instance.UIUpgradeMerchantPanel : null;
        public override bool ShowsStoreCounters => true;

        public override string GetTitle()
        {
            try
            {
                string n = LocHelper.GetLocalizedClientName(NameKey);
                if (!string.IsNullOrEmpty(n) && n != NameKey) return n;
            }
            catch
            {
            }
            return FallbackName;
        }

        public override bool IsDiscovered() => true;

        internal bool IsUnlocked()
        {
            try { return Unlocked == null || Unlocked(); } catch { return false; }
        }

        internal static int Today => StoreStation.Instance != null ? StoreStation.Instance.dayCounter : 0;

        internal bool VisitsToday() => Today > 0 && Today % 7 == Weekday;

        public override bool IsAvailable() => IsUnlocked() && VisitsToday() && !IsClosedByEvent();

        public override Vector2? GetFrontCounterPosition(int w, int h) => new Vector2((float)(w / 100) * 78.5f, -(h / 100 * 10));

        public override Vector2? GetBackCounterPosition(int w, int h) => new Vector2((float)(w / 2) - (float)(w / 100) * 18f, 0f - (float)(h / 100) * 55f);

        public override string GetLeaveWarning() => LocHelper.GetLocalizedMechanic("afterhour_jackson_warning");

        // 잭슨 박사가 한 번이라도 방문됐으면 무대에 업그레이드 태블릿이 붙어 있다 → 상인 집에서는 숨김
        private void SetJacksonTablet(bool on)
        {
            GameObject stage = StageObject;
            Transform t = stage != null ? stage.transform.Find("DrJacksonTablet") : null;
            if (t != null) t.gameObject.SetActive(on);
        }

        // ---- 배경 교체: 잭슨 박사 방의 가장 큰 그림을 이 상인 배경으로 바꾸고, 나머지 장식은 숨긴다 ----
        public string BackgroundFile; // Map/ 아래 PNG (없으면 잭슨 박사 방 그대로)
        private Sprite bgSprite;
        private SpriteRenderer swappedRenderer;
        private Sprite originalSprite;
        private readonly List<SpriteRenderer> hidden = new List<SpriteRenderer>();

        private Sprite LoadBackground(Sprite reference)
        {
            if (bgSprite != null || string.IsNullOrEmpty(BackgroundFile) || reference == null) return bgSprite;
            byte[] bytes = ModHelper.ReadModFileBytes(MapExtensionMod.ModId, "Map/" + BackgroundFile);
            if (bytes == null || bytes.Length == 0) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) return null;
            tex.filterMode = FilterMode.Point; // 픽셀 아트
            tex.wrapMode = TextureWrapMode.Clamp;
            // 원래 배경과 같은 월드 크기·피벗이 되도록 pixelsPerUnit / pivot 을 맞춘다
            Vector2 size = reference.bounds.size;
            float ppu = size.x > 0f ? tex.width / size.x : 100f;
            Vector2 pivot = new Vector2(reference.pivot.x / reference.rect.width, reference.pivot.y / reference.rect.height);
            bgSprite = UnityEngine.Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), pivot, ppu);
            bgSprite.name = BackgroundFile;
            return bgSprite;
        }

        private void SwapBackground()
        {
            if (string.IsNullOrEmpty(BackgroundFile)) return;
            GameObject stage = StageObject;
            if (stage == null) return;
            SpriteRenderer biggest = null;
            float best = 0f;
            var all = stage.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer r in all)
            {
                if (r == null || r.sprite == null) continue;
                Vector2 s = r.sprite.bounds.size * (Vector2)r.transform.lossyScale;
                float area = Mathf.Abs(s.x * s.y);
                if (area > best) { best = area; biggest = r; }
            }
            Debug.Log("[MapExtension] " + Id + " stage renderers: " + all.Length + ", background = " + (biggest != null ? biggest.name + " / " + biggest.sprite.name : "none"));
            if (biggest == null) return;
            Sprite bg = LoadBackground(biggest.sprite);
            if (bg == null) return;
            swappedRenderer = biggest;
            originalSprite = biggest.sprite;
            biggest.sprite = bg;
            // 잭슨 박사는 배경 그림(drjackson2)에 같이 그려져 있어서, 배경을 바꾸면 사람이 없다 → 상인 초상화를 따로 세운다
            ShowPerson(biggest);
            hidden.Clear();
            foreach (SpriteRenderer r in all)
                if (r != null && r != biggest && r.enabled)
                {
                    r.enabled = false;
                    hidden.Add(r);
                }
        }

        // 배경 그림 기준 상인이 설 자리 (0~1, 왼쪽 아래 기준): 카운터(회색 칸) 윗변 가운데
        public float PersonX = 0.476f, PersonFootY = 0.36f, PersonHeight = 0.42f;
        private GameObject person;

        private void ShowPerson(SpriteRenderer bgRenderer)
        {
            HidePerson();
            EnsureSpriteName();
            Sprite face = null;
            try { face = SpriteDict.Instance != null ? SpriteDict.Instance.GetSprite(cachedSprite) : null; } catch { }
            if (face == null) return;

            person = new GameObject("VME_Merchant");
            person.transform.SetParent(bgRenderer.transform.parent, false);
            var sr = person.AddComponent<SpriteRenderer>();
            sr.sprite = face;
            sr.sortingLayerID = bgRenderer.sortingLayerID;
            sr.sortingOrder = bgRenderer.sortingOrder + 1;

            // 원래 가게에서 손님이 서는 자리·크기 그대로: 가게 손님 오브젝트를 잠깐 도착 상태로 놓고 위치/크기를 읽은 뒤 다시 숨긴다
            SpriteRenderer store = null;
            try
            {
                StoreClientMono mono = StoreUIManager.Instance.clientGameObject;
                mono.image.sprite = face;
                mono.SetImmediatlyArrive();
                store = mono.image;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] read client spot: " + ex.Message);
            }
            if (store != null)
            {
                person.transform.position = new Vector3(store.transform.position.x, store.transform.position.y, bgRenderer.transform.position.z);
                person.transform.rotation = store.transform.rotation;
                person.transform.localScale = Vector3.one;
                Vector3 pl = person.transform.lossyScale, want = store.transform.lossyScale;
                person.transform.localScale = new Vector3(want.x / Mathf.Max(Mathf.Abs(pl.x), 1e-4f), want.y / Mathf.Max(Mathf.Abs(pl.y), 1e-4f), 1f);
                sr.flipX = store.flipX;
                sr.color = Color.white;
            }
            else
            {
                // 못 읽으면 배경 비율로 대충: 카운터 윗변 가운데
                Bounds bg = bgRenderer.bounds;
                Vector2 raw = face.bounds.size;
                float scale = raw.y > 0f ? bg.size.y * PersonHeight / raw.y : 1f;
                person.transform.localScale = new Vector3(scale, scale, 1f);
                Bounds b = sr.bounds;
                Vector3 target = new Vector3(bg.min.x + bg.size.x * PersonX, bg.min.y + bg.size.y * PersonFootY, bgRenderer.transform.position.z);
                person.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, 0f);
            }
            try { StoreUIManager.Instance.HideClient(); } catch { }

            // 숨쉬기: 가게 손님과 같은 IdleAnimator(위아래로 살짝 흔들림), 세기·속도도 가게 손님 값 그대로
            try
            {
                IdleAnimator src = store != null ? store.GetComponentInParent<IdleAnimator>() : null;
                if (src == null && StoreUIManager.Instance.clientGameObject != null)
                    src = StoreUIManager.Instance.clientGameObject.GetComponent<IdleAnimator>();
                var idle = person.AddComponent<IdleAnimator>(); // 위치를 잡은 뒤에 붙여야 기준 위치가 맞다
                if (src != null)
                {
                    // 흔들림은 부모 기준 로컬 단위라, 부모 크기 차이만큼 보정해서 화면상 움직임을 같게
                    float srcParent = src.transform.parent != null ? Mathf.Abs(src.transform.parent.lossyScale.y) : 1f;
                    float myParent = person.transform.parent != null ? Mathf.Abs(person.transform.parent.lossyScale.y) : 1f;
                    idle.amplitude = src.amplitude * srcParent / Mathf.Max(myParent, 1e-4f);
                    idle.frequency = src.frequency;
                    idle.axis = src.axis;
                    idle.useUnscaledTime = src.useUnscaledTime;
                }
                idle.Play();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] idle: " + ex.Message);
            }

            // 클릭하면 대사 (잭슨 박사 태블릿과 같은 방식: 콜라이더 + Interactable + IClickable)
            try
            {
                var col = person.AddComponent<BoxCollider2D>();
                col.size = face.bounds.size;
                col.offset = face.bounds.center;
                var inter = person.AddComponent<Interactable>();
                inter.isControlable = false;
                var talk = person.AddComponent<MerchantPerson>();
                talk.owner = this;
                person.AddComponent<TooltipContainer>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] merchant click: " + ex.Message);
            }
        }

        // ---- 대사 ----
        public string[] Lines;
        private int lastLine = -1;

        internal void Say()
        {
            try
            {
                string line;
                if (Lines == null || Lines.Length == 0) line = LocHelper.GetLocalizedDialogue("dialog_retired_generic_1");
                else
                {
                    int i = UnityEngine.Random.Range(0, Lines.Length);
                    if (Lines.Length > 1 && i == lastLine) i = (i + 1) % Lines.Length;
                    lastLine = i;
                    line = Lines[i];
                }
                DialogUIManager.Instance.StartDialogue(new Dialogue().SetText(GetTitle(), line));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] say: " + ex.Message);
            }
        }

        private void HidePerson()
        {
            if (person != null) UnityEngine.Object.Destroy(person);
            person = null;
        }

        private void RestoreBackground()
        {
            HidePerson();
            if (swappedRenderer != null && originalSprite != null) swappedRenderer.sprite = originalSprite;
            swappedRenderer = null;
            originalSprite = null;
            foreach (SpriteRenderer r in hidden)
                if (r != null) r.enabled = true;
            hidden.Clear();
        }

        public override void Arrive()
        {
            MapUIManager map = MapUIManager.Instance;
            PlayerStore.Instance.playerLocation = PlayerStore.PlayerLocation.INVENTOR;
            SetJacksonTablet(false);
            EmporiumEntry.Instance.frontInvWindow.Show();
            EmporiumEntry.Instance.backInvWindow.Show();
            map.TransferBackInvToCounter();
            EmporiumEntry.Instance.ExpandFrontCounterGrid();
            // 판매 재고 없음 (카운터 비움)

            StoreClient client = CreateClient();
            PlayerStore.Instance.currentClientInstance = StoreClientInstance.CreateClientInstance(client);
            StoreClientMono.Instance.image.sprite = SpriteDict.Instance.ShowClientSprite(client.GetSprite());
            StoreUIManager.Instance.HideClient();
            PlayerStore.Instance.isClientArrived = true;
            // 손님을 정한 뒤에 배경 교체 + 상인 세우기 (가게 손님 자리를 읽어야 해서)
            try { SwapBackground(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] background swap: " + ex.Message); }
            Say(); // 도착 인사

            StoreUIManager.Instance.SetClientPositionToDrJackson();
            StoreUIManager.Instance.storeCreditDisplay.creditDisplayText.gameObject.SetActive(true);
            map.SetCreditDisplayPositionToDrJackson();
        }

        // 전용 초상화가 없는 상인(원래 게임에서도 매번 랜덤): 이 얼굴 풀들에서 세이브마다 고정으로 하나 고른다
        public string[] Pools;
        private string cachedRun;

        private void EnsureSpriteName()
        {
            string run = PlayerStore.Instance != null ? PlayerStore.Instance.runID ?? "" : "";
            if (!string.IsNullOrEmpty(cachedSprite) && cachedRun == run) return;
            cachedRun = run;
            cachedSprite = null;
            if (Pools != null && Pools.Length > 0)
            {
                List<string> faces = VanillaFaces(Pools);
                if (faces.Count > 0) cachedSprite = faces[(int)(StableHash(run + "|" + Key) % (uint)faces.Count)];
            }
            if (string.IsNullOrEmpty(cachedSprite))
            {
                try { cachedSprite = Sprite != null ? Sprite() : null; } catch { cachedSprite = null; }
            }
            if (string.IsNullOrEmpty(cachedSprite)) cachedSprite = SpriteDict.GetRandomLowerLevelSpriteName();
        }

        // 바닐라 얼굴만: 게임 그림 사전의 "풀이름1, 풀이름2 …" 연속 번호 키.
        // SpriteDict.GetPool 은 초상화 모드가 자기 얼굴(풀이름_psrl_…)을 덧붙이므로 쓰지 않는다.
        private static List<string> VanillaFaces(string[] pools)
        {
            var list = new List<string>();
            var dict = SpriteDict.Instance != null ? SpriteDict.Instance.spriteDictionary : null;
            if (dict == null) return list;
            foreach (string prefix in pools)
                for (int i = 1; i < 1000 && dict.ContainsKey(prefix + i); i++)
                    list.Add(prefix + i);
            return list;
        }

        // 실행마다 같은 값 (string.GetHashCode 는 런타임에 따라 달라질 수 있어 직접 계산: FNV-1a)
        private static uint StableHash(string s)
        {
            uint h = 2166136261;
            foreach (char c in s)
            {
                h ^= c;
                h *= 16777619;
            }
            return h;
        }

        private StoreClient CreateClient()
        {
            EnsureSpriteName();
            var c = new StoreClient();
            c.identifier = Id;
            c.displayName = GetTitle();
            c.spriteName = cachedSprite;
            c.clientFaction = "FACTION_LOWER_LEVEL";
            c.nonShootable = true;
            c.isBargainAvailable = false;
            c.clientIntent = StoreClient.ClientIntent.DIALOGUE;
            c.mainDialogue.SetText(c.displayName, LocHelper.GetLocalizedDialogue("dialog_retired_generic_1"));
            c.AddBasicDialog();
            c.CompleteClientCreation();
            return c;
        }

        public override void Leave()
        {
            // 잭슨 박사 Leave 와 동일: 카운터의 내 물건은 뒷 카운터로, 바닥에 둔 물건도 회수
            MapUIManager map = MapUIManager.Instance;
            NegociationUIManager.Instance.CloseUI();
            var mine = new List<GameItem>();
            GameGridInventory[] invs = { EmporiumEntry.Instance.frontInvinvElement, EmporiumEntry.Instance.backInvinvElement };
            foreach (GameInventory inv in invs)
                foreach (GameItem item in new List<GameItem>(inv.childItems))
                    if (GeneralHelper.IsItemOwned(item))
                    {
                        item.CloseContentWindow(includeChildren: true);
                        inv.Expel(item);
                        mine.Add(item);
                    }
            EmporiumEntry.Instance.ResetFrontCounterGrid();
            EmporiumEntry.Instance.ClearFrontInv();
            EmporiumEntry.Instance.ClearBackInv();
            map.TransferCounterToBackInv();
            foreach (GameItem item in mine)
                GraphUtils.EmporiumTryAdd(EmporiumEntry.Instance.backInvWindow, item);
            foreach (GameItem item in new List<GameItem>(EmporiumEntry.Instance.afterhourInventory.childItems))
                GraphUtils.EmporiumTryAdd(EmporiumEntry.Instance.backInvWindow, item);
            map.ResetCreditDisplayPosition();
            map.DeactivateClientOutsideStore();
            try { RestoreBackground(); } catch { }
            SetJacksonTablet(true);
        }

        internal string WeekdayName()
        {
            string[] keys = { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };
            try { return LocHelper.GetLocalizedMechanic("mech_calendar_" + keys[Weekday]); }
            catch { return keys[Weekday]; }
        }
    }

    // 상인 집에 세운 초상화: 마우스를 올리면 이름, 클릭하면 대사
    internal class MerchantPerson : TooltipInterface, IClickable
    {
        public RetiredMerchantLocation owner;

        public override string GetTooltip() => owner != null ? owner.GetTitle() : "";

        public override bool IsTooltipEnabled() => owner != null;

        public void OnClick() => owner?.Say();
    }

    internal static class RetiredMerchants
    {
        private static PlayerStore PS => PlayerStore.Instance;
        private static bool Chemist() => PS.IsChemistStart() || NetworkUpgrade.IsRetiredChemistUnlocked();

        internal static readonly List<RetiredMerchantLocation> All = new List<RetiredMerchantLocation>
        {
            new RetiredMerchantLocation { Key = "chemist", NameKey = "name_retired_chemist", FallbackName = "은퇴한 화학자", Weekday = 2,
                Desc = "창문마다 은박지를 붙여 둔 아파트. 이웃들은 냄새에 대해 묻지 않는 법을 배웠다.",
                Lines = new[] { "어, 가게 주인 양반. 문 닫고 들어와. 환기는… 안 하는 게 좋아.", "요즘은 손 뗐어. 진짜로. 저 플라스크들은 그냥 장식이야.", "냄새? 무슨 냄새?" },
                Unlocked = Chemist, Sprite = SpriteDict.GetRetiredChemistSpriteName },
            new RetiredMerchantLocation { Key = "aquafarmer", BackgroundFile = "bg_aquafarmer.png", NameKey = "name_retired_aquafarmer", FallbackName = "은퇴한 양식업자", Weekday = 2,
                Desc = "강가의 낡은 수조 창고. 물고기보다 이끼가 더 잘 자란다.",
                Lines = new[] { "물 튀니까 조심해. 녀석들이 오늘따라 신났거든.", "수조 하나 보는 데 평생이 걸려. 은퇴한 지금도 여전하지.", "생선 사러 왔어? 아니면 그냥 구경?" },
                Unlocked = NetworkUpgrade.IsAquacultureUnlocked, Sprite = SpriteDict.GetRetiredAquafarmerSpriteName },
            new RetiredMerchantLocation { Key = "junker", NameKey = "name_retired_junker", FallbackName = "은퇴한 고물상", Weekday = 3,
                Desc = "고철 더미 사이 어딘가에 집이 있다. 정확히 어디인지는 주인도 가끔 헷갈린다.",
                Lines = new[] { "발밑 조심해. 그 고철, 아직 쓸 데가 있거든.", "버려진 기계는 없어. 아직 고쳐지지 않은 기계만 있을 뿐.", "어디 보자… 내가 그걸 어디 뒀더라." },
                Unlocked = () => PS.IsJunkerStart() || NetworkUpgrade.IsRuinedMachineUnlocked(), Pools = new[] { "maleScav", "femaleScav" } },
            new RetiredMerchantLocation { Key = "water_merchant", BackgroundFile = "bg_water_merchant.png", NameKey = "name_retired_water_merchant", FallbackName = "은퇴한 물장수", Weekday = 3,
                Desc = "정거장에서 가장 깨끗한 물이 나온다는 소문의 집. 출처는 영업 비밀.",
                Lines = new[] { "어서 와. 오늘 물맛 좋아. 매일 좋지만.", "필터는 거짓말을 안 해. 사람이나 거짓말을 하지.", "한 모금 할래? 첫 잔은 공짜야. 두 번째부턴 아니고." },
                Unlocked = () => PS.IsWaterMerchantStart() || NetworkUpgrade.IsWaterTraderUnlocked(), Pools = new[] { "maleLower", "femaleLower" } },
            new RetiredMerchantLocation { Key = "gunsmith", BackgroundFile = "bg_gunsmith.png", NameKey = "name_retired_gunsmith", FallbackName = "은퇴한 총기 장인", Weekday = 4,
                Desc = "문 앞에 '노크 금지'라고 적혀 있다. 총알구멍으로.",
                Lines = new[] { "노크 안 했지? 잘했어.", "총은 정직해. 쏘는 놈이 문제지.", "부품 얘기라면 밤새도 할 수 있어. 근데 오늘은 안 해." },
                Unlocked = () => PS.IsGunsmithStart() || NetworkUpgrade.IsRetiredGunsmithUnlocked(), Pools = new[] { "maleLower", "femaleLower" } },
            new RetiredMerchantLocation { Key = "farmer", NameKey = "name_retired_farmer", FallbackName = "은퇴한 농부", Weekday = 5,
                Desc = "옥상 텃밭이 딸린 집. 정거장에서 진짜 흙을 만질 수 있는 몇 안 되는 곳.",
                Lines = new[] { "흙 묻은 손으로 미안해. 방금까지 텃밭에 있었거든.", "정거장에서 진짜 토마토 먹어본 적 있어? 없을걸.", "씨앗은 정직해. 심은 대로 나지." },
                Unlocked = () => PS.IsFarmerStart() || NetworkUpgrade.IsRetiredFarmerUnlocked(), Sprite = SpriteDict.GetRetiredFarmerSpriteName },
            new RetiredMerchantLocation { Key = "winemaker", NameKey = "name_retired_winemaker", FallbackName = "은퇴한 와인 제조자", Weekday = 5,
                Desc = "지하 저장고에서 뭔가 발효되고 있다. 와인이길 바랄 뿐.",
                Lines = new[] { "딱 맞춰 왔네. 이번 통이 막 숨을 쉬기 시작했어.", "와인은 기다림이야. 정거장 사람들은 그걸 못 하지.", "시음은 안 돼. 아직은." },
                Unlocked = NetworkUpgrade.IsWinemakerUnlocked, Pools = new[] { "maleFarmer", "femaleFarmer" } },
            new RetiredMerchantLocation { Key = "rancher", NameKey = "name_retired_rancher", FallbackName = "은퇴한 목장주", Weekday = 5,
                Desc = "쥐 우리가 층층이 쌓인 집. 주인은 녀석들 이름을 전부 외운다.",
                Lines = new[] { "쉿, 막 재웠어. 저 녀석이 제일 예민하거든.", "이름 다 외우냐고? 당연하지. 저건 3번 우리 나이젤이야.", "좋은 혈통은 거짓말을 안 해. 털만 봐도 알지." },
                Unlocked = () => PS.IsRancherStart() || NetworkUpgrade.IsRetiredRancherUnlocked(), Pools = new[] { "maleFarmer", "femaleFarmer" } },
        };

        internal static void Register()
        {
            foreach (RetiredMerchantLocation loc in All)
            {
                try { ModHelper.AddAfterhourLocation(loc); }
                catch (Exception ex) { Debug.LogWarning("[MapExtension] register " + loc.Id + ": " + ex.Message); }
            }
        }

        internal static void Unregister()
        {
            try { ModHelper.RemoveAfterhourLocations(MapExtensionMod.ModId); } catch { }
        }
    }
}
