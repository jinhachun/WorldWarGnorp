using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 이벤트 땅 방문의 후보 하나 — 종류마다 채우는 칸이 다르다(RunState.BeginVisit). 화면은 이 값을 무대 위 사물로 그리고, 누르면 RunState.ChooseVisit(그 번호)
    public class EventChoice
    {
        // 대장간 — 올릴 기물과 그 칸(Storage = 보관함 · Index = 칸) · 지금 등급 → 오를 등급
        public OwnedBuilding Building;
        public bool Storage;
        public int Index;
        public BuildingRarity From;
        public BuildingRarity To;
        // 신전 — 올릴 내 업그레이드(From → To) · 새로 얻기(New — 올릴 것이 없을 때, To 등급으로) / 피의 제단 — 얻을 지역 특수(To = 신화) / 지형 성소 — 얻을 업그레이드
        public UpgradeDefinition Upgrade;
        public bool New;
        // 용병 상인 — 얻을 기물(To = 그 등급)
        public BuildingDefinition Offer;
        // 훈련장 — 고를 유닛과 영구히 더하는 값
        public UnitDefinition Unit;
        public int Hp;
        public int Attack;
        // 선동가 — 고를 영주 · 지금 행복도 · 깎는 값
        public Lord Lord;
        public int Happiness;
        public int Drop;
        // 지형 성소 — 그 땅의 지형
        public MapDefinition Terrain;
        // 보물 창고 — 받을 골드
        public int Gold;
        // false면 골라도 받지 못한다(용병 상인 — 탑 · 보관함에 자리가 없다). 그래도 고를 수 있고, 고르면 땅이 닫힌다
        public bool Receivable = true;
    }

    // 이벤트 땅 한 번의 방문 — 고르기 전의 후보들. 고를 때까지 땅은 열려 있고 턴도 쓰지 않는다
    public class EventOffer
    {
        public int Region;
        public EventLandKind Kind;
        public EventLandDefinition Definition;
        public EventChoice[] Choices;
        // 고르지 않고 닫을 수 있나 — 피의 제단(「바치지 않기」) · 후보가 없을 때(대장간에 올릴 것이 없음 등). 닫아도 땅은 닫히고 턴을 쓴다
        public bool CanDecline;
        // 고를 수 없다 — 피의 제단에서 목숨이 바칠 만큼 없을 때. 후보는 보여 준다
        public bool Blocked;
    }

    public static partial class RunState
    {
        // ── 이벤트 땅(아웃게임기획 §3-1) — 방문 = 그 턴의 침략 대신. BeginVisit(후보) → ChooseVisit(고름) 또는 DeclineVisit(닫기) → 효과 · 닫힘 · Phase = Aftermath(쉬기와 같은 길 — 지도가 EndTurn · AI 턴 재생)
        private static EventOffer _visit;

        // 지금 고르는 중인 방문(없으면 null)
        public static EventOffer CurrentVisit => _visit;

        // 그 땅의 이벤트 데이터(이름 · 아이콘) — 이벤트 땅이 아니면 null
        public static EventLandDefinition EventOf(int region)
            => World.State.Graph.IsEvent[region] ? _config.World.EventOf(World.State.EventKind[region]) : null;

        // 툴팁 글 — 이번 턴의 값으로(§5-2 6번). 지형 성소는 그 땅의 지형 이름
        public static string EventText(int region)
        {
            EventLandDefinition definition = EventOf(region);
            if (definition == null)
                return "";
            switch (definition.Kind)
            {
                case EventLandKind.Mercenary: return definition.Describe(definition.Choices);
                case EventLandKind.Training: return definition.Describe(definition.Hp, definition.Attack);
                case EventLandKind.BloodAltar: return definition.Describe(definition.LifeCost, definition.Choices);
                case EventLandKind.Agitator: return definition.Describe(definition.HappinessDrop);
                case EventLandKind.Shrine:
                    MapDefinition terrain = World.Map.TerrainOf(region);
                    UpgradeDefinition shrine = definition.ShrineOf(terrain);
                    return definition.Describe(terrain.DisplayName, shrine != null ? shrine.ValueAt(BuildingRarity.Common).ToString("0.##") : "0");
                case EventLandKind.Treasury: return definition.Describe(definition.GoldAt(World.State.Turn));
                default: return definition.Describe();
            }
        }

        // 방문을 연다 — 후보를 굴린다(같은 땅으로 다시 부르면 같은 후보). 방문할 수 없는 땅이면 null
        public static EventOffer BeginVisit(int region)
        {
            if (_visit != null && _visit.Region == region)
                return _visit;
            if (Phase != RunPhase.Map || !VisitableEvents().Contains(region))
                return null;
            EventLandKind kind = World.State.EventKind[region];
            EventLandDefinition definition = _config.World.EventOf(kind);
            var choices = new List<EventChoice>();
            switch (kind)
            {
                case EventLandKind.Forge: ForgeChoices(choices); break;
                case EventLandKind.Temple: TempleChoices(choices, definition); break;
                case EventLandKind.Mercenary: MercenaryChoices(choices, definition); break;
                case EventLandKind.Training: TrainingChoices(choices, definition); break;
                case EventLandKind.BloodAltar: AltarChoices(choices, definition); break;
                case EventLandKind.Agitator: AgitatorChoices(choices, definition); break;
                case EventLandKind.Shrine:
                    MapDefinition terrain = World.Map.TerrainOf(region);
                    UpgradeDefinition shrine = definition.ShrineOf(terrain);
                    if (shrine != null)
                        choices.Add(new EventChoice { Upgrade = shrine, Terrain = terrain, To = BuildingRarity.Common, New = true });
                    else
                        Debug.LogWarning($"[World] 지형 성소 — 「{terrain.DisplayName}」 지형의 업그레이드가 없다(이벤트 에셋의 지형 성소 칸)");
                    break;
                case EventLandKind.Treasury: choices.Add(new EventChoice { Gold = definition.GoldAt(World.State.Turn) }); break;
            }
            _visit = new EventOffer
            {
                Region = region,
                Kind = kind,
                Definition = definition,
                Choices = choices.ToArray(),
                Blocked = kind == EventLandKind.BloodAltar && Lives <= definition.LifeCost,
            };
            _visit.CanDecline = kind == EventLandKind.BloodAltar || _visit.Choices.Length == 0;
            return _visit;
        }

        // 지금 방문의 index번째 후보를 고른다 — 효과 · 땅 닫힘 · 턴 끝(Phase = Aftermath · 런 기록 한 줄). 고를 수 없으면 false(아무것도 안 바뀐다)
        public static bool ChooseVisit(int index)
        {
            if (_visit == null || _visit.Blocked || index < 0 || index >= _visit.Choices.Length)
                return false;
            EventChoice choice = _visit.Choices[index];
            EventLandDefinition definition = _visit.Definition;
            string what = "";
            switch (_visit.Kind)
            {
                case EventLandKind.Forge:
                    choice.Building.Upgrade();
                    OwnedBuilding.NotifyEffect(choice.Building);
                    what = $"{choice.Building.Definition.name} {choice.From} → {choice.To}";
                    break;
                case EventLandKind.Temple:
                    if (choice.New)
                        GainUpgrade(choice.Upgrade, choice.To);
                    else
                    {
                        int at = Player.Upgrades.FindIndex(g => g.Definition == choice.Upgrade && g.Rarity == choice.From);
                        Player.Upgrades[at] = new UpgradeGain { Definition = choice.Upgrade, Rarity = choice.To };
                    }
                    UpgradesChanged?.Invoke();
                    what = $"{choice.Upgrade.DisplayName} {(choice.New ? "새로" : choice.From.ToString())} → {choice.To}";
                    break;
                case EventLandKind.Mercenary:
                    if (choice.Receivable)
                        Acquire(choice.Offer, choice.To - choice.Offer.Rarity);
                    what = $"{choice.Offer.name}({choice.To}){(choice.Receivable ? "" : " — 자리가 없어 못 받음")}";
                    break;
                case EventLandKind.Training:
                    StatBook.AddForRun(Team.Ally, choice.Unit, new StatModifier(UnitStat.MaxHp, StatOp.Flat, choice.Hp));
                    StatBook.AddForRun(Team.Ally, choice.Unit, new StatModifier(UnitStat.Attack, StatOp.Flat, choice.Attack));
                    what = $"{choice.Unit.DisplayName} 체력 +{choice.Hp} · 공격력 +{choice.Attack}";
                    break;
                case EventLandKind.BloodAltar:
                    World.State.Lives -= definition.LifeCost;
                    GainUpgrade(choice.Upgrade, BuildingRarity.Mythic);
                    UpgradesChanged?.Invoke();
                    what = $"목숨 −{definition.LifeCost} · {choice.Upgrade.DisplayName}";
                    break;
                case EventLandKind.Agitator:
                    choice.Lord.Happiness = Mathf.Max(0, choice.Lord.Happiness - choice.Drop);
                    what = $"영주 {choice.Lord.Id}번 행복도 {choice.Happiness} → {choice.Lord.Happiness}";
                    break;
                case EventLandKind.Shrine:
                    GainUpgrade(choice.Upgrade, choice.To);
                    UpgradesChanged?.Invoke();
                    what = choice.Upgrade.DisplayName;
                    break;
                case EventLandKind.Treasury:
                    GainGold(choice.Gold);
                    what = $"골드 +{choice.Gold}";
                    break;
            }
            FinishVisit(what);
            return true;
        }

        // 고르지 않고 닫는다 — 피의 제단 「바치지 않기」 · 후보가 없을 때만. 땅은 닫히고 턴을 쓴다
        public static bool DeclineVisit()
        {
            if (_visit == null || !_visit.CanDecline)
                return false;
            FinishVisit("고르지 않음");
            return true;
        }

        // 치트 · 테스트 — 방문을 열고 무작위 후보를 바로 고른다(고를 수 없으면 닫기)
        public static void Visit(int region)
        {
            EventOffer offer = BeginVisit(region);
            if (offer == null)
                return;
            if (!offer.Blocked && offer.Choices.Length > 0)
                ChooseVisit(Random.Range(0, offer.Choices.Length));
            else
                DeclineVisit();
        }

        private static void FinishVisit(string what)
        {
            int region = _visit.Region;
            EventLandKind kind = WorldTurn.Visit(World.State, region);
            _visit = null;
            LastOutcome = Outcome.None;
            Phase = RunPhase.Aftermath;
            RecordTurn();
            int open = EventRegions().FindAll(e => !World.State.EventClosed[e]).Count;
            Debug.Log($"[World] 턴 {Round + 1} — 이벤트 땅 {region + 1:00} 방문 · {EventLands.Name(kind)} · {what} · 닫힘 · 남은 이벤트 땅 {open}칸");
        }

        // 대장간 — 탑 · 보관함의 기물 중 신화 아래인 것 전부(탑 아래층부터, 그다음 보관함)
        private static void ForgeChoices(List<EventChoice> choices)
        {
            foreach (bool storage in new[] { false, true })
            {
                OwnedBuilding[] row = storage ? Player.Storage : Player.Field;
                for (int i = 0; i < row.Length; i++)
                    if (!OwnedBuilding.IsEmpty(row[i]) && row[i].CanUpgrade)
                        choices.Add(new EventChoice { Building = row[i], Storage = storage, Index = i, From = row[i].Rarity, To = row[i].Rarity + 1 });
            }
        }

        // 신전 — 내 합치기 보상 업그레이드(합치기 풀의 것 — 지역 특수 · 지형 성소 제외) 중 전설 아래이고 다음 등급 값이 있는 것(같은 종 · 같은 등급은 하나로).
        // 없으면 공용 업그레이드 중 그 등급 값이 있는 것 무작위 N개를 새로 얻기로
        private static void TempleChoices(List<EventChoice> choices, EventLandDefinition definition)
        {
            foreach (UpgradeGain gain in Player.Upgrades)
                if (System.Array.IndexOf(_config.Upgrades, gain.Definition) >= 0 && gain.Rarity < BuildingRarity.Legendary && gain.Definition.AvailableAt(gain.Rarity + 1)
                    && !choices.Exists(c => c.Upgrade == gain.Definition && c.From == gain.Rarity))
                    choices.Add(new EventChoice { Upgrade = gain.Definition, From = gain.Rarity, To = gain.Rarity + 1 });
            if (choices.Count > 0)
                return;
            var pool = new List<UpgradeDefinition>(System.Array.FindAll(_config.Upgrades, u => u.InPool && u.Nation == BuildingNation.Common && u.AvailableAt(definition.NewRarity)));
            foreach (UpgradeDefinition upgrade in Pick(pool, definition.Choices))
                choices.Add(new EventChoice { Upgrade = upgrade, New = true, To = definition.NewRarity });
        }

        // 용병 상인 — 내가 고르지 않은 국가(공용 제외)의 기물 N개(서로 다른 기물). 칸마다 등급을 이번 턴의 상점 등급 확률로 먼저 굴리고 그 등급의 기물 하나(내 진열과 같은 표)
        private static void MercenaryChoices(List<EventChoice> choices, EventLandDefinition definition)
        {
            var pool = new List<BuildingDefinition>(System.Array.FindAll(Pool, b => b.Nation != BuildingNation.Common && b.Nation != Nation));
            bool room = FirstEmpty(Player.Field) >= 0 || FirstEmpty(Player.Storage) >= 0;
            for (int i = 0; i < definition.Choices && pool.Count > 0; i++)
            {
                float total = 0f;
                foreach (BuildingRarity rarity in System.Enum.GetValues(typeof(BuildingRarity)))
                    if (pool.Exists(b => b.Rarity == rarity))
                        total += _config.RarityWeight(rarity, ShopTier);
                float pick = Random.Range(0f, total);
                BuildingRarity rolled = pool[pool.Count - 1].Rarity;
                foreach (BuildingRarity rarity in System.Enum.GetValues(typeof(BuildingRarity)))
                {
                    if (!pool.Exists(b => b.Rarity == rarity))
                        continue;
                    pick -= _config.RarityWeight(rarity, ShopTier);
                    if (pick < 0f)
                    {
                        rolled = rarity;
                        break;
                    }
                }
                List<BuildingDefinition> group = pool.FindAll(b => b.Rarity == rolled);
                BuildingDefinition offer = group[Random.Range(0, group.Count)];
                pool.Remove(offer);
                choices.Add(new EventChoice { Offer = offer, To = offer.Rarity, Receivable = room });
            }
        }

        // 훈련장 — 내 탑(필드)의 기물이 낳는 유닛 중 무작위 N종
        private static void TrainingChoices(List<EventChoice> choices, EventLandDefinition definition)
        {
            var units = new List<UnitDefinition>();
            foreach (OwnedBuilding owned in Player.Field)
                if (!OwnedBuilding.IsEmpty(owned) && owned.Definition.Unit != null && !units.Contains(owned.Definition.Unit))
                    units.Add(owned.Definition.Unit);
            foreach (UnitDefinition unit in Pick(units, definition.Choices))
                choices.Add(new EventChoice { Unit = unit, Hp = definition.Hp, Attack = definition.Attack });
        }

        // 피의 제단 — 이번 런 지도에 놓이지 않은 지역 특수 중 내가 안 가진 것 무작위 N개(국가 위기와 같은 풀). 목숨이 모자라도 후보는 보여 준다(Blocked)
        private static void AltarChoices(List<EventChoice> choices, EventLandDefinition definition)
        {
            List<UpgradeDefinition> pool = World.UnplacedSpecials.FindAll(special => !Player.Upgrades.Exists(gain => gain.Definition == special));
            foreach (UpgradeDefinition special in Pick(pool, definition.Choices))
                choices.Add(new EventChoice { Upgrade = special, New = true, To = BuildingRarity.Mythic });
        }

        // 선동가 — 내 땅과 국경을 맞댄 AI 영주(최후의 연합 제외 — 연합은 반란이 없다) 무작위 N명
        private static void AgitatorChoices(List<EventChoice> choices, EventLandDefinition definition)
        {
            WorldState state = World.State;
            List<Lord> lords = state.Neighbors(state.Player).FindAll(lord => lord.Alive && !lord.IsPlayer && lord != state.Coalition);
            foreach (Lord lord in Pick(lords, definition.Choices))
                choices.Add(new EventChoice { Lord = lord, Happiness = lord.Happiness, Drop = definition.HappinessDrop });
        }

        // 목록에서 무작위 count개(순서도 섞인다) — 목록은 줄어든다
        private static List<T> Pick<T>(List<T> pool, int count)
        {
            var picked = new List<T>();
            while (picked.Count < count && pool.Count > 0)
            {
                int at = Random.Range(0, pool.Count);
                picked.Add(pool[at]);
                pool.RemoveAt(at);
            }
            return picked;
        }
    }
}
