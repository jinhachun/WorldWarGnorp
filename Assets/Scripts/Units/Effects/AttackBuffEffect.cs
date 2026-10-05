using UnityEngine;

namespace GnorpWar
{
    // 회복 유닛이 회복과 번갈아 아군에게 공격력 버프 십자가를 던진다 — 대상 = 사거리 안 버프 없는 공격 유닛 중 가장 앞
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Attack Buff")]
    public class AttackBuffEffect : UnitEffect
    {
        [Tooltip("공격력 증가율 (0.5 = +50%)")]
        [SerializeField] private float _bonus = 0.5f;
        [SerializeField] private float _duration = 5f;
        [SerializeField] private Projectile _cross;

        public Unit FindTarget(Unit healer)
        {
            Unit best = null;
            float bestAhead = float.MinValue;
            foreach (Collider2D col in healer.AlliesInRange(healer.Definition.AttackRange))
            {
                if (!col.TryGetComponent(out Unit ally) || ally == healer || ally.Team != healer.Team || !ally.IsAlive
                    || ally.IsBuffed || ally.Definition.AttackType == AttackType.Heal)
                    continue;

                float ahead = ally.transform.position.x * healer.Forward;
                if (ahead > bestAhead)
                {
                    bestAhead = ahead;
                    best = ally;
                }
            }
            return best;
        }

        public void Throw(Unit healer, Unit target)
        {
            healer.ThrowBuff(_cross, _bonus, _duration, target);
        }
    }
}
