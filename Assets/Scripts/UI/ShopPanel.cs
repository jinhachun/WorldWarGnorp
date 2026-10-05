using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 상점 단계 화면 (The Bazaar식) — 위: 진열 · 가운데: 내 필드 · 아래: 보관함.
    // 끌어서 옮긴다: 칸 → 칸(빈 칸이면 이동, 차 있으면 맞바꿈) · 진열 → 칸(그 칸에 구매) · 칸 → 판매 버튼(판매).
    // 누르면: 칸은 선택(설명·판매 버튼), 진열은 빈 칸에 구매. 적 칸은 설명만
    public class ShopPanel : MonoBehaviour
    {
        [SerializeField] private BattleManager _battle;
        [SerializeField] private OfferCard _offerTemplate;
        [SerializeField] private float _offerStep = 250f;
        [SerializeField] private Text _status;
        [SerializeField] private Button _reroll;
        [SerializeField] private Text _rerollLabel;
        [SerializeField] private Button _sell;
        [SerializeField] private Text _sellLabel;
        [SerializeField] private Button _start;
        [Tooltip("끄는 동안 포인터를 따라다니는 그림 — 레이캐스트를 막지 않아야 아래 칸이 놓기를 받는다")]
        [SerializeField] private RectTransform _dragGhost;
        [SerializeField] private Image _dragGhostIcon;

        private OfferCard[] _offers;
        private bool _hasSelection;
        private BoardView.Source _selectedSource;
        private int _selectedIndex;

        private bool _dragging;
        private bool _dragFromOffer;
        private BoardView.Source _dragSource;
        private int _dragIndex;

        private void Start()
        {
            _offers = new OfferCard[RunState.Offers.Length];
            for (int i = 0; i < _offers.Length; i++)
            {
                OfferCard card = Instantiate(_offerTemplate, _offerTemplate.transform.parent);
                var rect = (RectTransform)card.transform;
                rect.anchoredPosition = ((RectTransform)_offerTemplate.transform).anchoredPosition + new Vector2(i * _offerStep, 0f);
                int index = i;
                card.Bind(this, index);
                card.Button.onClick.AddListener(() => RunState.Buy(index));
                card.gameObject.SetActive(true);
                _offers[i] = card;
            }
            _reroll.onClick.AddListener(RunState.Reroll);
            _sell.onClick.AddListener(SellSelected);
            _start.onClick.AddListener(_battle.StartBattle);
            _dragGhost.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            _hasSelection = false;
            _dragging = false;
            if (_dragGhost != null)
                _dragGhost.gameObject.SetActive(false);
        }

        public bool Dragging => _dragging;
        public bool IsSelected(BoardView.Source source, int index) => _hasSelection && _selectedSource == source && _selectedIndex == index;
        public bool IsDragging(BoardView.Source source, int index) => _dragging && !_dragFromOffer && _dragSource == source && _dragIndex == index;

        public void OnSlotClicked(BoardView.Source source, int index)
        {
            if (BattleManager.Fighting || IsSelected(source, index) || OwnedBuilding.IsEmpty(BuildingAt(source, index)))
            {
                _hasSelection = false;
                return;
            }
            _hasSelection = true;
            _selectedSource = source;
            _selectedIndex = index;
        }

        // --- 끌기 ---

        public void BeginDragSlot(BoardView.Source source, int index, PointerEventData eventData)
        {
            if (BattleManager.Fighting || source == BoardView.Source.EnemyField)
                return;
            OwnedBuilding building = BuildingAt(source, index);
            if (OwnedBuilding.IsEmpty(building))
                return;
            StartDrag(false, source, index, building.Definition.Icon, eventData);
        }

        public void BeginDragOffer(int index, PointerEventData eventData)
        {
            BuildingDefinition offer = RunState.Offers[index];
            if (BattleManager.Fighting || offer == null)
                return;
            StartDrag(true, default, index, offer.Icon, eventData);
        }

        private void StartDrag(bool fromOffer, BoardView.Source source, int index, Sprite icon, PointerEventData eventData)
        {
            _dragging = true;
            _dragFromOffer = fromOffer;
            _dragSource = source;
            _dragIndex = index;
            _hasSelection = false;
            _dragGhostIcon.sprite = icon;
            _dragGhost.gameObject.SetActive(true);
            Drag(eventData);
        }

        public void Drag(PointerEventData eventData)
        {
            if (_dragging)
                _dragGhost.position = eventData.position;   // Overlay 캔버스라 화면 좌표 = 월드 좌표
        }

        // 놓기(OnDrop)가 끝난 뒤에 불린다 — 아무 데도 안 놓았으면 그냥 제자리
        public void EndDrag()
        {
            _dragging = false;
            _dragGhost.gameObject.SetActive(false);
        }

        public void DropOnSlot(BoardView.Source source, int index)
        {
            if (!_dragging || source == BoardView.Source.EnemyField)
                return;
            bool storage = source == BoardView.Source.Storage;
            if (_dragFromOffer)
                RunState.BuyInto(_dragIndex, storage, index);
            else
                RunState.Swap(_dragSource == BoardView.Source.Storage, _dragIndex, storage, index);
        }

        public void DropOnSell()
        {
            if (_dragging && !_dragFromOffer)
                RunState.Sell(_dragSource == BoardView.Source.Storage, _dragIndex);
        }

        // --- 표시 ---

        private static OwnedBuilding BuildingAt(BoardView.Source source, int index)
        {
            switch (source)
            {
                case BoardView.Source.Storage: return RunState.Storage[index];
                case BoardView.Source.AllyField: return RunState.Field[index];
                default: return BoardRunner.For(Team.Enemy).Slots[index].Building;
            }
        }

        private bool SelectionSellable => _hasSelection && _selectedSource != BoardView.Source.EnemyField;

        private void SellSelected()
        {
            if (!SelectionSellable)
                return;
            RunState.Sell(_selectedSource == BoardView.Source.Storage, _selectedIndex);
            _hasSelection = false;
        }

        private void Update()
        {
            for (int i = 0; i < _offers.Length; i++)
                _offers[i].Show(RunState.Offers[i], RunState.CanBuy(i));

            _status.text = $"라운드 {RunState.Round + 1}   골드 {RunState.Gold}   목숨 {RunState.Lives}   승리 {RunState.Wins}";
            _reroll.interactable = RunState.CanReroll;

            OwnedBuilding selected = _hasSelection ? BuildingAt(_selectedSource, _selectedIndex) : null;
            if (OwnedBuilding.IsEmpty(selected))
            {
                _hasSelection = false;
                selected = null;
            }
            // 끌어서 팔 수 있게 끄는 동안엔 판매 버튼이 늘 받는다
            bool dragSellable = _dragging && !_dragFromOffer;
            _sell.interactable = dragSellable || (SelectionSellable && selected != null);
            if (dragSellable)
                _sellLabel.text = $"판매 +{RunState.SellValue(BuildingAt(_dragSource, _dragIndex))}G";
            else
                _sellLabel.text = SelectionSellable && selected != null ? $"판매 +{RunState.SellValue(selected)}G" : "판매";
        }
    }
}
