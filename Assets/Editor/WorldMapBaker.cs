using System;
using System.Collections.Generic;
using System.Linq;

namespace GnorpWar.Editor
{
    // 영토 지도 한 장을 만든다(손으로 고른 모양 — 사용자 10/25: 기획 시트 Captures/Map40/seeds_v3.png의 6번 = 「땅이 자라는 방식」 D, 씨앗 3886. 숫자를 바꾸면 다른 지도가 된다).
    // ① 대륙 — 가운데 타원 + 큰 · 중간 잡음, 가장자리에서 줄어듦. 땅 칸 수가 LandTarget이 되게 문턱을 고르고, 가장 큰 덩어리만 남기고 호수는 메운다
    // ② 땅 위에 서로 떨어진 무작위 점 40개 · 점마다 자라는 속도 · 칸마다 거친 비용 → 점에서부터 비용이 가장 싼 길로 자라 칸을 차지한다(최단 거리)
    // ③ 떨어진 조각을 정리하고, 땅 번호 = 점을 위에서부터 다섯 줄(8개씩) × 줄 안에서 왼쪽부터(01 = 북서) — 지형 표(WorldMapTable 8×5)의 칸이 그 땅의 지형(⑤에서 합친 땅은 남는 쪽의 지형)
    // ④ 판(지도 자리 — Canvas칸)의 왼쪽 위에 땅 상자를 붙이고, 북 · 서 여백은 가장자리 땅을 넓혀 판 끝까지 채운다(ExtendToFrame — 남 · 동만 바다와 맞닿는 해안, 10/25 「삼국지 지도처럼」).
    // ⑤ 작은 땅 10칸을 이웃에 합쳐 영주 땅 30칸으로 하고, 영주 땅 사이 경계를 따라 산맥 · 강 띠 10개(이벤트 땅 — 자리 고정)를 깎는다(WorldMapRidges, 10/26). 지형은 땅마다 Terrain
    // 내는 것: 칸마다 땅 번호(바다는 -1, 위 왼쪽이 0번 칸) · 국경을 MinBorder칸 이상 맞댄 땅끼리의 이웃 표(띠도 포함 — 영주 땅끼리만의 길은 WorldMapDefinition.ToGraph가 띠를 빼고 만든다) · 땅마다 아이콘을 놓을 자리 · 넓이 · 내접 반지름.
    // 기획 러너(scratchpad evsim/Gen.cs Growth)와 칸 단위로 같은 결과를 낸다 — 그 러너의 .NET PriorityQueue를 같은 순서로 도는 4갈래 힙(Heap)으로 옮겼다.
    // UnityEngine을 쓰지 않는다 — Unity 밖 검산(시안 생성기 · 규칙 러너)이 같은 코드를 읽는다
    internal class WorldMapBaker
    {
        public const int Sea = -1;
        // 이만큼(칸) 이상 국경을 맞대야 이어진 땅이다 — 짧게 스치는 접점은 길이 아니다
        public const int MinBorder = 6;
        private const int Seed = 3886;
        // 만드는 판 · 대륙의 땅 칸 수(10/25 첫 40칸 지도의 땅 칸 수 — 땅 하나 크기를 그대로 두려고) · 남 · 동 해안 밖으로 남기는 가장 작은 바다(칸)
        private const int SourceWidth = 660, SourceHeight = 360, LandTarget = 107743, Margin = 4;
        private const int Regions = 40;
        // 땅이 없는 여백 줄에서 해안이 흔들리는 폭(칸) — ExtendToFrame
        private const float MarginWobble = 10f;
        // 판 = 지도 자리 1500×996px ÷ 한 칸 3px(사용자 10/25 — 판을 꽉 채운다)
        private const int CanvasWidth = 500, CanvasHeight = 332;

