using UnityEngine;

namespace GnorpWar
{
    // 한 런(월드맵 한 판 — 통일하거나 국가 위기에서 질 때까지)의 상태. 씬을 다시 불러와도 남도록 static이다.
    // 한 턴 = 턴 시작(BeginTurn — AI 영주들의 상점 · 내 수입) → 지도에서 상점 팝업을 여닫으며 침략할 땅을 고름(Invade — 전투 씬으로) 또는 쉬기(Rest)
    // → 전투 결과(FinishBattle) → AI 턴·정산(EndTurn) → 다음 턴.
    // 판도·영주·AI 영주의 탑은 World(World/WorldRun)에, 내 탑은 여기(Player)에 있다. 상점 규칙(구매·판매·리롤·자리 바꾸기)도 여기 — UI는 이 메서드만 부른다
    public static class RunState
    {
        public enum Outcome { None, Win, Lose }
        // 씬이 뜰 때 그 씬의 매니저가 본다 — Map 지도(상점은 지도 위 팝업) · Battle 전투 · Aftermath 내 차례가 끝나 AI 턴(EndTurn)을 돌릴 차례
        public enum RunPhase { Map, Battle, Aftermath }

        // 탑 한 채(TowerState) — 골드·탑·보관함·진열·영구 강화. 내 탑은 여기(Player)에 있고, 아래 규칙(구매·판매·리롤·층 올리기)은 지금 차례인 탑(_side)에 걸린다.
        // 월드맵의 영주들은 저마다 탑을 따로 들고 ShopAs로 차례를 받는다(World/WorldRun)
        private static readonly TowerState Player = new TowerState(Team.Ally);
        // 상대를 고르기 전의 빈 탑 — 적 탑 자리와 적 진영의 영구 강화 목록을 비워 둔다
        private static readonly TowerState NoOpponent = new TowerState(Team.Enemy);
        // 지금 상점을 도는 탑 — ShopAs 동안만 다른 탑
        private static TowerState _side = Player;

        public static bool Started { get; private set; }
        public static int Gold { get => _side.Gold; private set => _side.Gold = value; }
        // 내 목숨(지면 −1 · 늘지 않는다 · 0이면 국가 위기 — 위기에서 지면 게임 오버) · 내 땅 수(전부면 통일). AI 영주는 행복도(Lord.Happiness)
        public static int Lives => World.State.Lives;
        public static int LivesMax => World.State.Rules.LivesStart;
        public static bool InCrisis => World.State.InCrisis;
        public static int Lands => World.State.LandsOf(World.State.Player);
        public static int RegionCount => World.State.Owner.Length;
        // 턴 − 1(0부터) — 턴이 끝날 때마다 +1(쉬어도). 진열 등급 확률 · 성 체력이 읽는다
        public static int Round { get; private set; }
        public static RunPhase Phase { get; private set; }
        // 판도 · 영주 · AI 영주의 탑 — 런마다 새로
        public static WorldRun World { get; private set; }
        // 이번 턴에 침략한 영주(상점 동안은 null)와 그 영주의 탑 · 침략한 땅
        public static Lord Opponent { get; private set; }
        private static TowerState _opponentTower;
        private static int _invadedRegion;
        public static Outcome LastOutcome { get; private set; }
        public static OwnedBuilding[] Field { get => _side.Field; private set => _side.Field = value; }
        public static OwnedBuilding[] Storage { get => _side.Storage; private set => _side.Storage = value; }
        public static BuildingDefinition[] Offers { get => _side.Offers; private set => _side.Offers = value; }
        // 상점 잠금 — 잠긴 채 다음 라운드 상점에 들어오면 진열이 그대로(산 칸만 새로). 다시 누르거나 리롤하거나 새 런이면 풀린다
        public static bool ShopLocked { get => _side.ShopLocked; private set => _side.ShopLocked = value; }
        // 전장의 맵 — 침략한 땅의 지형(Invade). 상대를 고르기 전(상점)에는 첫 맵(기본). 양 진영에 똑같이
        public static MapDefinition Map { get; private set; }

        private static BattleConfig _config;
        // 런의 설정 — 런을 한 번도 시작하지 않았으면 null. 전투 씬 밖(지도)에서도 읽는다
        public static BattleConfig Config => _config;
        // 상점 골드·진열을 이미 준 턴 — 씬만 다시 불러와도 두 번 주지 않게
        private static int _shopEnteredRound;
        // 상점 팝업이 저절로 내려온 턴 — 턴마다 한 번만(씬만 다시 불러와도 또 내려오지 않게)
        private static int _shopCueRound;

        // 상대의 탑 — 침략한 영주가 상점에서 키운 탑. 상대를 고르기 전에는 빈 탑
        public static OwnedBuilding[] EnemyField => (_opponentTower ?? PreviewTower ?? NoOpponent).Field;

        // 지도에서 고른 땅(없으면 -1) — 지도 패널과 상점 패널이 이 땅과 그 영주를 보여 준다. 남의 땅이면 적 탑 자리(EnemyField) · 적 진영 색이 그 영주의 것이 된다.
        // 보기만 한다: 상대(Opponent) · 전장(Map) · 영주 색은 Invade로 확정할 때 정해진다
        public static int PreviewRegion { get; private set; } = -1;
        public static void Preview(int region) => PreviewRegion = region;
        private static TowerState PreviewTower => PreviewRegion >= 0 ? World.TowerOf(World.State.OwnerOf(PreviewRegion)) : null;
        // 고른 땅의 영주(내 땅이거나 고른 땅이 없으면 null)와 그 영주의 탑이 영구히 쌓은 강화 — 전투 밖의 유닛 카드가 읽는다(RunStats.Preview)
        public static Lord PreviewLord => PreviewTower != null ? World.State.OwnerOf(PreviewRegion) : null;
        public static RunBook PreviewBook => PreviewTower != null ? PreviewTower.Book : null;
        // 영주 색 — 유닛 그림의 파란 자리(Core/LordPaint) · 탑 지붕 · 깃발 · 지도의 땅이 이 색이다. 나는 그린 그대로(열쇠 색)
        public static Color LordKeyColor => _config.LordKeyColor;
        public static Color ColorOf(Lord lord) => lord.IsPlayer ? _config.LordKeyColor : World.ColorOf(lord);
        // 영주의 닉네임(표시용 — 규칙은 읽지 않는다). 런 동안 영주 번호에 고정이고, 런마다 목록의 다른 자리에서 시작한다
        public static string NameOf(Lord lord) => _config.World.LordName(lord.Id, _nameOffset);
        private static int _nameOffset;
        // 적 진영 = 침략한 영주, 고르기 전에는 미리 보는 땅의 영주(없으면 흰색)
        public static Color LordColor(Team team)
        {
            if (team == Team.Ally)
                return _config.LordKeyColor;
            Lord lord = Opponent ?? (PreviewRegion >= 0 ? World.State.OwnerOf(PreviewRegion) : null);
            return lord != null ? ColorOf(lord) : Color.white;
        }
        // 진영의 국가 — 나는 고른 국가, 적은 침략한 영주(고르기 전에는 미리 보는 땅의 영주)의 빌드 방향의 국가. 없으면 공용. 본진 그림(Bases/Tower)이 읽는다
        public static BuildingNation NationOf(Team team)
        {
            if (team == Team.Ally)
                return Nation;
            Lord lord = Opponent ?? (PreviewRegion >= 0 ? World.State.OwnerOf(PreviewRegion) : null);
            if (lord == null)
                return BuildingNation.Common;
            return lord.IsPlayer ? Nation : World.Map.BuildOf(lord.Build).Nation;
        }
        // 상점 효과(골드 훅 등)를 받는 진영 — 지금 상점을 도는 쪽
        public static Team ActingTeam => _side.Team;

        public static bool IsOver => Started && (World.State.GameOver || World.State.Unified);
        public static bool Cleared => Started && World.State.Unified;

        // 플레이 모드를 새로 켤 때(도메인 리로드를 끈 설정에서도) 지난 런이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Started = false;
            _side = Player;
            CheatMap = null;
            CheatEnemyField = null;
        }

        // ── 치트 훅(Dev/CheatPanel만 부른다) — 꺼 둘 때까지(null) 라운드·런이 바뀌어도 남는다
        // 맵 고정 — 상점에 들어올 때 무작위로 뽑는 대신 이 맵
        public static MapDefinition CheatMap { get; private set; }
        // 적 보드 덮어쓰기 — 적이 상점에서 짠 탑(EnemyField) 대신 이 보드(BattleManager.Awake)
        public static OwnedBuilding[] CheatEnemyField;

