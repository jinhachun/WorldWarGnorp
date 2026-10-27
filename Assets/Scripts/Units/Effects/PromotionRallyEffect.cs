using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 반군의 깃발(사용자 10/28) — 적용 대상 유닛(내 경보병)이 승급할 때마다 전장의 모든 적용 대상 유닛이 N초간 무적(진영마다 M초에 한 번까지)
    // · 무적 상태인 적용 대상 유닛의 공격력 +K%(합연산 — 공주 루니카의 +100%와 같은 칸에 더해진다, Unit.Compute)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Promotion Rally")]
    public class PromotionRallyEffect : UnitEffect
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 1f;
        [Tooltip("진영마다 이 시간에 한 번까지")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _cooldown = 3f;
        [Tooltip("1 = +100%")]
        [SerializeField] private float _attackBonus = 1f;

        // 진영마다 다음에 발동할 수 있는 시각(Time.fixedTime — 전투가 바뀌어도 거꾸로 가지 않는다)
        [System.NonSerialized] private readonly float[] _readyAt = new float[2];

        public override float InvulnerableAttackBonus => _attackBonus;

        public override void OnPromoted(Unit promoted, int stacks)
        {
            Team team = promoted.Team;
            if (Time.fixedTime < _readyAt[(int)team])
                return;
            _readyAt[(int)team] = Time.fixedTime + _cooldown;
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == team && this.AppliesFor(team, unit.Definition))
                    unit.GrantInvulnerable(_seconds);
            BuffLinks.EffectToUnits(team, this, u => this.AppliesFor(team, u.Definition), BuffKind.Invulnerable);
        }

        public override string Describe(int level) => string.Format(Description, _seconds, _cooldown, _attackBonus * 100f);
        public override string Headline(int level) => $"{_seconds:0.#}초";
    }
}
