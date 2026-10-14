using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 전투당 한 번, 적용 대상 유닛이 N기 죽으면 유닛을 소환한다 — N번째로 죽은 자리에서
    // (미궁의 입구: 내 경보병이 40기 죽으면 미노타우로스 1기 — 사용자 10/15. 필요한 수가 등급마다 -5(고급 40 · 희귀 35 · 영웅 30 · 전설 25), 소환 수는 늘 그대로).
    // 경보병으로도 취급되는 유닛(군제 개혁)도 센다(적용 판정이 UnitAliases를 본다). 죽은 수는 이 효과의 스택으로 센다 — 기물이 쿨다운이 없어 스택이 따로 오르지 않는다, 전투마다 0부터
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Death Count Summon")]
    public class DeathCountSummonEffect : UnitEffect
    {
        [Tooltip("이만큼 죽으면(전투당 한 번, 시작 등급)")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _deaths = 40;
        [Tooltip("등급이 오를 때마다 필요한 수에 더한다(음수 = 줄어든다)")]
        [SerializeField] private int _deathsPerTier = -5;
        [SerializeField] private UnitDefinition _summon;
        [Tooltip("소환 수")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;
        [Tooltip("신화 표시 — 이 진영에 켜져 있으면 이 효과가 소환할 때 아래 유닛도 함께 소환한다(미궁의 입구 신화: 원시 졸병 5기 — 이 기물의 소환에만, 총괄 결정 10/15)")]
        [SerializeField] private UnitEffect _mythicMark;
        [SerializeField] private UnitDefinition _mythicExtra;
        [SuffixLabel("기", true)]
        [SerializeField] private int _mythicExtraCount = 5;

        private int Deaths(int level) => Mathf.Max(1, OwnedBuilding.Scaled(_deaths, _deathsPerTier, level));

        // 이번 전투에서 발동까지 남은 수(진영별, 툴팁 · 탑 칸 표시용) — 이미 발동했으면 0, 전투 밖에서는 시작 등급 값
        public int RemainingOf(Team team)
        {
            if (!BattleManager.Fighting)
                return _deaths;
            foreach (TeamEffects.Entry e in TeamEffects.For(team))
                if (e.Effect == this)
                    return Mathf.Max(0, Deaths(Level(team)) - e.Stacks);
            return Deaths(Level(team));
        }

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            TeamEffects.AddStack(unit.Team, this);
            if (stacks + 1 != Deaths(Level(unit.Team)))
                return;   // 정확히 N번째에만 — 그 뒤로는 이번 전투에서 다시 나오지 않는다
            Vector2 ground = new Vector2(at.x, Ground.Instance.HeightAt(at.x));
            BoardRunner board = BoardRunner.For(unit.Team);
            board.Summon(_summon, _count, -1, ground, Source(unit.Team));
            if (_mythicMark != null && _mythicExtra != null)
                foreach (TeamEffects.Entry e in TeamEffects.For(unit.Team))
                    if (e.Effect == _mythicMark)
                    {
                        board.Summon(_mythicExtra, _mythicExtraCount, -1, ground, Source(unit.Team));
                        break;
                    }
            BuffLinks.EffectToPoint(unit.Team, this, ground, BuffKind.Summon);
        }

        // 설명의 {0} = 지금 등급의 필요한 수 · {1} = 소환 수
        public override string Describe(int level) => string.Format(Description, Deaths(level), _count);
        public override string Headline(int level) => $"{_count}기";
    }
}
