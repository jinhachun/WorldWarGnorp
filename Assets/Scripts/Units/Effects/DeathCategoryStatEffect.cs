using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(내 경보병)이 죽을 때마다, 전투 동안 다른 병종의 내 유닛 전부(나중에 소환되는 유닛도)의 스탯을 올린다
    // (거대 괴수의 무덤: 내 경보병이 죽을 때마다 전투 동안 내 중기병의 공격력 +5% — 트라이브 명세 §4 · §5). 진영 스탯(StatBook)에 건다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Death Category Stat")]
    public class DeathCategoryStatEffect : UnitEffect
    {
        // 받는 쪽 — 이 병종의 유닛(「~로도 취급」 포함)
        [System.Serializable]
        private class Recipients : IUnitFilter, ICategoryFilter
        {
            public UnitCategory[] Categories = new UnitCategory[0];

            public bool AppliesTo(UnitDefinition unit) => System.Array.IndexOf(Categories, unit.Category) >= 0;
            public bool AppliesTo(UnitCategory category) => System.Array.IndexOf(Categories, category) >= 0;
        }

        [Tooltip("받는 병종")]
        [SerializeField] private Recipients _recipients = new Recipients();
        [SerializeField] private StatModifier _stat = new StatModifier(UnitStat.Attack, StatOp.Percent, 0.05f);

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            StatBook.AddForBattle(unit.Team, _recipients, _stat);
            BuffLinks.EffectFromUnitToUnits(unit.Team, this, at, unit.Team, u => _recipients.AppliesFor(u.Team, u.Definition), BuffKind.Stat, BuffLabel.Of(_stat));
        }

        public override string Describe(int level) => string.Format(Description, _stat.ValueText);
    }
}
