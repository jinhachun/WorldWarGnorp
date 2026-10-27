using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 전장 탑의 대상 표시(사용자 10/27 「더 바자르처럼」) — 툴팁을 띄운 층 기물의 효과를 받는 층의 네 모서리에 흰 꺾쇠(모양은 TargetMarkShape · 검은 1px 받침).
    // 전장 탑 호버(UI/TowerHover)가 매 프레임 Show(진영, 층들)을 부르고, 안 부른 프레임에는 지운다. 메시 하나(PixelMesh). FxDirector가 만든다
    public class TargetMarkFx : MonoBehaviour
    {
        private const int SortingOrder = 170;   // 유닛 호버 꺾쇠(HoverMark)와 같은 층
        // 층 테두리에서 안쪽으로 1px · 굵기 1px · 팔 3px(유닛 꺾쇠와 같은 팔)
        private const int Inset = 1;
        private const int Thick = 1;
        private const int Arm = 3;

        private static TargetMarkFx _instance;
        private PixelMesh _mesh;
        private Tower _tower;
        private readonly List<int> _floors = new List<int>();
        private readonly List<HitRect> _rects = new List<HitRect>();
        private int _shownFrame = -1;

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "TargetMarks", material, SortingOrder, 512);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // 이번 프레임에 보일 층들 — 다음 프레임에 다시 부르지 않으면 사라진다
        public static void Show(Team team, List<int> floors)
        {
            if (_instance == null)
                return;
            _instance._tower = Tower.For(team);
            _instance._floors.Clear();
            _instance._floors.AddRange(floors);
            _instance._shownFrame = Time.frameCount;
        }

        private void LateUpdate()
        {
            _mesh.Begin();
            if (_shownFrame == Time.frameCount && _tower != null)
                foreach (int floor in _floors)
                {
                    if (floor < 0 || floor >= _tower.FloorCount)
                        continue;
                    Rect box = _tower.FloorRect(floor);
                    int x = PixelMesh.ToGrid(box.xMin), y = PixelMesh.ToGrid(box.yMin);
                    int w = Mathf.RoundToInt(box.width / PixelMesh.Pixel), h = Mathf.RoundToInt(box.height / PixelMesh.Pixel);
                    // 검은 받침을 먼저(사방 1px 크게), 흰 꺾쇠를 그 위에 — 흰 벽 위에서도 읽히게
                    Draw(x, y, w, h, Inset - 1, Thick + 2, Arm + 2, Color.black);
                    Draw(x, y, w, h, Inset, Thick, Arm, Color.white);
                }
            _mesh.End();
        }

        private void Draw(int x, int y, int w, int h, int inset, int thick, int arm, Color32 color)
        {
            TargetMarkShape.Rects(_rects, x, y, w, h, inset, thick, arm);
            foreach (HitRect r in _rects)
                _mesh.Rect(r.X, r.Y, r.W, r.H, color);
        }
    }
}
