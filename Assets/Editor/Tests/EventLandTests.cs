using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace GnorpWar.Editor.Tests
{
    // 이벤트 땅(아웃게임기획 §3-1 — 산맥 · 강 띠, 자리는 지도에 고정 · 종류만 런마다) — 띠의 조건 · 방문 규칙 · 판도 밖(통일 · 연합 · 수입) · AI가 이벤트 땅을 안 침 · 런 시작 순서
    public class EventLandTests
    {
        private BattleConfig _config;
        private WorldGraph _map;
        private WorldRules _rules;

        [OneTimeSetUp]
        public void Load()
        {
            _config = AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");
            Assert.NotNull(_config, "BattleConfig");
            _map = _config.World.Map.ToGraph();
            _rules = _config.World.ToRules();
            Assert.AreEqual(40, _map.RegionCount, "지도 40칸 = 영주 땅 30 + 산맥 · 강 띠 10");
            Assert.AreEqual(30, _map.LordRegionCount);
        }

        // 띠의 조건(굽기 검사와 같은 것을 에셋으로 한 번 더) — 띠마다 영주 땅 셋 이상과 맞닿음 · 띠끼리 안 맞닿음(칸으로) · 영주 땅 30이 하나로 이어짐 · 영주 땅마다 영주 이웃 둘 이상 · 길은 영주 땅끼리만
        [Test]
        public void FixedRidgesMeetRules()
        {
            int start = System.Array.FindIndex(_map.IsEvent, e => !e);
            var seen = new HashSet<int> { start };
            var queue = new Queue<int>(seen);
            while (queue.Count > 0)
                foreach (int next in _map.Links[queue.Dequeue()])
                {
                    Assert.IsFalse(_map.IsEvent[next], "영주 땅의 길이 이벤트 땅으로 이어짐");
                    if (seen.Add(next))
                        queue.Enqueue(next);
                }
            Assert.AreEqual(30, seen.Count, "영주 땅이 하나로 이어진다");
            for (int r = 0; r < _map.RegionCount; r++)
                if (_map.IsEvent[r])
                {
                    Assert.AreEqual(0, _map.Links[r].Length, "이벤트 땅에는 길이 없다");
                    Assert.GreaterOrEqual(_map.EventBorders[r].Length, 3, $"띠 {r + 1} — 영주 땅 셋 이상과 맞닿음");
                }
                else
                {
                    Assert.AreEqual(0, _map.EventBorders[r].Length);
                    Assert.GreaterOrEqual(_map.Links[r].Length, 2, $"영주 땅 {r + 1} — 영주 이웃 둘 이상");
                }
            // 띠끼리 — 그림에서 서로 다른 띠가 4방향으로 맞닿은 칸이 없다
            int[] cells = _config.World.Map.ReadCells(out int width, out int height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int a = cells[y * width + x];
                    if (a < 0 || !_map.IsEvent[a])
                        continue;
                    foreach (int b in new[] { x + 1 < width ? cells[y * width + x + 1] : -1, y + 1 < height ? cells[(y + 1) * width + x] : -1 })
                        Assert.IsFalse(b >= 0 && b != a && _map.IsEvent[b], $"띠 {a + 1}와 {b + 1}이 맞닿음");
                }
        }


        // 종류 — 10칸 · 10종(10/28)이면 한 판에 종류마다 꼭 한 곳
        [Test]
        public void KindsDealEachOnce()
        {
            int count = System.Enum.GetValues(typeof(EventLandKind)).Length;
            Assert.AreEqual(10, count);
            for (int seed = 0; seed < 50; seed++)
            {
                var kinds = new HashSet<EventLandKind>(EventLands.Deal(10, new System.Random(seed)));
                Assert.AreEqual(count, kinds.Count);
            }
        }

        // 방문 — 국경을 맞댄 · 안 닫힌 이벤트 땅만, 한 번 쓰면 닫힘, 상한 없음. 목숨 · 판도는 그대로
        [Test]
        public void VisitRules()
        {
            WorldState state = NewState(11, out _);
            int lives = state.Lives;
            int visits = 0;
            // 내 땅을 하나씩 늘려 가며(치트처럼) 열린 이벤트 땅을 전부 방문 — 상한 없이 10번까지 된다
            for (int guard = 0; guard < 100 && visits < 10; guard++)
            {
                List<int> open = state.VisitableEvents(state.Player);
                foreach (int e in open)
                {
                    Assert.IsTrue(System.Array.Exists(state.Graph.EventBorders[e], land => state.Owner[land] == state.Player.Id), "국경을 맞댄 땅만");
                    int[] owners = (int[])state.Owner.Clone();
                    WorldTurn.Visit(state, e);
                    visits++;
                    Assert.IsTrue(state.EventClosed[e]);
                    Assert.AreEqual(WorldState.NoOwner, state.Owner[e], "방문한 이벤트 땅도 내 땅이 되지 않는다");
                    CollectionAssert.AreEqual(owners, state.Owner, "방문은 판도를 안 바꾼다");
                    Assert.Throws<System.ArgumentException>(() => WorldTurn.Visit(state, e), "닫힌 땅은 다시 못 씀");
                }
                // 국경 밖의 열린 이벤트 땅은 못 간다
                for (int r = 0; r < state.Owner.Length; r++)
                    if (state.Graph.IsEvent[r] && !state.EventClosed[r])
                    {
                        int region = r;
                        Assert.Throws<System.ArgumentException>(() => WorldTurn.Visit(state, region));
                    }
                // 이웃 땅 하나를 내 것으로
                foreach (int land in Lands(state, state.Player))
                {
                    int k = System.Array.FindIndex(state.Graph.Links[land], n => state.Owner[n] != state.Player.Id);
                    if (k >= 0)
                    {
                        state.Owner[state.Graph.Links[land][k]] = state.Player.Id;
                        break;
                    }
                }
            }
            Assert.AreEqual(10, visits, "방문 상한 없음 — 열린 이벤트 땅 10칸을 다 쓴다");
            Assert.AreEqual(lives, state.Lives, "방문은 목숨을 안 바꾼다");
            Assert.Throws<System.ArgumentException>(() => WorldTurn.Visit(state, System.Array.FindIndex(state.Graph.IsEvent, e => !e)), "영주 땅은 방문할 수 없다");
        }

        // 판도 밖 — 이벤트 땅은 주인이 없고, 통일 = 영주 땅 30을 다 먹음 · 땅 수(수입) · 연합 조건에 안 센다
        [Test]
        public void EventLandsOutsideTheMap()
        {
            WorldState state = NewState(5, out _);
            Assert.AreEqual(30, state.Lords.Count, "영주 = 영주 땅 수");
            for (int r = 0; r < state.Owner.Length; r++)
                if (state.Graph.IsEvent[r])
                {
                    Assert.AreEqual(WorldState.NoOwner, state.Owner[r]);
                    Assert.IsNull(state.OwnerOf(r));
                }
            // 연합 — 내 땅 15개(이벤트 땅은 안 셈)
            List<int> lords = new List<int>();
            for (int r = 0; r < state.Owner.Length; r++)
                if (!state.Graph.IsEvent[r])
                    lords.Add(r);
            foreach (int r in lords.GetRange(0, 15))
                state.Owner[r] = state.Player.Id;
            foreach (Lord lord in state.Lords)
                if (!lord.IsPlayer && state.LandsOf(lord) == 0)
                    lord.Alive = false;
            Assert.AreEqual(15, state.LandsOf(state.Player));
            List<WorldState.Coalition> formed = WorldTurn.FormCoalitions(state, lord => 1f + lord.Id, lord => false);
            Assert.AreEqual(1, formed.Count, "내 땅 15개 = 연합 조건(이벤트 땅 없이)");
            Lord coalition = formed[0].Lord;
            Assert.AreEqual(15, state.LandsOf(coalition), "연합의 땅 = 남은 영주 땅 — 이벤트 땅은 안 들어간다");
            for (int r = 0; r < state.Owner.Length; r++)
                if (state.Graph.IsEvent[r])
                    Assert.AreEqual(WorldState.NoOwner, state.Owner[r]);
            Assert.IsFalse(state.Unified);
            foreach (int r in lords)
                state.Owner[r] = state.Player.Id;
            Assert.AreEqual(30, state.LandsOf(state.Player), "수입이 읽는 땅 수 = 영주 땅만");
            Assert.IsTrue(state.Unified, "통일 = 영주 땅 30을 다 먹음");
        }

        // AI는 이벤트 땅을 안 친다 — 수십 턴을 돌려도 이벤트 땅의 주인 · 닫힘이 그대로, 침략한 땅은 늘 영주 땅
        [Test]
        public void AiIgnoresEventLands()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                WorldState state = NewState(seed, out System.Random random);
                for (int turn = 0; turn < 30 && state.AiLords > 1; turn++)
                {
                    foreach (Lord lord in WorldTurn.AiOrder(state, random))
                    {
                        Lord target = WorldTurn.AiInvade(state, lord, l => 1f + l.Id % 5, random, out int region, out _);
                        if (target != null)
                            Assert.IsFalse(state.Graph.IsEvent[region], "AI가 이벤트 땅을 침략");
                        foreach (Lord other in state.Neighbors(lord))
                            Assert.NotNull(other);
                    }
                    WorldTurn.Settle(state, random);
                    for (int r = 0; r < state.Owner.Length; r++)
                        if (state.Graph.IsEvent[r])
                        {
                            Assert.AreEqual(WorldState.NoOwner, state.Owner[r]);
                            Assert.IsFalse(state.EventClosed[r], "AI는 방문하지 않는다");
                        }
                }
            }
        }

        // 런 시작 — 실제 설정으로: 이벤트 땅 10 · 나는 영주 땅 · 지역 특수는 영주 땅에만(시작 땅과 그 이웃 제외) · 자리는 런마다 같고 종류만 다름
        [Test]
        public void WorldRunStartsOnLordLands()
        {
            RunState.StartNew(_config);   // 영주의 상점이 런 설정을 읽는다
            var layouts = new HashSet<string>();
            var kinds = new HashSet<string>();
            for (int seed = 0; seed < 5; seed++)
            {
                var run = new WorldRun(_config.World, _config, new System.Random(seed));
                WorldState state = run.State;
                Assert.AreEqual(10, RunEvents(state).Count);
                int home = System.Array.IndexOf(state.Owner, state.Player.Id);
                Assert.IsFalse(state.Graph.IsEvent[home], "시작 땅은 영주 땅");
                for (int r = 0; r < state.Owner.Length; r++)
                    if (run.RegionSpecial[r] != null)
                    {
                        Assert.IsFalse(state.Graph.IsEvent[r], "지역 특수는 영주 땅에만");
                        Assert.AreNotEqual(home, r);
                        Assert.IsFalse(System.Array.IndexOf(state.Graph.Links[home], r) >= 0, "시작 땅의 이웃에는 안 놓는다");
                    }
                layouts.Add(string.Join(",", RunEvents(state)));
                kinds.Add(string.Join(",", RunEvents(state).ConvertAll(e => state.EventKind[e])));
            }
            Assert.AreEqual(1, layouts.Count, "이벤트 땅 자리는 지도에 고정 — 런마다 같다");
            Assert.Greater(kinds.Count, 1, "종류는 런마다 섞인다");
        }

        private WorldState NewState(int seed, out System.Random random)
        {
            random = new System.Random(seed);
            WorldGraph graph = _map;
            int home = System.Array.FindIndex(graph.IsEvent, e => !e);
            return new WorldState(graph, _rules, home, random);
        }

        private static List<int> Lands(WorldState state, Lord lord)
        {
            var lands = new List<int>();
            for (int r = 0; r < state.Owner.Length; r++)
                if (state.Owner[r] == lord.Id)
                    lands.Add(r);
            return lands;
        }

        private static List<int> RunEvents(WorldState state)
        {
            var events = new List<int>();
            for (int r = 0; r < state.Owner.Length; r++)
                if (state.Graph.IsEvent[r])
                    events.Add(r);
            return events;
        }
    }
}