        public readonly int Width, Height;
        // [y * Width + x] = 땅 번호
        public readonly int[] Cells;
        public int[][] Links;
        // [a, b] = 두 땅이 맞댄 국경의 길이(칸)
        public int[,] BorderLength;
        public int[] AnchorX, AnchorY;
        // 땅마다 넓이(칸) · 내접 반지름(경계에서 가장 먼 칸까지, 칸)
        public int[] Area, Radius;
        // 땅마다 지형 글자(WorldMapTable.TerrainLetters) · 깎은 띠 수(WorldMapRidges — 영주 땅 0..29 · 띠 30..39)
        public readonly char[] Terrain;
        public readonly int RidgesMade;

        private static readonly int[] DX = { 1, -1, 0, 0 }, DY = { 0, 0, 1, -1 };
        private readonly float[,] _lattice = new float[64, 64];

        public int At(int x, int y) => Cells[y * Width + x];

        public WorldMapBaker()
        {
            int w = SourceWidth, h = SourceHeight;
            var random = new Random(Seed ^ 0x5bd1e995);
            for (int i = 0; i < 64; i++)
                for (int j = 0; j < 64; j++)
                    _lattice[i, j] = (float)(random.NextDouble() * 2 - 1);
            bool[] land = Continent(w, h, LandTarget);
            List<(float x, float y)> points = Points(land, w, h, random, (float)Math.Sqrt(LandTarget / 40.0) * 0.62f);
            if (points.Count < Regions)
                throw new InvalidOperationException($"땅 위에 점 {Regions}개를 못 놓았다({points.Count}개)");
            float[] speed = points.Select(_ => (float)(0.7 + 0.75 * Math.Pow(random.NextDouble(), 1.3))).ToArray();
            var cost = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    cost[y * w + x] = 1f + 0.9f * Noise(x + 333, y + 777, 6f) + 0.5f * (float)(random.NextDouble() - 0.5);

            // 자라기 — 칸마다 가장 싸게 닿는 점의 땅
            var cells = Enumerable.Repeat(Sea, w * h).ToArray();
            var dist = Enumerable.Repeat(float.MaxValue, w * h).ToArray();
            var heap = new Heap();
            for (int i = 0; i < Regions; i++)
            {
                int c = (int)points[i].y * w + (int)points[i].x;
                dist[c] = 0;
                cells[c] = i;
                heap.Enqueue(c, i, 0);
            }
            while (heap.TryDequeue(out int cell, out int region, out float d))
            {
                if (d > dist[cell] || cells[cell] != region)
                    continue;
                int ux = cell % w, uy = cell / w;
                for (int k = 0; k < 4; k++)
                {
                    int nx = ux + DX[k], ny = uy + DY[k];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        continue;
                    int v = ny * w + nx;
                    if (!land[v])
                        continue;
                    float nd = d + Math.Max(0.2f, cost[v]) / speed[region];
                    if (nd < dist[v])
                    {
                        dist[v] = nd;
                        cells[v] = region;
                        heap.Enqueue(v, region, nd);
                    }
                }
            }
            MergeStrays(cells, w, h);

            // 땅 번호 = 위에서부터 다섯 줄 × 왼쪽부터(지형 표의 칸)
            var renumber = new int[Regions];
            List<int> byY = Enumerable.Range(0, Regions).OrderBy(i => points[i].y).ToList();
            int columns = WorldMapTable.Columns;
            for (int row = 0; row * columns < Regions; row++)
            {
                List<int> band = byY.Skip(row * columns).Take(columns).OrderBy(i => points[i].x).ToList();
                for (int col = 0; col < band.Count; col++)
                    renumber[band[col]] = row * columns + col;
            }

            // 땅이 차지한 상자 + 둘레 바다로 잘라 낸다
            int x0 = w, x1 = 0, y0 = h, y1 = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (cells[y * w + x] != Sea)
                    {
                        x0 = Math.Min(x0, x);
                        x1 = Math.Max(x1, x);
                        y0 = Math.Min(y0, y);
                        y1 = Math.Max(y1, y);
                    }
            // 판 = 지도 자리(Canvas) 크기 — 땅 상자를 판의 왼쪽 위에 붙이고(북 · 서는 판 밖으로 이어지는 대륙), 남 · 동 여백은 바다(사용자 10/25 「삼국지 지도처럼」). 상자가 더 크면 상자 + 남 · 동 바다
            int boxWidth = x1 - x0 + 1, boxHeight = y1 - y0 + 1;
            Width = Math.Max(CanvasWidth, boxWidth + Margin);
            Height = Math.Max(CanvasHeight, boxHeight + Margin);
            int left = x0, top = y0;
            Cells = new int[Width * Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int sx = left + x, sy = top + y;
                    int c = sx < 0 || sy < 0 || sx >= w || sy >= h ? Sea : cells[sy * w + sx];
                    Cells[y * Width + x] = c == Sea ? Sea : renumber[c];
                }
            ExtendToFrame();
            // ⑤ 산맥 · 강 띠(이벤트 땅) — 작은 땅 10칸을 합쳐 영주 땅 30 + 띠 10(WorldMapRidges)
            Terrain = WorldMapRidges.Apply(Cells, Width, Height, r => WorldMapTable.TerrainLetters[WorldMapTable.TerrainOf(r)], out RidgesMade);
            Area = new int[Regions];
            foreach (int c in Cells)
                if (c >= 0)
                    Area[c]++;
            MeasureBorders();
            PlaceAnchors();
            AddSeaRoutes();
        }

