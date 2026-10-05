using UnityEngine;

namespace GnorpWar
{
    // 인접 기물들이 소환하는 유닛의 스탯을 올린다 (군기) — 소환되는 그 유닛 하나에만 건다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Neighbor Summon Stat")]
    public class NeighborSummonStatPassive : BuildingPassive
    {
        [SerializeField] private StatModifier[] _modifiers = new StatModifier[0];

        public override void OnNeighborSummoned(BoardRunner board, int slot, Unit unit)
        {
            foreach (StatModifier m in _modifiers)
                unit.AddModifier(m);
        }

        public override string Describe(int level)
            => $"인접 기물들이 소환하는 유닛의 {string.Join(", ", System.Array.ConvertAll(_modifiers, m => m.Describe()))}";
    }
}
