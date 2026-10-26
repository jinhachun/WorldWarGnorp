using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 적을 처치할 때마다 본진에서 유닛을 소환한다 (왕족친위대: 경기병 → 친위기사).
    // 「연계는 한 번만」(사용자 10/26)의 예외 — 친위기사도 경기병이라 그 처치가 또 친위기사를 부른다(기물기획서 「친위기사도 연쇄 발동」, 사용자 결정 10/26).
    // 끝없지는 않다 — 처치 한 번에 한 번이고 유닛 수 상한(Battle/ChainGuard) 아래다. 낸 유닛은 연계로 태어남(죽음 연계는 안 부른다)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Kill Summon")]
    public class KillSummonEffect : UnitEffect
    {
        [SerializeField] private UnitDefinition _summon;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _countPerTier = 1;

        private int Count(int level) => OwnedBuilding.Scaled(_count, _countPerTier, level);

        public override void OnKill(Unit killer, Unit victim, int stacks)
        {
            BoardRunner board = BoardRunner.For(killer.Team);
            board.Summon(_summon, Count(Level(killer.Team)), -1, source: Source(killer.Team), chained: true);
            // 처치한 유닛에서 성에서 나온 유닛으로
            if (board.LastSpawned != null)
                BuffLinks.EffectFromUnit(killer.Team, this, killer.Position, board.LastSpawned.Position, BuffKind.Summon);
        }

        public override string Describe(int level) => string.Format(Description, Count(level));
        public override string Headline(int level) => $"{Count(level)}기";
    }
}
