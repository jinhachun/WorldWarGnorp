using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 지도 씬의 왼쪽 패널 — 위에서부터 내 영지 요약(내 색 · 땅 · 행복도 · 가진 업그레이드) / 고른 땅(지형 · 업그레이드) / 그 땅의 영주
    // (빌드 방향 · 성향 · 가진 업그레이드 · 이기면 얻는 땅 · 지면 잃는 행복도). 글 묶음은 글 높이에 맞춰 아래로 쌓인다.
    // 오른쪽 기둥에는 그 영주의 탑이 선다 — 상점 탑과 같은 층 그림(SlotView)이고 층에 마우스를 올리면 기물 설명 창이 뜬다. 내 땅을 고르면 내 탑.
    // 무엇을 고를지 · 명령 버튼은 WorldManager가 정한다
    public class WorldSidePanel : MonoBehaviour
    {
        private static readonly string[] TemperNames = { "약탈자", "야심가", "무모한 자", "안전주의자" };
        // 글 묶음 사이 · 제목과 본문 사이(px)
        private const float BlockGap = 28f;
        private const float TitleGap = 6f;
        // 영주 표시(유닛 그림)가 들어갈 칸(px) — 그림은 이 안에 드는 가장 큰 정수배
        private const float LordIconBox = 32f;

        [Header("내 영지 요약")]
        [SerializeField] private Image _mineIcon;
        [SerializeField] private Text _mineTitle;
        [SerializeField] private Text _mineBody;
        [Header("고른 땅")]
        [SerializeField] private Image _landIcon;
        [SerializeField] private Text _landTitle;
        [SerializeField] private Text _landBody;
        [Header("그 땅의 영주")]
        [SerializeField] private Image _lordIcon;
        [SerializeField] private Text _lordTitle;
        [SerializeField] private Text _lordBody;
        [Tooltip("영주 표시 — 이 유닛 그림의 파란 자리를 그 영주의 색으로 칠해 보여 준다(전투에서 보게 될 모습 그대로)")]
        [SerializeField] private Sprite _lordSprite;
        [Header("영주의 탑")]
        [Tooltip("틀 층(꺼져 있음) — 이 자리가 1층, 위로 쌓는다")]
        [SerializeField] private SlotView _slotTemplate;
        [SerializeField] private RectTransform _roof;
        [SerializeField] private float _step = 120f;

        private SlotView[] _slots;
        private Vector2 _origin;

        private void Awake()
        {
            _origin = ((RectTransform)_slotTemplate.transform).anchoredPosition;
            _slots = new SlotView[RunState.MaxFloors];
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = Instantiate(_slotTemplate, _slotTemplate.transform.parent);
                ((RectTransform)_slots[i].transform).anchoredPosition = _origin + new Vector2(0f, i * _step);
            }
        }

        // selected = 고른 땅(없으면 WorldMapView.None). land = 고른 땅 · 영주 · 탑을 보여 주나 — 끄면 내 영지 요약만(그 아래 자리는 WorldManager의 알림이 쓴다)
        public void Show(int selected, bool land)
        {
            WorldRun world = RunState.World;
            WorldState state = world.State;
            float y = ((RectTransform)_mineTitle.transform).anchoredPosition.y;

            ShowLord(_mineIcon, state.Player);
            _mineTitle.text = "내 영지";
            _mineBody.text = BuildingTooltip.Highlight($"땅 {RunState.Lands}/{RunState.RegionCount}\n행복도 {RunState.Happiness}/{RunState.HappinessMax}\n가진 업그레이드: {Upgrades(state, state.Player)}");
            y = Stack(_mineIcon, _mineTitle, _mineBody, y);

            bool any = land && selected >= 0;
            Lord lord = any ? state.OwnerOf(selected) : null;
            bool mine = any && lord == state.Player;
            _landTitle.enabled = _landBody.enabled = land;
            _landIcon.enabled = any;
            _lordIcon.enabled = _lordTitle.enabled = _lordBody.enabled = any;
            if (!any)
            {
                _landTitle.text = "고른 땅 없음";
                _landBody.text = "땅을 누르면 그 땅과 영주의 탑을 봅니다.";
                Stack(_landIcon, _landTitle, _landBody, y);
                ShowTower(null, false, Color.white);
                return;
            }

            MapDefinition terrain = world.Map.TerrainOf(selected);
            RegionUpgradeDefinition upgrade = world.Map.UpgradeOf(selected);
            _landIcon.sprite = terrain.Icon;
            _landTitle.text = $"{selected + 1:00}번 땅 · {terrain.DisplayName}";
            _landBody.text = BuildingTooltip.Highlight($"{terrain.Description}\n{upgrade.DisplayName} — {upgrade.Description}");
            y = Stack(_landIcon, _landTitle, _landBody, y);

            ShowLord(_lordIcon, lord);
            int lands = state.LandsOf(lord);
            if (mine)
            {
                _lordTitle.text = "내 땅";
                _lordBody.text = "";
            }
            else
            {
                _lordTitle.text = $"영주 · 땅 {lands}개";
                string body = $"{world.Map.BuildOf(lord.Build).DisplayName}\n성향 {TemperNames[(int)lord.Temper]}\n가진 업그레이드: {Upgrades(state, lord)}\n이기면 땅 +{lands}\n지면 행복도 -{state.Rules.LossDrop(lands)}";
                body = BuildingTooltip.Highlight(body);
                // 상태 줄(효과가 아닌 것)은 회색, 맨 아래
                if (!RunState.InvadableRegions().Contains(selected))
                    body += $"\n<color={BuildingTooltip.NoteColor}>내 땅과 국경을 맞대지 않았습니다.</color>";
                _lordBody.text = body;
            }
            Stack(_lordIcon, _lordTitle, _lordBody, y);
            ShowTower(mine ? RunState.Field : world.TowerOf(lord).Field, !mine, RunState.ColorOf(lord));
        }

        // 제목(아이콘이 그 왼쪽) · 본문을 y부터 아래로 놓고 다음 묶음의 y를 돌려준다 — 전부 왼쪽 위 기준
        private static float Stack(Image icon, Text title, Text body, float y)
        {
            RectTransform titleRect = title.rectTransform;
            titleRect.anchoredPosition = new Vector2(titleRect.anchoredPosition.x, y);
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchoredPosition = new Vector2(iconRect.anchoredPosition.x, y - titleRect.sizeDelta.y * 0.5f);
            y -= titleRect.sizeDelta.y + TitleGap;
            RectTransform bodyRect = body.rectTransform;
            float height = body.text == "" ? 0f : body.preferredHeight;
            bodyRect.anchoredPosition = new Vector2(bodyRect.anchoredPosition.x, y);
            bodyRect.sizeDelta = new Vector2(bodyRect.sizeDelta.x, height);
            return y - height - BlockGap;
        }

        // 유닛 그림을 그 영주의 색으로 — 그림 1픽셀 = 화면 정수 px
        private void ShowLord(Image icon, Lord lord)
        {
            icon.sprite = LordPaint.Of(_lordSprite, RunState.ColorOf(lord));
            Vector2 size = _lordSprite.rect.size;
            icon.rectTransform.sizeDelta = size * Mathf.Max(1, Mathf.FloorToInt(LordIconBox / Mathf.Max(size.x, size.y)));
        }

        // 그 영주의 땅들에 붙은 업그레이드 — 「무기 강화 2 · 세금 증산 1」
        private static string Upgrades(WorldState state, Lord lord)
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

        // field = 보여 줄 탑(없으면 null — 탑을 숨긴다). 층 수만큼 1층부터 쌓고 맨 위에 그 영주 색 지붕(전장 탑과 같다)
        private void ShowTower(OwnedBuilding[] field, bool enemy, Color color)
        {
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
