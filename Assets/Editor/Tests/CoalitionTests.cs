using System.Collections.Generic;
using NUnit.Framework;

namespace GnorpWar.Editor.Tests
{
    // 연합(WorldTurn.FormCoalitions · 사용자 10/27) — 유령마다 연합 하나 · 남은 AI는 가장 가까운 유령에게(같으면 땅이 많은 쪽) · 땅 수별 이길 횟수 · 유령이 없으면 옛 규칙
    public class CoalitionTests
    {
        // 땅 0~7이 한 줄로 이어진 지도 — 0번이 나
        private static WorldState LineState()
        {
            const int count = 8;
            var links = new int[count][];
            for (int i = 0; i < count; i++)
                links[i] = i == 0 ? new[] { 1 } : i == count - 1 ? new[] { i - 1 } : new[] { i - 1, i + 1 };
            var builds = new int[count][];
            for (int i = 0; i < count; i++)
                builds[i] = new[] { 0 };
            var rules = new WorldRules { CoalitionLands = 1 };
            return new WorldState(new WorldGraph(links, new int[count], builds), rules, 0, new System.Random(3));
        }

        [Test]
        public void TwoGhostsMakeTwoCoalitionsNearestFirst()
        {
            WorldState state = LineState();
            // 유령 = 땅 2 · 땅 6의 영주. 6번 유령은 땅 7도 가진다(땅 2개)
            state.Owner[7] = 6;
            state.Lords[7].Alive = false;
            List<WorldState.Coalition> formed = WorldTurn.FormCoalitions(state, lord => 1f, lord => lord.Id == 2 || lord.Id == 6);
            Assert.AreEqual(2, formed.Count, "유령 2명 = 연합 2개");
            WorldState.Coalition left = state.CoalitionOf(state.Lords[2]), right = state.CoalitionOf(state.Lords[6]);
            Assert.NotNull(left);
            Assert.NotNull(right);
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, left.Members, "가까운 유령에게");
            CollectionAssert.AreEquivalent(new[] { 4, 5 }, right.Members, "4번은 두 유령에게 거리 2로 같다 — 땅이 많은 6번에게");
            Assert.AreEqual(3, state.LandsOf(left.Lord));
            Assert.AreEqual(4, state.LandsOf(right.Lord));
            Assert.AreEqual(1, left.WinsNeeded, "땅 3개 = 1번");
            Assert.AreEqual(2, right.WinsNeeded, "땅 4개 = 2번");
            Assert.AreEqual(2, state.AiLords, "남은 AI 영주 = 연합들뿐");
            Assert.IsEmpty(WorldTurn.FormCoalitions(state, lord => 1f, lord => true), "런에 한 번");

            // 연합마다의 몫 — 4개 · 2번이면 2 · 나머지
            List<int> taken = WorldTurn.Invade(state, state.Player, 4, true);
            Assert.AreEqual(2, taken.Count);
            Assert.IsTrue(right.Lord.Alive);
            Assert.AreEqual(1, state.CoalitionWinsLeft(right));
            WorldTurn.Invade(state, state.Player, System.Array.IndexOf(state.Owner, right.Lord.Id), true);
            Assert.IsFalse(right.Lord.Alive, "마지막 승리 = 멸망");
            Assert.AreEqual(0, left.Wins, "다른 연합의 승리 수는 그대로");
        }

        // 연합은 최대 3개(사용자 10/28) — 유령 5명이면 땅이 많은 유령(같으면 전투력 · 번호 순) 3명이 바탕, 남은 유령 2명은 가까운 연합에 흡수
        [Test]
        public void AtMostThreeCoalitions()
        {
            WorldState state = LineState();
            // 유령 = 2 · 3 · 4 · 6 · 7. 4번은 땅 5도 가진다(땅 2개)
            state.Owner[5] = 4;
            state.Lords[5].Alive = false;
            var ghosts = new HashSet<int> { 2, 3, 4, 6, 7 };
            List<WorldState.Coalition> formed = WorldTurn.FormCoalitions(state, lord => lord.Id == 3 ? 2f : 1f, lord => ghosts.Contains(lord.Id));
            Assert.AreEqual(3, formed.Count, "유령 5명이어도 연합 3개");
            // 바탕: 땅 2개인 4번 → 땅 1개 중 전투력이 센 3번 → 번호가 앞선 2번
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, formed.ConvertAll(c => c.Lord.Id));
            CollectionAssert.AreEquivalent(new[] { 1 }, state.CoalitionOf(state.Lords[2]).Members);
            CollectionAssert.IsEmpty(state.CoalitionOf(state.Lords[3]).Members);
            CollectionAssert.AreEquivalent(new[] { 6, 7 }, state.CoalitionOf(state.Lords[4]).Members, "남은 유령 6 · 7은 가까운 4번 연합에");
            Assert.IsFalse(state.Lords[6].Alive);
            Assert.IsFalse(state.Lords[7].Alive);
            Assert.AreEqual(4, state.LandsOf(state.Lords[4]));
        }

        [Test]
        public void GhostCoalitionWinsByLands()
        {
            var rules = new WorldRules();
            Assert.AreEqual(1, rules.GhostCoalitionWins(1));
            Assert.AreEqual(1, rules.GhostCoalitionWins(3));
            Assert.AreEqual(2, rules.GhostCoalitionWins(4));
            Assert.AreEqual(2, rules.GhostCoalitionWins(7));
            Assert.AreEqual(3, rules.GhostCoalitionWins(8));
        }

        // 유령이 없으면 옛 규칙 — 가장 센 영주 아래 하나로, CoalitionWins번
        [Test]
        public void NoGhostsMakeOneCoalition()
        {
            WorldState state = LineState();
            List<WorldState.Coalition> formed = WorldTurn.FormCoalitions(state, lord => lord.Id == 5 ? 9f : 1f, lord => false);
            Assert.AreEqual(1, formed.Count);
            Assert.AreEqual(5, formed[0].Lord.Id, "가장 센 영주가 바탕");
            Assert.AreEqual(state.Rules.CoalitionWins, formed[0].WinsNeeded);
            Assert.AreEqual(7, state.LandsOf(formed[0].Lord));
            Assert.IsFalse(formed[0].Ghost);
        }

        // 연합은 침략하지 않는다 — 여럿이어도 서로 안 싸운다
        [Test]
        public void CoalitionsDoNotInvade()
        {
            WorldState state = LineState();
            foreach (Lord lord in state.Lords)
                lord.Temper = LordTemper.Raider;
            WorldTurn.FormCoalitions(state, lord => 1f, lord => lord.Id == 2 || lord.Id == 6);
            Assert.IsNull(WorldTurn.PickTarget(state, state.Lords[2], lord => 1f, new System.Random(1)));
        }
    }
}
