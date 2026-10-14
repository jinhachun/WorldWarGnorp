using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 죽을 때마다 그 진영의 적용 대상 유닛 전체에 스탯을 더한다 — 전투 동안 (이동식 공장 신화: 톱니병사 공격력 +1)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Death Battle Stat")]
    public class DeathBattleStatEffect : UnitEffect
    {
        [SerializeField] private StatModifier _stat = new StatModifier(UnitStat.Attack, StatOp.Flat, 1f);

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            StatBook.AddForBattle(unit.Team, this, _stat);
            BuffLinks.EffectFromUnitToUnits(unit.Team, this, at, unit.Team, u => this.AppliesFor(u.Team, u.Definition), BuffKind.Stat);
        }

        public override string Describe(int level) => string.Format(Description, _stat.Describe());
    }
}