        public static void CheatSetMap(MapDefinition map)
        {
            CheatMap = map;
            if (map != null)
                Map = map;
        }

        // 턴만 바꾼다 — 상점 골드·새 진열은 주지 않는다
        public static void CheatSetRound(int round)
        {
            Round = _shopEnteredRound = _shopCueRound = round;
            World.State.Turn = round + 1;
        }

        // 값을 바로 넣는다 — 골드 훅(주식시장 등) 없이. 목숨은 0(국가 위기 — 고르기 창은 뜨지 않는다)부터 시작 목숨까지
        public static void CheatSetGold(int gold) => Player.Gold = Mathf.Max(0, gold);
        public static void CheatSetLives(int lives) => World.State.Lives = Mathf.Clamp(lives, 0, LivesMax);

        // 이어진 영주 하나(땅이 가장 적은 영주)를 전투 없이 이긴 것으로 친다 — 땅 전부. 마지막 영주는 남긴다(통일은 전투로)
        public static void CheatConquer()
        {
            WorldState state = World.State;
            if (state.AiLords <= 1)
                return;
            int target = -1;
            foreach (int region in InvadableRegions())
                if (target < 0 || state.LandsOf(state.OwnerOf(region)) < state.LandsOf(state.OwnerOf(target)))
                    target = region;
            PreviewRegion = -1;
            WorldTurn.Invade(state, state.Player, target, true);
            World.Settle();
        }

        // 골드 없이 내 탑 층수만 — 다음 층 비용은 그대로. 줄이면 위층 기물은 사라진다
        public static void CheatSetFloors(int floors)
        {
            var resized = new OwnedBuilding[Mathf.Clamp(floors, 1, MaxFloors)];
            System.Array.Copy(Player.Field, resized, Mathf.Min(Player.Field.Length, resized.Length));
            Player.Field = resized;
        }

        // 골드·리롤 훅 없이 진열만 새로
        public static void CheatRollOffers() => RollOffers();

        // 런을 처음부터 — 씬은 부른 쪽이 다시 불러온다
        public static void CheatNewRun() => StartNew(_config);

        public static void StartNew(BattleConfig config)
        {
            _config = config;
            Started = true;
            Round = 0;
            Phase = RunPhase.Map;
            LastOutcome = Outcome.None;
            Opponent = null;
            _opponentTower = null;
            PreviewRegion = -1;
            foreach (TowerState side in new[] { Player, NoOpponent })
            {
                side.Reset(config);
                RunBook.Use(side.Team, side.Book);
            }
            _shopEnteredRound = _shopCueRound = -1;
            Pending.Clear();
            StatBook.ClearRun();
            TowerBook.ClearRun();
            // 닉네임이 시작하는 자리 — 규칙의 난수와 섞이지 않게 따로 뽑는다
            _nameOffset = new System.Random().Next(1000);
            // 새 런 = 새 월드 — 영주가 지역마다 하나씩, 그중 무작위 한 자리가 나
            var random = new System.Random(Random.Range(0, int.MaxValue));
            World = new WorldRun(config.World, config, random.Next(config.World.Map.RegionCount), random);
            // 내 국가는 지도의 첫 화면에서 고른다(ChooseNation) — 그 전의 임시값은 풀의 첫 국가
            NationChosen = false;
            SetNation(NationChoices[0]);
            Map = CheatMap != null ? CheatMap : ShopMap;
        }

        // 상대를 고르기 전(상점)에 깔리는 지형 — 첫 맵(기본)
        private static MapDefinition ShopMap => _config.Maps[0];

        // 이번 턴 내 수입 = 턴마다 얻는 골드(TurnGold), 직전 침략을 이겼으면 승리 보너스(AI 영주도 같은 식 + 약자 보정 — WorldRun.IncomeOf)
        public static int Income => TurnGold(Player, Lands) + (LastOutcome == Outcome.Win ? _config.WinBonus : 0);

        // 턴마다 얻는 골드 = 1라운드 상점 골드 + 땅 수 × 땅당 골드 + 업그레이드(곡창). 중상주의면 통째로 ×N(승리 보너스 · 약자 보정은 여기 밖이라 안 곱한다)
        public static int TurnGold(TowerState tower, int lands)
        {
            float gold = _config.ShopGold + lands * _config.World.GoldPerLand + UpgradeDefinition.TotalOf(tower.Upgrades, UpgradeKind.TurnGold);
            UpgradeDefinition mercantilism = tower.Special(SpecialKind.Mercantilism);
            return Mathf.RoundToInt(mercantilism != null ? gold * mercantilism.ValueAt(BuildingRarity.Mythic) : gold);
        }

        // 턴이 시작할 때 — AI 영주 전원이 상점을 한 번 돌고, 나는 수입을 받고 새 진열을 받는다(턴당 한 번 — 씬을 다시 불러와도 두 번 주지 않는다).
        // 지도에서 보는 영주의 탑이 이번 턴에 싸울 탑이 되도록, 상점에 들어갈 때가 아니라 턴이 열릴 때 한다
        // 이번 턴이 이미 열렸나(수입 · 진열을 받았나) — 지도가 첫 턴 전 대기(업그레이드 고르기)가 남았을 때 첫 턴을 열지 않으려고 묻는다
        public static bool TurnOpened => _shopEnteredRound == Round;

        public static void BeginTurn()
        {
            if (_shopEnteredRound == Round)
                return;
            _shopEnteredRound = Round;
            // 지난 턴의 상대는 여기서 내려놓는다(결과 화면까지는 그 영주의 색 · 탑이 남아 있어야 한다)
            Opponent = null;
            _opponentTower = null;
            PreviewRegion = -1;
            RunBook.Use(Team.Enemy, NoOpponent.Book);
            World.ShopPhase();
            ChargeFreeRerolls(Player);
            GainInterest();
            GainGold(Income);
            if (ShopLocked)
                RollOffers(onlyEmpty: true);
            else
                RollOffers();
        }

        // 이번 턴에 상점 팝업을 저절로 내릴 차례인가 — 턴마다 처음 한 번만 true(지도 씬이 턴을 열 때 묻는다)
        public static bool TakeShopCue()
        {
            if (_shopCueRound == Round)
                return false;
            _shopCueRound = Round;
            return true;
        }

        // 내 땅과 길로 이어진 남의 땅들 — 침략할 수 있는 곳
        public static System.Collections.Generic.List<int> InvadableRegions()
        {
            WorldState state = World.State;
            var regions = new System.Collections.Generic.List<int>();
            for (int region = 0; region < state.Owner.Length; region++)
                if (state.OwnerOf(region) == state.Player)
                    foreach (int next in state.Graph.Links[region])
                        if (state.OwnerOf(next) != state.Player && !regions.Contains(next))
                            regions.Add(next);
            return regions;
        }

        // region을 침략한다 — 그 땅의 영주가 상대, 그 땅의 지형이 전장. 씬은 부른 쪽이 다시 불러온다(Phase = Battle이라 상점 없이 전투로 뜬다)
        public static void Invade(int region)
        {
            // 고르지 않은 합치기 보상은 전투 전에 무작위로 고른 것으로 친다
            while (Pending.Count > 0)
                ChooseUpgrade(Random.Range(0, Pending[0].Choices.Length));
            Opponent = World.State.OwnerOf(region);
            _invadedRegion = region;
            _opponentTower = World.TowerOf(Opponent);
            World.SyncSpecials(Opponent);   // 그 영주가 지금 가진 땅의 지역 특수 업그레이드로 싸운다
            RunBook.Use(Team.Enemy, _opponentTower.Book);
            Map = CheatMap != null ? CheatMap : World.Map.TerrainOf(region);
            Phase = RunPhase.Battle;
            PreviewRegion = -1;
        }

        // 쉬는 턴 — 침략하지 않는다. 목숨은 그대로다. 이어서 EndTurn(AI 턴)을 부른다
        public static void Rest()
        {
            LastOutcome = Outcome.None;
            Phase = RunPhase.Aftermath;
        }

