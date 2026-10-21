using UnityEngine;

namespace GnorpWar
{
    // 내 유닛이 남기는 뼛가루의 양 +N% (국가 전용 업그레이드 「뼈 쌓기」 — 트라이브) — N은 업그레이드의 합쳐진 값. 뼈무덤 신화(×2)와 곱한다(BoneDustOnDeathEffect가 진영의 배율을 모두 곱한다)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Bone Dust")]
    public class UpgradeBoneDustEffect : UnitEffect
    {
        public override float BoneDustFactor(Team team) => 1f + Value(team) / 100f;
    }
}
