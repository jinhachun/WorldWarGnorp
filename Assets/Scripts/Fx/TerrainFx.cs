using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 지형 연출(사용자 10/20: 「지역의 효과가 인게임에서 더 직관적으로 보였으면」) — 이번 맵(RunState.Map)을 전장 자체로 보여 준다. 판정은 읽기만 한다.
    // ① 땅 장식(풀 · 덩굴 · 물웅덩이 · 얼음 껍질 …) — 판이 열릴 때 한 번 그린다. 흰 그림 톤 · 낮은 알파 · 유닛 · 탑보다 뒤
    // ② 하늘 입자(눈발 · 낙엽 · 포자 · 거품 · 먼지) — 화면 안에만 · 동시 상한 · 유닛 · 탑보다 뒤
    // ③ 효과 순간 — 얼음의 「전부 발동」(BoardRunner.MapFiredAll) 때 탑 창마다 얼음 섬광 · 늪지가 끈 층은 물에 잠김 · 맵 쿨다운 배율이 걸린 창은 불빛이 차오르는 끝줄이 지형 색
    // 셋 다 메시 하나씩(PixelMesh) — 드로우콜 3. 맵 종류는 맵 에셋(MapDefinition.Look). Battle 씬의 TerrainFx 오브젝트에 붙는다
    public class TerrainFx : MonoBehaviour
    {
        private const float Pixel = PixelMesh.Pixel;
        private const int GroundSorting = -8;   // 땅 속(-10 · 뼛가루 -9) 위 · 탑(-1) · 유닛(0) 아래
        private const int AirSorting = -2;      // 탑 · 유닛 뒤 — 전투 가독성
        private const int TowerSorting = 1;     // 탑 창 위
        private const int MaxParticles = 160;
        private const float ViewMargin = 1f;

        // 탑 창 자리(층 그림 픽셀) — Tower의 창과 같은 값: 아래에서 5번째 줄부터 11줄, 안쪽 폭 10px
        private const int WindowBottom = 5;
        private const int WindowRows = 11;
        private const int WindowHalf = 5;

        // 얼음 섬광 · 늪지 물 · 쿨다운 기운 색 — 색은 효과에만(CLAUDE.md §5 아트 톤)
        private static readonly Color IceColor = new Color(0.6f, 0.88f, 1f);
        private static readonly Color JungleColor = new Color(0.45f, 1f, 0.45f);
        private static readonly Color WaterColor = new Color(0.35f, 0.62f, 0.9f);
        private const float IceFlashSeconds = 0.5f;
        // 늪지 — 꺼진 층이 판이 열린 뒤 이 시간 동안 이 높이(px)까지 잠긴다
        private const float FloodSeconds = 1f;
        private const int FloodPixels = 7;

        [Tooltip("픽셀 재질 — 파편 · 버프 선과 같은 것(FX_Pixel)")]
        [SerializeField] private Material _material;

        private struct Particle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Phase;
        }

        private PixelMesh _ground;
        private PixelMesh _air;
        private PixelMesh _tower;
        private readonly List<Particle> _particles = new List<Particle>();
        // 물웅덩이(늪지) — 수면 높이(격자) · 왼쪽 · 오른쪽 끝(격자). 거품과 반짝임이 여기서 난다
        private readonly List<(int y, int left, int right)> _puddles = new List<(int, int, int)>();
        private readonly float[] _iceFlashAt = { float.NegativeInfinity, float.NegativeInfinity };
        private MapDefinition _map;
        private bool _built;
        private float _spawnDebt;
        private float _openedAt;

        private void Awake()
        {
            _ground = new PixelMesh(transform, "TerrainGround", _material, GroundSorting, 4096);
            _air = new PixelMesh(transform, "TerrainAir", _material, AirSorting, 1024);
            _tower = new PixelMesh(transform, "TerrainTower", _material, TowerSorting, 1024);
            BoardRunner.MapFiredAll += OnMapFiredAll;
        }

        private void OnDestroy() => BoardRunner.MapFiredAll -= OnMapFiredAll;

        private void LateUpdate()
        {
            if (!_built)
            {
                // 판이 열린 뒤 — 땅 모양(Ground)이 이번 맵으로 정해진 다음
                if (!BattleManager.Fighting || Ground.Instance == null)
                    return;
                _map = RunState.Map;
                _openedAt = Time.time;
                BuildGround();
                _built = true;
            }
            if (_map == null)
                return;
            DrawAir();
            DrawTowers();
        }

        // ── ① 땅 장식 — 한 번만 그린다(뼛가루가 쌓여도 따라가지 않는다)
        private void BuildGround()
        {
            if (_map == null)
                return;
            var random = new System.Random(_map.name.GetHashCode());
            _ground.Begin();
            int left = PixelMesh.ToGrid(-72f), right = PixelMesh.ToGrid(72f);
            switch (_map.Look)
            {
                case MapDefinition.TerrainLook.Ice:
                    // 얼음 껍질 — 땅 윗선 2px 아래로 한 줄 더 · 그 위로 군데군데 눈 더미(1px 볼록)
                    for (int x = left; x <= right; x++)
                    {
                        int y = SurfaceAt(x);
                        _ground.Rect(x, y - 3, 1, 1, Gray(0.3f));
                        if (random.Next(9) == 0)
                        {
                            int w = 2 + random.Next(3);
                            for (int k = 0; k < w; k++)
                                _ground.Rect(x + k, SurfaceAt(x + k) + 1, 1, 1, Gray(0.4f));
                            x += w;
                        }
                    }
                    break;
                case MapDefinition.TerrainLook.Grassland:
                    // 풀잎 — 1~3px, 촘촘히
                    for (int x = left; x <= right; x += 1 + random.Next(3))
                        _ground.Rect(x, SurfaceAt(x) + 1, 1, 1 + random.Next(3) / 2 + random.Next(2), Gray(0.3f));
                    break;
                case MapDefinition.TerrainLook.Jungle:
                    // 덩굴 — 땅에서 꼬불꼬불 오르는 줄기 3~7px + 끝에 잎 하나
                    for (int x = left; x <= right; x += 4 + random.Next(9))
                    {
                        int y = SurfaceAt(x) + 1, h = 3 + random.Next(5), lean = random.Next(2) == 0 ? -1 : 1;
                        for (int k = 0; k < h; k++)
                            _ground.Rect(x + ((k / 2) % 2 == 0 ? 0 : lean), y + k, 1, 1, Gray(0.32f));
                        _ground.Rect(x + lean * 2, y + h - 1, 1, 1, Gray(0.32f));
                    }
                    break;
                case MapDefinition.TerrainLook.Forest:
                    // 낙엽 · 잔가지 — 땅 위에 드문드문 2×1
                    for (int x = left; x <= right; x += 5 + random.Next(10))
                        _ground.Rect(x, SurfaceAt(x) + 1, 2, 1, Gray(0.3f));
                    break;
                case MapDefinition.TerrainLook.Swamp:
                    BuildPuddles();
                    break;
                default:
                    // 기본 — 자갈 한 알씩 드물게
                    for (int x = left; x <= right; x += 10 + random.Next(14))
                        _ground.Rect(x, SurfaceAt(x) + 1, 1, 1, Gray(0.25f));
                    break;
            }
            _ground.End();
        }

        // 늪지 — 골짜기마다 평평한 수면을 채운다: 전장의 가장 낮은 땅에서 높이차의 30%까지 물이 찬 것으로
        private void BuildPuddles()
        {
            int left = PixelMesh.ToGrid(-60f), right = PixelMesh.ToGrid(60f);
            int low = int.MaxValue, high = int.MinValue;
            for (int x = left; x <= right; x++)
            {
                low = Mathf.Min(low, SurfaceAt(x));
                high = Mathf.Max(high, SurfaceAt(x));
            }
            int level = low + Mathf.Max(3, Mathf.RoundToInt((high - low) * 0.3f));
            int start = int.MinValue;
            for (int x = left; x <= right + 1; x++)
            {
                bool wet = x <= right && SurfaceAt(x) < level;
                if (wet && start == int.MinValue)
                    start = x;
                else if (!wet && start != int.MinValue)
                {
                    if (x - start >= 6)
                        _puddles.Add((level, start, x - 1));
                    start = int.MinValue;
                }
            }
            foreach (var (y, l, r) in _puddles)
                for (int x = l; x <= r; x++)
                {
                    _ground.Rect(x, y, 1, 1, Gray(0.45f));
                    // 물 속 — 두 줄마다 흐린 가로줄
                    for (int k = y - 2; k > SurfaceAt(x); k -= 2)
                        _ground.Rect(x, k, 1, 1, Gray(0.12f));
                }
        }

        // ── ② 하늘 입자 — 화면 안에서 나고, 화면을 벗어나거나 수명이 다하면 사라진다
        private void DrawAir()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            float halfH = cam.orthographicSize + ViewMargin, halfW = cam.orthographicSize * cam.aspect + ViewMargin;
            Vector2 c = cam.transform.position;
            Rect view = new Rect(c.x - halfW, c.y - halfH, halfW * 2f, halfH * 2f);
            float dt = Time.deltaTime;

            _spawnDebt += dt * SpawnRate(_map.Look);
            while (_spawnDebt >= 1f)
            {
                _spawnDebt -= 1f;
                if (_particles.Count < Mathf.Min(MaxParticles, Cap(_map.Look)))
                    Spawn(view);
            }

            _air.Begin();
            float now = Time.time;
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle p = _particles[i];
                p.Age += dt;
                p.Position += p.Velocity * dt;
                if (p.Age >= p.Life || !view.Contains(p.Position) || p.Position.y < Ground.Instance.HeightAt(p.Position.x) - Pixel)
                {
                    _particles.RemoveAt(i);
                    continue;
                }
                _particles[i] = p;
                Draw(p, now);
            }
            // 늪지 수면의 반짝임 — 화면 안 웅덩이에서 몇 칸씩
            if (_map.Look == MapDefinition.TerrainLook.Swamp)
                foreach (var (y, l, r) in _puddles)
                    for (int x = l; x <= r; x += 3)
                        if (Mathf.Sin(x * 1.7f + now * 2.2f) > 0.93f && view.Contains(new Vector2(x * Pixel, y * Pixel)))
                            _air.Rect(x, y, 2, 1, Gray(0.7f));
            _air.End();
        }

        // 초당 새로 나는 수 · 동시 상한 — 유닛 수백 전투에서도 늘 이 안
        private static float SpawnRate(MapDefinition.TerrainLook look)
        {
            switch (look)
            {
                case MapDefinition.TerrainLook.Ice: return 30f;
                case MapDefinition.TerrainLook.Forest: return 3f;
                case MapDefinition.TerrainLook.Grassland: return 0f;   // 바람 줄은 뺐다(사용자 10/21 — 어색하다) · 풀잎은 땅에 그대로
                case MapDefinition.TerrainLook.Jungle: return 4f;
                case MapDefinition.TerrainLook.Swamp: return 4f;
                default: return 1f;
            }
        }

        private static int Cap(MapDefinition.TerrainLook look)
            => look == MapDefinition.TerrainLook.Ice ? 140 : look == MapDefinition.TerrainLook.Plain ? 12 : 30;

        private void Spawn(Rect view)
        {
            var p = new Particle { Phase = Random.value * 10f };
            float x = Random.Range(view.xMin, view.xMax);
            switch (_map.Look)
            {
                case MapDefinition.TerrainLook.Ice:      // 눈발 — 위에서 천천히, 옆으로 흔들리며
                    p.Position = new Vector2(x, view.yMax - 0.1f);
                    p.Velocity = new Vector2(Random.Range(-0.3f, 0.1f), Random.Range(-1.4f, -0.8f));
                    p.Life = 30f;
                    break;
                case MapDefinition.TerrainLook.Forest:   // 낙엽 — 더 느리게, 크게 흔들리며
                    p.Position = new Vector2(x, view.yMax - 0.1f);
                    p.Velocity = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(-0.7f, -0.45f));
                    p.Life = 40f;
                    break;
                case MapDefinition.TerrainLook.Jungle:   // 포자 — 땅에서 천천히 떠오른다
                    p.Position = new Vector2(x, Ground.Instance.HeightAt(x) + Random.Range(0.2f, 2f));
                    p.Velocity = new Vector2(Random.Range(-0.1f, 0.1f), Random.Range(0.15f, 0.35f));
                    p.Life = Random.Range(4f, 7f);
                    break;
                case MapDefinition.TerrainLook.Swamp:    // 거품 — 웅덩이 수면에서 짧게 오른다
                    if (_puddles.Count == 0)
                        return;
                    var (y, l, r) = _puddles[Random.Range(0, _puddles.Count)];
                    p.Position = new Vector2(Random.Range(l, r + 1) * Pixel, (y + 1) * Pixel);
                    if (!view.Contains(p.Position))
                        return;
                    p.Velocity = new Vector2(0f, Random.Range(0.25f, 0.4f));
                    p.Life = Random.Range(0.6f, 1f);
                    break;
                default:                                 // 먼지 — 느리게 떠다닌다
                    p.Position = new Vector2(x, Ground.Instance.HeightAt(x) + Random.Range(0.5f, 6f));
                    p.Velocity = new Vector2(Random.Range(0.1f, 0.3f), Random.Range(-0.05f, 0.05f));
                    p.Life = Random.Range(4f, 7f);
                    break;
            }
            _particles.Add(p);
        }

        private void Draw(Particle p, float now)
        {
            // 처음 · 끝 0.3초는 흐려진다
            float fade = Mathf.Clamp01(Mathf.Min(p.Age, p.Life - p.Age) / 0.3f);
            Vector2 at = p.Position;
            switch (_map.Look)
            {
                case MapDefinition.TerrainLook.Ice:
                    at.x += Mathf.Sin(now * 1.3f + p.Phase) * 0.25f;
                    _air.Square(at, 1, Gray(0.55f * fade));
                    break;
                case MapDefinition.TerrainLook.Forest:
                    at.x += Mathf.Sin(now * 1.1f + p.Phase) * 0.8f;
                    int g = PixelMesh.ToGrid(at.x), h = PixelMesh.ToGrid(at.y);
                    // 흔들리는 쪽으로 기운다 — 가로 2px · 세로 2px를 번갈아
                    if (Mathf.Cos(now * 1.1f + p.Phase) > 0f)
                        _air.Rect(g, h, 2, 1, Gray(0.4f * fade));
                    else
                        _air.Rect(g, h, 1, 2, Gray(0.4f * fade));
                    break;
                case MapDefinition.TerrainLook.Jungle:
                    at.x += Mathf.Sin(now * 0.9f + p.Phase) * 0.15f;
                    _air.Square(at, 1, Gray(0.4f * fade));
                    break;
                case MapDefinition.TerrainLook.Swamp:
                    _air.Square(at, 1, Gray(0.5f * fade));
                    break;
                default:
                    _air.Square(at, 1, Gray(0.25f * fade));
                    break;
            }
        }

        // ── ③ 효과 순간 — 탑 창 위에
        private void OnMapFiredAll(Team team)
        {
            _iceFlashAt[(int)team] = Time.time;
            Tower tower = Tower.For(team);
            BoardRunner board = BoardRunner.For(team);
            if (tower == null || board == null)
                return;
            if (MapMarker.For(team) is MapMarker marker)
                RingFx.Pulse(marker.Position, 1.25f, IceColor, null);
            Vector2 forward = new Vector2(team == Team.Ally ? 1f : -1f, 0f);
            for (int i = 0; i < Mathf.Min(tower.FloorCount, board.Slots.Length); i++)
                if (FiresOnTimer(board.Slots[i]) && FxDirector.Instance != null)
                    FxDirector.Instance.HitSpark(tower.FloorFront(i), forward, IceColor);
        }

        private void DrawTowers()
        {
            _tower.Begin();
            float rate = _map.CooldownRate;
            Color rateColor = rate < 1f ? IceColor : JungleColor;
            foreach (Team team in new[] { Team.Ally, Team.Enemy })
            {
                Tower tower = Tower.For(team);
                BoardRunner board = BoardRunner.For(team);
                if (tower == null || board == null)
                    continue;
                int cx = PixelMesh.ToGrid(tower.transform.position.x);
                int half = Mathf.RoundToInt(Mathf.Abs(tower.FloorFront(0).x - tower.transform.position.x) / Pixel);
                float flash = 1f - (Time.time - _iceFlashAt[(int)team]) / IceFlashSeconds;
                for (int i = 0; i < Mathf.Min(tower.FloorCount, board.Slots.Length); i++)
                {
                    BoardRunner.Slot slot = board.Slots[i];
                    int bottom = PixelMesh.ToGrid(tower.FloorBottom(i));
                    if (BoardRunner.StoppedByMap(slot.Building))
                        Flood(cx, half, bottom);
                    else if (!FiresOnTimer(slot))
                        continue;
                    else
                    {
                        if (rate != 1f && !BattleManager.BoardsStopped)
                            RateEdge(cx, bottom, Mathf.Clamp01(slot.Timer / slot.Cooldown), rateColor);
                        if (flash > 0f)
                            WindowOutline(cx, bottom, PixelMesh.Faded(IceColor, flash));
                    }
                }
            }
            _tower.End();
        }

        private static bool FiresOnTimer(BoardRunner.Slot slot) => !OwnedBuilding.IsEmpty(slot.Building) && slot.Cooldown > 0f;

        // 늪지가 끈 층 — 판이 열리면 물이 차올라 층 아래쪽이 잠긴다. 수면은 출렁인다
        private void Flood(int cx, int half, int bottom)
        {
            int level = Mathf.RoundToInt(FloodPixels * Mathf.Clamp01((Time.time - _openedAt) / FloodSeconds));
            if (level <= 0)
                return;
            float now = Time.time;
            for (int x = cx - half; x < cx + half; x++)
            {
                int top = level + (Mathf.Sin(x * 0.6f + now * 3f) > 0.3f ? 1 : 0);
                _tower.Rect(x, bottom, 1, top, PixelMesh.Faded(WaterColor, 0.3f));
                _tower.Rect(x, bottom + top, 1, 1, PixelMesh.Faded(WaterColor, 0.85f));
            }
        }

        // 맵 쿨다운 배율이 걸린 창 — 불빛이 차오르는 맨 윗줄을 지형 색으로(얼음 = 하늘색 · 정글 = 초록)
        private void RateEdge(int cx, int bottom, float fill, Color color)
        {
            int row = Mathf.FloorToInt(fill * WindowRows) - 1;
            if (row < 0)
                return;
            int w = WindowHalfAt(row);
            _tower.Rect(cx - w, bottom + WindowBottom + row, w * 2, 1, PixelMesh.Faded(color, 0.9f));
        }

        // 얼음 「전부 발동」 — 창 둘레 1px 테두리가 번쩍였다 사라진다
        private void WindowOutline(int cx, int bottom, Color32 color)
        {
            for (int row = -1; row <= WindowRows; row++)
            {
                int w = row < 0 ? WindowHalf : row >= WindowRows ? WindowHalfAt(WindowRows - 1) : WindowHalfAt(row);
                int y = bottom + WindowBottom + row;
                if (row < 0 || row >= WindowRows)
                    _tower.Rect(cx - w, y, w * 2, 1, color);
                else
                {
                    _tower.Rect(cx - w - 1, y, 1, 1, color);
                    _tower.Rect(cx + w, y, 1, 1, color);
                }
            }
        }

        // 창 안쪽 반폭(px) — 아래는 네모, 위는 반원(아치 · 둥근 창 둘 다 안에 들어오게)
        private static int WindowHalfAt(int row)
        {
            float above = row + 0.5f - (WindowRows - WindowHalf);
            return above <= 0f ? WindowHalf : Mathf.Max(1, Mathf.FloorToInt(Mathf.Sqrt(Mathf.Max(0f, WindowHalf * WindowHalf - above * above))));
        }

        private static int SurfaceAt(int gridX) => PixelMesh.ToGrid(Ground.Instance.HeightAt(gridX * Pixel));

        private static Color32 Gray(float alpha) => PixelMesh.Faded(Color.white, alpha);
    }
}
