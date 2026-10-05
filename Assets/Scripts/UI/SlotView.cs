using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 건물 칸 하나 — 아이콘 · 레벨 (+ 전투 줄은 생산바). 빈 칸이면 틀만. 마우스를 올리면 설명 창(BuildingTooltip).
    // 누르면 선택, 끌어서 다른 칸·판매 버튼에 놓을 수 있다 — 규칙은 ShopPanel이 정한다
    public class SlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color SelectedColor = new Color(1f, 0.85f, 0.35f);
        // 끌려 나간 자리는 흐리게
        private const float DraggedAlpha = 0.35f;

        [SerializeField] private Image _frame;
        [SerializeField] private Image _icon;
        [SerializeField] private Text _level;
        [Tooltip("전투 줄에만 — 다음 생산까지 채워지는 층. Image Type이 Filled여야 fillAmount가 먹는다. 상점 칸은 비워 둔다")]
        [SerializeField] private Image _gauge;
        [SerializeField] private Button _button;

        private Color _frameColor;
        private ShopPanel _shop;
        private BoardView.Source _source;
        private int _index;
        private bool _hovered;

        public Button Button => _button;

        private void Awake() => _frameColor = _frame.color;

        private void OnDisable()
        {
            _hovered = false;
            BuildingTooltip.Hide(this);
        }

        public void Bind(ShopPanel shop, BoardView.Source source, int index)
        {
            _shop = shop;
            _source = source;
            _index = index;
        }

        // running = 전투 중 돌고 있는 칸(상점에선 null) — 생산바와 줄어든 간격을 여기서 읽는다
        public void Show(OwnedBuilding building, BoardRunner.Slot running, bool selected, bool dragged)
        {
            bool empty = OwnedBuilding.IsEmpty(building);
            _icon.enabled = !empty;
            if (!empty)
                _icon.sprite = building.Definition.Icon;
            _level.text = empty ? "" : building.Rarity.DisplayName();
            if (_gauge != null)
            {
                // 바 전체(바탕 = 채움 층의 부모)를 전투 중에만 보인다
                _gauge.transform.parent.gameObject.SetActive(!empty && running != null && running.Cooldown > 0f);
                _gauge.fillAmount = running == null ? 0f : Mathf.Clamp01(running.Timer / running.Cooldown);
            }
            Color frame = selected ? SelectedColor : _frameColor;
            frame.a = dragged ? DraggedAlpha : _frameColor.a;
            _frame.color = frame;

            if (!_hovered)
                return;
            if (empty || _shop.Dragging)
                BuildingTooltip.Hide(this);
            else
            {
                bool enemy = _source == BoardView.Source.EnemyField;
                float cooldown = running != null ? running.Cooldown : building.Definition.Cooldown;
                string body = BuildingTooltip.Describe(building.Definition, building.Level, cooldown);
                if (!enemy && !BattleManager.Fighting)
                    body += (building.Stacks > 0 ? BuildingTooltip.Note($"스택 {building.Stacks}") : "") + BuildingTooltip.UpgradeNote(building) + BuildingTooltip.Note($"팔면 {RunState.SellValue(building)}골드를 얻습니다.");
                OwnedBuilding[] board = enemy ? System.Array.ConvertAll(BoardRunner.For(Team.Enemy).Slots, s => s.Building) : RunState.Field;
                BuildingTooltip.Show(this, BuildingTooltip.Title(building.Definition, enemy, building.Rarity), body,
                    BuildingTooltip.UnitInfo(building.Definition, board));
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            BuildingTooltip.Hide(this);
        }

        public void OnBeginDrag(PointerEventData eventData) => _shop.BeginDragSlot(_source, _index, eventData);
        public void OnDrag(PointerEventData eventData) => _shop.Drag(eventData);
        public void OnEndDrag(PointerEventData eventData) => _shop.EndDrag();
        public void OnDrop(PointerEventData eventData) => _shop.DropOnSlot(_source, _index);
    }
}
