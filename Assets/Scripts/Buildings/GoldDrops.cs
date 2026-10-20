using UnityEngine;

namespace GnorpWar
{
    // 전투 중에 얻은 골드의 알림 한 곳 — 어느 진영이 어디서 얼마를 얻었나(주식시장). 골드는 이미 들어간 뒤다(RunState.GainBattleGold).
    // 게임 규칙은 건드리지 않는다 — 듣는 쪽(동전 연출 Fx/GoldCoinFx)이 없으면 아무 일도 없다
    public static class GoldDrops
    {
        public static event System.Action<Team, Vector2, int> Dropped;

        public static void Raise(Team team, Vector2 at, int amount) => Dropped?.Invoke(team, at, amount);
    }
}
