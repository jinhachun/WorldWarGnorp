using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 솟는 기둥(전투 시각화 F6) — 발밑에서 키만큼 1px 흰 세로 막대가 올라가고 꼭대기에 초록 점 하나. 다시 일어나기 · 그 자리 소환 · 재건.
    // 기둥 전부를 메시 하나로(PixelMesh) · 동시 상한. FxDirector가 만든다
    public class PillarFx : MonoBehaviour
    {
        private const float Seconds = 0.3f;
        // 이 몫 동안 올라가고, 나머지 동안 흐려진다
        private const float RiseShare = 0.5f;
        private const int Capacity = 48;
        private const int TopDot = 2;
        private const int SortingOrder = 140;

        private struct Pillar
        {
            public Vector2 Feet;
            public float Height;
            public float Age;
        }

        private static PillarFx _instance;
        private PixelMesh _mesh;
        private readonly List<Pillar> _active = new List<Pillar>();

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "Pillars", material, SortingOrder, Capacity * 2);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public static void Rise(Vector2 feet, float height)
        {
            if (_instance != null && _instance._active.Count < Capacity)
                _instance._active.Add(new Pillar { Feet = feet, Height = Mathf.Max(height, 0.5f) });
        }

        private void LateUpdate()
        {
            _mesh.Begin();
            float dt = Time.deltaTime;
            Color green = BuffLinkFx.ColorOf(BuffKind.Summon);
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Pillar p = _active[i];
                p.Age += dt;
                if (p.Age >= Seconds)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                _active[i] = p;
                float t = p.Age / Seconds;
                float grown = Mathf.Clamp01(t / RiseShare);
                float alpha = t < RiseShare ? 1f : 1f - (t - RiseShare) / (1f - RiseShare);
                int x = PixelMesh.ToGrid(p.Feet.x), y = PixelMesh.ToGrid(p.Feet.y);
                int h = Mathf.Max(1, Mathf.RoundToInt(PixelMesh.ToGrid(p.Height) * grown));
                _mesh.Rect(x, y, 1, h, PixelMesh.Faded(Color.white, alpha));
                _mesh.Rect(x - TopDot / 2, y + h, TopDot, TopDot, PixelMesh.Faded(green, alpha));
            }
            _mesh.End();
        }
    }
}
