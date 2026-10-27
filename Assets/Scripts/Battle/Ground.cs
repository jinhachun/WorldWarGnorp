using UnityEngine;

namespace GnorpWar
{
    // 굽은 땅 — 판마다 이번 맵(RunState.Map)의 지형 스타일로 언덕·골짜기·고원을 무작위로 놓고, 잔물결을 더해 높이 표를 만든다.
    // 표로 충돌(PolygonCollider2D)과 그림(LineRenderer)을 만들고, 높이가 필요한 곳(소환)은 HeightAt(표를 잇는다)을 쓴다.
    // 기지·소환 지점이 있는 양 끝은 평평하고, 탑(Flat Anchors) 밑도 평평한 단을 만들어 밑면을 그 위에 맞춘다.
    // 경사는 스타일의 상한을 넘지 않게 깎는다(유닛이 걸어 오를 수 있게).
    // 이번 맵은 BattleManager.Awake가 상점에 들어서며 정한다 — 그보다 늦게 Awake 하도록 실행 순서를 뒤로 둔다
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PolygonCollider2D), typeof(LineRenderer))]
    public class Ground : MonoBehaviour
    {
        private const float Step = 0.5f;
        private const float BottomDepth = 8f;
        private const float LineWidth = 0.25f;
        // 잔물결 (파장, 비중) — 높이는 스타일의 Roughness
        private static readonly Vector2[] Ripples = { new Vector2(13f, 0.6f), new Vector2(7f, 0.4f) };
        // 덩어리를 놓는 범위 — 평평한 끝에서 이만큼 안쪽까지(골짜기가 성 앞을 파지 않게)
        private const float FeatureEdgeMargin = 4f;
        // 그리는 순서 — 뒷배경(겹마다 +1) < 땅 속 < 탑(-1) < 유닛·땅 선(0)
        private const int BackdropSortingOrder = -30;
        public const int FillSortingOrder = -10;

        [Tooltip("밑이 평평해야 하는 것(탑) — 콜라이더 폭 + 여유만큼 평평한 단을 만들고, 밑면을 단 위에 맞춰 옮긴다")]
        [SerializeField] private BoxCollider2D[] _flatAnchors = new BoxCollider2D[0];
        [Tooltip("평평한 단의 양옆 여유 폭")]
        [SerializeField] private float _flatMargin = 1f;
        [Tooltip("평평한 단에서 언덕으로 서서히 바뀌는 폭")]
        [SerializeField] private float _flatBlend = 4f;

        [Tooltip("평평한 곳의 땅 윗면 높이")]
        [SerializeField] private float _baseY = -3.89f;
        [Tooltip("언덕 전체를 위로 올리는 값")]
        [SerializeField] private float _lift = 0.7f;
        [Tooltip("평평한 곳보다 이만큼 넘게 파이지 않는다 — 화면 아래가 y -7.2(CameraRig y 12.8 − 크기 20)라 골짜기가 너무 깊으면 잘린다")]
        [SerializeField] private float _maxDepth = 2.5f;
        [Tooltip("평평한 곳보다 이만큼 넘게 솟지 않는다")]
        [SerializeField] private float _maxHeight = 7f;
        [SerializeField] private float _minX = -72f;
        [SerializeField] private float _maxX = 72f;
        [Tooltip("|x|가 이 값보다 크면 평평(기지·소환 자리)")]
        [SerializeField] private float _flatFrom = 64f;
        [Tooltip("평평한 곳에서 언덕 높이까지 서서히 바뀌는 폭")]
        [SerializeField] private float _blendWidth = 10f;

        // Step 간격의 땅 높이(월드 y) — _minX부터
        private float[] _heights;

        public static Ground Instance { get; private set; }
        // 맵 양 끝(월드 x) — 본진 무너짐 카메라(Fx/BaseCollapse)가 화면을 이 안에 묶는다
        public float MinX => _minX;
        public float MaxX => _maxX;

        // 땅 윗면의 높이 — 원래 지형 + 그 위에 쌓인 뼛가루(전투 중에 자란다)
        public float HeightAt(float x)
        {
            if (_heights == null)
                Generate(new MapDefinition.TerrainStyle());
            return Sample(_heights, x) + (_dust != null ? Sample(_dust, x) : 0f);
        }

        private float Sample(float[] table, float x)
        {
            float t = Mathf.Clamp((x - _minX) / Step, 0f, table.Length - 1);
            int i = Mathf.Min((int)t, table.Length - 2);
            return Mathf.Lerp(table[i], table[i + 1], t - i);
        }

        // ── 뼛가루 언덕(트라이브 명세 §2-2) — 유닛이 죽은 자리에 쏟아진 가루가 칸마다 쌓이고, 윗면의 기울기가 상한을 넘으면 낮은 옆 칸으로 흘러 완만한 더미가 된다.
        // 알갱이마다 물리를 돌리지 않는다 — 칸별 두께 표(_dust)만 세고, 그 윗면을 유닛이 딛는 땅(둘째 PolygonCollider2D)으로 만든다. 전투 씬마다 새로 뜨므로 전투가 끝나면 사라진다.
        // 더미가 자랄 때는 그 칸 위의 유닛을 자란 만큼 같이 들어 올린다 — 물리에 맡기면 쌓인 산이 튄다
        [Tooltip("뼛가루 두께가 이 값 이상인 자리를 「뼛가루 언덕 위」로 친다")]
        [SerializeField] private float _dustMinDepth = 0.1f;
        [Tooltip("양쪽 본진(성)의 앞면에서 이 거리 안과 그 뒤쪽에는 뼛가루가 쌓이지도 흘러들지도 않는다 — 그리로 갈 가루는 경계 안쪽(전장 쪽)에 쌓인다")]
        [SerializeField] private float _dustClearance = 6f;

        // [칸] = 뼛가루가 못 쌓이는 칸(본진 앞 · 뒤) — 가루가 처음 쌓일 때 본진 자리에서 정한다
        private bool[] _dustBlocked;

        // Step 간격의 뼛가루 두께 — _heights와 같은 칸. 아직 한 번도 안 쌓였으면 null
        private float[] _dust;
        // 쏟아지는 중인 가루들 — 자리 · 남은 양(칸²) · 초당 쏟는 양
        private readonly System.Collections.Generic.List<(float x, float left, float rate)> _pours = new System.Collections.Generic.List<(float, float, float)>();
        // 흘러내리는 기울기 상한(세로 ÷ 가로) — 마지막으로 쏟은 가루의 값
        private float _dustSlope = 0.5f;
        private float[] _dustBefore;
        private PolygonCollider2D _dustCollider;
        private LineRenderer _dustLine;
        private Mesh _dustMesh;
        private readonly System.Collections.Generic.List<Collider2D> _dustOverlaps = new System.Collections.Generic.List<Collider2D>();
        private readonly System.Collections.Generic.HashSet<Unit> _dustLifted = new System.Collections.Generic.HashSet<Unit>();
        private readonly System.Collections.Generic.HashSet<Base> _dustLiftedTowers = new System.Collections.Generic.HashSet<Base>();
        // 아직 흘러내리는 중인가 — 쏟기가 끝나도 다 자리 잡을 때까지 계속 센다
        private bool _dustMoving;
        private static readonly ContactFilter2D SolidOnly = new ContactFilter2D { useTriggers = false };

        // 맵에서 가장 낮은 땅(원래 지형)의 높이 — 유닛의 고도가 여기서부터 잰다
        public float LowestY { get; private set; }

        // x 자리에 쌓인 뼛가루의 두께(칸) · 그 자리가 뼛가루 언덕 위인가(맵의 원래 언덕과 구분된다)
        public float DustAt(float x) => _dust != null ? Sample(_dust, x) : 0f;
        public bool OnBoneDust(float x) => DustAt(x) >= _dustMinDepth;

        // 뼛가루가 가장 두껍게 쌓인 자리의 x(뼛가루 언덕이 없으면 false) — 뼈 투척(BoneThrowAction)이 그 언덕 위에서 던진다
        public bool DustPeak(out float x)
        {
            x = 0f;
            if (_dust == null)
                return false;
            float best = _dustMinDepth;
            bool found = false;
            for (int i = 0; i < _dust.Length; i++)
                if (_dust[i] >= best)
                {
                    best = _dust[i];
                    x = XAt(i);
                    found = true;
                }
            return found;
        }

        // x 자리에 뼛가루 area(칸² — 옆에서 본 넓이)를 seconds 동안 쏟는다. maxSlope = 더미가 흘러내리는 기울기 상한
        public void AddBoneDust(float x, float area, float maxSlope, float seconds)
        {
            if (area <= 0f)
                return;
            _dustSlope = maxSlope;
            _pours.Add((x, area, area / Mathf.Max(seconds, Time.fixedDeltaTime)));
        }

        private void FixedUpdate()
        {
            if (_pours.Count == 0 && !_dustMoving)
                return;
            if (_dust == null)
                StartDust();
            System.Array.Copy(_dust, _dustBefore, _dust.Length);
            for (int p = _pours.Count - 1; p >= 0; p--)
            {
                (float x, float left, float rate) = _pours[p];
                float poured = Mathf.Min(left, rate * Time.fixedDeltaTime);
                // 넓이를 칸 폭으로 나누면 그 칸의 두께 — 두 칸 사이에 떨어지면 가까운 만큼 나눠 준다
                float t = Mathf.Clamp((x - _minX) / Step, 0f, _dust.Length - 1);
                int i = Mathf.Min((int)t, _dust.Length - 2);
                _dust[Open(i)] += poured / Step * (1f - (t - i));
                _dust[Open(i + 1)] += poured / Step * (t - i);
                if (left - poured <= 0f)
                    _pours.RemoveAt(p);
                else
                    _pours[p] = (x, left - poured, rate);
            }
            _dustMoving = !SettleDust();
            RebuildDust();
            LiftUnits();
        }

        // i칸이 막힌 칸이면 전장 쪽으로 가장 가까운 열린 칸
        private int Open(int i)
        {
            int toward = XAt(i) < 0f ? 1 : -1;
            while (_dustBlocked[i] && i + toward >= 0 && i + toward < _dustBlocked.Length)
                i += toward;
            return i;
        }

        private void StartDust()
        {
            _dust = new float[_heights.Length];
            _dustBefore = new float[_heights.Length];
            // 본진(성) 앞면에서 _dustClearance 안과 그 뒤는 막는다 — 본진이 이미 무너졌으면 그쪽은 막지 않는다
            _dustBlocked = new bool[_heights.Length];
            foreach (Team team in new[] { Team.Ally, Team.Enemy })
            {
                Base home = Base.For(team);
                if (home == null)
                    continue;
                for (int i = 0; i < _dustBlocked.Length; i++)
                    _dustBlocked[i] |= team == Team.Ally ? XAt(i) < home.Bounds.max.x + _dustClearance : XAt(i) > home.Bounds.min.x - _dustClearance;
            }
            var ground = GetComponent<PolygonCollider2D>();
            _dustCollider = gameObject.AddComponent<PolygonCollider2D>();
            _dustCollider.sharedMaterial = ground.sharedMaterial;
            var groundLine = GetComponent<LineRenderer>();
            var body = new GameObject("BoneDust");
            body.transform.SetParent(transform, false);
            _dustLine = body.AddComponent<LineRenderer>();
            _dustLine.useWorldSpace = false;
            _dustLine.sharedMaterial = groundLine.sharedMaterial;
            _dustLine.startColor = _dustLine.endColor = groundLine.startColor;
            _dustLine.widthMultiplier = LineWidth;
            _dustLine.sortingLayerID = groundLine.sortingLayerID;
            _dustLine.sortingOrder = groundLine.sortingOrder;
            // 더미 속 — 검은 바탕에 흰 알갱이가 박힌 무늬(8칸당 1칸 = 그림 1픽셀). 원래 땅 선 위에 덮인다
            _dustMesh = new Mesh();
            body.AddComponent<MeshFilter>().mesh = _dustMesh;
            var fill = body.AddComponent<MeshRenderer>();
            fill.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = GrainTexture() };
            fill.sortingOrder = FillSortingOrder + 1;
        }

        private const int GrainSize = 32;
        private const float GrainPixelsPerUnit = 8f;

        private static Texture2D GrainTexture()
        {
            var texture = new Texture2D(GrainSize, GrainSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var random = new System.Random(7);
            var pixels = new Color32[GrainSize * GrainSize];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = random.Next(4) == 0 ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // 흘러내리기 — 윗면(지형 + 가루)의 이웃 칸 높이 차가 상한을 넘으면 넘는 만큼의 절반을 낮은 쪽으로 옮긴다(가진 가루까지만). 더 옮길 것이 없을 때까지
        private const int SettlePasses = 400;

        // 다 자리 잡았으면 true
        private bool SettleDust()
        {
            float maxDelta = _dustSlope * Step;
            for (int pass = 0; pass < SettlePasses; pass++)
            {
                bool moved = false;
                for (int i = 0; i < _dust.Length; i++)
                {
                    if (_dust[i] <= 0f)
                        continue;
                    for (int j = i - 1; j <= i + 1; j += 2)
                    {
                        if (j < 0 || j >= _dust.Length)
                            continue;
                        // 막힌 칸(본진 앞)으로는 못 흐른다 — 그쪽 비탈은 아래 Lean이 맞춘다
                        if (_dustBlocked[j])
                            continue;
                        float over = _heights[i] + _dust[i] - (_heights[j] + _dust[j]) - maxDelta;
                        if (over <= 0.001f)
                            continue;
                        float flow = Mathf.Min(over * 0.5f, _dust[i]);
                        _dust[i] -= flow;
                        _dust[j] += flow;
                        moved = true;
                    }
                }
                moved |= Lean(0, 1, maxDelta) | Lean(_dust.Length - 1, -1, maxDelta);
                if (!moved)
                    return true;
            }
            return false;
        }

        // 막힌 구역의 경계에서 전장 쪽으로 — 윗면이 「본진 쪽 이웃 칸 + 상한」보다 높으면 넘는 만큼을 통째로 전장 쪽 다음 칸에 넘긴다.
        // 그래서 경계에 벽이 서지 않고 본진 쪽 비탈도 상한 기울기로 눕는다(양은 그대로). edge = 그쪽 맨 끝 칸 · toward = 전장 쪽 방향
        private bool Lean(int edge, int toward, float maxDelta)
        {
            int i = edge;
            while (i >= 0 && i < _dust.Length && _dustBlocked[i])
                i += toward;
            if (i == edge || i < 0 || i >= _dust.Length)
                return false;   // 그쪽에 막힌 구역이 없다(본진이 없다)
            bool moved = false;
            float behind = _heights[i - toward];
            for (; i + toward >= 0 && i + toward < _dust.Length; i += toward)
            {
                float over = Mathf.Min(_heights[i] + _dust[i] - behind - maxDelta, _dust[i]);
                if (over <= 0.001f)
                    break;
                _dust[i] -= over;
                _dust[i + toward] += over;
                moved = true;
                behind = _heights[i] + _dust[i];
            }
            return moved;
        }

        // 가루가 있는 칸들(양옆 한 칸 더)의 윗면으로 충돌 · 선 · 속을 다시 만든다
        private void RebuildDust()
        {
            int first = System.Array.FindIndex(_dust, d => d > 0f), last = System.Array.FindLastIndex(_dust, d => d > 0f);
            if (first < 0)
                return;
            first = Mathf.Max(0, first - 1);
            last = Mathf.Min(_dust.Length - 1, last + 1);
            int count = last - first + 1;
            var path = new Vector2[count * 2];
            var vertices = new Vector3[count * 2];
            var uv = new Vector2[count * 2];
            var triangles = new int[(count - 1) * 6];
            _dustLine.positionCount = count;
            for (int k = 0; k < count; k++)
            {
                int i = first + k;
                var top = new Vector2(XAt(i), _heights[i] + _dust[i]);
                var bottom = new Vector2(XAt(i), _heights[i]);
                path[k] = top;
                // 충돌의 밑면은 원래 땅보다 한 칸 아래 — 두께가 0인 칸에서도 모양이 찌그러지지 않는다
                path[count * 2 - 1 - k] = bottom + Vector2.down;
                vertices[k * 2] = top;
                vertices[k * 2 + 1] = bottom;
                uv[k * 2] = top * (GrainPixelsPerUnit / GrainSize);
                uv[k * 2 + 1] = bottom * (GrainPixelsPerUnit / GrainSize);
                _dustLine.SetPosition(k, top + Vector2.down * (LineWidth * 0.5f));
            }
            for (int k = 0; k < count - 1; k++)
            {
                int v = k * 2, t = k * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }
            _dustCollider.SetPath(0, path);
            _dustMesh.Clear();
            _dustMesh.vertices = vertices;
            _dustMesh.uv = uv;
            _dustMesh.triangles = triangles;
        }

        // 이번 스텝에 자란 칸 위의 유닛 전부(아군을 밟고 선 유닛까지)를 그 자리에서 자란 만큼 올린다
        private void LiftUnits()
        {
            int first = -1, last = -1;
            for (int i = 0; i < _dust.Length; i++)
                if (_dust[i] - _dustBefore[i] > 0.0001f)
                {
                    if (first < 0)
                        first = i;
                    last = i;
                }
            if (first < 0)
                return;
            Physics2D.OverlapArea(new Vector2(XAt(first) - Step, _baseY - BottomDepth), new Vector2(XAt(last) + Step, _baseY + 1000f), SolidOnly, _dustOverlaps);
            _dustLifted.Clear();
            _dustLiftedTowers.Clear();
            foreach (Collider2D col in _dustOverlaps)
            {
                // 작은 타워(감시탑 · 건축가의 타워 · 전초 기지)도 유닛처럼 같이 올린다 — 본진은 그 앞에 가루가 안 쌓인다
                if (col.TryGetComponent(out Base tower))
                {
                    float under = Sample(_dust, tower.transform.position.x) - Sample(_dustBefore, tower.transform.position.x);
                    if (tower.IsSmall && under > 0f && _dustLiftedTowers.Add(tower))
                        tower.transform.position += Vector3.up * under;
                    continue;
                }
                if (!col.TryGetComponent(out Unit unit) || !_dustLifted.Add(unit))
                    continue;
                float x = unit.Position.x;
                float rise = Sample(_dust, x) - Sample(_dustBefore, x);
                if (rise > 0f)
                    unit.Lift(rise);
            }
        }

        private float XAt(int i) => Mathf.Min(_minX + i * Step, _maxX);

        private void Awake()
        {
            Instance = this;
            Generate(RunState.Map != null ? RunState.Map.Terrain : new MapDefinition.TerrainStyle());
            Build();
            PlaceAnchors();
            BuildFill();
            PlaceBackdrop(RunState.Map);
        }

        // 땅 속 — 땅 선 아래를 검게 채운다. 뒷배경이 땅 밑으로 비치지 않게(언덕이 뒷배경을 가린다)
        private void BuildFill()
        {
            int count = _heights.Length;
            var vertices = new Vector3[count * 2];
            var triangles = new int[(count - 1) * 6];
            for (int i = 0; i < count; i++)
            {
                vertices[i * 2] = new Vector3(XAt(i), _heights[i]);
                vertices[i * 2 + 1] = new Vector3(XAt(i), _baseY - BottomDepth);
            }
            for (int i = 0; i < count - 1; i++)
            {
                int v = i * 2, t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }
            var fill = new GameObject("Fill");
            fill.transform.SetParent(transform, false);
            fill.AddComponent<MeshFilter>().mesh = new Mesh { vertices = vertices, triangles = triangles };
            var fillRenderer = fill.AddComponent<MeshRenderer>();
            // 땅 선의 재질(Sprite-Lit)은 이 메시를 그리지 않는다 — 내장 스프라이트 셰이더(늘 빌드에 들어간다)를 쓴다
            fillRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.black };
            fillRenderer.sortingOrder = FillSortingOrder;
        }

        // 먼 산 실루엣의 밝기(HSV V) = Base + 겹 밝기 × Scale — 시안(Captures/BattleBg)에서 고른 값
        private const float BackdropValueBase = 0.08f, BackdropValueScale = 1.2f;

        // 맵 뒷배경 — 겹마다 실루엣을 맵 폭만큼 늘어놓는다. 밑변은 평평한 땅 기준이라 언덕 뒤로는 가려진다
        private void PlaceBackdrop(MapDefinition map)
        {
            if (map == null)
                return;
            for (int l = 0; l < map.Backdrop.Length; l++)
            {
                MapDefinition.BackdropLayer layer = map.Backdrop[l];
                if (layer.Sprites.Length == 0)
                    continue;
                var root = new GameObject("Backdrop" + l).AddComponent<BackdropParallax>();
                root.transform.SetParent(transform, false);
                root.Factor = layer.Parallax;
                // 실루엣 = 그 지형의 지도 색(MapColor)의 색상 · 채도 + 겹 밝기로 정한 어두운 톤(사용자 10/27 3안 — 하늘은 검게 두고 먼 산만 지형색)
                Color.RGBToHSV(map.MapColor, out float hue, out float saturation, out _);
                var color = Color.HSVToRGB(hue, saturation, BackdropValueBase + layer.Brightness * BackdropValueScale);
                // 간격이 0이면 끝나지 않는다 — 반 칸 아래로는 안 내려간다
                for (float x = _minX; x <= _maxX; x += Mathf.Max(0.5f, Random.Range(layer.Spacing.x, layer.Spacing.y)))
                {
                    Sprite sprite = layer.Sprites[Random.Range(0, layer.Sprites.Length)];
                    var piece = new GameObject(sprite.name).AddComponent<SpriteRenderer>();
                    piece.transform.SetParent(root.transform, false);
                    piece.transform.localPosition = new Vector3(x, _baseY + Random.Range(layer.Bottom.x, layer.Bottom.y) - sprite.bounds.min.y, 0f);
                    piece.sprite = sprite;
                    piece.color = color;
                    piece.flipX = Random.value < 0.5f;
                    piece.sortingOrder = BackdropSortingOrder + l;
                }
            }
        }

        private void Generate(MapDefinition.TerrainStyle style)
        {
            int count = Mathf.CeilToInt((_maxX - _minX) / Step) + 1;
            var rel = new float[count];   // 평평한 곳(_baseY) 기준 높이

            // 잔물결
            for (int r = 0; r < Ripples.Length; r++)
            {
                float phase = Random.Range(0f, 2f * Mathf.PI);
                for (int i = 0; i < count; i++)
                    rel[i] += style.Roughness * Ripples[r].y * Mathf.Sin(2f * Mathf.PI * XAt(i) / Ripples[r].x + phase);
            }

            // 덩어리 — 언덕(코사인 봉우리) · 고원(윗면 평평) · 골짜기(아래로)
            int features = Random.Range(style.FeatureCount.x, style.FeatureCount.y + 1);
            for (int f = 0; f < features; f++)
            {
                float half = Random.Range(style.FeatureWidth.x, style.FeatureWidth.y) * 0.5f;
                float reach = Mathf.Max(0f, _flatFrom - FeatureEdgeMargin - half);
                float center = Random.Range(-reach, reach);
                bool valley = Random.value < style.ValleyChance;
                bool plateau = !valley && Random.value < style.PlateauChance;
                float height = Random.Range(style.FeatureHeight.x, style.FeatureHeight.y) * (valley ? -1f : 1f);
                for (int i = 0; i < count; i++)
                {
                    float d = Mathf.Abs(XAt(i) - center) / half;
                    if (d >= 1f)
                        continue;
                    float shape = plateau ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.5f, d)) : 0.5f * (1f + Mathf.Cos(Mathf.PI * d));
                    rel[i] += height * shape;
                }
            }

            // 높이 한도 · 양 끝으로 갈수록 평평
            for (int i = 0; i < count; i++)
            {
                float blend = Mathf.SmoothStep(0f, 1f, (_flatFrom - Mathf.Abs(XAt(i))) / _blendWidth);
                rel[i] = Mathf.Clamp(_lift + rel[i], -_maxDepth, _maxHeight) * blend;
            }

            _heights = new float[count];
            for (int i = 0; i < count; i++)
                _heights[i] = _baseY + rel[i];

            // 탑 밑 평평한 단 — 단 높이 = 그 자리의 땅 높이
            foreach (BoxCollider2D anchor in _flatAnchors)
            {
                float x = anchor.transform.position.x;
                float halfWidth = anchor.size.x * 0.5f + _flatMargin;
                float top = HeightAt(x);
                for (int i = 0; i < count; i++)
                    _heights[i] = Mathf.Lerp(top, _heights[i], Mathf.SmoothStep(0f, 1f, (Mathf.Abs(XAt(i) - x) - halfWidth) / _flatBlend));
            }

            LimitSlope(style.MaxSlope * Step);
            LowestY = Mathf.Min(_heights);
        }

        // 봉우리를 깎아 이웃 칸과의 높이 차를 maxDelta 이하로 — 앞뒤로 한 번씩이면 어디서나 경사가 상한 안이다.
        // 깎기만 하므로 평평한 끝(높이 0 근처)은 그대로 남는다
        private void LimitSlope(float maxDelta)
        {
            for (int i = 1; i < _heights.Length; i++)
                _heights[i] = Mathf.Min(_heights[i], _heights[i - 1] + maxDelta);
            for (int i = _heights.Length - 2; i >= 0; i--)
                _heights[i] = Mathf.Min(_heights[i], _heights[i + 1] + maxDelta);
        }

        // 탑 밑면(콜라이더 아래 끝)을 단 위에 맞춘다
        private void PlaceAnchors()
        {
            foreach (BoxCollider2D box in _flatAnchors)
            {
                Transform t = box.transform;
                float bottom = t.position.y + (box.offset.y - box.size.y * 0.5f) * t.lossyScale.y;
                t.position += Vector3.up * (HeightAt(t.position.x) - bottom);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // 에디터 메뉴 Build는 (아직 표가 없으면) 기본 스타일로 한 판을 만들어 보여 준다
        [ContextMenu("Build")]
        public void Build()
        {
            if (_heights == null)
                Generate(new MapDefinition.TerrainStyle());
            int count = _heights.Length;
            var surface = new Vector2[count];
            for (int i = 0; i < count; i++)
                surface[i] = new Vector2(XAt(i), _heights[i]);

            var path = new Vector2[count + 2];
            surface.CopyTo(path, 0);
            path[count] = new Vector2(_maxX, _baseY - BottomDepth);
            path[count + 1] = new Vector2(_minX, _baseY - BottomDepth);
            GetComponent<PolygonCollider2D>().SetPath(0, path);

            // 선의 윗면이 땅 윗면과 맞도록 두께 절반만큼 내린다
            var line = GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.widthMultiplier = LineWidth;
            line.positionCount = count;
            for (int i = 0; i < count; i++)
                line.SetPosition(i, surface[i] + Vector2.down * (LineWidth * 0.5f));
        }
    }
}
