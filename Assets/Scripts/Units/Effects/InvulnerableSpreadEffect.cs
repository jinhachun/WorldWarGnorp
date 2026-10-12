using UnityEngine;

namespace GnorpWar
{
    // 이 효과를 가진 기물이 부여한 무적을 받은 아군 주변의 아군도 같은 무적을 함께 받는다 (공주의 축복 신화 — 기획서 §7-1).
    // 진영에 켜지는 효과로 돌지 않는다 — 무적을 주는 능력(InvulnerableLowestAction)이 자기 기물에서 이 반경을 읽어 Unit.GrantInvulnerable에 넘긴다.
    // 그래서 다른 무적(루니카 · 충정의 맹세 · 친위기사 소환)은 퍼지지 않는다. 퍼져서 걸린 무적은 다시 퍼지지 않는다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Invulnerable Spread")]
    public class InvulnerableSpreadEffect : UnitEffect
    {
        [SerializeField] private float _radius = 3f;

        public override float InvulnerableSpreadRadius => _radius;
    }
}
