using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(내 톱니거인)이 소환될 때 크기 +N%(기준 크기에 더한다 — 순례지 · 크루세이더 길드와 같은 방식, 상한은 Unit.Grow) (국가 전용 업그레이드 「거인 설계도」의 둘째 줄 — 티크톡).
    // N은 업그레이드의 합쳐진 값(체력 +N%와 같은 값 — 체력은 업그레이드의 UnitStat이 건다). 크기가 커지면 고도도 오른다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Grow On Summon")]
    public class UpgradeGrowOnSummonEffect : UnitEffect
    {
        public override void OnSummoned(Unit unit, int sourceSlot, int stacks)
        {
            float grow = Value(unit.Team) / 100f;
            if (grow > 0f)
                unit.Grow(grow);
        }
    }
}
