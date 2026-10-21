using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛(내 톱니병사)이 N기 죽을 때마다, 골드 1을 얻는다 (국가 전용 업그레이드 「재활용」 — 티크톡) — N은 얻은 것마다 따로(UnitEffect.Values — 둘 가지면 둘 다 센다).
    // 죽은 수는 이 효과의 스택(전투마다 0부터). 소모(조립 라인)는 죽음이 아니라 세지 않는다. 골드는 그 자리에서 그 진영의 런 골드로(주식시장과 같은 길 · 동전 연출)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Death Gold")]
    public class UpgradeDeathGoldEffect : UnitEffect
    {
        [SerializeField] private int _gold = 1;

        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            TeamEffects.AddStack(unit.Team, this);
            int deaths = stacks + 1, gold = 0;
            foreach (float every in Values(unit.Team))
                if (every >= 1f && deaths % Mathf.RoundToInt(every) == 0)
                    gold += _gold;
            if (gold <= 0)
                return;
            RunState.GainBattleGold(unit.Team, gold);
            GoldDrops.Raise(unit.Team, at, gold);
        }
    }
}
