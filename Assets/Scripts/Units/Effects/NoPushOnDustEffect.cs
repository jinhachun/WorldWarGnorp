using UnityEngine;

namespace GnorpWar
{
    // 뼛가루 언덕 위에 있는 적용 대상 유닛은 맞아도 밀려나지 않는다 (전쟁 북소리 신화 — 트라이브 명세)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/No Push On Dust")]
    public class NoPushOnDustEffect : UnitEffect
    {
        public override bool OnHit(Unit unit, IDamageable attacker, Vector2 hitDirection, float push, int stacks)
        {
            if (!unit.OnBoneDust)
                return false;
            // 밀려야 할 타격을 버틴 순간 — 발밑에서 흰 파편이 아래로(버틴 게 보이게)
            if (push > 0f && FxDirector.Instance != null)
                FxDirector.Instance.HitSpark(new Vector2(unit.Position.x, unit.Feet), Vector2.down, Color.white);
            return true;
        }
    }
}
