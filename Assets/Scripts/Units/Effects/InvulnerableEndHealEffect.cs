using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛의 무적이 끝날 때 최대 체력의 N%를 회복한다 (결사대 신화 — 내 경보병 30%, 사용자 10/28). 어느 기물이 준 무적이든 끝나면 회복한다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Invulnerable End Heal")]
    public class InvulnerableEndHealEffect : UnitEffect
    {
        [SuffixLabel("%", true)]
        [SerializeField] private float _percent = 30f;

        public override void OnInvulnerableEnded(Unit unit) => unit.Heal(unit.MaxHp * _percent / 100f);

        public override string Describe(int level) => string.Format(Description, _percent);
    }
}
