using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면 전투 동안 내 유닛(고른 것)의 스탯을 바꾼다 — 나중에 나오는 유닛도 받는다 (총동원 개조령: 내 톱니로켓병의 체력 +50%)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Battle Stat")]
    public class BattleStatPassive : BuildingPassive, IUnitFilter, ICategoryFilter
    {
        [Tooltip("이 병종 태그의 유닛에게 — 유닛 칸과 함께 비우면 전부")]
        [SerializeField] private UnitCategory[] _categories = new UnitCategory[0];
        [SerializeField] private UnitDefinition[] _units = new UnitDefinition[0];
        [SerializeField] private StatModifier _modifier = new StatModifier(UnitStat.MaxHp, StatOp.Percent, 0.5f);

        public bool AppliesTo(UnitDefinition unit)
            => (_categories.Length == 0 && _units.Length == 0)
               || System.Array.IndexOf(_categories, unit.Category) >= 0
               || System.Array.IndexOf(_units, unit) >= 0;

        public bool AppliesTo(UnitCategory category) => System.Array.IndexOf(_categories, category) >= 0;

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            StatBook.AddForBattle(board.Team, this, _modifier);
            BuffLinks.ToPoint(board.Team, slot, board.SpawnPoint, BuffKind.Stat);   // 이 층 → 유닛이 나오는 성문 앞(전투 시작엔 아직 유닛이 없다)
        }

        public override string Describe(int level)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (UnitCategory c in _categories)
                names.Add(c.DisplayName);
            foreach (UnitDefinition u in _units)
                names.Add(u.DisplayName);
            string who = names.Count == 0 ? "" : $"{string.Join("·", names)} ";
            return $"{who}{_modifier.Describe()}";
        }
    }
}
