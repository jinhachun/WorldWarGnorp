using UnityEngine;

namespace GnorpWar
{
    // 이 효과를 켠 기물이 소환한 유닛이 죽을 때만 소환한다 (조립 라인 신화 — 기획서 §7-1 「소환한 톱니거인이 죽으면 ~」).
    // 다른 기물이 낳은 같은 유닛은 대상이 아니다 — 출처는 Unit.SummonedBy
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Own Death Summon")]
    public class OwnDeathSummonEffect : DeathSummonEffect
    {
        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            if (unit.SummonedBy != null && unit.SummonedBy == Source(unit.Team))
                base.OnDied(unit, at, stacks);
        }
    }
}
