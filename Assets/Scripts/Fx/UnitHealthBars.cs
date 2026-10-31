using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 유닛 체력바(손맛 J1 — 사용자 10/31: 맞은 유닛만 잠깐). 맞은 유닛 머리 위에 흰 채움 1px 바가 뜨고, 깎인 구간은 피해 숫자 색(적 노랑 · 아군 빨강)
    // 잔상으로 남았다가 따라 준다. 회복은 초록 잔상이 먼저 차고 채움이 따라 찬다. 마지막으로 바뀐 뒤 일정 시간 지나면 계단식으로 흐려지며 사라진다.
    // 맞는 순간 Unit.TakeDamage가 Hit을 부르고, 그다음은 유닛 체력을 읽기만 한다 — 죽거나 풀로 돌아가면 뺀다.
    // 상태 점(StatusDots, 머리 위 2~3px) 위에 띄운다. 바 전부를 메시 하나로(PixelMesh). FxDirector가 만든다. 세기 값은 BattleConfig 「손맛」
    public class UnitHealthBars : MonoBehaviour
    {
        private const int Capacity = 512;
        // 머리 위 띄움(px) — 채움 줄 높이. 검은 테두리가 한 줄 아래 · 위로 붙는다(상태 점 2~3px · 호버 꺾쇠 위)
        private const int Lift = 6;
        private const int MinWidth = 6;
        private const int FadeSteps = 3;
        private const int SortingOrder = 162;   // 상태 점(160) 위 · 호버 꺾쇠(170) · 숫자(200) 아래
        // 빈 칸 — 메시 정점 색은 선형으로 읽혀 화면에서 더 밝게 나온다(HealFx와 같은 이유) → .linear
        private static readonly Color Empty = new Color(0.22f, 0.22f, 0.22f).linear;

        private struct Bar
        {
            public Unit Unit;
            public int Life;
            public int Width;
            public float Value;
            public float Fill;
            public float Ghost;
            public bool Healing;
            public float LagFrom;
            public float LagAge;
            public float LastChange;
        }

        private static UnitHealthBars _instance;
        private PixelMesh _mesh;
        private BattleConfig _config;
        private readonly List<Bar> _bars = new List<Bar>();
        private readonly HashSet<Unit> _watching = new HashSet<Unit>();

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "UnitHealthBars", material, SortingOrder, Capacity * 4);
        }

        private void Start() => _config = BattleManager.Config;

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // 맞았다 — hpBefore = 맞기 전 체력. 이미 떠 있으면 그 바가 다음 프레임에 변화를 읽는다
        public static void Hit(Unit unit, float hpBefore)
        {
            if (_instance == null || _instance._bars.Count >= Capacity || !_instance._watching.Add(unit))
                return;
            float before = Mathf.Clamp01(hpBefore / unit.MaxHp);
            var box = unit.GetComponent<BoxCollider2D>();
            int width = box != null ? Mathf.Max(MinWidth, Mathf.RoundToInt(box.bounds.size.x / PixelMesh.Pixel)) : 8;
            _instance._bars.Add(new Bar { Unit = unit, Life = unit.Life, Width = width, Value = before, Fill = before, Ghost = before, LastChange = Time.time });
        }

        private void LateUpdate()
        {
            _mesh.Begin();
            float now = Time.time, dt = Time.deltaTime;
            float show = _config.UnitBarSeconds, fade = _config.UnitBarFadeSeconds;
            Color32 black = Color.black, white = Color.white;
            for (int i = _bars.Count - 1; i >= 0; i--)
            {
                Bar b = _bars[i];
                Unit unit = b.Unit;
                if (unit == null || !unit.isActiveAndEnabled || unit.Life != b.Life || !unit.IsAlive)
                {
                    _watching.Remove(unit);
                    _bars.RemoveAt(i);
                    continue;
                }

                float hp01 = Mathf.Clamp01(unit.Hp / unit.MaxHp);
                if (hp01 < b.Value)
                {
                    b.Ghost = b.Healing ? b.Fill : Mathf.Max(b.Ghost, b.Value);
                    b.Healing = false;
                    b.LagFrom = b.Ghost;
                    b.LagAge = 0f;
                    b.LastChange = now;
                }
                else if (hp01 > b.Value)
                {
                    b.Healing = true;
                    b.LagFrom = b.Fill;
                    b.LagAge = 0f;
                    b.LastChange = now;
                }
                b.Value = hp01;
                // 이번 프레임에 또 맞았으면 위에서 시간이 다시 세어졌다 — 그다음에 끝났는지 본다
                if (now - b.LastChange >= show)
                {
                    _watching.Remove(unit);
                    _bars.RemoveAt(i);
                    continue;
                }
                b.LagAge += dt;
                float lag = Mathf.Lerp(b.LagFrom, b.Value, Juice.OutQuad((b.LagAge - _config.HpGhostDelay) / _config.HpGhostDrop));
                b.Fill = b.Healing ? lag : b.Value;
                b.Ghost = b.Healing ? b.Value : lag;
                _bars[i] = b;

                // 끝 몫에서 계단식으로 흐려진다
                float left = show - (now - b.LastChange);
                float alpha = left >= fade ? 1f : Mathf.Ceil(left / fade * FadeSteps) / FadeSteps;
                Color chip = b.Healing ? DamageNumbers.Healed : unit.Team == Team.Ally ? DamageNumbers.AllyHurt : DamageNumbers.EnemyHurt;

                int w = b.Width, x = PixelMesh.ToGrid(unit.Position.x) - w / 2, y = PixelMesh.ToGrid(unit.Top) + Lift;
                _mesh.Rect(x - 1, y - 1, w + 2, 3, PixelMesh.Faded(black, alpha));
                _mesh.Rect(x, y, w, 1, PixelMesh.Faded(Empty, alpha));
                _mesh.Rect(x, y, Mathf.CeilToInt(b.Ghost * w), 1, PixelMesh.Faded(chip, alpha));
                _mesh.Rect(x, y, Mathf.Max(1, Mathf.CeilToInt(b.Fill * w)), 1, PixelMesh.Faded(white, alpha));
            }
            _mesh.End();
        }
    }
}
