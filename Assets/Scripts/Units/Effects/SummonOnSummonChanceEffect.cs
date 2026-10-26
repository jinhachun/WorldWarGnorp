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
        [Tooltip("켜면 확률을 켠 합치기 보상 업그레이드의 값에서 읽는다(짐승 몰이 — 위 확률 칸은 안 쓴다)")]
        [SerializeField] private bool _chanceFromUpgrade;

        // 이 효과가 부른 소환으로 나온 유닛은 다시 굴리지 않는다 — 짐승 몰이(경보병 → 원시 졸병, 졸병도 경보병)가 100%까지 쌓이면 끝없이 소환하다 스택이 넘쳤다(10/25)
        private static bool _rolling;

        public override void OnSummoned(Unit unit, int sourceSlot, int stacks)
        {
            if (_rolling)
                return;
            if (Random.value * 100f < (_chanceFromUpgrade ? Value(unit.Team) : _chance))
            {
                Vector2 ground = new Vector2(unit.Position.x, Ground.Instance.HeightAt(unit.Position.x));
                _rolling = true;
                try
                {
                    BoardRunner.For(unit.Team).Summon(_summon, _count, -1, ground, Source(unit.Team), chained: true);   // 낸 유닛은 연계로 태어남 — 죽음 · 처치 연계도 안 부른다
                }
                finally
                {
                    _rolling = false;
                }
                // 소환된 유닛 옆에 나온다 — 그 유닛에서(겹치면 켠 기물의 층에서)
                BuffLinks.EffectFromUnit(unit.Team, this, unit.Position, ground, BuffKind.Summon);
            }
        }

        public override string Describe(int level) => string.Format(Description, _chance.ToString("0.##"), _count);
    }
}
