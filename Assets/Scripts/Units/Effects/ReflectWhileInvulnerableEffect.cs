using UnityEngine;

namespace GnorpWar
{
    // 무적 상태인 적용 대상 유닛이 막은 피해를 공격한 적에게 그대로 돌려준다 (공주 루니카 신화 — 기획서 §7-1).
    // 「적 궁병의 공격」은 공격자의 병종으로 가린다 — 근접·사격을 가리지 않고, 타워의 화살은 궁병이 아니라 걸리지 않는다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Reflect While Invulnerable")]
    public class ReflectWhileInvulnerableEffect : UnitEffect
    {
        [Tooltip("이 병종의 적이 때렸을 때만 돌려준다 — 비우면 전부")]
        [SerializeField] private UnitCategory[] _attackerCategories = new UnitCategory[0];

        public override bool ReflectsBlockedDamage(IDamageable attacker)
            => _attackerCategories.Length == 0
               || (attacker is Unit enemy && System.Array.IndexOf(_attackerCategories, enemy.Definition.Category) >= 0);
    }
}
