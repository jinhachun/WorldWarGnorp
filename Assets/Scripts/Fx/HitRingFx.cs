using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 전장 탑의 맞음 표시(사용자 10/26 A안 · 10/27 링만) — 버프 선(BuffLinkFx)이 층에 닿는 순간 창틀 바깥으로 효과 색 링이 퍼진다(모양·시간은 HitRingShape).
    // 전부를 메시 하나로(PixelMesh) · 동시 상한. FxDirector가 만든다
    public class HitRingFx : MonoBehaviour
    {
        private const int Capacity = 48;
        private const int SortingOrder = 149;   // 유닛 위 · 버프 선(150) 아래
        private const int MaxQuads = 4096;

        private class Hit
        {
            public int X, Y, W, H;
            public Color Color;
            public bool Weak;
            public float Age;
        }

        private static HitRingFx _instance;
        private PixelMesh _mesh;
        private readonly List<Hit> _active = new List<Hit>();
        private readonly Stack<Hit> _free = new Stack<Hit>();
        private readonly List<HitRect> _rects = new List<HitRect>();

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "HitRings", material, SortingOrder, MaxQuads);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // 그 진영 탑의 slot층에 맞음 표시 — weak = 늘 켜진 효과의 재알림(링만 얇게)
        public static void Show(Team team, int slot, Color color, bool weak)
        {
            Tower tower = Tower.For(team);
            if (_instance == null || tower == null || slot < 0 || slot >= tower.FloorCount || _instance._active.Count >= Capacity)
                return;
            Rect box = tower.FloorRect(slot);
            Hit hit = _instance._free.Count > 0 ? _instance._free.Pop() : new Hit();
            hit.X = PixelMesh.ToGrid(box.xMin);
            hit.Y = PixelMesh.ToGrid(box.yMin);
            hit.W = Mathf.RoundToInt(box.width / PixelMesh.Pixel);
            hit.H = Mathf.RoundToInt(box.height / PixelMesh.Pixel);
            hit.Color = color;
            hit.Weak = weak;
            hit.Age = 0f;
            _instance._active.Add(hit);
        }

        private void LateUpdate()
        {
            _mesh.Begin();
            // 한 프레임이 길어도 표시가 건너뛰어지지 않게 프레임당 진행을 묶는다(버프 선과 같게)
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Hit hit = _active[i];
                hit.Age += dt;
                if (hit.Age >= HitRingShape.End)
                {
                    _free.Push(hit);
                    _active.RemoveAt(i);
                    continue;
                }
                HitRingShape.Rects(_rects, hit.X, hit.Y, hit.W, hit.H, hit.Age, hit.Weak);
                foreach (HitRect r in _rects)
                    _mesh.Rect(r.X, r.Y, r.W, r.H, PixelMesh.Faded(hit.Color, r.Alpha));
            }
            _mesh.End();
        }
    }
}
