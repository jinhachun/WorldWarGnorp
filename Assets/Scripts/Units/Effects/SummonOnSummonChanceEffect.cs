using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛을 소환할 때마다(한 마리마다), 확률로 다른 유닛을 그 자리에 추가로 소환한다
    // (거대 괴수의 무덤: 내가 경보병을 소환할 때마다 25% 확률로 툰드라 전쟁소 1기 — 트라이브 명세 §4 · §5). 다른 국가의 경보병 · 조건 소환(툰드라 행진)에도 굴린다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Summon On Summon Chance")]
    public class SummonOnSummonChanceEffect : UnitEffect
    {
        [SerializeField] private UnitDefinition _summon;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;
        [SuffixLabel("%", true)]
        [SerializeField] private float _chance = 25f;

        public override void OnSummoned(Unit unit, int sourceSlot, int stacks)
        {
            if (Random.value * 100f < _chance)
                BoardRunner.For(unit.Team).Summon(_summon, _count, -1, new Vector2(unit.Position.x, Ground.Instance.HeightAt(unit.Position.x)), Source(unit.Team));
        }

        public override string Describe(int level) => string.Format(Description, _chance.ToString("0.##"), _count);
    }
}
