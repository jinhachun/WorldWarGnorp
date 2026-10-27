using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(내 경보병)이 승급할 때 그 자리에 유닛을 소환한다 (반군의 깃발 신화 — 병사 1기, 사용자 10/28).
    // 연계 소환이다 — 낸 유닛은 chained, 연계로 태어난 유닛의 승급(승급은 연계 출생 표시를 이어 간다)은 다시 부르지 않는다(사용자 10/26)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Promotion Summon")]
    public class PromotionSummonEffect : UnitEffect
    {
        [SerializeField] private UnitDefinition _summon;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;

        public override void OnPromoted(Unit promoted, int stacks)
        {
            if (promoted.ChainBorn)
                return;
            Vector2 ground = new Vector2(promoted.Position.x, Ground.Instance.HeightAt(promoted.Position.x));
            BoardRunner.For(promoted.Team).Summon(_summon, _count, -1, ground, Source(promoted.Team), chained: true);
            BuffLinks.EffectToPoint(promoted.Team, this, ground, BuffKind.Summon);
        }

        public override string Describe(int level) => string.Format(Description, _count);
    }
}