        // 침략의 결과 — 이기면 상대의 땅을 전부(연합이면 몫만큼 — 침략한 땅부터) 얻는다. 지면 목숨 −1(0이 되는 순간 국가 위기 — OfferCrisis), 위기에서 지면 게임 오버. 이어서 EndTurn(AI 턴)을 부른다
        // 지난 전투로 넘어온 땅들(졌으면 빈 목록) — 지도가 쓰러진 영주를 그 땅에 세웠다가 날린다(World/WorldManager.FellBeaten)
        public static System.Collections.Generic.IReadOnlyList<int> LastGains { get; private set; } = new int[0];

        public static void FinishBattle(bool won)
        {
            LastOutcome = won ? Outcome.Win : Outcome.Lose;
            int lives = Lives;
            LastGains = WorldTurn.Invade(World.State, World.State.Player, _invadedRegion, won);
            if (lives > 0 && Lives == 0)
                OfferCrisis();
            Phase = RunPhase.Aftermath;
            NotifyBattleWon(won ? Player : _opponentTower);
            // 지역 특수 업그레이드 — 영주를 멸망시키면(땅을 전부 차지하면) 그 영주가 가졌던 것 중 최대 셋에서 하나를 고른다(사용자 결정 10/13).
            // 고르지 않은 것 · 멸망이 아닌 승리(연합의 땅 일부)로 넘어온 땅의 것은 사라진다 — 내 땅에는 놓이지 않는다
            var fallen = new System.Collections.Generic.List<UpgradeDefinition>();
            foreach (int land in LastGains)
            {
                if (World.RegionSpecial[land] != null)
                    fallen.Add(World.RegionSpecial[land]);
                World.RegionSpecial[land] = null;
            }
            if (won && !Opponent.Alive && fallen.Count > 0)
            {
                while (fallen.Count > UpgradeChoices)
                    fallen.RemoveAt(Random.Range(0, fallen.Count));
                Pending.Add(new UpgradeOffer { Rarity = BuildingRarity.Mythic, Choices = fallen.ToArray() });
                UpgradesChanged?.Invoke();
            }
            ApplyBattleResult(won ? Player : _opponentTower, won ? _opponentTower : Player);
        }

        // 국가 위기(사용자 10/23) — 목숨이 0이 되는 순간 런에 한 번. 이번 런 지도에 놓이지 않은 지역 특수(WorldRun.UnplacedSpecials) 중 내가 안 가진 것 무작위 셋에서 하나를 고른다.
        // 고르기 창은 지도로 돌아와 AI 턴 재생이 끝난 뒤에 뜨고(UI/UpgradePicker), 건너뛸 수 없다. 후보가 없으면 위기 상태만 된다
        private static void OfferCrisis()
        {
            // 국가주의는 뺀다 — 목숨 0이라 효과가 0이다(총괄 결정 10/23)
            var pool = World.UnplacedSpecials.FindAll(special => special.Special != SpecialKind.Nationalism && !Player.Upgrades.Exists(gain => gain.Definition == special));
            while (pool.Count > UpgradeChoices)
                pool.RemoveAt(Random.Range(0, pool.Count));
            Debug.Log($"[World] 국가 위기 — 목숨 0 · 후보 {pool.Count}개(지도에 안 놓인 {World.UnplacedSpecials.Count}개 중): {string.Join(", ", pool.ConvertAll(special => special.DisplayName))}");
            if (pool.Count == 0)
                return;
            Pending.Add(new UpgradeOffer { Rarity = BuildingRarity.Mythic, Choices = pool.ToArray(), Crisis = true });
            UpgradesChanged?.Invoke();
        }

        // 이 탑이 차례인 것처럼 action을 한다 — 얻기(Acquire) 같은 상점 규칙이 그 탑에 걸린다
        private static void As(TowerState tower, System.Action action)
        {
            TowerState side = _side;
            try
            {
                _side = tower;
                action();
            }
            finally
            {
                _side = side;
            }
        }

        // 전투 한 번의 승패에 걸린 지역 특수 업그레이드 — 내 전투도 AI 영주끼리의 침략(World/WorldRun.AiPhase)도 여기를 지난다. 탑이 없는 쪽은 null
        //   제국주의: 이긴 탑이 진 탑의 필드 기물 중 무작위 하나를 등급 그대로 얻는다(공짜로 — Acquire)
        //   계몽주의: 진 탑의 쿨다운 있는 필드 기물 중 무작위 하나의 쿨다운이 영구히 ×N
        public static void ApplyBattleResult(TowerState winner, TowerState loser)
        {
            if (winner != null && loser != null && winner.Special(SpecialKind.Imperialism) != null)
            {
                OwnedBuilding[] spoils = System.Array.FindAll(loser.Field, b => !OwnedBuilding.IsEmpty(b));
                if (spoils.Length > 0)
                {
                    OwnedBuilding spoil = spoils[Random.Range(0, spoils.Length)];
                    As(winner, () => Acquire(spoil.Definition, spoil.Upgrades));
                }
            }
            UpgradeDefinition enlightenment = loser != null ? loser.Special(SpecialKind.Enlightenment) : null;
            if (enlightenment != null)
            {
                OwnedBuilding[] cooling = System.Array.FindAll(loser.Field, b => !OwnedBuilding.IsEmpty(b) && b.Definition.Cooldown > 0f);
                if (cooling.Length > 0)
                {
                    OwnedBuilding picked = cooling[Random.Range(0, cooling.Length)];
                    picked.ScaleCooldown(enlightenment.ValueAt(BuildingRarity.Mythic));
                    OwnedBuilding.NotifyEffect(picked);
                }
            }
        }

        // 가진 땅의 지역 특수 업그레이드 중 얻는 순간 한 번뿐인 것(절대왕정)을 이 탑이 처음 가졌으면 지금 받는다 — AI 영주의 상점 앞(World/WorldRun.Shop)
        public static void GrantLandSpecials(TowerState tower)
        {
            foreach (UpgradeDefinition upgrade in tower.LandSpecials)
                if (upgrade.Special == SpecialKind.MythicBuilding && tower.GrantedOnce.Add(upgrade))
                    As(tower, AcquireMythic);
        }

        // 절대왕정 — 이번 판에 등장하는 무작위 기물 1개를 신화 등급으로 얻는다(공짜 — 판매가 0골드 · 놓을 자리가 없으면 못 얻는다, 판도라의 상자와 같다)
        private static void AcquireMythic()
        {
            BuildingDefinition pick = Pool[Random.Range(0, Pool.Length)];
            Acquire(pick, BuildingRarity.Mythic - pick.Rarity);
        }

        // 기물 툴팁용(보드 없이 — 상점 · 지도에서도) — building이 든 탑(내 탑 · 침략했거나 보고 있는 영주의 탑)이 쓰는 지역 특수 업그레이드를 읽는다.
        // 징병제: 쿨다운마다 유닛을 소환하는 기물이 한 번에 더 소환하는 수 / 군주제: 이 기물이 그 탑의 맨 위층이라 발동할 때마다 효과를 2번 내는가
        public static int ExtraSummonsFor(OwnedBuilding building)
            => TowerHolding(building)?.Special(SpecialKind.Conscription) is UpgradeDefinition conscription ? Mathf.RoundToInt(conscription.ValueAt(BuildingRarity.Mythic)) : 0;

        public static bool FiresTwice(OwnedBuilding building)
        {
            TowerState tower = TowerHolding(building);
            if (tower == null || tower.Special(SpecialKind.Monarchy) == null)
                return false;
            return System.Array.FindLast(tower.Field, b => !OwnedBuilding.IsEmpty(b)) == building;
        }

        // building이 필드에 든 탑 — 내 탑, 아니면 상대(침략한 영주 · 지도에서 보는 영주)의 탑. 보관함 · 진열의 기물이면 null
        private static TowerState TowerHolding(OwnedBuilding building)
        {
            foreach (TowerState tower in new[] { Player, _opponentTower ?? PreviewTower })
                if (tower != null && System.Array.IndexOf(tower.Field, building) >= 0)
                    return tower;
            return null;
        }

        // ── 유닛 카드 · 기물 툴팁의 「걸린 업그레이드」 — 그 탑이 가진 업그레이드(합치기 보상 + 지역 특수) 중 그것에 걸리는 것(가진 순서). 보드 없이 읽는다(상점 · 지도에서도).
        // 무엇에 걸리는지는 업그레이드 정의가 답한다(UpgradeDefinition.Affects)

