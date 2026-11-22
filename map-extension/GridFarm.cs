using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapExtension
{
    // ===== 격자 수색 (테스트: 약국) =====
    // 수색처에 들어가면 회색 판 위에 바둑판 칸이 깔린다. 안 보이는 칸은 진한 회색 네모.
    //  - 시야: 내가 선 칸 + 상하좌우(십자) 칸이 보인다 (벽 너머는 안 보임). 걸어가는 동안 계속 밝혀진다.
    //  - 이동: 공짜. 보이는 칸이면 어디든 (적이 버틴 칸, 안 보이는 칸, 벽은 못 지나감).
    //  - 방(GridRoom): 여러 칸 묶음. 테두리는 벽, 문으로만 드나든다. 들어가면 방 전체가 보인다.
    //    잠긴 문은 도구(키카드 등)가 있어야 열린다 (한 번 열면 계속 열림).
    //  - 상자: 그 칸에 선 채로 한 번 더 누르면 뒤진다 (처음 1턴). 턴 = 기력 1 당 TurnsPerEnergy.
    //  - 적(빨간 사람): 바로 옆(벽 없이 붙은 칸)에서 누르면 싸운다 (GridCombat.cs). 이기면 그 칸으로 들어간다.
    //  - 적을 전부 치우면 "제압" (나중에 분점 개조 조건).
    // 칸 배치는 장소마다 한 번 무작위(씨앗)로 정해서 세이브에. 칸 상태/위치/턴도 외출 장소 카운터로 세이브에.
    internal enum CellKind { Empty, Loot, Locked, Enemy, Entrance }

    internal sealed class GridCellData
    {
        public CellKind Kind;
        public FarmSpot Loot;   // 물건 칸의 전리품 정의
        public int Group;       // 적 칸: 적 무리 종류 (GridCombat.Group)
        public int Room = -1;   // 속한 방 번호 (-1 = 방 밖)
    }

    // 방: 칸 (X, Y) 부터 W x H (Y 는 아래가 0). 문 = 방 안 칸(DoorIn) ↔ 방 밖 칸(DoorOut)
    internal sealed class GridRoom
    {
        public string Name;
        public int X, Y, W, H;
        public Vector2Int DoorIn, DoorOut;
        public bool Locked;          // 잠긴 문
        public string[] NeedsAny;    // 잠긴 문을 여는 도구 (이 중 하나가 외출 가방에)
        public string NeedsText;
        public bool ConsumesTool;    // 열 때 도구 1개를 쓴다
        public FarmSpot[] Loot;      // 방 안 상자들
        public int[] Enemies;        // 방 안 적 무리들 (GridCombat.Group 번호)

        public bool Contains(int c, int r) => c >= X && c < X + W && r >= Y && r < Y + H;
    }

    internal class GridFarmLocation : FarmLocation
    {
        public int Cols = 8, Rows = 5;
        public Rect GridArea = new Rect(0.308f, 0.42f, 0.384f, 0.32f); // 배경 그림 비율 (왼쪽 아래 기준). 가운데, 바닥 창 위
        public FarmSpot[] LootSpots;  // 방 밖 물건 칸 전리품 (돌려 가며 배정)
        public int EnemyCells = 3, LootCells = 6; // 방 밖
        public int[] EnemyGroups = { 0, 0, 1, 2 }; // 방 밖 적: 입구에서 가까운 적부터 이 순서로
        public List<GridRoom> Rooms = new List<GridRoom>();
        internal const int TurnsPerEnergy = 6;
        public bool Purchasable = true; // 부동산 구매 가능한 곳 (목표: 적을 모두 처리 → 살 수 있음)
        public int DetectRange = 2; // 적이 나를 알아채는 거리 (벽/닫힌 문 너머로는 못 봄, 길 따라 칸 수)

        // 적 (움직이는 개체): 처음 자리는 칸 배치의 적 칸. 자리/죽음/눈치/멈춤은 세이브 카운터 e<k>/ed<k>/ea<k>/es<k>
        //  - 내가 한 칸 걸을 때마다 적도 움직인다: 알아챈 적은 나를 향해 한 칸. 바로 옆이 되면 전투(기습)
        //  - 도망치거나 지면 그 적은 잠깐(2~3걸음) 멈춘다
        private sealed class Foe { public int Home, Group; }
        private List<Foe> foes = new List<Foe>();
        private GameObject[] foeIcons;
        private MarkerMover[] foeMovers;
        private Vector2[] cellPos; // 칸 가운데 (UI 층 좌표)
        private int ambush = -1;   // 걷다가 기습한 적 (걸음이 끝나면 전투)

        // ---- 디버그 (테스트 끝나면 false) ----
        //  - 들어갈 때 Player.log 에 칸 배치 그림 (ASCII)
        //  - 클릭마다 로그 (칸 번호, 종류, 상태, 내 위치)
        //  - F9: 전부 보기 켜기/끄기 (그림만, 세이브는 안 바뀜) / F10: 이 장소 격자 처음부터 (새 배치)
        internal const bool GridDebug = true;
        private static bool debugShowAll;

        // 칸 상태 (세이브): 0 안 보임, 1 보임, 2 적 처치. 키 앞머리 = P (배치 규칙이 바뀌면 올려서 옛 기록과 안 섞이게)
        private const int Hidden = 0, Seen = 1, Cleared = 2;
        private const string P = "g2_";

        // ---- 모양 (참고 그림: 회색 판, 진회색 칸, 밝은 회색 방, 흰 아이콘, 빨간 적, 점선 격자) ----
        private static readonly Color BoardColor = new Color32(0x6a, 0x6a, 0x6a, 0xf0);
        private static readonly Color HiddenColor = new Color32(0x44, 0x44, 0x44, 0xff);
        private static readonly Color HiddenEdge = new Color32(0x58, 0x58, 0x58, 0xff);
        private static readonly Color RoomColor = new Color32(0x9a, 0x9a, 0x9a, 0xff);
        private static readonly Color RoomEdge = new Color32(0x5c, 0x5c, 0x5c, 0xff);
        private static readonly Color DoorColor = new Color32(0xc4, 0xc4, 0xc4, 0xff);
        private static readonly Color DoorLockedColor = new Color32(0xff, 0x9f, 0x43, 0xff);
        private static readonly Color DotColor = new Color32(0x4a, 0x4a, 0x4a, 0xc0);
        private static readonly Color IconWhite = new Color32(0xee, 0xee, 0xee, 0xff);
        private static readonly Color EnemyRed = new Color32(0xff, 0x00, 0x00, 0xff);
        private static readonly Color LockedOrange = new Color32(0xff, 0x9f, 0x43, 0xff);
        private const float Gap = 5f; // 칸 사이 (UI 단위)

        private GridCellData[] cells;
        private GridCell[] cellObjs;
        private Image[] cellImgs;
        private Outline[] cellEdges;
        private GameObject[] boxIcons;
        private readonly List<(int a, int b, GameObject line)> dotLines = new List<(int, int, GameObject)>();
        private readonly List<(int room, GameObject floor)> roomFloors = new List<(int, GameObject)>();
        private readonly List<(int room, Image door)> doorBars = new List<(int, Image)>();
        private TextMeshProUGUI statusText;
        private RectTransform marker;
        private MarkerMover mover;
        private readonly Dictionary<int, (GameItem box, GameInventory inv)> boxes = new Dictionary<int, (GameItem, GameInventory)>();
        internal bool InCombat;

        private int Count => Cols * Rows;
        private int Idx(int c, int r) => r * Cols + c;
        private int Idx(Vector2Int v) => Idx(v.x, v.y);
        private int Col(int i) => i % Cols;
        private int Row(int i) => i / Cols;
        private int Entrance => Idx(Cols / 2, 0);

        private int Counter(string k, int def) => ModHelper.GetAfterhourCounter(Id, P + k, def);
        private void SetCounter(string k, int v) => ModHelper.SetAfterhourCounter(Id, P + k, v);
        private int State(int i) => cells[i].Kind == CellKind.Entrance ? Seen : Counter(i.ToString(), Hidden);
        private int ShownState(int i) { int s = State(i); return debugShowAll && s == Hidden ? Seen : s; } // 그림용
        private void SetState(int i, int s) => SetCounter(i.ToString(), s);
        internal int Turns
        {
            get => Counter("turns", 0);
            set => SetCounter("turns", Mathf.Max(0, value));
        }
        // 지금 서 있는 칸. 처음 = 입구
        internal int Pos
        {
            get { int p = Counter("pos", -1); return p >= 0 && p < Count ? p : Entrance; }
            set => SetCounter("pos", value);
        }
        internal bool Subdued => ModHelper.GetAfterhourCounter(Id, "subdued", 0) == 1;
        // 문은 처음엔 닫혀 있다 (시야/이동 막음). 문 앞에서 눌러 연다 (잠긴 문은 도구 필요). 한 번 열면 계속 열림
        private bool DoorOpen(int room) => Counter("door_" + room, 0) == 1;

        private int FoePos(int k) { int p = Counter("e" + k, -2); return p == -2 ? foes[k].Home : p; }
        private void SetFoePos(int k, int p) => SetCounter("e" + k, p);
        private bool FoeDead(int k) => Counter("ed" + k, 0) == 1 || State(foes[k].Home) == Cleared; // 옛 기록(칸 처치)도
        private bool FoeAware(int k) => Counter("ea" + k, 0) == 1;
        private int FoeStun(int k) => Counter("es" + k, 0);

        // 그 칸에 서 있는 살아 있는 적 (없으면 -1)
        private int EnemyAt(int i)
        {
            for (int k = 0; k < foes.Count; k++) if (!FoeDead(k) && FoePos(k) == i) return k;
            return -1;
        }

        // ---- 칸 배치 (씨앗으로 늘 같은 배치) ----
        private int layoutSeed;

        private void EnsureLayout()
        {
            int seed = Counter("seed", 0);
            if (cells != null && (seed == 0 || seed == layoutSeed)) return; // 다른 세이브를 불러오면 씨앗이 달라짐 → 다시 배치
            if (seed == 0)
            {
                seed = UnityEngine.Random.Range(1, int.MaxValue);
                SetCounter("seed", seed);
            }
            layoutSeed = seed;
            var rng = new System.Random(seed);
            cells = new GridCellData[Count];
            for (int i = 0; i < Count; i++) cells[i] = new GridCellData { Kind = CellKind.Empty };
            cells[Entrance].Kind = CellKind.Entrance;

            // 방: 칸 표시 + 방 안 내용물 (방 안에서만 무작위 자리)
            for (int ri = 0; ri < Rooms.Count; ri++)
            {
                GridRoom room = Rooms[ri];
                var inside = new List<int>();
                for (int r = room.Y; r < room.Y + room.H; r++)
                    for (int c = room.X; c < room.X + room.W; c++)
                        if (c >= 0 && r >= 0 && c < Cols && r < Rows) { cells[Idx(c, r)].Room = ri; inside.Add(Idx(c, r)); }
                Shuffle(inside, rng);
                int k = 0;
                if (room.Enemies != null)
                    foreach (int g in room.Enemies)
                        if (k < inside.Count) { cells[inside[k]].Kind = CellKind.Enemy; cells[inside[k]].Group = g; k++; }
                if (room.Loot != null)
                    foreach (FarmSpot s in room.Loot)
                        if (k < inside.Count) { cells[inside[k]].Kind = CellKind.Loot; cells[inside[k]].Loot = s; k++; }
            }

            // 방 밖: 문 앞 칸과 입구는 비운다
            var reserved = new HashSet<int> { Entrance };
            foreach (GridRoom room in Rooms) reserved.Add(Idx(room.DoorOut));
            var free = new List<int>();
            for (int i = 0; i < Count; i++) if (cells[i].Room < 0 && !reserved.Contains(i)) free.Add(i);
            Shuffle(free, rng);

            // 적: 입구와 두 칸 이상 떨어진 곳. 가까운 적일수록 약한 무리
            var enemyAt = new List<int>();
            foreach (int i in new List<int>(free))
            {
                if (enemyAt.Count >= EnemyCells) break;
                if (Dist(i, Entrance) < 2) continue;
                enemyAt.Add(i);
                free.Remove(i);
            }
            enemyAt.Sort((a, b) => Dist(a, Entrance).CompareTo(Dist(b, Entrance)));
            for (int k = 0; k < enemyAt.Count; k++)
            {
                cells[enemyAt[k]].Kind = CellKind.Enemy;
                cells[enemyAt[k]].Group = EnemyGroups[Mathf.Min(k, EnemyGroups.Length - 1)];
            }
            // 물건
            for (int k = 0; k < LootCells && k < free.Count; k++)
            {
                cells[free[k]].Kind = CellKind.Loot;
                cells[free[k]].Loot = LootSpots != null && LootSpots.Length > 0 ? LootSpots[k % LootSpots.Length] : null;
            }
            // 적 개체 (칸 번호 순서 = 세이브 번호)
            foes = new List<Foe>();
            for (int i = 0; i < Count; i++)
                if (cells[i].Kind == CellKind.Enemy) foes.Add(new Foe { Home = i, Group = cells[i].Group });
        }

        private static void Shuffle(List<int> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
        }

        private int Dist(int a, int b) => Mathf.Abs(Col(a) - Col(b)) + Mathf.Abs(Row(a) - Row(b));

        private IEnumerable<int> Neighbors4(int i)
        {
            int c = Col(i), r = Row(i);
            if (c > 0) yield return Idx(c - 1, r);
            if (c < Cols - 1) yield return Idx(c + 1, r);
            if (r > 0) yield return Idx(c, r - 1);
            if (r < Rows - 1) yield return Idx(c, r + 1);
        }

        // 두 칸 사이 문 (없으면 -1 = 방 번호)
        private int DoorBetween(int a, int b)
        {
            for (int ri = 0; ri < Rooms.Count; ri++)
            {
                int din = Idx(Rooms[ri].DoorIn), dout = Idx(Rooms[ri].DoorOut);
                if ((a == din && b == dout) || (a == dout && b == din)) return ri;
            }
            return -1;
        }

        // 벽: 서로 다른 방(또는 방 안/밖)인 이웃 칸 사이. 문이면 벽 아님
        private bool Wall(int a, int b) => cells[a].Room != cells[b].Room && DoorBetween(a, b) < 0;

        // 지나갈 수 있는 이웃 사이 (벽 없고, 문이면 열려 있어야)
        private bool Passable(int a, int b)
        {
            if (Wall(a, b)) return false;
            int d = DoorBetween(a, b);
            return d < 0 || DoorOpen(d);
        }

        private bool Adjacent(int a, int b) => Dist(a, b) == 1 && Passable(a, b);

        // 밟고 지나갈 수 있는 칸 = 보이고, 적이 서 있지 않은 칸
        private bool Open(int i) => State(i) != Hidden && EnemyAt(i) < 0;

        // from → to 길 (from 제외, to 포함). 열린 칸만, 벽/잠긴 문은 못 지나감. 없으면 빈 목록
        private List<int> Path(int from, int to)
        {
            var path = new List<int>();
            if (from == to || !Open(to)) return path;
            var prev = new Dictionary<int, int> { [from] = -1 };
            var q = new Queue<int>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                if (c == to) break;
                foreach (int n in Neighbors4(c))
                {
                    if (prev.ContainsKey(n) || !Open(n) || !Passable(c, n)) continue;
                    prev[n] = c;
                    q.Enqueue(n);
                }
            }
            if (!prev.ContainsKey(to)) return path;
            for (int c = to; c != from; c = prev[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        // 시야: 그 칸 + 벽 없이 붙은 십자 칸. 방 안에 들어서면 방 전체
        private void Reveal(int at)
        {
            if (State(at) == Hidden) SetState(at, Seen);
            foreach (int n in Neighbors4(at)) if (Passable(at, n) && State(n) == Hidden) SetState(n, Seen);
            int room = cells[at].Room;
            if (room >= 0)
                for (int i = 0; i < Count; i++)
                    if (cells[i].Room == room && State(i) == Hidden) SetState(i, Seen);
        }

        private int EnemiesLeft()
        {
            int n = 0;
            for (int k = 0; k < foes.Count; k++) if (!FoeDead(k)) n++;
            return n;
        }

        // 적이 from 에서 to 까지 가는 길 (보이든 말든 지도는 안다. 벽/닫힌 문/다른 적은 못 지나감). 없으면 null
        private List<int> FoePath(int from, int to, int self)
        {
            var prev = new Dictionary<int, int> { [from] = -1 };
            var q = new Queue<int>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                if (c == to) break;
                foreach (int n in Neighbors4(c))
                {
                    if (prev.ContainsKey(n) || !Passable(c, n)) continue;
                    int other = EnemyAt(n);
                    if (n != to && other >= 0 && other != self) continue;
                    prev[n] = c;
                    q.Enqueue(n);
                }
            }
            if (!prev.ContainsKey(to)) return null;
            var path = new List<int>();
            for (int c = to; c != from; c = prev[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        // 적 차례 (내가 한 칸 걸은 뒤): 알아채기 → 다가오기. 바로 옆이 된 적이 있으면 그 번호 (기습), 없으면 -1
        private int EnemiesAct()
        {
            int me = Pos, hit = -1;
            for (int k = 0; k < foes.Count; k++)
            {
                if (FoeDead(k)) continue;
                int at = FoePos(k);
                if (FoeStun(k) > 0) { SetCounter("es" + k, FoeStun(k) - 1); continue; }
                List<int> path = FoePath(at, me, k);
                if (path == null) continue;
                if (!FoeAware(k))
                {
                    if (path.Count > DetectRange) continue;
                    SetCounter("ea" + k, 1);
                    if (State(at) != Hidden) Notify("적이 이쪽을 알아챘다!");
                    if (GridDebug) Debug.Log("[MapExtension] grid foe " + k + " aware at " + at + " (dist " + path.Count + ")");
                }
                if (path.Count <= 1) { if (hit < 0) hit = k; continue; } // 이미 바로 옆
                int next = path[0];
                if (next == me || EnemyAt(next) >= 0) continue;
                SetFoePos(k, next);
                MoveFoeIcon(k, next);
                if (path.Count == 2 && hit < 0) hit = k; // 한 칸 다가와서 바로 옆
            }
            return hit;
        }

        private void MoveFoeIcon(int k, int to)
        {
            if (foeMovers == null || k >= foeMovers.Length || foeMovers[k] == null || cellPos == null) return;
            foeMovers[k].Walk(new List<(Vector3, Action)> { (cellPos[to], null) }, null);
        }

        // 망원경: 칸이 전부 보이고 적도 다 치워야
        internal override bool AllCleared()
        {
            EnsureLayout();
            for (int i = 0; i < Count; i++) if (State(i) == Hidden) return false;
            return EnemiesLeft() == 0;
        }

        // ---- 무대: 칸마다 클릭 판정(월드 콜라이더). 그림은 UI 층(BuildOverlay) ----
        private float cellWorldW, cellWorldH;

        protected override void BuildExtra(GameObject stage, float imgW, float imgH, float px)
        {
            EnsureLayout();
            cellObjs = new GridCell[Count];
            cellWorldW = GridArea.width * imgW / Cols;
            cellWorldH = GridArea.height * imgH / Rows;
            for (int i = 0; i < Count; i++)
            {
                float fx = GridArea.x + (Col(i) + 0.5f) * GridArea.width / Cols;
                float fy = GridArea.y + (Row(i) + 0.5f) * GridArea.height / Rows;
                var go = new GameObject("Cell_" + i);
                go.transform.SetParent(stage.transform, false);
                go.transform.localPosition = new Vector3((fx - 0.5f) * imgW, (fy - 0.5f) * imgH, 0f);
                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(cellWorldW * 0.95f, cellWorldH * 0.95f);
                var inter = go.AddComponent<Interactable>();
                inter.isControlable = false;
                var gc = go.AddComponent<GridCell>();
                gc.owner = this;
                gc.index = i;
                go.AddComponent<TooltipContainer>();
                cellObjs[i] = gc;
            }
        }

        protected override void BuildOverlay(RectTransform layer, Func<Vector3, Vector2?> toLocal)
        {
            EnsureLayout();
            if (cellObjs == null) return;
            cellImgs = new Image[Count];
            cellEdges = new Outline[Count];
            boxIcons = new GameObject[Count];
            dotLines.Clear();
            roomFloors.Clear();
            doorBars.Clear();

            // 칸 크기/자리 (UI 단위)
            var at = new Vector2[Count];
            for (int i = 0; i < Count; i++)
            {
                Vector2? v = toLocal(cellObjs[i].transform.position);
                if (!v.HasValue) return;
                at[i] = v.Value;
            }
            cellPos = at;
            Vector2 step = new Vector2(Mathf.Abs(at[1].x - at[0].x), Mathf.Abs(at[Cols].y - at[0].y));
            float cs = Mathf.Min(step.x, step.y) - Gap;
            Vector2 cell = new Vector2(step.x - Gap, step.y - Gap);

            // 1) 바탕 회색 판
            Vector2 mid = (at[0] + at[Count - 1]) * 0.5f;
            Vector2 boardSize = new Vector2(Mathf.Abs(at[Count - 1].x - at[0].x) + step.x + Gap, Mathf.Abs(at[Count - 1].y - at[0].y) + step.y + Gap);
            GridArt.Rect(layer, "Board", mid, boardSize, BoardColor);

            // 2) 점선 격자 (보이는 칸끼리, 벽 아닌 곳)
            for (int i = 0; i < Count; i++)
                foreach (int n in Neighbors4(i))
                {
                    if (n < i || Wall(i, n)) continue;
                    bool vertical = Row(i) == Row(n); // 가로로 붙음 → 세로 점선
                    Vector2 p = (at[i] + at[n]) * 0.5f;
                    Vector2 size = vertical ? new Vector2(1.5f, step.y) : new Vector2(step.x, 1.5f);
                    GameObject line = GridArt.Dotted(layer, p, size, vertical, DotColor);
                    dotLines.Add((i, n, line));
                }

            // 3) 방 바닥 + 이름
            for (int ri = 0; ri < Rooms.Count; ri++)
            {
                GridRoom room = Rooms[ri];
                Vector2 a = at[Idx(room.X, room.Y)], b = at[Idx(room.X + room.W - 1, room.Y + room.H - 1)];
                Vector2 size = new Vector2(Mathf.Abs(b.x - a.x) + step.x - Gap, Mathf.Abs(b.y - a.y) + step.y - Gap);
                GameObject floor = GridArt.Rect(layer, "Room_" + room.Name, (a + b) * 0.5f, size, RoomColor);
                var ol = floor.AddComponent<Outline>();
                ol.effectColor = RoomEdge;
                ol.effectDistance = new Vector2(2f, -2f);
                var label = new GameObject("Name", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                label.transform.SetParent(floor.transform, false);
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                label.alignment = TextAlignmentOptions.Center;
                label.enableWordWrapping = false;
                label.raycastTarget = false;
                label.fontSize = cs * 0.22f;
                label.color = new Color32(0x10, 0x10, 0x10, 0xff);
                SetText(label, room.Name);
                roomFloors.Add((ri, floor));
            }

            // 4) 칸 (안 보이는 칸 덮개) + 아이콘
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("Cell_" + i, typeof(RectTransform));
                go.transform.SetParent(layer, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = cell;
                rt.localPosition = at[i];
                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                cellImgs[i] = img;
                var ol = go.AddComponent<Outline>();
                ol.effectDistance = new Vector2(1.5f, -1.5f);
                cellEdges[i] = ol;
                boxIcons[i] = GridArt.Box(rt, cs, IconWhite);
            }

            // 5) 문 (벽 위 밝은 막대)
            for (int ri = 0; ri < Rooms.Count; ri++)
            {
                int din = Idx(Rooms[ri].DoorIn), dout = Idx(Rooms[ri].DoorOut);
                bool vertical = Row(din) == Row(dout);
                Vector2 p = (at[din] + at[dout]) * 0.5f;
                Vector2 size = vertical ? new Vector2(Gap * 1.6f, cell.y * 0.8f) : new Vector2(cell.x * 0.8f, Gap * 1.6f);
                GameObject door = GridArt.Rect(layer, "Door_" + ri, p, size, DoorColor);
                var ol = door.AddComponent<Outline>();
                ol.effectColor = RoomEdge;
                ol.effectDistance = new Vector2(1f, -1f);
                doorBars.Add((ri, door.GetComponent<Image>()));
            }

            // 5-1) 건물 출입구: 입구 칸 아래 판 가장자리에 문 막대 + "입구" 글자 + 밖을 가리키는 화살표
            {
                Vector2 e = at[Entrance];
                float edgeY = mid.y - boardSize.y * 0.5f; // 판 아래 끝
                GameObject gate = GridArt.Rect(layer, "Entrance", new Vector2(e.x, edgeY + Gap * 0.3f), new Vector2(cell.x * 0.8f, Gap * 1.6f), DoorColor);
                var gol = gate.AddComponent<Outline>();
                gol.effectColor = RoomEdge;
                gol.effectDistance = new Vector2(1f, -1f);
                var lgo = new GameObject("EntranceLabel", typeof(RectTransform));
                lgo.transform.SetParent(layer, false);
                var lt = lgo.AddComponent<TextMeshProUGUI>();
                lt.alignment = TextAlignmentOptions.Top;
                lt.enableWordWrapping = false;
                lt.raycastTarget = false;
                lt.fontSize = cs * 0.2f;
                lt.color = IconWhite;
                var lrt = lt.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
                lrt.pivot = new Vector2(0.5f, 1f);
                lrt.sizeDelta = new Vector2(cell.x * 1.5f, cs * 0.35f);
                lrt.localPosition = new Vector2(e.x, edgeY - Gap * 0.8f);
                SetText(lt, "▼ 입구");
            }

            // 6) 적 (빨간 사람, 움직임)
            foeIcons = new GameObject[foes.Count];
            foeMovers = new MarkerMover[foes.Count];
            for (int k = 0; k < foes.Count; k++)
            {
                foeIcons[k] = GridArt.Person(layer, cs, EnemyRed);
                foeIcons[k].transform.localPosition = at[FoePos(k)];
                foeMovers[k] = foeIcons[k].AddComponent<MarkerMover>();
            }

            // 7) 나 (흰 사람)
            var me = GridArt.Person(layer, cs, IconWhite);
            marker = (RectTransform)me.transform;
            mover = me.AddComponent<MarkerMover>();
            marker.localPosition = at[Pos];

            // 8) 상태 줄: 판 위
            var sgo = new GameObject("Status", typeof(RectTransform));
            sgo.transform.SetParent(layer, false);
            statusText = sgo.AddComponent<TextMeshProUGUI>();
            statusText.alignment = TextAlignmentOptions.BottomLeft;
            statusText.enableWordWrapping = false;
            statusText.raycastTarget = false;
            statusText.fontSize = 9f;
            statusText.color = IconWhite;
            var srt = statusText.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0f, 0f);
            srt.sizeDelta = new Vector2(500f, 34f);
            srt.localPosition = new Vector2(mid.x - boardSize.x * 0.5f, mid.y + boardSize.y * 0.5f + 2f);
            RefreshGrid();
        }

        internal void RefreshGrid()
        {
            if (cells == null || cellImgs == null) return;
            for (int i = 0; i < Count; i++)
            {
                if (cellImgs[i] == null) continue;
                int st = ShownState(i);
                CellKind k = cells[i].Kind;
                bool hidden = st == Hidden;
                cellImgs[i].color = hidden ? HiddenColor : new Color(0f, 0f, 0f, 0f);
                cellEdges[i].effectColor = hidden ? HiddenEdge : new Color(0f, 0f, 0f, 0f);

                bool box = !hidden && (k == CellKind.Loot || k == CellKind.Locked || boxes.ContainsKey(i));
                boxIcons[i].SetActive(box);
                if (box)
                {
                    bool fresh = (k == CellKind.Loot || k == CellKind.Locked) && !Looted(i);
                    Color c = fresh ? (k == CellKind.Locked ? LockedOrange : IconWhite)
                                    : BoxHasItems(i) ? new Color(IconWhite.r, IconWhite.g, IconWhite.b, 0.7f) : new Color(IconWhite.r, IconWhite.g, IconWhite.b, 0.25f);
                    GridArt.Tint(boxIcons[i], c);
                }
            }
            foreach (var (a, b, line) in dotLines)
                if (line != null) line.SetActive(ShownState(a) != Hidden && ShownState(b) != Hidden);
            foreach (var (room, floor) in roomFloors)
            {
                bool any = false;
                for (int i = 0; i < Count && !any; i++) if (cells[i].Room == room && ShownState(i) != Hidden) any = true;
                if (floor != null) floor.SetActive(any);
            }
            foreach (var (room, door) in doorBars)
            {
                if (door == null) continue;
                bool show = ShownState(Idx(Rooms[room].DoorIn)) != Hidden || ShownState(Idx(Rooms[room].DoorOut)) != Hidden;
                door.gameObject.SetActive(show);
                // 닫힘 = 진한 막대 (잠긴 문은 주황), 열림 = 흐린 막대
                door.color = DoorOpen(room) ? new Color(DoorColor.r, DoorColor.g, DoorColor.b, 0.3f) : Rooms[room].Locked ? DoorLockedColor : DoorColor;
            }
            if (foeIcons != null)
                for (int k = 0; k < foeIcons.Length && k < foes.Count; k++)
                {
                    if (foeIcons[k] == null) continue;
                    bool show = !FoeDead(k) && ShownState(FoePos(k)) != Hidden;
                    foeIcons[k].SetActive(show);
                    if (show) foeIcons[k].transform.SetAsLastSibling();
                }
            if (marker != null) marker.SetAsLastSibling();
            if (statusText != null)
            {
                // 1줄: 목표 / 2줄: 수색처 상태
                int left = EnemiesLeft(), seen = 0, boxTotal = 0, boxDone = 0;
                for (int i = 0; i < Count; i++)
                {
                    if (State(i) != Hidden) seen++;
                    if (cells[i].Kind == CellKind.Loot || cells[i].Kind == CellKind.Locked) { boxTotal++; if (Looted(i)) boxDone++; }
                }
                string goal;
                if (Purchasable)
                    goal = left > 0
                        ? "<color=#ffcf6b>부동산 구매 가능한 곳</color> : 적을 모두 처리하세요. <color=#ff8a6b>(남은 적 " + left + "/" + foes.Count + ")</color>"
                        : "<color=#4df0e0>부동산 구매 가능한 곳 : 제압 완료. 이 건물을 살 수 있다.</color>";
                else
                    goal = left > 0 ? "적을 조심하세요. <color=#ff8a6b>(남은 적 " + left + ")</color>" : "<color=#4df0e0>안전하다.</color>";
                string s = "<size=125%>" + goal + "</size>\n"
                           + "<color=#c8c8c8>탐색 " + seen + "/" + Count + "칸 · 상자 " + boxDone + "/" + boxTotal
                           + " · 남은 턴 " + Turns + " (상자 1턴, 기력 1 = " + TurnsPerEnergy + "턴)</color>"
                           + (GridDebug ? " <color=#ffcf6b>[디버그 F9 전부 보기" + (debugShowAll ? " 켜짐" : "") + " · F10 초기화]</color>" : "");
                SetText(statusText, s);
            }
        }

        public override void Arrive()
        {
            EnsureLayout();
            Reveal(Pos); // 들어서면 내 칸 + 십자 칸이 보인다
            base.Arrive();
            RefreshGrid();
            if (GridDebug)
            {
                DumpLayout("arrive");
                if (cellObjs != null && cellObjs[0] != null && cellObjs[0].GetComponent<GridDebugKeys>() == null)
                    cellObjs[0].gameObject.AddComponent<GridDebugKeys>().owner = this;
            }
        }

        // Player.log 에 칸 배치 (맨 윗줄부터). 괄호 = 아직 안 보임
        //  @ 나  E 입구  B 상자(b 뒤짐)  X<무리> 적(x 처치)  . 빈칸. 방 칸은 뒤에 방 이름 첫 글자
        internal void DumpLayout(string why)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("[MapExtension] grid ").Append(Id).Append(" (").Append(why).Append(") seed ").Append(layoutSeed)
                  .Append(" pos ").Append(Pos).Append(" turns ").Append(Turns).Append(" enemies ").Append(EnemiesLeft()).AppendLine();
                for (int r = Rows - 1; r >= 0; r--)
                {
                    sb.Append("  ");
                    for (int c = 0; c < Cols; c++)
                    {
                        int i = Idx(c, r);
                        int st = State(i);
                        CellKind k = cells[i].Kind;
                        int fe = EnemyAt(i);
                        string ch = i == Pos ? "@" : fe >= 0 ? "X" + foes[fe].Group + (FoeAware(fe) ? "!" : "")
                                  : k == CellKind.Entrance ? "E" : k == CellKind.Loot ? (Looted(i) ? "b" : "B") : ".";
                        if (cells[i].Room >= 0) ch += Rooms[cells[i].Room].Name.Substring(0, 1);
                        if (st == Hidden) ch = "(" + ch + ")";
                        sb.Append(ch.PadRight(7));
                    }
                    sb.AppendLine();
                }
                for (int ri = 0; ri < Rooms.Count; ri++)
                    sb.Append("  room ").Append(ri).Append(' ').Append(Rooms[ri].Name).Append(" door ").Append(Rooms[ri].DoorIn).Append("-").Append(Rooms[ri].DoorOut)
                      .Append(DoorOpen(ri) ? " open" : Rooms[ri].Locked ? " closed+LOCKED" : " closed").AppendLine();
                Debug.Log(sb.ToString());
            }
            catch (Exception ex) { Debug.LogWarning("[MapExtension] grid dump: " + ex.Message); }
        }

        internal void DebugToggleShowAll()
        {
            debugShowAll = !debugShowAll;
            Notify("디버그: 전부 보기 " + (debugShowAll ? "켜짐" : "꺼짐"));
            RefreshGrid();
        }

        // 이 장소 격자 기록을 지우고 새 배치로 (씨앗/칸/위치/턴/상자/문/제압)
        internal void DebugReset()
        {
            if (Busy || InCombat) return;
            for (int i = 0; i < Count; i++) { SetCounter(i.ToString(), Hidden); SetCounter("loot_" + i, 0); }
            for (int ri = 0; ri < Rooms.Count; ri++) SetCounter("door_" + ri, 0);
            for (int k = 0; k < foes.Count; k++) { SetCounter("e" + k, -2); SetCounter("ed" + k, 0); SetCounter("ea" + k, 0); SetCounter("es" + k, 0); }
            SetCounter("pos", -1);
            SetCounter("turns", 0);
            SetCounter("seed", 0);
            ModHelper.SetAfterhourCounter(Id, "subdued", 0);
            foreach (var kv in boxes) { try { kv.Value.box?.contentWindow?.Hide(); kv.Value.box?.Destroy(); } catch { } }
            boxes.Clear();
            cells = null;
            EnsureLayout();
            Reveal(Pos);
            if (marker != null && cellImgs != null && cellImgs[Pos] != null) marker.localPosition = cellImgs[Pos].rectTransform.localPosition;
            if (foeIcons != null && cellPos != null)
                for (int k = 0; k < foeIcons.Length && k < foes.Count; k++)
                    if (foeIcons[k] != null) foeIcons[k].transform.localPosition = cellPos[FoePos(k)];
            Notify("디버그: 격자 초기화 (새 배치)");
            RefreshGrid();
            DumpLayout("reset");
        }

        public override void Leave()
        {
            GridCombat.Close();
            InCombat = false;
            Busy = false;
            var keep = new HashSet<GameItem>();
            foreach (var kv in boxes)
            {
                try { kv.Value.box?.contentWindow?.Hide(); } catch { }
                try { foreach (GameItem g in kv.Value.inv.childItems) keep.Add(g); } catch { }
            }
            LootCover.RemoveOutside(keep);
            base.Leave();
        }

        // ---- 칸 툴팁 / 클릭 ----
        // 닫힌 문 너머 칸이면 그 방 번호와 문 앞 칸 (멀리서 눌러도 문 앞까지 걸어가서 연다)
        private int ClosedDoorBehind(int i, out int front)
        {
            front = -1;
            for (int ri = 0; ri < Rooms.Count; ri++)
            {
                if (DoorOpen(ri)) continue;
                int din = Idx(Rooms[ri].DoorIn), dout = Idx(Rooms[ri].DoorOut);
                if (i == din && cells[Pos].Room != ri) { front = dout; return ri; }
                if (i == dout && cells[Pos].Room == ri) { front = din; return ri; }
            }
            return -1;
        }

        internal string Tooltip(int i)
        {
            if (cells == null) return "";
            int st = State(i);
            CellKind k = cells[i].Kind;
            string roomName = cells[i].Room >= 0 ? Rooms[cells[i].Room].Name + "\n" : "";
            int door = ClosedDoorBehind(i, out int front);
            if (door >= 0)
            {
                GridRoom r = Rooms[door];
                return r.Name + " 문" + (r.Locked ? " (잠김)\n<color=#ff9f43>" + r.NeedsText + "</color>" : " (닫힘)")
                       + "\n<color=#9a9aa8>클릭: " + (Pos == front ? "문 열기" : "문 앞으로 가서 열기") + "</color>";
            }
            if (st == Hidden) return "보이지 않는 곳\n<color=#9a9aa8>가까이 가면 보인다.</color>";
            int fe = EnemyAt(i);
            if (fe >= 0)
                return roomName + "<color=#ff8a6b>적" + (FoeAware(fe) ? " (나를 쫓고 있다)" : "") + "</color>\n<color=#9a9aa8>"
                       + (Adjacent(Pos, i) ? "클릭: 싸우기" : "바로 옆 칸에서 싸울 수 있다. 가까이 가면(" + DetectRange + "칸) 알아채고 쫓아온다.") + "</color>";
            string here = i == Pos ? "지금 여기 서 있다.\n" : "";
            if (k == CellKind.Loot || k == CellKind.Locked || boxes.ContainsKey(i))
            {
                string nm = cells[i].Loot != null ? cells[i].Loot.Name : k == CellKind.Enemy ? "쓰러진 적" : "상자";
                bool fresh = (k == CellKind.Loot || k == CellKind.Locked) && !Looted(i);
                string act = i == Pos ? (fresh ? "클릭: 뒤지기 (1턴)" : "클릭: 상자 열기") : "클릭: 이동";
                return roomName + here + nm + (fresh ? "" : " (뒤짐)") + "\n<color=#9a9aa8>" + act + "</color>";
            }
            if (i == Pos) return roomName + "지금 여기 서 있다." + (k == CellKind.Entrance ? " (입구: 여기서 나갈 수 있다)" : "");
            return roomName + (k == CellKind.Entrance ? "입구\n" : "") + "<color=#9a9aa8>클릭: 이동</color>";
        }

        internal void Click(int i)
        {
            if (GridDebug)
                Debug.Log("[MapExtension] grid click " + i + " (" + Col(i) + "," + Row(i) + ") kind " + (cells != null ? cells[i].Kind.ToString() : "?")
                          + " state " + (cells != null ? State(i) : -1) + " room " + (cells != null ? cells[i].Room : -1) + " pos " + Pos
                          + (Busy ? " BUSY" : "") + (InCombat ? " COMBAT" : "") + (Dialogs.IsOpen ? " DIALOG" : "") + (GridCombat.IsOpen ? " COMBATWIN" : ""));
            if (cells == null || Busy || InCombat || Dialogs.IsOpen || GridCombat.IsOpen) return; // 창 위 클릭이 뒤 칸까지 가지 않게
            int door = ClosedDoorBehind(i, out int front);
            if (door >= 0)
            {
                if (Pos == front) { OpenDoor(door); return; }
                List<int> toDoor = Path(Pos, front);
                if (toDoor.Count == 0) { Notify("문까지 갈 길이 없다."); return; }
                int d = door;
                WalkTo(toDoor, () => OpenDoor(d)); // 문 앞까지 걸어가서 연다
                return;
            }
            int st = State(i);
            CellKind k = cells[i].Kind;
            if (st == Hidden) { Notify("아직 안 보이는 곳이다. 가까이 가면 보인다."); return; }
            int fe = EnemyAt(i);
            if (fe >= 0)
            {
                if (!Adjacent(Pos, i)) { Notify("바로 옆 칸까지 가야 싸울 수 있다."); return; }
                if (PlayerStore.Instance.healthData.isWoundedFresh) { Notify(LocHelper.GetLocalizedMechanic("afterhour_attempts_wounded")); return; }
                Fight(fe);
                return;
            }
            if (i == Pos) { Interact(i); return; }
            List<int> path = Path(Pos, i);
            if (path.Count == 0) { Notify("막혀서 갈 수 없다."); return; }
            WalkTo(path);
        }

        // 문 열기 (문 앞에서). 잠긴 문은 도구가 있어야 (쓰는 문이면 1개 소모). 한 번 열면 계속 열림. 열면 문 너머가 보인다
        private void OpenDoor(int room)
        {
            GridRoom r = Rooms[room];
            if (r.Locked)
            {
                var probe = new FarmSpot { NeedsAny = r.NeedsAny, NeedsText = r.NeedsText };
                if (!HasTool(probe)) { Notify(r.NeedsText ?? "잠겨 있다."); return; }
                if (r.ConsumesTool) ConsumeTool(probe);
            }
            SetCounter("door_" + room, 1);
            try { AudioManager.Instance.Play2("click_vintage"); } catch { }
            Notify(r.Name + " 문을 열었다.");
            Reveal(Pos);
            RefreshGrid();
        }

        // 길을 따라 걸어간다 (공짜). 한 칸 도착할 때마다 시야가 밝혀진다
        private void WalkTo(List<int> path, Action then = null)
        {
            ambush = -1;
            if (mover == null || cellImgs == null)
            {
                foreach (int c in path)
                {
                    Pos = c;
                    Reveal(c);
                    ambush = EnemiesAct();
                    if (ambush >= 0) break;
                }
                AfterWalk(then);
                return;
            }
            Busy = true;
            var steps = new List<(Vector3, Action)>();
            foreach (int c in path)
            {
                int cell = c;
                if (cellImgs[cell] == null) continue;
                steps.Add((cellImgs[cell].rectTransform.localPosition, () =>
                {
                    Pos = cell;
                    Reveal(cell);
                    ambush = EnemiesAct();
                    if (ambush >= 0) mover.StopAfterStep(); // 적이 바로 옆 → 남은 걸음 취소
                    RefreshGrid();
                }));
            }
            try { AudioManager.Instance.Play2("click_vintage"); } catch { }
            mover.Walk(steps, () => { Busy = false; AfterWalk(then); });
        }

        private void AfterWalk(Action then)
        {
            RefreshGrid();
            CheckDone();
            if (ambush >= 0)
            {
                int k = ambush;
                ambush = -1;
                Notify("적이 덮쳐 온다!");
                Fight(k);
                return;
            }
            then?.Invoke();
        }

        // 턴 1개 쓰기. 없으면 기력 1 → TurnsPerEnergy 턴 (확인 창)
        private void UseTurnThen(Action then)
        {
            if (Turns > 0) { Turns--; then(); return; }
            int now = ScavHelper.GetScavTimeLeft();
            if (now < 1) { Notify("기력이 없다. 오늘은 여기까지."); return; }
            Dialogs.Confirm("<b>턴이 없다</b>\n\n기력 1을 써서 " + TurnsPerEnergy + "턴을 얻는다.\n남은 기력: " + now + " → <color=#ff5a5a>" + (now - 1) + "</color>",
                "계속", "취소", () =>
                {
                    PlayerStore.Instance.scavengingAttempts += 1;
                    Turns = TurnsPerEnergy - 1;
                    RefreshGrid();
                    then();
                });
        }

        // 상자 뒤짐 여부 (세이브)
        private bool Looted(int i) => Counter("loot_" + i, 0) == 1;

        // 서 있는 칸에서 상호작용: 안 뒤진 상자면 뒤지고(1턴), 이미 뒤진 상자면 다시 연다
        private void Interact(int i)
        {
            CellKind k = cells[i].Kind;
            if ((k == CellKind.Loot || k == CellKind.Locked) && !Looted(i))
            {
                if (k == CellKind.Locked && !HasTool(cells[i].Loot)) { Notify(cells[i].Loot.NeedsText); return; }
                if (PlayerStore.Instance.healthData.isWoundedFresh) { Notify(LocHelper.GetLocalizedMechanic("afterhour_attempts_wounded")); return; }
                UseTurnThen(() =>
                {
                    if (k == CellKind.Locked) ConsumeTool(cells[i].Loot);
                    SetCounter("loot_" + i, 1);
                    try { AudioManager.Instance.Play2("debris"); } catch { }
                    LootCell(i, cells[i].Loot, false);
                    if (BoxHasItems(i)) OpenBox(i);
                    RefreshGrid();
                });
                return;
            }
            if (boxes.ContainsKey(i)) OpenBox(i);
        }

        // 전리품: 정의대로 굴려 그 칸 상자에 넣는다 (여는 건 Interact)
        private void LootCell(int i, FarmSpot def, bool fromFight)
        {
            var found = new List<GameItem>();
            if (def != null)
            {
                int n = fromFight ? 1 : UnityEngine.Random.Range(Mathf.Max(1, def.MinItems), Mathf.Max(def.MinItems, def.MaxItems) + 1);
                if (!fromFight && def.Empty > 0f && RNG.Roll(def.Empty)) n = 0;
                for (int k = 0; k < n; k++)
                {
                    GameItem g = FarmHotspot.RollSpot(def);
                    if (g != null) found.Add(g);
                }
            }
            if (found.Count == 0) { if (!fromFight) Notify("아무것도 없었다."); return; }
            var (box, inv) = Box(i, def != null ? def.Name : Name);
            foreach (GameItem g in found)
                if (GraphUtils.TryAcceptAll(inv, g) != 0) GraphUtils.TryAcceptAll(EmporiumEntry.Instance.afterhourInventory, g);
            foreach (GameItem g in found) LootCover.Attach(g);
            if (fromFight) Notify("쓰러진 적이 뭔가 떨어뜨렸다. 그 칸에서 한 번 더 누르면 연다.");
        }

        private (GameItem, GameInventory) Box(int i, string title)
        {
            if (boxes.TryGetValue(i, out var b) && b.box != null) return b;
            (PixelWindow win, GameInventory inv) = DirectoryUtils.CreateInventoryWindow(8, 8);
            try { win.SetTitle(title); } catch { }
            GameItem box = ItemDirectory.CreateEmptyItem("vme_search_box").SetContentWindow(win).SetSpriteAndShape("Items/items_container", "evidence_box");
            box.name = title;
            box.EnableTag("IS_OWNED_TAG");
            boxes[i] = (box, inv);
            return (box, inv);
        }

        private bool BoxHasItems(int i)
        {
            try { return boxes.TryGetValue(i, out var b) && b.inv != null && b.inv.childItems.Count > 0; }
            catch { return false; }
        }

        private void OpenBox(int i)
        {
            if (!boxes.TryGetValue(i, out var b) || b.box == null || b.box.contentWindow == null) { Notify("이미 다 뒤졌다."); return; }
            b.box.contentWindow.Show();
            b.box.contentWindow.Center();
            LootCover.EnsureCovers(b.inv.childItems);
            try { AudioManager.Instance.Play2("click_vintage"); } catch { }
        }

        // 적 k 와 전투. 이기면 쓰러진 자리에 가끔 떨군 물건. 도망/패배면 그 적은 잠깐 멈춘다
        private void Fight(int k)
        {
            InCombat = true;
            int at = FoePos(k);
            SetCounter("ea" + k, 1);
            GridCombat.Start(foes[k].Group, result =>
            {
                InCombat = false;
                if (result == GridCombat.Result.Won)
                {
                    SetCounter("ed" + k, 1);
                    if (RNG.Roll(60f)) LootCell(at, EnemyLoot, true);
                }
                else if (result == GridCombat.Result.Lost)
                {
                    Turns = 0; // 쓰러짐 → 오늘 수색 끝
                    SetCounter("es" + k, 3);
                }
                else SetCounter("es" + k, 2); // 도망: 적이 잠깐 놓친다
                RefreshGrid();
                CheckDone();
            });
        }

        private static readonly FarmSpot EnemyLoot = new FarmSpot { Name = "쓰러진 적", Tables = new[] { "junkTable", "medicalTable", "packedFoodTable" }, MinItems = 1, MaxItems = 1 };

        private void CheckDone()
        {
            if (!Subdued && EnemiesLeft() == 0)
            {
                ModHelper.SetAfterhourCounter(Id, "subdued", 1);
                Notify(Name + "을(를) 제압했다! 이제 여기는 안전하다.");
                RefreshGrid();
            }
            RefreshAll(); // 망원경
        }

        private static void SetText(TextMeshProUGUI t, string s)
        {
            if (t == null) return;
            if (string.IsNullOrEmpty(s)) { t.text = ""; return; }
            ScriptFontSwap.Apply(t, s);
        }

        // 이런 건물은 출입구 칸에 서 있어야 나갈 수 있다 (MapUIManager.Return 패치)
        internal bool CanLeave() => cells == null || Pos == Entrance;

        internal static void Notify(string text)
        {
            try { StoreUIManager.Instance.Notify(text); } catch { Debug.Log("[MapExtension] " + text); }
        }
    }

    // ---- 평면 그림 (사람 = 동그라미 머리 + 둥근 몸, 상자 = 뚜껑 + 몸통, 점선). 그림 파일 없이 코드로 ----
    internal static class GridArt
    {
        private static Sprite circle, rounded, dotH, dotV;
        private static readonly Color IconEdge = new Color32(0x55, 0x55, 0x55, 0xff);

        private static Sprite Circle()
        {
            if (circle != null) return circle;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return circle;
        }

        // 둥근 네모 (9칸 늘이기, 모서리 반지름 8px)
        private static Sprite Rounded()
        {
            if (rounded != null) return rounded;
            const int n = 32;
            const float rad = 8f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, rad, n - rad), cy = Mathf.Clamp(y + 0.5f, rad, n - rad);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(rad - d + 0.5f) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            rounded = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(rad, rad, rad, rad));
            return rounded;
        }

        // 점선 (타일 반복): 점 2px + 빈칸 4px
        private static Sprite Dots(bool vertical)
        {
            if (vertical && dotV != null) return dotV;
            if (!vertical && dotH != null) return dotH;
            int w = vertical ? 2 : 6, h = vertical ? 6 : 2;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int along = vertical ? y : x;
                    px[y * w + x] = along < 2 ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            if (vertical) dotV = s; else dotH = s;
            return s;
        }

        internal static GameObject Dotted(Transform parent, Vector2 pos, Vector2 size, bool vertical, Color c)
        {
            GameObject go = Rect(parent, vertical ? "DotV" : "DotH", pos, size, c);
            var img = go.GetComponent<Image>();
            img.sprite = Dots(vertical);
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = 1f / 1.5f; // 점 크기 약 3 UI 단위
            return go;
        }

        // 단색 네모 (UI 층, 가운데 기준 자리/크기)
        internal static GameObject Rect(Transform parent, string name, Vector2 pos, Vector2 size, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.localPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return go;
        }

        private static Image Part(Transform parent, string name, Sprite sp, Vector2 pos, Vector2 size, Color c, float radius = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = sp;
            img.color = c;
            img.raycastTarget = false;
            if (sp != null && sp == rounded && radius > 0f)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 8f / radius; // 모서리 8px → radius UI 단위
            }
            var ol = go.AddComponent<Outline>(); // 아이콘 어두운 테두리
            ol.effectColor = IconEdge;
            ol.effectDistance = new Vector2(1f, -1f);
            return img;
        }

        private static RectTransform Holder(Transform parent, string name, float cs)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cs, cs);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        internal static GameObject Person(Transform parent, float cs, Color c)
        {
            RectTransform h = Holder(parent, "Person", cs);
            Part(h, "Head", Circle(), new Vector2(0f, cs * 0.12f), new Vector2(cs * 0.28f, cs * 0.28f), c);
            Rounded();
            Part(h, "Body", rounded, new Vector2(0f, -cs * 0.16f), new Vector2(cs * 0.4f, cs * 0.24f), c, cs * 0.06f);
            return h.gameObject;
        }

        internal static GameObject Box(Transform parent, float cs, Color c)
        {
            RectTransform h = Holder(parent, "Box", cs);
            Part(h, "Lid", null, new Vector2(0f, cs * 0.15f), new Vector2(cs * 0.36f, cs * 0.08f), c);
            Part(h, "Body", null, new Vector2(0f, -cs * 0.06f), new Vector2(cs * 0.36f, cs * 0.28f), c);
            return h.gameObject;
        }

        internal static void Tint(GameObject icon, Color c)
        {
            if (icon == null) return;
            foreach (Image img in icon.GetComponentsInChildren<Image>(true)) img.color = c;
            foreach (Outline ol in icon.GetComponentsInChildren<Outline>(true))
                ol.effectColor = new Color(IconEdge.r, IconEdge.g, IconEdge.b, c.a);
        }
    }

    // 말 걷기: 칸마다 StepTime 초, 걸음마다 살짝 통통 튄다. 칸에 도착할 때마다 그 칸 동작(시야 밝히기)
    internal class MarkerMover : MonoBehaviour
    {
        private const float StepTime = 0.14f, Hop = 0.18f;
        private readonly Queue<(Vector3 pos, Action arrive)> steps = new Queue<(Vector3, Action)>();
        private Vector3 from, to;
        private Action arrive, done;
        private float t = 1f;
        private bool walking;

        // 지금 걸음까지만 걷고 나머지는 취소 (기습 등). 끝 동작(onDone)은 그대로 불린다
        internal void StopAfterStep() => steps.Clear();

        internal void Walk(List<(Vector3, Action)> path, Action onDone)
        {
            steps.Clear();
            foreach (var p in path) steps.Enqueue(p);
            done = onDone;
            arrive = null;
            t = 1f;
            walking = true;
        }

        private void Update()
        {
            if (!walking) return;
            if (t >= 1f)
            {
                try { arrive?.Invoke(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] step: " + ex.Message); }
                arrive = null;
                if (steps.Count == 0)
                {
                    walking = false;
                    transform.localScale = Vector3.one;
                    Action d = done;
                    done = null;
                    try { d?.Invoke(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] walk done: " + ex.Message); }
                    return;
                }
                var s = steps.Dequeue();
                from = transform.localPosition;
                to = s.pos;
                arrive = s.arrive;
                t = 0f;
            }
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / StepTime);
            float e = t * t * (3f - 2f * t); // 부드럽게
            transform.localPosition = Vector3.Lerp(from, to, e);
            float k = 1f + Hop * Mathf.Sin(t * Mathf.PI);
            transform.localScale = new Vector3(k, k, 1f);
        }

        // 걷다가 장소를 떠나는 등 꺼지면: 남은 걸음 동작은 바로 다 처리
        private void OnDisable()
        {
            if (!walking) return;
            walking = false;
            try { arrive?.Invoke(); while (steps.Count > 0) steps.Dequeue().arrive?.Invoke(); done?.Invoke(); } catch { }
            arrive = null;
            done = null;
        }
    }

    // 디버그 키 (GridFarmLocation.GridDebug): F9 전부 보기 / F10 초기화. 격자 첫 칸 오브젝트에 붙는다 (장소에 있을 때만 돈다)
    internal class GridDebugKeys : MonoBehaviour
    {
        public GridFarmLocation owner;

        private void Update()
        {
            if (owner == null || !GridFarmLocation.GridDebug) return;
            try
            {
                if (Input.GetKeyDown(KeyCode.F9)) owner.DebugToggleShowAll();
                if (Input.GetKeyDown(KeyCode.F10)) owner.DebugReset();
            }
            catch (Exception ex) { Debug.LogWarning("[MapExtension] grid debug key: " + ex.Message); }
        }
    }

    // [돌아가기]: 격자 장소에서는 출입구 칸에 있을 때만. 아니면 막고 알림 (밤이 끝나 강제로 돌아가는 건 안 막음)
    [HarmonyLib.HarmonyPatch(typeof(MapUIManager), nameof(MapUIManager.Return))]
    internal static class GridLeavePatch
    {
        private static bool Prefix(MapUIManager __instance)
        {
            try
            {
                if (__instance.Current is GridFarmLocation g && !g.CanLeave() && !g.InCombat)
                {
                    GridFarmLocation.Notify("출입구에서만 나갈 수 있다. 입구 칸으로 가자.");
                    return false;
                }
            }
            catch (Exception ex) { Debug.LogWarning("[MapExtension] grid leave: " + ex.Message); }
            return true;
        }
    }

    // 칸 하나의 클릭 판정 + 툴팁 (월드)
    internal class GridCell : TooltipInterface, IClickable
    {
        public GridFarmLocation owner;
        public int index;
        public override bool IsTooltipEnabled() => owner != null;
        public override string GetTooltip() => owner != null ? owner.Tooltip(index) : "";
        public void OnClick()
        {
            try { owner?.Click(index); } catch (Exception ex) { Debug.LogWarning("[MapExtension] grid click: " + ex); }
        }
    }

    // 장소 정의
    internal static class GridFarmData
    {
        internal static GridFarmLocation Pharmacy()
        {
            var cabinet = new FarmSpot { Key = "cabinet", Name = "약장", Tables = new[] { "medicalTable" }, MinItems = 2, MaxItems = 4, Empty = 5f,
                Items = new[] { "bandage_item:30", "topical_bandage_item:20", "hemostatic_bandage_item:10", "rubbing_alcohol:20", "salve:12", "smelling_salt:8", "phagimycin_pill:6", "pink_injector:3" }, ItemChance = 75f };
            var counter = new FarmSpot { Key = "counter", Name = "조제실 카운터", Tables = new[] { "householdTable", "junkTable" }, MinItems = 2, MaxItems = 3, Empty = 5f,
                Items = new[] { "caffeine_pill:25", "phagimycin_pill:15", "unlicensed_phagimycin_pill:8", "birth_control_pill:12", "bandage_item:15", "chem_scanner:2", "surgery_tool:3" }, ItemChance = 55f };
            var shelf = new FarmSpot { Key = "shelf", Name = "벽면 선반", Tables = new[] { "packedFoodTable", "materialTable", "medicalTable" }, MinItems = 2, MaxItems = 4, Empty = 5f,
                Items = new[] { "bandage_item:20", "topical_bandage_item:15", "rubbing_alcohol:20", "salve:10", "blood_bag:4" }, ItemChance = 40f };
            var display = new FarmSpot { Key = "trash", Name = "진열대", Tables = new[] { "junkTable" }, MinItems = 1, MaxItems = 3, Empty = 10f };
            var vault = new FarmSpot { Key = "safe", Name = "지하 창고 상자", Tables = new[] { "medicalTable", "toolTable", "allModuleTable" }, MinItems = 3, MaxItems = 5, Empty = 0f };

            // 8 x 5, 입구 = 아래 줄 가운데 (4,0)
            return new GridFarmLocation
            {
                Key = "pharmacy", Name = "버려진 약국", BackgroundFile = "bg_farm_pharmacy.png",
                Desc = "셔터가 반쯤 뜯겨 나간 동네 약국. 요즘 깡패들이 드나든다는 소문이 있다.",
                Cols = 8, Rows = 5, EnemyCells = 3, LootCells = 6,
                EnemyGroups = new[] { 0, 0, 1 },
                LootSpots = new[] { shelf, display, counter, display },
                Rooms = new List<GridRoom>
                {
                    // 제약실: 오른쪽 2x2, 문은 왼쪽 벽
                    new GridRoom { Name = "제약실", X = 5, Y = 2, W = 2, H = 2, DoorIn = new Vector2Int(5, 2), DoorOut = new Vector2Int(4, 2),
                        Loot = new[] { cabinet, cabinet }, Enemies = new[] { 1 } },
                    // 지하실: 왼쪽 위 2x1, 문은 아래 벽. 원래는 의무부 키카드 잠금이지만 테스트라 열려 있음 (Locked = false)
                    new GridRoom { Name = "지하실", X = 0, Y = 4, W = 2, H = 1, DoorIn = new Vector2Int(1, 4), DoorOut = new Vector2Int(1, 3),
                        Locked = false, NeedsAny = new[] { "med_keycard", "cmd_keycard" }, ConsumesTool = true,
                        NeedsText = "의무부 키카드 잠금장치다. 의무부 키카드(또는 사령부 키카드)가 외출 가방에 있어야 한다.",
                        Loot = new[] { vault, vault }, Enemies = new[] { 2 } },
                },
            };
        }
    }
}
