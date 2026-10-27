using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 불꽃 꼬리(사용자 10/27) — 처형인의 도끼 · 관통탄이 지나간 자리에 픽셀 불씨가 남아 노랑 → 주황 → 빨강으로 식으며 작아지고 사그라든다.
    // 머리(방금 지난 자리) = 노랑 · 꼬리 = 빨강이라, 꼬리부터 머리 쪽으로 읽으면 사용자 원문 순서 「빨강 → 주황 → 노랑」이다.
    // 불씨 전부를 메시 하나로(PixelMesh). 화살 몫은 상한을 낮게 두어 도끼 몫이 늘 남는다. 게임 시간으로 흐른다(히트스톱 때 같이 멈춘다). FxDirector가 만든다
    public class FireTrails : MonoBehaviour
    {
        private const int Capacity = 1024;
        private const int ArrowCapacity = 640;   // 화살 불씨는 여기까지 — 넘치면 그 불씨는 버린다
        private const int SortingOrder = 159;    // 회복(158) 위 · 도끼(160) 아래
        private const float Rise = 0.6f;         // 칸/초 — 불씨가 살짝 떠오른다

        private static readonly Color Hot = new Color(1f, 0.95f, 0.45f);
        private static readonly Color Warm = new Color(1f, 0.55f, 0.1f);
        private static readonly Color Cool = new Color(0.85f, 0.12f, 0.08f);

        private struct Ember
        {
            public Vector2 Point;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public int Size;
        }

        private static FireTrails _instance;
        private PixelMesh _mesh;
        private readonly List<Ember> _embers = new List<Ember>();

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "FireTrails", material, SortingOrder, Capacity);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // 불 색 — t = 0 노랑(뜨거움) · 0.3 주황 · 0.6부터 빨강(식음). 빨강이 흐려지기(0.7) 전에 또렷이 보이게 일찍 닿는다
        public static Color Heat(float t)
            => t < 0.3f ? Color.Lerp(Hot, Warm, t / 0.3f) : Color.Lerp(Warm, Cool, (t - 0.3f) / 0.3f);

        // 불씨 하나 — size = 처음 픽셀 크기(수명 따라 1px까지 준다)
        public static void Emit(Vector2 point, Vector2 drift, int size, float life, bool arrow)
        {
            if (_instance == null || _instance._embers.Count >= (arrow ? ArrowCapacity : Capacity))
                return;
            _instance._embers.Add(new Ember
            {
                Point = point,
                Velocity = drift + Vector2.up * Rise,
                Life = life,
                Size = size,
            });
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            _mesh.Begin();
            for (int i = _embers.Count - 1; i >= 0; i--)
            {
                Ember e = _embers[i];
                e.Age += dt;
                if (e.Age >= e.Life)
                {
                    // 순서는 상관없다 — 끝 것을 당겨 와 지운다
                    _embers[i] = _embers[_embers.Count - 1];
                    _embers.RemoveAt(_embers.Count - 1);
                    continue;
                }
                e.Point += e.Velocity * dt;
                _embers[i] = e;
                float t = e.Age / e.Life;
                // 크기는 계단으로 처음의 30%까지(최소 1px) · 마지막 30%만 흐려진다
                int size = Mathf.Max(1, Mathf.CeilToInt(e.Size * (1f - 0.7f * t)));
                _mesh.Square(e.Point, size, PixelMesh.Faded(Heat(t), t < 0.7f ? 1f : (1f - t) / 0.3f));
            }
            _mesh.End();
        }
    }
}