        // 해로(사용자 10/30 「구석 땅에 걸리면 막막하다」) — 바다를 건너 이웃이 되는 땅 쌍(땅 번호 0부터: 27-28 · 17-30).
        // 출발 칸(fromX, fromY) · 도착 칸(toX, toY)이 있으면 그 칸에 가장 가까운 해안에서 떠나고 닿는다, 없으면(−1) 가장 짧은 길.
        // 27-28은 맞닿을 듯한 해협 대신 27의 남동쪽 해안에서 떠나 28 곶의 남동쪽 해안에 닿는다(사용자 10/30 「해로가 너무 짧다」 — 해협에 닿으면 27의 육지 화살표와 겹친다).
        // 칸(Cells)은 건드리지 않는다 — 지도 모양은 그대로이고 이웃 표에 쌍만 더해진다
        public static readonly (int a, int b, int fromX, int fromY, int toX, int toY)[] SeaRoutes = { (26, 27, 63, 318, 140, 284), (16, 29, -1, -1, -1, -1) };
        // 해로 길이 해안에서 떨어지려는 거리(칸) · 그보다 가까운 칸 한 칸 더 다가갈 때마다 붙는 비용
        private const int RouteClearance = 4;
        private const float RouteHug = 2f;
        // [해로] = 바다 칸 번호(y × Width + x)들 — a 해안에 닿은 칸부터 b 해안에 닿은 칸까지(8방향으로 이어진다)
        public int[][] RoutePaths;

