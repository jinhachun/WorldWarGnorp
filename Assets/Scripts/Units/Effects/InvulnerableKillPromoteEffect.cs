using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 무적 상태인 적용 대상 유닛(내 경보병)이 적을 처치하면 그 자리에서 다른 유닛(기사)으로 승급한다 (기사 서임식 — 사용자 10/28).
    // 무적이 아니어도 등급에 따라 N% 확률로 승급한다(영웅 0 · 전설 25). 승급은 Promotion.Promote(남은 체력 비율 · 남은 무적 · 연계 출생 표시를 이어 간다)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Invulnerable Kill Promote")]
    public class InvulnerableKillPromoteEffect : UnitEffect
    {
        [Tooltip("승급해 될 유닛")]
        [SerializeField] private UnitDefinition _into;
        [Tooltip("무적이 아닐 때 승급할 확률 — 시작 등급 값")]
        [SuffixLabel("%", true)]
        [SerializeField] private float _chance = 0f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SuffixLabel("%p", true)]
        [SerializeField] private float _chancePerTier = 25f;
        [Tooltip("무적이 아닐 때 확률을 보여 주는 줄 — {0}에 확률(확률이 0이면 안 보인다)")]
        [TextArea]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _chanceLine = "";

        private float Chance(int level) => OwnedBuilding.Scaled(_chance, _chancePerTier, level);

        // 처치 훅(Unit.Perish)은 개수를 잡아 둔 for로 돈다 — 승급한 유닛이 늘 켜진 효과를 목록에 붙여도 안전하다
        public override void OnKill(Unit killer, Unit victim, int stacks)
        {
            if (!killer.IsAlive || killer.Definition == _into)
                return;
            if (killer.IsInvulnerable || Random.value * 100f < Chance(Level(killer.Team)))
                Promotion.Promote(killer, _into);
        }

        public override string Describe(int level)
            => Chance(level) > 0f ? $"{Description}\n{string.Format(_chanceLine, Chance(level))}" : Description;
        public override string Headline(int level) => Chance(level) > 0f ? $"{Chance(level):0.#}%" : null;
    }
}
