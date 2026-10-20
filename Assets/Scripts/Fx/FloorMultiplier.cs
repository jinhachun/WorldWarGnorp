using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 층 배수 글자(전투 시각화 F7) — 한 번 발동에 효과를 여러 번 낸 층의 앞쪽에 「×2」가 잠깐 뜬다(늪지 · 물레방앗간 신화 · 군주제 — 곱해지면 「×4」).
    // 글꼴은 데미지 숫자(DamageNumbers.Glyphs)의 3×5 + 「×」, 검은 1px 테두리. 글자 전부를 메시 하나로(PixelMesh). FxDirector가 만든다
    public class FloorMultiplier : MonoBehaviour
    {
        private const float Seconds = 0.4f;
        private const float FadeFrom = 0.6f;
        private const int Capacity = 32;
        private const int SortingOrder = 170;
        // 층 앞쪽 끝에서 띄움 · 층 가운데에서 올림(px)
        private const int Ahead = 2;
        private const int Above = 3;
        private const string Times = "000101010101000";

        private struct Mark
        {
            public Team Team;
            public int Floor;
            public int Count;
            public float Age;
        }

        private static FloorMultiplier _instance;
        private PixelMesh _mesh;
        private readonly List<Mark> _active = new List<Mark>();

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "FloorMultiplier", material, SortingOrder, Capacity * 60);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // floor층이 이번 발동에 효과를 count번 냈다 — 같은 층에 떠 있으면 새로 띄운다
        public static void Show(Team team, int floor, int count)
        {
            if (_instance == null || count <= 1)
                return;
            List<Mark> active = _instance._active;
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].Team == team && active[i].Floor == floor)
                    active.RemoveAt(i);
            if (active.Count < Capacity)
                active.Add(new Mark { Team = team, Floor = floor, Count = Mathf.Min(count, 9) });
        }

        private void LateUpdate()
        {
            _mesh.Begin();
            float dt = Time.deltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Mark m = _active[i];
                m.Age += dt;
                Tower tower = Tower.For(m.Team);
                if (m.Age >= Seconds || tower == null || m.Floor >= tower.FloorCount)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                _active[i] = m;
                float t = m.Age / Seconds;
                float alpha = t < FadeFrom ? 1f : 1f - (t - FadeFrom) / (1f - FadeFrom);
                Vector2 front = tower.FloorFront(m.Floor);
                int forward = m.Team == Team.Ally ? 1 : -1;
                // 글자 폭 = 3 + 1 + 3 — 적 탑은 앞쪽이 왼쪽이라 오른쪽 끝을 맞춘다
                int x = PixelMesh.ToGrid(front.x) + forward * Ahead - (forward < 0 ? 7 : 0), y = PixelMesh.ToGrid(front.y) + Above;
                Write(x, y, Times, alpha);
                Write(x + 4, y, DamageNumbers.Glyphs[m.Count], alpha);
            }
            _mesh.End();
        }

        // 3×5 글자 하나 — 테두리(검정) 먼저, 그 위에 흰 글자. (x, y) = 왼쪽 아래
        private void Write(int x, int y, string glyph, float alpha)
        {
            Color32 outline = PixelMesh.Faded(Color.black, alpha), fill = PixelMesh.Faded(Color.white, alpha);
            for (int pass = 0; pass < 2; pass++)
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        if (glyph[row * 3 + col] != '1')
                            continue;
                        int px = x + col, py = y + 4 - row;
                        if (pass == 0)
                            _mesh.Rect(px - 1, py - 1, 3, 3, outline);
                        else
                            _mesh.Rect(px, py, 1, 1, fill);
                    }
        }
    }
}
