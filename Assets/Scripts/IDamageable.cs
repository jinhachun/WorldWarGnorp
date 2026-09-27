using UnityEngine;

namespace GnorpWar
{
    // 유닛·기지 공통 — 공격 대상이 될 수 있는 것
    public interface IDamageable
    {
        Team Team { get; }
        bool IsAlive { get; }
        // hitDirection: 공격이 날아가는 방향(공격자 → 피격자), 정규화됨
        void TakeDamage(float amount, Vector2 hitDirection);
    }
}
