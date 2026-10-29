using System.Collections.Generic;

namespace GnorpWar
{
    // 기물이 효과를 주는 내 다른 기물(같은 탑의 층) — 툴팁을 띄운 기물의 대상 표시(UI 탑 · 전장 탑, 사용자 10/27 「더 바자르처럼」)가 읽는다.
    // 능력마다의 규칙은 그 능력의 TargetsFloor(BuildingAction · BuildingPassive · UnitEffect) — 기본은 자리 규칙(Reaches)이고, 자리 밖 대상(가장 위 · 무작위 · 모든)은 능력이 덮어쓴다.
    // 무작위 대상은 후보 전부. 유닛 · 타워 · 골드에만 걸리는 능력은 대상이 없다
    public static class BuildingTargets
    {
        // field의 slot층 기물이 효과를 주는 층들(자기 자신 · 빈 층 제외)을 into에 담는다. federated = 그 탑에 연방제가 걸려 있나(자리 효과가 내 다른 모든 기물에)
        public static void Of(OwnedBuilding[] field, int slot, bool federated, List<int> into)
        {
            into.Clear();
            if (field == null || slot < 0 || slot >= field.Length || OwnedBuilding.IsEmpty(field[slot]))
                return;
            OwnedBuilding self = field[slot];
            for (int other = 0; other < field.Length; other++)
                if (other != slot && !OwnedBuilding.IsEmpty(field[other]) && Hits(self, field, slot, other, federated))
                    into.Add(other);
        }

        // 아직 탑에 없는 기물(상점 진열 · 보관함, 사용자 10/29) — 자리와 상관없이 대상이 정해지는 능력(SlotFree)만 field의 층에 표시한다.
        // 능력은 자기 자리를 읽으므로(가장 위 찾기의 건너뛰기 등) field 맨 위 바깥 칸에 둔 셈으로 묻는다
        public static void OfUnplaced(OwnedBuilding[] field, OwnedBuilding self, List<int> into)
        {
            into.Clear();
            if (field == null || OwnedBuilding.IsEmpty(self))
                return;
            var placed = new OwnedBuilding[field.Length + 1];
            field.CopyTo(placed, 0);
            placed[field.Length] = self;
            for (int other = 0; other < field.Length; other++)
                if (!OwnedBuilding.IsEmpty(field[other]) && Hits(self, placed, field.Length, other, false, true))
                    into.Add(other);
        }

        private static bool Hits(OwnedBuilding self, OwnedBuilding[] field, int slot, int other, bool federated, bool slotFreeOnly = false)
        {
            foreach (BuildingAction action in self.Actions)
                if ((!slotFreeOnly || action.SlotFree) && action.TargetsFloor(field, slot, other, federated))
                    return true;
            foreach (BuildingPassive passive in self.Passives)
                if ((!slotFreeOnly || passive.SlotFree) && passive.TargetsFloor(field, slot, other, federated))
                    return true;
            foreach (UnitEffect effect in self.Effects)
                if ((!slotFreeOnly || effect.SlotFree) && effect.TargetsFloor(field, slot, other, federated))
                    return true;
            return false;
        }

        // 자리 효과가 닿는가 — BoardRunner.Linked와 같은 뜻(byPosition = 그 능력의 원래 자리 규칙, 연방제면 내 다른 모든 기물)
        public static bool Linked(int slot, int other, bool byPosition, bool federated) => other != slot && (federated || byPosition);

        // 상점 · 지도에서 그 기물의 탑에 연방제가 걸려 있나 — 툴팁의 「걸려 있는 업그레이드」 줄과 같은 판정(RunState.UpgradesOn)
        public static bool Federated(OwnedBuilding self)
            => RunState.UpgradesOn(self).Exists(holding => holding.Definition.Special == SpecialKind.Federation);

        // 쿨다운이 있는 기물인가 — 발동 · 가속 · 쿨다운 배율을 받는 기물
        public static bool Cooling(OwnedBuilding[] field, int other) => field[other].Definition.Cooldown > 0f;
    }
}
