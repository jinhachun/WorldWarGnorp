using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GnorpWar.Editor.Tests
{
    // 탑 스냅샷(Run/TowerSnapshot) — 저장 → 다시 세운 탑이 원본과 같은가 · 이상한 JSON은 실패로 알리는가
    public class TowerSnapshotTests
    {
        private BattleConfig _config;
        private AssetIds _ids;

        [OneTimeSetUp]
        public void Load()
        {
            _config = AssetDatabase.LoadAssetAtPath<BattleConfig>("Assets/Data/BattleConfig.asset");
            _ids = AssetDatabase.LoadAssetAtPath<AssetIds>(AssetIdsMenu.Path);
            Assert.NotNull(_config, "BattleConfig");
            Assert.NotNull(_ids, "등록부 — 메뉴 「GnorpWar → 에셋 ID 등록부 갱신」");
        }

        // 등록부가 프로젝트의 기물 · 업그레이드 · 기물 능력을 전부 담는가(새 에셋을 만들고 갱신을 잊으면 여기서 걸린다)
        [Test]
        public void RegistryHasEveryAsset()
        {
            var listed = new HashSet<string>();
            foreach (AssetIds.Entry entry in _ids.Entries)
                listed.Add(entry.Id + "|" + (entry.Asset != null ? entry.Asset.name : "null"));
            var missing = new List<string>();
            List<AssetIds.Entry> all = AssetIdsMenu.Collect();
            foreach (AssetIds.Entry entry in all)
                if (!listed.Contains(entry.Id + "|" + entry.Asset.name))
                    missing.Add(entry.Asset.name);
            Assert.IsEmpty(missing, "등록부에 빠진 에셋 — 메뉴 「GnorpWar → 에셋 ID 등록부 갱신」");
            Assert.AreEqual(all.Count, _ids.Entries.Count, "등록부에 지워진 에셋이 남음");
            Assert.Greater(all.Count, 50, "검사 대상이 실제로 들어갔나");
        }

        [TestCase(BuildingNation.Kingdom)]
        [TestCase(BuildingNation.TickTock)]
        [TestCase(BuildingNation.Tribe)]
        public void RoundTripKeepsTower(BuildingNation nation)
        {
            TowerState original = MakeTower(nation);
            Color color = new Color(0.3f, 0.71f, 0.123f, 1f);
            string json = TowerSnapshot.ToJson(original, nation, color, 4, 7, _ids);
            TowerSnapshot snapshot = TowerSnapshot.FromJson(json, Team.Enemy, _ids, _config);
            TowerState copy = snapshot.Tower;

            Assert.AreEqual(nation, snapshot.Nation);
            Assert.AreEqual(color, snapshot.LordColor);
            Assert.AreEqual(4, snapshot.Lives);
            Assert.AreEqual(7, snapshot.Turn);
            Assert.AreEqual(Application.version, snapshot.GameVersion);
            Assert.AreEqual(Team.Enemy, copy.Team);

            AssertSameRow(original.Field, copy.Field, "필드");
            AssertSameRow(original.Storage, copy.Storage, "보관함");
            Assert.AreEqual(original.FloorRaises, copy.FloorRaises, "층 올림");

            Assert.AreEqual(original.Upgrades.Count, copy.Upgrades.Count, "업그레이드 수");
            for (int i = 0; i < original.Upgrades.Count; i++)
            {
                Assert.AreSame(original.Upgrades[i].Definition, copy.Upgrades[i].Definition, $"업그레이드 {i}");
                Assert.AreEqual(original.Upgrades[i].Rarity, copy.Upgrades[i].Rarity, $"업그레이드 {i} 등급");
            }
            List<UpgradeHolding> heldA = original.Holdings(), heldB = copy.Holdings();
            Assert.AreEqual(heldA.Count, heldB.Count);
            for (int i = 0; i < heldA.Count; i++)
                Assert.AreEqual(heldA[i].Description, heldB[i].Description, "업그레이드 설명");

            AssertSameBook(original.Book, copy.Book);

            Assert.AreEqual(TowerPower.Of(original.Field, _config, _config.World), TowerPower.Of(copy.Field, _config, _config.World), "탑 전투력");
            Assert.Greater(TowerPower.Of(original.Field, _config, _config.World), 0f, "전투력이 0이면 비교가 빈 것");

            for (int i = 0; i < original.Field.Length; i++)
                Assert.AreEqual(Tooltip(original, original.Field[i]), Tooltip(copy, copy.Field[i]), $"툴팁 {i}층");
            for (int i = 0; i < original.Storage.Length; i++)
                Assert.AreEqual(Tooltip(original, original.Storage[i]), Tooltip(copy, copy.Storage[i]), $"보관함 툴팁 {i}");

            // 다시 적으면 글자 하나까지 같다
            Assert.AreEqual(json, TowerSnapshot.ToJson(copy, snapshot.Nation, snapshot.LordColor, snapshot.Lives, snapshot.Turn, _ids));
        }

        [Test]
        public void UnknownBuildingIdFails()
        {
            string json = Json(BuildingNation.Kingdom);
            string id = _ids.IdOf(MakeTower(BuildingNation.Kingdom).Field[0].Definition);
            Assert.Throws<TowerSnapshot.InvalidException>(() => Read(json.Replace(id, "00000000000000000000000000000000")));
        }

        [Test]
        public void UnknownUpgradeIdFails()
        {
            string json = Json(BuildingNation.Kingdom);
            string id = _ids.IdOf(_config.Upgrades[0]);
            Assert.Throws<TowerSnapshot.InvalidException>(() => Read(json.Replace(id, "ffffffffffffffffffffffffffffffff")));
        }

        // 기물 ID 자리에 업그레이드 ID — 종류가 다르면 실패
        [Test]
        public void WrongKindIdFails()
        {
            string json = Json(BuildingNation.Kingdom);
            string building = _ids.IdOf(MakeTower(BuildingNation.Kingdom).Field[0].Definition);
            Assert.Throws<TowerSnapshot.InvalidException>(() => Read(json.Replace(building, _ids.IdOf(_config.Upgrades[0]))));
        }

        [Test]
        public void OtherGameVersionFails()
        {
            string json = Json(BuildingNation.TickTock);
            string version = $"\"GameVersion\":\"{Application.version}\"";
            StringAssert.Contains(version, json);
            Assert.Throws<TowerSnapshot.InvalidException>(() => Read(json.Replace(version, "\"GameVersion\":\"0.0.0-old\"")));
            Assert.Throws<TowerSnapshot.InvalidException>(() => TowerSnapshot.FromJson(json, Team.Enemy, _ids, _config, "9.9.9"));
        }

        [Test]
        public void OtherFormatFails()
        {
            string json = Json(BuildingNation.Tribe);
            string format = $"\"Format\":{TowerSnapshot.CurrentFormat}";
            StringAssert.Contains(format, json);
            Assert.Throws<TowerSnapshot.InvalidException>(() => Read(json.Replace(format, "\"Format\":99")));
        }

        [Test]
        public void BrokenJsonFails()
        {
            Assert.Throws<TowerSnapshot.InvalidException>(() => Read("{\"Field\":[{"));
            Assert.Throws<TowerSnapshot.InvalidException>(() => Read("{}"));
        }

        // 오른 횟수가 신화를 넘으면 실패
        [Test]
        public void ImpossibleRarityFails()
        {
            string json = Json(BuildingNation.Kingdom);
            Assert.Throws<TowerSnapshot.InvalidException>(() => Read(json.Replace("\"Upgrades\":0,", "\"Upgrades\":9,")));
        }

        // 등록부에 없는 에셋은 적을 때 실패
        [Test]
        public void UnregisteredAssetFailsOnWrite()
        {
            TowerState tower = MakeTower(BuildingNation.Kingdom);
            var stray = ScriptableObject.CreateInstance<BuildingDefinition>();
            try
            {
                tower.Storage[1] = new OwnedBuilding(stray);
                Assert.Throws<TowerSnapshot.InvalidException>(() => TowerSnapshot.ToJson(tower, BuildingNation.Kingdom, Color.white, 3, 1, _ids));
            }
            finally
            {
                Object.DestroyImmediate(stray);
            }
        }

        // ── 만들기 · 비교

        private string Json(BuildingNation nation) => TowerSnapshot.ToJson(MakeTower(nation), nation, Color.white, 3, 5, _ids);

        private TowerSnapshot Read(string json) => TowerSnapshot.FromJson(json, Team.Enemy, _ids, _config);

        // 그 국가의 기물로 — 층 올림 2 · 맨 위층은 비움 · 등급 여럿(신화 하나) · 스택 · 쓴 골드 · 계몽주의 쿨다운 배율 · 보관함 둘 ·
        // 합치기 보상(같은 종 두 번 포함) · 지역 특수 신화 둘(징병제 · 국가주의) · 영구 강화(유닛 · 타워)
        private TowerState MakeTower(BuildingNation nation)
        {
            var pool = new List<BuildingDefinition>();
            foreach (BuildingDefinition b in _config.ShopPool)
                if (b.Nation == nation)
                    pool.Add(b);
            Assert.GreaterOrEqual(pool.Count, 6, $"{nation} 기물");

            var tower = new TowerState(Team.Ally);
            tower.Reset(_config);
            tower.FloorRaises = 2;
            tower.Field = new OwnedBuilding[_config.StartFloors + 2];
            for (int i = 0; i < tower.Field.Length - 1; i++)
            {
                BuildingDefinition definition = pool[i];
                int upgrades = i == 0 ? BuildingRarity.Mythic - definition.Rarity : i % 3;
                upgrades = Mathf.Min(upgrades, BuildingRarity.Mythic - definition.Rarity);
                var owned = new OwnedBuilding(definition);
                for (int u = 0; u < upgrades; u++)
                    owned.Upgrade();
                owned.AddStacks(i * 2);
                owned.AddSpent(3 * (i + 1));
                if (i == 1)
                    owned.ScaleCooldown(0.8f);
                tower.Field[i] = owned;
            }
            tower.Storage[0] = new OwnedBuilding(pool[pool.Count - 1]);
            var stored = new OwnedBuilding(pool[pool.Count - 2]);
            stored.Upgrade();
            tower.Storage[3] = stored;

            UpgradeDefinition first = _config.Upgrades[0], second = _config.Upgrades[1];
            tower.Upgrades.Add(new UpgradeGain { Definition = first, Rarity = BuildingRarity.Common });
            tower.Upgrades.Add(new UpgradeGain { Definition = second, Rarity = BuildingRarity.Epic });
            tower.Upgrades.Add(new UpgradeGain { Definition = first, Rarity = BuildingRarity.Rare });
            tower.Upgrades.Add(new UpgradeGain { Definition = Special(SpecialKind.Conscription), Rarity = BuildingRarity.Mythic });
            tower.Upgrades.Add(new UpgradeGain { Definition = Special(SpecialKind.Nationalism), Rarity = BuildingRarity.Mythic });

            RunStatPassive runStat = FirstAsset<RunStatPassive>();
            RunBook previous = RunBook.Of(Team.Ally);
            try
            {
                RunBook.Use(Team.Ally, tower.Book);
                StatBook.AddForRun(Team.Ally, runStat, new StatModifier(UnitStat.Attack, StatOp.Flat, 1f));
                StatBook.AddForRun(Team.Ally, runStat, new StatModifier(UnitStat.Attack, StatOp.Flat, 2f));   // 스택 — 한 줄로 합쳐진다
                StatBook.AddForRun(Team.Ally, runStat, new StatModifier(UnitStat.MaxHp, StatOp.Percent, 0.05f));
                TowerBook.AddForRun(Team.Ally, new TowerModifier(TowerStat.Attack, StatOp.Percent, 0.1f));
                TowerBook.AddForRun(Team.Ally, new TowerModifier(TowerStat.Range, StatOp.Multiply, 1.1f));
            }
            finally
            {
                RunBook.Use(Team.Ally, previous);
            }
            return tower;
        }

        private UpgradeDefinition Special(SpecialKind kind)
        {
            foreach (UpgradeDefinition upgrade in _config.SpecialUpgrades)
                if (upgrade.Special == kind)
                    return upgrade;
            Assert.Fail($"지역 특수 {kind}");
            return null;
        }

        private static T FirstAsset<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            Assert.IsNotEmpty(guids, typeof(T).Name);
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static void AssertSameRow(OwnedBuilding[] a, OwnedBuilding[] b, string what)
        {
            Assert.AreEqual(a.Length, b.Length, $"{what} 칸 수");
            int filled = 0;
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(OwnedBuilding.IsEmpty(a[i]), OwnedBuilding.IsEmpty(b[i]), $"{what} {i} 빈 칸");
                if (OwnedBuilding.IsEmpty(a[i]))
                    continue;
                filled++;
                Assert.AreSame(a[i].Definition, b[i].Definition, $"{what} {i} 기물");
                Assert.AreEqual(a[i].Upgrades, b[i].Upgrades, $"{what} {i} 오른 횟수");
                Assert.AreEqual(a[i].Rarity, b[i].Rarity, $"{what} {i} 등급");
                Assert.AreEqual(a[i].Copies, b[i].Copies, $"{what} {i} 합쳐진 수");
                Assert.AreEqual(a[i].Stacks, b[i].Stacks, $"{what} {i} 스택");
                Assert.AreEqual(a[i].SpentGold, b[i].SpentGold, $"{what} {i} 쓴 골드");
                Assert.AreEqual(a[i].CooldownScale, b[i].CooldownScale, $"{what} {i} 쿨다운 배율");
                Assert.AreEqual(a[i].Cooldown, b[i].Cooldown, $"{what} {i} 쿨다운");
            }
            Assert.Greater(filled, 0, $"{what}에 비교할 기물이 있나");
        }

        // 영구 강화 — 모든 유닛 × 스탯의 합 · 타워 스탯 계산이 같은가
        private static void AssertSameBook(RunBook a, RunBook b)
        {
            int units = 0;
            bool any = false;
            foreach (string guid in AssetDatabase.FindAssets("t:UnitDefinition"))
            {
                var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                units++;
                foreach (UnitStat stat in System.Enum.GetValues(typeof(UnitStat)))
                {
                    StatSum sa = StatSum.Identity, sb = StatSum.Identity;
                    StatBook.AccumulateBook(a, Team.Ally, unit, stat, ref sa);
                    StatBook.AccumulateBook(b, Team.Enemy, unit, stat, ref sb);
                    Assert.AreEqual(sa.Flat, sb.Flat, $"{unit.name} {stat} 고정");
                    Assert.AreEqual(sa.Percent, sb.Percent, $"{unit.name} {stat} %");
                    Assert.AreEqual(sa.Multiply, sb.Multiply, $"{unit.name} {stat} ×");
                    any |= sa.Flat != 0f || sa.Percent != 0f;
                }
            }
            Assert.Greater(units, 0);
            Assert.IsTrue(any, "유닛 영구 강화가 걸린 유닛이 하나는 있어야 비교가 된다");

            RunBook previous = RunBook.Of(Team.Ally);
            try
            {
                foreach (TowerStat stat in System.Enum.GetValues(typeof(TowerStat)))
                {
                    RunBook.Use(Team.Ally, a);
                    float va = TowerBook.Compute(Team.Ally, stat, 100f);
                    RunBook.Use(Team.Ally, b);
                    float vb = TowerBook.Compute(Team.Ally, stat, 100f);
                    Assert.AreEqual(va, vb, $"타워 {stat}");
                }
            }
            finally
            {
                RunBook.Use(Team.Ally, previous);
            }
        }

        // 기물 툴팁 본문 — 그 탑의 징병제(추가 소환)까지. 스택은 툴팁의 상태 줄에 쓰이므로 같이 붙인다
        private static string Tooltip(TowerState tower, OwnedBuilding owned)
        {
            if (OwnedBuilding.IsEmpty(owned))
                return "";
            UpgradeDefinition conscription = tower.Special(SpecialKind.Conscription);
            int extra = conscription != null ? Mathf.RoundToInt(conscription.ValueAt(BuildingRarity.Mythic)) : 0;
            return BuildingTooltip.Describe(owned.Definition, owned.Level, owned.Cooldown, owned.Rarity == BuildingRarity.Mythic, extra)
                + "\n스택 " + owned.Stacks;
        }
    }
}
