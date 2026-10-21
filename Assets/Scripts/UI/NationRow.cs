using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 국가 고르기 목록의 한 줄(UI/NationCard가 국가 수만큼 복제한다) — 국가 기호 · 이름 · 한 줄. 상태 셋: 평소(어두운 꺾쇠) · 올림(흰 꺾쇠) · 고름(흰 바탕 반전).
    // 보여 주기만 한다 — 눌리면 Clicked로 알린다
    public class NationRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        // 고른 줄의 글 색 — 흰 바탕 위 검정 이름 · 짙은 회색 한 줄
        private static readonly Color OnName = Color.black;
        private static readonly Color OnLine = new Color(0.27f, 0.27f, 0.27f);

        [Tooltip("줄 판 — 상태마다 그림을 바꾼다(254×104 원본)")]
        [SerializeField] private Image _back;
        [SerializeField] private Image _emblem;
        [SerializeField] private Text _name;
        [SerializeField] private Text _line;
        [SerializeField] private Sprite _normal;
        [SerializeField] private Sprite _hover;
        [SerializeField] private Sprite _on;

        public event System.Action Clicked;

        private bool _hovered;
        private bool _selected;
        private Color _nameColor, _lineColor;
        private bool _read;

        public void Set(Sprite emblem, string name, string line)
        {
            _emblem.sprite = emblem;
            _emblem.SetNativeSize();
            _name.text = name;
            _line.text = line;
        }

        public bool Selected
        {
            set
            {
                _selected = value;
                Refresh();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Refresh();
        }

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();

        private void OnDisable() => _hovered = false;

        private void Refresh()
        {
            // 글의 제 색(씬에 놓인 값)은 처음 칠하기 전에 읽어 둔다
            if (!_read)
            {
                _nameColor = _name.color;
                _lineColor = _line.color;
                _read = true;
            }
            _back.sprite = _selected ? _on : _hovered ? _hover : _normal;
            _emblem.color = _selected ? OnName : Color.white;
            _name.color = _selected ? OnName : _nameColor;
            _line.color = _selected ? OnLine : _lineColor;
        }
    }
}
