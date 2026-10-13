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

        public TowerState(Team team) => Team = team;

        // 빈 시작 탑으로 — 영구 강화(Book)는 건드리지 않는다
        public void Reset(BattleConfig config)
        {
            Upgrades.Clear();
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
