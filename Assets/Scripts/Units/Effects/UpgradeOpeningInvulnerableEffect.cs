using UnityEngine;

namespace GnorpWar
{
    // 전투 시작 시, N초간 적용 대상 유닛(내 유닛)이 무적 (국가 전용 업그레이드 「성전 선포」 — 킹덤) — N은 업그레이드의 합쳐진 값.
    // 전투 시작부터 N초 동안 소환되는 유닛도 남은 시간만큼 무적이다(산업혁명의 「전투 시작 후 N초 동안」과 같은 읽기). 무적 표시는 유닛의 노란 원
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Opening Invulnerable")]
    public class UpgradeOpeningInvulnerableEffect : UnitEffect
    {
        public override void OnSummoned(Unit unit, int sourceSlot, int stacks)
        {
            float left = Value(unit.Team) - BattleManager.Elapsed;
            if (left > 0f)
                unit.GrantInvulnerable(left);
        }
    }
}
