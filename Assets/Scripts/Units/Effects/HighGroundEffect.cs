using UnityEngine;

namespace GnorpWar
{
    // 내 유닛과 타워가 상대보다 높은 고도에 있으면 주는 피해 +N% (기름투하대) — 타워는 늘 높은 고도로 친다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/High Ground")]
    public class HighGroundEffect : UnitEffect
    {
        [Tooltip("0.5 = +50%")]
        [SerializeField] private float _bonus = 0.5f;
        [Tooltip("등급이 오를 때마다 더한다")]
        [SerializeField] private float _bonusPerTier = 0.25f;

        public override float HighGroundDamageBonus(int level) => OwnedBuilding.Scaled(_bonus, _bonusPerTier, level);

        public override string Describe(int level) => string.Format(Description, $"{HighGroundDamageBonus(level) * 100f:0.#}");
        public override string Headline(int level) => $"+{HighGroundDamageBonus(level) * 100f:0.#}%";
    }
}
