using UnityEngine;

namespace GnorpWar
{
    // 병종 태그(경보병·중보병·궁병·경기병·중기병·지원병 + 적 전용 포병). 효과(UnitEffect)가 적용 대상을 태그로 고른다
    [CreateAssetMenu(menuName = "GnorpWar/Unit Category")]
    public class UnitCategory : ScriptableObject
    {
        [SerializeField] private string _displayName = "";
        [Tooltip("사거리에 칠 것이 없고 등 뒤 가까이(BattleConfig 「뒤돌아 싸움」 칸) 적 유닛이 있으면 돌아서서 싸운다 — 보병·궁병·포병. 기병은 끄고 뚫고 나간다(사용자 10/16)")]
        [SerializeField] private bool _turnBack;

        public string DisplayName => _displayName;
        public bool TurnBack => _turnBack;
    }
}
