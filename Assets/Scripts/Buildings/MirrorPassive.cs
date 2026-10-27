using UnityEngine;

namespace GnorpWar
{
    // 거울 공방(공용 영웅 — 사용자 10/27): 전투 시작 시 바로 위 기물과 같은 기물로 변신한다 — 이 기물의 등급으로(위 기물의 시작 등급보다 낮으면 그 시작 등급 —
    // 기물은 시작 등급 아래로 내려갈 수 없다). 그래서 이 기물의 등급이 오르면 변신한 기물도 강해진다. 신화면 변신한 기물도 신화라 그 기물의 신화 줄까지 켜진다.
    // 변신은 BoardRunner.Begin이 효과를 켜기 전에 한다(BoardRunner.Reflect) — 전투 동안만: 칸 · 탑 그림만 이 전투의 사본으로 바꾸고 런의 필드(RunState)는 그대로라
    // 전투가 끝나면 거울 공방이다. 위가 비었거나 위가 변신하지 못한 거울 공방이면 아무 일도 없다. 위 기물이 모은 스택은 그대로 본뜬다(전투 중에 쌓인 몫은 전투가 끝나면 사라진다)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Mirror")]
    public class MirrorPassive : BuildingPassive
    {
        [Tooltip("신화 줄의 표시 — 이 기물의 능력 목록에 있으면 변신한 기물을 전투 시작 때 즉시 1회 발동한다")]
        [SerializeField] private MirrorMythicPassive _mythicMark;

        // self(거울 공방)가 above로 변신한 이 전투의 사본 — 등급은 self의 등급, 위 기물의 시작 등급 ~ 신화 사이로 맞춘다. 쿨다운 배율(계몽주의)은 이 한 채(self)의 것
        public static OwnedBuilding Reflect(OwnedBuilding self, OwnedBuilding above)
        {
            int start = (int)above.Definition.Rarity;
            int upgrades = Mathf.Clamp((int)self.Rarity - start, 0, (int)BuildingRarity.Mythic - start);
            return new OwnedBuilding(above.Definition, upgrades, above.Stacks, 0, self.CooldownScale);
        }

        public bool FiresOnStart(OwnedBuilding self) => _mythicMark != null && System.Array.IndexOf(self.Passives, _mythicMark) >= 0;

        // 바로 위 기물에 닿는다 — 상대 탑의 자리 고르기(Run/EnemyShopper)가 읽는다
        public override bool Reaches(int slot, int other, BuildingDefinition target) => other == slot + 1;

        public override string Describe(int level) => "전투 시작 시, 이 기물의 등급 그대로 바로 위 기물과 같은 기물로 변신합니다.";
    }
}
