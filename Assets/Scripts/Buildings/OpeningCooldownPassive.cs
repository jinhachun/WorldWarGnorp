using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 전투 시작 후 S초 동안 내 모든 기물의 쿨다운 ×A, 그 뒤로는 ×B (모래시계 — 사용자 10/27). 신화면 S가 _mythicSeconds(30 → 60초, 수치 동결의 예외).
    // 맵의 쿨다운 배율(얼음 · 정글)과 같은 길 — 게이지가 차는 속도에 곱한다(BoardRunner.AddTimedCooldown). 쿨다운 숫자 자체는 바꾸지 않는다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Opening Cooldown")]
    public class OpeningCooldownPassive : BuildingPassive
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 30f;
        [SuffixLabel("초", true)]
        [SerializeField] private float _mythicSeconds = 60f;
        [Tooltip("처음 S초 동안 쿨다운에 곱한다")]
        [SerializeField] private float _early = 0.5f;
        [Tooltip("그 뒤로 쿨다운에 곱한다")]
        [SerializeField] private float _late = 1.2f;

        private float Seconds(bool mythic) => mythic ? _mythicSeconds : _seconds;

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            bool mythic = board.Slots[slot].Building.Rarity == BuildingRarity.Mythic;
            board.AddTimedCooldown(slot, Seconds(mythic), _early, _late);
        }

        // 「내 모든 기물」 — 쿨다운 배율이 달라지는 것은 쿨다운이 있는 기물뿐
        public override bool SlotFree => true;
        public override bool TargetsFloor(OwnedBuilding[] field, int slot, int other, bool federated) => BuildingTargets.Cooling(field, other);
        public override string Describe(int level) => Describe(level, false);
        public override string Describe(int level, bool mythic)
            => $"처음 {Seconds(mythic):0.#}초간 모든 기물 쿨다운 ×{_early:0.##}\n그 뒤로는 쿨다운 ×{_late:0.##}";
        public override string Headline(int level) => $"{_seconds:0.#}초";
    }
}
