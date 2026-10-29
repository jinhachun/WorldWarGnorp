using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 적을 처치할 때마다 본진에서 유닛을 소환한다.
    // 「연계는 한 번만」(사용자 10/26) — 연계로 태어난 유닛(Unit.ChainBorn)의 처치는 다시 부르지 않는다. 낸 유닛은 연계로 태어남
    // 왕족친위대(사용자 10/30): 기사가 처치하면 확률로 친위기사, 친위기사가 처치하면 같은 확률로 기사(_back) — 서로 이어지므로 연계 규칙의 예외(_chainAgain). 등급마다 확률이 오른다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Kill Summon")]
    public class KillSummonEffect : UnitEffect
    {
        [SerializeField] private UnitDefinition _summon;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _countPerTier = 1;
        [Tooltip("발동 확률(시작 등급, 100 = 언제나)")]
        [SuffixLabel("%", true)]
        [SerializeField] private float _chance = 100f;
        [Tooltip("등급이 오를 때마다 확률에 더한다")]
        [SuffixLabel("%", true)]
        [SerializeField] private float _chancePerTier = 0f;
        [Tooltip("위 소환 유닛이 처치하면 대신 이 유닛을 소환한다(왕족친위대: 친위기사 → 기사). 적용 대상 유닛에 위 소환 유닛도 넣을 것. 비우면 없음")]
        [SerializeField] private UnitDefinition _back;
        [Tooltip("연계로 태어난 유닛의 처치도 발동한다(「연계는 한 번만」의 예외 — 확률이라 저절로 끊긴다)")]
        [SerializeField] private bool _chainAgain;

        private int Count(int level) => OwnedBuilding.Scaled(_count, _countPerTier, level);
        private float Chance(int level) => OwnedBuilding.Scaled(_chance, _chancePerTier, level);

        public override void OnKill(Unit killer, Unit victim, int stacks)
        {
            if (killer.ChainBorn && !_chainAgain)
                return;
            if (Chance(Level(killer.Team)) < 100f && Random.value * 100f >= Chance(Level(killer.Team)))
                return;
            SummonFor(killer, _back != null && killer.Definition == _summon ? _back : _summon);
        }

        protected void SummonFor(Unit killer) => SummonFor(killer, _summon);

        private void SummonFor(Unit killer, UnitDefinition summon)
        {
            BoardRunner board = BoardRunner.For(killer.Team);
            board.Summon(summon, Count(Level(killer.Team)), -1, source: Source(killer.Team), chained: true);
            // 처치한 유닛에서 성에서 나온 유닛으로
            if (board.LastSpawned != null)
                BuffLinks.EffectFromUnit(killer.Team, this, killer.Position, board.LastSpawned.Position, BuffKind.Summon);
        }

        // 설명의 {0} = 소환 수 · {1} = 확률
        public override string Describe(int level) => string.Format(Description, Count(level), Chance(level));
        public override string Headline(int level) => _chance < 100f ? $"{Chance(level):0.#}%" : $"{Count(level)}기";
    }
}
