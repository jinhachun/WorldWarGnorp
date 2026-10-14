using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 죽을 때마다, 확률로 그 자리에서 다시 일어난다 — 받은 강화는 그대로다 (죽지 않는 전사 — 트라이브 명세 §4 · §5).
    // 판정과 일어나기는 Unit.Perish가 한다(사망 시 효과 · 처치 효과가 먼저 돈다 · 소환이 아니다). 일어날 때마다 거는 스탯(신화 — 공격력 +20%)은 값이 0이면 없다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Revive Chance")]
    public class ReviveChanceEffect : UnitEffect
    {
        [Tooltip("0.25 = 25% — 0이면 확률은 안 보태고 아래 스탯만 건다(신화 줄)")]
        [SerializeField] private float _chance = 0.25f;
        [Tooltip("다시 일어날 때마다 그 유닛에 거는 스탯(전투 동안 · 쌓인다) — 값이 0이면 없음")]
        [SerializeField] private StatModifier _onRevive = new StatModifier(UnitStat.Attack, StatOp.Percent, 0f);

        public override float ReviveChance => _chance;

        public override void OnRevived(Unit unit)
        {
            if (_onRevive.Value != 0f)
                unit.AddModifier(_onRevive);
            // 부활은 소환 색(사용자 10/14 기준) — 그 자리에서 일어나므로 켠 기물의 층에서
            BuffLinks.EffectToPoint(unit.Team, this, unit.Position, BuffKind.Summon);
        }

        public override string Describe(int level) => string.Format(Description, $"{_chance * 100f:0.#}", _onRevive.ValueText);
    }
}
