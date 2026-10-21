using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 탭 하나(스타일 가이드 §2 「탭」 — 평소 어두운 꺾쇠 · 올림 흰 꺾쇠 · 고름 흰 바탕 반전, UI/NationRow와 같은 문법). 보여 주기만 한다 — 눌리면 Clicked로 알린다
    public class TabButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private Image _back;
        private Text _label;
        private Sprite _normal, _hover, _on;
        private bool _hovered;
        private bool _selected;

        public event System.Action Clicked;

        public void Init(Image back, Text label, Sprite normal, Sprite hover, Sprite on)
        {
            _back = back;
            _label = label;
            _normal = normal;
            _hover = hover;
            _on = on;
            Refresh();
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
            if (_back == null)
                return;
            _back.sprite = _selected ? _on : _hovered ? _hover : _normal;
            _label.color = _selected ? Color.black : Color.white;
        }
    }
}
