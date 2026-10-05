using UnityEngine;

namespace GnorpWar
{
    // 상점을 리롤할 때마다 스택 +1, 전투 시작 시 스택마다 대상 유닛의 스탯에 곱한다 (노동운동가 스패너).
    // 스택은 이 기물(OwnedBuilding)에 쌓인다 — 팔면 사라지고 등급이 오르면 남는다(사용자 결정)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Reroll Stack")]
    public class RerollStackPassive : BuildingPassive, IUnitFilter
    {
        [Tooltip("이 유닛에게 — 비우면 전부")]
        [SerializeField] private UnitDefinition[] _units = new UnitDefinition[0];
        [SerializeField] private UnitStat[] _stats = new UnitStat[0];
        [Tooltip("스택 하나마다 곱하는 값")]
        [SerializeField] private float _perStack = 1.05f;

        public bool AppliesTo(UnitDefinition unit) => _units.Length == 0 || System.Array.IndexOf(_units, unit) >= 0;

        public override void OnReroll(OwnedBuilding self) => self.AddStacks(1);

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            int stacks = board.Slots[slot].Building.Stacks;
            if (stacks <= 0)
                return;
            float factor = Mathf.Pow(_perStack, stacks);
            foreach (UnitStat stat in _stats)
                StatBook.AddForBattle(board.Team, this, new StatModifier(stat, StatOp.Multiply, factor));
        }

        public override string Describe(int level)
        {
            string target = _units.Length == 0 ? "내 유닛들" : "내 " + string.Join("·", System.Array.ConvertAll(_units, u => u.DisplayName));
            string stats = string.Join(", ", System.Array.ConvertAll(_stats, s => new StatModifier(s, StatOp.Multiply, _perStack).Name));
            return $"상점을 리롤할 때마다 스택을 1 얻습니다.\n스택마다 {target}의 {stats} ×{_perStack:0.##}";
        }
    }
}
