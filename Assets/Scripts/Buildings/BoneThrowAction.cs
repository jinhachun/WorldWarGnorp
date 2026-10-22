using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 뼈 투척(저주의 뼈언덕 리메이크 — 사용자 10/22): 발동할 때마다 뼛가루 언덕 위에서 뼈를 던져, 그 자리에서 가장 가까운 적 유닛 N기에게 피해.
    // 던지는 자리 = 뼛가루가 가장 두꺼운 언덕 꼭대기(Ground.DustPeak), 언덕이 없으면 내 본진 꼭대기. 뼈는 원거리 투사체(포물선 — 프리팹)를 그대로 쓴다.
    // 피해의 출처는 이 칸(Core/SlotAttacker — 딜 미터기가 이 기물 몫으로 센다). 신화 표시(_slowMark)가 이 기물에 있으면 맞은 적이 잠깐 느려진다
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Bone Throw")]
    public class BoneThrowAction : BuildingAction
    {
        [Tooltip("뼈 투사체(포물선)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Projectile _projectile;
        [Tooltip("포물선 꼭짓점 — 출발 · 대상 중 높은 쪽보다 이만큼(칸) 위")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private float _arcHeight = 3f;
        [SuffixLabel("기", true)]
        [SerializeField] private int _targets = 3;
        [Tooltip("등급이 오를 때마다 대상 수에 더한다")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _targetsPerTier = 1;
        [SerializeField] private float _damage = 25f;
        [Tooltip("등급이 오를 때마다 피해에 더한다")]
        [SerializeField] private float _damagePerTier = 0f;
        [Tooltip("밀치는 힘(맞은 적의 밀림 값에 곱한다)")]
        [SerializeField] private float _push = 0.5f;
        [Tooltip("신화 줄의 표시 — 이 기물의 능력 목록에 있으면 맞은 적이 그 표시의 값만큼 느려진다")]
        [SerializeField] private BoneThrowSlowPassive _slowMark;

        private bool HideNonBalance => BalanceView.HideNonBalance;

        // 언덕 꼭대기에서 이만큼 위에서 떠난다(칸) — 언덕 위에 선 적 머리 위에서 출발해야 날아가는 게 보인다
        private const float Lift = 1.5f;
        private static readonly List<Unit> Found = new List<Unit>();

        private int Targets(int level) => OwnedBuilding.Scaled(_targets, _targetsPerTier, level);
        private float Damage(int level) => OwnedBuilding.Scaled(_damage, _damagePerTier, level);

        public override void Execute(BoardRunner board, int slot, int level)
        {
            Vector2 from;
            if (Ground.Instance != null && Ground.Instance.DustPeak(out float x))
                from = new Vector2(x, Ground.Instance.HeightAt(x) + Lift);
            else if (Base.For(board.Team) is Base home)
                from = home.Muzzle;
            else
                return;

            // 던지는 자리에서 가까운 적 유닛 N기
            Found.Clear();
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team != board.Team)
                    Found.Add(unit);
            Found.Sort((a, b) => (a.Position - from).sqrMagnitude.CompareTo((b.Position - from).sqrMagnitude));

            BoneThrowSlowPassive slow = _slowMark != null && System.Array.IndexOf(board.Slots[slot].Building.Passives, _slowMark) >= 0 ? _slowMark : null;
            var source = new SlotAttacker(board.Team, slot);
            int count = Mathf.Min(Targets(level), Found.Count);
            for (int i = 0; i < count; i++)
            {
                Projectile bone = Pooled.Get(_projectile, from, Quaternion.identity);
                bone.Launch(board.Team, Damage(level), _push, Found[i].Position, _arcHeight, 0f, source);
                if (slow != null)
                    bone.SlowOnHit(slow.Factor, slow.Seconds);
            }
        }

        public override string Describe(int level)
            => $"내 뼛가루 언덕에서 뼈를 던져, 가장 가까운 적 {Targets(level)}기에게 피해 {Damage(level):0.#}를 줍니다.";
        public override string Headline(int level) => $"{Damage(level):0.#}";
    }
}
