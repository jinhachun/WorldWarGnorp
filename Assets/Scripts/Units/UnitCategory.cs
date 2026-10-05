using UnityEngine;

namespace GnorpWar
{
    // 유닛 분류(보병·궁병·기병·포병·지원·괴수). 효과(UnitEffect)가 적용 대상을 분류로 고른다
    [CreateAssetMenu(menuName = "GnorpWar/Unit Category")]
    public class UnitCategory : ScriptableObject
    {
        [SerializeField] private string _displayName = "";

        public string DisplayName => _displayName;
    }
}
