using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛은 맞아도 밀려나지 않는다 (국가 전용 업그레이드 「뿔 투구」의 둘째 줄 — 내 미노타우로스, 트라이브). 언덕 위에서만이면 NoPushOnDustEffect
    [CreateAssetMenu(menuName = "GnorpWar/Effects/No Push")]
    public class NoPushEffect : UnitEffect
    {
        public override bool OnHit(Unit unit, IDamageable attacker, Vector2 hitDirection, float push, int stacks)
        {
            // 밀려야 할 타격을 버틴 순간 — 발밑에서 흰 파편이 아래로(전쟁 북소리와 같은 연출)
            if (push > 0f && FxDirector.Instance != null)
                FxDirector.Instance.HitSpark(new Vector2(unit.Position.x, unit.Feet), Vector2.down, Color.white);
            return true;
        }
    }
}
