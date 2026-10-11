using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 죽으면 그 자리에 유닛을 소환한다 (하마 교련장: 기사 → 병사)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Death Summon")]
    public class DeathSummonEffect : UnitEffect
    {
        [SerializeField] private UnitDefinition _summon;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _countPerTier = 1;

        private int Count(int level) => OwnedBuilding.Scaled(_count, _countPerTier, level);

        public override void OnDied(Unit unit, Vector2 at, int stacks)
            => BoardRunner.For(unit.Team).Summon(_summon, Count(Level(unit.Team)), -1, new Vector2(at.x, Ground.Instance.HeightAt(at.x)));

        public override string Describe(int level) => string.Format(Description, Count(level));
        public override string Headline(int level) => $"{Count(level)}기";
    }
}
