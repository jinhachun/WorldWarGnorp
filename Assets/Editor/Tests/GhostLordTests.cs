using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GnorpWar.Editor.Tests
{
    // 유령 영주(World/WorldRun — 영주 = 다른 런 기록) — 턴마다 탑 갈아 끼우기 · 끝난 기록은 AI가 이어 둠 · 못 읽는 기록 건너뜀 · 같은 신화 한 번 · 기록이 모자라면 AI
    public class GhostLordTests
    {
        private BattleConfig _config;
        private AssetIds _ids;
        private string _folder;

        [OneTimeSetUp]
        public void Load()
        {
            _config = AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");
            _ids = AssetDatabase.LoadAssetAtPath<AssetIds>(AssetIdsMenu.Path);
            Assert.NotNull(_config, "BattleConfig");
            Assert.NotNull(_ids, "등록부");
            Assert.AreSame(_ids, _config.AssetIds, "런 설정의 등록부");
            RunState.StartNew(_config);   // AI 영주의 상점(RunState.ShopAs)이 런 설정을 읽는다. 플레이 중이 아니라 유령은 안 읽는다
        }

        [SetUp]
        public void MakeFolder() => _folder = Path.Combine(Path.GetTempPath(), "GnorpGhostTests", System.Guid.NewGuid().ToString("N"));

        [TearDown]
        public void RemoveFolder()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [Test]
        public void TowerFollowsRecordTurn()
        {
            RunRecord.Loaded record = WriteRecord(3, BuildingNation.Tribe, lives => 6 - lives);
            WorldRun run = NewRun();
            run.AddGhosts(new List<RunRecord.Loaded> { record });
            Lord ghostLord = GhostLord(run);
            Assert.NotNull(ghostLord, "기록 하나 = 유령 영주 하나");
            Assert.AreEqual(1, run.GhostCount);
            Assert.AreEqual(BuildingNation.Tribe, run.NationOf(ghostLord), "유령의 국가 = 기록의 국가");

            foreach ((int turn, int shown) in new[] { (1, 1), (2, 2), (3, 3) })
            {
                run.State.Turn = turn;
                run.ShopPhase();
                TowerState tower = run.TowerOf(ghostLord);
                Assert.AreSame(Pool(shown), tower.Field[0].Definition, $"내 턴 {turn} = 기록의 턴 {shown}");
                Assert.AreEqual(shown, run.GhostOf(ghostLord).Turn);
                Assert.AreEqual(6 - shown, run.GhostOf(ghostLord).Lives, "충성 서약이 읽는 목숨 = 스냅샷 목숨");
                Assert.AreEqual(0, tower.Gold, "유령은 상점을 돌지 않는다(골드를 받지 않는다)");
                Assert.AreEqual(Team.Enemy, tower.Team);
            }
        }

        // 기록이 턴 N에서 끝나면 N+1부터 AI가 이어 둔다 — 마지막 탑 그대로 상점을 돌고(갈아 끼우지 않음), 닉네임 · 국가는 그대로, 빌드 방향은 기록의 국가(사용자 10/30)
        [Test]
        public void RecordEndHandsOverToAi()
        {
            RunRecord.Loaded record = WriteRecord(3, BuildingNation.Tribe, null, "끝난 유령");
            WorldRun run = NewRun();
            run.AddGhosts(new List<RunRecord.Loaded> { record });
            Lord lord = GhostLord(run);
            for (run.State.Turn = 1; run.State.Turn <= 3; run.State.Turn++)
                run.ShopPhase();
            TowerState last = run.TowerOf(lord);
            Assert.AreSame(Pool(3), last.Field[0].Definition);
            int before = Built(last);

            LogAssert.Expect(LogType.Log, new Regex(@"\[Ghost\] 턴 4 — 영주 \d+번의 기록 .*턴 3에서 끝남 — AI가 이어 둔다"));
            for (run.State.Turn = 4; run.State.Turn <= 6; run.State.Turn++)
            {
                run.ShopPhase();
                Assert.AreSame(last, run.TowerOf(lord), $"턴 {run.State.Turn} — 기록의 마지막 탑을 이어받는다(갈아 끼우지 않음)");
                run.AiPhase();   // 침략은 AI와 같은 규칙 — 유령이었다고 막히지 않는다
                if (!lord.Alive)
                    break;
            }
            Assert.IsTrue(Built(last) > before || last.Gold > 0, "AI가 수입을 받아 상점을 돈다");
            WorldRun.Ghost ghost = run.GhostOf(lord);
            Assert.NotNull(ghost, "유령 표시(닉네임)는 남는다");
            Assert.IsTrue(ghost.Retired);
            Assert.AreEqual("끝난 유령", ghost.Record.Nickname);
            Assert.AreEqual(BuildingNation.Tribe, run.NationOf(lord));
            Assert.AreEqual(BuildingNation.Tribe, run.Map.BuildOf(lord.Build).Nation, "AI 빌드 방향 = 기록의 국가");
        }

        private static int Built(TowerState tower)
        {
            int count = 0;
            foreach (OwnedBuilding[] row in new[] { tower.Field, tower.Storage })
                foreach (OwnedBuilding building in row)
                    if (!OwnedBuilding.IsEmpty(building))
                        count++;
            return count;
        }

        // 기록이 영주보다 적으면 나머지는 AI — AI 영주는 상점을 돈다
        [Test]
        public void FewRecordsLeaveAiLords()
        {
            WorldRun run = NewRun();
            run.AddGhosts(new List<RunRecord.Loaded> { WriteRecord(2, BuildingNation.Kingdom), WriteRecord(5, BuildingNation.TickTock) });
            Assert.AreEqual(2, run.GhostCount);
            // 첫 턴은 거리 배율(0.6대)로 수입이 적어 아무것도 안 사는 빌드가 있다 — 세 턴 돌린다
            for (run.State.Turn = 1; run.State.Turn <= 3; run.State.Turn++)
                run.ShopPhase();
            int ai = 0, aiBuilt = 0;
            foreach (Lord lord in run.State.Lords)
                if (lord.Alive && !lord.IsPlayer && run.GhostOf(lord) == null)
                {
                    ai++;
                    if (System.Array.Exists(run.TowerOf(lord).Field, b => !OwnedBuilding.IsEmpty(b)))
                        aiBuilt++;
                }
            Assert.Greater(ai, 0);
            Assert.AreEqual(ai, aiBuilt, "AI 영주는 상점에서 탑을 짓는다");
        }

        // 기록이 많아도 유령은 런 시작 때 상대 영주 수 × GhostShare(내림)까지 — 나머지는 AI(사용자 10/27)
        [Test]
        public void GhostsCappedAtShareOfLords()
        {
            WorldRun run = NewRun();
            int cap = Mathf.FloorToInt(run.State.AiLords * _config.World.GhostShare);
            Assert.Greater(cap, 0);
            var records = new List<RunRecord.Loaded>();
            for (int i = 0; i < run.State.AiLords; i++)
                records.Add(WriteRecord(1, BuildingNation.Kingdom));
            run.AddGhosts(records);
            Assert.AreEqual(cap, run.GhostCount, "기록이 상대 영주 수만큼 있어도 상한까지");
            run.AddGhosts(new List<RunRecord.Loaded> { WriteRecord(1, BuildingNation.Kingdom) });
            Assert.AreEqual(cap, run.GhostCount, "다시 붙여도 상한을 넘지 않는다");
        }

        // 같은 사람(기록의 닉네임)은 한 판에 유령 한 번 — 닉네임이 빈 기록은 사람을 몰라 각각 쓴다(사용자 10/28)
        [Test]
        public void SamePersonHauntsOnce()
        {
            WorldRun run = NewRun();
            run.AddGhosts(new List<RunRecord.Loaded>
            {
                WriteRecord(3, BuildingNation.Kingdom, null, "가"),
                WriteRecord(2, BuildingNation.Kingdom, null, "가"),
                WriteRecord(2, BuildingNation.Tribe, null, "나"),
                WriteRecord(1, BuildingNation.Tribe),
                WriteRecord(1, BuildingNation.Tribe),
            });
            Assert.AreEqual(4, run.GhostCount, "「가」 둘 중 하나만 · 「나」 · 이름 없는 둘");
            var names = new List<string>();
            foreach (Lord lord in run.State.Lords)
                if (run.GhostOf(lord) is WorldRun.Ghost ghost && ghost.Record.Nickname != "")
                    names.Add(ghost.Record.Nickname);
            CollectionAssert.AreEquivalent(new[] { "가", "나" }, names);
        }

        // 유령 연합 — 유령 2명 = 연합 2개, 바탕은 유령 그대로(탑 = 스냅샷 · 골드 없음), 흡수된 영주의 탑은 사라진다
        [Test]
        public void GhostCoalitionsKeepGhostTowers()
        {
            WorldRun run = NewRun();
            run.AddGhosts(new List<RunRecord.Loaded> { WriteRecord(2, BuildingNation.Kingdom), WriteRecord(3, BuildingNation.Tribe) });
            run.State.Turn = 2;
            run.ShopPhase();
            WorldState state = run.State;
            // 내 땅을 연합 조건(15)까지 — 나와 이어진 땅부터
            for (int region = 0, mine = 1; region < state.Owner.Length && mine < state.Rules.CoalitionLands; region++)
            {
                Lord owner = state.OwnerOf(region);
                if (owner == null || owner.IsPlayer || run.GhostOf(owner) != null)
                    continue;
                state.Owner[region] = state.Player.Id;
                mine++;
                if (state.LandsOf(owner) == 0)
                    owner.Alive = false;
            }
            List<WorldState.Coalition> formed = run.FormCoalitions();
            Assert.AreEqual(2, formed.Count, "유령 2명 = 연합 2개");
            foreach (WorldState.Coalition coalition in formed)
            {
                Assert.IsTrue(coalition.Ghost);
                Assert.NotNull(run.GhostOf(coalition.Lord), "바탕은 유령 그대로");
                Assert.AreEqual(0, run.TowerOf(coalition.Lord).Gold, "흡수한 영주의 골드를 쓰지 않는다");
                foreach (int member in coalition.Members)
                    Assert.IsNull(run.TowerOf(state.Lords[member]), "흡수된 영주의 탑은 사라진다");
            }
            Assert.AreEqual(2, state.AiLords);
        }

        // 못 읽는 기록(깨진 줄 · 다른 버전)은 건너뛴다 — 경고 로그. 읽히는 것은 오래 버틴 것부터
        [Test]
        public void BadRecordsAreSkipped()
        {
            WriteRecord(2, BuildingNation.Kingdom);
            WriteRecord(4, BuildingNation.Tribe);
            File.WriteAllText(Path.Combine(_folder, "broken.json"), "{\"Kind\":\"Run\",\"Format\":1}\n{\"Field\":[{");
            string old = Path.Combine(_folder, "old.json");
            File.WriteAllLines(old, File.ReadAllLines(Directory.GetFiles(_folder, "2*.json")[0]));
            File.WriteAllText(old, File.ReadAllText(old).Replace($"\"GameVersion\":\"{Application.version}\"", "\"GameVersion\":\"0.0.0-old\""));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[RunRecord\] 기록을 건너뜀.*broken"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[RunRecord\] 기록을 건너뜀.*old"));
            List<RunRecord.Loaded> all = RunRecord.ReadAll(_folder, _ids, _config);
            Assert.AreEqual(2, all.Count);
            Assert.AreEqual(4, all[0].LastTurn, "오래 버틴 것부터");
            Assert.AreEqual(2, all[1].LastTurn);
        }

        // 신화 ①안 — 기록에 든 신화와 같은 땅 신화는 한 번만, 다른 땅 신화는 붙는다
        [Test]
        public void SameMythicCountsOnce()
        {
            UpgradeDefinition picked = _config.SpecialUpgrades[0], other = _config.SpecialUpgrades[1];
            TowerState source = NewTower(Pool(1));
            source.Upgrades.Add(new UpgradeGain { Definition = picked, Rarity = BuildingRarity.Mythic });
            var writer = new RunRecord(_folder, _ids);
            writer.Add(source, BuildingNation.Kingdom, Color.white, 6, 1);
            RunRecord.Loaded record = RunRecord.Read(writer.FilePath, _ids, _config);

            WorldRun run = NewRun();
            run.AddGhosts(new List<RunRecord.Loaded> { record });
            Lord lord = GhostLord(run);
            int land = System.Array.IndexOf(run.State.Owner, lord.Id);
            Assert.GreaterOrEqual(land, 0);
            for (int region = 0; region < run.RegionSpecial.Length; region++)
                run.RegionSpecial[region] = null;
            run.RegionSpecial[land] = picked;
            run.State.Turn = 1;
            run.ShopPhase();
            Assert.AreEqual(1, Count(run.TowerOf(lord).Holdings(), picked), "기록의 신화 = 땅 신화 — 한 번만");

            run.RegionSpecial[land] = other;
            run.SyncSpecials(lord);
            Assert.AreEqual(1, Count(run.TowerOf(lord).Holdings(), picked));
            Assert.AreEqual(1, Count(run.TowerOf(lord).Holdings(), other), "다른 땅 신화는 쥔 동안 붙는다");
        }

        // ── 만들기

        private WorldRun NewRun() => new WorldRun(_config.World, _config, new System.Random(7));

        private static Lord GhostLord(WorldRun run)
        {
            foreach (Lord lord in run.State.Lords)
                if (run.GhostOf(lord) != null)
                    return lord;
            return null;
        }

        // 턴 1..turns — 턴 t의 탑 1층 = Pool(t)
        private RunRecord.Loaded WriteRecord(int turns, BuildingNation nation, System.Func<int, int> lives = null, string nickname = "")
        {
            var writer = new RunRecord(_folder, _ids);
            for (int turn = 1; turn <= turns; turn++)
                writer.Add(NewTower(Pool(turn)), nation, Color.white, lives != null ? lives(turn) : 6, turn, nickname);
            // 런 id는 초 단위 시각이라 같은 초에 둘을 쓰면 겹친다 — 파일 이름을 턴 수로 갈라 둔다
            string path = Path.Combine(_folder, $"{turns}-{System.Guid.NewGuid():N}.json");
            File.Move(writer.FilePath, path);
            return RunRecord.Read(path, _ids, _config);
        }

        private BuildingDefinition Pool(int index) => _config.ShopPool[index];

        private TowerState NewTower(BuildingDefinition first)
        {
            var tower = new TowerState(Team.Ally);
            tower.Reset(_config);
            tower.Field[0] = new OwnedBuilding(first);
            return tower;
        }

        private static int Count(List<UpgradeHolding> holdings, UpgradeDefinition upgrade)
        {
            int count = 0;
            foreach (UpgradeHolding holding in holdings)
                if (holding.Definition == upgrade)
                    count += holding.Count;
            return count;
        }
    }
}
