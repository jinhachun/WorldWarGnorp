using UnityEngine;

namespace GnorpWar
{
    // 글만 있는 유닛 효과 — 훅이 없다. 다른 효과가 이 표시가 그 진영에 켜져 있는지 보고 동작을 바꾼다
    // (미궁의 입구 신화: DeathCountSummonEffect가 이 표시가 있으면 원시 졸병도 함께 소환한다)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Mark")]
    public class MarkEffect : UnitEffect
    {
    }
}
