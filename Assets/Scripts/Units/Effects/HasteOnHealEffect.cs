using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(사제)이 회복할 때마다 무작위 기물 하나를 가속한다 (수도원 — 쿨다운 있는 기물만)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Haste On Heal")]
    public class HasteOnHealEffect : UnitEffect
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 0.5f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _secondsPerTier = 0.5f;

        private float Seconds(int level) => OwnedBuilding.Scaled(_seconds, _secondsPerTier, level);

        // 던진 순간이 아니라 구슬이 닿아 체력이 실제로 찼을 때(사용자 10/10 "닿으면 걸리게") — 빗나간 구슬은 가속 없음
        public override void OnHealLanded(Team team, Unit patient, float amount, float overflow, int stacks)
        {
            int picked = BoardRunner.For(team).HasteRandom(Seconds(Level(team)));
            if (picked >= 0)
                BuffLinks.EffectToSlot(team, this, picked, BuffKind.Haste);
        }

        // 무작위 — 후보(쿨다운이 있는 기물) 전부
        public override bool SlotFree => true;
        public override bool TargetsFloor(OwnedBuilding[] field, int slot, int other, bool federated) => BuildingTargets.Cooling(field, other);
        public override string Describe(int level) => string.Format(Description, $"{Seconds(level):0.#}");
        public override string Headline(int level) => $"{Seconds(level):0.#}초";
    }
}
