using UnityEngine;

namespace GnorpWar
{
    // 사냥 — 앞쪽 (Range) 안에 노리는 분류의 적이 있으면 그 밖의 적은 상대하지 않고 뛰어넘어 간다.
    // 이동·표적 선택을 바꾸는 효과라 Unit이 직접 읽는다(훅이 아니라)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Vault")]
    public class VaultEffect : UnitEffect
    {
        [Tooltip("노리지 않는 적을 뛰어넘는 높이")]
        [SerializeField] private float _jumpHeight = 2.5f;
        [Tooltip("사냥감을 찾는 앞쪽 거리")]
        [SerializeField] private float _range = 15f;
        [SerializeField] private UnitCategory[] _targetCategories = new UnitCategory[0];

        public float JumpHeight => _jumpHeight;
        public float Range => _range;

        public bool Targets(UnitCategory category) => System.Array.IndexOf(_targetCategories, category) >= 0;
    }
}
