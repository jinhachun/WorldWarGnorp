using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 월드맵 런 한 판 — 판도(WorldState)와 AI 영주마다의 탑(TowerState)을 들고 턴을 돌린다:
    // ① ShopPhase(영주 전원이 자기 수입으로 상점 한 번) → 플레이어의 침략(바깥 — WorldTurn.Invade) → ③ AiPhase → ④ Settle → 다음 턴.
    // 영주의 상점은 지금의 적 상점 AI 그대로다(RunState.ShopAs → Run/EnemyShopper). AI끼리의 승패는 탑 전투력(TowerPower)의 주사위.
    // 런 한 판에 하나(RunState.World). 플레이어의 탑 · 전투 · 화면은 여기 없다 — 플레이어의 침략 결과는 RunState가 WorldTurn.Invade로 넣는다
    public class WorldRun
    {
        public readonly WorldState State;

        private readonly WorldConfig _world;
        private readonly BattleConfig _battle;
        private readonly System.Random _random;
        // [영주 번호] = 그 AI 영주의 탑 — 먹히거나 멸망하면 사라진다
        private readonly Dictionary<int, TowerState> _towers = new Dictionary<int, TowerState>();

        // 영주의 상점(RunState.ShopAs)은 런이 시작돼 있어야 돈다 — RunState.StartNew가 이것을 만든다
        public WorldRun(WorldConfig world, BattleConfig battle, int playerRegion, System.Random random)
        {
            _world = world;
            _battle = battle;
            _random = random;
            State = new WorldState(world.Map.ToGraph(), world.ToRules(), playerRegion, random);
            foreach (Lord lord in State.Lords)
                if (!lord.IsPlayer)
                    _towers[lord.Id] = NewTower();
        }

        public WorldMapDefinition Map => _world.Map;
        public Color ColorOf(Lord lord) => _world.LordColor(lord.Id);

        public TowerState TowerOf(Lord lord) => _towers.TryGetValue(lord.Id, out TowerState tower) ? tower : null;

        public float PowerOf(Lord lord) => TowerPower.Of(_towers[lord.Id].Field, _battle, _world);

        // 턴 수입 = 1라운드 상점 골드 + 땅 수 × 땅당 골드, 직전 침략을 이겼으면 승리 보너스
        public int IncomeOf(Lord lord)
            => _battle.ShopGold + State.LandsOf(lord) * _world.GoldPerLand + (lord.WonLastInvasion ? _battle.WinBonus : 0);

        // ① 살아 있는 AI 영주 전원이 상점을 한 번씩
        public void ShopPhase()
        {
            foreach (Lord lord in State.Lords)
                if (lord.Alive && !lord.IsPlayer)
                    Shop(lord);
        }

        // ③ AI 침략 — 한 번의 침략마다 report(침략한 영주, 대상, 이겼나)
        public void AiPhase(System.Action<Lord, Lord, bool> report = null)
        {
            foreach (Lord lord in WorldTurn.AiOrder(State, _random))
            {
                Lord target = WorldTurn.AiInvade(State, lord, PowerOf, _random, out bool won);
                if (target != null)
                    report?.Invoke(lord, target, won);
            }
        }

        // ④ 정산 — 사라진 영주의 탑을 치우고, 반란으로 들어선 새 영주에게 시작 탑과 첫 상점을 준다
        public void Settle()
        {
            List<Lord> created = WorldTurn.Settle(State, _random);
            foreach (Lord lord in State.Lords)
                if (!lord.Alive)
                    _towers.Remove(lord.Id);
            foreach (Lord lord in created)
            {
                _towers[lord.Id] = NewTower();
                for (int i = 0; i < _world.NewLordShops; i++)
                    Shop(lord);
            }
        }

        private TowerState NewTower()
        {
            var tower = new TowerState(Team.Enemy);
            tower.Reset(_battle);
            return tower;
        }

        private void Shop(Lord lord)
        {
            int tier = System.Math.Min(State.Turn - 1, _world.ShopTierMax);
            // 영주가 수십 명이라 상점 한 번마다의 로그는 끈다
            bool quiet = EnemyShopper.Quiet;
            EnemyShopper.Quiet = true;
            try
            {
                RunState.ShopAs(_towers[lord.Id], _world.Map.BuildOf(lord.Build), IncomeOf(lord), tier);
            }
            finally
            {
                EnemyShopper.Quiet = quiet;
            }
            lord.WonLastInvasion = false;   // 승리 보너스는 이긴 다음 상점에 한 번
        }
    }
}
