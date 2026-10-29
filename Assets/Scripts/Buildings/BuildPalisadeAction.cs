using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 목책(공용 고급 — 사용자 10/29 안 C): 발동할 때 가장 앞 아군 앞에 목책(Bases/Palisade)을 세운다. 목책은 적 유닛만 막는다. 체력은 등급마다 오른다.
    // 이 기물이 세운 목책이 이미 _max개 서 있으면 맨 뒤(내 성 쪽) 것을 걷고 새로 세운다 — 목책이 늘 전선을 따라간다(사용자 10/29). 세울 자리가 없으면 건너뛴다.
    // 신화 표시(_mythicMark — 이 기물의 능력 목록에 있으면)가 있으면 목책이 받은 피해의 일부를 공격한 쪽에 돌려준다
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Build Palisade")]
    public class BuildPalisadeAction : BuildingAction
    {
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Palisade _prefab;
        [Tooltip("목책 체력(시작 등급)")]
        [SerializeField] private float _hp = 300f;
        [Tooltip("등급이 오를 때마다 위 체력에 더한다(150 = 300 · 450 · 600 · 750)")]
        [SerializeField] private float _hpPerTier = 150f;
        [Tooltip("가장 앞 아군(없으면 성 앞면)에서 띄우는 거리")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _gap = 1f;
        [Tooltip("이 기물이 세운 목책이 동시에 서 있을 수 있는 수 — 다 서 있으면 맨 뒤(내 성 쪽) 것을 걷고 새로 세운다")]
        [SerializeField] private int _max = 3;
        [Tooltip("신화 줄의 표시 — 이 기물의 능력 목록에 있으면 그 표시의 비율만큼 받은 피해를 돌려준다")]
        [SerializeField] private PalisadeMythicPassive _mythicMark;

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        public float HpAt(int level) => OwnedBuilding.Scaled(_hp, _hpPerTier, level);
        public int Max => _max;

        public override void Execute(BoardRunner board, int slot, int level)
        {
            OwnedBuilding self = board.Slots[slot].Building;
            float reflect = _mythicMark != null && System.Array.IndexOf(self.Passives, _mythicMark) >= 0 ? _mythicMark.Reflect : 0f;
            if (Palisade.CountOf(self) < _max)
            {
                Palisade.BuildAhead(_prefab, board.Team, HpAt(level), reflect, _gap, self);
                return;
            }
            // 상한 — 맨 뒤 것을 걷고 새로 세운다(그 자리도 쓸 수 있게 몸을 먼저 끄고 찾는다). 새 자리가 없으면 아무것도 걷지 않는다
            Palisade rear = Palisade.Rearmost(self);
            rear.SetSolid(false);
            if (Palisade.BuildAhead(_prefab, board.Team, HpAt(level), reflect, _gap, self) != null)
                rear.Remove();
            else
                rear.SetSolid(true);
        }

        public override string Describe(int level) => $"가장 앞 아군 앞에 체력 {HpAt(level):0}의 목책을 세웁니다.\n목책은 적만 막습니다.";
        public override string Headline(int level) => $"{HpAt(level):0}";
    }
}
