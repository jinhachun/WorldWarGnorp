using System;
using System.Collections.Generic;
using System.Linq;

namespace GnorpWar.Editor
{
    // 이벤트 땅 = 길쭉한 띠(산맥 · 강 — 영토 전쟁에 편입되지 않는 자연 경계, 사용자 10/26 「땅을 작게 하는 대신 길쭉하게, 닿는 땅이 여러 개」 · 자리 고정).
    // 굽기(WorldMapBaker)가 40칸 지도를 다 만든 뒤에 부른다:
    //   1) 작은 땅 MergeCount칸을 국경을 가장 길게 맞댄 이웃에 합쳐 영주 땅 LordCount칸(합친 뒤에도 땅마다 이웃 둘 이상 · 얼음이 늪지/정글과 안 맞닿는 짝만)
    //   2) 영주 땅 사이 경계를 따라 띠 RidgeCount개를 깎는다 — 세 땅이 만나는 점에서 경계만 따라 MinLength~MaxLength칸 가는 길의 둘레.
    //      굵기는 띠마다 성격이 다르고(봉우리 PeakLo~PeakHi칸) 길이 방향으로 잡음으로 들쭉날쭉(Sharp — 대부분 가는 목 · 군데군데 봉우리) · 양 끝은 가늘게 · 가장자리는 칸 잡음 · 곁가지 0~Branches개.
    //      띠를 넣을 때마다 조건 — 맞닿는 영주 땅 셋 이상 · 다른 띠와 안 맞닿음 · 영주 땅이 다 남고 이어짐 · 영주 땅마다 영주 이웃 둘 이상 · 띠 한 덩어리 · 넓이 ThinAreaCap 이하. 깨지면 버리고 다른 길(여기까지가 사용자가 고른 ridge2 b).
    //   3) 같은 길 · 같은 굵기 성격으로 ThickSteps배 굵게 다시 칠한다(가는 목도 MinNeck칸 · 다른 띠와 맞닿을 칸은 비움) — 띠가 조건 · AreaCap(영주 땅 평균의 절반도)을 깨면 그 띠만 한 단계 덜 굵게(사용자 10/26 「틈 말고 산맥처럼 두껍게」).
    // 번호: 영주 땅 0..29(합치고 남은 땅의 원래 번호 순) · 띠 30..39. 띠의 지형 = 깎인 칸이 가장 많이 속했던 영주 땅의 지형.
    // 1) · 2)는 사용자가 고른 미리 보기(Captures/Map40/ridge2_preview_b_wild.png)와 칸 단위로 같다. UnityEngine을 쓰지 않는다
    internal static class WorldMapRidges
    {
        public const int LordCount = 30, RidgeCount = 10;
        private const int MergeCount = 10, Seed = 200, MaxAttempts = 4000;
        private const int MinLength = 45, MaxLength = 95, Gap = 8;
        private const float PeakLo = 1.5f, PeakHi = 6f, Sharp = 1.8f, Edge = 1.3f;
        private const int Knot = 11, Branches = 2;
        // 1차(길 고르기 — ridge2 b) 넓이 상한 · 2차(굵게 칠하기) 넓이 상한(영주 땅 평균의 절반도 넘지 않게 — 「한가운데 구멍」 방지)
        private const int ThinAreaCap = 1020;
        public const int AreaCap = 1600;
        // 2차 굵기 배율 — 앞에서부터 해 보고 조건을 깨면 다음(1 = 1차 그대로) · 가는 목의 최소 반지름(칸)
        private static readonly float[] ThickSteps = { 1.7f, 1.5f, 1.3f, 1.15f, 1f };
        private const float MinNeck = 2f;
        // 영주 땅이 띠에 깎이고 남아야 하는 넓이(칸)
        private const int MinLordArea = 300;

        private static readonly int[] DX = { 1, -1, 0, 0 }, DY = { 0, 0, 1, -1 };

