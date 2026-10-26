using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 탑 한 채의 스냅샷(아웃게임기획 §9-0 — 비동기 멀티의 유령 영주 한 턴) — JSON으로 적고 다시 세운다.
    // 담는 것: 필드(층 순서 · 빈 층 포함) · 보관함 · 층 올린 수 · 얻은 업그레이드(등급 포함, 얻은 순서) · 「영구히」 강화(RunBook) · 국가 · 영주 색 · 목숨(국가주의) · 턴 · 게임 버전.
    // 안 담는 것: 땅 신화(LandSpecials — 상대 지도에서 정해진다) · GrantedOnce(땅 신화의 한 번 받기) · 골드 · 진열 · 잠금 · 무료 리롤 · 진열 등급(상점 상태 — 유령은 상점을 안 돈다).
    // 에셋은 AssetIds의 ID로 적는다. 모르는 ID · 다른 형식 · 다른 게임 버전이면 InvalidException — 이상한 탑을 조용히 세우지 않는다
    public class TowerSnapshot
    {
        // JSON 모양이 바뀌면 올린다 — 다른 값의 JSON은 읽지 않는다
        public const int CurrentFormat = 1;

        public class InvalidException : System.Exception
        {
            public InvalidException(string message) : base(message) { }
        }

        public TowerState Tower;
        public BuildingNation Nation;
        public Color LordColor;
        public int Lives;
        public int Turn;
        public string GameVersion;

        // ── JSON 모양(JsonUtility). 빈 칸 = Id ""

        [System.Serializable]
        private class Data
        {
            public int Format;
            public string GameVersion;
            public int Turn;
            public int Lives;
            public BuildingNation Nation;
            public Color LordColor;
            public int FloorRaises;
            public Building[] Field;
            public Building[] Storage;
            public Gain[] Upgrades;
            public UnitEntry[] UnitBook;
            public TowerModifier[] TowerBook;
        }

        [System.Serializable]
        private class Building
        {
            public string Id;
            public int Upgrades;
            public int Stacks;
            public int SpentGold;
            public float CooldownScale;
        }

        [System.Serializable]
        private class Gain
        {
            public string Id;
            public BuildingRarity Rarity;
        }

        [System.Serializable]
        private class UnitEntry
        {
            public string Source;
            public StatModifier Modifier;
        }

        public static string ToJson(TowerState tower, BuildingNation nation, Color lordColor, int lives, int turn, AssetIds ids)
        {
            var data = new Data
            {
                Format = CurrentFormat,
                GameVersion = Application.version,
                Turn = turn,
                Lives = lives,
                Nation = nation,
                LordColor = lordColor,
                FloorRaises = tower.FloorRaises,
                Field = System.Array.ConvertAll(tower.Field, b => Write(b, ids)),
                Storage = System.Array.ConvertAll(tower.Storage, b => Write(b, ids)),
                Upgrades = tower.Upgrades.ConvertAll(g => new Gain { Id = ids.IdOf(g.Definition), Rarity = g.Rarity }).ToArray(),
                UnitBook = tower.Book.Units.ConvertAll(e => new UnitEntry { Source = ids.IdOf(SourceAsset(e.Source)), Modifier = e.Modifier }).ToArray(),
                TowerBook = tower.Book.Towers.ToArray(),
            };
            return JsonUtility.ToJson(data);
        }

        // expectedVersion = 이 버전의 JSON만 읽는다(기본 = 지금 게임 버전). config = 빈 탑의 칸 수(진열 등 — 안 담은 것은 빈 시작 값)
        public static TowerSnapshot FromJson(string json, Team team, AssetIds ids, BattleConfig config, string expectedVersion = null)
        {
            Data data;
            try
            {
                data = JsonUtility.FromJson<Data>(json);
            }
            catch (System.ArgumentException e)
            {
                throw new InvalidException($"JSON을 읽지 못함 — {e.Message}");
            }
            if (data == null || data.Field == null || data.Storage == null || data.Upgrades == null || data.UnitBook == null || data.TowerBook == null)
                throw new InvalidException("탑 스냅샷 JSON이 아님(칸이 빠짐)");
            if (data.Format != CurrentFormat)
                throw new InvalidException($"스냅샷 형식 {data.Format} — 지금 형식은 {CurrentFormat}");
            string version = expectedVersion ?? Application.version;
            if (data.GameVersion != version)
                throw new InvalidException($"게임 버전 「{data.GameVersion}」의 스냅샷 — 지금 버전은 「{version}」");
            if (!System.Enum.IsDefined(typeof(BuildingNation), data.Nation))
                throw new InvalidException($"모르는 국가 {(int)data.Nation}");

            var tower = new TowerState(team);
            tower.Reset(config);
            tower.Field = System.Array.ConvertAll(data.Field, b => Read(b, ids));
            tower.Storage = System.Array.ConvertAll(data.Storage, b => Read(b, ids));
            tower.FloorRaises = data.FloorRaises;
            foreach (Gain gain in data.Upgrades)
            {
                CheckRarity(gain.Rarity);
                tower.Upgrades.Add(new UpgradeGain { Definition = ids.Find<UpgradeDefinition>(gain.Id), Rarity = gain.Rarity });
            }
            foreach (UnitEntry entry in data.UnitBook)
            {
                if (!(ids.Find<ScriptableObject>(entry.Source) is IUnitFilter source))
                    throw new InvalidException($"영구 강화의 출처 「{entry.Source}」가 유닛을 고르는 에셋이 아님");
                tower.Book.Units.Add(new StatBook.Entry { Source = source, Modifier = entry.Modifier });
            }
            tower.Book.Towers.AddRange(data.TowerBook);

            return new TowerSnapshot
            {
                Tower = tower,
                Nation = data.Nation,
                LordColor = data.LordColor,
                Lives = data.Lives,
                Turn = data.Turn,
                GameVersion = data.GameVersion,
            };
        }

        private static Building Write(OwnedBuilding owned, AssetIds ids)
            => OwnedBuilding.IsEmpty(owned)
                ? new Building { Id = "" }
                : new Building { Id = ids.IdOf(owned.Definition), Upgrades = owned.Upgrades, Stacks = owned.Stacks, SpentGold = owned.SpentGold, CooldownScale = owned.CooldownScale };

        private static OwnedBuilding Read(Building building, AssetIds ids)
        {
            if (building == null || string.IsNullOrEmpty(building.Id))
                return null;
            BuildingDefinition definition = ids.Find<BuildingDefinition>(building.Id);
            if (building.Upgrades < 0 || (int)definition.Rarity + building.Upgrades > (int)BuildingRarity.Mythic)
                throw new InvalidException($"「{definition.name}」의 오른 횟수 {building.Upgrades}가 신화를 넘음");
            return new OwnedBuilding(definition, building.Upgrades, building.Stacks, building.SpentGold, building.CooldownScale);
        }

        private static void CheckRarity(BuildingRarity rarity)
        {
            if (!System.Enum.IsDefined(typeof(BuildingRarity), rarity))
                throw new InvalidException($"모르는 등급 {(int)rarity}");
        }

        // 영구 강화의 출처는 에셋이어야 ID로 적힌다(RunStatPassive · 훈련장의 UnitDefinition)
        private static ScriptableObject SourceAsset(IUnitFilter source)
            => source as ScriptableObject ?? throw new InvalidException($"영구 강화의 출처 {source?.GetType().Name}가 에셋이 아님 — 스냅샷에 못 적는다");
    }
}
