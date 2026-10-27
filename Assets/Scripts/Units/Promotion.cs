using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 승급 — 내 경보병이 그 자리에서 기사로 바뀐다(국가 전용 업그레이드 「기사 서임」 · 기사 서임식, 사용자 10/28).
    // 바뀌는 것은 소환이 아니다(Unit.Replace — 추가 소환 · 변환 · 「소환할 때마다」를 지나지 않는다).
    // 바뀐 직후 전 유닛의 정의로 거르는 효과들에 「승급했다」를 알린다(UnitEffect.OnPromoted — 반군의 깃발 · 서임식 신화)
    public static class Promotion
    {
        // 이미 바뀌었거나 죽은 유닛이면 null — 한 처치에 승급 효과가 둘 걸려도(기사 서임 + 서임식) 한 번만 바뀐다
        public static Unit Promote(Unit soldier, UnitDefinition into)
        {
            if (!soldier.IsAlive || into == null || soldier.Definition == into)
                return null;
            Team team = soldier.Team;
            UnitDefinition from = soldier.Definition;
            Unit knight = Unit.Replace(soldier, into);
            RingFx.Pulse(knight.Position, knight.Top - knight.Feet, Color.white, knight);
            // 알림 훅이 소환할 수 있다(반군의 깃발 신화) — 지금 개수까지만
            List<TeamEffects.Entry> effects = TeamEffects.For(team);
            for (int i = 0, n = effects.Count; i < n; i++)
                if (effects[i].Effect.AppliesFor(team, from))
                    effects[i].Effect.OnPromoted(knight, effects[i].Stacks);
            return knight;
        }
    }
}
