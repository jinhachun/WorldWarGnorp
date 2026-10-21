using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 전투당 한 번, 내 본진의 체력이 일정 비율 아래로 떨어지면 유닛(기사) N기를 소환한다 (국가 전용 업그레이드 「왕성 수비대」 — 킹덤) — N은 업그레이드의 합쳐진 값.
    // 비율은 그 전투의 본진 최대 체력 기준(성벽 보강 포함). 작은 타워는 아니다. 기사는 성에서 나온다(자리를 정하지 않은 소환). 나왔는지는 이 효과의 스택(전투마다 0부터)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Base Rally")]
    public class UpgradeBaseRallyEffect : UnitEffect
    {
        [SerializeField] private UnitDefinition _summon;
        [Tooltip("본진 체력이 최대 체력의 이 비율 아래로 떨어지면 — 0.5 = 50%")]
        [SerializeField] private float _hpBelow = 0.5f;

        public override object[] UpgradeNumbers() => new object[] { (_hpBelow * 100f).ToString("0.##") };

        public override void OnBaseDamaged(Base home)
        {
            if (_summon == null || home.Hp01 >= _hpBelow)
                return;
            foreach (TeamEffects.Entry e in TeamEffects.For(home.Team))
                if (e.Effect == this)
                {
                    if (e.Stacks > 0)
                        return;
                    e.Stacks = 1;
                    break;
                }
            BoardRunner board = BoardRunner.For(home.Team);
            board.Summon(_summon, Mathf.Max(1, Mathf.RoundToInt(Value(home.Team))), -1);
            // 켠 기물이 없다 — 본진에서 나온 무리로 소환 선, 첫 기 발밑에 기둥
            if (board.LastSpawned is Unit first)
            {
                PillarFx.Rise(new Vector2(first.Position.x, first.Feet), first.Top - first.Feet);
                BuffLinks.FromBase(home.Team, first.Position, BuffKind.Summon);
            }
        }
    }
}
