using UnityEngine;

namespace GnorpWar
{
    // 뼛가루 언덕 위에 있는 적용 대상 유닛은 맞아도 밀려나지 않는다 (전쟁 북소리 신화 — 트라이브 명세)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/No Push On Dust")]
    public class NoPushOnDustEffect : UnitEffect
    {
        public override bool OnHit(Unit unit, IDamageable attacker, Vector2 hitDirection, float push, int stacks) => unit.OnBoneDust;
    }
}
