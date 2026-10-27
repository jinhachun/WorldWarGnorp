using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 발동할 때 전장의 내 유닛 N기를 그 자리에서 다른 유닛으로 바꾼다 (로켓 개조 공방: 톱니병사 → 톱니로켓병, 등급마다 +1기 — 사용자 10/28).
    // 고르는 것은 뒤쪽(내 성에 가까운)부터 — 원거리 유닛이 되므로 뒤에 선 것부터 바꾸고 앞줄은 남긴다. 그 유닛 정의 그대로인 것만(「~로도 취급」되는 유닛은 안 고른다).
    // 바뀌는 것은 소환이 아니다(기사 서임과 같다 — 추가 소환 · 변환 · 「소환할 때마다」를 지나지 않는다). 남은 체력 비율을 이어 가고,
    // 전 유닛이 이 전투 동안 받은 개별 강화는 사라진다(진영 단위 강화는 새 유닛도 받는다). 전 유닛은 소모된다(죽음이 아니다 — 사망 효과 없음)
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Convert Units")]
    public class ConvertUnitsAction : BuildingAction
    {
        [SerializeField] private UnitDefinition _from;
        [SerializeField] private UnitDefinition _into;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;
        [Tooltip("등급이 오를 때마다 바꾸는 수에 더한다")]
        [SuffixLabel("기", true)]
        [SerializeField] private int _countPerTier = 1;

        public int CountAt(int level) => OwnedBuilding.Scaled(_count, _countPerTier, level);

        private readonly List<Unit> _found = new List<Unit>();

        public override void Execute(BoardRunner board, int slot, int level)
        {
            _found.Clear();
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == board.Team && unit.Definition == _from)
                    _found.Add(unit);
            _found.Sort((a, b) => (a.Position.x * a.Forward).CompareTo(b.Position.x * b.Forward));
            int count = Mathf.Min(CountAt(level), _found.Count);
            for (int i = 0; i < count; i++)
            {
                Unit old = _found[i];
                float ratio = old.Hp / old.MaxHp;
                Vector2 at = old.Position;
                old.Consume();
                Unit converted = Unit.Spawn(_into.PrefabFor(board.Team), new Vector2(at.x, Ground.Instance.HeightAt(at.x)));
                converted.SetHpRatio(ratio);
                // 바뀐 순간 — 발밑 기둥 + 기물 → 그 유닛 소환 색 선(있는 부품으로)
                PillarFx.Rise(new Vector2(converted.Position.x, converted.Feet), converted.Top - converted.Feet);
                BuffLinks.ToPoint(board.Team, slot, converted.Position, BuffKind.Summon);
            }
        }

        public override string Describe(int level) => $"{_from.DisplayName} {CountAt(level)}기를 {_into.DisplayName}으로 바꿉니다.";
        public override string Headline(int level) => $"{CountAt(level)}기";
    }
}
