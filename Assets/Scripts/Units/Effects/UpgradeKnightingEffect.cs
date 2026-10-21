using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(내 경보병)이 적 N기를 처치하면 그 자리에서 다른 유닛(기사)으로 바뀐다 (국가 전용 업그레이드 「기사 서임」 — 킹덤) — N은 업그레이드의 값(여럿이면 가장 센 것 하나).
    // 처치 수는 유닛마다 이 생애에서 센다(Unit.AddCount). 바뀌는 것은 소환이 아니다 — 추가 소환 · 변환 · 「소환할 때마다」를 지나지 않는다(Unit.Spawn).
    // 남은 체력 비율을 이어 가고, 전 유닛이 이 전투 동안 받은 강화는 사라진다. 전 유닛은 소모된다(죽음이 아니다 — 사망 효과 없음)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Knighting")]
    public class UpgradeKnightingEffect : UnitEffect
    {
        [Tooltip("바뀌어 될 유닛")]
        [SerializeField] private UnitDefinition _into;

        // 바뀔 유닛의 늘 켜진 효과를 미리 켠다 — 처치 훅 안에서 효과 목록이 늘지 않게(UnitEffect.OnEnabled)
        public override void OnEnabled(Team team)
        {
            if (_into != null)
                foreach (UnitEffect innate in _into.InnateEffects)
                    TeamEffects.Enable(team, innate, 1);
        }

        public override void OnKill(Unit killer, Unit victim, int stacks)
        {
            int needed = Mathf.Max(1, Mathf.RoundToInt(Value(killer.Team)));
            if (_into == null || killer.Definition == _into || killer.AddCount(this) != needed)
                return;
            float ratio = killer.Hp / killer.MaxHp;
            Vector2 at = killer.Position;
            Team team = killer.Team;
            killer.Consume();
            Unit knight = Unit.Spawn(_into.PrefabFor(team), new Vector2(at.x, Ground.Instance.HeightAt(at.x)));
            knight.SetHpRatio(ratio);
            // 바뀐 순간 — 발밑 기둥 + 흰 고리(새 연출은 수수하게, 있는 부품으로)
            PillarFx.Rise(new Vector2(knight.Position.x, knight.Feet), knight.Top - knight.Feet);
            RingFx.Pulse(knight.Position, knight.Top - knight.Feet, Color.white, knight);
        }
    }
}
