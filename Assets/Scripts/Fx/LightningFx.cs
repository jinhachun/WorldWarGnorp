using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 번개(주술사의 공격) — 두 점 사이에 꺾인 줄이 번쩍였다가 사라진다. 색은 이펙트라 허용 —
    // 3픽셀 파란 띠 위에 1픽셀 밝은 심(사용자 11/1: 파랗고 굵게, 눈에 잘 보이게).
    // 번개 전부를 메시 하나로(PixelMesh). 피해는 Unit.ChainLightning이 바로 주고, 이건 연출뿐. 게임 시간으로 흐른다. FxDirector가 만든다
    public class LightningFx : MonoBehaviour
    {
        private const int Capacity = 8192;
        private const int SortingOrder = 159;     // 불꽃 꼬리와 같은 층
        private const float Life = 0.25f;
        private const float SegmentLength = 0.6f; // 칸 — 이만큼마다 한 번 꺾인다
        private const float Jitter = 0.3f;        // 칸 — 꺾인 점이 곧은 선에서 벗어나는 최대 거리
        private const int MaxBolts = 96;
        private const int GlowSize = 3;           // 픽셀 — 파란 띠 굵기

        private static readonly Color Core = new Color(0.8f, 0.92f, 1f);
        private static readonly Color Glow = new Color(0.2f, 0.45f, 1f);

        private struct Bolt
        {
            public Vector2[] Points;
            public float Age;
        }

        private static LightningFx _instance;
        private PixelMesh _mesh;
        private readonly List<Bolt> _bolts = new List<Bolt>();

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "Lightning", material, SortingOrder, Capacity);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // from에서 to까지 번개 한 줄기
        public static void Strike(Vector2 from, Vector2 to)
        {
            if (_instance == null || _instance._bolts.Count >= MaxBolts)
                return;
            Vector2 along = to - from;
            int segments = Mathf.Max(1, Mathf.CeilToInt(along.magnitude / SegmentLength));
            Vector2 side = along.sqrMagnitude > 0.0001f ? new Vector2(-along.y, along.x).normalized : Vector2.up;
            var points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float offset = i == 0 || i == segments ? 0f : Random.Range(-Jitter, Jitter);
                points[i] = from + along * t + side * offset;
            }
            _instance._bolts.Add(new Bolt { Points = points });
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            _mesh.Begin();
            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                Bolt bolt = _bolts[i];
                bolt.Age += dt;
                if (bolt.Age >= Life)
                {
                    _bolts[i] = _bolts[_bolts.Count - 1];
                    _bolts.RemoveAt(_bolts.Count - 1);
                    continue;
                }
                _bolts[i] = bolt;
            }
            // 띠를 전부 먼저 깔고 심을 위에 — 다른 번개의 띠가 심을 덮지 않게
            foreach (Bolt bolt in _bolts)
                Draw(bolt, GlowSize, Glow);
            foreach (Bolt bolt in _bolts)
                Draw(bolt, 1, Core);
            _mesh.End();
        }

        private void Draw(Bolt bolt, int size, Color color)
        {
            float t = bolt.Age / Life;
            Color32 faded = PixelMesh.Faded(color, t < 0.5f ? 1f : (1f - t) / 0.5f);
            for (int s = 0; s + 1 < bolt.Points.Length; s++)
            {
                Vector2 a = bolt.Points[s], b = bolt.Points[s + 1];
                int steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / PixelMesh.Pixel));
                for (int k = 0; k < steps; k++)
                    _mesh.Square(Vector2.Lerp(a, b, (float)k / steps), size, faded);
            }
        }
    }
}
