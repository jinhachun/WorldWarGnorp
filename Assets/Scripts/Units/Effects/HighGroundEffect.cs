using UnityEngine;

namespace GnorpWar
{
    // 내 유닛과 타워가 상대보다 높은 고도에 있으면 주는 피해 +N% (기름투하대) — 타워는 늘 높은 고도로 친다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/High Ground")]
    public class HighGroundEffect : UnitEffect
    {
        [Tooltip("0.5 = +50%")]
        [SerializeField] private float _bonus = 0.5f;

        public override float HighGroundDamageBonus => _bonus;
    }
}
