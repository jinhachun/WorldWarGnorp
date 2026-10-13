using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 적을 처치할 때마다, 처치한 그 자리에 뼛가루를 남긴다 (대족장의 분노 신화: 내 미노타우로스가 적을 처치할 때마다 원시 졸병 1기분 — 트라이브 명세).
    // 양은 원시 졸병의 뼛가루 효과 에셋에서 읽는다(1기분 — 죽음이 아니라 배율은 안 곱한다)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Bone Dust On Kill")]
    public class BoneDustOnKillEffect : UnitEffect
    {
        [Tooltip("1기분의 양을 읽을 뼛가루 효과(원시 졸병의 것)")]
        [SerializeField] private BoneDustOnDeathEffect _dust;

        public override void OnKill(Unit killer, Unit victim, int stacks) => _dust.Pour(victim.Position.x);
    }
}
