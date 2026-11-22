using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MapExtension
{
    // 바닐라 외출 창(TransitUI)은 그대로 쓰고, 카드 목록 자리(1808x832)만 도시 지도로 바꾼다.
    //  - 창, 닫기 버튼, "이동" 버튼, 입력 처리는 바닐라 그대로
    //  - 장소 마커 클릭 = TransitUI.Select (바닐라 선택), 더블클릭 = 바로 이동
    //  - 선택한 장소의 이벤트는 지도 왼쪽 아래 정보 칸에 표시
    internal static class TransitMap
    {
        // 지도 위 장소 위치 (보이는 지도 영역 기준 0~1, 왼쪽 위가 0,0)
        private static readonly Dictionary<string, Vector2> Positions = new Dictionary<string, Vector2>
        {
            { "dumping_ground", new Vector2(0.10f, 0.62f) },
            { "commissary",     new Vector2(0.46f, 0.47f) },
            { "dr_jackson",     new Vector2(0.62f, 0.70f) },
            // 파밍 장소 (FarmLocation.cs)
            { "vme_farm_pharmacy",          new Vector2(0.38f, 0.38f) },
            // 은퇴한 상인 집 (RetiredMerchants.cs)
            { "vme_retired_chemist",        new Vector2(0.30f, 0.28f) },
            { "vme_retired_aquafarmer",     new Vector2(0.20f, 0.42f) },
            { "vme_retired_junker",         new Vector2(0.09f, 0.84f) },
            { "vme_retired_water_merchant", new Vector2(0.78f, 0.50f) },
            { "vme_retired_gunsmith",       new Vector2(0.40f, 0.80f) },
            { "vme_retired_farmer",         new Vector2(0.89f, 0.26f) },
            { "vme_retired_winemaker",      new Vector2(0.70f, 0.20f) },
            { "vme_retired_rancher",        new Vector2(0.86f, 0.82f) },
        };

        // 수색처(FarmLocation) 위치 = 미확인 장소 자리 (같은 key)
        private static bool UnknownPos(AfterhourLocation loc, out Vector2 p)
        {
            p = Vector2.zero;
            if (!(loc is FarmLocation fl)) return false;
            foreach (var u in UnknownSpots) if (u.key == fl.Key) { p = u.pos; return true; }
            return false;
        }

        // 위치를 모르는 장소(다른 모드)는 이 자리들에 차례로 놓는다
        private static readonly Vector2[] Spare =
        {
            new Vector2(0.30f, 0.30f), new Vector2(0.80f, 0.40f), new Vector2(0.35f, 0.78f),
            new Vector2(0.86f, 0.75f), new Vector2(0.20f, 0.20f), new Vector2(0.70f, 0.22f),
            new Vector2(0.55f, 0.88f), new Vector2(0.92f, 0.20f),
        };

        private const float AreaW = 1852f, AreaH = 972f; // 창(1856x1016) 본문: 제목 줄(40) 아래, 테두리 2px 안쪽

        private static readonly Color Available = new Color32(0x4d, 0xf0, 0xe0, 0xff);
        private static readonly Color Selected = new Color32(0xff, 0xd8, 0x4a, 0xff);
        private static readonly Color Unavailable = new Color32(0x80, 0x80, 0x88, 0xff);
        private static readonly Color EventColor = new Color32(0xff, 0x55, 0x6e, 0xff);
        private static readonly Color Locked = new Color32(0x3a, 0x38, 0x46, 0xff);

        private static GameObject root;
        private static TextMeshProUGUI infoTitle, infoBody;
        private static GameObject infoBox;
        private static TMP_FontAsset font;
        private static readonly List<Marker> markers = new List<Marker>();

        internal static AfterhourLocation SelectedLocation
        {
            get
            {
                try { return Traverse.Create(typeof(TransitUI)).Field("selected").GetValue<AfterhourLocation>(); }
                catch { return null; }
            }
        }

        internal static void Destroy()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            markers.Clear();
        }

        internal static void Build()
        {
            TransitFrame frame = Traverse.Create(typeof(TransitUI)).Field("frame").GetValue<TransitFrame>();
            if (frame == null || frame.cardsHost == null) return;
            // 제네릭 GetValue<List<...>> 는 스캐너가 reflection-external 로 막으므로 object 로 받아 변환
            var listed = Traverse.Create(typeof(TransitUI)).Field("listed").GetValue() as List<AfterhourLocation>;
            if (listed == null) return;

            Texture2D bg = FindTexture("map_background");
            if (bg == null)
            {
                Debug.LogWarning("[MapExtension] map_background texture not loaded; keeping vanilla cards");
                return;
            }

            // 바닐라 카드/선로/페이지 화살표 숨김
            frame.cardsHost.gameObject.SetActive(false);
            if (frame.trackHost != null) frame.trackHost.gameObject.SetActive(false);
            if (frame.prevButton != null) frame.prevButton.gameObject.SetActive(false);
            if (frame.nextButton != null) frame.nextButton.gameObject.SetActive(false);
            font = frame.fontNotoRegular;

            Destroy();
            // 창 본문 전체(제목 줄 아래 ~ 바닥, 테두리 2px 안쪽)를 덮는다.
            // 카드 영역은 창 기준 (64, 202) 이므로 그 차이만큼 옮겨 (2, 42) 에 놓는다.
            Vector2 cards = frame.cardsHost.anchoredPosition;
            Transform parent = frame.cardsHost.parent;
            root = new GameObject("MapExtension_Map", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.transform.SetSiblingIndex(frame.cardsHost.GetSiblingIndex());
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = cards + new Vector2(-62f, 160f);
            rt.sizeDelta = new Vector2(AreaW, AreaH);
            root.AddComponent<RectMask2D>();

            // 제목 / 닫기 / 이동 버튼은 지도 위로
            foreach (Component c in new Component[] { frame.title, frame.closeButton, frame.travelButton })
            {
                Transform top = TopChild(c != null ? c.transform : null, parent);
                if (top != null && top != root.transform) top.SetAsLastSibling();
            }

            // 배경: 영역을 꽉 채우고 넘치는 위아래(또는 좌우)는 잘라낸다
            var img = NewRect(root.transform, "Background", 0f, 0f, AreaW, AreaH).gameObject.AddComponent<RawImage>();
            img.texture = bg;
            img.raycastTarget = true;
            img.uvRect = CoverUv(bg.width, bg.height, AreaW, AreaH);
            root.AddComponent<BackgroundClick>();

            // 구역 이름 (글자만, 마커보다 뒤)
            foreach (var z in Districts) CreateDistrictLabel(z.name, z.pos, z.color);

            int spare = 0;
            // 발견한 장소 + 아직 해금 안 된 장소(이름 없는 잠긴 네모)
            var all = new List<AfterhourLocation>(listed);
            try
            {
                foreach (AfterhourLocation loc in AfterhourLocation.GetAll())
                    if (loc != null && !all.Contains(loc)) all.Add(loc);
            }
            catch
            {
            }
            foreach (AfterhourLocation loc in all)
            {
                if (loc == null || loc is BranchLocation) continue; // 분점은 분점 마커로 따로
                Vector2 p;
                if (!Positions.TryGetValue(loc.Id ?? "", out p) && !UnknownPos(loc, out p))
                    p = Spare[Math.Min(spare++, Spare.Length - 1)];
                // 아직 망원경으로 못 찾은 수색처: 예전처럼 흐린 "미확인 장소"
                if (loc is FarmLocation fl && !(loc is BranchLocation) && !fl.IsDiscovered())
                {
                    CreateLockedLabel(UnknownName, UnknownDesc, p, "unknown");
                    continue;
                }
                markers.Add(CreateMarker(loc, p, !listed.Contains(loc)));
            }

            // 은퇴한 상인들 가게 (아직 기능 없음: 잠긴 네모 + 이름표만)
            infoMarkers.Clear();
            foreach (var spot in RetiredSpots)
                CreateLockedLabel(spot.name, spot.desc, spot.pos);
            // 아직 안 만든 수색처: "미확인 장소" (망원경 해금 예정)
            foreach (var u in UnknownSpots)
            {
                bool made = false;
                foreach (FarmLocation l in FarmLocations.All) if (l.Key == u.key) { made = true; break; }
                if (!made) CreateLockedLabel(UnknownName, UnknownDesc, u.pos, "unknown"); // 아직 안 만든 자리만
            }

            // 분점 부지 / 내 분점
            branchMarkers.Clear();
            foreach (BranchSite b in Branches.All) branchMarkers.Add(CreateBranchMarker(b));

            HomeSelected = false;
            home = CreateHomeMarker(HomePosition);

            BuildInfoBox();
            try { BuildHomeButton(frame); } catch (Exception ex) { Debug.LogWarning("[MapExtension] home button: " + ex.Message); }
            Refresh();
        }

        // ---- [이동] 버튼 왼쪽 [돌아가기]: 누르면 바로 가게로 (확인 없음) ----
        private static GameObject homeButton;

        private static void BuildHomeButton(TransitFrame frame)
        {
            if (homeButton != null) UnityEngine.Object.Destroy(homeButton);
            homeButton = null;
            if (frame.travelButton == null) return;
            // 이동 버튼 묶음(CanvasGroup 이 위에 있으면 그것째)을 복제
            Transform src = frame.travelButton.transform;
            if (frame.travelGroup != null && src.IsChildOf(frame.travelGroup.transform)) src = frame.travelGroup.transform;
            // 버튼 너비 줄이기 (지도를 열 때마다 줄지 않게 한 번만)
            const float ButtonScale = 0.55f, ButtonGap = 14f;
            var srt0 = (RectTransform)src;
            if (src.GetComponent<ResizedMark>() == null)
            {
                float oldW = srt0.rect.width;
                float oldCenter = CenterX(srt0);
                ShrinkWidth(src, oldW, ButtonScale);
                src.gameObject.AddComponent<ResizedMark>().center = oldCenter;
            }
            float pairCenter = src.GetComponent<ResizedMark>().center;
            float bw = srt0.rect.width;
            // [이동] 은 왼쪽, [돌아가기] 는 오른쪽. 두 버튼 묶음의 가운데 = 원래 이동 버튼 가운데
            SetCenterX(srt0, pairCenter - (bw + ButtonGap) * 0.5f);
            string pOuter = PathFrom(frame.travelOuter != null ? frame.travelOuter.transform : null, src);
            string pLabel = PathFrom(frame.travelLabel != null ? frame.travelLabel.transform : null, src);
            string pButton = PathFrom(frame.travelButton.transform, src);

            var holder = new GameObject("VME_Holder");
            holder.SetActive(false);
            GameObject go = UnityEngine.Object.Instantiate(src.gameObject, holder.transform, false);
            go.name = "VME_HomeButton";
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string ns = mb.GetType().Namespace ?? "";
                if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro")) continue;
                UnityEngine.Object.DestroyImmediate(mb); // 게임 hover 스크립트 등
            }
            go.transform.SetParent(src.parent, false);
            UnityEngine.Object.Destroy(holder);
            var rt = (RectTransform)go.transform;
            var srt = (RectTransform)src;
            rt.anchoredPosition = srt.anchoredPosition + new Vector2(bw + ButtonGap, 0f);
            go.transform.SetSiblingIndex(src.GetSiblingIndex() + 1);

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }
            Transform bt = Find(go.transform, pButton);
            Button b = bt != null ? bt.GetComponent<Button>() : null;
            Image outer = Find(go.transform, pOuter)?.GetComponent<Image>();
            TMP_Text label = Find(go.transform, pLabel)?.GetComponent<TMP_Text>();
            if (b == null) { UnityEngine.Object.Destroy(go); return; }
            if (label != null) { label.text = "돌아가기"; label.color = TransitStyle.BUTTON_BORDER; }
            if (outer != null) outer.color = TransitStyle.BUTTON_BORDER;
            b.onClick = new Button.ButtonClickedEvent();
            b.interactable = true;
            b.onClick.AddListener(() =>
            {
                try { EmporiumAudioPlayer.Instance.audioButton2.Play(); } catch { }
                MapUIManager.Instance?.CloseUI();
            });
            var hover = b.gameObject.AddComponent<HoverTint>();
            hover.outer = outer;
            hover.label = label;
            homeButton = go;
        }

        // 부모 좌표에서 가운데 x (앵커가 한 점일 때)
        private static float CenterX(RectTransform rt) => rt.anchoredPosition.x + (0.5f - rt.pivot.x) * rt.rect.width;

        private static void SetCenterX(RectTransform rt, float cx)
        {
            rt.anchoredPosition = new Vector2(cx - (0.5f - rt.pivot.x) * rt.rect.width, rt.anchoredPosition.y);
        }

        // root 와 그 안에서 root 와 비슷하게 넓은(늘림 앵커가 아닌) 요소들의 너비를 factor 배로
        private static void ShrinkWidth(Transform root, float rootWidth, float factor)
        {
            foreach (RectTransform r in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (r != root && !Mathf.Approximately(r.anchorMin.x, r.anchorMax.x)) continue; // 부모 따라 늘어나는 건 자동
                float w = r.rect.width;
                if (r != root && w < rootWidth * 0.8f) continue;
                float c = r == root ? 0f : CenterX(r);
                r.sizeDelta = new Vector2(r.sizeDelta.x - w * (1f - factor), r.sizeDelta.y);
                if (r != root) SetCenterX(r, c * factor);
            }
        }

        // 지도 영역(배경 그림) — HUD 를 그 안 왼쪽 아래에 붙이려고
        internal static RectTransform MapArea => root != null && root.activeInHierarchy ? (RectTransform)root.transform : null;

        private static string PathFrom(Transform t, Transform root)
        {
            if (t == null) return null;
            if (t == root) return "";
            var parts = new List<string>();
            while (t != null && t != root) { parts.Insert(0, t.name); t = t.parent; }
            return t == root ? string.Join("/", parts) : null;
        }

        private static Transform Find(Transform root, string path)
        {
            if (path == null) return null;
            return path.Length == 0 ? root : root.Find(path);
        }

        // t 의 조상 중 parent 의 바로 아래 자식
        private static Transform TopChild(Transform t, Transform parent)
        {
            while (t != null && t.parent != parent) t = t.parent;
            return t;
        }

        private static Rect CoverUv(float texW, float texH, float w, float h)
        {
            float texAspect = texW / texH, areaAspect = w / h;
            if (areaAspect > texAspect)
            {
                float vh = texAspect / areaAspect;
                return new Rect(0f, (1f - vh) * 0.5f, 1f, vh);
            }
            float vw = areaAspect / texAspect;
            return new Rect((1f - vw) * 0.5f, 0f, vw, 1f);
        }

        private static Texture2D cachedBg;

        // 모드 스캐너가 ModLoader 접근을 막으므로, 게임의 모드 전용 API로 자기 폴더의 PNG를 읽는다.
        private static Texture2D FindTexture(string key)
        {
            if (cachedBg != null) return cachedBg;
            byte[] bytes = ModHelper.ReadModFileBytes(MapExtensionMod.ModId, "Map/" + key + ".png");
            if (bytes == null || bytes.Length == 0) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = key;
            cachedBg = tex;
            return tex;
        }

        private static Marker CreateMarker(AfterhourLocation loc, Vector2 p, bool locked)
        {
            float x = p.x * AreaW, y = Mathf.Min(p.y, MaxMarkerY) * AreaH;
            RectTransform box = NewRect(root.transform, "Marker_" + loc.Id, x - 110f, y - 18f, 220f, 72f);
            ScaleMarker(box);
            var marker = box.gameObject.AddComponent<Marker>();
            marker.location = loc;
            marker.locked = locked;

            if (locked)
            {
                // 잠긴 장소: 이름 없이 어두운 네모만, 클릭 영역 없음
                marker.inner = NewImage(box, "Inner", 98f, 12f, 24f, 24f, Locked);
                return marker;
            }

            // 클릭 영역(투명)
            var hit = box.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            marker.inner = NewImage(box, "Inner", 98f, 12f, 24f, 24f, Available);
            SetIcon(marker.inner, IconFor(loc));
            marker.alert = NewText(box, "Alert", 118f, 0f, 18f, 20f, 20f, EventColor, TextAlignmentOptions.Center); // 아이콘 오른쪽 위
            ScriptFontSwap.Apply(marker.alert, "!");

            // 이름표: 어두운 띠 + 글자
            marker.label = NewText(box, "Label", 0f, 40f, 220f, 30f, 20f, Color.white, TextAlignmentOptions.Center);
            ScriptFontSwap.Apply(marker.label, SafeTitle(loc));
            return marker;
        }

        // ---- 은퇴한 상인 가게 자리 (준비 중: 잠금) ----
        private static readonly (string name, string desc, Vector2 pos)[] RetiredSpots =
        {
            // 세력 거점 (준비 중: 잠금)
            ("보안대 본부", "정거장의 질서를 지키는 곳. 누구의 질서인지는 묻지 않는 게 좋다.", new Vector2(0.52f, 0.20f)),
            ("암시장", "간판도 주소도 없다. 찾을 수 있다면 이미 손님이다.", new Vector2(0.25f, 0.64f)),
            ("혁명군 아지트", "안테나 탑 아래 어딘가. 오늘의 암호는 매일 바뀐다.", new Vector2(0.31f, 0.11f)),
            ("카르텔 거점", "겉으로는 평범한 창고. 안에서 셈하는 돈은 평범하지 않다.", new Vector2(0.70f, 0.89f)),
        };

        // ---- 미확인 장소: 앞으로 만들 수색처 자리 (key / 실제 이름 / 단계 / 위치). 만들면 FarmLocations 로 옮기고 여기서 뺀다 ----
        private const string UnknownName = "미확인 장소";
        private const float UnknownAlpha = 0.2f;
        private const string UnknownDesc = "아직 아무도 확인하지 않은 곳. 높은 곳에서 내려다보면 뭔가 보일지도 모른다.";
        internal static readonly (string key, string name, int tier, Vector2 pos)[] UnknownSpots =
        {
            ("hardware",     "버려진 철물점",       1, new Vector2(0.48f, 0.33f)),
            ("convenience",  "편의점",              2, new Vector2(0.66f, 0.52f)),
            ("apartment",    "빈 아파트",           2, new Vector2(0.60f, 0.36f)),
            ("laundry",      "세탁소",              2, new Vector2(0.36f, 0.55f)),
            ("clinic",       "동네 의원",           2, new Vector2(0.73f, 0.62f)),
            ("radio_shop",   "전파사",              2, new Vector2(0.45f, 0.66f)),
            ("sewer",        "하수구 입구",         2, new Vector2(0.52f, 0.78f)),
            ("lost_found",   "정거장 분실물 보관소", 3, new Vector2(0.43f, 0.06f)),
            ("loading_dock", "화물 하역장",         3, new Vector2(0.14f, 0.27f)),
            ("factory",      "폐공장",              3, new Vector2(0.08f, 0.44f)),
            ("garage",       "수리소",              3, new Vector2(0.84f, 0.38f)),
            ("school",       "폐교",                3, new Vector2(0.18f, 0.12f)),
            ("soup_kitchen", "무료 급식소",         3, new Vector2(0.30f, 0.74f)),
            ("church",       "무너진 교회",         3, new Vector2(0.81f, 0.10f)),
            ("checkpoint",   "보안대 검문소 폐허",  4, new Vector2(0.61f, 0.26f)),
            ("rebel_hideout","혁명군 은신처 흔적",  4, new Vector2(0.40f, 0.22f)),
            ("cartel_storage","카르텔 창고",        4, new Vector2(0.58f, 0.92f)),
            ("mansion",      "상류층 저택 빈집",    4, new Vector2(0.93f, 0.58f)),
            ("hospital",     "폐병원",              4, new Vector2(0.20f, 0.90f)),
        };

        // 잠긴 자리: 클릭은 안 되고 마우스를 올리면 설명 툴팁만
        private static readonly List<Marker> infoMarkers = new List<Marker>();

        private static void CreateLockedLabel(string name, string desc, Vector2 p, string icon = null)
        {
            float x = p.x * AreaW, y = Mathf.Min(p.y, MaxMarkerY) * AreaH;
            RectTransform box = NewRect(root.transform, "Locked_" + name, x - 110f, y - 18f, 220f, 72f);
            ScaleMarker(box);
            var marker = box.gameObject.AddComponent<Marker>();
            marker.infoOnly = true;
            marker.tipTitle = name;
            marker.tipBody = desc;
            var hit = box.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            // 아이콘이 있으면 회색 아이콘 (어두운 네모 색은 지도 위에서 안 보임)
            marker.inner = NewImage(box, "Inner", 98f, 12f, 24f, 24f, icon != null ? Unavailable : Locked);
            if (icon != null) SetIcon(marker.inner, icon);
            marker.label = NewText(box, "Label", 0f, 40f, 220f, 30f, 20f, Unavailable, TextAlignmentOptions.Center);
            ScriptFontSwap.Apply(marker.label, name);
            if (icon == "unknown")
            {
                // 미확인 장소는 희미하게
                Color ic = marker.inner.color; ic.a = UnknownAlpha; marker.inner.color = ic;
                Color lc = marker.label.color; lc.a = UnknownAlpha; marker.label.color = lc;
            }
            infoMarkers.Add(marker);
        }

        // ---- 분점: 안 산 부지 = 회색, 산 분점 = 금색. 클릭하면 사기 확인 창 ----
        private static readonly List<Marker> branchMarkers = new List<Marker>();
        private static readonly Color BranchOwned = new Color32(0xff, 0xd8, 0x4a, 0xff);

        private static Marker CreateBranchMarker(BranchSite b)
        {
            float x = b.Pos.x * AreaW, y = Mathf.Min(b.Pos.y, MaxMarkerY) * AreaH;
            RectTransform box = NewRect(root.transform, "Branch_" + b.Key, x - 110f, y - 18f, 220f, 72f);
            ScaleMarker(box);
            var marker = box.gameObject.AddComponent<Marker>();
            marker.branch = b;
            marker.location = Branches.LocationOf(b); // 산 분점: 일반 장소처럼 선택/이동
            var hit = box.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            marker.inner = NewImage(box, "Inner", 98f, 12f, 24f, 24f, Unavailable);
            SetIcon(marker.inner, "stock");
            marker.label = NewText(box, "Label", 0f, 40f, 220f, 30f, 20f, Unavailable, TextAlignmentOptions.Center);
            ScriptFontSwap.Apply(marker.label, b.Name);
            return marker;
        }

        // ---- 내 전당포 (가게로 돌아가기) ----
        private static readonly Vector2 HomePosition = new Vector2(0.55f, 0.58f);
        private static readonly Color HomeColor = new Color32(0xff, 0x9f, 0x43, 0xff);
        private const string HomeName = "내 전당포";
        private const string HomeDesc = "내가 운영 중인 전당포. 동네 사람들은 '그 가게'라고만 부른다.";
        private const string HomeAsk = "아무것도 하지 않고 가게로 돌아가시겠습니까?";
        private static Marker home;
        internal static bool HomeSelected;

        private static Marker CreateHomeMarker(Vector2 p)
        {
            float x = p.x * AreaW, y = Mathf.Min(p.y, MaxMarkerY) * AreaH;
            RectTransform box = NewRect(root.transform, "Marker_home", x - 110f, y - 18f, 220f, 72f);
            ScaleMarker(box);
            var marker = box.gameObject.AddComponent<Marker>();
            marker.isHome = true;
            var hit = box.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            marker.inner = NewImage(box, "Inner", 98f, 12f, 24f, 24f, HomeColor);
            SetIcon(marker.inner, "home");
            marker.label = NewText(box, "Label", 0f, 40f, 220f, 30f, 20f, Color.white, TextAlignmentOptions.Center);
            ScriptFontSwap.Apply(marker.label, HomeName);
            return marker;
        }

        internal static void SelectHome()
        {
            HomeSelected = !HomeSelected;
            AfterhourLocation sel = SelectedLocation;
            if (HomeSelected && sel != null) TransitUI.Select(sel); // 바닐라 선택 해제 (같은 장소 다시 고르면 풀림)
            try { EmporiumAudioPlayer.Instance.audioButton1.Play(); } catch { }
            RefreshTravelButton();
            Refresh();
        }

        internal static void RefreshTravelButton()
        {
            try { Traverse.Create(typeof(TransitUI)).Method("RefreshTravelButton").GetValue(); } catch { }
        }

        internal static void AskReturnHome()
        {
            try { EmporiumAudioPlayer.Instance.audioButton2.Play(); } catch { }
            if (GenericConfirmationUIManager.Instance == null)
            {
                MapUIManager.Instance?.CloseUI();
                return;
            }
            Dialogs.Confirm(HomeAsk, LocHelper.GetLocalizedMainMenu("yes"), LocHelper.GetLocalizedMainMenu("no"),
                () => MapUIManager.Instance?.CloseUI());
        }

        // 마우스를 올린 마커 바로 위에 뜨는 툴팁 (위에 자리가 없으면 마커 아래)
        private const float TipW = 420f;
        private static RectTransform infoRect, infoBg, infoEdge;

        // 게임 불러오기 창 스타일: 어두운 남보라 바탕 + 민트 테두리 + 민트 제목 + 밝은 회색 본문
        private static readonly Color TipBg = new Color32(0x24, 0x1f, 0x2e, 0xf2);
        private static readonly Color TipBorder = new Color32(0x8b, 0xc4, 0xbe, 0xff);
        private static readonly Color TipTitle = new Color32(0x9a, 0xd6, 0xcf, 0xff);
        private static readonly Color TipBody = new Color32(0xe2, 0xe2, 0xe6, 0xff);
        private const float TipLine = 2f;

        private static void BuildInfoBox()
        {
            infoRect = NewRect(root.transform, "Info", 0f, 0f, TipW, 120f);
            infoBox = infoRect.gameObject;
            // 테두리 = 민트 판 위에 2px 안쪽으로 어두운 판
            infoEdge = NewImage(infoRect, "Border", 0f, 0f, TipW, 120f, TipBorder).rectTransform;
            infoBg = NewImage(infoRect, "Bg", TipLine, TipLine, TipW - TipLine * 2f, 120f - TipLine * 2f, TipBg).rectTransform;
            // 높이가 줄 높이보다 작으면 Ellipsis 가 글자를 통째로 숨기므로 넉넉히 + Overflow
            infoTitle = NewText(infoRect, "Title", 16f, 8f, TipW - 28f, 40f, 24f, TipTitle, TextAlignmentOptions.TopLeft);
            infoTitle.overflowMode = TextOverflowModes.Overflow;
            infoBody = NewText(infoRect, "Body", 16f, 48f, TipW - 28f, 60f, 18f, TipBody, TextAlignmentOptions.TopLeft);
            infoBody.overflowMode = TextOverflowModes.Overflow;
            infoBody.enableWordWrapping = true;
            infoBox.SetActive(false);
        }

        private static void ShowTip(Marker anchor, string title, string body)
        {
            infoBox.SetActive(true);
            infoRect.SetAsLastSibling();
            ScriptFontSwap.Apply(infoTitle, title);
            ScriptFontSwap.Apply(infoBody, body ?? "");

            float bodyH = string.IsNullOrEmpty(body) ? 0f : infoBody.GetPreferredValues(body, TipW - 28f, 0f).y + 4f;
            float h = 48f + bodyH + 10f;
            infoRect.sizeDelta = new Vector2(TipW, h);
            infoEdge.sizeDelta = new Vector2(TipW, h);
            infoBg.sizeDelta = new Vector2(TipW - TipLine * 2f, h - TipLine * 2f);
            infoBody.rectTransform.sizeDelta = new Vector2(TipW - 28f, Mathf.Max(bodyH, 1f));

            // 마커의 실제 화면 영역 (축소 반영) → 지도(root) 좌표. root 는 왼쪽 위 기준이라 아래쪽이 -y
            var m = (RectTransform)anchor.transform;
            var wc = new Vector3[4];
            m.GetWorldCorners(wc);
            Vector3 bl = root.transform.InverseTransformPoint(wc[0]);
            Vector3 tl = root.transform.InverseTransformPoint(wc[1]);
            Vector3 tr = root.transform.InverseTransformPoint(wc[2]);
            float cx = (tl.x + tr.x) * 0.5f;
            float top = -tl.y;
            float bottom = -bl.y;
            float x = Mathf.Clamp(cx - TipW * 0.5f, 6f, AreaW - TipW - 6f);
            float y = top - h - 8f;
            if (y < 6f) y = bottom + 8f;
            y = Mathf.Clamp(y, 6f, AreaH - h - 6f);
            infoRect.anchoredPosition = new Vector2(x, -y);
        }

        internal static void Refresh()
        {
            if (root == null) return;
            AfterhourLocation sel = SelectedLocation;
            foreach (Marker m in markers)
            {
                if (m == null || m.location == null || m.locked) continue;
                bool ok =Safe(() => m.location.IsAvailable());
                bool hasEvents = Safe(() => m.location.ActiveEvents.Count > 0);
                Color c = !ok ? Unavailable : (m.location == sel ? Selected : Available);
                if (m.hover && ok && m.location != sel) c = Color.Lerp(c, Color.white, 0.4f);
                m.inner.color = c;
                m.alert.gameObject.SetActive(hasEvents);
                if (m.label != null) m.label.color = ok ? Color.white : Unavailable;
            }

            foreach (Marker m in branchMarkers)
            {
                if (m == null || m.branch == null) continue;
                Color c = m.branch.Owned ? (m.location != null && m.location == sel ? Selected : BranchOwned) : Unavailable;
                if (m.hover && !(m.location != null && m.location == sel)) c = Color.Lerp(c, Color.white, 0.4f);
                m.inner.color = c;
                if (m.label != null) m.label.color = m.branch.Owned ? Color.white : Unavailable;
            }
            foreach (Marker m in branchMarkers)
                if (m != null && m.hover && m.branch != null)
                {
                    ShowTip(m, m.branch.Name, Branches.Tooltip(m.branch));
                    return;
                }

            if (home != null)
            {
                Color hc = HomeSelected ? Selected : HomeColor;
                if (home.hover && !HomeSelected) hc = Color.Lerp(hc, Color.white, 0.4f);
                home.inner.color = hc;
                if (home.hover)
                {
                    ShowTip(home, HomeName, HomeDesc);
                    return;
                }
            }

            foreach (Marker m in infoMarkers)
                if (m != null && m.hover)
                {
                    ShowTip(m, m.tipTitle, m.tipBody);
                    return;
                }

            foreach (Marker m in markers)
                if (m != null && m.hover && m.location != null && !m.locked)
                {
                    ShowTip(m, SafeTitle(m.location), Describe(m.location));
                    return;
                }
            infoBox.SetActive(false);
        }

        private static string Describe(AfterhourLocation loc)
        {
            var sb = new StringBuilder();
            if (loc is FarmLocation farm)
            {
                int ready = 0, total = 0, fillSum = 0;
                foreach (FarmSpot s in farm.Spots)
                {
                    if (s.Telescope) continue;
                    total++;
                    fillSum += farm.FillPercent(s);
                    if (farm.DaysUntilRefill(s) == 0) ready++;
                }
                sb.Append(farm.Desc).Append('\n');
                sb.Append("<color=#9a9aa8>뒤질 수 있는 곳: ").Append(ready).Append(" / ").Append(total).Append("</color>  ")
                  .Append(FarmHotspot.FillText(total > 0 ? fillSum / total : 0));
                return sb.ToString();
            }
            if (loc is RetiredMerchantLocation rm)
            {
                // 설명 + 방문 요일 + 못 가는 이유
                sb.Append(rm.Desc).Append('\n');
                sb.Append("<color=#9a9aa8>방문 가능: 매주 ").Append(rm.WeekdayName()).Append("</color>\n");
                if (!rm.IsUnlocked())
                    sb.Append("<color=#ff6b6b>아직 이 상인과 연이 없다 (해금 필요)</color>\n");
                else if (!rm.VisitsToday())
                    sb.Append("<color=#ff6b6b>오늘은 집에 없다</color>\n");
                return sb.ToString().TrimEnd('\n');
            }
            if (!Safe(() => loc.IsAvailable()))
                sb.Append("<color=#ff6b6b>").Append(LocHelper.GetLocalizedUI("ui_transit_unavailable")).Append("</color>\n");
            List<AfterhourEventDefinition> events = null;
            try { events = loc.ActiveEvents; } catch { }
            if (events != null)
                foreach (AfterhourEventDefinition ev in events)
                {
                    if (ev == null) continue;
                    sb.Append("<color=#ff8aa0>! ").Append(ev.GetTitle()).Append("</color>");
                    string duration = null;
                    try { duration = Traverse.Create(typeof(TransitUI)).Method("DurationText", ev.id).GetValue() as string; } catch { }
                    if (!string.IsNullOrEmpty(duration)) sb.Append(" <color=#9a9aa8>(").Append(duration).Append(")</color>");
                    sb.Append('\n');
                    try
                    {
                        foreach (string t in ev.GetEffectTexts())
                            if (!string.IsNullOrEmpty(t)) sb.Append("   ").Append(t).Append('\n');
                    }
                    catch { }
                }
            return sb.ToString().TrimEnd('\n');
        }

        private static string SafeTitle(AfterhourLocation loc)
        {
            try
            {
                string t = loc.GetTitle();
                return string.IsNullOrEmpty(t) ? loc.Id : t;
            }
            catch
            {
                return loc.Id;
            }
        }

        private static bool Safe(Func<bool> f)
        {
            try { return f(); } catch { return false; }
        }

        internal static void Travel(AfterhourLocation loc)
        {
            if (loc == null || !Safe(() => loc.IsAvailable()) || MapUIManager.Instance == null) return;
            try { EmporiumAudioPlayer.Instance.audioButton2.Play(); } catch { }
            MapUIManager.Instance.Travel(loc);
        }

        private static RectTransform NewRect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        // ---- 구역 표시: 클릭/툴팁 없는 흐린 큰 글자 ----
        private static readonly (string name, Vector2 pos, Color color)[] Districts =
        {
            ("상류층 구역", new Vector2(0.63f, 0.11f), new Color32(0xe8, 0xd2, 0x9a, 0x66)),
            ("하층민 구역", new Vector2(0.28f, 0.88f), new Color32(0xb8, 0xb8, 0xc4, 0x66)),
        };

        private static void CreateDistrictLabel(string name, Vector2 p, Color color)
        {
            float x = p.x * AreaW, y = p.y * AreaH;
            var t = NewText(root.transform, "District_" + name, x - 160f, y - 20f, 320f, 40f, 24f, color, TextAlignmentOptions.Center);
            t.characterSpacing = 18f;
            t.overflowMode = TextOverflowModes.Overflow;
            ScriptFontSwap.Apply(t, name);
        }

        // ---- 마커 크기: 아이콘 가운데를 기준으로 줄인다 (위치는 그대로) ----
        private const float MarkerScale = 0.7f;
        private const float MaxMarkerY = 0.86f; // 지도 아래쪽 버튼/HUD 줄에 마커가 겹치지 않게 (0~1, 위가 0)

        private static void ScaleMarker(RectTransform box)
        {
            // 상자(220x72) 안 아이콘 가운데 = (110, 24) → 그 점을 피벗으로 옮기고 같은 자리에 둔 뒤 축소
            Vector2 tl = box.anchoredPosition; // 왼쪽 위 (피벗 0,1)
            box.pivot = new Vector2(110f / 220f, 1f - 24f / 72f);
            box.anchoredPosition = tl + new Vector2(110f, -24f);
            box.localScale = new Vector3(MarkerScale, MarkerScale, 1f);
        }

        // ---- 지도 아이콘 (Map/icon_<key>.png: 검은 실루엣 → 불러올 때 흰색으로 바꿔서 Image.color 로 색칠) ----
        private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        private const float IconSize = 30f;

        // 수색할 수 있는 곳 = 돋보기, 그 외 가게 = 카트
        private static string IconFor(AfterhourLocation loc)
        {
            if (loc is FarmLocation || loc.Id == "dumping_ground") return "zoom_in";
            return "cart";
        }

        internal static Sprite Icon(string key) // 1x1 유닛 크기 (월드에서 쓸 때는 scale 로 크기)
        {
            if (icons.TryGetValue(key, out Sprite cached)) return cached;
            Sprite sp = null;
            try
            {
                byte[] bytes = ModHelper.ReadModFileBytes(MapExtensionMod.ModId, "Map/icon_" + key + ".png");
                if (bytes != null && bytes.Length > 0)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false); // 밉맵 없음 (픽셀 그대로)
                    if (tex.LoadImage(bytes))
                    {
                        Color32[] px = tex.GetPixels32();
                        for (int i = 0; i < px.Length; i++) { px[i].r = 255; px[i].g = 255; px[i].b = 255; }
                        tex.SetPixels32(px);
                        tex.wrapMode = TextureWrapMode.Clamp;
                        tex.filterMode = FilterMode.Point; // 픽셀 아트: 번지지 않게
                        tex.Apply(false);
                        tex.name = "icon_" + key;
                        sp = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] icon " + key + ": " + ex.Message);
            }
            icons[key] = sp; // 없으면 null 도 기억 (네모로 표시)
            return sp;
        }

        // 네모(24x24) 자리 가운데에 아이콘을 얹는다. 아이콘 파일이 없으면 네모 그대로.
        private static void SetIcon(Image img, string key)
        {
            Sprite sp = Icon(key);
            if (sp == null) return;
            img.sprite = sp;
            img.preserveAspect = true;
            var rt = img.rectTransform;
            rt.anchoredPosition += new Vector2(-(IconSize - 24f) * 0.5f, (IconSize - 24f) * 0.5f);
            rt.sizeDelta = new Vector2(IconSize, IconSize);
        }

        private static Image NewImage(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var img = NewRect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI NewText(Transform parent, string name, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align)
        {
            var t = NewRect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }
    }

    internal class Marker : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public AfterhourLocation location;
        public Image outer, inner;
        public TextMeshProUGUI label, alert;
        public bool hover;
        public bool locked;
        public bool isHome;
        public bool infoOnly; // 설명 툴팁만 (잠긴 자리)
        public BranchSite branch; // 분점 부지
        public string tipTitle, tipBody;

        public void OnPointerClick(PointerEventData e)
        {
            if (locked || e.button != PointerEventData.InputButton.Left || e.dragging) return;
            if (branch != null && !branch.Owned)
            {
                Branches.Click(branch); // 안 산 부지: 사기
                return;
            }
            if (isHome || infoOnly) // 내 전당포 / 잠긴 자리: 설명 툴팁만, 클릭은 아무 동작 없음
            {
                return;
            }
            if (location == null) return;
            if (e.clickCount >= 2)
            {
                TransitMap.Travel(location);
                return;
            }
            if (TransitMap.HomeSelected)
            {
                TransitMap.HomeSelected = false;
                TransitMap.RefreshTravelButton();
            }
            TransitUI.Select(location);
            TransitMap.Refresh();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (locked) return;
            hover = true;
            TransitMap.Refresh();
        }

        public void OnPointerExit(PointerEventData e)
        {
            hover = false;
            TransitMap.Refresh();
        }
    }

    // 이동 버튼을 이미 줄였다는 표시 (+ 원래 가운데)
    internal class ResizedMark : MonoBehaviour
    {
        public float center;
    }

    // 복제한 [돌아가기] 버튼: 마우스를 올리면 이동 버튼처럼 노랗게
    internal class HoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image outer;
        public TMP_Text label;

        public void OnPointerEnter(PointerEventData e) => Set(TransitStyle.BUTTON_BORDER_HOVER);
        public void OnPointerExit(PointerEventData e) => Set(TransitStyle.BUTTON_BORDER);

        private void Set(Color c)
        {
            if (outer != null) outer.color = c;
            if (label != null) label.color = c;
        }
    }

    // 지도 빈 곳 클릭 = 선택 해제
    internal class BackgroundClick : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || e.dragging) return;
            AfterhourLocation sel = TransitMap.SelectedLocation;
            if (sel != null) TransitUI.Select(sel); // 같은 장소를 다시 고르면 바닐라가 선택을 푼다
            if (TransitMap.HomeSelected)
            {
                TransitMap.HomeSelected = false;
                TransitMap.RefreshTravelButton();
            }
            TransitMap.Refresh();
        }
    }

    // "내 전당포"를 골랐을 때: 이동 버튼을 켜 두고, 누르면 돌아갈지 묻는다
    [HarmonyPatch(typeof(TransitUI), "RefreshTravelButton")]
    internal static class TravelButtonPatch
    {
        private static void Postfix()
        {
            if (!TransitMap.HomeSelected) return;
            try
            {
                TransitFrame frame = Traverse.Create(typeof(TransitUI)).Field("frame").GetValue<TransitFrame>();
                if (frame == null) return;
                if (frame.travelButton != null) frame.travelButton.interactable = true;
                if (frame.travelGroup != null) frame.travelGroup.alpha = 1f;
            }
            catch
            {
            }
        }
    }

    [HarmonyPatch(typeof(TransitUI), "OnTravelClicked")]
    internal static class TravelClickPatch
    {
        private static bool Prefix()
        {
            if (!TransitMap.HomeSelected) return true;
            TransitMap.AskReturnHome();
            return false;
        }
    }

    [HarmonyPatch(typeof(TransitUI), nameof(TransitUI.Open))]
    internal static class TransitOpenPatch
    {
        private static void Postfix()
        {
            try
            {
                TransitMap.Build();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MapExtension] map build failed: " + ex);
            }
        }
    }
}