        // 내 유닛(enemy = 침략했거나 지도에서 보고 있는 영주의 유닛 — RunStats.Preview와 같은 뜻)
        public static System.Collections.Generic.List<UpgradeHolding> UpgradesOn(UnitDefinition unit, bool enemy = false)
        {
            var on = new System.Collections.Generic.List<UpgradeHolding>();
            TowerState tower = enemy ? OpponentOrPreviewTower() : Player;
            if (!Started || tower == null)
                return on;
            System.Collections.Generic.List<UpgradeHolding> holdings = tower.Holdings();
            var alsoAs = new System.Collections.Generic.List<UnitCategory>();
            foreach (UpgradeHolding holding in holdings)
                if (holding.Definition.AliasFor(unit) is UnitCategory category)
                    alsoAs.Add(category);
            foreach (UpgradeHolding holding in holdings)
                if (holding.Definition.Affects(unit, alsoAs))
                    on.Add(holding);
            return on;
        }

        // 가진 기물 — 탑(내 탑 · 상대 영주의 탑)에 놓인 것이면 그 탑의 업그레이드로, 보관함의 기물이면 내 업그레이드 중 자리와 무관한 것만
        public static System.Collections.Generic.List<UpgradeHolding> UpgradesOn(OwnedBuilding building)
        {
            if (OpponentOrPreviewTower() is TowerState other && System.Array.IndexOf(other.Field, building) >= 0)
                return UpgradesOn(other, building.Definition, building, other.Field);
            bool placed = System.Array.IndexOf(Player.Field, building) >= 0;
            return UpgradesOn(Player, building.Definition, building, placed ? Player.Field : null);
        }

        // 진열의 기물 — 사서 놓으면 걸릴 내 업그레이드(자리와 무관한 것만)
        public static System.Collections.Generic.List<UpgradeHolding> UpgradesOn(BuildingDefinition offer)
            => UpgradesOn(Player, offer, null, null);

        private static System.Collections.Generic.List<UpgradeHolding> UpgradesOn(TowerState tower, BuildingDefinition building, OwnedBuilding owned, OwnedBuilding[] field)
        {
            var on = new System.Collections.Generic.List<UpgradeHolding>();
            if (!Started)
                return on;
            foreach (UpgradeHolding holding in tower.Holdings())
                if (holding.Definition.Affects(building, owned, field))
                    on.Add(holding);
            return on;
        }

        // 상대(침략한 영주 · 지도에서 보는 영주)의 탑 — 그 영주가 지금 가진 땅의 지역 특수 업그레이드를 다시 적어서 돌려준다. 없으면 null
        private static TowerState OpponentOrPreviewTower()
        {
            if (!Started)
                return null;
            Lord lord = Opponent ?? PreviewLord;
            if (lord != null)
                World.SyncSpecials(lord);
            return _opponentTower ?? PreviewTower;
        }

        // 국가주의가 읽는 목숨 — 나는 지금 목숨, AI 영주는 고정값으로 친다(AI는 목숨이 없다)
        private const int AiLivesForUpgrades = 3;
        public static int LivesFor(Team team) => team == Team.Ally ? Lives : AiLivesForUpgrades;

        // 전투에서 이긴 탑의 필드 기물들에게 알린다(「전투에서 승리할 때마다,」 — 지금 쓰는 기물 없음) — 그동안 상점 규칙(얻기)은 그 탑에 걸린다.
        // 실제로 싸운 전투만이다 — AI 영주끼리의 승패(World/TowerPower)는 여기를 지나지 않는다
        private static void NotifyBattleWon(TowerState tower)
        {
            if (tower == null)
                return;
            TowerState side = _side;
            try
            {
                _side = tower;
                foreach (OwnedBuilding owned in (OwnedBuilding[])tower.Field.Clone())   // 얻은 기물이 필드에 들어오며 배열이 바뀐다
                    if (!OwnedBuilding.IsEmpty(owned))
                        foreach (BuildingPassive passive in owned.Passives)
                            passive.OnBattleWon(owned);
            }
            finally
            {
                _side = side;
            }
        }

        // 지난 EndTurn이 판도에 남긴 일을 글 몇 줄로 — 런이 끝난 턴이면 빈 글
        public static string TurnSummary { get; private set; } = "";

        // 턴을 닫는다 — AI 영주들의 침략 → 정산(반란 · 새 영주) → 최후의 연합(조건이 찼으면) → 다음 턴. 판도는 바로 끝 상태가 되고, 그 사이에 벌어진 일을 순서대로 돌려준다
        // (지도가 재생한다 — 부르기 전의 「지역 → 주인」 표에 차례로 WorldEvent.Apply하면 지금 표가 된다).
        // 국가 위기에서 졌으면 게임 오버(IsOver), 내가 통일했으면 클리어(Cleared) — 그때는 턴이 넘어가지 않는다
        public static System.Collections.Generic.List<WorldEvent> EndTurn()
        {
            WorldState state = World.State;
            var events = new System.Collections.Generic.List<WorldEvent>();
            Phase = RunPhase.Map;
            Map = CheatMap != null ? CheatMap : ShopMap;
            TurnSummary = "";
            if (state.Unified || state.GameOver)
            {
                World.Settle();
                return events;
            }
            int moved = 0, vanished = 0, before = state.Lords.Count;
            World.AiPhase((attacker, target, region, won) =>
            {
                events.Add(new WorldEvent { Lord = attacker.Id, Region = region, Won = won });
                if (won)
                    moved++;
                if (!target.Alive)
                    vanished++;
            });
            // 반란 — 정산 전에 누가 어느 땅을 들고 무너지는지 적어 두고, 정산 뒤에 그 땅의 새 영주를 읽는다
            int invasions = events.Count;
            foreach (Lord lord in state.Lords)
            {
                if (!lord.Alive || lord.IsPlayer || lord.Happiness > 0)
                    continue;
                var regions = new System.Collections.Generic.List<int>();
                for (int region = 0; region < state.Owner.Length; region++)
                    if (state.Owner[region] == lord.Id)
                        regions.Add(region);
                events.Add(new WorldEvent { Rebellion = true, Lord = lord.Id, Regions = regions.ToArray() });
            }
            World.Settle();
            for (int i = invasions; i < events.Count; i++)
                events[i].NewLords = System.Array.ConvertAll(events[i].Regions, region => state.Owner[region]);
            int rebels = events.Count - invasions, risen = state.Lords.Count - before, largest = 0;
            // 최후의 연합 — 정산이 끝난 판도에서 조건이 찼으면 남은 영주들이 하나로 뭉친다(런에 한 번)
            var merged = new System.Collections.Generic.List<Lord>();
            Lord coalition = World.FormCoalition(merged);
            if (coalition != null)
            {
                state.CoalitionMembers = merged.ConvertAll(lord => lord.Id).ToArray();
                events.Add(new WorldEvent { Coalition = true, Lord = coalition.Id, Members = state.CoalitionMembers });
            }
            foreach (Lord lord in state.Lords)
                if (lord.Alive && !lord.IsPlayer)
                    largest = Mathf.Max(largest, state.LandsOf(lord));
            Round++;
            state.Turn = Round + 1;
            Debug.Log($"[World] 턴 {Round} 끝 — 영주들의 침략 {invasions}번 · 넘어간 땅 {moved}개 · 사라진 영주 {vanished}명 · 반란 {rebels}건(새 영주 {risen}명) · 남은 영주 {state.AiLords}명{(coalition != null ? $"(최후의 연합 — {merged.Count + 1}명이 뭉침)" : "")} · 가장 큰 영주의 땅 {largest}개 · 내 땅 {Lands}개 · 내 목숨 {Lives}{(InCrisis ? "(국가 위기)" : "")}");
            // 연합이 이미 뭉친 뒤의 턴에는 AI끼리 벌어지는 일이 없다
            TurnSummary = coalition == null && state.Coalition != null ? $"최후의 연합의 땅은 {largest}개입니다."
                : $"영주들의 침략으로 땅 {moved}개의 주인이 바뀌었습니다.\n"
                + (vanished > 0 ? $"땅을 모두 잃은 영주 {vanished}명이 사라졌습니다.\n" : "")
                + (rebels > 0 ? $"반란이 {rebels}번 일어나 새 영주 {risen}명이 들어섰습니다.\n" : "")
                + (coalition != null
                    ? $"남은 영주 {merged.Count + 1}명이 최후의 연합으로 뭉쳤습니다.\n최후의 연합의 땅은 {largest}개입니다."
                    : $"남은 영주는 {state.AiLords}명이고, 가장 큰 영주의 땅은 {largest}개입니다.");
            return events;
        }

