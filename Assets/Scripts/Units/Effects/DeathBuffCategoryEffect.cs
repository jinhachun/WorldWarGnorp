using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(내 경보병)이 죽을 때마다, 전투 동안 그 병종의 내 유닛에게 스탯을 건다 (바위의 토템: 무작위 내 중보병의 최대 체력 +5% — 트라이브 명세 §4 · §5).
    // 신화는 무작위 하나 대신 전부에게 건다 — 신화 줄의 에셋(_all)이 켜져 있으면 본 효과 에셋은 그것에 자리를 내준다(_replacedBy)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Death Buff Category")]
    public class DeathBuffCategoryEffect : UnitEffect
    {
        [Tooltip("받는 병종 — 내 유닛 중 이 병종(다른 국가의 유닛도)")]
        [SerializeField] private UnitCategory _target;
        [SerializeField] private StatModifier _stat = new StatModifier(UnitStat.MaxHp, StatOp.Percent, 0.05f);
        [Tooltip("등급이 오를 때마다 _stat 값에 더한다")]
        [SerializeField] private float _perTier;
        [Tooltip("켜면 무작위 하나가 아니라 그 병종의 내 유닛 전부에게")]
        [SerializeField] private bool _all;
        [Tooltip("이 효과가 그 진영에 켜져 있으면 이 효과는 쉰다(신화 줄이 본 효과를 대신한다)")]
        [SerializeField] private UnitEffect _replacedBy;

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            Team team = unit.Team;
            if (_replacedBy != null)
                foreach (TeamEffects.Entry e in TeamEffects.For(team))
                    if (e.Effect == _replacedBy)
                        return;
            var targets = new System.Collections.Generic.List<Unit>();
            foreach (Unit other in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (other.IsAlive && other.Team == team && UnitAliases.Is(team, other.Definition, _target))
                    targets.Add(other);
            if (targets.Count == 0)
                return;
            StatModifier stat = _stat.Scaled(_perTier, Level(team));
            if (_all)
            {
                foreach (Unit target in targets)
                    target.AddModifier(stat);
                BuffLinks.EffectFromUnitToUnits(team, this, at, team, u => targets.Contains(u), BuffKind.Stat);
            }
            else
            {
                Unit target = targets[Random.Range(0, targets.Count)];
                target.AddModifier(stat);
                BuffLinks.EffectFromUnit(team, this, at, target.Position, BuffKind.Stat);
            }
        }

        public override string Describe(int level) => string.Format(Description, _stat.Scaled(_perTier, level).ValueText);
        public override string Headline(int level) => _stat.Scaled(_perTier, level).ValueText;
    }
}
