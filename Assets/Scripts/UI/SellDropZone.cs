using UnityEngine;
using UnityEngine.EventSystems;

namespace GnorpWar
{
    // 판매 버튼 — 칸을 끌어다 놓으면 판다
    public class SellDropZone : MonoBehaviour, IDropHandler
    {
        [SerializeField] private ShopPanel _shop;

        public void OnDrop(PointerEventData eventData) => _shop.DropOnSell();
    }
}
