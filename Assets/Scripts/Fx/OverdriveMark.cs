using UnityEngine;

namespace GnorpWar
{
    // 데스매치에서도 타워가 쏘게 하는 기물(TowerDeathmatchPassive — 영구기관 오버드라이브)의 층에 뜨는 번개 표시.
    // 데스매치부터 전투가 끝날 때까지, 그 진영의 효과가 켜져 있을 때만. 가속 표시와 같은 자리·같은 크기, 숫자 없이 아이콘만 —
    // 전장 탑(Tower의 TowerBadge)과 UI 탑 칸(SlotView)이 이 판정·색을 같이 쓴다
    public static class OverdriveMark
    {
        public static readonly Color Color = new Color(1f, 0.9f, 0.2f);

        public static bool On(OwnedBuilding building, Team team)
        {
            if (OwnedBuilding.IsEmpty(building) || !BattleManager.TowersStopped || !TowerBook.AttacksInDeathmatch(team))
                return false;
            foreach (BuildingPassive passive in building.Definition.Passives)
                if (passive is TowerDeathmatchPassive)
                    return true;
            return false;
        }
    }
}
