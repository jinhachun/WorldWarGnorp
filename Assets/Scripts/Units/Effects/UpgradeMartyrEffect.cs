using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(내 지원병)이 죽을 때마다, 죽은 자리 주변의 아군이 N초간 무적 (국가 전용 업그레이드 「순교」 — 킹덤) — N은 업그레이드의 합쳐진 값.
    // 무적은 Unit.GrantInvulnerable 한 곳 — 이미 무적이면 남은 시간이 긴 쪽. 연출: 범위 고리 + 죽은 자리 → 받은 무리 가운데 무적 선
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Martyr")]
    public class UpgradeMartyrEffect : UnitEffect
    {
        [SuffixLabel("칸", true)]
        [SerializeField] private float _radius = 3f;

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            float seconds = Value(unit.Team);
            if (seconds <= 0f)
                return;
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Unit ally in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (ally != unit && ally.IsAlive && ally.Team == unit.Team && (ally.Position - at).sqrMagnitude <= _radius * _radius)
                {
                    ally.GrantInvulnerable(seconds);
                    sum += ally.Position;
                    count++;
                }
            if (count == 0)
                return;
            RingFx.Pulse(at, _radius, BuffLinkFx.ColorOf(BuffKind.Invulnerable), this);
            BuffLinks.EffectFromUnit(unit.Team, this, at, sum / count, BuffKind.Invulnerable);
        }
    }
}
