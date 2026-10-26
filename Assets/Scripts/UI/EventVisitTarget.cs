using UnityEngine;
using UnityEngine.EventSystems;

namespace GnorpWar
{
    // 방문 장면(UI/EventVisitPopup)에서 고를 수 있는 것 하나 — 무대 위 사물 · 왼쪽 탑 층. 올림 · 내림 · 누름을 장면에 알린다(무엇을 할지는 장면이 정한다)
    public class EventVisitTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public System.Action<EventVisitTarget> Entered, Exited, Clicked;

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke(this);

        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);

        private void OnDisable() => Exited?.Invoke(this);
    }
}
