namespace GnorpWar
{
    // 상점을 도는 한 쪽의 탑 한 채 — 골드 · 탑(필드) · 보관함 · 진열 · 그 탑이 「영구히」 쌓은 강화(RunBook). 스택은 기물(OwnedBuilding)에 있다.
    // 플레이어와 적이 하나씩 갖고(RunState), 월드맵에서는 영주마다 하나씩 갖는다. 상점 규칙(구매·판매·리롤·층 올리기)은 RunState가
    // 「지금 차례인 탑」에 건다 — RunState.ShopAs
    public class TowerState
    {
        public readonly Team Team;
        public int Gold;
        public OwnedBuilding[] Field;
        public OwnedBuilding[] Storage;
        public BuildingDefinition[] Offers;
        public bool ShopLocked;
        public int FloorRaises;
        public readonly RunBook Book = new RunBook();
        // 이 탑이 합치기로 얻은 업그레이드(얻은 순서) · 이번 턴에 남은 무료 리롤(암시장 — 턴 상점이 열릴 때 채운다)
        public readonly System.Collections.Generic.List<UpgradeGain> Upgrades = new System.Collections.Generic.List<UpgradeGain>();
        public int FreeRerolls;
        // 이 탑의 영주가 지금 가진 땅에 놓인 지역 특수 업그레이드(AI 영주 — 가진 동안만 쓴다. World/WorldRun.SyncSpecials가 채운다). 내가 얻은 것은 Upgrades에 든다
        public readonly System.Collections.Generic.List<UpgradeDefinition> LandSpecials = new System.Collections.Generic.List<UpgradeDefinition>();
        // 얻는 순간 한 번뿐인 것(절대왕정)을 이미 받은 땅 업그레이드 — 땅을 잃었다 되찾아도 다시 받지 않는다
        public readonly System.Collections.Generic.HashSet<UpgradeDefinition> GrantedOnce = new System.Collections.Generic.HashSet<UpgradeDefinition>();
        // 진열 칸마다 그 기물이 시작 등급에서 오른 횟수(중립외교 — 보통 0)
        public int[] OfferUpgrades;

        public TowerState(Team team) => Team = team;

        // 이 탑이 쓰는 그 종류의 지역 특수 업그레이드(얻은 것 · 가진 땅의 것) — 없으면 null
        public UpgradeDefinition Special(SpecialKind kind)
        {
            foreach (UpgradeGain gain in Upgrades)
                if (gain.Definition.Special == kind)
                    return gain.Definition;
            foreach (UpgradeDefinition upgrade in LandSpecials)
                if (upgrade.Special == kind)
                    return upgrade;
            return null;
        }

        // 이 탑이 지금 쓰는 업그레이드 전부 — 얻은 것(종류별로 합친 값) 뒤에 가진 땅의 지역 특수 업그레이드(신화 등급)
        public System.Collections.Generic.List<UpgradeHolding> Holdings()
        {
            System.Collections.Generic.List<UpgradeHolding> holdings = UpgradeDefinition.Summarize(Upgrades);
            foreach (UpgradeDefinition upgrade in LandSpecials)
                holdings.Add(new UpgradeHolding { Definition = upgrade, Count = 1, Rarity = BuildingRarity.Mythic, Value = upgrade.ValueAt(BuildingRarity.Mythic) });
            return holdings;
        }

        // 빈 시작 탑으로 — 영구 강화(Book)는 건드리지 않는다
        public void Reset(BattleConfig config)
        {
            Upgrades.Clear();
            LandSpecials.Clear();
            GrantedOnce.Clear();
            OfferUpgrades = new int[config.OfferCount];
            FreeRerolls = 0;
            Gold = 0;
            Field = new OwnedBuilding[config.StartFloors];
            FloorRaises = 0;
            Storage = new OwnedBuilding[config.StorageSlots];
            Offers = new BuildingDefinition[config.OfferCount];
            ShopLocked = false;
        }
    }
}
