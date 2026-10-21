using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛은 고도 1당 스탯 +N%(합연산) — 지금 고도로 실시간 (미노타우로스: 고도 1당 공격력 +10%, 공격속도 +10% — 트라이브 명세 §3).
    // 고도 = 발 높이 − 맵에서 가장 낮은 땅(Unit.Altitude)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Altitude Stat")]
    public class AltitudeStatEffect : UnitEffect
    {
        [Tooltip("고도 1당 더하는 값들 — 합연산(0.1 = +10%)")]
        [SerializeField] private StatModifier[] _perAltitude = new StatModifier[0];

        // {0} = 「공격력 +10%, 공격속도 +10%」
        protected override object[] TraitNumbers()
            => new object[] { string.Join(", ", System.Array.ConvertAll(_perAltitude, modifier => modifier.Describe())) };

        public override float AltitudeBonus(Team team, UnitStat stat)
        {
            float bonus = 0f;
            foreach (StatModifier modifier in _perAltitude)
                if (modifier.Stat == stat)
                    bonus += modifier.Value;
            return bonus;
        }
    }
}
