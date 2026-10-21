using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 버튼 호버 강조 — 카드·탑 칸의 창틀과 같은 결: 평소엔 판(테두리)이 한 단계 어둡고, 마우스를 올리면 제 밝기.
    // 못 누르는 버튼은 강조하지 않는다. 글·아이콘 색은 건드리지 않는다
    // 상태마다 그림이 따로 있는 버튼(지도의 명령 버튼 — Button의 Sprite Swap)은 _idle = 1로 두고, 누르는 동안 글 줄(첫 자식)을 _pressDepth만큼 내린다(눌린 그림의 얼굴이 받침만큼 내려가므로)
    [RequireComponent(typeof(Button))]
    public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("마우스를 올리지 않았을 때 판 밝기 — 흰 판 버튼(전투 시작 등)은 회색으로 죽어 보이지 않게 덜 어둡게")]
        [SerializeField] private float _idle = 0.65f;
        [Tooltip("누르는 동안 첫 자식(글 줄)을 내리는 거리(px) — 0이면 안 움직인다")]
        [SerializeField] private float _pressDepth;

        private Button _button;
        private bool _hovered;
        private bool _pressed;

        // 판의 제 색 — 색을 바꾸는 쪽(판매 칸의 놓는 자리 노랑)은 판 색을 직접 쓰지 말고 여기에 쓴다
        public Color Base { get; set; } = Color.white;

        public bool Hovered => _hovered;

        private void Awake() => _button = GetComponent<Button>();

        private void OnDisable()
        {
            _hovered = false;
            Press(false);
        }

        private void LateUpdate()
        {
            float k = _hovered && _button.interactable ? 1f : _idle;
            _button.image.color = new Color(Base.r * k, Base.g * k, Base.b * k, Base.a);
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        public void OnPointerDown(PointerEventData eventData) => Press(_button.interactable);

        public void OnPointerUp(PointerEventData eventData) => Press(false);

        private void Press(bool on)
        {
            if (_pressed == on || _pressDepth == 0f || transform.childCount == 0)
                return;
            _pressed = on;
            var label = (RectTransform)transform.GetChild(0);
            label.anchoredPosition += new Vector2(0f, on ? -_pressDepth : _pressDepth);
        }
    }
}
