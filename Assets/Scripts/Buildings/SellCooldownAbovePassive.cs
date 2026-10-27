using UnityEngine;

namespace GnorpWar
{
    // 「판매 시,」 쿨다운이 있는 내 기물 중 가장 위 기물의 쿨다운이 영구히 ×N (보급 창고 — 사용자 10/27 「바로 위」 → 「쿨다운 있는 기물 중 가장 위」, 등급마다 ×0.9 · 0.85 · 0.8 · 0.75 · 0.7).
    // 영구히 = 그 한 채의 쿨다운 배율(OwnedBuilding.ScaleCooldown — 계몽주의와 같은 길). 판 칸은 이미 비었으니 남은 탑에서 찾는다(보관함에서 팔아도 같다).
    // 상점 일이라 연방제(전투 동안)는 걸리지 않는다. 신화 표시(_mythicMark)가 있으면 그다음으로 위에 있는 쿨다운 기물도 같은 값을 받는다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Sell Cooldown Above")]
    public class SellCooldownAbovePassive : BuildingPassive
    {
        [Tooltip("쿨다운에 곱한다(영구히, 시작 등급)")]
        [SerializeField] private float _factor = 0.9f;
        [Tooltip("등급이 오를 때마다 곱하는 값에 더한다(음수 = 더 줄어든다)")]
        [SerializeField] private float _factorPerTier = -0.05f;
        [Tooltip("이 기물의 능력 목록에 이것이 있으면(신화 줄) 그다음으로 위에 있는 쿨다운 기물도 받는다")]
        [SerializeField] private BuildingPassive _mythicMark;

        private float Factor(int level) => OwnedBuilding.Scaled(_factor, _factorPerTier, level);

        public override void OnSell(OwnedBuilding self, int floor)
        {
            int top = TopCooling(RunState.Field, -1, RunState.Field.Length - 1);
            if (top < 0)
                return;
            float factor = Factor(self.Level);
            Scale(top, factor);
            if (Mythic(self))
                Scale(TopCooling(RunState.Field, -1, top - 1), factor);
        }

        // 툴팁 대상 표시 — 팔기 전이라 자기 층(slot)은 빼고 같은 규칙으로 찾는다
        public override bool TargetsFloor(OwnedBuilding[] field, int slot, int other, bool federated)
        {
            int top = TopCooling(field, slot, field.Length - 1);
            return top >= 0 && (other == top || (Mythic(field[slot]) && other == TopCooling(field, slot, top - 1)));
        }

        private bool Mythic(OwnedBuilding self) => _mythicMark != null && System.Array.IndexOf(self.Passives, _mythicMark) >= 0;

        // from 층부터 아래로 내려가며 처음 만나는 쿨다운 있는 기물의 층(skip층은 건너뛴다, 없으면 -1)
        private static int TopCooling(OwnedBuilding[] field, int skip, int from)
        {
            for (int floor = from; floor >= 0; floor--)
                if (floor != skip && !OwnedBuilding.IsEmpty(field[floor]) && field[floor].Definition.Cooldown > 0f)
                    return floor;
            return -1;
        }

        private static void Scale(int floor, float factor)
        {
            if (floor < 0 || floor >= RunState.Field.Length || OwnedBuilding.IsEmpty(RunState.Field[floor]))
                return;
            RunState.Field[floor].ScaleCooldown(factor);
            OwnedBuilding.NotifyEffect(RunState.Field[floor]);
        }

        public override string Describe(int level) => $"「판매 시,」 가장 위 기물의 쿨다운 영구히 ×{Factor(level):0.##}";
        public override string Headline(int level) => $"×{Factor(level):0.##}";
    }
}
