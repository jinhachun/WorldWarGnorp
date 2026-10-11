using UnityEngine;

namespace GnorpWar
{
    // 위에 있는 모든 기물이 소환하는 유닛의 스탯을 올린다 (군기) — 소환되는 그 유닛 하나에만 건다. 칸 번호가 클수록 위층
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Neighbor Summon Stat")]
    public class NeighborSummonStatPassive : BuildingPassive
    {
        [SerializeField] private StatModifier[] _modifiers = new StatModifier[0];
        [Tooltip("등급이 오를 때마다 각 _modifiers 값에 더한다")]
        [SerializeField] private float _perTier;

        public override void OnSummonedFrom(BoardRunner board, int slot, int sourceSlot, Unit unit)
        {
            if (sourceSlot <= slot)
                return;
            int level = board.Slots[slot].Building.Level;
            foreach (StatModifier m in _modifiers)
                unit.AddModifier(m.Scaled(_perTier, level));
            // 스탯이 여럿이어도 알림은 하나 — 첫 스탯의 종류로
            if (_modifiers.Length > 0)
                BuffLinks.ToPoint(board.Team, slot, unit.Position, BuffLinks.KindOf(_modifiers[0].Stat));
        }

        public override bool Reaches(int slot, int other, BuildingDefinition target) => other > slot && target.Unit != null;

        public override string Describe(int level)
            => $"위에 있는 모든 기물이 소환하는 유닛의 {string.Join(", ", System.Array.ConvertAll(_modifiers, m => m.Scaled(_perTier, level).Describe()))}";

        public override string Headline(int level) => _modifiers.Length == 0 ? null : _modifiers[0].Scaled(_perTier, level).ValueText;
    }
}
