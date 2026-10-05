using UnityEngine;

namespace GnorpWar
{
    // 유닛·기지 공통 — 공격 대상이 될 수 있는 것
    public interface IDamageable
    {
        Team Team { get; }
        bool IsAlive { get; }
        // hitDirection: 공격이 날아가는 방향(공격자 → 피격자), 정규화됨
        // push: 공격자의 밀치는 힘 배율 — 피격자의 밀림 값에 곱한다
        // attacker: 피해를 준 쪽(유닛 — 근접·투사체·화염 모두, 또는 본진). 모르면 null — 처치자 판정(OnKill)에 쓴다
        void TakeDamage(float amount, Vector2 hitDirection, float push, IDamageable attacker);
    }
}
