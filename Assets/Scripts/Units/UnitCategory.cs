using UnityEngine;

namespace GnorpWar
{
    // 병종 태그(경보병·중보병·궁병·경기병·중기병·지원병 + 적 전용 포병). 효과(UnitEffect)가 적용 대상을 태그로 고른다
    [CreateAssetMenu(menuName = "GnorpWar/Unit Category")]
    public class UnitCategory : ScriptableObject
    {
        [SerializeField] private string _displayName = "";

        public string DisplayName => _displayName;
    }
}
