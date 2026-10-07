using UnityEngine;

namespace GnorpWar
{
    // 발동할 때마다 전투 동안 내 유닛들의 스탯을 올린다 — 나중에 소환되는 유닛도 받는다 (훈련용 허수아비)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Add Battle Stat")]
    public class AddBattleStatAction : BuildingAction, IUnitFilter
    {
        [Tooltip("이 병종 태그의 유닛에게 — 유닛 칸과 함께 비우면 전부")]
        [SerializeField] private UnitCategory[] _categories = new UnitCategory[0];
        [SerializeField] private UnitDefinition[] _units = new UnitDefinition[0];
        [SerializeField] private StatModifier _modifier = new StatModifier(UnitStat.Attack, StatOp.Percent, 0.05f);
        [Tooltip("등급이 오를 때마다 _modifier 값에 더한다")]
        [SerializeField] private float _perTier = 0.05f;

        public bool AppliesTo(UnitDefinition unit)
            => (_categories.Length == 0 && _units.Length == 0)
               || System.Array.IndexOf(_categories, unit.Category) >= 0
               || System.Array.IndexOf(_units, unit) >= 0;

        public override void Execute(BoardRunner board, int slot, int level) => StatBook.AddForBattle(board.Team, this, _modifier.Scaled(_perTier, level));

        public override string Describe(int level) => $"전투 동안 {TargetName()} {_modifier.Scaled(_perTier, level).Describe()}";
        public override string Headline(int level) => _modifier.Scaled(_perTier, level).ValueText;

        private string TargetName()
        {
            if (_categories.Length == 0 && _units.Length == 0)
                return "내 유닛들의";
            var names = new System.Collections.Generic.List<string>();
            foreach (UnitCategory c in _categories)
                names.Add(c.DisplayName);
            foreach (UnitDefinition u in _units)
                names.Add(u.DisplayName);
            return $"내 {string.Join("·", names)}의";
        }
    }
}
