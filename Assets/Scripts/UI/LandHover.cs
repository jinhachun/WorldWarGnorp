using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 지도의 땅에 마우스를 올리면 뜨는 땅 카드(사용자 10/26 — 지도에 늘 떠 있던 지형 아이콘 대신 · 10/27 시안 B 「아이콘 + 짧은 값」).
    // 같은 판(UI_LandCard = 검은 판 + NationFrame) · 마우스 오른쪽 아래(모자라면 반대쪽) · 폭 440, 높이는 글에 맞춘다. 위에서부터:
    //   [영주 그림] 이름(도트 36 — 나는 「내 영지」, 연합은 「닉네임 연합」) / [국가 아이콘] 국가 [왕관] 땅 수(나는 없음) · 오른쪽 위 [하트] -1(침략할 수 있는 땅)
    //   [지형 아이콘] 지형 이름 / 그 아래 짧은 효과 줄(깔끔 22 · 줄 안 스탯 아이콘) — 이벤트 땅에는 쓰지 않는다(싸우지 않는다)
    //   [지역 특수 아이콘] 이름 / 그 아래 효과 한 줄
    // 이벤트 땅: 미지의 땅(UI/UnknownLand)은 [?] 이름 + 회색 한 줄, 드러난 땅은 이름 + 효과 글, 닫힌 땅은 흐리게 + 「이미 방문했습니다.」. 자세한 것(업그레이드 줄 · 탑)은 누르면 뜨는 왼쪽 패널.
    // 🔸 지형 효과 줄은 「~합니다」체(CLAUDE.md §5-2 1번)의 예외 — 훑어보는 이 카드에서만 값 위주로 짧게 쓴다(TerrainLines, 사용자 10/27 「직관적 · 간결」).
    // 명령을 받는 동안에만, 다른 설명 창(칸 · 탑 층 · 지역 특수 칸)이 떠 있으면 비킨다. 뷰 — 상태를 읽기만 한다
    public class LandHover : MonoBehaviour
    {
        // 판 문법 — 머리(그림 칸 · 이름)는 UI/UnitHover와 같은 값
        private const float Width = 440f;
        private const float Pad = 24f;
        private const float Side = 28f;
        private const float IconBox = 64f;
        private const float NameGap = 16f;
        // 이름과 국가 줄 사이 · 묶음(머리 · 지형 · 지역 특수 · 본문) 사이 · 묶음 머리 줄과 그 아래 효과 줄 사이
        private const float SubGap = 4f;
        private const float Section = 14f;
        private const float RowGap = 2f;
        // 효과 줄 들여쓰기(= 아이콘 32 + 아이콘 옆 틈 8) · 아이콘과 그 옆 글 사이 · 국가 줄의 아이콘 옆 틈 · 국가와 땅 수 사이
        private const float Indent = 40f;
        private const float IconGap = 8f;
        private const float SmallGap = 6f;
        private const float PairGap = 16f;
        private const float Offset = 24f;
        private const float Margin = 8f;
        private const int NameSize = 36;
        private const int SubSize = 24;
        // 영주 그림 — 원본 1px = 화면 4px(몸 8px → 32px, 깃발까지 아이콘 칸 64 안)
        private const int FigureScale = 4;
        private static readonly Color Faded = new Color(1f, 1f, 1f, 0.35f);
        private const string UnknownLine = "가 봐야 압니다";
        // 프리팹 UI_LandCard 안 왕관 그림 — 이 컴포넌트의 참조는 씬에 있어 이름으로 찾는다
        private const string LandsIconName = "LandsIcon";

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
        [SerializeField] private Sprite _lordFlag;
        [SerializeField] private Sprite _unknownIconLarge;

        // 실행 중에 만드는 칸 — 도트 글은 _name, 깔끔 글은 _body를 본으로 복제한다
        private Image _nationIcon, _landsIcon, _terrainIcon, _specialIcon;
        private Text _nationText, _landsText, _terrainText, _specialName, _specialBody;
        private InlineIcons _bodyIcons, _specialIcons;

        private int _shown = WorldMapView.None;
        // 마지막으로 그린 상태 — 바뀔 때만 다시 짠다
        private string _shownKey;

        private void Awake()
        {
            _card.gameObject.SetActive(false);
            // 본에 줄 안 아이콘을 붙이기 전에 복제한다(붙인 뒤면 아이콘 자식까지 따라온다)
            _specialBody = Instantiate(_body, _card);
            _specialBody.name = "SpecialBody";
            _bodyIcons = _body.gameObject.AddComponent<InlineIcons>();
            _specialIcons = _specialBody.gameObject.AddComponent<InlineIcons>();
            _nationText = DotText("Nation");
            _landsText = DotText("Lands");
            _terrainText = DotText("Terrain");
            _specialName = DotText("SpecialName");
            _nationIcon = NewIcon("NationIcon");
            _terrainIcon = NewIcon("TerrainIcon");
            _specialIcon = NewIcon("SpecialIcon");
            _landsIcon = _card.Find(LandsIconName).GetComponent<Image>();
        }

        private Text DotText(string name)
        {
            Text text = Instantiate(_name, _card);
            text.name = name;
            return text;
        }

        private Image NewIcon(string name)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            RectTransform rect = image.rectTransform;
            rect.SetParent(_card, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

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

            string body = "";
            bool owned = false, terrainShown = false;
            UpgradeDefinition special = null;
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
                _name.text = $"<size={NameSize}>{(closed ? Note(name) : name)}</size>";
                // 미지의 땅은 지형 효과 · 긴 안내를 쓰지 않는다(사용자 10/27 「너무 복잡해」)
                if (hidden)
                    _name.text += $"\n<size={SubSize}>{Note(UnknownLine)}</size>";
                else
                    body = BuildingTooltip.Highlight(RunState.EventText(region)) + (closed ? "\n" + Note("이미 방문했습니다.") : "");
            }
            else
            {
                bool mine = lord == state.Player, coalition = state.CoalitionOf(lord) != null;
                var figure = (RectTransform)new GameObject("Lord", typeof(RectTransform)).transform;
                figure.SetParent(_icon, false);
                // 그림의 자리 = 몸 한가운데 — 깃발까지 든 그림(몸 아래 ~ 깃발 위, 국가마다 몸 크기가 다르다)이 칸 가운데에 오게 내린다
                Sprite lordBody = WorldLords.BodyOf(lord.Id);
                (float down, float up) = WorldLords.Reach(lordBody, _lordFlag);
                figure.anchoredPosition = new Vector2(0f, -(up - down) * 0.5f * FigureScale);
                WorldLords.Figure(figure, lordBody, _lordFlag, WorldLords.PhotoOf(lord.Id), RunState.ColorOf(lord), FigureScale);
                _name.text = $"<size={NameSize}>{(mine ? "내 영지" : coalition ? RunState.NameOf(lord) + " 연합" : RunState.NameOf(lord))}</size>";
                // 내 국가 · 땅 수는 위 줄과 왼쪽 패널에서 이미 읽힌다
                owned = !mine;
                if (owned)
                {
                    BuildingNation nation = RunState.World.NationOf(lord);
                    _nationIcon.sprite = RunState.Config.World.NationIcon(nation);
                    _nationText.text = nation.DisplayName();
                    _landsText.text = BuildingTooltip.ColorNumbers(state.LandsOf(lord).ToString());
                }
                MapDefinition terrain = RunState.World.Map.TerrainOf(region);
                terrainShown = true;
                _terrainIcon.sprite = terrain.Icon;
                _terrainText.text = terrain.DisplayName;
                body = BuildingTooltip.Highlight(string.Join("\n", TerrainLines(terrain)));
                special = RunState.SpecialAt(region);
                if (special != null)
                {
                    _specialIcon.sprite = special.Icon;
                    _specialName.text = special.DisplayName;
                    _specialIcons.SetText(BuildingTooltip.Highlight(special.Describe(special.ValueAt(BuildingRarity.Mythic))));
                }
            }
            _bodyIcons.SetText(body);
            _body.gameObject.SetActive(body.Length > 0);
            Show(owned, _nationIcon, _nationText, _landsIcon, _landsText);
            Show(terrainShown, _terrainIcon, _terrainText);
            Show(special != null, _specialIcon, _specialName, _specialBody);
            _loseIcon.gameObject.SetActive(invadable);
            _loseText.gameObject.SetActive(invadable);
            if (invadable)
                _loseText.text = RunState.InCrisis ? "게임 오버" : BuildingTooltip.Highlight("-1");
            Layout(isEvent);
        }

        // 지형 효과의 짧은 줄 — 수치는 맵 에셋에서(§5-2 6번), 글은 이 카드 전용 짧은 표기(위 🔸). 효과가 없는 지형(기본)은 줄이 없다
        private static IEnumerable<string> TerrainLines(MapDefinition map)
        {
            foreach (MapDefinition.UnitRule rule in map.UnitRules)
                yield return rule.Describe();
            if (map.FireAllInterval > 0f)
                yield return $"{map.FireAllInterval:0.#}초마다 모든 기물 발동";
            // 게이지가 차는 속도 2배 = 쿨다운 ×0.5
            if (map.CooldownRate != 1f)
                yield return $"모든 기물의 쿨다운 ×{1f / map.CooldownRate:0.##}";
            if (map.PassiveBuildingsOff)
                yield return "쿨다운 없는 기물 작동 안 함";
            if (map.FireRepeats > 1)
                yield return $"쿨다운 있는 기물 효과 {map.FireRepeats}번";
        }

        private static void Show(bool on, params Graphic[] parts)
        {
            foreach (Graphic part in parts)
                part.gameObject.SetActive(on);
        }

        private static string Note(string text) => $"<color={BuildingTooltip.NoteColor}>{text}</color>";

        // 위에서부터 머리 / 지형 줄 + 효과 / 지역 특수 + 효과 (이벤트 땅은 머리 / 본문) — 판 높이를 글에 맞춘다
        private void Layout(bool isEvent)
        {
            float x0 = Side + IconBox + NameGap;
            float nameWidth = _name.preferredWidth, nameHeight = _name.preferredHeight;
            bool owned = _nationIcon.gameObject.activeSelf;
            float subHeight = owned ? Mathf.Max(PairHeight(_nationIcon, _nationText), PairHeight(_landsIcon, _landsText)) : 0f;
            float block = nameHeight + (owned ? SubGap + subHeight : 0f);
            float header = Mathf.Max(IconBox, block), top = Pad + (header - block) / 2f;
            Put(_icon, Side, Pad + (header - IconBox) / 2f, IconBox, IconBox);
            Put(_name.rectTransform, x0, Mathf.Round(top), nameWidth, nameHeight);
            if (owned)
            {
                float x = x0 + Pair(_nationIcon, _nationText, x0, top + nameHeight + SubGap, SmallGap, subHeight) + PairGap;
                Pair(_landsIcon, _landsText, x, top + nameHeight + SubGap, SmallGap, subHeight);
            }
            // 패배 배지 — 오른쪽 위. 이름이 길어 닿으면 국가 줄 높이로 내린다
            if (_loseIcon.gameObject.activeSelf)
            {
                float height = PairHeight(_loseIcon, _loseText);
                float badge = _loseIcon.rectTransform.sizeDelta.x + SmallGap + _loseText.preferredWidth, bx = Width - Side - badge;
                float by = x0 + nameWidth + NameGap > bx ? top + nameHeight + SubGap : Pad;
                Pair(_loseIcon, _loseText, bx, by, SmallGap, height);
            }
            float y = Pad + header;
            if (_terrainIcon.gameObject.activeSelf)
            {
                y += Section;
                float height = PairHeight(_terrainIcon, _terrainText);
                Pair(_terrainIcon, _terrainText, Side, y, IconGap, height);
                y += height;
            }
            if (_body.gameObject.activeSelf)
            {
                // 지형 효과는 지형 이름 아래 들여 쓰고, 이벤트 글은 판 너비 그대로
                y += isEvent ? Section : RowGap;
                float x = isEvent ? Side : Side + Indent;
                y += Paragraph(_body, _bodyIcons, x, y);
            }
            if (_specialIcon.gameObject.activeSelf)
            {
                y += Section;
                float height = PairHeight(_specialIcon, _specialName);
                Pair(_specialIcon, _specialName, Side, y, IconGap, height);
                y += height + RowGap;
                y += Paragraph(_specialBody, _specialIcons, Side + Indent, y);
            }
            _card.sizeDelta = new Vector2(Width, y + Pad);
        }

        // 깔끔 글 한 덩이를 (x, y)부터 판 오른쪽 여백까지 — 줄 안 아이콘은 높이를 정한 뒤에 얹는다. 차지한 높이
        private static float Paragraph(Text text, InlineIcons icons, float x, float y)
        {
            float width = Width - Side - x;
            text.rectTransform.sizeDelta = new Vector2(width, 0f);
            float height = text.preferredHeight;
            Put(text.rectTransform, x, y, width, height);
            icons.Place();
            return height;
        }

        private static float PairHeight(Image icon, Text text)
        {
            icon.SetNativeSize();
            return Mathf.Max(icon.rectTransform.sizeDelta.y, text.preferredHeight);
        }

        // 아이콘 + 글 한 쌍을 (x, y)부터 줄 높이 안 세로 가운데로 — 차지한 폭
        private static float Pair(Image icon, Text text, float x, float y, float gap, float height)
        {
            icon.SetNativeSize();
            Vector2 size = icon.rectTransform.sizeDelta;
            icon.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y + (height - size.y) / 2f));
            float width = text.preferredWidth, textHeight = text.preferredHeight;
            Put(text.rectTransform, Mathf.Round(x + size.x + gap), Mathf.Round(y + (height - textHeight) / 2f), width, textHeight);
            return size.x + gap + width;
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
            Rect screen = Letterbox.Rect;
            float x = mouse.x + offset;
            if (x + w > screen.xMax - margin)
                x = mouse.x - offset - w;
            x = Mathf.Max(screen.xMin + margin, x);
            float top = mouse.y - offset;
            if (top - h < screen.yMin + margin)
                top = screen.yMin + h + margin;
            top = Mathf.Min(top, screen.yMax - margin);
            _card.position = new Vector3(x, top, 0f);
        }
    }
}