        // ShopAs로 도는 탑의 국가 — 진열의 국가 비율(HomeOffer)이 읽는다
        private static BuildingNation _shopNation;

        // 탑 한 채에 상점 한 번 — gold를 받고 새 진열에서 build 방향대로 산다(Run/EnemyShopper). 그동안 상점 규칙과 그 진영의 영구 강화는 이 탑에 걸린다. 진열의 국가 비율은 build의 국가로 건다.
        // round = 진열 등급 확률을 정하는 라운드(0부터). 월드맵 영주의 턴 상점(World/WorldRun)이 쓴다
        public static void ShopAs(TowerState tower, EnemyBuildProfile build, int gold, int round)
        {
            TowerState side = _side;
            RunBook book = RunBook.Of(tower.Team);
            int now = Round;
            try
            {
                _side = tower;
                RunBook.Use(tower.Team, tower.Book);
                Round = round;
                _shopNation = build.Nation;
                ChargeFreeRerolls(tower);
                GainInterest();
                GainGold(gold);
                RollOffers(onlyEmpty: ShopLocked);
                EnemyShopper.Shop(build);
            }
            finally
            {
                _side = side;
                RunBook.Use(tower.Team, book);
                Round = now;
            }
        }

        // ── 내 국가(기획 §4 「내 국가와 상점 풀」) — 런을 시작할 때 고른다. 민족주의가 상대 영주의 국가와 견준다
        public static BuildingNation Nation => World.PlayerNation;
        public static bool NationChosen { get; private set; }

        // 고를 수 있는 국가 — 상점 풀에 기물이 있는 국가들(공용 제외), 풀에 나온 순서
        public static BuildingNation[] NationChoices
        {
            get
            {
                var nations = new System.Collections.Generic.List<BuildingNation>();
                foreach (BuildingDefinition b in _config.ShopPool)
                    if (b.Nation != BuildingNation.Common && !nations.Contains(b.Nation))
                        nations.Add(b.Nation);
                return nations.ToArray();
            }
        }

        public static void ChooseNation(BuildingNation nation)
        {
            // 시작 때 지역 특수 업그레이드를 고르지 않는다(사용자 10/15) — 지역 특수는 영주를 멸망시켰을 때만 얻는다
            SetNation(nation);
            NationChosen = true;
        }

        // region에 놓인 지역 특수 업그레이드(없으면 null) — 지도 표시 · 툴팁. 이름 · 아이콘 · 설명은 그 정의에서(Describe(ValueAt(신화)))
        public static UpgradeDefinition SpecialAt(int region) => World.RegionSpecial[region];

        private static void SetNation(BuildingNation nation) => World.PlayerNation = nation;

        public static void ToggleShopLock() => ShopLocked = !ShopLocked;

        // 골드를 얻는다 — 얻을 때마다(금액과 상관없이 한 번) 필드 기물의 골드 훅. 기물 효과(주식시장)도 이걸로 준다
        public static void GainGold(int amount)
        {
            Gold += amount;
            ForEachFieldPassive((passive, owned) => passive.OnGoldGained(owned));
        }

        // 복리 — 턴 수입을 받기 전, 지금 차례인 탑이 가진 골드로(AI 영주도 같다)
        private static void GainInterest()
        {
            int interest = UpgradeDefinition.InterestOf(_side.Upgrades, Gold);
            if (interest > 0)
                GainGold(interest);
        }

        // 전투 중에 그 진영의 탑이 골드를 얻는다(주식시장) — 아군 = 내 탑 · 적 = 침략한 영주의 탑. 상점 밖이라 그 탑을 잠깐 지금 쪽으로 세운다
        public static void GainBattleGold(Team team, int amount)
        {
            TowerState tower = team == Team.Ally ? Player : _opponentTower;
            if (tower != null)
                As(tower, () => GainGold(amount));
        }

        // 상점 훅은 필드의 기물만 — 보관함은 효과가 없다
        private static void ForEachFieldPassive(System.Action<BuildingPassive, OwnedBuilding> call)
        {
            foreach (OwnedBuilding owned in Field)
                if (!OwnedBuilding.IsEmpty(owned))
                    foreach (BuildingPassive passive in owned.Passives)
                        call(passive, owned);
        }

        // 「구매할 때마다,」 — 산 기물(합쳐졌으면 합쳐진 결과)에서. 그다음 필드 기물의 "기물을 샀다" 훅(이동식 공장)
        private static void OnBought(OwnedBuilding owned)
        {
            BuildingPassive.TriggerBuyEffects(owned, _side.Team, 1);
            ForEachFieldPassive((passive, self) => passive.OnAnyBought(self, owned));
            // 견본품 — 「구매할 때마다,」 효과가 있는 기물을 샀으면 확률로 무료 리롤 +1회(합은 에셋의 상한 100까지)
            if (BuildingPassive.HasBuyEffects(owned.Definition) && Random.value * 100f < UpgradeDefinition.TotalOf(_side.Upgrades, UpgradeKind.BuyReroll))
                _side.FreeRerolls++;
        }

        // 이번 라운드 성 최대 체력에 더할 값(양 진영) — 런 밖이면 0
        public static float BaseHpBonus => Started ? _config.BaseHpBonus(Round) : 0f;

        // 탑 — 필드 칸 하나가 한 층(칸 0 = 1층, 성 바로 위). 층 올리기로 골드를 내고 맨 위에 빈 층을 하나 더한다
        public static int Floors => Field.Length;
        public static int FloorRaises { get => _side.FloorRaises; private set => _side.FloorRaises = value; }
        public static int MaxFloors => _config.MaxFloors;
        public static int NextFloorCost => _config.FloorCost(FloorRaises);
        public static bool CanRaiseFloor => Floors < _config.MaxFloors && Gold >= NextFloorCost;

        public static void RaiseFloor()
        {
            if (!CanRaiseFloor)
                return;
            Gold -= NextFloorCost;
            FloorRaises++;
            var raised = new OwnedBuilding[Field.Length + 1];
            System.Array.Copy(Field, raised, Field.Length);
            Field = raised;
        }

        // 무료 리롤(업그레이드 「암시장」)이 남아 있으면 그것부터 쓴다 — 그동안 리롤 값은 0골드
        public static int FreeRerolls => _side.FreeRerolls;
        public static int RerollCost => FreeRerolls > 0 ? 0 : CommunismPrice ?? _config.RerollCost;
        // 공산주의 — 지금 차례인 탑이 가졌으면 상점의 모든 기물과 리롤이 이 값(아니면 null). 층 올리기 값 · 판매가는 그대로
        private static int? CommunismPrice
            => _side.Special(SpecialKind.Communism) is UpgradeDefinition communism ? Mathf.RoundToInt(communism.ValueAt(BuildingRarity.Mythic)) : (int?)null;
        public static bool CanReroll => Gold >= RerollCost;

        // 턴 상점이 열릴 때 — 남은 무료 리롤을 그 탑의 「턴마다 +N회」로 다시 채운다(지난 턴에 안 쓴 것은 넘어가지 않는다)
        private static void ChargeFreeRerolls(TowerState tower)
            => tower.FreeRerolls = Mathf.RoundToInt(UpgradeDefinition.TotalOf(tower.Upgrades, UpgradeKind.FreeReroll));

        public static void Reroll()
        {
            if (!CanReroll)
                return;
            Gold -= RerollCost;
            if (FreeRerolls > 0)
                _side.FreeRerolls--;
            ShopLocked = false;
            RollOffers();
            ForEachFieldPassive((passive, owned) => passive.OnReroll(owned));
        }

        public static int PriceOf(BuildingDefinition definition) => CommunismPrice ?? _config.PriceOf(definition.Rarity);

        // 진열 칸마다 그 기물이 시작 등급에서 오른 횟수(중립외교 — 보통 0, 한 등급 오른 칸은 1). 값은 시작 등급 값 그대로고, 사면 그 등급으로 들어온다(합치기는 같은 등급끼리)
        public static int[] OfferUpgrades => _side.OfferUpgrades;