        // cells = 40칸 지도(바다 음수, 바뀐다) · terrainOf = 원래 땅의 지형 글자 → 띠를 깎은 지도의 땅마다 지형 글자 · 깎은 띠 수
        public static char[] Apply(int[] cells, int w, int h, Func<int, char> terrainOf, out int made)
        {
            int n = w * h, regions = LordCount + MergeCount;
            var terrain = Enumerable.Range(0, regions).Select(terrainOf).ToArray();
            var alive = Enumerable.Repeat(true, regions).ToArray();
            for (int k = 0; k < MergeCount; k++)
            {
                int[] area = Areas(cells, regions);
                int[,] border = Borders(cells, w, h, regions);
                bool done = false;
                foreach (int small in Enumerable.Range(0, regions).Where(r => alive[r]).OrderBy(r => area[r]))
                {
                    foreach (int into in Enumerable.Range(0, regions).Where(r => alive[r] && r != small && border[small, r] >= WorldMapBaker.MinBorder).OrderByDescending(r => border[small, r]))
                    {
                        var trial = (int[])cells.Clone();
                        for (int i = 0; i < n; i++)
                            if (trial[i] == small)
                                trial[i] = into;
                        int[,] merged = Borders(trial, w, h, regions);
                        bool ok = true;
                        for (int a = 0; a < regions && ok; a++)
                        {
                            if (!alive[a] || a == small)
                                continue;
                            int degree = 0;
                            for (int c = 0; c < regions; c++)
                                if (c != a && c != small && alive[c] && merged[a, c] >= WorldMapBaker.MinBorder)
                                {
                                    degree++;
                                    if (IceClash(terrain[a], terrain[c]))
                                        ok = false;
                                }
                            if (degree < 2)
                                ok = false;
                        }
                        if (!ok)
                            continue;
                        Array.Copy(trial, cells, n);
                        alive[small] = false;
                        done = true;
                        break;
                    }
                    if (done)
                        break;
                }
                if (!done)
                    break;
            }
            // 영주 땅 번호 0..
            var lordOf = new int[regions];
            int lords = 0;
            var lordTerrain = new List<char>();
            for (int r = 0; r < regions; r++)
                if (alive[r])
                {
                    lordOf[r] = lords++;
                    lordTerrain.Add(terrain[r]);
                }
            for (int i = 0; i < n; i++)
                if (cells[i] >= 0)
                    cells[i] = lordOf[cells[i]];

            var random = new Random(Seed);
            made = 0;
            var origin = (int[])cells.Clone();
            // 1차 — 가는 굵기(Thin)로 길을 고른다(사용자가 고른 ridge2 b의 띠 10개 · 경계 그대로)
            var accepted = new List<List<Stroke>>();
            for (int attempts = 0; made < RidgeCount && attempts < MaxAttempts; attempts++)
            {
                List<int> path = PickPath(cells, w, h, random);
                if (path == null)
                    continue;
                var trial = (int[])cells.Clone();
                int id = LordCount + made;
                List<Stroke> strokes = Carve(trial, w, h, path, id, random, cells);
                foreach (Stroke s in strokes)
                    Paint(trial, w, h, s, id, 1f, 1f, null);
                if (Areas(trial, regions)[id] > ThinAreaCap || !Check(trial, w, h, lords, id))
                    continue;
                Array.Copy(trial, cells, n);
                accepted.Add(strokes);
                made++;
            }
            // 2차 — 같은 길 · 같은 굵기 성격으로 Thick배 굵게(가는 목도 MinNeck칸) 다시 칠한다(사용자 10/26 「틈 말고 산맥처럼 더 두껍게」).
            // 다른 띠와 맞닿을 칸은 칠하지 않고, 띠 하나가 조건 · 넓이 상한을 깨면 그 띠만 굵기를 한 단계씩 줄인다(끝내 1배 = 1차 그대로)
            Array.Copy(origin, cells, n);
            int lordArea = 0;
            for (int i = 0; i < n; i++)
                if (origin[i] >= 0)
                    lordArea++;
            int areaCap = Math.Min(AreaCap, lordArea / lords / 2);
            for (int k = 0; k < accepted.Count; k++)
            {
                int id = LordCount + k;
                foreach (float scale in ThickSteps)
                {
                    var trial = (int[])cells.Clone();
                    float neck = scale > 1f ? MinNeck : 1f;
                    foreach (Stroke s in accepted[k])
                        Paint(trial, w, h, s, id, scale, neck, cells);
                    if (scale > 1f && (Areas(trial, regions)[id] > areaCap || !Check(trial, w, h, lords, id)))
                        continue;
                    Array.Copy(trial, cells, n);
                    break;
                }
            }
            var result = new char[regions];
            for (int r = 0; r < lords; r++)
                result[r] = lordTerrain[r];
            for (int k = LordCount; k < LordCount + made; k++)
            {
                var count = new int[lords];
                for (int i = 0; i < n; i++)
                    if (cells[i] == k && origin[i] >= 0 && origin[i] < lords)
                        count[origin[i]]++;
                result[k] = lordTerrain[Array.IndexOf(count, count.Max())];
            }
            return result;
        }

