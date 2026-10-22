using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GnorpWar.Editor.Tests
{
    // 내 런 기록(Run/RunRecord) — 턴마다 덧붙여 쓴 파일을 다시 읽으면 턴 수 · 각 턴의 탑 · 끝 표시가 그대로인가 · 실패하면 기록만 멈추는가
    public class RunRecordTests
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
        }

        [SetUp]
        public void MakeFolder() => _folder = Path.Combine(Path.GetTempPath(), "GnorpRunRecordTests", System.Guid.NewGuid().ToString("N"));

        [TearDown]
        public void RemoveFolder()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        // 런 설정이 런타임에 등록부를 들고 있는가(빠지면 실제 게임에서 기록이 멈춘다)
        [Test]
        public void ConfigHasRegistry() => Assert.AreSame(_ids, _config.AssetIds);

        [Test]
        public void WriteThenReadKeepsEveryTurn()
        {
            var record = new RunRecord(_folder, _ids);
            TowerState tower = NewTower();
            for (int turn = 1; turn <= 5; turn++)
            {
                tower.Field[0] = new OwnedBuilding(_config.ShopPool[turn]);   // 턴마다 다른 탑
                record.Add(tower, BuildingNation.TickTock, _config.LordKeyColor, 7 - turn, turn, "그놉");
                record.Add(tower, BuildingNation.TickTock, _config.LordKeyColor, 0, turn);   // 같은 턴 두 번 — 무시
            }
            record.End(RunRecord.Ending.GameOver, 5);
            record.Add(tower, BuildingNation.TickTock, _config.LordKeyColor, 0, 6);   // 끝난 뒤 — 무시

            Assert.AreEqual(1 + 5 + 1, File.ReadAllLines(record.FilePath).Length, "머리 + 턴 5 + 끝");
            RunRecord.Loaded loaded = RunRecord.Read(record.FilePath, _ids, _config);
            Assert.AreEqual(new[] { 1, 2, 3, 4, 5 }, loaded.Turns.ToArray());
            Assert.AreEqual(RunRecord.Ending.GameOver, loaded.Ending);
            Assert.AreEqual(BuildingNation.TickTock, loaded.Nation);
            Assert.AreEqual("그놉", loaded.Nickname);
            Assert.AreEqual(Application.version, loaded.GameVersion);
            for (int turn = 1; turn <= 5; turn++)
            {
                TowerSnapshot snapshot = TowerSnapshot.FromJson(loaded.SnapshotAt(turn), Team.Enemy, _ids, _config);
                Assert.AreEqual(turn, snapshot.Turn);
                Assert.AreEqual(7 - turn, snapshot.Lives, $"턴 {turn} 목숨 — 같은 턴의 두 번째 줄이 아니라 첫 줄");
                Assert.AreSame(_config.ShopPool[turn], snapshot.Tower.Field[0].Definition, $"턴 {turn} 탑");
            }
            // 기록이 일찍 끝났으면 마지막 스냅샷에 멈춘다
            Assert.AreEqual(5, TowerSnapshot.FromJson(loaded.SnapshotAt(12), Team.Enemy, _ids, _config).Turn);
        }

        // 끝 줄이 없으면 중간 이탈
        [Test]
        public void NoEndMeansAbandoned()
        {
            var record = new RunRecord(_folder, _ids);
            record.Add(NewTower(), BuildingNation.Kingdom, Color.white, 6, 1);
            record.Add(NewTower(), BuildingNation.Kingdom, Color.white, 6, 2);
            RunRecord.Loaded loaded = RunRecord.Read(record.FilePath, _ids, _config);
            Assert.AreEqual(RunRecord.Ending.Abandoned, loaded.Ending);
            Assert.AreEqual(2, loaded.Turns.Count);
        }

        [Test]
        public void UnifiedEnding()
        {
            var record = new RunRecord(_folder, _ids);
            record.Add(NewTower(), BuildingNation.Tribe, Color.white, 6, 1);
            record.End(RunRecord.Ending.Unified, 1);
            Assert.AreEqual(RunRecord.Ending.Unified, RunRecord.Read(record.FilePath, _ids, _config).Ending);
        }

        // 턴이 하나도 없으면 파일을 만들지 않는다(끝 표시도)
        [Test]
        public void NoTurnsNoFile()
        {
            var record = new RunRecord(_folder, _ids);
            record.End(RunRecord.Ending.GameOver, 1);
            Assert.IsFalse(File.Exists(record.FilePath));
        }

        // 등록 안 된 에셋 — 경고 로그 + 그 뒤 기록 중단(예외는 밖으로 안 나간다). 그때까지의 줄은 남는다
        [Test]
        public void UnregisteredAssetStopsRecording()
        {
            var record = new RunRecord(_folder, _ids);
            TowerState tower = NewTower();
            record.Add(tower, BuildingNation.Kingdom, Color.white, 6, 1);
            var stray = ScriptableObject.CreateInstance<BuildingDefinition>();
            try
            {
                tower.Storage[0] = new OwnedBuilding(stray);
                LogAssert.Expect(LogType.Warning, new Regex(@"\[RunRecord\] 런 기록을 멈춤"));
                record.Add(tower, BuildingNation.Kingdom, Color.white, 6, 2);
            }
            finally
            {
                Object.DestroyImmediate(stray);
            }
            tower.Storage[0] = null;
            record.Add(tower, BuildingNation.Kingdom, Color.white, 6, 3);   // 멈춘 뒤 — 무시
            record.End(RunRecord.Ending.GameOver, 3);
            RunRecord.Loaded loaded = RunRecord.Read(record.FilePath, _ids, _config);
            Assert.AreEqual(new[] { 1 }, loaded.Turns.ToArray());
            Assert.AreEqual(RunRecord.Ending.Abandoned, loaded.Ending);
        }

        [Test]
        public void MissingRegistryStopsRecording()
        {
            var record = new RunRecord(_folder, null);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[RunRecord\] 런 기록을 멈춤"));
            record.Add(NewTower(), BuildingNation.Kingdom, Color.white, 6, 1);
            Assert.IsFalse(File.Exists(record.FilePath));
        }

        // 다른 버전 · 깨진 스냅샷 줄은 읽기에서 실패로 알린다
        [Test]
        public void BadFileFails()
        {
            var record = new RunRecord(_folder, _ids);
            record.Add(NewTower(), BuildingNation.Kingdom, Color.white, 6, 1);
            Assert.Throws<TowerSnapshot.InvalidException>(() => RunRecord.Read(record.FilePath, _ids, _config, "0.0.0-old"));
            File.AppendAllLines(record.FilePath, new[] { "{\"Field\":[{" });
            Assert.Throws<TowerSnapshot.InvalidException>(() => RunRecord.Read(record.FilePath, _ids, _config));
            string other = Path.Combine(_folder, "other.json");
            File.WriteAllText(other, "");
            Assert.Throws<TowerSnapshot.InvalidException>(() => RunRecord.Read(other, _ids, _config));
        }

        private TowerState NewTower()
        {
            var tower = new TowerState(Team.Ally);
            tower.Reset(_config);
            tower.Field[0] = new OwnedBuilding(_config.ShopPool[0]);
            return tower;
        }
    }
}
