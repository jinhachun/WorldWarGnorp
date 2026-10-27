using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 바로 위 기물의 쿨다운 +N초, 소환하는 유닛 수 +M (제물의 의식 — 사용자 10/27). 칸 번호가 클수록 위층. 위가 비면 효과 없음.
    // 전투 시작 때 한 번 건다(풍차와 같은 길). 쿨다운 없는 기물은 아무것도 받지 않는다(발동하지 않으므로). 소환하지 않는 기물은 쿨다운만 늘어난다.
    // 소환 수는 징병제가 더해지는 곳(기물의 소환 · SummonCountAction)에 같이 더해진다.
    // 신화는 수치 동결의 예외 — 신화 값을 따로 둔다(사용자 10/27: 신화 = 쿨다운 +1초 · 소환 +2). 신화 줄은 따로 없다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Above Cooldown Summon")]
    public class AboveCooldownSummonPassive : BuildingPassive
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _cooldownAdd = 5f;
        [Tooltip("등급이 오를 때마다 더한다 — 줄일수록 덜 느려지므로 음수")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _cooldownAddPerTier = -2f;
        [SuffixLabel("초", true)]
        [SerializeField] private float _mythicCooldownAdd = 1f;
        [SuffixLabel("기", true)]
        [SerializeField] private int _extraSummons = 1;
        [SuffixLabel("기", true)]
        [SerializeField] private int _mythicExtraSummons = 2;

        private float CooldownAdd(int level, bool mythic) => mythic ? _mythicCooldownAdd : OwnedBuilding.Scaled(_cooldownAdd, _cooldownAddPerTier, level);
        private int ExtraSummons(bool mythic) => mythic ? _mythicExtraSummons : _extraSummons;

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            bool mythic = board.Slots[slot].Building.Rarity == BuildingRarity.Mythic;
            float seconds = CooldownAdd(level, mythic);
            for (int i = 0; i < board.Slots.Length; i++)
            {
                BoardRunner.Slot target = board.Slots[i];
                if (!board.Linked(slot, i, i == slot + 1) || target.Building == null || target.Cooldown <= 0f)
                    continue;
                board.AddCooldown(i, seconds);
                target.ExtraSummons += ExtraSummons(mythic);
                OwnedBuilding.NotifyEffect(board.Slots[slot].Building);
                OwnedBuilding.NotifyEffect(target.Building);
                // 늘 켜진 효과 — 지금 한 번, 그 뒤로는 대상이 발동할 때마다 약하게(BuffLinks.Standing). 소환하는 기물이면 소환 색, 아니면 쿨다운 색
                bool summons = target.Building.Definition.Unit != null;
                BuffLinks.Standing(board.Team, slot, i, summons ? BuffKind.Summon : BuffKind.Haste, BuffLabel.CooldownSeconds(seconds));
            }
        }

        public override bool Reaches(int slot, int other, BuildingDefinition target) => other == slot + 1 && target.Cooldown > 0f;

        public override bool UsesAdjacency => true;
        public override string Describe(int level) => Describe(level, false);
        // 한 줄 = 한 효과(CLAUDE.md §5-2-2) — 툴팁이 패시브 하나를 한 줄로 붙이므로 두 효과를 줄바꿈으로 나눈다
        public override string Describe(int level, bool mythic)
            => $"바로 위 기물의 쿨다운 +{CooldownAdd(level, mythic):0.#}초\n바로 위 기물이 소환하는 유닛 수 +{ExtraSummons(mythic)}";
    }
}
