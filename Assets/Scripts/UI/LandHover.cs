using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 지도의 땅에 마우스를 올리면 뜨는 땅 카드(사용자 10/26 — 지도에 늘 떠 있던 지형 아이콘 대신). 전투 유닛 호버 카드(UI/UnitHover)와 같은 문법 —
    // 같은 판(UI_LandCard = 검은 판 + NationFrame) · 마우스 오른쪽 아래(모자라면 반대쪽) · 이름 도트 36 + 한 줄 24 · 본문 깔끔 22.
    // 훑어보기만: 주인(영주 그림 + 이름 — 나는 「내 영지」, 연합은 「연합」) · 국가 · 땅 수 / 지형 이름 + 그 지형의 전투 효과 / 지역 특수(이름 + 효과) / 침략할 수 있는 땅이면 「패배시 ♥ -1」.
    // 이벤트 땅은 미지의 땅 규칙(UI/UnknownLand)을 따르고, 닫힌 땅은 무엇이었는지 흐리게 + 「이미 방문했습니다.」. 자세한 것(업그레이드 줄 · 탑)은 누르면 뜨는 왼쪽 패널.
    // 명령을 받는 동안에만, 다른 설명 창(칸 · 탑 층 · 지역 특수 칸)이 떠 있으면 비킨다. 뷰 — 상태를 읽기만 한다
    public class LandHover : MonoBehaviour
    {
        // 판 문법 — UI/UnitHover와 같은 값
        private const float Width = 420f;
        private const float Pad = 24f;
        private const float Side = 28f;
        private const float InnerGap = 8f;
        private const float IconBox = 64f;
        private const float NameGap = 16f;
        private const float RowIconGap = 8f;
        private const float Offset = 24f;
        private const float Margin = 8f;
        private const int NameSize = 36;
        private const int SubSize = 24;
        // 영주 그림 — 원본 1px = 화면 4px(몸 8px → 32px, 깃발까지 아이콘 칸 64 안)
        private const int FigureScale = 4;
        private static readonly Color Faded = new Color(1f, 1f, 1f, 0.35f);

        [SerializeField] private WorldMapView _map;
        [Tooltip("상점 팝업 — 떠 있는 동안은 카드를 띄우지 않는다")]
        [SerializeField] private ShopPanel _shop;
        [Tooltip("이벤트 방문 장면 — 떠 있는 동안은 카드를 띄우지 않는다")]
        [SerializeField] private EventVisitPopup _visit;
        [Header("카드(부품 프리팹 UI_LandCard의 인스턴스 — 왼위 기준, 레이캐스트를 막지 않는다)")]
        [SerializeField] private RectTransform _card;
        [SerializeField] private RectTransform _icon;
        [SerializeField] private Text _name;
        [SerializeField] private Text _body;
        [SerializeField] private Image _loseIcon;
        [SerializeField] private Text _loseText;
        [Header("그림")]
        [SerializeField] private Sprite _lordSprite;
        [SerializeField] private Sprite _lordFlag;
        [SerializeField] private Sprite _unknownIconLarge;

        private int _shown = WorldMapView.None;
        // 마지막으로 그린 상태 — 바뀔 때만 다시 짠다
        private string _shownKey;

        private void Awake() => _card.gameObject.SetActive(false);

        private void Update()
        {
            // 자리는 지도가 받은 마지막 포인터 이동(WorldMapView.HoverPosition) — 지도가 가리킨 땅과 같은 사건에서 읽는다
            int region = _map.Hovered;
            bool show = region >= 0 && WorldManager.Commanding && !BuildingTooltip.IsOpen
                && !(_shop != null && _shop.gameObject.activeInHierarchy) && !(_visit != null && _visit.Busy);
            if (_card.gameObject.activeSelf != show)
                _card.gameObject.SetActive(show);
            if (!show)
            {
                _shown = WorldMapView.None;
                return;
            }
            Fill(region);
            Place(_map.HoverPosition);
        }

        private void Fill(int region)
        {
            WorldState state = RunState.World.State;
            bool isEvent = state.Graph.IsEvent[region];
            Lord lord = isEvent ? null : state.OwnerOf(region);
            bool invadable = !isEvent && RunState.InvadableRegions().Contains(region);
            string key = $"{region}|{state.Turn}|{(lord != null ? lord.Id : -1)}|{(lord != null ? state.LandsOf(lord) : 0)}|{state.EventClosed[region]}|{UnknownLand.Hidden(region)}|{invadable}|{RunState.InCrisis}|{RunState.SpecialAt(region)}";
            if (region == _shown && key == _shownKey)
                return;
            _shown = region;
            _shownKey = key;
            foreach (Transform part in _icon)
                Destroy(part.gameObject);

            MapDefinition terrain = RunState.World.Map.TerrainOf(region);
            string body = $"지형 : {terrain.DisplayName}";
            if (!string.IsNullOrEmpty(terrain.Description))
                body += "\n" + BuildingTooltip.Highlight(terrain.Description);
            bool lose = false;
            if (isEvent)
            {
                bool hidden = UnknownLand.Hidden(region), closed = state.EventClosed[region];
                EventLandDefinition definition = RunState.EventOf(region);
                Sprite sprite = hidden ? _unknownIconLarge : definition.IconLarge != null ? definition.IconLarge : definition.Icon;
                Image image = new GameObject("Event", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(_icon, false);
                image.raycastTarget = false;
                image.sprite = sprite;
                image.preserveAspect = true;
                image.SetNativeSize();
                image.color = closed ? Faded : Color.white;
                string name = hidden ? UnknownLand.Name : definition.DisplayName;
                if (closed)
                    name = Note(name);
                _name.text = $"<size={NameSize}>{name}</size>\n<size={SubSize}>{Note("이벤트 땅")}</size>";
                body += "\n\n" + (hidden ? UnknownLand.Line : BuildingTooltip.Highlight(RunState.EventText(region)));
                if (closed)
                    body += "\n" + Note("이미 방문했습니다.");
            }
            else
            {
                bool mine = lord == state.Player, coalition = lord == state.Coalition;
                var figure = (RectTransform)new GameObject("Lord", typeof(RectTransform)).transform;
                figure.SetParent(_icon, false);
                // 그림의 자리 = 몸 한가운데 — 깃발까지 든 그림(몸 아래 4px ~ 위 11px, UI/WorldSidePanel 초상화와 같은 범위)이 칸 가운데에 오게 내린다
                figure.anchoredPosition = new Vector2(0f, -3.5f * FigureScale);
                WorldLords.Figure(figure, _lordSprite, _lordFlag, WorldLords.PhotoOf(lord.Id), RunState.ColorOf(lord), FigureScale);
                string name = mine ? "내 영지" : coalition ? "연합" : RunState.NameOf(lord);
                string nation = (mine ? RunState.Nation : RunState.World.NationOf(lord)).DisplayName();
                _name.text = $"<size={NameSize}>{name}</size>\n<size={SubSize}>{nation} · 땅 {state.LandsOf(lord)}</size>";
                UpgradeDefinition special = RunState.SpecialAt(region);
                if (special != null)
                    body += $"\n\n지역 특수 : {special.DisplayName}\n{BuildingTooltip.Highlight(special.Describe(special.ValueAt(BuildingRarity.Mythic)))}";
                lose = invadable;
            }
            _body.text = body;
            _loseIcon.gameObject.SetActive(lose);
            _loseText.gameObject.SetActive(lose);
            if (lose)
                _loseText.text = RunState.InCrisis ? "패배시 게임 오버" : $"패배시 {BuildingTooltip.Highlight("-1")}";
            Layout();
        }

        private static string Note(string text) => $"<color={BuildingTooltip.NoteColor}>{text}</color>";

        // 위에서부터 그림 · 이름 / 본문 / 패배시 줄 — 판 높이를 글에 맞춘다
        private void Layout()
        {
            float nameHeight = _name.preferredHeight;
            float header = Mathf.Max(IconBox, nameHeight);
            Put(_icon, Side, Pad + (header - IconBox) / 2f, IconBox, IconBox);
            Put(_name.rectTransform, Side + IconBox + NameGap, Pad + (header - nameHeight) / 2f, Width - Side * 2f - IconBox - NameGap, nameHeight);
            float y = Pad + header + InnerGap;
            _body.rectTransform.sizeDelta = new Vector2(Width - Side * 2f, 0f);
            float bodyHeight = _body.preferredHeight;
            Put(_body.rectTransform, Side, y, Width - Side * 2f, bodyHeight);
            y += bodyHeight;
            if (_loseIcon.gameObject.activeSelf)
            {
                y += InnerGap;
                float iconHeight = _loseIcon.rectTransform.sizeDelta.y, textHeight = _loseText.preferredHeight;
                float row = Mathf.Max(iconHeight, textHeight);
                _loseIcon.rectTransform.anchoredPosition = new Vector2(Side, -(y + (row - iconHeight) / 2f));
                Put(_loseText.rectTransform, Side + _loseIcon.rectTransform.sizeDelta.x + RowIconGap, y + (row - textHeight) / 2f, _loseText.preferredWidth, textHeight);
                y += row;
            }
            _card.sizeDelta = new Vector2(Width, y + Pad);
        }

        private static void Put(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        // 마우스 오른쪽 아래 — 오른쪽이 모자라면 왼쪽, 아래가 모자라면 위로(UI/UnitHover와 같다. Overlay 캔버스라 월드 좌표 = 화면 픽셀)
        private void Place(Vector2 mouse)
        {
            float scale = _card.lossyScale.x;
            float w = _card.sizeDelta.x * scale, h = _card.sizeDelta.y * scale;
            float offset = Offset * scale, margin = Margin * scale;
            float x = mouse.x + offset;
            if (x + w > Screen.width - margin)
                x = mouse.x - offset - w;
            x = Mathf.Max(margin, x);
            float top = mouse.y - offset;
            if (top - h < margin)
                top = h + margin;
            top = Mathf.Min(top, Screen.height - margin);
            _card.position = new Vector3(x, top, 0f);
        }
    }
}
