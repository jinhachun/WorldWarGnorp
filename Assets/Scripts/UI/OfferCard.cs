using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 상점 진열 한 칸 — 아이콘 · 가격. 마우스를 올리면 설명 창(BuildingTooltip).
    // 누르면 빈 칸에 사고, 끌어서 원하는 칸에 놓아 살 수도 있다(ShopPanel)
    public class OfferCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _price;
        [SerializeField] private Button _button;

        private ShopPanel _shop;
        private int _index;
        private bool _hovered;

        public Button Button => _button;

        private void OnDisable()
        {
            _hovered = false;
            BuildingTooltip.Hide(this);
        }

        public void Bind(ShopPanel shop, int index)
        {
            _shop = shop;
            _index = index;
        }

        public void Show(BuildingDefinition building, bool canBuy)
        {
            bool empty = building == null;
            _icon.enabled = !empty;
            if (!empty)
                _icon.sprite = building.Icon;
            _price.text = empty ? "" : $"{RunState.PriceOf(building)}G";
            _button.interactable = canBuy;

            if (!_hovered)
                return;
            if (empty || _shop.Dragging)
                BuildingTooltip.Hide(this);
            else
            {
                OwnedBuilding pair = RunState.FindMergeTarget(building, 0);
                string body = BuildingTooltip.Describe(building, 1, building.Cooldown);
                if (pair != null)
                    body += BuildingTooltip.Note($"사면 가진 것과 합쳐져 {(pair.Rarity + 1).DisplayName()} 등급");
                BuildingTooltip.Show(this, BuildingTooltip.Title(building, false, building.Rarity), body, BuildingTooltip.UnitInfo(building, RunState.Field));
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            BuildingTooltip.Hide(this);
        }

        public void OnBeginDrag(PointerEventData eventData) => _shop.BeginDragOffer(_index, eventData);
        public void OnDrag(PointerEventData eventData) => _shop.Drag(eventData);
        public void OnEndDrag(PointerEventData eventData) => _shop.EndDrag();
    }
}
