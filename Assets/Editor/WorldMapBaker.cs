using System;
using System.Collections.Generic;

namespace GnorpWar.Editor
{
    // 영토 지도 한 장을 만든다(손으로 고른 모양 — 씨앗 · 흐트림 · 해안선은 사용자가 시안을 보고 정했다. 숫자를 바꾸면 다른 지도가 된다).
    // 낮은 해상도 격자에서 6×5 바둑판 자리를 흐트린 씨앗 30개로 땅을 나누고(경계에 잡음을 섞어 국경처럼), 가장자리는 해안선으로 깎는다.
    // 내는 것: 칸마다 땅 번호(바다는 -1, 위 왼쪽이 0번 칸) · 국경을 MinBorder칸 이상 맞댄 땅끼리의 이웃 표(= 침략할 수 있는 길) · 땅마다 아이콘을 놓을 자리.
    // UnityEngine을 쓰지 않는다 — Unity 밖 검산(시안 생성기 · 규칙 러너)이 같은 코드를 읽는다
    internal class WorldMapBaker
    {
        // 지도 1칸 = 화면 4px — 높이는 지도 칸(화면 테두리 막대 사이 999px, UI/FrameBars)에 들어가게 249칸(사용자 10/23)
        public const int Width = 375, Height = 249;
        public const int Sea = -1;
        // 이만큼(칸) 이상 국경을 맞대야 이어진 땅이다 — 짧게 스치는 접점은 길이 아니다
        public const int MinBorder = 6;
        private const int Seed = 20261011;
        private const float Jitter = 0.46f;

        // [y * Width + x] = 땅 번호
        public readonly int[] Cells = new int[Width * Height];
        public int[][] Links;
        // [a, b] = 두 땅이 맞댄 국경의 길이(칸)
        public int[,] BorderLength;
        public int[] AnchorX, AnchorY;

        private static readonly int[] DX = { 1, -1, 0, 0 }, DY = { 0, 0, 1, -1 };
        private readonly float[,] _lattice = new float[64, 64];
        private readonly int _regions = WorldMapTable.RegionCount;

        public int At(int x, int y) => Cells[y * Width + x];

        public WorldMapBaker()
        {
            var random = new Random(Seed);
            for (int i = 0; i < 64; i++)
                for (int j = 0; j < 64; j++)
                    _lattice[i, j] = (float)(random.NextDouble() * 2 - 1);
            int columns = WorldMapTable.Columns, rows = _regions / columns;
            float cellWidth = Width / (float)columns, cellHeight = Height / (float)rows;
            var seedX = new float[_regions];
            var seedY = new float[_regions];
            for (int r = 0; r < _regions; r++)
            {
                seedX[r] = (r % columns + 0.5f) * cellWidth + (float)(random.NextDouble() * 2 - 1) * cellWidth * Jitter;
                seedY[r] = (r / columns + 0.5f) * cellHeight + (float)(random.NextDouble() * 2 - 1) * cellHeight * Jitter;
            }
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    // 바다 — 가장자리가 울퉁불퉁한 해안선이 되게
                    float edge = Math.Min(Math.Min(x, Width - 1 - x), Math.Min(y, Height - 1 - y));
                    if (edge + Noise(x + 300, y + 100, 38f) * 20f + Noise(x, y + 700, 11f) * 4f < 9f)
                    {
                        Cells[y * Width + x] = Sea;
                        continue;
                    }
                    // 좌표에 잡음을 얹은 뒤 가장 가까운 씨앗 — 국경이 삐뚤빼뚤해진다
                    float wx = x + Noise(x, y, 26f) * 11f + Noise(x + 50, y + 90, 9f) * 4f, wy = y + Noise(x + 200, y + 400, 26f) * 11f + Noise(x + 500, y + 20, 9f) * 4f;
                    int best = 0;
                    float bestDistance = float.MaxValue;
                    for (int r = 0; r < _regions; r++)
                    {
                        float dx = wx - seedX[r], dy = wy - seedY[r], d = dx * dx + dy * dy;
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = r;
                        }
                    }
                    Cells[y * Width + x] = best;
                }
            MergeStrays();
            MeasureBorders();
            PlaceAnchors();
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

        // 떨어져 나온 조각 정리 — 땅마다 가장 큰 덩어리만 남기고 나머지는 맞닿은 땅에 붙인다
        private void MergeStrays()
        {
            for (int pass = 0; pass < 4; pass++)
            {
                var component = new int[Width, Height];
                var sizes = new List<int> { 0 };
                var owners = new List<int> { 0 };
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {
                        if (At(x, y) == Sea || component[x, y] != 0)
                            continue;
                        int id = sizes.Count, size = 0;
                        var stack = new Stack<(int, int)>();
                        stack.Push((x, y));
                        component[x, y] = id;
                        while (stack.Count > 0)
                        {
                            (int cx, int cy) = stack.Pop();
                            size++;
                            for (int k = 0; k < 4; k++)
                            {
                                int nx = cx + DX[k], ny = cy + DY[k];
                                if (nx < 0 || ny < 0 || nx >= Width || ny >= Height || component[nx, ny] != 0 || At(nx, ny) != At(x, y))
                                    continue;
                                component[nx, ny] = id;
                                stack.Push((nx, ny));
                            }
                        }
                        sizes.Add(size);
                        owners.Add(At(x, y));
                    }
                var biggest = new int[_regions];
                for (int id = 1; id < sizes.Count; id++)
                    if (biggest[owners[id]] == 0 || sizes[id] > sizes[biggest[owners[id]]])
                        biggest[owners[id]] = id;
                bool changed = false;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {
                        if (At(x, y) == Sea || component[x, y] == biggest[At(x, y)])
                            continue;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = x + DX[k], ny = y + DY[k];
                            if (nx < 0 || ny < 0 || nx >= Width || ny >= Height || At(nx, ny) == Sea || At(nx, ny) == At(x, y))
                                continue;
                            Cells[y * Width + x] = At(nx, ny);
                            changed = true;
                            break;
                        }
                    }
                if (!changed)
                    break;
            }
        }

        private void MeasureBorders()
        {
            BorderLength = new int[_regions, _regions];
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
            var links = new List<int>[_regions];
            for (int a = 0; a < _regions; a++)
            {
                links[a] = new List<int>();
                for (int b = 0; b < _regions; b++)
                    if (b != a && BorderLength[a, b] >= MinBorder)
                        links[a].Add(b);
            }
            Links = Array.ConvertAll(links, list => list.ToArray());
        }

        // 땅 안에서 경계와 가장 먼 칸 — 아이콘 · 번호를 놓을 자리
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
            AnchorX = new int[_regions];
            AnchorY = new int[_regions];
            var best = new int[_regions];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    int r = At(x, y);
                    if (r != Sea && distance[x, y] > best[r])
                    {
                        best[r] = distance[x, y];
                        AnchorX[r] = x;
                        AnchorY[r] = y;
                    }
                }
        }
    }
}
