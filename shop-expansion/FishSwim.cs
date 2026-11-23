using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShopExpansion
{
    // 양식장 방 배경의 큰 유리 수조 안에서, 대형 수조에 실제로 들어 있는 물고기들이 헤엄친다.
    //  - 그림 = 그 물고기 아이템의 게임 그림, 크기 = 수조 창 안에서 보이는 크기와 같게 (칸 수 x 게임 칸 크기).
    //  - 알/치어/치어 무리는 그리지 않는다. 죽은 물고기는 배를 뒤집고 위쪽에 떠 있다.
    //  - 종류별 속도: 틸라피아 빠름, 장어 중간 + 꿈틀, 철갑상어 느림.
    //  - 수조 내용이 바뀌면(넣기/빼기/성장/죽음) 0.5초 안에 반영.
    //  - 공기원근법: 물고기마다 앞뒤 깊이(무작위), 뒤일수록 푸르스름 + 살짝 흐림, 뒤에 있는 물고기부터 그림.
    //  - 유리 글씨: 대형 수조 기계 툴팁 내용(nakedText)을 흰 글씨로 수조 왼쪽 위에, 0.5초마다 갱신.
    //  - 물방울: 바닥 4곳에서 줄지어 올라오고, 물고기 입에서도 가끔. 수면 근처에서 흐려져 사라짐.
    // 덮개(VSE_RoomCover) 안에 있어 메인 인벤/창보다 뒤에 그려지고, 클릭은 받지 않는다.
    internal class FishSwim : MonoBehaviour
    {
        // 그릴 영역: 배경 유리 수조 (RoomPanel 수조 자리와 같은 곳, 1920x1080 기준 아래 왼쪽 원점)
        internal Rect Area = new Rect(435f, 370f, 1027f, 443f);
        private const float Margin = 12f;

        private class Swimmer
        {
            public GameItem Item;
            public RectTransform Rt;
            public Image Img;
            public string Sprite;
            public Vector2 Pos, Target, Size;
            public float Speed, Wiggle, Phase, Pause;
            public bool FacingRight, Dead;
            public float Depth; // 0 = 앞(유리 쪽), 1 = 뒤 (공기원근법: 뒤일수록 푸르스름)
        }

        private readonly Dictionary<GameItem, Swimmer> swimmers = new Dictionary<GameItem, Swimmer>();
        private float nextSync;
        private static FishSwim active;

        private void OnEnable() { nextSync = 0f; active = this; }

        // 화면 좌표 아래에 있는 물고기 (겹치면 앞(덜 깊은) 물고기). 툴팁용 (TankTooltip)
        internal static GameItem FishAt(Vector2 screen)
        {
            if (active == null || !active.isActiveAndEnabled) return null;
            Swimmer best = null;
            foreach (Swimmer s in active.swimmers.Values)
            {
                if (s.Rt == null || s.Img == null || s.Img.sprite == null) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(s.Rt, screen, null)) continue;
                if (best == null || s.Depth < best.Depth) best = s;
            }
            return best?.Item;
        }

        // ---- 물방울: 바닥 몇 군데에서 줄지어 올라오고, 가끔 물고기 입에서도 뽀글. 물고기보다 뒤(먼저 그림) ----
        private class Bubble
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Pos;
            public float Speed, Drift, Phase, Size, Life;
        }

        private const int MaxBubbles = 60;
        private readonly List<Bubble> bubbles = new List<Bubble>();
        private RectTransform bubbleLayer;
        private static Sprite bubbleSprite;
        private float[] vents;          // 바닥에서 물방울이 나오는 x 위치
        private float nextVent;

        private void EnsureBubbleLayer()
        {
            if (bubbleLayer != null) return;
            var go = new GameObject("Bubbles", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling(); // 물고기 뒤
            bubbleLayer = (RectTransform)go.transform;
            bubbleLayer.anchorMin = bubbleLayer.anchorMax = bubbleLayer.pivot = Vector2.zero;
            bubbleLayer.sizeDelta = new Vector2(1920f, 1080f);
            vents = new float[4];
            for (int i = 0; i < vents.Length; i++)
                vents[i] = Mathf.Lerp(Area.xMin + 60f, Area.xMax - 60f, (i + UnityEngine.Random.Range(0.1f, 0.9f)) / vents.Length);
        }

        // 픽셀 아트 동그라미 (테두리 진하게, 안은 살짝, 왼쪽 위 반짝)
        private static Sprite BubbleSprite()
        {
            if (bubbleSprite != null) return bubbleSprite;
            const int n = 9;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    Color col = Color.clear;
                    if (d <= c + 0.3f && d > c - 1.1f) col = new Color(0.85f, 1f, 1f, 0.85f);      // 테두리
                    else if (d <= c - 1.1f) col = new Color(0.7f, 0.95f, 1f, 0.18f);               // 안
                    if (x == 2 && y == n - 3) col = new Color(1f, 1f, 1f, 0.95f);                    // 반짝
                    tex.SetPixel(x, y, col);
                }
            tex.Apply();
            bubbleSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 1f);
            return bubbleSprite;
        }

        private void SpawnBubble(Vector2 at, float size)
        {
            if (bubbles.Count >= MaxBubbles) return;
            var go = new GameObject("Bubble", typeof(RectTransform));
            go.transform.SetParent(bubbleLayer, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = BubbleSprite();
            img.raycastTarget = false;
            bubbles.Add(new Bubble
            {
                Rt = rt, Img = img, Pos = at, Size = size,
                Speed = UnityEngine.Random.Range(35f, 60f) * (0.7f + size / 30f),
                Drift = UnityEngine.Random.Range(4f, 10f),
                Phase = UnityEngine.Random.value * 6f,
            });
        }

        private void UpdateBubbles(float dt)
        {
            EnsureBubbleLayer();
            // 바닥 구멍: 0.15~0.5초마다 한 곳에서 1개
            if (Time.unscaledTime >= nextVent)
            {
                nextVent = Time.unscaledTime + UnityEngine.Random.Range(0.15f, 0.5f);
                float x = vents[UnityEngine.Random.Range(0, vents.Length)] + UnityEngine.Random.Range(-6f, 6f);
                SpawnBubble(new Vector2(x, Area.yMin + 6f), Mathf.Round(UnityEngine.Random.Range(2f, 4f)) * 4f);
            }
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                Bubble b = bubbles[i];
                b.Life += dt;
                b.Phase += dt * 3f;
                b.Pos.y += b.Speed * dt;
                float x = b.Pos.x + Mathf.Sin(b.Phase) * b.Drift;
                // 수면 근처에서 흐려지다 사라짐
                float top = Area.yMax - 8f;
                float fade = Mathf.Clamp01((top - b.Pos.y) / 40f);
                if (b.Pos.y >= top || b.Rt == null)
                {
                    if (b.Rt != null) Destroy(b.Rt.gameObject);
                    bubbles.RemoveAt(i);
                    continue;
                }
                b.Rt.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(b.Pos.y));
                b.Img.color = new Color(1f, 1f, 1f, fade);
            }
        }

        private void OnDisable()
        {
            foreach (Bubble b in bubbles) if (b.Rt != null) Destroy(b.Rt.gameObject);
            bubbles.Clear();
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextSync)
            {
                nextSync = Time.unscaledTime + 0.5f;
                try { Sync(); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] fish sync: " + ex.Message); }
                try { UpdateGlassText(); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] glass text: " + ex.Message); }
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            foreach (Swimmer s in swimmers.Values) Move(s, dt);
            try { UpdateBubbles(dt); } catch (Exception ex) { Debug.LogWarning("[ShopExpansion] bubbles: " + ex.Message); }
        }

        // ---- 수조 내용과 맞추기 ----
        private void Sync()
        {
            GameItem tank = Rooms.InRoom ? AquaTank.Get(Rooms.Current) : null;
            GameGridInventory grid = tank != null ? MachineRAS.GetTankGrid(tank) : null;
            var seen = new HashSet<GameItem>();
            if (grid != null)
            {
                foreach (GameItem i in grid.childItems)
                {
                    if (!Drawable(i)) continue;
                    seen.Add(i);
                    if (!swimmers.TryGetValue(i, out Swimmer s)) swimmers[i] = s = Create(i);
                    Refresh(s);
                }
            }
            var gone = new List<GameItem>();
            foreach (var kv in swimmers) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (GameItem g in gone)
            {
                if (swimmers[g].Rt != null) Destroy(swimmers[g].Rt.gameObject);
                swimmers.Remove(g);
            }
            if (sortDirty) SortByDepth();
        }

        // ---- 공기원근법: 뒤에 있을수록 물빛(푸르스름)이 섞이고 살짝 흐려진다. 뒤에 있는 물고기를 먼저(아래에) 그린다 ----
        private static readonly Color DeepWater = new Color(0.45f, 0.72f, 0.92f, 1f);
        private const float MaxHaze = 0.65f;   // 맨 뒤 물고기에 섞이는 물빛 비율
        private bool sortDirty;

        private static Color DepthTint(float depth)
        {
            Color c = Color.Lerp(Color.white, DeepWater, depth * MaxHaze);
            c.a = Mathf.Lerp(1f, 0.8f, depth);
            return c;
        }

        private void SortByDepth()
        {
            sortDirty = false;
            var list = new List<Swimmer>(swimmers.Values);
            list.Sort((a, b) => b.Depth.CompareTo(a.Depth)); // 뒤(깊은 쪽)부터
            int baseIndex = bubbleLayer != null ? 1 : 0;      // 물방울 층은 맨 뒤
            for (int k = 0; k < list.Count; k++)
                if (list[k].Rt != null) list[k].Rt.SetSiblingIndex(baseIndex + k);
        }

        // ---- 유리에 적힌 글씨: 대형 수조 기계 툴팁 내용을 흰 글씨로 (물고기 앞, 왼쪽 위) ----
        private TextMeshProUGUI glassText;
        private const float GlassPad = 22f, GlassW = 460f, GlassFont = 15f;

        private void UpdateGlassText()
        {
            if (glassText == null)
            {
                var go = new GameObject("GlassText", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                glassText = go.AddComponent<TextMeshProUGUI>();
                TMP_FontAsset font = FontLoader.Instance != null ? (FontLoader.Instance.notoSans_Normal ?? FontLoader.Instance.notoSans) : null;
                if (font != null) glassText.font = font;
                glassText.fontSize = GlassFont;
                glassText.color = new Color(1f, 1f, 1f, 0.85f);
                glassText.alignment = TextAlignmentOptions.TopLeft;
                glassText.enableWordWrapping = true;
                glassText.richText = false;
                glassText.raycastTarget = false;
                var rt = glassText.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(Area.xMin + GlassPad, Area.yMax - GlassPad);
                rt.sizeDelta = new Vector2(GlassW, Area.height - GlassPad * 2f);
            }
            glassText.transform.SetAsLastSibling(); // 물고기 앞 (유리 위 글씨)
            GameItem tank = Rooms.InRoom ? AquaTank.Get(Rooms.Current) : null;
            string text = tank != null ? GlassLines(tank) : "";
            if (glassText.text != text) ScriptFontSwap.Apply(glassText, text);
        }

        // 유리 글씨 = 수조 상태를 직접 읽어 적는다 (툴팁을 잘라 쓰면 태그/가격 줄 구조가 달라 어긋났다, 0.8.7)
        //   필요 에너지 / (전기뱀장어 있을 때) 전기뱀장어 : 매일 NkJ 생성 중 / 물 / 오염도 / 개체 / 업그레이드
        private static string GlassLines(GameItem tank)
        {
            var sb = new System.Text.StringBuilder();
            int power = tank.GetTagReadonly("machinery_drawn_power").GetInt();
            sb.Append("필요 에너지: ").Append(power).Append("/일\n");
            GameGridInventory grid = MachineRAS.GetTankGrid(tank);
            int eel = grid != null ? AquacultureHelper.GetTankPowerOutput(grid) : 0;
            if (eel > 0) sb.Append("전기뱀장어 : 매일 ").Append(eel).Append("kJ 생성 중\n");
            GameItem water = MachineRAS.GetWaterSlot(tank)?.childItem;
            if (water == null) sb.Append("물탱크 없음\n");
            else
            {
                int ml = AquacultureHelper.GetWaterMl(water);
                int need = grid != null ? AquacultureHelper.GetTotalWaterNeedMl(grid) : 0;
                sb.Append("물: ").Append(ml.ToString("N0")).Append(" ml, 필요량 ").Append(need.ToString("N0")).Append(" ml\n");
                sb.Append("오염도: ").Append(AquacultureHelper.GetWastePercent(water).ToString("0.0")).Append("%\n");
            }
            if (grid != null)
            {
                int live = 0, dead = 0;
                foreach (GameItem c in grid.childItems)
                {
                    if (AquacultureHelper.IsLiveStock(c)) live++;
                    else if (AquacultureHelper.IsCarcass(c)) dead++;
                }
                sb.Append("개체: 생존 ").Append(live).Append("마리");
                if (dead > 0) sb.Append(", 죽음 ").Append(dead).Append("마리");
                sb.Append('\n');
            }
            int effUp = tank.GetTagReadonly("RAS_POWER_UPGRADE_COUNT_TAG").GetInt();
            sb.Append("업그레이드:\n효율 (전자 부품): ").Append(effUp).Append("/12");
            return sb.ToString();
        }

        private static bool Drawable(GameItem i)
        {
            if (i == null || !AquacultureHelper.IsFish(i)) return false;
            if (i.IsTag("FISH_ROE_TAG") || i.IsTag("FISH_FRY_TAG") || i.IsTag("FISH_GROUP_TAG")) return false;
            return true;
        }

        private Swimmer Create(GameItem i)
        {
            var go = new GameObject("Fish", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            var s = new Swimmer { Item = i, Rt = rt, Img = img, Phase = UnityEngine.Random.value * 10f, Depth = UnityEngine.Random.value };
            img.color = DepthTint(s.Depth);
            sortDirty = true;
            SpeedFor(i, s);
            s.Pos = RandomPoint(s);
            s.Target = RandomPoint(s);
            s.FacingRight = s.Target.x > s.Pos.x;
            return s;
        }

        // 그림/크기/죽음 상태 갱신 (자라면 그림과 칸 크기가 바뀐다)
        private void Refresh(Swimmer s)
        {
            GameItem i = s.Item;
            string key = i.spriteAtlasPath + "/" + i.spritePath;
            if (key != s.Sprite)
            {
                s.Sprite = key;
                try { s.Img.sprite = RenderHandler.LoadFromAtlas(i.spriteAtlasPath, i.spritePath); } catch { s.Img.sprite = null; }
            }
            // 수조 창 안과 같은 크기: 칸 수 x 게임 칸 크기(화면 픽셀) → 덮개 단위(1080 기준)
            float cell = CellInCoverUnits();
            int w = i.shape != null ? i.shape.width : 2, h = i.shape != null ? i.shape.height : 1;
            s.Size = new Vector2(Mathf.Max(w, h) * cell, Mathf.Min(w, h) * cell); // 세워 놓은 물고기도 가로로 헤엄
            s.Rt.sizeDelta = s.Size;
            bool dead = AquacultureHelper.IsDead(i);
            if (dead && !s.Dead) s.Target = new Vector2(s.Pos.x, Area.yMax - Margin - s.Size.y / 2f); // 위로 떠오른다
            s.Dead = dead;
        }

        private static float CellInCoverUnits()
        {
            float grid = 16f, sf = 1f;
            try { grid = UISettings.current.gridSize; } catch { }
            try { sf = RenderHandler.current.windowUICanvas.scaleFactor; } catch { }
            float px = grid * sf;                     // 화면 픽셀
            return px / (Screen.height / 1080f);      // 덮개 단위
        }

        // 종류별 속도 (덮개 단위/초)와 꿈틀거림
        private static void SpeedFor(GameItem i, Swimmer s)
        {
            string id = "";
            try { id = AquacultureHelper.GetSpecies(i)?.speciesID ?? ""; } catch { }
            float r = UnityEngine.Random.Range(0.8f, 1.2f);
            if (id.Contains("STURGEON")) { s.Speed = 28f * r; s.Wiggle = 3f; }
            else if (id.Contains("EEL")) { s.Speed = 45f * r; s.Wiggle = 9f; }
            else { s.Speed = 70f * r; s.Wiggle = 4f; } // 틸라피아 종류
        }

        private Vector2 RandomPoint(Swimmer s)
        {
            float hx = s.Size.x / 2f + Margin, hy = s.Size.y / 2f + Margin;
            return new Vector2(
                UnityEngine.Random.Range(Area.xMin + hx, Mathf.Max(Area.xMin + hx, Area.xMax - hx)),
                UnityEngine.Random.Range(Area.yMin + hy, Mathf.Max(Area.yMin + hy, Area.yMax - hy)));
        }

        private void Move(Swimmer s, float dt)
        {
            if (s.Rt == null) return;
            s.Phase += dt;
            if (s.Dead)
            {
                // 천천히 위로 떠올라 둥둥
                s.Pos = Vector2.MoveTowards(s.Pos, s.Target, 15f * dt);
                Place(s, Mathf.Sin(s.Phase * 0.8f) * 2f, 180f);
                return;
            }
            if (s.Pause > 0f) s.Pause -= dt;
            else
            {
                Vector2 to = s.Target - s.Pos;
                if (to.magnitude < 4f)
                {
                    s.Target = RandomPoint(s);
                    s.Pause = UnityEngine.Random.value < 0.3f ? UnityEngine.Random.Range(0.5f, 2f) : 0f; // 가끔 멈칫
                }
                else
                {
                    // 대체로 가로로 헤엄 (세로는 느리게)
                    Vector2 step = to.normalized * s.Speed * dt;
                    step.y *= 0.5f;
                    s.Pos += step;
                    if (Mathf.Abs(to.x) > 2f) s.FacingRight = to.x > 0f;
                }
            }
            float bob = Mathf.Sin(s.Phase * (s.Wiggle > 6f ? 4f : 1.6f)) * s.Wiggle;
            Place(s, bob, 0f);
            // 가끔 입에서 작은 물방울 (머리 = 바라보는 쪽 끝)
            if (bubbleLayer != null && UnityEngine.Random.value < dt * 0.15f)
            {
                float headX = s.Pos.x + (s.FacingRight ? 1f : -1f) * s.Size.x * 0.45f;
                SpawnBubble(new Vector2(headX, s.Pos.y + bob + s.Size.y * 0.1f), 8f);
            }
        }

        // 게임 물고기 그림은 왼쪽을 본다 → 오른쪽으로 갈 때 좌우 뒤집기
        private static void Place(Swimmer s, float bob, float rotZ)
        {
            s.Rt.anchoredPosition = new Vector2(s.Pos.x, s.Pos.y + bob);
            float sx = s.FacingRight ? -1f : 1f;
            s.Rt.localScale = new Vector3(sx, rotZ != 0f ? -1f : 1f, 1f); // 죽으면 배를 위로
        }
    }
}
