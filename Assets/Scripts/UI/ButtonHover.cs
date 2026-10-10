using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 버튼 호버 강조 — 카드·탑 칸의 창틀과 같은 결: 평소엔 판(테두리)이 한 단계 어둡고, 마우스를 올리면 제 밝기.
    // 못 누르는 버튼은 강조하지 않는다. 글·아이콘 색은 건드리지 않는다
    [RequireComponent(typeof(Button))]
    public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("마우스를 올리지 않았을 때 판 밝기 — 흰 판 버튼(전투 시작 등)은 회색으로 죽어 보이지 않게 덜 어둡게")]
        [SerializeField] private float _idle = 0.65f;

        private Button _button;
        private bool _hovered;

        // 판의 제 색 — 색을 바꾸는 쪽(판매 칸의 놓는 자리 노랑)은 판 색을 직접 쓰지 말고 여기에 쓴다
        public Color Base { get; set; } = Color.white;

        private void Awake() => _button = GetComponent<Button>();

        private void OnDisable() => _hovered = false;

        private void LateUpdate()
        {
            float k = _hovered && _button.interactable ? 1f : _idle;
            _button.image.color = new Color(Base.r * k, Base.g * k, Base.b * k, Base.a);
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData) => _hovered = false;
    }
}