        private void AddSeaRoutes()
        {
            // 바다 칸마다 가장 가까운 땅까지 거리(체비쇼프) — 해로가 해안에 붙지 않게
            var shore = Enumerable.Repeat(int.MaxValue, Width * Height).ToArray();
            var queue = new Queue<int>();
            for (int i = 0; i < Cells.Length; i++)
                if (Cells[i] != Sea)
                {
                    shore[i] = 0;
                    queue.Enqueue(i);
                }
            while (queue.Count > 0)
            {
                int u = queue.Dequeue(), ux = u % Width, uy = u / Width;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = ux + dx, ny = uy + dy;
                        if (nx < 0 || ny < 0 || nx >= Width || ny >= Height || shore[ny * Width + nx] != int.MaxValue)
                            continue;
                        shore[ny * Width + nx] = shore[u] + 1;
                        queue.Enqueue(ny * Width + nx);
                    }
            }
            var links = Array.ConvertAll(Links, row => row.ToList());
            RoutePaths = new int[SeaRoutes.Length][];
            for (int r = 0; r < SeaRoutes.Length; r++)
            {
                (int a, int b, int fromX, int fromY, int toX, int toY) = SeaRoutes[r];
                // 떠나는 a의 해안 칸 · 닿는 b 앞바다 칸 — 고른 칸이 있으면 거기에 가장 가까운 하나, 없으면 전부
                List<int> starts = Closest(i => Cells[i] == a && Touches(i, Sea), fromX, fromY);
                var ends = new HashSet<int>(Closest(i => Cells[i] == Sea && Touches(i, b), toX, toY));
                RoutePaths[r] = SeaPath(starts, ends, b, shore);
                links[a].Add(b);
                links[b].Add(a);
            }
            Links = Array.ConvertAll(links, list => list.ToArray());
        }

        // pick을 만족하는 칸들 — (x, y)가 있으면 그중 거기에 가장 가까운 하나만(x < 0이면 전부)
        private List<int> Closest(Func<int, bool> pick, int x, int y)
        {
            var found = new List<int>();
            int best = int.MaxValue;
            for (int i = 0; i < Cells.Length; i++)
            {
                if (!pick(i))
                    continue;
                if (x < 0)
                {
                    found.Add(i);
                    continue;
                }
                int d = (i % Width - x) * (i % Width - x) + (i / Width - y) * (i / Width - y);
                if (d < best)
                {
                    best = d;
                    found.Clear();
                    found.Add(i);
                }
            }
            return found;
        }

