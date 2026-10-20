using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 공격할 때마다, 아군을 포함해 주변의 다른 모든 유닛에게도 같은 피해를 준다 (툰드라 맘모스 — 트라이브 명세 §3).
    // 노린 대상은 원래 타격으로 이미 맞았으므로 여기서는 뺀다. 아군을 죽인 것도 이 유닛의 처치다(사용자 결정 10/14). 본진 · 타워는 맞지 않는다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Attack Splash All")]
    public class AttackSplashAllEffect : UnitEffect
    {
        [Tooltip("공격한 유닛의 중심에서")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _radius = 3f;

        private static readonly System.Collections.Generic.List<Collider2D> Overlaps = new System.Collections.Generic.List<Collider2D>();
        private static readonly System.Collections.Generic.HashSet<Unit> Hit = new System.Collections.Generic.HashSet<Unit>();
        private static readonly ContactFilter2D SolidOnly = new ContactFilter2D { useTriggers = false };

        public override void OnAttack(Unit attacker, IDamageable target, int stacks)
        {
            // 맞는 범위 — 아군도 맞으므로 빨강
            RingFx.Pulse(attacker.Position, _radius, BuffLinkFx.ColorOf(BuffKind.Stat), attacker);
            Physics2D.OverlapCircle(attacker.Position, _radius, SolidOnly, Overlaps);
            Hit.Clear();
            float damage = attacker.AttackDamage * attacker.DamageScale;
            foreach (Collider2D col in Overlaps.ToArray())
            {
                if (!col.TryGetComponent(out Unit other) || other == attacker || ReferenceEquals(other, target) || !other.IsAlive || !Hit.Add(other))
                    continue;
                Vector2 away = other.Position - attacker.Position;
                other.TakeDamage(damage, away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up, 0f, attacker);
            }
        }
    }
}
