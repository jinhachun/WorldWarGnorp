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
            if (!board.Linked(slot, sourceSlot, sourceSlot > slot))
                return;
            int level = board.Slots[slot].Building.Level;
            foreach (StatModifier m in _modifiers)
                unit.AddModifier(m.Scaled(_perTier, level));
            // 스탯이 여럿이어도 알림은 하나 — 첫 스탯의 종류로
            if (_modifiers.Length > 0)
                BuffLinks.ToPoint(board.Team, slot, unit.Position, BuffKind.Stat, BuffLabel.Of(_modifiers[0].Scaled(_perTier, level)));
        }

        public override bool Reaches(int slot, int other, BuildingDefinition target) => other > slot && target.Unit != null;

        public override bool UsesAdjacency => true;
        public override string Describe(int level)
            => $"위에 있는 모든 기물의 유닛 {Stats(level)}";

        // 값이 모두 같으면 낱말만 잇는다 — 「체력·공격력 +90%」. 다르면 「체력 +30%, 공격력 +20%」
        private string Stats(int level)
        {
            StatModifier[] scaled = System.Array.ConvertAll(_modifiers, m => m.Scaled(_perTier, level));
            bool same = System.Array.TrueForAll(scaled, m => m.ValueText == scaled[0].ValueText);
            return same
                ? $"{string.Join("·", System.Array.ConvertAll(scaled, m => m.Name))} {scaled[0].ValueText}"
                : string.Join(", ", System.Array.ConvertAll(scaled, m => m.Describe()));
        }

        public override string Headline(int level) => _modifiers.Length == 0 ? null : _modifiers[0].Scaled(_perTier, level).ValueText;
    }
}
