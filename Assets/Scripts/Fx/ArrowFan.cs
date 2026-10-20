using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 갈래 발사(전투 시각화 F2) — 한 번에 여러 발을 쏘면 발마다 화살 그림만 옆으로 잠깐 벌렸다 모은다(사용자 10/20 Q2: 판정 · 궤적은 그대로).
    // 그림 칸(Projectile.Body)의 비행 방향 수직 자리만 움직인다 — 물리 몸은 건드리지 않는다. FxDirector가 만든다
    public class ArrowFan : MonoBehaviour
    {
        // 발 사이 벌어짐(2px) · 벌렸다 모으는 시간
        private const float Step = 2f * PixelMesh.Pixel;
        private const float Seconds = 0.3f;
        private const int Capacity = 256;

        private struct Spread
        {
            public Projectile Shot;
            public int Flight;
            public float Offset;
            public float Age;
        }

        private static ArrowFan _instance;
        private readonly List<Spread> _active = new List<Spread>();

        private void Awake() => _instance = this;

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // 한 번에 쏜 count발 중 index번째 — 가운데를 0으로 양옆에 벌린다
        public static void Fan(Projectile shot, int index, int count)
        {
            if (_instance == null || count <= 1 || _instance._active.Count >= Capacity)
                return;
            _instance._active.Add(new Spread { Shot = shot, Flight = shot.Flight, Offset = (index - (count - 1) * 0.5f) * Step });
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Spread s = _active[i];
                bool same = s.Shot.isActiveAndEnabled && s.Shot.Flight == s.Flight;
                s.Age += dt;
                if (!same || s.Age >= Seconds)
                {
                    if (same)
                        s.Shot.Body.localPosition = Vector3.zero;
                    _active.RemoveAt(i);
                    continue;
                }
                // 0 → 벌어짐 → 0 (가운데에서 가장 넓다) — 격자에 맞춘다
                float y = s.Offset * Mathf.Sin(Mathf.PI * s.Age / Seconds);
                s.Shot.Body.localPosition = new Vector3(0f, Mathf.Round(y / PixelMesh.Pixel) * PixelMesh.Pixel, 0f);
                _active[i] = s;
            }
        }
    }
}
