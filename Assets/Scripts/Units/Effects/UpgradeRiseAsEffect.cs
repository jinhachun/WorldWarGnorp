using UnityEngine;

namespace GnorpWar
{
    // 전투마다, 처음 죽는 적용 대상 유닛 N기는 그 자리에서 다른 유닛(원시의 전사)으로 다시 일어난다 (국가 전용 업그레이드 「영혼의 메아리」 — 트라이브) — N은 업그레이드의 합쳐진 값.
    // 센 수는 이 효과의 스택(전투마다 0부터). 그 자리에서 다시 일어나는 죽음(죽지 않는 전사 — Unit.Reviving)은 세지 않고, 이 효과로 일어난 유닛이 또 죽어도 세지 않는다(일어날 때 표시 — UseOnce).
    // 일어나는 것은 소환이 아니다(추가 소환 · 변환 · 「소환할 때마다」를 지나지 않는다 — Unit.Spawn)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Rise As")]
    public class UpgradeRiseAsEffect : UnitEffect
    {
        [Tooltip("다시 일어날 유닛")]
        [SerializeField] private UnitDefinition _into;

        // 일어날 유닛의 늘 켜진 효과(원시의 전사 — 뼛가루)를 미리 켠다 — 사망 훅 안에서 효과 목록이 늘지 않게
        public override void OnEnabled(Team team)
        {
            if (_into != null)
                foreach (UnitEffect innate in _into.InnateEffects)
                    TeamEffects.Enable(team, innate, 1);
        }

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            if (_into == null || unit.Reviving || !unit.UseOnce(this) || stacks >= Mathf.RoundToInt(Value(unit.Team)))
                return;
            TeamEffects.AddStack(unit.Team, this);
            Vector2 ground = new Vector2(at.x, Ground.Instance.HeightAt(at.x));
            Unit risen = Unit.Spawn(_into.PrefabFor(unit.Team), ground);
            risen.UseOnce(this);   // 이 유닛이 죽어도 세지 않는다
            PillarFx.Rise(ground, risen.Top - ground.y);   // 다시 일어남 — 죽지 않는 전사와 같은 기둥
        }
    }
}
