using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 죽으면 그 자리에 유닛을 소환한다 (하마 교련장: 기사 → 병사)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Death Summon")]
    public class DeathSummonEffect : UnitEffect
    {
        [SerializeField] private UnitDefinition _summon;
        [SerializeField] private int _count = 1;

        public override void OnDied(Unit unit, Vector2 at, int stacks)
            => BoardRunner.For(unit.Team).Summon(_summon, _count, -1, new Vector2(at.x, Ground.Instance.HeightAt(at.x)));
    }
}