        // 칸마다 등급을 가중치로 먼저 뽑고(풀에 없는 등급은 빼고), 그 등급의 건물 중 하나. onlyEmpty = 산 칸(빈칸)만 새로(상점 잠금).
        // 내 상점도 AI 영주의 상점도 그 등급 안에서 국가 비율을 건다(HomeOffer)
        private static void RollOffers(bool onlyEmpty = false)
        {
            BuildingDefinition[] pool = Pool;
            for (int i = 0; i < Offers.Length; i++)
            {
                if (onlyEmpty && Offers[i] != null)
                    continue;
                Offers[i] = HomeOffer(pool);
                // 중립외교 — 일정 확률로 한 등급 오른 채 나온다. 전설은 오르지 않는다(신화는 상점에 나오지 않는다 — 사용자 결정 10/12)
                OfferUpgrades[i] = _side.Special(SpecialKind.NeutralDiplomacy) is UpgradeDefinition diplomacy && Offers[i].Rarity < BuildingRarity.Legendary
                                   && Random.value * 100f < diplomacy.ValueAt(BuildingRarity.Mythic) ? 1 : 0;
            }
        }

        // 진열 한 칸 — 등급을 먼저 뽑고(등급 확률 표가 우선), 그 등급 안에서 HomeNationShare 확률로 「공용 + 그 탑의 국가」 기물,
        // 나머지 확률로 다른 국가들의 기물. 그 등급에 한쪽 기물이 없으면 다른 쪽에서 뽑는다(등급을 건너뛰지 않는다)
        private static BuildingDefinition HomeOffer(BuildingDefinition[] pool)
        {
            float total = 0f;
            foreach (BuildingRarity rarity in System.Enum.GetValues(typeof(BuildingRarity)))
                if (CountOf(pool, rarity) > 0)
                    total += _config.RarityWeight(rarity, ShopTier);
            float pick = Random.Range(0f, total);
            BuildingRarity rolled = pool[pool.Length - 1].Rarity;
            foreach (BuildingRarity rarity in System.Enum.GetValues(typeof(BuildingRarity)))
            {
                if (CountOf(pool, rarity) == 0)
                    continue;
                pick -= _config.RarityWeight(rarity, ShopTier);
                if (pick < 0f)
                {
                    rolled = rarity;
                    break;
                }
            }
            bool home = Random.value < _config.HomeNationShare;
            BuildingDefinition[] group = System.Array.FindAll(pool, b => b.Rarity == rolled && IsHome(b) == home);
            if (group.Length == 0)
                group = System.Array.FindAll(pool, b => b.Rarity == rolled);
            return group[Random.Range(0, group.Length)];
        }

        // 공용이거나 지금 상점을 도는 탑의 국가(내 차례면 내 국가, 영주의 차례면 그 영주의 빌드 방향의 국가) 기물인가
        private static bool IsHome(BuildingDefinition building)
            => building.Nation == BuildingNation.Common || building.Nation == SideNation;

        // 지금 상점을 도는 탑의 국가 — 내 차례면 내 국가, 영주의 차례(ShopAs)면 그 영주의 빌드 방향의 국가. 진열 비율 · 국가 전용 업그레이드가 읽는다
        private static BuildingNation SideNation => _side == Player ? Nation : _shopNation;

        // 진열 한 칸에서 b가 뽑힐 가중치(국가 비율을 걸기 전 — 등급 비율만) — 화면의 등급 확률(OfferChance)이 쓴다
        private static float OfferWeight(BuildingDefinition[] pool, BuildingDefinition b)
            => _config.RarityWeight(b.Rarity, ShopTier) / CountOf(pool, b.Rarity);

        // 상점 등급 단계 — 내 상점은 그 턴의 단계(WorldConfig.ShopTier — 턴 수만으로 오른다). 다른 탑의 차례(ShopAs)에는 넘겨받은 단계 그대로(상한까지)
        private static int ShopTier
            => Mathf.Min(_side == Player ? _config.World.ShopTier(Round) : Round, _config.World.ShopTierMax);

        // 지금 라운드에 진열 한 칸이 rarity 등급일 확률(0~1) — 풀에 없는 등급은 0
        public static float OfferChance(BuildingRarity rarity)
        {
            BuildingDefinition[] pool = Pool;
            float total = 0f, mine = 0f;
            foreach (BuildingDefinition b in pool)
            {
                float weight = OfferWeight(pool, b);
                total += weight;
                if (b.Rarity == rarity)
                    mine += weight;
            }
            return total > 0f ? mine / total : 0f;
        }

        private static int CountOf(BuildingDefinition[] pool, BuildingRarity rarity)
        {
            int count = 0;
            foreach (BuildingDefinition b in pool)
                if (b.Rarity == rarity)
                    count++;
            return count;
        }

        // 가진 것(필드·보관함) 중 같은 기물·같은 등급이면서 더 오를 수 있는 것 — 새로 산 시작 등급 기물이 여기에 합쳐진다
        public static OwnedBuilding FindMergeTarget(BuildingDefinition definition, int upgrades, OwnedBuilding exclude = null)
        {
            foreach (OwnedBuilding[] row in new[] { Field, Storage })
                foreach (OwnedBuilding b in row)
                    if (!OwnedBuilding.IsEmpty(b) && b != exclude && b.Definition == definition && b.Upgrades == upgrades && b.CanUpgrade)
                        return b;
            return null;
        }

        // 합쳐서 오른 결과가 또 같은 등급 짝을 만나면 계속 합친다 — 결과는 먼저 있던 짝의 칸에 남는다(플레이어가 놓은 자리).
        // 스택은 합친다(사용자 결정: 등급이 오르면 스택 유지). 합쳐진 결과를 돌려준다.
        // spent = 방금 산 조각에 낸 골드 — 쓴 골드도 결과 한 채에 모인다(판매가의 바탕, RunState.SellValue)
        private static OwnedBuilding MergeInto(OwnedBuilding target, int spent)
        {
            target.AddSpent(spent);
            // 내 상점이면 합치기를 한 번씩 적어 둔다 — 화면이 차례로 보여 준다(상태는 여기서 한 번에 끝난다)
            var steps = _side == Player && Merged != null ? new System.Collections.Generic.List<MergeStep>() : null;
            OwnedBuilding[] field = steps != null ? (OwnedBuilding[])Field.Clone() : null, storage = steps != null ? (OwnedBuilding[])Storage.Clone() : null;
            // 합치기 보상은 연쇄가 이어져도 하나 — 마지막 단계의 재료 등급으로(사용자 결정 10/13: 일반 → 고급 → 희귀로 이어지면 고급 하나)
            BuildingRarity material = target.Rarity;
            target.Upgrade();
            steps?.Add(new MergeStep(target, field, storage));
            OwnedBuilding pair = FindMergeTarget(target.Definition, target.Upgrades, target);
            while (pair != null && target.CanUpgrade)
            {
                Remove(target);
                material = pair.Rarity;
                pair.Upgrade();
                pair.AddStacks(target.Stacks);
                pair.AddSpent(target.SpentGold);
                pair.ScaleCooldown(target.CooldownScale);
                target = pair;
                steps?.Add(new MergeStep(target, field, storage));
                pair = FindMergeTarget(target.Definition, target.Upgrades, target);
            }
            OfferUpgrade(material);
            if (steps != null)
                Merged(steps);
            if (_side == Player)
                UpgradesChanged?.Invoke();
            return target;
        }

        // ── 합치기 보상 업그레이드(아웃게임기획 §4) — 합치기 한 번(연쇄는 통틀어 한 번)마다 재료 등급의 업그레이드 셋 중 하나. 얻은 것은 그 탑에 런 동안 남는다(TowerState.Upgrades)
        // 고르기를 기다리는 한 번 — 합친 재료의 등급과 후보(서로 다른 종, 많아야 UpgradeChoices개)
        public class UpgradeOffer
        {
            public BuildingRarity Rarity;
            public UpgradeDefinition[] Choices;
            // 공화정 — 후보를 이미 모두 얻었다. 화면은 보여 주기만 하고, 닫아도(ChooseUpgrade) 다시 얻지 않는다
            public bool Granted;
            // 국가 위기(OfferCrisis) — 화면은 위기 머리말을 쓰고 건너뛰기를 숨긴다
            public bool Crisis;
        }

        private const int UpgradeChoices = 3;
        private static readonly System.Collections.Generic.List<UpgradeOffer> Pending = new System.Collections.Generic.List<UpgradeOffer>();

