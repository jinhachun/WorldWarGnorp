using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 월드 지도의 「살아 있는 세계」(사용자 10/26) — 지형마다 날리는 것 · 새 떼 · 바다 반짝임 · 영주 땅 연기와 불빛 · 산맥 불티.
    // 지도 RawImage의 첫 자식인 투명 오버레이(지도와 같은 칸 해상도 · Point)에 칸 단위 점으로 찍는다 — 표시 칸 · 영주 그림 아래에 그려지고 확대 · 끌기를 따라간다.
    // 텍스처 하나 · 입자 수 상한 · 틱 갱신. 시간은 unscaled(팝업이 timeScale을 멈춰도 지도는 살아 있게)
    [RequireComponent(typeof(RawImage))]
    public class WorldAmbience : MonoBehaviour
    {
        // 다시 그리는 간격(초) · 한 틱에 흐르는 시간 상한(초 — 멈췄다 돌아와도 입자가 순간이동하지 않게)
        private const float Tick = 1f / 15f, MaxStep = 0.25f;
        // 살아 있는 입자 상한(새 떼는 따로)
        private const int MaxMotes = 1500;
        // 땅 위 입자(지형 · 불티)가 1초에 생기는 수 — 땅 넓이에 고르게
        private const float LandPerSecond = 220f;
        // 바다 반짝임이 1초에 켜지는 수
        private const float SparkPerSecond = 6f;
        // 굴뚝 하나가 연기를 내는 간격(초)
        private const float SmokeGap = 0.55f;
        // 새 떼가 나오는 간격(초) · 빠르기(칸/초) · 마리 수
        private const float FlockGapMin = 7f, FlockGapMax = 15f, FlockSpeed = 28f;
        private const int FlockMin = 3, FlockMax = 5;
        // 나타나고 사라지는 부분(수명 비율)
        private const float FadeIn = 0.2f, FadeOut = 0.35f;
        // 표시 칸(지역 특수 · 이벤트 칸 · 왕관) 둘레를 이만큼(칸) 비운다 — 새만 지나간다
        private const int MarkPad = 1;
        // 굴뚝 불빛 밝기(깜박임 아래 · 위)
        private const float LightMin = 0.6f, LightMax = 1f;
        private static readonly Color32 LightColor = new Color32(255, 160, 48, 255);
        private static readonly Color32 BirdColor = new Color32(240, 240, 240, 255);
        private const float BirdAlpha = 0.85f;
        // 새 그림 — 날개 든 「v」 · 내린 「^」
        private static readonly string[][] BirdFrames = { new[] { "#.#", ".#." }, new[] { ".#.", "#.#" } };

        private sealed class Kind
        {
            public Color32[] Colors;
            public float Alpha;
            // 처음 빠르기(칸/초, y는 아래가 +) · 아래로 당기는 힘
            public float VxMin, VxMax, VyMin, VyMax, Gravity;
            public float LifeMin, LifeMax;
            // 좌우 흔들림 폭(칸) · 빠르기
            public float SwayAmp, SwayFreq;
            // 깜박임 빠르기(0 = 안 깜박인다)
            public float Pulse;
            // 그 땅 칸이 뽑히면 한꺼번에 내는 수 — 점이 작거나 어두운 것(반딧불 · 흙먼지)과 가는 산맥 띠의 불티를 같은 땅 넓이에서 더 보이게
            public int Count = 1;
            // 난 땅 밖으로 나가면 사라진다
            public bool Bound;
            // 색이 수명 동안 Colors[0] → Colors[1]로 바뀐다(아니면 색 하나를 골라 쓴다)
            public bool Shift;
            // 가운데 무렵 십자로 빛난다(바다 반짝임) · 오른쪽에 옅은 꼬리 칸(흙먼지) · 늙으면 두 칸으로 퍼진다(연기)
            public bool Cross, Trail, Grow;
        }

        private const int Snow = 0, Leaf = 1, Firefly = 2, Pollen = 3, Seed = 4, Dust = 5, Ember = 6, Spark = 7, Smoke = 8;
        private static readonly Kind[] Kinds =
        {
            new Kind { Colors = new[] { new Color32(255, 255, 255, 255) }, Alpha = 1f, VxMin = -0.3f, VxMax = 0.5f, VyMin = 2.5f, VyMax = 3.5f, LifeMin = 3f, LifeMax = 5f, SwayAmp = 0.8f, SwayFreq = 2f, Bound = true },   // 눈송이
            new Kind { Colors = new[] { new Color32(222, 128, 52, 255), new Color32(196, 84, 44, 255), new Color32(234, 182, 70, 255) }, Alpha = 0.95f, VxMin = 1f, VxMax = 2f, VyMin = 1.5f, VyMax = 2.5f, LifeMin = 2.5f, LifeMax = 4f, SwayAmp = 1.5f, SwayFreq = 3f, Bound = true },   // 낙엽
            new Kind { Colors = new[] { new Color32(206, 255, 110, 255) }, Alpha = 1f, VxMin = -0.8f, VxMax = 0.8f, VyMin = -0.8f, VyMax = 0.8f, LifeMin = 3f, LifeMax = 5f, SwayAmp = 1.2f, SwayFreq = 1.3f, Pulse = 4f, Count = 2, Bound = true },   // 반딧불
            new Kind { Colors = new[] { new Color32(255, 226, 120, 255), new Color32(255, 180, 200, 255) }, Alpha = 1f, VxMin = -0.5f, VxMax = 0.5f, VyMin = -1.2f, VyMax = -0.6f, LifeMin = 3f, LifeMax = 5f, SwayAmp = 0.8f, SwayFreq = 1.8f, Bound = true },   // 꽃가루
            new Kind { Colors = new[] { new Color32(255, 255, 236, 255) }, Alpha = 1f, VxMin = 2.5f, VxMax = 4f, VyMin = -0.4f, VyMax = 0.2f, LifeMin = 2.5f, LifeMax = 4f, SwayAmp = 0.5f, SwayFreq = 2.5f, Bound = true },   // 풀씨
            new Kind { Colors = new[] { new Color32(252, 230, 176, 255) }, Alpha = 0.85f, VxMin = 4f, VxMax = 6f, VyMin = -0.2f, VyMax = 0f, LifeMin = 1f, LifeMax = 1.8f, Count = 2, Trail = true, Bound = true },   // 흙먼지
            new Kind { Colors = new[] { new Color32(255, 222, 110, 255), new Color32(226, 76, 30, 255) }, Alpha = 1f, VxMin = -1.5f, VxMax = 1.5f, VyMin = -8f, VyMax = -5f, Gravity = 9f, LifeMin = 0.7f, LifeMax = 1.2f, Count = 4, Shift = true, Bound = true },   // 불티
            new Kind { Colors = new[] { new Color32(255, 255, 255, 255) }, Alpha = 0.9f, LifeMin = 0.5f, LifeMax = 1f, Cross = true },   // 바다 반짝임
            new Kind { Colors = new[] { new Color32(150, 150, 150, 255) }, Alpha = 0.7f, VxMin = 0.4f, VxMax = 0.8f, VyMin = -2.2f, VyMax = -1.6f, LifeMin = 2f, LifeMax = 3f, SwayAmp = 0.5f, SwayFreq = 2f, Grow = true },   // 연기
        };

        // 지형(전투 맵 에셋 이름) → 날리는 것. 표에 없는 지형은 아무것도 안 날린다
        private static readonly Dictionary<string, int> TerrainKinds = new Dictionary<string, int>
        {
            { "Map_Ice", Snow }, { "Map_Forest", Leaf }, { "Map_Swamp", Firefly }, { "Map_Jungle", Pollen }, { "Map_Grassland", Seed }, { "Map_Plain", Dust },
        };

        private struct Mote
        {
            public float X, Y, Vx, Vy, Age, Life, Phase;
            public int Kind, Region, Tint;
        }

        private struct Bird
        {
            public float X, Y, Phase;
        }

        private RawImage _image;
        private Texture2D _texture;
        private Color32[] _buffer;
        private int[] _cells;
        private int _width, _height;
        // WorldMapView가 Show마다 고쳐 쓰는 배열(같은 참조) — 땅마다 주인 · 이벤트 땅 상태(0 = 이벤트 땅 아님)
        private int[] _owner;
        private byte[] _events;
        private bool[] _blocked;
        private int[] _terrainKind;
        private int[] _land, _sea;
        // 땅마다 굴뚝 칸(없으면 x < 0) · 다음 연기 시각
        private Vector2Int[] _chimneys;
        private float[] _smokeAt;
        private readonly List<Mote> _motes = new List<Mote>();
        private readonly List<Bird> _flock = new List<Bird>();
        private float _flockX, _flockY, _flockVx, _flockVy, _flockAt;
        private float _landDebt, _sparkDebt;
        private float _last, _next;
        // 게임 쪽 난수(UnityEngine.Random)를 건드리지 않게 따로
        private readonly System.Random _random = new System.Random(1026);

        public void Init(Transform map, WorldMapDefinition definition, int[] cells, int width, int height, int[] owner, byte[] events, RectInt[] marks)
        {
            var rect = (RectTransform)transform;
            rect.SetParent(map, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _cells = cells;
            _width = width;
            _height = height;
            _owner = owner;
            _events = events;
            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _buffer = new Color32[width * height];
            _texture.SetPixels32(_buffer);
            _texture.Apply();
            _image = GetComponent<RawImage>();
            _image.texture = _texture;
            _image.raycastTarget = false;

            _blocked = new bool[width * height];
            foreach (RectInt mark in marks)
                for (int y = mark.yMin - MarkPad; y < mark.yMax + MarkPad; y++)
                    for (int x = mark.xMin - MarkPad; x < mark.xMax + MarkPad; x++)
                        if (x >= 0 && y >= 0 && x < width && y < height)
                            _blocked[y * width + x] = true;

            int regions = definition.RegionCount;
            _terrainKind = new int[regions];
            for (int region = 0; region < regions; region++)
                _terrainKind[region] = TerrainKinds.TryGetValue(definition.TerrainOf(region).name, out int kind) ? kind : -1;
            var land = new List<int>();
            var sea = new List<int>();
            for (int i = 0; i < cells.Length; i++)
                (cells[i] < 0 ? sea : land).Add(i);
            _land = land.ToArray();
            _sea = sea.ToArray();

            _chimneys = new Vector2Int[regions];
            _smokeAt = new float[regions];
            for (int region = 0; region < regions; region++)
            {
                _chimneys[region] = FindChimney(region, marks[region]);
                _smokeAt[region] = (float)_random.NextDouble() * SmokeGap;
            }
            _flockAt = Time.unscaledTime + Range(2f, FlockGapMin);
            _last = Time.unscaledTime;
        }

        // 표시 칸 바로 옆(가까운 곳부터) — 그 땅 안 · 비운 칸 밖이고, 위로 연기가 오를 세 칸도 그 땅인 자리
        private Vector2Int FindChimney(int region, RectInt mark)
        {
            int baseY = mark.yMax - 2;
            for (int d = 1; d <= 14; d++)
                foreach (Vector2Int at in new[] { new Vector2Int(mark.xMax + MarkPad + d, baseY), new Vector2Int(mark.xMin - MarkPad - 1 - d, baseY), new Vector2Int(mark.xMax + MarkPad + d, baseY + d), new Vector2Int(mark.xMin - MarkPad - 1 - d, baseY + d) })
                {
                    bool fits = true;
                    for (int up = 0; up <= 3 && fits; up++)
                        fits = CellAt(at.x, at.y - up) == region && !_blocked[(at.y - up) * _width + at.x];
                    if (fits)
                        return at;
                }
            return new Vector2Int(-1, -1);
        }

        private int CellAt(int x, int y) => x < 0 || y < 0 || x >= _width || y >= _height ? WorldMapDefinition.Sea : _cells[y * _width + x];

        private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now < _next)
                return;
            _next = now + Tick;
            Step(now, Mathf.Min(now - _last, MaxStep));
            _last = now;
        }

        // 한 틱 — 새로 내고 · 움직이고 · 다시 그린다
        private void Step(float now, float dt)
        {
            Spawn(now, dt);
            for (int i = _motes.Count - 1; i >= 0; i--)
            {
                Mote mote = _motes[i];
                Kind kind = Kinds[mote.Kind];
                mote.Age += dt;
                mote.Vy += kind.Gravity * dt;
                mote.X += mote.Vx * dt;
                mote.Y += mote.Vy * dt;
                bool gone = mote.Age >= mote.Life || mote.X < 0f || mote.Y < 0f || mote.X >= _width || mote.Y >= _height
                    || kind.Bound && CellAt(Mathf.FloorToInt(mote.X), Mathf.FloorToInt(mote.Y)) != mote.Region;
                if (gone)
                {
                    // 순서는 상관없다 — 끝 것을 끌어와 지운다
                    _motes[i] = _motes[_motes.Count - 1];
                    _motes.RemoveAt(_motes.Count - 1);
                }
                else
                    _motes[i] = mote;
            }
            MoveFlock(now, dt);
            Draw(now);
        }

        private void Spawn(float now, float dt)
        {
            // 땅 위 — 고른 칸의 땅이 이벤트 땅(산맥)이면 불티, 아니면 그 지형의 것
            _landDebt += LandPerSecond * dt;
            for (; _landDebt >= 1f; _landDebt--)
            {
                int cell = _land[_random.Next(_land.Length)];
                int region = _cells[cell];
                int kind = _events[region] != 0 ? Ember : _terrainKind[region];
                if (kind < 0 || _blocked[cell])
                    continue;
                for (int n = Kinds[kind].Count; n > 0; n--)
                    Add(kind, cell % _width + 0.5f, cell / _width + 0.5f, region);
            }
            _sparkDebt += SparkPerSecond * dt;
            for (; _sparkDebt >= 1f; _sparkDebt--)
            {
                int cell = _sea[_random.Next(_sea.Length)];
                Add(Spark, cell % _width + 0.5f, cell / _width + 0.5f, WorldMapDefinition.Sea);
            }
            // 영주가 가진 땅의 굴뚝
            for (int region = 0; region < _chimneys.Length; region++)
            {
                if (_chimneys[region].x < 0 || _owner[region] < 0 || _events[region] != 0 || now < _smokeAt[region])
                    continue;
                _smokeAt[region] = now + SmokeGap * Range(0.7f, 1.3f);
                Add(Smoke, _chimneys[region].x + 0.5f, _chimneys[region].y - 0.5f, region);
            }
        }

        private void Add(int kind, float x, float y, int region)
        {
            if (_motes.Count >= MaxMotes)
                return;
            Kind k = Kinds[kind];
            _motes.Add(new Mote
            {
                X = x, Y = y, Vx = Range(k.VxMin, k.VxMax), Vy = Range(k.VyMin, k.VyMax),
                Life = Range(k.LifeMin, k.LifeMax), Phase = Range(0f, 6.283f),
                Kind = kind, Region = region, Tint = k.Shift ? 0 : _random.Next(k.Colors.Length),
            });
        }

        // 새 떼 — 한 떼씩, 지도 한쪽 끝에서 다른 끝으로 「V」 꼴로
        private void MoveFlock(float now, float dt)
        {
            if (_flock.Count == 0)
            {
                if (now < _flockAt)
                    return;
                bool right = _random.Next(2) == 0;
                _flockX = right ? -8f : _width + 8f;
                _flockY = Range(_height * 0.1f, _height * 0.7f);
                _flockVx = (right ? 1f : -1f) * FlockSpeed;
                _flockVy = Range(-0.15f, 0.15f) * FlockSpeed;
                int count = _random.Next(FlockMin, FlockMax + 1);
                for (int i = 0; i < count; i++)
                {
                    // 앞장 하나 뒤로 양쪽에 번갈아 — 줄마다 4칸 뒤 · 3칸 옆
                    int rank = (i + 1) / 2, side = i % 2 == 0 ? 1 : -1;
                    _flock.Add(new Bird { X = -rank * 4f * Mathf.Sign(_flockVx) + Range(-0.5f, 0.5f), Y = rank * 3f * side, Phase = Range(0f, 6.283f) });
                }
                return;
            }
            _flockX += _flockVx * dt;
            _flockY += _flockVy * dt;
            if (_flockX < -20f || _flockX > _width + 20f)
            {
                _flock.Clear();
                _flockAt = now + Range(FlockGapMin, FlockGapMax);
            }
        }

        private void Draw(float now)
        {
            System.Array.Clear(_buffer, 0, _buffer.Length);
            // 굴뚝 불빛 — 영주 땅의 굴뚝 밑 한 칸이 일렁인다
            for (int region = 0; region < _chimneys.Length; region++)
                if (_chimneys[region].x >= 0 && _owner[region] >= 0 && _events[region] == 0)
                    Plot(_chimneys[region].x, _chimneys[region].y, LightColor, Mathf.Lerp(LightMin, LightMax, Mathf.PerlinNoise(now * 3f, region * 1.7f)), false);
            foreach (Mote mote in _motes)
            {
                Kind kind = Kinds[mote.Kind];
                float t = mote.Age / mote.Life;
                float alpha = kind.Cross ? kind.Alpha * (1f - Mathf.Abs(2f * t - 1f)) : kind.Alpha * Mathf.Clamp01(Mathf.Min(t / FadeIn, (1f - t) / FadeOut));
                if (kind.Pulse > 0f)
                    alpha *= 0.35f + 0.65f *Mathf.Max(0f, Mathf.Sin(mote.Age * kind.Pulse + mote.Phase));
                Color32 color = kind.Shift ? Color32.Lerp(kind.Colors[0], kind.Colors[1], t) : kind.Colors[mote.Tint];
                int x = Mathf.FloorToInt(mote.X + kind.SwayAmp * Mathf.Sin(mote.Age * kind.SwayFreq + mote.Phase)), y = Mathf.FloorToInt(mote.Y);
                Plot(x, y, color, alpha, false);
                if (kind.Cross && t > 0.35f && t < 0.65f)
                {
                    Plot(x - 1, y, color, alpha * 0.5f, false);
                    Plot(x + 1, y, color, alpha * 0.5f, false);
                    Plot(x, y - 1, color, alpha * 0.5f, false);
                    Plot(x, y + 1, color, alpha * 0.5f, false);
                }
                if (kind.Trail)
                    Plot(x - (mote.Vx > 0f ? 1 : -1), y, color, alpha * 0.45f, false);
                if (kind.Grow && t > 0.45f)
                    Plot(x + 1, y, color, alpha * 0.7f, false);
            }
            foreach (Bird bird in _flock)
            {
                string[] frame = BirdFrames[Mathf.FloorToInt(now * 4f + bird.Phase) & 1];
                int left = Mathf.FloorToInt(_flockX + bird.X) - 1, top = Mathf.FloorToInt(_flockY + bird.Y);
                for (int sy = 0; sy < frame.Length; sy++)
                    for (int sx = 0; sx < frame[sy].Length; sx++)
                        if (frame[sy][sx] == '#')
                            Plot(left + sx, top + sy, BirdColor, BirdAlpha, true);
            }
            _texture.SetPixels32(_buffer);
            _texture.Apply();
        }

        // 칸 (x, y)(위 왼쪽이 0) — 겹치면 진한 쪽. overMarks = 표시 칸 둘레도 찍는다(새)
        private void Plot(int x, int y, Color32 color, float alpha, bool overMarks)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height || !overMarks && _blocked[y * _width + x])
                return;
            int index = (_height - 1 - y) * _width + x;
            byte a = (byte)(Mathf.Clamp01(alpha) * 255f);
            if (a > _buffer[index].a)
                _buffer[index] = new Color32(color.r, color.g, color.b, a);
        }

        private void OnDestroy()
        {
            if (_texture != null)
                Destroy(_texture);
        }
    }
}
