using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(내 경보병)이 피해를 입으면 N% 확률로 M초간 무적 (결사대 — 등급마다 +5%p, 사용자 10/28).
    // 피해를 입은 뒤에 굴린다(무적인 동안은 피해를 안 입으므로 굴리지 않는다 — Unit.TakeDamage)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Hit Chance Invulnerable")]
    public class HitChanceInvulnerableEffect : UnitEffect
    {
        [SuffixLabel("%", true)]
        [SerializeField] private float _chance = 20f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SuffixLabel("%p", true)]
        [SerializeField] private float _chancePerTier = 5f;
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 1f;

        private float Chance(int level) => OwnedBuilding.Scaled(_chance, _chancePerTier, level);

        public override bool OnHit(Unit unit, IDamageable attacker, Vector2 hitDirection, float push, int stacks)
        {
            if (Random.value * 100f < Chance(Level(unit.Team)))
            {
                unit.GrantInvulnerable(_seconds);
                BuffLinks.EffectToPoint(unit.Team, this, unit.Position, BuffKind.Invulnerable);
            }
            return false;
        }

        public override string Describe(int level) => string.Format(Description, Chance(level), _seconds);
        public override string Headline(int level) => $"{Chance(level):0.#}%";
    }
}
