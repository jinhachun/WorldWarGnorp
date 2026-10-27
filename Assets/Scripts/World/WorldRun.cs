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

        // 영주의 상점(RunState.ShopAs)은 런이 시작돼 있어야 돈다 — RunState.StartNew가 이것을 만든다.
        // 순서(아웃게임기획 §3-1): 지도의 영주 땅으로 규칙 그래프(이벤트 땅 = 지도에 고정된 산맥 · 강 띠 — 종류만 WorldState가 섞는다) · 영주들 → 내 시작 땅(영주 땅 중 무작위) → 지역 특수(영주 땅에만)
        public WorldRun(WorldConfig world, BattleConfig battle, System.Random random)
        {
            _world = world;
            _battle = battle;
            _random = random;
            WorldRules rules = world.ToRules();
            WorldGraph graph = world.Map.ToGraph();
            var lordRegions = new List<int>();
            for (int region = 0; region < graph.RegionCount; region++)
                if (!graph.IsEvent[region])
                    lordRegions.Add(region);
            int playerRegion = lordRegions[random.Next(lordRegions.Count)];
            State = new WorldState(graph, rules, playerRegion, random);
            _regionDistance = Distances(graph, playerRegion);
            foreach (Lord lord in State.Lords)
                if (!lord.IsPlayer)
                {
                    _towers[lord.Id] = NewTower();
                    MeasureDistance(lord);
                }
            _ghostCap = Mathf.FloorToInt(State.AiLords * world.GhostShare);

            // 지역 특수 업그레이드 — 전체에서 무작위로 뽑아 지역마다 하나씩(겹침 없음), 내 시작 땅과 그 이웃 땅은 뺀다(사용자 10/15). 남은 것은 이번 런에 나오지 않는다(시작 때 고르기는 없앴다 — 사용자 10/15)
            RegionSpecial = new UpgradeDefinition[State.Owner.Length];
            UnplacedSpecials.AddRange(battle.SpecialUpgrades);
            var lands = new List<int>();
            foreach (int region in lordRegions)
                if (region != playerRegion && System.Array.IndexOf(State.Graph.Links[playerRegion], region) < 0)
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
                if (RegionSpecial[region] != null && State.Owner[region] == lord.Id && !tower.Upgrades.Exists(gain => gain.Definition == RegionSpecial[region]))
                    tower.LandSpecials.Add(RegionSpecial[region]);   // 이미 얻은 신화(유령 영주가 자기 런에서 고른 것)와 같으면 한 번만 친다
        }

        // ── 유령 영주(아웃게임기획 §9-0 · ROADMAP 2-4 3번) — 영주 한 명 = 다른 런 기록 하나(Run/RunRecord). 내 턴 N에 그 영주의 탑 = 그 기록의 턴 N 스냅샷(일찍 끝났으면 마지막).
        // 유령은 상점을 돌지 않는다(골드를 안 씀 · 업그레이드는 기록에 든 그대로) — 턴이 열릴 때(ShopPhase) 탑을 갈아 끼운다. 땅 신화는 AI처럼 쥔 동안(SyncSpecials).
        // 영주끼리의 전쟁 · 행복도 · 반란 · 연합은 AI와 같다. 기록 하나는 한 런에서 한 영주에만 — 모자라면 나머지는 AI
        public class Ghost
        {
            public RunRecord.Loaded Record;
            // 지금 세운 스냅샷의 턴 · 목숨(국가주의가 읽는다 — RunState.LivesFor)
            public int Turn;
            public int Lives;
            public BuildingNation Nation;
        }

        private readonly Dictionary<int, Ghost> _ghosts = new Dictionary<int, Ghost>();
        // 아직 영주에 안 붙은 기록(오래 버틴 것부터) — 반란으로 선 새 영주가 받는다
        private readonly List<RunRecord.Loaded> _ghostPool = new List<RunRecord.Loaded>();

        public Ghost GhostOf(Lord lord) => _ghosts.TryGetValue(lord.Id, out Ghost ghost) ? ghost : null;
        public int GhostCount => _ghosts.Count;

        // 영주의 국가 — 유령이면 그 기록의 국가, AI면 빌드 방향의 국가(나는 RunState.Nation)
        public BuildingNation NationOf(Lord lord) => GhostOf(lord) is Ghost ghost ? ghost.Nation : _world.Map.BuildOf(lord.Build).Nation;

        // 동시에 앉을 수 있는 유령 수 = 런 시작 때 상대 영주 수 × WorldConfig.GhostShare(내림) — 절반의 진영은 AI(사용자 10/27)
        private readonly int _ghostCap;

        // 런을 시작할 때 — records(오래 버틴 것부터)를 살아 있는 AI 영주에게 무작위 자리로 하나씩, 상한까지 붙인다. 남은 기록은 반란 영주 몫
        public void AddGhosts(List<RunRecord.Loaded> records)
        {
            _ghostPool.AddRange(records);
            var lords = new List<Lord>();
            foreach (Lord lord in State.Lords)
                if (lord.Alive && !lord.IsPlayer && !_ghosts.ContainsKey(lord.Id))
                    lords.Add(lord);
            while (lords.Count > 0 && _ghostPool.Count > 0 && _ghosts.Count < _ghostCap)
            {
                int pick = _random.Next(lords.Count);
                Haunt(lords[pick]);
                lords.RemoveAt(pick);
            }
        }

        // 이번 런에 유령으로 쓴 사람(기록의 닉네임) — 같은 사람은 한 판에 한 번만(사용자 10/28). 닉네임이 빈 기록은 누구인지 몰라 따로 센다
        private readonly HashSet<string> _hauntedNames = new HashSet<string>();

        // lord에 남은 기록 중 맨 앞(가장 오래 버틴 것)을 붙인다 — 이번 런에 이미 쓴 사람의 기록은 건너뛴다. 유령이 상한에 차 있거나 쓸 기록이 없으면 붙이지 않는다(AI)
        private void Haunt(Lord lord)
        {
            if (_ghosts.Count >= _ghostCap)
                return;
            int pick = _ghostPool.FindIndex(record => string.IsNullOrEmpty(record.Nickname) || !_hauntedNames.Contains(record.Nickname));
            if (pick < 0)
                return;
            RunRecord.Loaded chosen = _ghostPool[pick];
            _ghosts[lord.Id] = new Ghost { Record = chosen, Nation = chosen.Nation };
            _ghostPool.RemoveAt(pick);
            if (!string.IsNullOrEmpty(chosen.Nickname))
                _hauntedNames.Add(chosen.Nickname);
        }

        // 유령의 탑을 이번 턴(State.Turn)의 스냅샷으로 — 유령이면 true. 세우지 못하면 경고 로그를 남기고 그 영주는 지금 탑 그대로 AI가 된다(false)
        private bool Possess(Lord lord)
        {
            if (!(GhostOf(lord) is Ghost ghost))
                return false;
            try
            {
                TowerSnapshot snapshot = TowerSnapshot.FromJson(ghost.Record.SnapshotAt(State.Turn), Team.Enemy, _battle.AssetIds, _battle);
                _towers[lord.Id] = snapshot.Tower;
                ghost.Turn = snapshot.Turn;
                ghost.Lives = snapshot.Lives;
                SyncSpecials(lord);
                return true;
            }
            catch (TowerSnapshot.InvalidException e)
            {
                Debug.LogWarning($"[Ghost] 영주 {lord.Id}의 기록 「{ghost.Record.RunId}」를 못 세움 — AI로 · {e.Message}");
                _ghosts.Remove(lord.Id);
                return false;
            }
        }

        public WorldMapDefinition Map => _world.Map;
        public Color ColorOf(Lord lord) => _world.LordColor(lord.Id);

        public TowerState TowerOf(Lord lord) => _towers.TryGetValue(lord.Id, out TowerState tower) ? tower : null;

        public float PowerOf(Lord lord) => TowerPower.Of(_towers[lord.Id].Field, _battle, _world);

        // 턴 수입 = 턴마다 얻는 골드(RunState.TurnGold — 기본 + 땅 + 곡창, 중상주의면 ×N) × 거리 배율(반올림) + 약자 보정(가장 큰 영주보다 땅이 적은 만큼), 직전 침략을 이겼으면 승리 보너스.
        // 거리 배율(사용자 10/27) = WorldConfig.DistanceGoldScale(시작 땅의 거리, 턴) — 내 가까운 영주는 덜, 먼 영주는 턴이 갈수록 더 번다. 약자 보정 · 승리 보너스는 고정값이라 안 곱한다
        public int IncomeOf(Lord lord)
            => Mathf.RoundToInt(RunState.TurnGold(_towers[lord.Id], State.LandsOf(lord)) * _world.DistanceGoldScale(DistanceOf(lord), State.Turn))
               + WorldTurn.UnderdogGold(State, lord) + (lord.WonLastInvasion ? _battle.WinBonus : 0);

        // [지역] = 내 시작 땅에서 건너는 영주 땅 수(규칙 그래프의 최단 거리 — 이벤트 땅은 길이 아니다). 런 시작 때 한 번
        private readonly int[] _regionDistance;
        // [영주 번호] = 그 영주가 선 땅(런 시작 · 반란)의 거리 — 땅을 먹거나 잃어도 그대로
        private readonly Dictionary<int, int> _lordDistance = new Dictionary<int, int>();

        public int DistanceOf(Lord lord) => _lordDistance.TryGetValue(lord.Id, out int distance) ? distance : 0;

        // 영주가 막 섰을 때(땅 하나) 그 땅의 거리를 적는다
        private void MeasureDistance(Lord lord) => _lordDistance[lord.Id] = _regionDistance[System.Array.IndexOf(State.Owner, lord.Id)];

        // from에서 영주 땅끼리의 길로 건너는 수 — 닿지 않는 땅(지도가 끊긴 경우)은 닿는 땅 중 가장 먼 거리로 친다
        private static int[] Distances(WorldGraph graph, int from)
        {
            var distance = new int[graph.RegionCount];
            for (int i = 0; i < distance.Length; i++)
                distance[i] = -1;
            distance[from] = 0;
            var queue = new Queue<int>();
            queue.Enqueue(from);
            int farthest = 0;
            while (queue.Count > 0)
            {
                int region = queue.Dequeue();
                farthest = distance[region];
                foreach (int next in graph.Links[region])
                    if (distance[next] < 0)
                    {
                        distance[next] = distance[region] + 1;
                        queue.Enqueue(next);
                    }
            }
            for (int i = 0; i < distance.Length; i++)
                if (distance[i] < 0)
                    distance[i] = farthest;
            return distance;
        }

        // ① 살아 있는 AI 영주 전원이 상점을 한 번씩 — 유령 영주는 상점 대신 이번 턴의 스냅샷으로
        public void ShopPhase()
        {
            var haunted = new List<string>();
            foreach (Lord lord in State.Lords)
                if (lord.Alive && !lord.IsPlayer)
                {
                    if (Possess(lord))
                        haunted.Add($"{lord.Id}번({GhostOf(lord).Record.RunId} 턴 {GhostOf(lord).Turn}/{GhostOf(lord).Record.LastTurn})");
                    else
                        Shop(lord);
                }
            if (haunted.Count > 0)
                Debug.Log($"[Ghost] 턴 {State.Turn} — 유령 영주 {haunted.Count}명: {string.Join(" · ", haunted)}");
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
                {
                    _towers.Remove(lord.Id);
                    _ghosts.Remove(lord.Id);   // 그 기록은 이번 런에 다시 쓰지 않는다
                }
            foreach (Lord lord in created)
            {
                _towers[lord.Id] = NewTower();
                MeasureDistance(lord);
                Haunt(lord);   // 남은 기록이 있고 유령이 상한 아래면 유령, 아니면 AI
                if (Possess(lord))
                    Debug.Log($"[Ghost] 반란으로 선 영주 {lord.Id}번 = 기록 {GhostOf(lord).Record.RunId}(턴 {GhostOf(lord).Turn})");
                else
                    for (int i = 0; i < _world.NewLordShops; i++)
                        Shop(lord);
            }
        }

        // 플레이어가 고른 국가(RunState.ChooseNation) — 상점 진열의 국가 비율이 읽는다. AI 영주의 국가는 빌드 방향의 것
        public BuildingNation PlayerNation;

        // 연합(기획 §6-1 · 사용자 10/27) — ④ 정산 뒤에 부른다. 뭉친 연합들을 돌려준다(안 뭉쳤으면 빈 목록 — WorldTurn.FormCoalitions).
        // 유령 연합: 탑 = 그 유령의 그 턴 스냅샷 그대로(턴마다 갈아 끼운다). 흡수한 영주의 탑 · 골드는 버린다.
        // 유령이 없을 때의 연합(하나): 탑 = 바탕 영주의 탑 + 나머지 영주들의 남은 골드와 탑 · 보관함을 판 값(지금 판매 규칙 — RunState.SellValue) × CoalitionGoldScale로 본 상점.
        // 상점은 합쳐진 영주 수만큼 돈다. 이 탑이 연합전 내내 그대로다 — 땅을 잃어도 줄지 않고, 턴마다 남은 땅의 수입으로 상점을 돈다(ShopPhase)
        public List<WorldState.Coalition> FormCoalitions()
        {
            List<WorldState.Coalition> formed = WorldTurn.FormCoalitions(State, PowerOf, lord => _ghosts.ContainsKey(lord.Id));
            foreach (WorldState.Coalition coalition in formed)
            {
                var merged = new List<Lord>(System.Array.ConvertAll(coalition.Members, id => State.Lords[id]));
                if (coalition.Ghost)
                    foreach (Lord lord in merged)
                    {
                        _towers.Remove(lord.Id);
                        _ghosts.Remove(lord.Id);
                    }
                else
                    Merge(coalition.Lord, merged);
            }
            return formed;
        }

        private void Merge(Lord leader, List<Lord> merged)
        {
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
                _ghosts.Remove(lord.Id);
            }
            gold += Mathf.RoundToInt(sold * _world.CoalitionGoldScale);
            // 모은 골드는 상점마다 고르게 나눠 받는다(나머지는 첫 상점에) — 못 쓴 골드는 탑에 남아 다음 상점으로 넘어간다
            for (int i = 0; i < merged.Count; i++)
                Shop(leader, gold / merged.Count + (i == 0 ? gold % merged.Count : 0));
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
