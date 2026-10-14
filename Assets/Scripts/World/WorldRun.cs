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
        // [영주 번호] = 그 AI 영주의 탑 — 땅을 다 잃거나 멸망하면 사라진다
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

            // 지역 특수 업그레이드 — 전체에서 무작위로 뽑아 지역마다 하나씩(겹침 없음), 내 시작 땅은 뺀다. 남은 것은 이번 런에 나오지 않는다(시작 때 고르기는 없앴다 — 사용자 10/15)
            RegionSpecial = new UpgradeDefinition[State.Owner.Length];
            UnplacedSpecials.AddRange(battle.SpecialUpgrades);
            var lands = new List<int>();
            for (int region = 0; region < RegionSpecial.Length; region++)
                if (region != playerRegion)
                    lands.Add(region);
            for (int i = 0; i < battle.SpecialRegions && UnplacedSpecials.Count > 0 && lands.Count > 0; i++)
            {
                int pick = random.Next(UnplacedSpecials.Count), land = random.Next(lands.Count);
                RegionSpecial[lands[land]] = UnplacedSpecials[pick];
                UnplacedSpecials.RemoveAt(pick);
                lands.RemoveAt(land);
            }
        }

        // 지역 특수 업그레이드(아웃게임기획 §4) — [지역] = 그 땅에 놓인 것(없으면 null). 런마다 새로 놓는다(지도 에셋에 굽지 않는다).
        // AI 영주는 그 땅을 가진 동안 쓴다(SyncSpecials). 내가 그 땅에서 싸워 이기면 내 것이 되고 땅에서는 사라진다(RunState.FinishBattle)
        public readonly UpgradeDefinition[] RegionSpecial;
        // 지도에 놓이지 않은 것 — 이번 런에는 나오지 않는다(놓을 때 뽑고 남은 것)
        public readonly List<UpgradeDefinition> UnplacedSpecials = new List<UpgradeDefinition>();

        // lord의 탑에 「지금 가진 땅의 지역 특수 업그레이드」를 다시 적는다 — 상점 · 전투 · 표시 앞에 부른다(땅은 턴마다 넘어간다). 탑이 없으면(나 · 사라진 영주) 아무것도 안 한다
        public void SyncSpecials(Lord lord)
        {
            TowerState tower = TowerOf(lord);
            if (tower == null)
                return;
            tower.LandSpecials.Clear();
            for (int region = 0; region < RegionSpecial.Length; region++)
                if (RegionSpecial[region] != null && State.Owner[region] == lord.Id)
                    tower.LandSpecials.Add(RegionSpecial[region]);
        }

        public WorldMapDefinition Map => _world.Map;
        public Color ColorOf(Lord lord) => _world.LordColor(lord.Id);

        public TowerState TowerOf(Lord lord) => _towers.TryGetValue(lord.Id, out TowerState tower) ? tower : null;

        public float PowerOf(Lord lord) => TowerPower.Of(_towers[lord.Id].Field, _battle, _world);

        // 턴 수입 = 턴마다 얻는 골드(RunState.TurnGold — 기본 + 땅 + 곡창, 중상주의면 ×N) + 약자 보정(가장 큰 영주보다 땅이 적은 만큼), 직전 침략을 이겼으면 승리 보너스
        public int IncomeOf(Lord lord)
            => RunState.TurnGold(_towers[lord.Id], State.LandsOf(lord)) + WorldTurn.UnderdogGold(State, lord) + (lord.WonLastInvasion ? _battle.WinBonus : 0);

        // ① 살아 있는 AI 영주 전원이 상점을 한 번씩
        public void ShopPhase()
        {
            foreach (Lord lord in State.Lords)
                if (lord.Alive && !lord.IsPlayer)
                    Shop(lord);
        }

        // ③ AI 침략 — 한 번의 침략마다 report(침략한 영주, 대상, 침략한 땅, 이겼나)
        public void AiPhase(System.Action<Lord, Lord, int, bool> report = null)
        {
            foreach (Lord lord in WorldTurn.AiOrder(State, _random))
            {
                Lord target = WorldTurn.AiInvade(State, lord, PowerOf, _random, out int region, out bool won);
                if (target == null)
                    continue;
                // 승패에 걸린 지역 특수 업그레이드(제국주의 · 계몽주의) — 이긴 탑 · 진 탑
                SyncSpecials(lord);
                SyncSpecials(target);
                RunState.ApplyBattleResult(TowerOf(won ? lord : target), TowerOf(won ? target : lord));
                report?.Invoke(lord, target, region, won);
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

        // 플레이어가 고른 국가(RunState.ChooseNation) — 상점 진열의 국가 비율이 읽는다. AI 영주의 국가는 빌드 방향의 것
        public BuildingNation PlayerNation;

        // 최후의 연합(기획 §6-1) — ④ 정산 뒤에 부른다. 뭉쳤으면 바탕 영주(탑 전투력이 가장 높은 영주)를 돌려주고 merged에 합쳐진 영주들을 담는다(아니면 null).
        // 연합의 탑 = 바탕 영주의 탑 + 나머지 영주들의 남은 골드와 탑 · 보관함을 판 값(지금 판매 규칙 — RunState.SellValue) × CoalitionGoldScale로 본 상점.
        // 상점은 합쳐진 영주 수만큼 돈다. 이 탑이 연합전 내내 그대로다 — 땅을 잃어도 줄지 않고, 턴마다 남은 땅의 수입으로 상점을 돈다(ShopPhase)
        public Lord FormCoalition(List<Lord> merged)
        {
            Lord leader = WorldTurn.FormCoalition(State, PowerOf, merged);
            if (leader == null)
                return null;
            int gold = 0, sold = 0;
            foreach (Lord lord in merged)
            {
                TowerState tower = _towers[lord.Id];
                gold += tower.Gold;
                foreach (OwnedBuilding[] row in new[] { tower.Field, tower.Storage })
                    foreach (OwnedBuilding building in row)
                        if (!OwnedBuilding.IsEmpty(building))
                            sold += RunState.SellValue(building);
                _towers.Remove(lord.Id);
            }
            gold += Mathf.RoundToInt(sold * _world.CoalitionGoldScale);
            // 모은 골드는 상점마다 고르게 나눠 받는다(나머지는 첫 상점에) — 못 쓴 골드는 탑에 남아 다음 상점으로 넘어간다
            for (int i = 0; i < merged.Count; i++)
                Shop(leader, gold / merged.Count + (i == 0 ? gold % merged.Count : 0));
            return leader;
        }

        private TowerState NewTower()
        {
            var tower = new TowerState(Team.Enemy);
            tower.Reset(_battle);
            return tower;
        }

        // 턴 상점 한 번 — 턴 수입을 받고 본다
        private void Shop(Lord lord)
        {
            SyncSpecials(lord);
            Shop(lord, IncomeOf(lord));
            lord.WonLastInvasion = false;   // 승리 보너스는 이긴 다음 상점에 한 번
        }

        private void Shop(Lord lord, int gold)
        {
            // 가진 땅의 지역 특수 업그레이드를 탑에 적고, 얻는 순간 한 번뿐인 것(절대왕정)을 처음 가진 영주면 받는다
            SyncSpecials(lord);
            RunState.GrantLandSpecials(_towers[lord.Id]);
            // 등급 단계 = 그 턴의 단계(턴 수만으로 오른다)
            int tier = _world.ShopTier(State.Turn - 1);
            // 영주가 수십 명이라 상점 한 번마다의 로그는 끈다
            bool quiet = EnemyShopper.Quiet;
            EnemyShopper.Quiet = true;
            try
            {
                RunState.ShopAs(_towers[lord.Id], _world.Map.BuildOf(lord.Build), gold, tier);
            }
            finally
            {
                EnemyShopper.Quiet = quiet;
            }
        }
    }
}