        // 내가 아직 고르지 않은 합치기 보상 — 일어난 순서대로. 화면은 맨 앞([0])부터 하나씩 고르게 한다(ChooseUpgrade)
        public static System.Collections.Generic.IReadOnlyList<UpgradeOffer> PendingUpgrades => Pending;
        // 대기열이나 내가 가진 업그레이드가 바뀌었다 — 내 상점의 합치기(연쇄 포함)가 끝난 뒤(Merged 다음) · 고른 뒤
        public static event System.Action UpgradesChanged;

        // 맨 앞 대기의 choice번째 후보를 고른다 — 내 탑이 그 등급으로 얻는다
        public static void ChooseUpgrade(int choice)
        {
            if (Pending.Count == 0 || choice < 0 || choice >= Pending[0].Choices.Length)
                return;
            UpgradeOffer offer = Pending[0];
            Pending.RemoveAt(0);
            if (!offer.Granted)
                GainUpgrade(offer.Choices[choice], offer.Rarity);
            UpgradesChanged?.Invoke();
        }

        // 맨 앞 대기가 지역 특수(신화) 고르기면 고르지 않고 골드를 받을 수 있다 — 플레이어만(AI · Invade 직전 자동 고르기는 쓰지 않는다, 사용자 10/15). 국가 위기는 반드시 고른다
        public static bool CanSkipUpgrade => Pending.Count > 0 && Pending[0].Rarity == BuildingRarity.Mythic && !Pending[0].Granted && !Pending[0].Crisis;
        public static int UpgradeSkipGold => _config.World.SpecialSkipGold;

        public static void SkipUpgrade()
        {
            if (!CanSkipUpgrade)
                return;
            Pending.RemoveAt(0);
            As(Player, () => GainGold(UpgradeSkipGold));
            UpgradesChanged?.Invoke();
        }

        // 지금 차례인 탑이 업그레이드 하나를 얻는다 — 운영 계열은 얻은 그 턴부터다(사용자 결정 10/13): 곡창은 그 등급의 골드를 바로 받고(골드를 얻은 것이라 골드 훅도 울린다),
        // 암시장은 그 등급의 횟수가 이번 턴 남은 무료 리롤에 더해진다. 다음 턴부터는 턴 수입 · 턴 시작 충전에 합쳐진 값으로 붙는다
        private static void GainUpgrade(UpgradeDefinition upgrade, BuildingRarity rarity)
        {
            _side.Upgrades.Add(new UpgradeGain { Definition = upgrade, Rarity = rarity });
            int value = Mathf.RoundToInt(upgrade.ValueAt(rarity));
            if (upgrade.Kind == UpgradeKind.TurnGold)
                GainGold(value);
            else if (upgrade.Kind == UpgradeKind.FreeReroll)
                _side.FreeRerolls += value;
            else if (upgrade.Special == SpecialKind.MythicBuilding)
                AcquireMythic();
        }

        // lord의 탑이 가진 업그레이드 — 종류별로 얻은 횟수 · 합쳐진 값 · 설명(처음 얻은 순서). 탑이 없는 영주면 빈 목록
        public static System.Collections.Generic.List<UpgradeHolding> UpgradesOf(Lord lord)
        {
            World.SyncSpecials(lord);
            TowerState tower = lord.IsPlayer ? Player : World.TowerOf(lord);
            return tower != null ? tower.Holdings() : new System.Collections.Generic.List<UpgradeHolding>();
        }

        // 이번 전투에서 team의 탑(내 탑 · 침략한 영주의 탑)이 가진 업그레이드 — 읽기만(전투 시작 알림 Fx/BattleStartLines)
        public static System.Collections.Generic.List<UpgradeHolding> HoldingsFor(Team team)
        {
            TowerState tower = team == Team.Ally ? Player : _opponentTower;
            return tower != null ? tower.Holdings() : new System.Collections.Generic.List<UpgradeHolding>();
        }

        // 전투 시작 — team의 탑(내 탑 · 침략한 영주의 탑)이 가진 업그레이드를 건다. spawns = 보드가 돈 뒤의 차례(소환 · 작은 타워), 아니면 그 앞(스탯 · 효과)
        public static void ApplyUpgrades(Team team, bool spawns)
        {
            TowerState tower = team == Team.Ally ? Player : _opponentTower;
            if (tower == null)
                return;
            foreach (UpgradeHolding holding in tower.Holdings())
                if (spawns)
                    holding.Definition.Spawn(team, holding.Value, holding.Count);
                else
                    holding.Definition.Apply(team, holding.Value, holding.Count);
        }

        // 합치기 한 번(연쇄 포함) — material = 마지막으로 합친 재료의 등급. 내 탑이면 대기열에 쌓고(화면이 고르게 한다), 다른 탑(AI 영주)은 셋 중 무작위로 바로 얻는다.
        // 공짜로 받은 기물(Acquire)은 합치기가 아니라 여기를 지나지 않는다
        private static void OfferUpgrade(BuildingRarity material)
        {
            // 공용 + 그 탑의 국가 것(국가 전용 — 아웃게임기획 §4, 총괄 10/21). 한 풀에서 가중치 없이 고르게
            var pool = new System.Collections.Generic.List<UpgradeDefinition>();
            foreach (UpgradeDefinition upgrade in _config.Upgrades)
                if (upgrade.InPool && upgrade.AvailableAt(material) && (upgrade.Nation == BuildingNation.Common || upgrade.Nation == SideNation))
                    pool.Add(upgrade);
            var choices = new UpgradeDefinition[Mathf.Min(UpgradeChoices, pool.Count)];
            for (int i = 0; i < choices.Length; i++)
            {
                int pick = Random.Range(0, pool.Count);
                choices[i] = pool[pick];
                pool.RemoveAt(pick);
            }
            if (choices.Length == 0)
                return;
            // 공화정 — 고르지 않고 후보를 모두 얻는다. 내 탑이면 얻은 것을 보여 주는 항목을 대기열에 싣는다(사용자 10/14 — 조용히 넣지 않는다)
            if (_side.Special(SpecialKind.Republic) != null)
            {
                foreach (UpgradeDefinition choice in choices)
                    GainUpgrade(choice, material);
                if (_side == Player)
                    Pending.Add(new UpgradeOffer { Rarity = material, Choices = choices, Granted = true });
            }
            else if (_side == Player)
                Pending.Add(new UpgradeOffer { Rarity = material, Choices = choices });
            else
                GainUpgrade(choices[Random.Range(0, choices.Length)], material);
        }

        // 합치기 한 번 — 들어온 조각(첫 단계는 방금 산 것, 그다음은 앞 단계의 결과)이 Into에 합쳐져 Into가 After 등급이 됐다.
        // 자리(Storage · Index)는 합치기가 시작되기 전의 탑 · 보관함 기준이다 — 화면은 아직 그 모습이다
        public readonly struct MergeStep
        {
            public readonly OwnedBuilding Into;
            public readonly bool Storage;
            public readonly int Index;
            public readonly BuildingRarity After;

            public MergeStep(OwnedBuilding into, OwnedBuilding[] field, OwnedBuilding[] storage)
            {
                Into = into;
                After = into.Rarity;
                int index = System.Array.IndexOf(field, into);
                Storage = index < 0;
                Index = Storage ? System.Array.IndexOf(storage, into) : index;
            }
        }

        // 내 상점에서 합치기(연쇄 포함)가 끝났다 — 단계들을 일어난 순서대로. 결과 기물 = 마지막 단계의 Into
        public static event System.Action<System.Collections.Generic.IReadOnlyList<MergeStep>> Merged;

        private static void Remove(OwnedBuilding building)
        {
            foreach (OwnedBuilding[] row in new[] { Field, Storage })
                for (int i = 0; i < row.Length; i++)
                    if (row[i] == building)
                        row[i] = null;
            CompactField();
        }

        // 탑은 중간 층을 비울 수 없다(사용자 10/8) — 기물은 늘 1층부터 빈틈없이. 빠진 자리는 위층이 내려와 메운다
        private static void CompactField()
        {
            int write = 0;
            for (int read = 0; read < Field.Length; read++)
                if (!OwnedBuilding.IsEmpty(Field[read]))
                    Field[write++] = Field[read];
            for (; write < Field.Length; write++)
                Field[write] = null;
        }

