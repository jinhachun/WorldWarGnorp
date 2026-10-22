using UnityEngine;

namespace GnorpWar
{
    // 기물이 직접 준 피해의 출처(뼈 투척 — 유닛도 타워도 아니다) — 딜 미터기(Battle/DamageMeter)가 그 칸 몫으로 센다.
    // 공격 대상이 아니다: 피해(반사 등)를 받지 않고, 처치 효과(OnKill · 타워 처치)도 없다
    public sealed class SlotAttacker : IDamageable
    {
        public SlotAttacker(Team team, int slot)
        {
            Team = team;
            Slot = slot;
        }

        public Team Team { get; }
        public int Slot { get; }
        public bool IsAlive => true;

        public void TakeDamage(float amount, Vector2 hitDirection, float push, IDamageable attacker) { }
    }
}
