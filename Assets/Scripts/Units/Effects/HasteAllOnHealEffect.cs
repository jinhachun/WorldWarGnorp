using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(사제)이 회복할 때마다 내 모든 기물을 가속한다 (수도원 신화 — 기획서 §7-1). 쿨다운 있는 기물만(§7과 같다).
    // 신화는 등급이 하나뿐이라 초를 등급으로 올리지 않는다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Haste All On Heal")]
    public class HasteAllOnHealEffect : UnitEffect
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 0.5f;

        // §7과 같은 자리 — 구슬이 닿아 체력이 실제로 찼을 때(HasteOnHealEffect)
        public override void OnHealLanded(Team team, Unit patient, float amount, float overflow, int stacks)
        {
            BoardRunner board = BoardRunner.For(team);
            board.HasteAll(_seconds);
            for (int i = 0; i < board.Slots.Length; i++)
                if (board.Slots[i].Building != null && board.Slots[i].Cooldown > 0f)
                    BuffLinks.EffectToSlot(team, this, i, BuffKind.Haste);
        }

        public override string Describe(int level) => string.Format(Description, $"{_seconds:0.#}");
    }
}
