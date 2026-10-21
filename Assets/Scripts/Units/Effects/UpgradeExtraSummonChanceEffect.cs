using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(톱니병사)을 소환할 때마다, N% 확률로 1기를 추가로 소환한다 (국가 전용 업그레이드 「초과 생산」 — 티크톡) — N은 업그레이드의 합쳐진 값(100까지).
    // 소환 처리 ① 추가 소환 자리라 변환(노동의 기계화)으로 바뀌어 나온 톱니병사에는 굴리지 않는다(총괄 10/21). 더 나온 기에는 기둥(BoardRunner.Summon)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Extra Summon Chance")]
    public class UpgradeExtraSummonChanceEffect : UnitEffect
    {
        public override int ExtraSummons(Team team, UnitDefinition unit, int count, int sourceSlot, int stacks, int level)
            => count > 0 && Random.value * 100f < Value(team) ? 1 : 0;
    }
}
