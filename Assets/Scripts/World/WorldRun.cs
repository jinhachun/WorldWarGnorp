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

        // 턴 수입 = 1라운드 상점 골드 + 땅 수 × 땅당 골드 + 세금 증산 + 약자 보정(가장 큰 영주보다 땅이 적은 만큼), 직전 침략을 이겼으면 승리 보너스
        public int IncomeOf(Lord lord)
            => _battle.ShopGold + State.LandsOf(lord) * _world.GoldPerLand + TaxGold(lord) + WorldTurn.UnderdogGold(State, lord) + (lord.WonLastInvasion ? _battle.WinBonus : 0);

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

        // ── 지역 업그레이드(기획 §4) — 영주마다 자기 땅에 붙은 것만 받는다. 같은 종류는 겹친다(RegionUpgradeDefinition.Total)
        // 플레이어가 고른 국가(RunState.ChooseNation) — 민족주의가 읽는다. AI 영주의 국가는 빌드 방향의 것
        public BuildingNation PlayerNation;

        public BuildingNation NationOf(Lord lord) => lord.IsPlayer ? PlayerNation : _world.Map.BuildOf(lord.Build).Nation;

        public int UpgradeCount(Lord lord, RegionUpgradeKind kind) => State.UpgradeCount(lord, (int)kind);

        // lord가 가진 kind의 합쳐진 값 — 하나도 없으면 none
        private float UpgradeTotal(Lord lord, RegionUpgradeKind kind, float none)
        {
            int count = UpgradeCount(lord, kind);
            return count > 0 ? _world.Map.UpgradeOfKind(kind).Total(count) : none;
        }

        // 턴 수입에 더 받는 골드(세금 증산) · 상점 등급 단계에 더하는 값(기술 연구) · 유닛을 소환하는 기물의 쿨다운에 곱하는 값(보급력)
        public int TaxGold(Lord lord) => Mathf.RoundToInt(UpgradeTotal(lord, RegionUpgradeKind.Tax, 0f));
        public int TechTiers(Lord lord) => Mathf.RoundToInt(UpgradeTotal(lord, RegionUpgradeKind.Tech, 0f));
        public float SupplyScale(Lord lord) => UpgradeTotal(lord, RegionUpgradeKind.Supply, 1f);

        // lord가 against와 싸울 때 유닛 스탯에 걸리는 업그레이드마다 each(업그레이드, 값) — 무기 · 방어구, 그리고 상대의 국가가 다르면 민족주의.
        // against가 null이면(전투 밖 — 상대가 아직 없다) 상대에 달린 것은 뺀다
        public void ForEachUnitUpgrade(Lord lord, Lord against, System.Action<RegionUpgradeDefinition, StatModifier> each)
        {
            foreach (RegionUpgradeKind kind in System.Enum.GetValues(typeof(RegionUpgradeKind)))
            {
                int count = UpgradeCount(lord, kind);
                if (count == 0 || !_world.Map.UpgradeOfKind(kind).UnitModifier(count, out StatModifier modifier))
                    continue;
                if (kind == RegionUpgradeKind.Nationalism && (against == null || NationOf(against) == NationOf(lord)))
                    continue;
                each(_world.Map.UpgradeOfKind(kind), modifier);
            }
        }

        // 최후의 연합(기획 §6-1) — ④ 정산 뒤에 부른다. 뭉쳤으면 바탕 영주(탑 전투력이 가장 높은 영주)를 돌려주고 merged에 합쳐진 영주들을 담는다(아니면 null).
        // 연합의 탑 = 바탕 영주의 탑 + 나머지 영주들의 남은 골드와 탑 · 보관함을 판 값(지금 판매 규칙 — RunState.SellValue)으로 본 상점. 상점은 합쳐진 영주 수만큼 돈다
        public Lord FormCoalition(List<Lord> merged)
        {
            Lord leader = WorldTurn.FormCoalition(State, PowerOf, merged);
            if (leader == null)
                return null;
            int gold = 0;
            foreach (Lord lord in merged)
            {
                TowerState tower = _towers[lord.Id];
                gold += tower.Gold;
                foreach (OwnedBuilding[] row in new[] { tower.Field, tower.Storage })
                    foreach (OwnedBuilding building in row)
                        if (!OwnedBuilding.IsEmpty(building))
                            gold += RunState.SellValue(building);
                _towers.Remove(lord.Id);
            }
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
            Shop(lord, IncomeOf(lord));
            lord.WonLastInvasion = false;   // 승리 보너스는 이긴 다음 상점에 한 번
        }

        private void Shop(Lord lord, int gold)
        {
            // 등급 단계 = 턴 − 1 + 기술 연구, 상한까지
            int tier = System.Math.Min(State.Turn - 1 + TechTiers(lord), _world.ShopTierMax);
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