        public static bool CanBuy(int offer)
        {
            BuildingDefinition definition = Offers[offer];
            if (definition == null || Gold < PriceOf(definition))
                return false;
            return FindMergeTarget(definition, OfferUpgrades[offer]) != null || FirstEmpty(Field) >= 0 || FirstEmpty(Storage) >= 0;
        }

        public static void Buy(int offer)
        {
            if (!CanBuy(offer))
                return;
            BuildingDefinition definition = Offers[offer];
            int price = PriceOf(definition);
            Gold -= price;
            Offers[offer] = null;

            OwnedBuilding target = FindMergeTarget(definition, OfferUpgrades[offer]);
            if (target != null)
            {
                OnBought(MergeInto(target, price));
                return;
            }
            OwnedBuilding bought = NewBought(definition, offer, price);
            int slot = FirstEmpty(Field);
            if (slot >= 0)
                Field[slot] = bought;
            else
                Storage[FirstEmpty(Storage)] = bought;
            OnBought(bought);
        }

        // 진열을 끌어다 칸에 놓아 산다 — 합쳐질 짝이 있으면 어느 칸에 놓든 합쳐지고, 아니면 보관함은 빈 칸에만,
        // 탑은 빈 층이 하나라도 있으면 그 층에 끼워 넣는다(위층들이 한 층씩 올라간다 · 맨 위 빈 층보다 높이 놓으면 맨 위 기물 바로 위로)
        public static bool CanBuyInto(int offer, bool storage, int index)
        {
            BuildingDefinition definition = Offers[offer];
            if (definition == null || Gold < PriceOf(definition))
                return false;
            if (FindMergeTarget(definition, OfferUpgrades[offer]) != null)
                return true;
            return storage ? OwnedBuilding.IsEmpty(Storage[index]) : FirstEmpty(Field) >= 0;
        }

        public static void BuyInto(int offer, bool storage, int index)
        {
            if (!CanBuyInto(offer, storage, index))
                return;
            BuildingDefinition definition = Offers[offer];
            int price = PriceOf(definition);
            Gold -= price;
            Offers[offer] = null;

            OwnedBuilding target = FindMergeTarget(definition, OfferUpgrades[offer]);
            OwnedBuilding bought;
            if (target != null)
                bought = MergeInto(target, price);
            else
            {
                bought = NewBought(definition, offer, price);
                if (storage)
                    Storage[index] = bought;
                else
                    InsertIntoField(bought, index);
            }
            OnBought(bought);
        }

        // 층 사이에 끼워 넣기(사용자 10/8: 1 2 3 4 5의 1층·2층 사이에 A → 1 A 2 3 4 5) — at = 넣은 뒤 그 기물의 칸(0..Floors).
        // at 위 기물은 한 칸씩 올라간다. 합치지 않는다(합치기는 층 가운데에 놓을 때). 밀어 올릴 빈 층이 있어야 한다
        public static bool CanBuyInsert(int offer, int at)
        {
            BuildingDefinition definition = Offers[offer];
            return definition != null && Gold >= PriceOf(definition) && at >= 0 && at <= Floors && FirstEmpty(Field) >= 0;
        }

        public static void BuyInsert(int offer, int at)
        {
            if (!CanBuyInsert(offer, at))
                return;
            BuildingDefinition definition = Offers[offer];
            int price = PriceOf(definition);
            Gold -= price;
            Offers[offer] = null;
            OwnedBuilding bought = NewBought(definition, offer, price);
            InsertIntoField(bought, at);
            OnBought(bought);
        }

        // 방금 산 진열 칸의 기물 한 채 — 그 칸이 오른 채 나왔으면(중립외교) 그 등급으로. 낸 골드를 적는다
        private static OwnedBuilding NewBought(BuildingDefinition definition, int offer, int price)
        {
            var bought = new OwnedBuilding(definition);
            for (int i = 0; i < OfferUpgrades[offer]; i++)
                bought.Upgrade();
            bought.AddSpent(price);
            return bought;
        }

        // 보관함이나 탑의 다른 층에서 옮겨 끼워 넣기 — 탑 안 이동은 원래 자리를 빼고 넣는다(빈 층이 없어도 된다).
        // at은 빼기 전 기준 칸 번호(1 2 3 4 5에서 4를 at 1로 → 1 4 2 3 5)
        public static bool CanMoveInsert(bool fromStorage, int from, int at)
        {
            OwnedBuilding[] row = fromStorage ? Storage : Field;
            if (from < 0 || from >= row.Length || OwnedBuilding.IsEmpty(row[from]) || at < 0 || at > Floors)
                return false;
            return !fromStorage || FirstEmpty(Field) >= 0;
        }

        public static void MoveInsert(bool fromStorage, int from, int at)
        {
            if (!CanMoveInsert(fromStorage, from, at))
                return;
            OwnedBuilding[] row = fromStorage ? Storage : Field;
            OwnedBuilding moved = row[from];
            row[from] = null;
            if (!fromStorage)
            {
                CompactField();
                if (at > from)
                    at--;
            }
            InsertIntoField(moved, at);
        }

        // 탑의 index층에 끼워 넣는다 — 그 위 기물들은 한 층씩 올라간다. 빈 층이 있어야 한다
        private static void InsertIntoField(OwnedBuilding building, int index)
        {
            int top = FirstEmpty(Field);
            index = Mathf.Min(index, top);
            for (int i = top; i > index; i--)
                Field[i] = Field[i - 1];
            Field[index] = building;
        }

        // 판매가 = 그 기물을 모으는 데 쓴 골드의 절반(사용자 결정 10/13 — 바토몬 방식).
        // 사서 모은 기물은 쓴 골드가 등급가 × 합친 개수라 전과 같은 숫자다. 공짜로 받은 기물(Acquire)만 0골드다 — 바닥을 두지 않는다
        public static int SellValue(OwnedBuilding building)
        {
            return Mathf.FloorToInt(building.SpentGold * _config.SellRatio);
        }

        public static void Sell(bool storage, int index)
        {
            OwnedBuilding[] row = storage ? Storage : Field;
            if (OwnedBuilding.IsEmpty(row[index]))
                return;
            OwnedBuilding sold = row[index];
            int value = SellValue(sold);
            row[index] = null;   // 판 기물은 자기 골드 훅을 받지 않는다 · 스택도 같이 사라진다
            CompactField();
            GainGold(value);
            foreach (BuildingPassive passive in sold.Passives)
                passive.OnSell(sold);
        }

        // 이번 판에 등장하는 기물(상점 풀) — 내 상점도 AI 영주의 상점도 같은 풀이다(내 진열은 국가 비율을 건다 — HomeOffer)
        public static BuildingDefinition[] Pool => _config.ShopPool;

        // 산 게 아니라 얻는다(판도라의 상자) — 「구매할 때마다,」·합치기 없이 필드 → 보관함 빈 칸에. 자리가 없으면 못 얻는다.
        // 쓴 골드가 0이라 판매가도 0골드다(사용자 결정 10/13)
        // upgrades = 시작 등급에서 올려서 준다(판도라의 상자 신화 — 신화 등급으로 온다). 정의의 등급은 그대로다
        public static void Acquire(BuildingDefinition definition, int upgrades = 0)
        {
            var acquired = new OwnedBuilding(definition);
            for (int i = 0; i < upgrades; i++)
                acquired.Upgrade();
            int slot = FirstEmpty(Field);
            if (slot >= 0)
                Field[slot] = acquired;
            else if ((slot = FirstEmpty(Storage)) >= 0)
                Storage[slot] = acquired;
            else
                return;
            OwnedBuilding.NotifyEffect(acquired);   // 받은 칸이 튄다(판도라의 상자 — 판 기물은 이미 없다)
        }

        // 두 칸을 맞바꾼다 — 필드↔보관함도, 빈 칸과도. 탑에 빈 층이 생기면 위층이 내려온다
        public static void Swap(bool storageA, int indexA, bool storageB, int indexB)
        {
            OwnedBuilding[] rowA = storageA ? Storage : Field;
            OwnedBuilding[] rowB = storageB ? Storage : Field;
            (rowA[indexA], rowB[indexB]) = (rowB[indexB], rowA[indexA]);
            CompactField();
        }

        private static int FirstEmpty(OwnedBuilding[] row)
        {
            for (int i = 0; i < row.Length; i++)
                if (OwnedBuilding.IsEmpty(row[i]))
                    return i;
            return -1;
        }
    }
}
