using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 지도 씬의 왼쪽 패널 — 고른 땅의 영주가 주인공인 초상화 배치(사용자가 준 그림 10/12), 글 기둥 안에서 전부 가운데 정렬. 위에서부터:
    //   [영주 그림 — 지도와 같은 깃발 든 유닛, 크게] / 닉네임(나는 「내 영지」, 최후의 연합은 「연합」 + 「땅 N개 · 앞으로 k번」) / 국가 / 그 영주가 가진 업그레이드 아이콘 줄(「×N」)
    //   ─ 가로 구분선 ─
    //   지형 : [아이콘] 이름 / 패배시 · [하트] -N
    // 고른 땅이 없으면 같은 배치로 나(구분선 아래는 안내 글만), 내 땅을 고르면 나 + 그 땅의 지형.
    // 내가 가진 업그레이드는 여기가 아니라 위 줄의 「땅 n/30」 오른쪽에 늘어놓는다(ShowStatus — 넘치면 좌우로 끌어서 민다).
    // 지형 · 업그레이드의 효과는 글로 쓰지 않는다 — 아이콘에 마우스를 올리면 뜬다(UI/UpgradeIcon). 행복도는 하트 아이콘이다.
    // 오른쪽 기둥에는 그 영주의 탑이 선다 — 상점 탑과 같은 층 그림(SlotView)이고 층에 마우스를 올리면 기물 설명 창이 뜬다. 내 땅을 고르면 내 탑.
    // 무엇을 고를지 · 명령 버튼은 WorldManager가 정한다
    public class WorldSidePanel : MonoBehaviour
    {
        // 글 묶음 사이 · 줄 사이(px)
        private const float BlockGap = 20f;
        private const float LineGap = 6f;
        // 초상화 — 원본 1px이 화면 몇 px인지(몸 8×8 → 64px, 전의 패널 아이콘의 2배) · 패널 위 끝에서 그림 위 끝까지(px)
        private const int PortraitScale = 8;
        private const float PortraitTop = 28f;
        // 초상화가 차지하는 높이(원본 px, 몸 한가운데에서) — 깃발 든 그림의 위 · 아래 끝
        private const float FigureUp = 7f, FigureDown = -4f;
        // 아이콘 줄(px) — 아이콘 한 변 · 아이콘과 그 옆 글 사이 · 줄 높이 · 「지형 :」 머리말의 폭
        private const float IconSize = 32f;
        private const float LabelGap = 4f;
        private const float CellHeight = 36f;
        private const float PrefixWidth = 76f;
        // 가진 업그레이드 줄 — 한 칸의 폭 · 한 줄의 칸 수(패널) · 한 칸에서 아이콘과 글이 차지하는 폭(가운데 정렬용)
        private const float CellWidth = 84f;
        private const int RowColumns = 3;
        private const float CellInk = 68f;
        // 구분선의 두께(px)
        private const float DividerHeight = 2f;
        // 위 줄 — 글과 하트 사이(px)
        private const float StatusGap = 48f;

        [Header("영주(고른 땅의 영주 — 없으면 나)")]
        [Tooltip("닉네임 — 도트 폰트 글 하나. 패널의 다른 도트 글도 이 글을 본뜬다. 이 글의 폭이 글 기둥의 폭이다")]
        [SerializeField] private Text _title;
        [Tooltip("국가 이름 한 줄")]
        [SerializeField] private Text _nation;
        [Tooltip("안내 글 — 고른 땅이 없을 때의 안내, 고른 땅이 있으면 「맞닿지 않았다」는 회색 글")]
        [SerializeField] private Text _landBody;
        [TextArea] [SerializeField] private string _noLandHint = "화살표가 가리키는 땅을 눌러 침략할 곳을 고릅니다.";
        [Tooltip("영주 그림 — 유닛 몸 그림(파란 자리를 그 영주의 색으로 칠한다)과 드는 깃발(흰 그림). 지도의 영주 그림과 같은 것")]
        [SerializeField] private Sprite _lordSprite;
        [SerializeField] private Sprite _lordFlag;
        [Tooltip("행복도 아이콘(하트) — 위 줄과 「패배시」 줄")]
        [SerializeField] private Sprite _heartIcon;
        [Header("영주의 탑")]
        [Tooltip("틀 층(꺼져 있음) — 이 자리가 1층, 위로 쌓는다. 비우면 탑을 그리지 않는다")]
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
            // 머리말의 폭(없으면 0)
            public float Prefix;
        }

        private SlotView[] _slots;
        private Vector2 _origin;
        private RectTransform _portrait;
        private Lord _portrayed;
        private Text _share;
        private UpgradeRow _owned;
        private Image _divider, _pillar;
        private IconLine _terrainLine, _loseLine;
        private Text _loseTitle;
        private HeartRow _hearts;
        private Text _statusLands;
        private UpgradeRow _mine;
        private RectTransform _mineView;

        // 위 줄 — 「턴 N    골드 G」 + 행복도 하트(UI/HeartRow) + 「땅 n/30」 + 내가 가진 업그레이드 아이콘 줄(UI/UpgradeRow). bar = 그 줄의 글(왼쪽 가운데 정렬).
        // 업그레이드 줄은 bar의 오른쪽 끝까지가 창이고, 넘치면 좌우로 끌어서 민다(스크롤바 없음)
        public void ShowStatus(Text bar, int turn)
        {
            if (_hearts == null)
            {
                _statusLands = Instantiate(bar, bar.transform);
                Anchor(_statusLands.rectTransform, new Vector2(0f, 0.5f));
                _statusLands.rectTransform.sizeDelta = new Vector2(0f, bar.rectTransform.sizeDelta.y);
                _hearts = new HeartRow(bar.rectTransform, _heartIcon);

                var view = new GameObject("Upgrades", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
                _mineView = (RectTransform)view.transform;
                _mineView.SetParent(bar.transform, false);
                Anchor(_mineView, new Vector2(0f, 0.5f));
                // 끌기를 받는 투명한 판
                view.GetComponent<Image>().color = Color.clear;
                _mine = new UpgradeRow(_mineView, _title, CellWidth, int.MaxValue, false);
                var scroll = view.GetComponent<ScrollRect>();
                scroll.viewport = _mineView;
                scroll.content = _mine.Root;
                scroll.horizontal = true;
                scroll.vertical = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
            }
            bar.text = $"턴 {turn}    골드 <color={BuildingTooltip.NumberColor}>{RunState.Gold}</color>";
            float x = _hearts.Show(bar.preferredWidth + StatusGap);
            _statusLands.rectTransform.anchoredPosition = new Vector2(x + StatusGap, 0f);
            _statusLands.text = $"땅 {RunState.Lands}/{RunState.RegionCount}";
            x += StatusGap + _statusLands.preferredWidth + StatusGap;
            _mineView.anchoredPosition = new Vector2(x, 0f);
            _mineView.sizeDelta = new Vector2(Mathf.Max(0f, bar.rectTransform.rect.width - x), UpgradeRow.BoxSize);
            _mine.Show(RunState.UpgradesOf(RunState.World.State.Player));
            // 밀 수 있는 폭 = 줄의 폭(끈 자리는 그대로 둔다)
            _mine.Root.sizeDelta = new Vector2(_mine.Width, UpgradeRow.BoxSize);
        }

        // 탑 기둥의 가로 자리(1층 틀 칸의 가운데)
        public float TowerX => ((RectTransform)_slotTemplate.transform).anchoredPosition.x;

        // selected = 고른 땅(없으면 WorldMapView.None). land = 고른 땅 · 영주 · 탑을 보여 주나 — 끄면 나만(그 아래 자리는 WorldManager의 알림이 쓴다).
        // shop = 상점 팝업이 보이나 — 그동안에는 나만 보여 주고 탑 기둥은 상점의 내 탑에 내준다
        public void Show(int selected, bool land, bool shop)
        {
            WorldRun world = RunState.World;
            WorldState state = world.State;
            if (_owned == null)
                Build();
            // 글 기둥의 한가운데 · 왼쪽 끝 · 폭
            float left = _title.rectTransform.anchoredPosition.x - _title.rectTransform.sizeDelta.x * 0.5f;
            float center = _title.rectTransform.anchoredPosition.x, width = _title.rectTransform.sizeDelta.x;

            // 상점이 열린 동안에는 고른 땅과 상관없이 나를 보여 준다(사용자 10/12 — 탑 기둥도 내 탑이다). 그 아래는 비운다
            bool any = land && !shop && selected >= 0;
            Lord lord = any ? state.OwnerOf(selected) : state.Player;
            bool mine = lord == state.Player;
            bool coalition = lord == state.Coalition;
            bool invadable = any && !mine && RunState.InvadableRegions().Contains(selected);

            // 초상화 — 몸(발)이 기둥 한가운데(이름 글의 중심)에 온다. 깃발은 옆으로 삐져나온다(사용자 10/12)
            if (_portrayed != lord)
            {
                _portrayed = lord;
                foreach (Transform part in _portrait)
                    Destroy(part.gameObject);
                WorldLords.Figure(_portrait, _lordSprite, _lordFlag, RunState.ColorOf(lord), PortraitScale);
            }
            float y = -PortraitTop;
            _portrait.anchoredPosition = new Vector2(center, y - FigureUp * PortraitScale);
            y -= (FigureUp - FigureDown) * PortraitScale + BlockGap;

            _title.text = mine ? "내 영지" : coalition ? "연합" : RunState.NameOf(lord);
            y = Place(_title.rectTransform, center, y) - LineGap;
            // 연합은 여러 번 이겨야 다 먹는다 — 남은 땅과 승리 수
            _share.gameObject.SetActive(coalition);
            if (coalition)
            {
                _share.text = $"땅 {state.LandsOf(lord)}개 · 앞으로 {state.CoalitionWinsLeft}번";
                y = Place(_share.rectTransform, center, y) - LineGap;
            }
            _nation.text = (mine ? RunState.Nation : world.Map.BuildOf(lord.Build).Nation).DisplayName();
            y = PlaceText(_nation, center, y);
            // 그 영주가 가진 업그레이드 — 내 것은 위 줄에 있다
            var holdings = mine ? new System.Collections.Generic.List<UpgradeHolding>() : RunState.UpgradesOf(lord);
            int kinds = holdings.Count;
            float rowHeight = _owned.Show(holdings);
            if (kinds > 0)
            {
                y -= LineGap;
                _owned.Root.anchoredPosition = new Vector2(center - ((Mathf.Min(kinds, RowColumns) - 1) * CellWidth + CellInk) * 0.5f, y);
                y -= rowHeight;
            }

            _landBody.enabled = land && !shop && (!any || (!mine && !invadable));
            // 세로 구분선은 탑 기둥을 쓰는 동안에만 — 알림(AI 턴 · 요약 · 끝)과 국가 고르기 때는 긋지 않는다
            if (_pillar != null)
                _pillar.enabled = land;
            _divider.enabled = any;
            _terrainLine.Root.gameObject.SetActive(any);
            _loseTitle.gameObject.SetActive(any && !mine);
            _loseLine.Root.gameObject.SetActive(any && !mine);
            if (!any)
            {
                _landBody.text = _noLandHint;
                PlaceText(_landBody, center, y - BlockGap);
                ShowTower(null, false, Color.white);
                return;
            }

            y -= BlockGap * 0.5f;
            _divider.rectTransform.anchoredPosition = new Vector2(left, y);
            _divider.rectTransform.sizeDelta = new Vector2(width, DividerHeight);
            y -= DividerHeight + BlockGap * 0.5f;

            MapDefinition terrain = world.Map.TerrainOf(selected);
            _terrainLine.Tip.Set(terrain.Icon, terrain.DisplayName, BuildingTooltip.Highlight(terrain.Description));
            _terrainLine.Name.text = terrain.DisplayName;
            y = Line(_terrainLine, center, y);
            if (!mine)
            {
                y -= BlockGap * 0.5f;
                y = Place(_loseTitle.rectTransform, center, y);
                _loseLine.Name.text = BuildingTooltip.Highlight($"-{state.Rules.LossDrop(state.LandsOf(lord))}");
                y = Line(_loseLine, center, y);
                // 상태 줄(효과가 아닌 것)은 회색, 맨 아래 — 「침략」이 왜 꺼졌는지
                if (!invadable)
                {
                    _landBody.text = $"<color={BuildingTooltip.NoteColor}>내 땅과 국경을 맞대지 않았습니다.</color>";
                    PlaceText(_landBody, center, y - LineGap);
                }
            }
            ShowTower(mine ? RunState.Field : world.TowerOf(lord).Field, !mine, RunState.ColorOf(lord));
        }

        // 코드가 만드는 줄들 — 글은 씬의 닉네임 글(도트 폰트)을 본떠 같은 폰트 · 크기로
        private void Build()
        {
            _title.alignment = TextAnchor.MiddleCenter;
            _nation.alignment = _landBody.alignment = TextAnchor.UpperCenter;
            _portrait = NewRoot("Portrait");
            _share = Instantiate(_title, transform);
            _share.name = "Share";
            _owned = new UpgradeRow((RectTransform)transform, _title, CellWidth, RowColumns, false);
            _divider = NewImage((RectTransform)transform, "Divider");
            Anchor(_divider.rectTransform, new Vector2(0f, 1f));
            // 정보 기둥과 탑 기둥 사이의 세로 구분선(사용자 10/12) — 패널 위에서 바닥까지. 가로 구분선과 같은 굵기 · 색
            if (_slotTemplate != null)
            {
                var slot = (RectTransform)_slotTemplate.transform;
                float columnRight = _title.rectTransform.anchoredPosition.x + _title.rectTransform.sizeDelta.x * 0.5f;
                float towerLeft = slot.anchoredPosition.x - slot.rect.width * 0.5f;
                Image pillar = _pillar = NewImage((RectTransform)transform, "Pillar");
                Anchor(pillar.rectTransform, new Vector2(0f, 1f));
                pillar.rectTransform.anchoredPosition = new Vector2(Mathf.Round((columnRight + towerLeft - DividerHeight) * 0.5f), 0f);
                pillar.rectTransform.sizeDelta = new Vector2(DividerHeight, ((RectTransform)transform).rect.height);
            }
            _terrainLine = NewLine("Terrain", "지형 :", true);
            _loseTitle = Instantiate(_title, transform);
            _loseTitle.name = "LoseTitle";
            _loseTitle.text = "패배시";
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
            if (prefix != "")
            {
                NewLabel(line.Root, 0f, 0f, prefix);
                line.Prefix = PrefixWidth;
            }
            float x = line.Prefix;
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

        // 도트 폰트 글 한 줄(왼쪽 정렬) — 아이콘 높이에 세로 가운데
        private Text NewLabel(RectTransform parent, float x, float y, string text)
        {
            Text label = Instantiate(_title, parent);
            label.name = "Label";
            label.text = text;
            label.alignment = TextAnchor.MiddleLeft;
            Anchor(label.rectTransform, new Vector2(0f, 1f));
            label.rectTransform.anchoredPosition = new Vector2(x, y);
            label.rectTransform.sizeDelta = new Vector2(0f, IconSize);
            return label;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor) => rect.anchorMin = rect.anchorMax = rect.pivot = anchor;

        // 가운데 정렬 글(위 가운데 기준)을 (center, y)에 놓고 그 아래 끝의 y를 돌려준다
        private static float Place(RectTransform rect, float center, float y)
        {
            rect.anchoredPosition = new Vector2(center, y);
            return y - rect.sizeDelta.y;
        }

        // 읽는 글(위 가운데 기준)을 (center, y)에 놓고 글 높이에 맞춘다 — 그 아래 끝의 y
        private static float PlaceText(Text text, float center, float y)
        {
            float height = text.preferredHeight;
            text.rectTransform.anchoredPosition = new Vector2(center, y);
            text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, height);
            return y - height;
        }

        // 머리말 + 아이콘 + 글 한 줄을 통째로 가운데에 놓는다 — 그 아래 끝의 y
        private static float Line(IconLine line, float center, float y)
        {
            float width = line.Prefix + IconSize + LabelGap + line.Name.preferredWidth;
            line.Root.anchoredPosition = new Vector2(Mathf.Round(center - width * 0.5f), y);
            return y - CellHeight;
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
