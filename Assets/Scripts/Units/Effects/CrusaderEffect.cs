using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 병종(중보병)이 소환될 때마다 그 병종 모두의 크기가 커지고, 크기가 N배 이상이면 공격할 때마다 공격력만큼 주변 아군을 회복한다 (크루세이더 길드)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Crusader")]
    public class CrusaderEffect : UnitEffect
    {
        [Tooltip("기준 크기 대비 — 0.05 = +5%")]
        [SerializeField] private float _growPerSummon = 0.05f;
        [SerializeField] private float _healerSize = 2f;
        [SerializeField] private float _healRadius = 3f;

        public override void OnSummoned(Unit unit, int sourceSlot, int stacks)
        {
            foreach (Unit ally in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (ally.IsAlive && ally.Team == unit.Team && this.AppliesFor(ally.Team, ally.Definition))
                    ally.Grow(_growPerSummon);
        }

        public override void OnAttack(Unit attacker, IDamageable target, int stacks)
        {
            if (attacker.Size >= _healerSize)
                attacker.HealAlliesAround(_healRadius, attacker.AttackDamage);
        }
    }
}
