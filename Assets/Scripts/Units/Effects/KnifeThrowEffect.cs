using UnityEngine;

namespace GnorpWar
{
    // 공격과 별개로, 앞쪽 수평 띠(내 높이)에 적이 있으면 가끔 칼을 곧게 던진다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Knife Throw")]
    public class KnifeThrowEffect : UnitEffect
    {
        // 띠가 비었을 때 다시 볼 때까지 — 매 스텝 검사하지 않게
        private const float EmptyLaneRecheck = 0.1f;
        private const float MinInterval = 0.1f;

        [Tooltip("던지는 간격(초)")]
        [SerializeField] private float _interval = 3f;
        [Tooltip("스택(건물 발동) 하나당 간격 감소율 (0.05 = -5%) — 원래 간격 기준으로 더한다")]
        [SerializeField] private float _intervalReductionPerStack = 0.05f;
        [Tooltip("칼이 닿는 거리 — 이 안의 띠에 적이 있어야 던진다")]
        [SerializeField] private float _range = 8f;
        [SerializeField] private float _laneHeight = 0.8f;
        [SerializeField] private float _speed = 14f;
        [SerializeField] private Projectile _knife;

        public override void Tick(Unit unit, int stacks)
        {
            if (!unit.EffectReady(this))
                return;

            if (!unit.HasEnemyInLane(_range, _laneHeight))
            {
                unit.SetEffectCooldown(this, EmptyLaneRecheck);
                return;
            }
            unit.ThrowStraight(_knife, _speed);
            float interval = _interval * (1f - _intervalReductionPerStack * stacks);
            unit.SetEffectCooldown(this, Mathf.Max(MinInterval, interval));
        }
    }
}
