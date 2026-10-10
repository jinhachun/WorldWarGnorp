using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 건물 칸 하나 — 아이콘 · 레벨 (탑 층이면 벽·창·창 불빛). 빈 칸이면 틀만. 마우스를 올리면 설명 창(BuildingTooltip).
    // 누르면 선택, 끌어서 다른 칸·판매 버튼에 놓을 수 있다 — 규칙은 ShopPanel이 정한다
    public class SlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color SelectedColor = new Color(1f, 0.85f, 0.35f);
        // 끌려 나간 자리는 흐리게
        private const float DraggedAlpha = 0.35f;
        // 효과가 터진 칸이 잠깐 커졌다 돌아온다 (The Bazaar식 '통통 튐') — 배속과 상관없이 같은 길이로
        private const float BounceScale = 1.2f;
        private const float BounceDuration = 0.2f;

        [SerializeField] private Image _frame;
        [Tooltip("등급 색 테두리(지금 등급) — 빈 칸이면 숨김")]
        [SerializeField] private Image _rarityFrame;
        [SerializeField] private Image _icon;
        [SerializeField] private Text _level;
        [SerializeField] private Button _button;
        [Tooltip("탑 층만(필드 탑 칸) — 전장 탑과 같은 층 그림(벽 = _frame · 창틀 = _rarityFrame · 창 불빛). 비우면 보통 칸(보관함)")]
        [SerializeField] private FloorLook _floor;
        [Tooltip("전투 중 탑 칸만 — 받은 효과 표시(칸 옆): 가속 = 날개 + 남은 시간 「0.4초」 · 쿨다운 배율 = 「×0.8」. 비우면 안 보임")]
        [SerializeField] private GameObject _hasteMark;
        [SerializeField] private Text _hasteText;
        [SerializeField] private GameObject _cooldownMark;
        [SerializeField] private Text _cooldownText;
        [Tooltip("데스매치 동안 오버드라이브 칸의 번개 — 가속 표시 자리에, 숫자 없이")]
        [SerializeField] private Sprite _overdriveIcon;

        private Color _frameColor;
        private ShopPanel _shop;
        private BoardView.Source _source;
        private int _index;
        private bool _hovered;
        private float _bounce = BounceDuration;
        // 데스매치 동안 오버드라이브 칸의 번개(Fx/OverdriveMark) — 가속 표시를 본떠 처음 뜰 때 만든다
        private GameObject _overdriveMark;

        public Button Button => _button;
        public BoardView.Source Source => _source;
        public int Index => _index;

        private void Awake() => _frameColor = _frame.color;

        public void Bounce() => _bounce = 0f;

        // 마우스를 올리지 않은 테두리는 한 단계 어둡게 — 올리면(끄는 중 놓을 자리 포함) 제 밝기로 강조된다
        private const float IdleDim = 0.65f;

        public static Color Highlight(Color color, bool hovered)
            => hovered ? color : new Color(color.r * IdleDim, color.g * IdleDim, color.b * IdleDim, color.a);

        private void Update()
        {
            // 한 프레임이 길어도(전투 시작 프레임 등) 튐이 건너뛰어지지 않게 프레임당 진행을 묶는다
            _bounce += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            transform.localScale = Vector3.one * Mathf.Lerp(BounceScale, 1f, Mathf.Clamp01(_bounce / BounceDuration));
        }

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

        // running = 전투 중 돌고 있는 칸(상점에선 null) — 창 불빛(쿨다운)과 쌓인 값을 여기서 읽는다
        public void Show(OwnedBuilding building, BoardRunner.Slot running, bool selected, bool dragged)
        {
            bool empty = OwnedBuilding.IsEmpty(building);
            _icon.enabled = !empty;
            if (!empty)
                _icon.sprite = building.Definition.Icon;
            // 등급은 테두리 색이 보여 주므로 글자는 대표 숫자(지금 등급 값). 전투 중 값을 쌓는 칸(허수아비 등)은 지금까지 쌓인 값
            _level.text = empty ? "" : running?.BattleGainText ?? BuildingTooltip.Headline(building.Definition, building.Level);
            // 탑 층은 빈 층도 꺼진 창틀을 보인다(칸 틀 색) — 보통 칸은 빈 칸이면 테두리를 숨긴다
            _rarityFrame.enabled = !empty || _floor != null;
            _rarityFrame.color = Highlight(empty ? _frameColor : building.Rarity.FrameColor(), _hovered);
            // 창 불빛이 쿨다운 게이지 — 전투 중 쿨다운 기물은 찬 만큼 아래부터, 쿨다운 없는 기물·상점은 은은히
            if (_floor != null)
                _floor.Show(empty ? null : building.Definition, running != null && running.Cooldown > 0f ? running.Timer / running.Cooldown : -1f);
            // 탑 층 벽은 전장 탑처럼 늘 제 밝기 — 호버 강조는 창틀 밝기로만
            Color frame = selected ? SelectedColor : _floor != null ? _frameColor : Highlight(_frameColor, _hovered);
            frame.a = dragged ? DraggedAlpha : _frameColor.a;
            _frame.color = frame;

            if (_hasteMark != null)
            {
                // 전장 탑 표시(TowerBadge.ShowSeconds)와 같은 숫자 — 0.1초 단위 반올림, 0이 되면 숨고, 10초부터는 내림 정수.
                // 쿨다운 없는 칸은 값이 0 · 1로 온다(안 보인다)
                int tenths = running != null ? Mathf.RoundToInt(running.HasteLeft * 10f) : 0;
                _hasteMark.SetActive(tenths > 0);
                if (tenths > 0)
                    _hasteText.text = tenths < 100 ? $"{tenths / 10}.{tenths % 10}초" : $"{tenths / 10}초";
                float scale = running != null ? running.CooldownScale : 1f;
                bool scaled = Mathf.Abs(scale - 1f) > 0.005f;
                _cooldownMark.SetActive(scaled);
                if (scaled)
                    _cooldownText.text = $"×{scale:0.##}";
            }

            ShowOverdrive(_hasteMark != null && OverdriveMark.On(building, _source == BoardView.Source.EnemyField ? Team.Enemy : Team.Ally));

            if (!_hovered)
                return;
            if (empty || _shop.Dragging)
                BuildingTooltip.Hide(this);
            else
                BuildingTooltip.ShowOwned(this, building, running, _source == BoardView.Source.EnemyField);
        }

        private void ShowOverdrive(bool on)
        {
            if (_overdriveMark == null)
            {
                if (!on)
                    return;
                // 가속 표시를 그대로 본뜬다(같은 자리·같은 크기) — 그림과 색만 바꾸고 숫자는 뺀다
                _overdriveMark = Instantiate(_hasteMark, transform);
                _overdriveMark.name = "OverdriveMark";
                Image icon = _overdriveMark.GetComponentInChildren<Image>(true);
                icon.sprite = _overdriveIcon;
                icon.color = OverdriveMark.Color;
                _overdriveMark.GetComponentInChildren<Text>(true).gameObject.SetActive(false);
            }
            if (_overdriveMark.activeSelf != on)
                _overdriveMark.SetActive(on);
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
