using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 목책(공용 고급 — 사용자 10/27): 전투 시작 시 내 성 앞에 목책(Bases/Palisade)을 세운다. 목책은 적 유닛만 막는다. 체력은 등급마다 오른다.
    // 신화 표시(_mythicMark — 이 기물의 능력 목록에 있으면)가 있으면 목책이 받은 피해의 일부를 공격한 쪽에 돌려준다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Palisade At Start")]
    public class PalisadeAtStartPassive : BuildingPassive
    {
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Palisade _prefab;
        [Tooltip("목책 체력(시작 등급)")]
        [SerializeField] private float _hp = 300f;
        [Tooltip("등급이 오를 때마다 위 체력에 더한다(150 = 300 · 450 · 600 · 750)")]
        [SerializeField] private float _hpPerTier = 150f;
        [Tooltip("성 앞면에서 띄우는 거리")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _gap = 1f;
        [Tooltip("내 목책이 여럿이면 서로 띄우는 거리(중심 사이)")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _spacing = 2f;
        [Tooltip("신화 줄의 표시 — 이 기물의 능력 목록에 있으면 그 표시의 비율만큼 받은 피해를 돌려준다")]
        [SerializeField] private PalisadeMythicPassive _mythicMark;

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        private float Hp(int level) => OwnedBuilding.Scaled(_hp, _hpPerTier, level);

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            OwnedBuilding self = board.Slots[slot].Building;
            float reflect = _mythicMark != null && System.Array.IndexOf(self.Passives, _mythicMark) >= 0 ? _mythicMark.Reflect : 0f;
            if (Palisade.Build(_prefab, board.Team, Hp(level), reflect, _gap, _spacing) != null)
                OwnedBuilding.NotifyEffect(self);
        }

        public override string Describe(int level)
            => $"전투 시작 시, 내 성 앞에 목책(체력 {Hp(level):0})을 세웁니다.\n목책은 적 유닛만 막습니다.";
    }
}
