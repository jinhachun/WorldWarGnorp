using UnityEngine;

namespace GnorpWar
{
    // 발동할 때마다 전투 동안 내 유닛들의 스탯을 올린다 — 나중에 소환되는 유닛도 받는다 (훈련용 허수아비)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Add Battle Stat")]
    public class AddBattleStatAction : BuildingAction, IUnitFilter, ICategoryFilter
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

        public bool AppliesTo(UnitCategory category) => System.Array.IndexOf(_categories, category) >= 0;

        public override void Execute(BoardRunner board, int slot, int level)
        {
            StatModifier modifier = _modifier.Scaled(_perTier, level);
            StatBook.AddForBattle(board.Team, this, modifier);
            // 훈련용 허수아비 신화 — 이 기물(이 칸의 한 채)이 올린 값이 전투가 끝나도 남는다: 발동을 그 기물의 스택으로 적어 두고,
            // 다음 전투부터는 KeepBattleStatPassive가 전투 시작 때 스택만큼 다시 건다. 기물에 딸린 값이라 팔면 같이 사라진다(사용자 10/13)
            OwnedBuilding self = board.Slots[slot].Building;
            foreach (BuildingPassive passive in self.Passives)
                if (passive is KeepBattleStatPassive)
                {
                    self.AddStacks(1);
                    break;
                }
            board.AddBattleGain(slot, modifier.Op, modifier.Value);
            BuffLinks.ToUnits(board.Team, slot, BuffKind.Stat, this, BuffLabel.Of(modifier));
        }

        // 발동 stacks번이 쌓은 값(지금 등급 기준) — +는 더하고 ×는 거듭 곱한다
        public StatModifier Stacked(int level, int stacks)
        {
            StatModifier modifier = _modifier.Scaled(_perTier, level);
            modifier.Value = modifier.Op == StatOp.Multiply ? Mathf.Pow(modifier.Value, stacks) : modifier.Value * stacks;
            return modifier;
        }

        public override bool BattleGain(out StatOp op)
        {
            op = _modifier.Op;
            return true;
        }

        public override string Describe(int level) => $"{TargetName()}{_modifier.Scaled(_perTier, level).Describe()}";
        public override string Headline(int level) => _modifier.Scaled(_perTier, level).ValueText;

        private string TargetName()
        {
            if (_categories.Length == 0 && _units.Length == 0)
                return "";
            var names = new System.Collections.Generic.List<string>();
            foreach (UnitCategory c in _categories)
                names.Add(c.DisplayName);
            foreach (UnitDefinition u in _units)
                names.Add(u.DisplayName);
            return $"{string.Join("·", names)} ";
        }
    }
}
