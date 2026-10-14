using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 죽을 때마다, 확률로 그 자리에 유닛을 소환한다 (툰드라 행진: 내 경보병이 죽을 때마다 30% 확률로 원시 졸병 1기 · 신화: 내 중보병이 죽을 때마다 5기 — 트라이브 명세).
    // 소환된 유닛이 다시 대상이면 그 유닛의 죽음에도 또 굴린다(연쇄가 의도) — 죽음 한 번에 한 번만 굴리므로 한 프레임에 끝없이 이어지지 않는다.
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

        private float Chance(int level) => OwnedBuilding.Scaled(_chance, _chancePerTier, level);

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            if (Random.value * 100f >= Chance(Level(unit.Team)))
                return;
            Vector2 ground = new Vector2(at.x, Ground.Instance.HeightAt(at.x));
            BoardRunner.For(unit.Team).Summon(_summon, _count, -1, ground, Source(unit.Team));
            BuffLinks.EffectToPoint(unit.Team, this, ground, BuffKind.Summon);
        }

        public override string Describe(int level) => string.Format(Description, Chance(level).ToString("0.##"), _count);
        public override string Headline(int level) => Chance(level) < 100f ? $"{Chance(level):0.##}%" : $"{_count}기";
    }
}
