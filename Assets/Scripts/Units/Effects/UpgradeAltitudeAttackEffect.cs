using UnityEngine;

namespace GnorpWar
{
    // 고도 1마다, 적용 대상 유닛(내 유닛)의 공격력 +N%(합연산) — 지금 고도로 실시간 (국가 전용 업그레이드 「봉우리의 함성」 — 트라이브) — N은 업그레이드의 합쳐진 값.
    // 읽는 곳은 미노타우로스의 고도 효과와 같다(Unit.Compute — AltitudeBonus). 고도 0이면 0
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Altitude Attack")]
    public class UpgradeAltitudeAttackEffect : UnitEffect
    {
        public override float AltitudeBonus(Team team, UnitStat stat) => stat == UnitStat.Attack ? Value(team) / 100f : 0f;
    }
}
