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
        private const int FillSortingOrder = -10;

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

        public float HeightAt(float x)
        {
            if (_heights == null)
                Generate(new MapDefinition.TerrainStyle());
            float t = Mathf.Clamp((x - _minX) / Step, 0f, _heights.Length - 1);
            int i = Mathf.Min((int)t, _heights.Length - 2);
            return Mathf.Lerp(_heights[i], _heights[i + 1], t - i);
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
                var color = new Color(layer.Brightness, layer.Brightness, layer.Brightness, 1f);
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
