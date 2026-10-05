using UnityEngine;

namespace GnorpWar
{
    // 상점에서 무슨 일이 생길 때마다 대상 유닛의 스탯을 영구히 올린다 —
    // 「구매할 때마다,」(교대근무계획서) · 골드를 얻을 때마다(이동식 공장). 영구히 = 이 기물을 팔아도 남는다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Run Stat")]
    public class RunStatPassive : BuildingPassive, IUnitFilter
    {
        public enum Trigger { Buy, GoldGained }

        [SerializeField] private Trigger _trigger;
        [Tooltip("이 유닛에게 — 비우면 전부")]
        [SerializeField] private UnitDefinition[] _units = new UnitDefinition[0];
        [SerializeField] private StatModifier _modifier;

        public bool AppliesTo(UnitDefinition unit) => _units.Length == 0 || System.Array.IndexOf(_units, unit) >= 0;

        public override void OnBuy(OwnedBuilding self)
        {
            if (_trigger == Trigger.Buy)
                StatBook.AddForRun(this, _modifier);
        }

        public override void OnGoldGained(OwnedBuilding self)
        {
            if (_trigger == Trigger.GoldGained)
                StatBook.AddForRun(this, _modifier);
        }

        public override string Describe(int level)
        {
            string target = _units.Length == 0 ? "내 유닛들" : string.Join("·", System.Array.ConvertAll(_units, u => u.DisplayName));
            string when = _trigger == Trigger.Buy ? "「구매할 때마다,」" : "내가 골드를 얻을 때마다,";
            return $"{when} {target}의 {_modifier.Name} 영구히 {_modifier.ValueText}";
        }
    }
}