        public static bool IceClash(char a, char b) => a == 'I' && (b == 'S' || b == 'J') || b == 'I' && (a == 'S' || a == 'J');

        // 값 잡음 — 칸 좌표의 해시(띠 씨앗 salt)로 -1~1, cell칸 격자를 부드럽게 잇는다
        private static float Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 32767.5f - 1f;
            }
        }

        private static float Noise(float x, float y, float cell, int salt)
        {
            float fx = x / cell, fy = y / cell;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            float a = Hash(x0, y0, salt), b = Hash(x0 + 1, y0, salt), c = Hash(x0, y0 + 1, salt), d = Hash(x0 + 1, y0 + 1, salt);
            return a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
        }

        // 칠할 붓질 하나 — 길 · 봉우리 반지름 · 잡음 씨앗 · 첫 칸 쪽 가늘어짐(줄기 0.9 · 곁가지 0 — 줄기에 붙은 쪽은 굵게)
        private class Stroke
        {
            public List<int> Path;
            public float Peak, StartTaper;
            public int Salt;
        }

        // 길을 따라 굵기가 들쭉날쭉한 띠 + 곁가지 0~Branches개(길 가운데쯤에서 경계를 따라 짧게) — 칠할 붓질들
        private static List<Stroke> Carve(int[] trial, int w, int h, List<int> path, int id, Random random, int[] cells)
        {
            float peak = PeakLo + (float)random.NextDouble() * (PeakHi - PeakLo);
            int salt = random.Next();
            var strokes = new List<Stroke> { new Stroke { Path = path, Peak = peak, Salt = salt, StartTaper = 0.9f } };
            int branches = random.Next(Branches + 1);
            for (int k = 0; k < branches && path.Count > 20; k++)
            {
                int from = path[path.Count / 4 + random.Next(path.Count / 2)];
                List<int> arm = Walk(cells, w, h, from, 12 + random.Next(18), random, path);
                if (arm != null)
                    strokes.Add(new Stroke { Path = arm, Peak = Math.Max(1.5f, peak * 0.6f), Salt = salt + 7 * (k + 1), StartTaper = 0f });
            }
            return strokes;
        }

        // 붓질 하나를 칠한다 — 반지름 = (1 + (봉우리 − 1) × 잡음^Sharp × 양 끝 가늘어짐) × scale, 적어도 neck칸.
        // separate면 다른 띠와 맞닿을 칸(둘레 8칸에 다른 띠)은 칠하지 않는다(굵게 칠할 때 띠끼리 붙지 않게)
        private static void Paint(int[] trial, int w, int h, Stroke s, int id, float scale, float neck, int[] separate)
        {
            int length = s.Path.Count;
            for (int i = 0; i < length; i++)
            {
                float t = length > 1 ? i / (float)(length - 1) : 0.5f;
                float taper = Math.Max(0.25f, Math.Min(1f, Math.Min(t / 0.15f + (1 - s.StartTaper), (1 - t) / 0.15f)));
                float along = (Noise(i, 0, Knot, s.Salt) + 1f) * 0.5f;
                float r = Math.Max(neck, (1f + (s.Peak - 1f) * (float)Math.Pow(along, Sharp) * taper) * scale);
                int cx = s.Path[i] % w, cy = s.Path[i] / w, reach = (int)Math.Ceiling(r + Edge);
                for (int dy = -reach; dy <= reach; dy++)
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= w || y >= h)
                            continue;
                        float edge = Edge * Noise(x, y, 3f, s.Salt + 99);
                        if (dx * dx + dy * dy > (r + edge) * (r + edge))
                            continue;
                        int v = y * w + x;
                        if (trial[v] < 0 || trial[v] >= LordCount || separate != null && NearOtherRidge(trial, w, h, x, y, id))
                            continue;
                        trial[v] = id;
                    }
            }
        }

        // (x, y) 둘레 8칸에 id가 아닌 띠가 있나
        private static bool NearOtherRidge(int[] cells, int w, int h, int x, int y, int id)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        continue;
                    int c = cells[ny * w + nx];
                    if (c >= LordCount && c != id)
                        return true;
                }
            return false;
        }

        // 영주 땅 경계 칸인가(남의 영주 땅과 4방향으로 맞닿음)
        private static bool Border(int[] cells, int w, int h, int i)
        {
            int c = cells[i];
            if (c < 0 || c >= LordCount)
                return false;
            int x = i % w, y = i / w;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + DX[k], ny = y + DY[k];
                if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                    continue;
                int o = cells[ny * w + nx];
                if (o >= 0 && o < LordCount && o != c)
                    return true;
            }
            return false;
        }

        // 둘레 3×3에 영주 땅이 셋 이상
        private static bool Junction(int[] cells, int w, int h, int i)
        {
            var set = new HashSet<int>();
            int x = i % w, y = i / w;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        continue;
                    int o = cells[ny * w + nx];
                    if (o >= 0 && o < LordCount)
                        set.Add(o);
                }
            return set.Count >= 3;
        }

        // from에서 경계 칸만 따라 length칸쯤 — 줄기(trunk)에서 멀어지는 쪽 끝
        private static List<int> Walk(int[] cells, int w, int h, int from, int length, Random random, List<int> trunk)
        {
            var onTrunk = new HashSet<int>(trunk);
            var parent = new Dictionary<int, int> { [from] = -1 };
            var depth = new Dictionary<int, int> { [from] = 0 };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            var ends = new List<int>();
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                if (depth[u] >= length)
                {
                    ends.Add(u);
                    continue;
                }
                int x = u % w, y = u / w;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            continue;
                        int v = ny * w + nx;
                        if (parent.ContainsKey(v) || !Border(cells, w, h, v) || onTrunk.Contains(v) && depth[u] > 3)
                            continue;
                        parent[v] = u;
                        depth[v] = depth[u] + 1;
                        queue.Enqueue(v);
                    }
            }
            ends = ends.Where(e => trunk.Min(t => Math.Abs(t % w - e % w) + Math.Abs(t / w - e / w)) > length / 2).ToList();
            if (ends.Count == 0)
                return null;
            var arm = new List<int>();
            for (int c = ends[random.Next(ends.Count)]; c != -1; c = parent[c])
                arm.Add(c);
            arm.Reverse();
            return arm;
        }

        // 경계 칸만 따라가는 길 — 세 땅이 만나는 칸에서 길이 [MinLength, MaxLength]인 다른 세 땅 점까지(이미 깎은 띠에서 Gap칸 안은 못 지남)
        private static List<int> PickPath(int[] cells, int w, int h, Random random)
        {
            int n = w * h;
            var queue = new Queue<int>();
            var distance = Enumerable.Repeat(int.MaxValue, n).ToArray();
            for (int i = 0; i < n; i++)
                if (cells[i] >= LordCount)
                {
                    distance[i] = 0;
                    queue.Enqueue(i);
                }
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                if (distance[u] >= Gap)
                    continue;
                for (int k = 0; k < 4; k++)
                {
                    int x = u % w + DX[k], y = u / w + DY[k];
                    if (x < 0 || y < 0 || x >= w || y >= h)
                        continue;
                    int v = y * w + x;
                    if (distance[v] > distance[u] + 1)
                    {
                        distance[v] = distance[u] + 1;
                        queue.Enqueue(v);
                    }
                }
            }
            List<int> junctions = Enumerable.Range(0, n).Where(i => Border(cells, w, h, i) && Junction(cells, w, h, i) && distance[i] > Gap).ToList();
            if (junctions.Count == 0)
                return null;
            int start = junctions[random.Next(junctions.Count)];
            var parent = new Dictionary<int, int> { [start] = -1 };
            var steps = new Dictionary<int, int> { [start] = 0 };
            queue.Enqueue(start);
            var ends = new List<int>();
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                if (steps[u] >= MaxLength)
                    continue;
                int x = u % w, y = u / w;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            continue;
                        int v = ny * w + nx;
                        if (parent.ContainsKey(v) || !Border(cells, w, h, v) || distance[v] <= Gap)
                            continue;
                        parent[v] = u;
                        steps[v] = steps[u] + 1;
                        queue.Enqueue(v);
                        if (steps[v] >= MinLength && Junction(cells, w, h, v))
                            ends.Add(v);
                    }
            }
            if (ends.Count == 0)
                return null;
            var path = new List<int>();
            for (int c = ends[random.Next(ends.Count)]; c != -1; c = parent[c])
                path.Add(c);
            return path;
        }

        // 띠 id를 넣은 뒤의 조건
        private static bool Check(int[] cells, int w, int h, int lords, int id)
        {
            int regions = LordCount + MergeCount;
            int[] area = Areas(cells, regions);
            for (int r = 0; r < lords; r++)
                if (area[r] < MinLordArea)
                    return false;
            int[,] border = Borders(cells, w, h, regions);
            if (Enumerable.Range(0, lords).Count(r => border[id, r] >= WorldMapBaker.MinBorder) < 3)
                return false;
            for (int k = LordCount; k < regions; k++)
                if (k != id && border[id, k] > 0)
                    return false;
            int[][] links = Enumerable.Range(0, lords).Select(a => Enumerable.Range(0, lords).Where(c => c != a && border[a, c] >= WorldMapBaker.MinBorder).ToArray()).ToArray();
            if (links.Any(l => l.Length < 2))
                return false;
            var seen = new bool[lords];
            var queue = new Queue<int>();
            queue.Enqueue(0);
            seen[0] = true;
            int reached = 1;
            while (queue.Count > 0)
                foreach (int v in links[queue.Dequeue()])
                    if (!seen[v])
                    {
                        seen[v] = true;
                        reached++;
                        queue.Enqueue(v);
                    }
            if (reached < lords)
                return false;
            // 띠 하나는 한 덩어리
            int first = Array.IndexOf(cells, id);
            if (first < 0)
                return false;
            var piece = new HashSet<int> { first };
            var fill = new Queue<int>(piece);
            while (fill.Count > 0)
            {
                int u = fill.Dequeue(), x = u % w, y = u / w;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + DX[k], ny = y + DY[k];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        continue;
                    int v = ny * w + nx;
                    if (cells[v] == id && piece.Add(v))
                        fill.Enqueue(v);
                }
            }
            return piece.Count == area[id];
        }

        private static int[] Areas(int[] cells, int count)
        {
            var area = new int[count];
            foreach (int c in cells)
                if (c >= 0)
                    area[c]++;
            return area;
        }

        private static int[,] Borders(int[] cells, int w, int h, int count)
        {
            var border = new int[count, count];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int a = cells[y * w + x];
                    if (a < 0)
                        continue;
                    if (x + 1 < w)
                    {
                        int c = cells[y * w + x + 1];
                        if (c >= 0 && c != a)
                        {
                            border[a, c]++;
                            border[c, a]++;
                        }
                    }
                    if (y + 1 < h)
                    {
                        int c = cells[(y + 1) * w + x];
                        if (c >= 0 && c != a)
                        {
                            border[a, c]++;
                            border[c, a]++;
                        }
                    }
                }
            return border;
        }
    }
}
