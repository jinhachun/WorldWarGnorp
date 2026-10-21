using UnityEngine;

namespace GnorpWar
{
    // 이 진영의 유닛이 죽으며 남기는 뼛가루의 양에 곱한다 (뼈무덤 신화: 원시 졸병이 남기는 뼛가루가 2배 — 트라이브 명세). 읽는 곳은 BoneDustOnDeathEffect
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Bone Dust Scale")]
    public class BoneDustScaleEffect : UnitEffect
    {
        [SerializeField] private float _factor = 2f;

        public override float BoneDustFactor(Team team) => _factor;
    }
}
