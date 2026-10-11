using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 지도 씬 · 상점의 왼쪽 패널(사용자가 준 배치 10/11) — 위에서부터:
    //   [내 색 유닛] 내 영지 - 땅 N개 / 가진 업그레이드 아이콘 줄(여러 개면 「×N」)
    //   [그 영주 색 유닛] 영주 - 땅 N개(최후의 연합이면 「연합」) / 국가 / 지형 [아이콘] 이름 / 강화 [아이콘] 이름 / 패배시 · [하트] -N
    // 지형 · 업그레이드의 효과는 글로 쓰지 않는다 — 아이콘에 마우스를 올리면 뜬다(UI/UpgradeIcon). 행복도는 하트 아이콘이다.
    // 오른쪽 기둥에는 그 영주의 탑이 선다 — 상점 탑과 같은 층 그림(SlotView)이고 층에 마우스를 올리면 기물 설명 창이 뜬다. 내 땅을 고르면 내 탑.
    // 상점의 패널은 탑 기둥이 없다(틀 층을 비워 둔다) — 글의 자리 · 폭은 지도와 같아서 오갈 때 글이 움직이지 않는다.
    // 위 줄(턴 · 골드 · 하트 · 땅)도 여기서 그린다(ShowStatus) — 두 씬이 같은 줄을 쓴다. 무엇을 고를지 · 명령 버튼은 WorldManager가 정한다
    public class WorldSidePanel : MonoBehaviour
    {
        // 글 묶음 사이 · 줄 사이(px)
        private const float BlockGap = 28f;
        private const float LineGap = 6f;
        // 영주 표시(유닛 그림)가 들어갈 칸(px) — 그림은 이 안에 드는 가장 큰 정수배
        private const float LordIconBox = 32f;
        // 아이콘 줄(px) — 아이콘 한 변 · 아이콘과 그 옆 글 사이 · 줄 높이 · 「지형」 · 「강화」 머리말의 폭
        private const float IconSize = 32f;
        private const float LabelGap = 4f;
        private const float CellHeight = 36f;
        private const float PrefixWidth = 60f;
        // 가진 업그레이드 줄 — 한 칸의 폭 · 한 줄의 칸 수
        private const float CellWidth = 84f;
        private const int RowColumns = 3;
        // 위 줄 — 글과 하트 사이(px)
        private const float StatusGap = 48f;

        [Header("내 영지")]
        [SerializeField] private Image _mineIcon;
        [SerializeField] private Text _mineTitle;
        [Header("고른 땅이 없을 때의 안내")]
        [Tooltip("안내 글 — 고른 땅이 있으면 이 자리에 「맞닿지 않았다」는 회색 글이 뜬다")]
        [SerializeField] private Text _landBody;
        [TextArea] [SerializeField] private string _noLandHint = "화살표가 가리키는 땅을 눌러 침략할 곳을 고릅니다.";
        [Tooltip("상점 팝업이 지도를 덮고 있는 동안의 안내 글 — 그동안에는 땅을 누를 수 없다")]
        [TextArea] [SerializeField] private string _shopHint = "지도에서 땅을 고르면 여기에 표시됩니다.";
        [Header("고른 땅의 영주")]
        [SerializeField] private Image _lordIcon;
        [SerializeField] private Text _lordTitle;
        [Tooltip("국가 이름 한 줄")]
        [SerializeField] private Text _lordBody;
        [Tooltip("영주 표시 — 이 유닛 그림의 파란 자리를 그 영주의 색으로 칠해 보여 준다(전투에서 보게 될 모습 그대로)")]
        [SerializeField] private Sprite _lordSprite;
        [Tooltip("행복도 아이콘(하트) — 위 줄과 「패배시」 줄")]
        [SerializeField] private Sprite _heartIcon;
        [Header("영주의 탑")]
        [Tooltip("틀 층(꺼져 있음) — 이 자리가 1층, 위로 쌓는다. 비우면 탑을 그리지 않는다(상점)")]
        [SerializeField] private SlotView _slotTemplate;
        [SerializeField] private RectTransform _roof;
        [SerializeField] private float _step = 120f;

        // 머리말 + 아이콘 + 글 한 줄
        private class IconLine
        {
            public RectTransform Root;
            public Image Icon;
            public UpgradeIcon Tip;
            public Text Name;
        }

        private SlotView[] _slots;
        private Vector2 _origin;
        private UpgradeRow _owned;
        private IconLine _terrainLine, _upgradeLine, _loseLine;
        private Text _loseTitle;
        private HeartRow _hearts;
        private Text _statusLands;

        // 위 줄 — 「턴 N    골드 G」 + 행복도 하트(UI/HeartRow) + 「땅 n/30」. bar = 그 줄의 글(왼쪽 가운데 정렬)
        public void ShowStatus(Text bar, int turn)
        {
            if (_hearts == null)
            {
                _statusLands = Instantiate(bar, bar.transform);
                Anchor(_statusLands.rectTransform, new Vector2(0f, 0.5f));
                _statusLands.rectTransform.sizeDelta = new Vector2(0f, bar.rectTransform.sizeDelta.y);
                _hearts = new HeartRow(bar.rectTransform, _heartIcon);
            }
            bar.text = $"턴 {turn}    골드 <color={BuildingTooltip.NumberColor}>{RunState.Gold}</color>";
            float x = _hearts.Show(bar.preferredWidth + StatusGap);
            _statusLands.rectTransform.anchoredPosition = new Vector2(x + StatusGap, 0f);
            _statusLands.text = $"땅 {RunState.Lands}/{RunState.RegionCount}";
        }
        // selected = 고른 땅(없으면 WorldMapView.None). land = 고른 땅 · 영주 · 탑을 보여 주나 — 끄면 내 영지만(그 아래 자리는 WorldManager의 알림이 쓴다).
        // shop = 상점 팝업이 지도를 덮고 있나(고른 땅이 없을 때의 안내 글이 달라진다)
        public void Show(int selected, bool land, bool shop)
        {
            WorldRun world = RunState.World;
            WorldState state = world.State;
            if (_owned == null)
                Build();
            float left = _landBody.rectTransform.anchoredPosition.x;
            float y = _mineTitle.rectTransform.anchoredPosition.y;

            ShowLord(_mineIcon, state.Player);
            _mineTitle.text = $"내 영지 - 땅 {RunState.Lands}개";
            y = Title(_mineIcon, _mineTitle, y);
            // 내가 가진 업그레이드(UI/UpgradeRow)
            _owned.Root.anchoredPosition = new Vector2(left, y);
            y -= _owned.Show(world, state.Player) + BlockGap;

            bool any = land && selected >= 0;
            Lord lord = any ? state.OwnerOf(selected) : null;
            bool mine = any && lord == state.Player;
            bool invadable = any && !mine && RunState.InvadableRegions().Contains(selected);
            _landBody.enabled = land && (!any || (!mine && !invadable));
            _lordIcon.enabled = _lordTitle.enabled = any;
            _lordBody.enabled = any && !mine;
            _terrainLine.Root.gameObject.SetActive(any);
            _upgradeLine.Root.gameObject.SetActive(any);
            _loseTitle.gameObject.SetActive(any && !mine);
            _loseLine.Root.gameObject.SetActive(any && !mine);
            if (!any)
            {
                _landBody.text = shop ? _shopHint : _noLandHint;
                PlaceText(_landBody, left, y);
                ShowTower(null, false, Color.white);
                return;
            }

            ShowLord(_lordIcon, lord);
            int lands = state.LandsOf(lord);
            // 연합은 여러 번 이겨야 다 먹는다 — 남은 승리 수를 제목에
            _lordTitle.text = mine ? "내 땅" : lord == state.Coalition ? $"연합 - 땅 {lands}개 · 앞으로 {state.CoalitionWinsLeft}번" : $"영주 - 땅 {lands}개";
            y = Title(_lordIcon, _lordTitle, y);
            if (!mine)
            {
                _lordBody.text = world.Map.BuildOf(lord.Build).Nation.DisplayName();
                y = PlaceText(_lordBody, left, y) - LineGap;
            }
            MapDefinition terrain = world.Map.TerrainOf(selected);
            _terrainLine.Tip.Set(terrain.Icon, terrain.DisplayName, BuildingTooltip.Highlight(terrain.Description));
            _terrainLine.Name.text = terrain.DisplayName;
            y = Line(_terrainLine, left, y);
            RegionUpgradeDefinition upgrade = world.Map.UpgradeOf(selected);
            _upgradeLine.Tip.Set(upgrade, 1);
            _upgradeLine.Name.text = upgrade.DisplayName;
            y = Line(_upgradeLine, left, y);
            if (!mine)
            {
                y -= BlockGap;
                _loseTitle.rectTransform.anchoredPosition = new Vector2(left, y);
                y -= _loseTitle.rectTransform.sizeDelta.y + LineGap;
                _loseLine.Name.text = BuildingTooltip.Highlight($"-{state.Rules.LossDrop(lands)}");
                y = Line(_loseLine, left, y);
                // 상태 줄(효과가 아닌 것)은 회색, 맨 아래 — 「침략」이 왜 꺼졌는지
                if (!invadable)
                {
                    _landBody.text = $"<color={BuildingTooltip.NoteColor}>내 땅과 국경을 맞대지 않았습니다.</color>";
                    PlaceText(_landBody, left, y - LineGap);
                }
            }
            ShowTower(mine ? RunState.Field : world.TowerOf(lord).Field, !mine, RunState.ColorOf(lord));
        }

        // 코드가 만드는 줄들 — 글은 씬의 제목(도트 폰트)을 본떠 같은 폰트 · 크기로
        private void Build()
        {
            _owned = new UpgradeRow((RectTransform)transform, _mineTitle, CellWidth, RowColumns, false);
            _terrainLine = NewLine("Terrain", "지형", true);
            _upgradeLine = NewLine("Upgrade", "강화", true);
            _loseTitle = NewLabel((RectTransform)transform, 0f, 0f, "패배시");
            _loseLine = NewLine("Lose", "", false);
            _loseLine.Icon.sprite = _heartIcon;
            _loseLine.Icon.SetNativeSize();
            _loseLine.Icon.color = HeartRow.HeartColor;
        }

        private RectTransform NewRoot(string name)
        {
            var root = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            root.SetParent(transform, false);
            Anchor(root, new Vector2(0f, 1f));
            root.sizeDelta = Vector2.zero;
            return root;
        }

        // 머리말(없으면 "") + 아이콘 + 글 — hover면 아이콘에 마우스를 올려 설명을 본다
        private IconLine NewLine(string name, string prefix, bool hover)
        {
            var line = new IconLine { Root = NewRoot(name) };
            float x = 0f;
            if (prefix != "")
            {
                NewLabel(line.Root, 0f, 0f, prefix);
                x = PrefixWidth;
            }
            if (hover)
            {
                line.Tip = NewTip(line.Root, new Vector2(x, 0f));
                line.Icon = line.Tip.GetComponent<Image>();
            }
            else
            {
                line.Icon = NewImage(line.Root, "Icon");
                Anchor(line.Icon.rectTransform, new Vector2(0f, 1f));
                line.Icon.rectTransform.anchoredPosition = new Vector2(x, 0f);
            }
            line.Name = NewLabel(line.Root, x + IconSize + LabelGap, 0f, "");
            return line;
        }

        private static UpgradeIcon NewTip(RectTransform parent, Vector2 position)
        {
            var tip = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(UpgradeIcon)).GetComponent<UpgradeIcon>();
            var rect = (RectTransform)tip.transform;
            rect.SetParent(parent, false);
            Anchor(rect, new Vector2(0f, 1f));
            rect.anchoredPosition = position;
            tip.GetComponent<Image>().preserveAspect = true;
            return tip;
        }

        private static Image NewImage(RectTransform parent, string name)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        // 도트 폰트 글 한 줄 — 아이콘 높이에 세로 가운데
        private Text NewLabel(RectTransform parent, float x, float y, string text)
        {
            Text label = Instantiate(_mineTitle, parent);
            label.name = "Label";
            label.text = text;
            label.alignment = TextAnchor.MiddleLeft;
            Anchor(label.rectTransform, new Vector2(0f, 1f));
            label.rectTransform.anchoredPosition = new Vector2(x, y);
            label.rectTransform.sizeDelta = new Vector2(0f, IconSize);
            return label;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor) => rect.anchorMin = rect.anchorMax = rect.pivot = anchor;

        // 제목(아이콘이 그 왼쪽)을 y에 놓고 그 아래 끝의 y를 돌려준다 — 전부 왼쪽 위 기준
        private static float Title(Image icon, Text title, float y)
        {
            RectTransform rect = title.rectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
            icon.rectTransform.anchoredPosition = new Vector2(icon.rectTransform.anchoredPosition.x, y - rect.sizeDelta.y * 0.5f);
            return y - rect.sizeDelta.y - LineGap;
        }

        // 읽는 글을 (x, y)에 놓고 글 높이에 맞춘다 — 그 아래 끝의 y
        private static float PlaceText(Text text, float x, float y)
        {
            float height = text.preferredHeight;
            text.rectTransform.anchoredPosition = new Vector2(x, y);
            text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, height);
            return y - height;
        }

        private static float Line(IconLine line, float x, float y)
        {
            line.Root.anchoredPosition = new Vector2(x, y);
            return y - CellHeight;
        }

        // 유닛 그림을 그 영주의 색으로 — 그림 1픽셀 = 화면 정수 px
        private void ShowLord(Image icon, Lord lord)
        {
            icon.sprite = LordPaint.Of(_lordSprite, RunState.ColorOf(lord));
            Vector2 size = _lordSprite.rect.size;
            icon.rectTransform.sizeDelta = size * Mathf.Max(1, Mathf.FloorToInt(LordIconBox / Mathf.Max(size.x, size.y)));
        }

        // field = 보여 줄 탑(없으면 null — 탑을 숨긴다). 층 수만큼 1층부터 쌓고 맨 위에 그 영주 색 지붕(전장 탑과 같다)
        private void ShowTower(OwnedBuilding[] field, bool enemy, Color color)
        {
            if (_slotTemplate == null)
                return;
            // 층은 처음 그릴 때 만든다 — 런이 시작된 뒤여야 최대 층 수를 안다
            if (_slots == null)
            {
                _origin = ((RectTransform)_slotTemplate.transform).anchoredPosition;
                _slots = new SlotView[RunState.MaxFloors];
                for (int i = 0; i < _slots.Length; i++)
                {
                    _slots[i] = Instantiate(_slotTemplate, _slotTemplate.transform.parent);
                    ((RectTransform)_slots[i].transform).anchoredPosition = _origin + new Vector2(0f, i * _step);
                }
            }
            int floors = field != null ? Mathf.Min(field.Length, _slots.Length) : 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                bool shown = i < floors;
                if (_slots[i].gameObject.activeSelf != shown)
                    _slots[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                _slots[i].Bind(null, enemy ? BoardView.Source.EnemyField : BoardView.Source.AllyField, i);
                _slots[i].Show(field[i], null, false, false);
            }
            if (_roof.gameObject.activeSelf != floors > 0)
                _roof.gameObject.SetActive(floors > 0);
            float slotHalf = ((RectTransform)_slotTemplate.transform).rect.height * 0.5f;
            _roof.anchoredPosition = _origin + new Vector2(0f, (floors - 1) * _step + slotHalf);
            _roof.GetComponent<Image>().color = color;
        }
    }
}
