using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 받은 피해의 N%를 공격한 적에게 돌려준다 (합치기 보상 업그레이드 「가시 갑옷」) — N은 업그레이드의 합쳐진 값
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Reflect")]
    public class UpgradeReflectEffect : UnitEffect
    {
        public override float DamageReflectRatio(Team team) => Value(team) / 100f;
    }
}
