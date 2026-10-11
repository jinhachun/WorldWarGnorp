using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 침략 목록(임시 화면 — 월드맵 그림이 생기면 그쪽으로 옮긴다) — 상점에서 「침략」을 누르면 열린다. 내 땅과 이어진 땅마다 한 줄:
    // 그 땅의 영주(색 · 빌드 방향 · 성향 · 땅 수) / 이 땅의 지형 · 업그레이드 / 이기면 얻는 것 · 지면 잃는 행복도.
    // 줄을 가리키면 그 영주의 탑과 그 땅의 지형이 상점 화면에 뜬다(RunState.Preview — 오른쪽 적 탑 자리 · 맵 칸). 줄을 누르면 고른 것이고,
    // 고른 채로 「침략」을 한 번 더 눌러야 전투에 들어간다. 고른 땅은 목록을 닫아도 남아, 그 영주의 탑을 보며 상점을 볼 수 있다.
    // 판 그림은 없다 — 검은 바탕 + 글 + 흰 선. 줄은 열 때마다 코드가 만든다
    public class InvadePanel : MonoBehaviour
    {
        private const float Pad = 20f;
        private const float HeaderHeight = 44f;
        private const float FooterHeight = 100f;
        private const float RowMinHeight = 108f;
        private const float LeftWidth = 520f;
        private const float IconBox = 32f;
        private const int DotSize = 24;
        private const int CleanSize = 22;

        private static readonly string[] TemperNames = { "약탈자", "야심가", "무모한 자", "안전주의자" };
        private static readonly Color SelectedColor = new Color(1f, 0.85f, 0.35f, 0.22f);
        private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.35f);

        [SerializeField] private BattleManager _battle;
        [Tooltip("도트 폰트 — 이름 · 숫자처럼 짧은 글")]
        [SerializeField] private Font _dotFont;
        [Tooltip("깔끔 폰트 — 지형 효과 · 업그레이드 설명처럼 읽는 글")]
        [SerializeField] private Font _cleanFont;
        [Tooltip("「쉬기」 · 「닫기」 버튼의 본 — 상점의 어두운 버튼 하나(그림 원본 크기 그대로 복제한다)")]
        [SerializeField] private Button _buttonTemplate;
        [Tooltip("줄 앞에 그 영주의 색으로 칠해 보여 줄 유닛 그림")]
        [SerializeField] private Sprite _lordSprite;

        private class Row
        {
            public int Region;
            public Image Background;
        }

        private readonly List<Row> _rows = new List<Row>();
        private Text _header;
        private RectTransform _content;
        private int _selected = -1;
        private int _hovered = -1;

        // 상점의 「침략」 버튼을 누를 수 있나 — 닫혀 있으면 여는 버튼이고, 열려 있으면 땅을 골라야 눌린다
        public bool CanPress => !gameObject.activeSelf || _selected >= 0;

        // 상점의 「침략」 버튼 — 닫혀 있으면 목록을 열고, 열려 있고 땅을 골랐으면 그 땅으로 전투에 들어간다
        public void OnStartPressed()
        {
            if (!gameObject.activeSelf)
            {
                // 진열 카드는 상점이 뜰 때 만들어져 이 판보다 뒤 형제다 — 열 때마다 맨 위로
                transform.SetAsLastSibling();
                gameObject.SetActive(true);
            }
            else if (_selected >= 0)
                _battle.Invade(_selected);
        }

        private void Awake()
        {
            var root = (RectTransform)transform;
            _header = NewText(root, _dotFont, DotSize, TextAnchor.MiddleLeft);
            Place(_header.rectTransform, Pad, 0f, root.rect.width - Pad * 2f, HeaderHeight);
            Text hint = NewText(root, _cleanFont, CleanSize, TextAnchor.MiddleRight);
            Place(hint.rectTransform, Pad, 0f, root.rect.width - Pad * 2f, HeaderHeight);
            hint.text = "땅을 고른 뒤 「침략」을 누르면 전투에 들어갑니다.";
            Line(root, HeaderHeight);

            // 줄 목록 — 화면에 다 안 들어가면 굴린다
            var list = new GameObject("List", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
            var view = (RectTransform)list.transform;
            view.SetParent(root, false);
            Place(view, 0f, HeaderHeight + 2f, root.rect.width, root.rect.height - HeaderHeight - FooterHeight - 4f);
            _content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
            _content.SetParent(view, false);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = _content.offsetMax = Vector2.zero;
            var scroll = list.GetComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            Line(root, root.rect.height - FooterHeight);
            Button rest = NewButton("쉬기", Pad + 110f);
            rest.onClick.AddListener(_battle.Rest);
            Button close = NewButton("닫기", Pad + 110f + 240f);
            close.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void OnEnable()
        {
            Rebuild();
        }

        // 닫아도 고른 땅의 미리보기는 남는다 — 가리키던 것만 뗀다
        private void OnDisable()
        {
            _hovered = -1;
            if (RunState.Started && RunState.Phase == RunState.RunPhase.Shop)
                RunState.Preview(_selected);
        }

        private void Update()
        {
            _header.text = $"턴 {RunState.Round + 1} · 땅 {RunState.Lands}/{RunState.RegionCount} · 행복도 {RunState.Happiness}/{RunState.HappinessMax}";
            foreach (Row row in _rows)
                row.Background.color = row.Region == _selected ? SelectedColor : row.Region == _hovered ? HoverColor : Color.clear;
        }

        private void Rebuild()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
            _rows.Clear();
            List<int> regions = RunState.InvadableRegions();
            regions.Sort();
            if (!regions.Contains(_selected))
                _selected = -1;
            float y = 0f;
            foreach (int region in regions)
                y += AddRow(region, y);
            _content.sizeDelta = new Vector2(0f, y);
            RunState.Preview(_selected);
        }

        // 한 줄을 y 아래에 놓고 그 높이를 돌려준다
        private float AddRow(int region, float y)
        {
            WorldRun world = RunState.World;
            WorldState state = world.State;
            Lord lord = state.OwnerOf(region);
            int lands = state.LandsOf(lord);
            float width = ((RectTransform)transform).rect.width;

            var row = new GameObject($"Row{region + 1:00}", typeof(RectTransform), typeof(Image), typeof(InvadeRow));
            var rect = (RectTransform)row.transform;
            rect.SetParent(_content, false);
            var background = row.GetComponent<Image>();
            background.color = Color.clear;
            row.GetComponent<InvadeRow>().Bind(this, region);

            // 영주의 색 — 유닛 그림의 파란 자리를 그 색으로(전투에서 보게 될 모습 그대로). 그림 1픽셀 = 정수배
            var icon = new GameObject("Lord", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(rect, false);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.sprite = LordPaint.Of(_lordSprite, world.ColorOf(lord));
            Vector2 size = _lordSprite.rect.size * Mathf.Max(1, Mathf.FloorToInt(IconBox / Mathf.Max(_lordSprite.rect.width, _lordSprite.rect.height)));
            Place(icon.rectTransform, Pad, 10f, size.x, size.y);

            MapDefinition terrain = world.Map.TerrainOf(region);
            RegionUpgradeDefinition upgrade = world.Map.UpgradeOf(region);
            float left = Pad + IconBox + 14f;
            Text title = NewText(rect, _dotFont, DotSize, TextAnchor.MiddleLeft);
            Place(title.rectTransform, left, 6f, LeftWidth, 32f);
            title.text = $"{region + 1:00}번 땅 · {terrain.DisplayName}";
            Text owner = NewText(rect, _dotFont, DotSize, TextAnchor.MiddleLeft);
            Place(owner.rectTransform, left, 38f, LeftWidth, 32f);
            owner.text = $"{world.Map.BuildOf(lord.Build).DisplayName} · {TemperNames[(int)lord.Temper]}";
            Text outcome = NewText(rect, _dotFont, DotSize, TextAnchor.MiddleLeft);
            Place(outcome.rectTransform, left, 70f, LeftWidth, 32f);
            outcome.text = $"이기면 땅 <color={BuildingTooltip.NumberColor}>+{lands}</color> · 지면 행복도 <color={BuildingTooltip.NumberColor}>-{state.Rules.LossDrop(lands)}</color>";

            // 읽는 글 — 이 땅의 지형 효과 · 이 땅의 업그레이드 · 이기면 함께 얻는 업그레이드(그 영주의 다른 땅 것까지)
            string body = terrain.Description + $"\n{upgrade.DisplayName} — {upgrade.Description}";
            string gains = Gains(state, lord);
            if (lands > 1)
                body += $"\n이기면 얻는 업그레이드: {gains}";
            float right = left + LeftWidth + 16f;
            Text description = NewText(rect, _cleanFont, CleanSize, TextAnchor.UpperLeft);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.text = BuildingTooltip.Highlight(body);
            Place(description.rectTransform, right, 8f, width - right - Pad, 0f);
            float bodyHeight = description.preferredHeight;
            description.rectTransform.sizeDelta = new Vector2(description.rectTransform.sizeDelta.x, bodyHeight);

            float height = Mathf.Max(RowMinHeight, bodyHeight + 20f);
            Place(rect, 0f, y, width, height);
            Line(rect, height - 2f);
            _rows.Add(new Row { Region = region, Background = background });
            return height;
        }

        // 그 영주의 땅들에 붙은 업그레이드 — 「무기 강화 2 · 세금 증산 1」
        private static string Gains(WorldState state, Lord lord)
        {
            var names = new List<string>();
            var counts = new List<int>();
            for (int region = 0; region < state.Owner.Length; region++)
            {
                if (state.OwnerOf(region) != lord)
                    continue;
                string name = RunState.World.Map.UpgradeOf(region).DisplayName;
                int at = names.IndexOf(name);
                if (at < 0)
                {
                    names.Add(name);
                    counts.Add(1);
                }
                else
                    counts[at]++;
            }
            for (int i = 0; i < names.Count; i++)
                names[i] += $" {counts[i]}";
            return string.Join(" · ", names);
        }

        public void Hover(int region, bool on)
        {
            if (on)
                _hovered = region;
            else if (_hovered == region)
                _hovered = -1;
            RunState.Preview(_hovered >= 0 ? _hovered : _selected);
        }

        // 누르면 고른다 — 고른 줄을 다시 누르면 푼다
        public void Select(int region)
        {
            _selected = _selected == region ? -1 : region;
            RunState.Preview(_hovered >= 0 ? _hovered : _selected);
        }

        // 왼쪽 위 기준으로 (x, y)에 놓는다 — y는 아래로
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Text NewText(RectTransform parent, Font font, int size, TextAnchor anchor)
        {
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // 흰 가로선 — parent 폭 전체, 위에서 y만큼 아래
        private static void Line(RectTransform parent, float y)
        {
            var line = new GameObject("Line", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            line.transform.SetParent(parent, false);
            line.color = LineColor;
            line.raycastTarget = false;
            RectTransform rect = line.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(0f, 2f);
        }

        // 상점의 어두운 버튼을 복제한 글자 버튼(아이콘 없이) — 아래 줄 가운데 높이, 중심 x
        private Button NewButton(string label, float centerX)
        {
            Button button = Instantiate(_buttonTemplate, transform);
            button.name = label;
            button.interactable = true;
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(centerX, FooterHeight * 0.5f);
            Text text = button.GetComponentInChildren<Text>();
            text.text = label;
            foreach (Image image in button.GetComponentsInChildren<Image>())
                if (image.gameObject != button.gameObject)
                    Destroy(image.gameObject);
            return button;
        }
    }

    // 침략 목록의 한 줄 — 가리킴 · 누름을 InvadePanel에 알린다
    public class InvadeRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private InvadePanel _panel;
        private int _region;

        public void Bind(InvadePanel panel, int region)
        {
            _panel = panel;
            _region = region;
        }

        public void OnPointerEnter(PointerEventData eventData) => _panel.Hover(_region, true);
        public void OnPointerExit(PointerEventData eventData) => _panel.Hover(_region, false);
        public void OnPointerClick(PointerEventData eventData) => _panel.Select(_region);
    }
}