        // cell(땅 칸)의 4방향 이웃에 region 칸이 있나
        private bool Touches(int cell, int region)
        {
            int x = cell % Width, y = cell / Width;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + DX[k], ny = y + DY[k];
                if (nx >= 0 && ny >= 0 && nx < Width && ny < Height && Cells[ny * Width + nx] == region)
                    return true;
            }
            return false;
        }

        // starts(해안 칸)에 닿은 바다 칸에서 ends(region 앞바다 칸) 중 하나까지 바다만 건너는 가장 싼 길(8방향, 해안에 가까울수록 비싸다)
        private int[] SeaPath(List<int> starts, HashSet<int> ends, int region, int[] shore)
        {
            var cost = Enumerable.Repeat(float.MaxValue, Width * Height).ToArray();
            var previous = Enumerable.Repeat(-1, Width * Height).ToArray();
            var heap = new Heap();
            foreach (int start in starts)
                for (int k = 0; k < 4; k++)
                {
                    int nx = start % Width + DX[k], ny = start / Width + DY[k];
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height || Cells[ny * Width + nx] != Sea)
                        continue;
                    cost[ny * Width + nx] = 0;
                    heap.Enqueue(ny * Width + nx, 0, 0);
                }
            while (heap.TryDequeue(out int u, out _, out float d))
            {
                if (d > cost[u])
                    continue;
                if (ends.Contains(u))
                {
                    var path = new List<int>();
                    for (int at = u; at >= 0; at = previous[at])
                        path.Add(at);
                    path.Reverse();
                    return path.ToArray();
                }
                int ux = u % Width, uy = u / Width;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = ux + dx, ny = uy + dy;
                        if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= Width || ny >= Height)
                            continue;
                        int v = ny * Width + nx;
                        // 대각선은 양옆 두 칸도 바다여야 건넌다(땅 모서리를 스쳐 넘지 않게)
                        if (Cells[v] != Sea || (dx != 0 && dy != 0 && (Cells[uy * Width + nx] != Sea || Cells[ny * Width + ux] != Sea)))
                            continue;
                        float step = (dx != 0 && dy != 0 ? 1.4142f : 1f) * (1f + RouteHug * Math.Max(0, RouteClearance - shore[v]));
                        if (d + step >= cost[v])
                            continue;
                        cost[v] = d + step;
                        previous[v] = u;
                        heap.Enqueue(v, 0, cost[v]);
                    }
            }
            throw new InvalidOperationException($"해로가 바다로 {region + 1}번 땅에 닿지 못한다");
        }

        // 북 · 서 판 가장자리까지 대륙(사용자 10/25 — 삼국지 지도: 위 변은 오른쪽 위 꼭짓점까지 · 왼쪽 변도 땅, 남 · 동만 해안).
        // 세로줄마다 땅의 북쪽 끝보다 위 · 가로줄마다 서쪽 끝보다 왼쪽인 바다 칸(땅이 없는 줄은 가장 가까운 줄의 끝)과, 남 · 동 가장자리에서 이어지지 않는 갇힌 물은
        // 가장 가까운 땅이 차지한다(가장자리 땅이 판 밖으로 이어진 것처럼 넓어진다). 남은 바다는 남 · 동 가장자리로 열린 하나다
        private void ExtendToFrame()
        {
            int n = Width * Height;
            var columnNorth = Enumerable.Repeat(Height, Width).ToArray();
            var rowWest = Enumerable.Repeat(Width, Height).ToArray();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (At(x, y) >= 0)
                    {
                        columnNorth[x] = Math.Min(columnNorth[x], y);
                        rowWest[y] = Math.Min(rowWest[y], x);
                    }
            NearestFill(columnNorth, Height, 100f);
            NearestFill(rowWest, Width, 900f);
            // 바다 = 남 · 동 가장자리에서 북 · 서 몫이 아닌 바다 칸을 따라 이어진 것
            var open = new bool[n];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    open[y * Width + x] = At(x, y) == Sea && y >= columnNorth[x] && x >= rowWest[y];
            var sea = new bool[n];
            var queue = new Queue<int>();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if ((x == Width - 1 || y == Height - 1) && open[i] && !sea[i])
                    {
                        sea[i] = true;
                        queue.Enqueue(i);
                    }
                }
            Flood(queue, sea, open);
            // 나머지 바다 칸은 땅에서부터 번져 차지한다 — 칸마다 거친 비용으로 가장 싸게 닿는 땅(땅을 만들 때의 자라기와 같은 방식이라 여백의 국경도 삐뚤빼뚤하다, 10/25)
            var fill = new bool[n];
            var dist = Enumerable.Repeat(float.MaxValue, n).ToArray();
            var heap = new Heap();
            for (int i = 0; i < n; i++)
            {
                fill[i] = Cells[i] == Sea && !sea[i];
                if (Cells[i] >= 0)
                {
                    dist[i] = 0f;
                    heap.Enqueue(i, Cells[i], 0f);
                }
            }
            while (heap.TryDequeue(out int u, out int region, out float d))
            {
                if (d > dist[u] || Cells[u] != region)
                    continue;
                int ux = u % Width, uy = u / Width;
                for (int k = 0; k < 4; k++)
                {
                    int nx = ux + DX[k], ny = uy + DY[k];
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height)
                        continue;
                    int v = ny * Width + nx;
                    if (!fill[v])
                        continue;
                    float nd = d + Math.Max(0.2f, 1f + 0.9f * Noise(nx + 333, ny + 777, 6f) + 0.7f * Noise(nx + 51, ny + 913, 20f));
                    if (nd < dist[v])
                    {
                        dist[v] = nd;
                        Cells[v] = region;
                        heap.Enqueue(v, region, nd);
                    }
                }
            }
        }


        // empty인 칸을 가장 가까운(앞쪽 먼저) empty 아닌 칸의 값으로 — 땅에서 멀어질수록 잡음으로 흔든다(여백의 해안이 곧은 줄이 되지 않게, ±MarginWobble칸). salt = 잡음 자리
        private void NearestFill(int[] values, int empty, float salt)
        {
            var source = (int[])values.Clone();
            for (int i = 0; i < values.Length; i++)
            {
                if (source[i] != empty)
                    continue;
                for (int d = 1; d < values.Length; d++)
                {
                    int from = i - d >= 0 && source[i - d] != empty ? i - d : i + d < values.Length && source[i + d] != empty ? i + d : -1;
                    if (from < 0)
                        continue;
                    values[i] = source[from] + (int)Math.Round(Noise(i, salt, 11f) * MarginWobble * Math.Min(1f, d / 6f));
                    break;
                }
            }
        }

        // queue에서부터 allowed를 따라 4방향으로 번지며 reached에 적는다
        private void Flood(Queue<int> queue, bool[] reached, bool[] allowed)
        {
            while (queue.Count > 0)
            {
                int u = queue.Dequeue(), ux = u % Width, uy = u / Width;
                for (int k = 0; k < 4; k++)
                {
                    int nx = ux + DX[k], ny = uy + DY[k];
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height)
                        continue;
                    int v = ny * Width + nx;
                    if (allowed[v] && !reached[v])
                    {
                        reached[v] = true;
                        queue.Enqueue(v);
                    }
                }
            }
        }

        // 부드러운 격자 잡음 — -1 ~ 1
        private float Noise(float x, float y, float cell)
        {
            float fx = x / cell, fy = y / cell;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            float a = _lattice[x0 & 63, y0 & 63], b = _lattice[(x0 + 1) & 63, y0 & 63], c = _lattice[x0 & 63, (y0 + 1) & 63], d = _lattice[(x0 + 1) & 63, (y0 + 1) & 63];
            return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * ty;
        }

        // 대륙 마스크 — 가운데 타원 + 큰 · 중간 잡음, 가장자리 40칸 안에서 줄어든다. 값이 큰 칸부터 target칸이 땅. 가장 큰 덩어리만 남기고, 바깥 바다와 안 이어진 바다(호수)는 땅으로
        private bool[] Continent(int w, int h, int target)
        {
            var f = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float ex = (x - w / 2f) / (w / 2f), ey = (y - h / 2f) / (h / 2f);
                    float e = (float)Math.Sqrt(ex * ex * 0.8 + ey * ey);
                    float edge = Math.Min(Math.Min(x, w - 1 - x), Math.Min(y, h - 1 - y));
                    f[y * w + x] = 1f - e + (Noise(x + 900, y + 300, 70f) * 0.45f + Noise(x + 100, y + 800, 28f) * 0.2f + Noise(x + 600, y, 9f) * 0.05f) - Math.Max(0f, (40f - edge) / 40f) * 1.6f;
                }
            var sorted = (float[])f.Clone();
            Array.Sort(sorted);
            float threshold = sorted[sorted.Length - 1 - Math.Min(f.Length - 1, target)];
            var land = new bool[w * h];
            for (int i = 0; i < f.Length; i++)
                land[i] = f[i] > threshold;
            // 가장 큰 덩어리
            var component = new int[w * h];
            int best = 0, bestSize = 0, id = 0;
            for (int i = 0; i < land.Length; i++)
            {
                if (!land[i] || component[i] != 0)
                    continue;
                id++;
                int size = 0;
                var stack = new Stack<int>();
                stack.Push(i);
                component[i] = id;
                while (stack.Count > 0)
                {
                    int u = stack.Pop();
                    size++;
                    int ux = u % w, uy = u / w;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = ux + DX[k], ny = uy + DY[k];
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            continue;
                        int v = ny * w + nx;
                        if (land[v] && component[v] == 0)
                        {
                            component[v] = id;
                            stack.Push(v);
                        }
                    }
                }
                if (size > bestSize)
                {
                    bestSize = size;
                    best = id;
                }
            }
            for (int i = 0; i < land.Length; i++)
                land[i] = land[i] && component[i] == best;
            // 호수 메우기
            var outer = new bool[w * h];
            var queue = new Queue<int>();
            for (int x = 0; x < w; x++)
                foreach (int y in new[] { 0, h - 1 })
                {
                    int i = y * w + x;
                    if (!land[i] && !outer[i])
                    {
                        outer[i] = true;
                        queue.Enqueue(i);
                    }
                }
            for (int y = 0; y < h; y++)
                foreach (int x in new[] { 0, w - 1 })
                {
                    int i = y * w + x;
                    if (!land[i] && !outer[i])
                    {
                        outer[i] = true;
                        queue.Enqueue(i);
                    }
                }
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                int ux = u % w, uy = u / w;
                for (int k = 0; k < 4; k++)
                {
                    int nx = ux + DX[k], ny = uy + DY[k];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        continue;
                    int v = ny * w + nx;
                    if (!land[v] && !outer[v])
                    {
                        outer[v] = true;
                        queue.Enqueue(v);
                    }
                }
            }
            for (int i = 0; i < land.Length; i++)
                if (!outer[i])
                    land[i] = true;
            return land;
        }

        // 땅 위에 점 40개 — 서로 minDistance 이상(무작위로 찍어 보고 가까우면 버린다)
        private static List<(float x, float y)> Points(bool[] land, int w, int h, Random random, float minDistance)
        {
            var points = new List<(float x, float y)>();
            for (int tries = 0; tries < 200000 && points.Count < Regions; tries++)
            {
                int x = random.Next(w), y = random.Next(h);
                if (!land[y * w + x])
                    continue;
                if (points.Any(p => (p.x - x) * (p.x - x) + (p.y - y) * (p.y - y) < minDistance * minDistance))
                    continue;
                points.Add((x, y));
            }
            return points;
        }

        // 떨어져 나온 조각 정리 — 땅마다 가장 큰 덩어리만 남기고 나머지는 맞닿은 다른 땅에 붙인다
        private static void MergeStrays(int[] cells, int w, int h)
        {
            for (int pass = 0; pass < 6; pass++)
            {
                var component = new int[w * h];
                var sizes = new List<int> { 0 };
                var owners = new List<int> { 0 };
                for (int i = 0; i < w * h; i++)
                {
                    if (cells[i] == Sea || component[i] != 0)
                        continue;
                    int id = sizes.Count, size = 0, own = cells[i];
                    var stack = new Stack<int>();
                    stack.Push(i);
                    component[i] = id;
                    while (stack.Count > 0)
                    {
                        int u = stack.Pop();
                        size++;
                        int ux = u % w, uy = u / w;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = ux + DX[k], ny = uy + DY[k];
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                                continue;
                            int v = ny * w + nx;
                            if (component[v] == 0 && cells[v] == own)
                            {
                                component[v] = id;
                                stack.Push(v);
                            }
                        }
                    }
                    sizes.Add(size);
                    owners.Add(own);
                }
                var biggest = new int[Regions];
                for (int id = 1; id < sizes.Count; id++)
                    if (biggest[owners[id]] == 0 || sizes[id] > sizes[biggest[owners[id]]])
                        biggest[owners[id]] = id;
                bool changed = false;
                for (int i = 0; i < w * h; i++)
                {
                    if (cells[i] == Sea || component[i] == biggest[cells[i]])
                        continue;
                    int ux = i % w, uy = i / w;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = ux + DX[k], ny = uy + DY[k];
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            continue;
                        int v = ny * w + nx;
                        if (cells[v] != Sea && cells[v] != cells[i])
                        {
                            cells[i] = cells[v];
                            changed = true;
                            break;
                        }
                    }
                }
                if (!changed)
                    break;
            }
        }

        private void MeasureBorders()
        {
            BorderLength = new int[Regions, Regions];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    int a = At(x, y);
                    if (a == Sea)
                        continue;
                    if (x + 1 < Width && At(x + 1, y) != Sea && At(x + 1, y) != a)
                    {
                        BorderLength[a, At(x + 1, y)]++;
                        BorderLength[At(x + 1, y), a]++;
                    }
                    if (y + 1 < Height && At(x, y + 1) != Sea && At(x, y + 1) != a)
                    {
                        BorderLength[a, At(x, y + 1)]++;
                        BorderLength[At(x, y + 1), a]++;
                    }
                }
            var links = new List<int>[Regions];
            for (int a = 0; a < Regions; a++)
            {
                links[a] = new List<int>();
                for (int b = 0; b < Regions; b++)
                    if (b != a && BorderLength[a, b] >= MinBorder)
                        links[a].Add(b);
            }
            Links = Array.ConvertAll(links, list => list.ToArray());
        }

        // 땅 안에서 경계와 가장 먼 칸 — 아이콘 · 번호를 놓을 자리. 그 거리가 내접 반지름
        private void PlaceAnchors()
        {
            var distance = new int[Width, Height];
            var queue = new Queue<(int, int)>();
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    distance[x, y] = int.MaxValue;
                    if (At(x, y) == Sea)
                        continue;
                    bool edge = false;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + DX[k], ny = y + DY[k];
                        if (nx < 0 || ny < 0 || nx >= Width || ny >= Height || At(nx, ny) != At(x, y))
                            edge = true;
                    }
                    if (edge)
                    {
                        distance[x, y] = 0;
                        queue.Enqueue((x, y));
                    }
                }
            while (queue.Count > 0)
            {
                (int cx, int cy) = queue.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + DX[k], ny = cy + DY[k];
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height || At(nx, ny) != At(cx, cy) || distance[nx, ny] <= distance[cx, cy] + 1)
                        continue;
                    distance[nx, ny] = distance[cx, cy] + 1;
                    queue.Enqueue((nx, ny));
                }
            }
            AnchorX = new int[Regions];
            AnchorY = new int[Regions];
            Radius = new int[Regions];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    int r = At(x, y);
                    if (r != Sea && distance[x, y] > Radius[r])
                    {
                        Radius[r] = distance[x, y];
                        AnchorX[r] = x;
                        AnchorY[r] = y;
                    }
                }
        }

        // 4갈래 최소 힙 — .NET PriorityQueue<TElement, TPriority>와 같은 순서로 넣고 뺀다(같은 값끼리의 순서까지 — 기획 러너와 칸 단위로 같으려면 필요하다. Unity의 .NET에는 PriorityQueue가 없다)
        private class Heap
        {
            private (int cell, int region, float priority)[] _nodes = new (int, int, float)[1024];
            private int _size;

            public void Enqueue(int cell, int region, float priority)
            {
                if (_size == _nodes.Length)
                    Array.Resize(ref _nodes, _nodes.Length * 2);
                var node = (cell, region, priority);
                int index = _size++;
                while (index > 0)
                {
                    int parent = (index - 1) >> 2;
                    if (node.priority < _nodes[parent].priority)
                    {
                        _nodes[index] = _nodes[parent];
                        index = parent;
                    }
                    else
                        break;
                }
                _nodes[index] = node;
            }

            public bool TryDequeue(out int cell, out int region, out float priority)
            {
                if (_size == 0)
                {
                    cell = region = 0;
                    priority = 0f;
                    return false;
                }
                (cell, region, priority) = _nodes[0];
                int last = --_size;
                if (last > 0)
                {
                    var node = _nodes[last];
                    int index = 0, child;
                    while ((child = (index << 2) + 1) < _size)
                    {
                        int min = child, bound = Math.Min(child + 4, _size);
                        while (++child < bound)
                            if (_nodes[child].priority < _nodes[min].priority)
                                min = child;
                        if (node.priority <= _nodes[min].priority)
                            break;
                        _nodes[index] = _nodes[min];
                        index = min;
                    }
                    _nodes[index] = node;
                }
                return true;
            }
        }
    }
}
