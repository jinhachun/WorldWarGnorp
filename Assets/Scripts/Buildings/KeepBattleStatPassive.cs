using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 발동으로 올린 스탯이 전투가 끝나도 사라지지 않는다 — 이 기물을 팔면 사라진다 (훈련용 허수아비 신화 — 기획서 §7-1).
    // §7 능력(AddBattleStatAction)이 자기 기물에서 이것을 찾으면 발동마다 그 기물의 스택을 1 올리고, 여기서 전투 시작 때 스택만큼 다시 건다.
    // 기물에 딸린 값이다(스패너와 같은 길) — 팔면 같이 사라지고 · 합치면 더해지고 · 보관함에 있는 동안은 걸리지 않는다. 「영구히」(팔아도 유지)가 아니다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Keep Battle Stat")]
    public class KeepBattleStatPassive : BuildingPassive, IUnitFilter
    {
        [Tooltip("이 기물의 발동 능력 — 대상 유닛과 발동 한 번의 값을 거기서 읽는다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private AddBattleStatAction _action;

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        public bool AppliesTo(UnitDefinition unit) => _action.AppliesTo(unit);

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            OwnedBuilding self = board.Slots[slot].Building;
            StatModifier stacked = _action.Stacked(self.Level, self.Stacks);
            if (BattleStartStat(self, stacked.Stat, out StatModifier modifier))
                StatBook.AddForBattle(board.Team, this, modifier);
        }

        // 유닛 카드 미리 보기(RunStats.Preview)도 이 값을 읽는다
        public override bool BattleStartStat(OwnedBuilding self, UnitStat stat, out StatModifier modifier)
        {
            modifier = _action.Stacked(self.Level, self.Stacks);
            return self.Stacks > 0 && modifier.Stat == stat;
        }

        public override string Describe(int level) => "이 기물이 올린 공격력이 전투가 끝나도 사라지지 않습니다. (이 기물을 팔면 사라집니다)";
    }
}
