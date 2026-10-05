using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면 전투 동안 A가 B에게 적용되는 효과를 함께 받는다 (조립 라인: 톱니거인 → 톱니병사)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Unit Alias")]
    public class UnitAliasPassive : BuildingPassive
    {
        [SerializeField] private UnitDefinition _unit;
        [SerializeField] private UnitDefinition _alsoAs;

        public override void OnBattleStart(BoardRunner board, int slot, int level) => UnitAliases.Add(board.Team, _unit, _alsoAs);

        public override string Describe(int level) => $"{_unit.DisplayName}은 {_alsoAs.DisplayName}에게 적용되는 효과를 함께 받습니다.";
    }
}
