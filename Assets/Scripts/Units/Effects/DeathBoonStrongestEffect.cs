using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 죽을 때, 공격력이 가장 높은 내 유닛(죽는 유닛 제외)에게 무언가를 준다 — 켜 둔 것만(트라이브 명세 §3):
    //   원시의 전사: 최대 체력의 N% 회복 / 툰드라 전쟁소: 전투 동안 죽은 유닛의 (죽기 직전) 공격력만큼 공격력(고정값)
    //   신화 — 전사의 움막: 전투 동안 공격속도 +N% / 약탈 전초기지: 전투 동안 죽은 유닛의 최대 체력의 N%만큼 체력(고정값)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Death Boon Strongest")]
    public class DeathBoonStrongestEffect : UnitEffect
    {
        [Tooltip("받는 유닛의 최대 체력 대비 회복 — 0.05 = 5%. 0이면 없음")]
        [SerializeField] private float _healRatio;
        [Tooltip("죽은 유닛의 지금 공격력 × 이 값을 공격력(고정)으로 — 1 = 그 공격력만큼. 0이면 없음")]
        [SerializeField] private float _attackShare;
        [Tooltip("죽은 유닛의 최대 체력 × 이 값을 체력(고정)으로 — 0.2 = 20%. 0이면 없음")]
        [SerializeField] private float _maxHpShare;
        [Tooltip("그대로 거는 스탯(공격속도 +2% 등) — 값이 0이면 없음")]
        [SerializeField] private StatModifier _modifier = new StatModifier(UnitStat.AttackSpeed, StatOp.Percent, 0f);

        // {0} = 회복 비율(%)
        protected override object[] TraitNumbers() => new object[] { $"{_healRatio * 100f:0.#}" };

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            Unit strongest = Strongest.Of(unit.Team, 0, unit);
            if (strongest == null)
                return;
            if (_healRatio > 0f)
                strongest.Heal(strongest.MaxHp * _healRatio);
            // 버프 글자는 처음 건 스탯 하나(회복뿐이면 글자 없음)
            BuffLabel label = default;
            if (_attackShare > 0f)
            {
                var attack = new StatModifier(UnitStat.Attack, StatOp.Flat, unit.AttackDamage * _attackShare);
                strongest.AddModifier(attack);
                label = BuffLabel.Of(attack);
            }
            if (_maxHpShare > 0f)
            {
                var hp = new StatModifier(UnitStat.MaxHp, StatOp.Flat, unit.MaxHp * _maxHpShare);
                strongest.AddModifier(hp);
                if (label.Text == null)
                    label = BuffLabel.Of(hp);
            }
            if (_modifier.Value != 0f)
            {
                strongest.AddModifier(_modifier);
                if (label.Text == null)
                    label = BuffLabel.Of(_modifier);
            }
            // 회복도 스탯 색(사용자 10/14 기준) — 죽은 자리에서 받은 유닛으로
            BuffLinks.EffectFromUnit(unit.Team, this, at, strongest.Position, BuffKind.Stat, label);
        }
    }
}
