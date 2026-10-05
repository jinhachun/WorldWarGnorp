using UnityEngine;

namespace GnorpWar
{
    // 무적 상태인 적용 대상 유닛의 공격력 +N% (공주 루니카 — 내 아군 전부)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Invulnerable Fury")]
    public class InvulnerableFuryEffect : UnitEffect
    {
        [Tooltip("1 = +100%")]
        [SerializeField] private float _bonus = 1f;

        public override float InvulnerableAttackBonus => _bonus;
    }
}
