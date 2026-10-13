using UnityEngine;

namespace GnorpWar
{
    // 발동할 때마다, 그 순간 뼛가루 언덕 위에 있는 유닛들의 스탯을 전투 동안 바꾼다 (전쟁 북소리: 내 유닛 공격력 +10% · 저주의 뼈언덕: 적 공격력 ×0.95 — 트라이브 명세 §4 · §5).
    // 유닛 하나하나에 붙는다 — 받은 뒤에는 언덕을 내려가도 남고, 발동마다 겹친다
    [CreateAssetMenu(menuName = "GnorpWar/Actions/Dust Stat")]
    public class DustStatAction : BuildingAction
    {
        [Tooltip("켜면 뼛가루 언덕 위의 적에게, 끄면 내 유닛에게")]
        [SerializeField] private bool _enemies;
        [SerializeField] private StatModifier _modifier = new StatModifier(UnitStat.Attack, StatOp.Percent, 0.1f);
        [Tooltip("등급이 오를 때마다 _modifier 값에 더한다")]
        [SerializeField] private float _perTier;

        public override void Execute(BoardRunner board, int slot, int level)
        {
            StatModifier modifier = _modifier.Scaled(_perTier, level);
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == board.Team != _enemies && unit.OnBoneDust)
                    unit.AddModifier(modifier);
        }

        public override string Describe(int level)
            => $"전투 동안 뼛가루 언덕 위에 있는 {(_enemies ? "적" : "내 유닛들")}의 {_modifier.Scaled(_perTier, level).Describe()}";
        public override string Headline(int level) => _modifier.Scaled(_perTier, level).ValueText;
    }
}
