using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 결과 창의 「이번 전투」 요약(사용자 10/20 U6) — 내 탑 층마다 이번 전투에 소환한 유닛 수(아이콘 + 「N기」, 많은 순) · 두 본진이 입은 피해.
    // 소환 수는 내 보드의 Summoned 이벤트를 세고(추가 소환·변환 반영 · 칸 없는 소환은 뺀다 · 뷰는 상태를 바꾸지 않는다), 피해는 결과 창이 뜰 때 본진 체력에서 읽는다.
    // 결과 창(ResultPanel)은 실행 중 꺼져 있어 거기 붙이면 전투 동안 못 센다 — 이 오브젝트는 켜 둔 채 결과 창 안의 칸에 그린다
    public class BattleSummary : MonoBehaviour
    {
        // 칸 하나의 폭 · 아이콘과 숫자 사이(px) · 보여 줄 칸 수 상한
        private const float CellWidth = 96f;
        private const float CountGap = 4f;
        private const int MaxCells = 8;
        private const int CountSize = 24;   // 도트 폰트 12의 정수배

        [Tooltip("결과 창 — 이것이 켜지는 순간 요약을 그린다")]
        [SerializeField] private GameObject _resultPanel;
        [Tooltip("층 칸이 놓이는 줄(가운데 기준) — 결과 창 안")]
        [SerializeField] private RectTransform _cells;
        [Tooltip("숫자 글의 본(도트 폰트) — 결과 창 안의 글 하나")]
        [SerializeField] private Text _countStyle;
        [Tooltip("본진 피해 한 줄 — 결과 창 안")]
        [SerializeField] private Text _damage;
        [SerializeField] private Base _allyBase;
        [SerializeField] private Base _enemyBase;

        // 층 번호 → 이번 전투 소환 수
        private readonly Dictionary<int, int> _summoned = new Dictionary<int, int>();
        private BoardRunner _board;
        private bool _shown;

        private void Update()
        {
            if (_board == null)
            {
                _board = BoardRunner.For(Team.Ally);
                if (_board != null)
                    _board.Summoned += OnSummoned;
            }
            bool open = _resultPanel.activeInHierarchy;
            if (open && !_shown)
                Show();
            _shown = open;
        }

        private void OnDestroy()
        {
            if (_board != null)
                _board.Summoned -= OnSummoned;
        }

        private void OnSummoned(int slot, int count)
        {
            if (slot >= 0)
                _summoned[slot] = (_summoned.TryGetValue(slot, out int n) ? n : 0) + count;
        }

        private void Show()
        {
            for (int i = _cells.childCount - 1; i >= 0; i--)
                Destroy(_cells.GetChild(i).gameObject);
            var rows = new List<(BuildingDefinition Building, int Count)>();
            foreach (var pair in _summoned)
            {
                OwnedBuilding building = pair.Key < _board.Slots.Length ? _board.Slots[pair.Key].Building : null;
                if (!OwnedBuilding.IsEmpty(building))
                    rows.Add((building.Definition, pair.Value));
            }
            rows.Sort((a, b) => b.Count.CompareTo(a.Count));
            int shown = Mathf.Min(rows.Count, MaxCells);
            for (int i = 0; i < shown; i++)
                Cell(rows[i].Building, rows[i].Count, (i - (shown - 1) * 0.5f) * CellWidth);
            // 빈 상태 — 칸 자리가 비어 보이지 않게 한 줄
            if (shown == 0)
                Line(_cells, "소환한 기물이 없습니다.", 0f, 0f);

            int dealt = Lost(_enemyBase), taken = Lost(_allyBase);
            _damage.text = $"적 본진에 준 피해 <color={BuildingTooltip.NumberColor}>{dealt}</color>    내 본진이 받은 피해 <color={BuildingTooltip.NumberColor}>{taken}</color>";
        }

        private static int Lost(Base target) => target == null ? 0 : Mathf.Max(0, Mathf.RoundToInt(target.MaxHp - Mathf.Max(0f, target.Hp)));

        // 기물 아이콘(원본 크기) + 그 아래 「N기」
        private void Cell(BuildingDefinition building, int count, float x)
        {
            var root = (RectTransform)new GameObject(building.name, typeof(RectTransform)).transform;
            root.SetParent(_cells, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(x, 0f);
            root.sizeDelta = Vector2.zero;
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.rectTransform.SetParent(root, false);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0.5f, 1f);
            icon.sprite = building.Icon;
            icon.SetNativeSize();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Line(root, $"<color={BuildingTooltip.NumberColor}>{count}</color>기", 0f, -(icon.rectTransform.sizeDelta.y + CountGap));
        }

        // 숫자 글의 본(회색 도트 글)을 본뜬 한 줄 — parent의 (x, y)에 위 가운데 기준
        private void Line(RectTransform parent, string content, float x, float y)
        {
            Text text = Instantiate(_countStyle, parent);
            text.name = "Line";
            text.gameObject.SetActive(true);
            text.fontSize = CountSize;
            text.alignment = TextAnchor.UpperCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = content;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(CellWidth, CountSize);
        }
    }
}
