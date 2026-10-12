using UnityEngine;

namespace GnorpWar
{
    // 이 효과를 켠 기물이 소환한 유닛이 적을 처치할 때만 소환한다 (하마 교련장 신화 — 기획서 §7-1 「이 기물이 소환한 병사가 ~」).
    // 다른 기물이 낳은 같은 유닛은 대상이 아니다 — 출처는 Unit.SummonedBy
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Own Kill Summon")]
    public class OwnKillSummonEffect : KillSummonEffect
    {
        public override void OnKill(Unit killer, Unit victim, int stacks)
        {
            if (killer.SummonedBy != null && killer.SummonedBy == Source(killer.Team))
                base.OnKill(killer, victim, stacks);
        }
    }
}
