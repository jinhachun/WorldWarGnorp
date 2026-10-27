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
        [Tooltip("곱연산(×)으로 깎는 효과의 하한 — 이 능력이 한 유닛에 겹쳐 건 배율은 이 값 아래로 내려가지 않는다(0.2 = ×0.2까지). 0 = 하한 없음. " +
                 "같은 능력 에셋을 쓰는 기물이 여럿이어도 합쳐서 센다. 다른 효과의 감속 · 약화와는 따로다")]
        [SerializeField] private float _floor;

        // 이 능력이 유닛마다 지금까지 겹쳐 건 배율(하한이 있을 때만 적는다) — 유닛은 풀에서 다시 쓰이므로 생애 번호를 같이 든다
        private readonly System.Collections.Generic.Dictionary<Unit, (int life, float factor)> _applied
            = new System.Collections.Generic.Dictionary<Unit, (int life, float factor)>();

        public override void Execute(BoardRunner board, int slot, int level)
        {
            StatModifier modifier = _modifier.Scaled(_perTier, level);
            bool floored = _floor > 0f && modifier.Op == StatOp.Multiply && modifier.Value < 1f;
            // 지난 전투의 유닛(씬과 함께 사라졌다)이 남아 있으면 비운다
            foreach (Unit key in _applied.Keys)
                if (key == null)
                {
                    _applied.Clear();
                    break;
                }
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
            {
                if (!unit.IsAlive || unit.Team == board.Team == _enemies || !unit.OnBoneDust)
                    continue;
                if (!floored)
                {
                    unit.AddModifier(modifier);
                    sum += unit.Position;
                    count++;
                    continue;
                }
                float before = _applied.TryGetValue(unit, out (int life, float factor) entry) && entry.life == unit.Life ? entry.factor : 1f;
                float after = Mathf.Max(before * modifier.Value, _floor);
                if (after >= before)
                    continue;   // 이미 하한이다
                unit.AddModifier(new StatModifier(modifier.Stat, modifier.Op, after / before));
                _applied[unit] = (unit.Life, after);
                sum += unit.Position;
                count++;
            }
            // 받은 무리 가운데로 선 하나 — 적에게 거는 것(저주)은 디버프, 내 유닛에게(북소리)는 스탯
            if (count > 0)
                BuffLinks.ToPoint(board.Team, slot, sum / count, _enemies ? BuffKind.Debuff : BuffKind.Stat, _enemies ? default : BuffLabel.Of(modifier));
        }

        public override string Describe(int level)
            => $"뼛가루 언덕 위 {(_enemies ? "적" : "유닛")} {_modifier.Scaled(_perTier, level).Describe()}";
        public override string Headline(int level) => _modifier.Scaled(_perTier, level).ValueText;
    }
}
