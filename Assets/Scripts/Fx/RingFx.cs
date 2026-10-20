using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 퍼지는 고리(전투 시각화 F4) — 1px 원이 짧게 반지름 0에서 효과 반지름까지 퍼지며 흐려진다. 광역 공격의 실제 범위 · 타워 사거리가 바뀐 순간의 새 사거리.
    // 고리 전부를 메시 하나로(PixelMesh) · 동시 상한 · 같은 출처는 솎는다. FxDirector가 만든다
    public class RingFx : MonoBehaviour
    {
        private const float Seconds = 0.3f;
        private const int Capacity = 24;
        private const int SortingOrder = 140;   // 유닛 위 · 버프 선(150) 아래
        private const int MaxQuads = 8192;

        private struct Ring
        {
            public Vector2 Center;
            public float Radius;
            public Color Color;
            public float Age;
        }

        private static RingFx _instance;
        private PixelMesh _mesh;
        private readonly List<Ring> _active = new List<Ring>();
        private readonly Dictionary<object, float> _last = new Dictionary<object, float>();

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "Rings", material, SortingOrder, MaxQuads);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // source = 솎는 열쇠(같은 유닛 · 타워) — gap초 안에 또 오면 건너뛴다
        public static void Pulse(Vector2 center, float radius, Color color, object source, float gap = 0.5f)
        {
            if (_instance == null || radius <= 0f || _instance._active.Count >= Capacity)
                return;
            float now = Time.time;
            if (source != null && _instance._last.TryGetValue(source, out float last) && now - last < gap && now >= last)
                return;
            if (source != null)
                _instance._last[source] = now;
            _instance._active.Add(new Ring { Center = center, Radius = radius, Color = color });
        }

        private void LateUpdate()
        {
            _mesh.Begin();
            float dt = Time.deltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Ring ring = _active[i];
                ring.Age += dt;
                if (ring.Age >= Seconds)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                _active[i] = ring;
                float t = ring.Age / Seconds;
                Write(ring.Center, ring.Radius * (1f - (1f - t) * (1f - t)), PixelMesh.Faded(ring.Color, 1f - t * t));
            }
            _mesh.End();
            if (_active.Count == 0 && _last.Count > 64)
                _last.Clear();
        }

        // 격자 원 — 둘레를 1px씩 걸으며 같은 칸은 한 번만
        private void Write(Vector2 center, float radius, Color32 color)
        {
            float pixels = radius / PixelMesh.Pixel;
            if (pixels < 1f)
                return;
            int steps = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * pixels));
            int cx = PixelMesh.ToGrid(center.x), cy = PixelMesh.ToGrid(center.y);
            int lastX = int.MinValue, lastY = int.MinValue;
            for (int s = 0; s < steps; s++)
            {
                float a = s * 2f * Mathf.PI / steps;
                int x = cx + Mathf.RoundToInt(Mathf.Cos(a) * pixels), y = cy + Mathf.RoundToInt(Mathf.Sin(a) * pixels);
                if (x == lastX && y == lastY)
                    continue;
                lastX = x;
                lastY = y;
                _mesh.Rect(x, y, 1, 1, color);
            }
        }
    }
}
