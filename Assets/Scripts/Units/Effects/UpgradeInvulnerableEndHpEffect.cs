using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛의 무적이 끝날 때마다, 전투 동안 그 유닛의 최대 체력 +N%(합연산 · 쌓인다) (국가 전용 업그레이드 「신앙의 갑주」 — 킹덤) — N은 업그레이드의 합쳐진 값.
    // 지금 체력은 최대 체력과 같은 비율로 오른다(Unit.RefreshStats — 바위의 토템과 같은 규칙). 연출: 그 유닛 위에 체력 버프 글자
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Invulnerable End Hp")]
    public class UpgradeInvulnerableEndHpEffect : UnitEffect
    {
        public override void OnInvulnerableEnded(Unit unit)
        {
            var modifier = new StatModifier(UnitStat.MaxHp, StatOp.Percent, Value(unit.Team) / 100f);
            if (modifier.Value <= 0f)
                return;
            unit.AddModifier(modifier);
            BuffTexts.Show(new Vector2(unit.Position.x, unit.Top), BuffLabel.Of(modifier));
        }
    }
}
