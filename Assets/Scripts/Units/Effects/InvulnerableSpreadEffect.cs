using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 무적을 받을 때 주변 아군도 같은 무적을 함께 받는다 (공주의 축복 신화 — 기획서 §7-1).
    // 퍼져서 걸린 무적은 다시 퍼지지 않는다(Unit.GrantInvulnerable)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Invulnerable Spread")]
    public class InvulnerableSpreadEffect : UnitEffect
    {
        [SerializeField] private float _radius = 3f;

        public override float InvulnerableSpreadRadius => _radius;
    }
}
