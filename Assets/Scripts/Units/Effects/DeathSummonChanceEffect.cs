using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 죽을 때마다, 확률로 그 자리에 유닛을 소환한다 (툰드라 행진: 내 경보병이 죽을 때마다 30% 확률로 원시 졸병 1기 · 신화: 내 중보병이 죽을 때마다 5기 — 트라이브 명세).
    // 연계로 태어난 유닛(Unit.ChainBorn — 이 효과 · 다른 「죽으면 · 처치하면 · 소환하면 소환」이 낸 유닛)의 죽음에는 굴리지 않는다(사용자 10/26: 서로 영향 주는 연계는 한 번만 —
    // 거대 괴수의 무덤 신화 + 군제 개혁·중장보병이면 전사의 죽음마다 전사 5기로 불어났다).
    // 자리는 다른 사망 소환(DeathSummonEffect)과 같다 — 죽은 자리의 땅
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Death Summon Chance")]
    public class DeathSummonChanceEffect : UnitEffect
    {
        [SerializeField] private UnitDefinition _summon;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;
        [Tooltip("100이면 늘")]
        [SuffixLabel("%", true)]
        [SerializeField] private float _chance = 30f;
        [Tooltip("등급이 오를 때마다 확률에 더한다(%p)")]
        [SuffixLabel("%p", true)]
        [SerializeField] private float _chancePerTier;
        [Tooltip("켜면 확률을 켠 합치기 보상 업그레이드의 값에서 읽는다(사냥의 계절 — 위 확률 칸은 안 쓴다)")]
        [SerializeField] private bool _chanceFromUpgrade;

        private float Chance(int level) => OwnedBuilding.Scaled(_chance, _chancePerTier, level);

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            if (unit.ChainBorn || Random.value * 100f >= (_chanceFromUpgrade ? Value(unit.Team) : Chance(Level(unit.Team))))
                return;
            Vector2 ground = new Vector2(at.x, Ground.Instance.HeightAt(at.x));
            BoardRunner.For(unit.Team).Summon(_summon, _count, -1, ground, Source(unit.Team), chained: true);
            BuffLinks.EffectToPoint(unit.Team, this, ground, BuffKind.Summon);
        }

        public override string Describe(int level) => string.Format(Description, Chance(level).ToString("0.##"), _count);
        public override string Headline(int level) => Chance(level) < 100f ? $"{Chance(level):0.##}%" : $"{_count}기";
    }
}
