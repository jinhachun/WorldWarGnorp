using UnityEngine;

namespace GnorpWar
{
    // 상점에서 무슨 일이 생길 때마다 대상 유닛의 스탯을 영구히 올린다 —
    // 「구매할 때마다,」(교대근무계획서 · 사격 통제소) · 골드를 얻을 때마다(이동식 공장). 영구히 = 이 기물을 팔아도 남는다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Run Stat")]
    public class RunStatPassive : BuildingPassive, IUnitFilter
    {
        public enum Trigger { Buy, GoldGained }

        [SerializeField] private Trigger _trigger;
        [Tooltip("이 유닛에게 — 유닛 · 병종 둘 다 비우면 전부")]
        [SerializeField] private UnitDefinition[] _units = new UnitDefinition[0];
        [Tooltip("이 병종 전체에게(사격 통제소: 궁병 — 사용자 11/1)")]
        [SerializeField] private UnitCategory[] _categories = new UnitCategory[0];
        [SerializeField] private StatModifier _modifier;
        [Tooltip("등급이 오를 때마다 _modifier 값에 더한다")]
        [SerializeField] private float _perTier;

        public bool AppliesTo(UnitDefinition unit) => _units.Length == 0 && _categories.Length == 0
            || System.Array.IndexOf(_units, unit) >= 0 || System.Array.IndexOf(_categories, unit.Category) >= 0;

        public override bool HasBuyEffect => _trigger == Trigger.Buy;

        public override void OnBuy(OwnedBuilding self, Team team)
        {
            if (_trigger == Trigger.Buy)
                StatBook.AddForRun(team, this, _modifier.Scaled(_perTier, self.Level));
        }

        public override void OnGoldGained(OwnedBuilding self)
        {
            if (_trigger != Trigger.GoldGained)
                return;
            StatBook.AddForRun(RunState.ActingTeam, this, _modifier.Scaled(_perTier, self.Level));
            OwnedBuilding.NotifyEffect(self);
        }

        public override string Describe(int level)
        {
            var names = new System.Collections.Generic.List<string>(System.Array.ConvertAll(_units, u => u.DisplayName));
            names.AddRange(System.Array.ConvertAll(_categories, c => c.DisplayName));
            string target = names.Count == 0 ? "" : string.Join("·", names) + " ";
            string when = _trigger == Trigger.Buy ? "「구매할 때마다,」" : "골드를 얻을 때마다,";
            return $"{when} {target}{_modifier.Name} 영구히 {_modifier.Scaled(_perTier, level).ValueText}";
        }

        public override string Headline(int level) => _modifier.Scaled(_perTier, level).ValueText;
    }
}
