using UnityEngine;

namespace GnorpWar
{
    // 굽은 땅 — 높이 함수(사인 몇 개를 겹친 비대칭 언덕)로 충돌(PolygonCollider2D)과 그림(LineRenderer)을 만든다.
    // 씬을 불러올 때마다(전투마다) 물결의 위상·높이를 무작위로 바꾼다.
    // 기지·소환 지점이 있는 양 끝은 평평하고, 탑(Flat Anchors) 밑도 평평한 단을 만들어 탑 밑면을 그 위에 맞춘다.
    // 높이가 필요한 곳(소환)은 HeightAt을 쓴다
    [RequireComponent(typeof(PolygonCollider2D), typeof(LineRenderer))]
    public class Ground : MonoBehaviour
    {
        private const float Step = 0.5f;
        private const float BottomDepth = 8f;
        private const float LineWidth = 0.25f;
        // 기본 물결 (높이, 파장, 위상) — 전투마다 위상은 새로 뽑고 높이는 이 범위 배율로 흔든다
        private static readonly Vector3[] BaseWaves =
        {
            new Vector3(1.6f, 52f, 0.7f),
            new Vector3(0.9f, 34f, 2.1f),
            new Vector3(0.25f, 13f, 4.0f),
        };
        private const float MinAmplitudeScale = 0.6f;
        private const float MaxAmplitudeScale = 1.4f;

        [Tooltip("밑이 평평해야 하는 것(탑) — 콜라이더 폭 + 여유만큼 평평한 단을 만들고, 밑면을 단 위에 맞춰 옮긴다")]
        [SerializeField] private BoxCollider2D[] _flatAnchors = new BoxCollider2D[0];
        [Tooltip("평평한 단의 양옆 여유 폭")]
        [SerializeField] private float _flatMargin = 1f;
        [Tooltip("평평한 단에서 언덕으로 서서히 바뀌는 폭")]
        [SerializeField] private float _flatBlend = 4f;

        private Vector3[] _waves = BaseWaves;
        // 평평한 단: (중심 x, 반폭, 높이)
        private Vector3[] _plateaus = new Vector3[0];

        [Tooltip("평평한 곳의 땅 윗면 높이")]
        [SerializeField] private float _baseY = -3.89f;
        [Tooltip("언덕 전체를 위로 올리는 값 — 화면 아래가 y -7.2(CameraRig y 12.8 − 크기 20)라 골짜기가 너무 깊으면 잘린다")]
        [SerializeField] private float _lift = 0.7f;
        [SerializeField] private float _minX = -72f;
        [SerializeField] private float _maxX = 72f;
        [Tooltip("|x|가 이 값보다 크면 평평(기지·소환 자리)")]
        [SerializeField] private float _flatFrom = 64f;
        [Tooltip("평평한 곳에서 언덕 높이까지 서서히 바뀌는 폭")]
        [SerializeField] private float _blendWidth = 10f;

        public static Ground Instance { get; private set; }

        public float HeightAt(float x)
        {
            float height = HillsAt(x);
            // 단에 가까울수록 단 높이로 — 단 안쪽은 정확히 평평
            foreach (Vector3 p in _plateaus)
            {
                float t = Mathf.SmoothStep(0f, 1f, (Mathf.Abs(x - p.x) - p.y) / _flatBlend);
                height = Mathf.Lerp(p.z, height, t);
            }
            return height;
        }

        private float HillsAt(float x)
        {
            float hills = _lift;
            foreach (Vector3 w in _waves)
                hills += w.x * Mathf.Sin(2f * Mathf.PI * x / w.y + w.z);
            float blend = Mathf.SmoothStep(0f, 1f, (_flatFrom - Mathf.Abs(x)) / _blendWidth);
            return _baseY + hills * blend;
        }

        private void Awake()
        {
            Instance = this;
            Randomize();
            Build();
            PlaceAnchors();
        }

        private void Randomize()
        {
            _waves = new Vector3[BaseWaves.Length];
            for (int i = 0; i < BaseWaves.Length; i++)
            {
                Vector3 w = BaseWaves[i];
                _waves[i] = new Vector3(w.x * Random.Range(MinAmplitudeScale, MaxAmplitudeScale), w.y, Random.Range(0f, 2f * Mathf.PI));
            }

            // 단 높이 = 그 자리의 원래 언덕 높이(단을 만들기 전 값)
            _plateaus = new Vector3[_flatAnchors.Length];
            for (int i = 0; i < _flatAnchors.Length; i++)
            {
                float x = _flatAnchors[i].transform.position.x;
                float halfWidth = _flatAnchors[i].size.x * 0.5f + _flatMargin;
                _plateaus[i] = new Vector3(x, halfWidth, HillsAt(x));
            }
        }

        // 탑 밑면(콜라이더 아래 끝)을 단 위에 맞춘다
        private void PlaceAnchors()
        {
            for (int i = 0; i < _flatAnchors.Length; i++)
            {
                BoxCollider2D box = _flatAnchors[i];
                Transform t = box.transform;
                float bottom = t.position.y + (box.offset.y - box.size.y * 0.5f) * t.lossyScale.y;
                t.position += Vector3.up * (_plateaus[i].z - bottom);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        [ContextMenu("Build")]
        public void Build()
        {
            int count = Mathf.CeilToInt((_maxX - _minX) / Step) + 1;
            var surface = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Min(_minX + i * Step, _maxX);
                surface[i] = new Vector2(x, HeightAt(x));
            }

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
