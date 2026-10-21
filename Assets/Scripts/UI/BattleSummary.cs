using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GnorpWar
{
    // 결과 창 — 판(검은 속 + 회색 2px 선 + 매듭, 스타일 가이드 「판 선」)과 딜 미터기(사용자 10/21).
    // 딜 미터기 = 내 탑 층마다 그 기물이 낸 유닛들이 이번 전투에 준 피해(많은 순 · 1위는 영주 색 그대로, 나머지는 어둡게) + 「타워」 · 「기타」 줄(작은 아이콘, 맨 아래).
    // 「나 / 적」 탭(사용자 10/22)으로 적 진영의 같은 표를 본다 — 적 기물 이름은 빨강, 막대는 적 영주 색. 판 크기는 두 쪽 중 긴 쪽에 맞춰 고정, 기본은 「나」.
    // 숫자는 전투 상태(Battle/DamageMeter)를 읽기만 한다. 판 높이 · 너비는 줄 수 · 이름 길이에 맞춰 이 코드가 정한다 — 제목 · 영향 · 버튼은 판의 위 · 아래 모서리에 붙어 따라온다.
    // 열리면 판이 위에서 계단으로 내려앉고, 막대가 위에서부터 차례로 차오르며 숫자가 굴러 올라간다(실제 시간 — 결과 창은 timeScale 0). 아무 클릭 · 키나 누르면 바로 끝 상태로
    public class BattleSummary : MonoBehaviour
    {
        // 판 안쪽 여백 · 머리(제목 72 + 8 + 결과 한 줄 32) · 「준 피해」 제목 줄 · 버튼 줄(UI_CmdButton 80) — 스타일 가이드 §4 8px 단위
        private const float Pad = 32f;
        private const float HeadHeight = 112f;
        private const float DividerGap = 24f;
        private const float DividerHeight = 24f;
        private const float RowsGap = 16f;
        private const float FootHeight = 80f;
        // 줄 — 기물 줄은 아이콘 64(IconLarge) · 타워 · 기타 줄은 아이콘 32, 줄 사이 8 · 두 묶음 사이 16
        private const float BigRow = 64f;
        private const float SmallRow = 32f;
        private const float RowGap = 8f;
        private const float GroupGap = 16f;
        private const float IconColumn = 64f;
        private const float Gap = 16f;
        private const float NumberColumn = 160f;
        private const float MinNameColumn = 96f;
        private const float MinWidth = 1040f;
        private const int NameSize = 24;   // 도트 폰트 12의 정수배
        private const int BigNumberSize = 36;
        private const int SmallNumberSize = 24;
        // 판 선 · 매듭(UI_WorldFrame과 같은 회색 · 2px)
        private static readonly Color LineColor = new Color32(166, 166, 166, 255);
        private const float LineWidth = 2f;
        // 움직임 — 판이 16px 위에서 계단 3번(0.12초) · 줄마다 0.08초 늦게 · 막대 하나 0.5초 · 막대는 2px 계단
        private const float DropHeight = 16f;
        private const float DropTime = 0.12f;
        private const float RowDelay = 0.08f;
        private const float FillTime = 0.5f;
        private const float BarStep = 2f;
        // 1위 막대가 다 찼을 때 흰색으로 번쩍이는 시간 · 1위 아닌 막대를 영주 색에서 어둡게 하는 비율
        private const float FlashTime = 0.1f;
        private const float DimRatio = 0.45f;
        // 「나 / 적」 탭(원본 88×40) — 「준 피해」 가로선 오른쪽 끝을 끊고 들어간다, 탭 사이 8
        private const float TabGap = 8f;

        [Tooltip("결과 창 — 이것이 켜지는 순간 요약을 그린다")]
        [SerializeField] private GameObject _resultPanel;
        [Tooltip("판(검은 속) — 크기를 이 코드가 정한다. 제목 · 영향 · 버튼은 이 판의 자식")]
        [SerializeField] private RectTransform _board;
        [Tooltip("줄이 놓이는 곳(판 왼쪽 위 기준)")]
        [SerializeField] private RectTransform _cells;
        [Tooltip("「준 피해」 판 제목(도트 24) — 줄의 이름 · 숫자 글도 이 글을 본뜬다")]
        [SerializeField] private Text _header;
        [SerializeField] private Sprite _knot;
        [Tooltip("막대(원본 600px — Filled로 채운다)")]
        [SerializeField] private Sprite _bar;
        [SerializeField] private Sprite _towerIcon;
        [Tooltip("「기타」 줄 아이콘 — 열쇠 색(#81ABFF) 자리가 내 영주 색으로 칠해진다")]
        [SerializeField] private Sprite _otherIcon;
        [Tooltip("「나 / 적」 탭 그림 — 평소 · 올림 · 고름(88×40 원본)")]
        [SerializeField] private Sprite _tab;
        [SerializeField] private Sprite _tabHover;
        [SerializeField] private Sprite _tabOn;

        // 한 진영의 줄 거리 — 기물 줄(많은 순) · 타워 · 기타 줄
        private class Sheet
        {
            public readonly List<(string Name, Sprite Icon, float Value)> Buildings = new List<(string, Sprite, float)>();
            public readonly List<(string Name, Sprite Icon, float Value)> Small = new List<(string, Sprite, float)>();
            public float Max;
            public float Height => Buildings.Count + Small.Count == 0 ? BigRow
                : Buildings.Count * (BigRow + RowGap) + (Small.Count > 0 && Buildings.Count > 0 ? GroupGap - RowGap : 0f) + Small.Count * (SmallRow + RowGap) - RowGap;
        }

        private class Row
        {
            public Image Bar;
            public Text Number;
            public float Value;
            public float Share;
            public bool Top;
            public bool Big;
        }

        private readonly List<Row> _rows = new List<Row>();
        // [Team] = 그 진영의 줄 거리 · 탭
        private readonly Sheet[] _sheets = new Sheet[2];
        private readonly TabButton[] _tabs = new TabButton[2];
        private Team _team;
        private float _width;
        private RectTransform _frame;
        private RectTransform _divider;
        private Color _lord;
        private float _openedAt;
        private bool _shown;
        private bool _animating;

        // LateUpdate — 결과 창은 BattleManager.Finish(Update · 물리)가 켠다. 같은 프레임 안에 판을 다 그려 옛 모습이 한 프레임도 안 보이게
        private void LateUpdate()
        {
            bool open = _resultPanel.activeInHierarchy;
            if (open && !_shown)
                Show();
            _shown = open;
            if (open && _animating)
                Animate();
        }

        // 결과 창이 열렸다 — 두 진영의 줄 거리를 모아 판 크기를 긴 쪽에 맞춰 고정하고(탭을 바꿔도 판이 출렁이지 않게, 사용자 10/22) 「나」부터
        private void Show()
        {
            _sheets[(int)Team.Ally] = Collect(Team.Ally);
            _sheets[(int)Team.Enemy] = Collect(Team.Enemy);

            // 이름 칸 — 두 쪽에서 가장 긴 이름에 맞춘다(8의 배수). 판 너비 = 여백 + 아이콘 + 이름 + 막대 600 + 숫자 + 여백
            float nameColumn = MinNameColumn;
            float rowsHeight = 0f;
            foreach (Sheet sheet in _sheets)
            {
                foreach (var r in sheet.Buildings) nameColumn = Mathf.Max(nameColumn, NameWidth(r.Name));
                rowsHeight = Mathf.Max(rowsHeight, sheet.Height);
            }
            nameColumn = Mathf.Ceil(nameColumn / 8f) * 8f;
            float barWidth = _bar.rect.width;
            _width = Mathf.Max(MinWidth, Pad + IconColumn + Gap + nameColumn + Gap + barWidth + Gap + NumberColumn + Pad);

            float rowsTop = Pad + HeadHeight + DividerGap + DividerHeight + RowsGap;
            float height = rowsTop + rowsHeight + DividerGap + FootHeight + Pad;
            _board.sizeDelta = new Vector2(_width, height);
            _cells.anchoredPosition = new Vector2(0f, -rowsTop);
            BuildFrame(Pad + HeadHeight + DividerGap + DividerHeight * 0.5f);

            _openedAt = Time.unscaledTime;
            Fill(Team.Ally);
        }

        // 탭 — 그 진영의 줄로 바꿔 그리고 막대 · 숫자 움직임을 처음부터(판은 그대로)
        private void Fill(Team team)
        {
            for (int i = _cells.childCount - 1; i >= 0; i--)
                Destroy(_cells.GetChild(i).gameObject);
            _rows.Clear();
            _team = team;
            _lord = RunState.LordColor(team);
            _tabs[(int)Team.Ally].Selected = team == Team.Ally;
            _tabs[(int)Team.Enemy].Selected = team == Team.Enemy;

            Sheet sheet = _sheets[(int)team];
            float barX = _width - Pad - NumberColumn - Gap - _bar.rect.width;
            float nameX = Pad + IconColumn + Gap;
            float y = 0f;
            bool topTaken = false;
            foreach (var r in sheet.Buildings)
            {
                AddRow(r, true, y, nameX, barX, sheet.Max, ref topTaken);
                y -= BigRow + RowGap;
            }
            if (sheet.Small.Count > 0 && sheet.Buildings.Count > 0)
                y -= GroupGap - RowGap;
            foreach (var r in sheet.Small)
            {
                AddRow(r, false, y, nameX, barX, sheet.Max, ref topTaken);
                y -= SmallRow + RowGap;
            }
            if (_rows.Count == 0)
            {
                Text empty = Line(_cells, "피해를 주지 못했습니다.", NameSize, TextAnchor.MiddleLeft);
                Place(empty.rectTransform, Pad, 0f, _width - 2f * Pad, BigRow);
            }

            _animating = true;
            Animate();
        }

        private void Toggle(Team team)
        {
            if (team == _team)
                return;
            _openedAt = Time.unscaledTime - DropTime;
            Fill(team);
        }

        // 줄 모으기 — 유닛을 내는 기물(0이어도 보여 준다: 회복만 하는 기물) · 피해를 낸 기물, 많은 순. 그다음 타워 · 기타(피해가 있을 때만).
        // 적 기물 이름은 빨강(§5-2-9), 「기타」 아이콘의 열쇠 색 자리는 그 진영 영주 색
        private Sheet Collect(Team team)
        {
            var sheet = new Sheet();
            IReadOnlyDictionary<int, float> dealt = DamageMeter.Of(team);
            BoardRunner board = BoardRunner.For(team);
            // 같은 기물이 여러 층에 있으면 한 줄로 — 층 피해 합 · 이름 뒤 「×N」(등급이 달라도 같은 기물이면 합친다, 사용자 10/21)
            var grouped = new Dictionary<BuildingDefinition, (int Count, float Value)>();
            var order = new List<BuildingDefinition>();
            if (board != null)
                for (int i = 0; i < board.Slots.Length; i++)
                {
                    OwnedBuilding owned = board.Slots[i].Building;
                    if (OwnedBuilding.IsEmpty(owned))
                        continue;
                    float value = dealt.TryGetValue(i, out float v) ? v : 0f;
                    if (owned.Definition.Unit == null && value <= 0f)
                        continue;
                    if (!grouped.TryGetValue(owned.Definition, out var sum))
                        order.Add(owned.Definition);
                    grouped[owned.Definition] = (sum.Count + 1, sum.Value + value);
                }
            foreach (BuildingDefinition definition in order)
            {
                var sum = grouped[definition];
                sheet.Buildings.Add((sum.Count > 1 ? $"{definition.DisplayName} ×{sum.Count}" : definition.DisplayName, definition.IconLarge, sum.Value));
            }
            sheet.Buildings.Sort((a, b) => b.Value.CompareTo(a.Value));
            if (dealt.TryGetValue(DamageMeter.Tower, out float tower) && tower > 0f)
                sheet.Small.Add(("타워", _towerIcon, tower));
            if (dealt.TryGetValue(DamageMeter.Other, out float other) && other > 0f)
                sheet.Small.Add(("기타", LordPaint.Of(_otherIcon, team), other));
            foreach (var r in sheet.Buildings) sheet.Max = Mathf.Max(sheet.Max, r.Value);
            foreach (var r in sheet.Small) sheet.Max = Mathf.Max(sheet.Max, r.Value);
            return sheet;
        }

        private void AddRow((string Name, Sprite Icon, float Value) data, bool big, float y, float nameX, float barX, float max, ref bool topTaken)
        {
            float height = big ? BigRow : SmallRow;
            var root = (RectTransform)new GameObject(data.Name, typeof(RectTransform)).transform;
            root.SetParent(_cells, false);
            Place(root, 0f, y, 0f, height);

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.rectTransform.SetParent(root, false);
            icon.sprite = data.Icon;
            icon.SetNativeSize();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Vector2 iconSize = icon.rectTransform.sizeDelta;
            Place(icon.rectTransform, Pad + (IconColumn - iconSize.x) * 0.5f, -(height - iconSize.y) * 0.5f, iconSize.x, iconSize.y);

            Text name = Line(root, data.Name, NameSize, TextAnchor.MiddleLeft);
            // 적 기물 이름은 빨강(§5-2-9) — 타워 · 기타는 기물이 아니라 흰색 그대로
            if (_team == Team.Enemy && big && ColorUtility.TryParseHtmlString(BuildingTooltip.EnemyColor, out Color enemy))
                name.color = enemy;
            Place(name.rectTransform, nameX, 0f, barX - nameX - Gap, height);

            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            bar.rectTransform.SetParent(root, false);
            bar.sprite = _bar;
            bar.type = Image.Type.Filled;   // 게이지 채움 층 — §5-1 예외
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillOrigin = (int)Image.OriginHorizontal.Left;
            bar.fillAmount = 0f;
            bar.SetNativeSize();
            bar.raycastTarget = false;
            Vector2 barSize = bar.rectTransform.sizeDelta;
            Place(bar.rectTransform, barX, -(height - barSize.y) * 0.5f, barSize.x, barSize.y);

            int numberSize = big ? BigNumberSize : SmallNumberSize;
            Text number = Line(root, "", numberSize, TextAnchor.MiddleRight);
            Place(number.rectTransform, barX + barSize.x + Gap, 0f, NumberColumn, height);

            bool top = !topTaken && max > 0f && data.Value >= max;
            topTaken |= top;
            _rows.Add(new Row { Bar = bar, Number = number, Value = data.Value, Share = max > 0f ? data.Value / max : 0f, Top = top, Big = big });
        }

        // 지금 시각의 움직임 — 판 내려앉기 · 줄마다 막대 · 숫자. 클릭 · 키면 끝 상태
        private void Animate()
        {
            bool skip = Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
                        || Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
            float t = skip ? float.MaxValue : Time.unscaledTime - _openedAt;

            int step = Mathf.Clamp(Mathf.FloorToInt(t / DropTime * 3f), 0, 3);   // 계단 3번
            _board.anchoredPosition = new Vector2(0f, Mathf.Round(DropHeight * (3 - step) / 3f));

            bool done = step == 3;
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                float k = Mathf.Clamp01((t - DropTime - i * RowDelay) / FillTime);
                float eased = 1f - (1f - k) * (1f - k) * (1f - k);   // 빨리 차고 천천히 멈춘다
                float width = row.Bar.rectTransform.sizeDelta.x;
                row.Bar.fillAmount = Mathf.Floor(width * row.Share * eased / BarStep) * BarStep / width;
                row.Number.text = BuildingTooltip.Highlight(Mathf.RoundToInt(row.Value * eased).ToString("N0"));
                Color color = row.Top ? _lord : Color.Lerp(_lord, Color.black, DimRatio);
                float flash = t - DropTime - i * RowDelay - FillTime;
                if (row.Top && flash >= 0f && flash < FlashTime && !skip)
                    color = Color.white;
                row.Bar.color = color;
                done &= k >= 1f && (!row.Top || flash >= FlashTime || skip);
            }
            _animating = !done;
        }

        // 판 선 네 변 + 네 모서리 매듭 + 「준 피해」 가로선(양 끝 매듭) — 선은 그림 없는 단색 칸(늘리는 그림이 아니다), 매듭은 원본 크기.
        // 판 크기를 따라가게 판의 모서리 · 변에 붙인다 — 한 번 만들고 가로선 높이만 옮긴다
        private void BuildFrame(float dividerY)
        {
            if (_frame == null)
            {
                _frame = (RectTransform)new GameObject("Frame", typeof(RectTransform)).transform;
                _frame.SetParent(_board, false);
                _frame.anchorMin = Vector2.zero;
                _frame.anchorMax = Vector2.one;
                _frame.offsetMin = _frame.offsetMax = Vector2.zero;
                Edge(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -LineWidth * 0.5f), new Vector2(0f, LineWidth));
                Edge(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, LineWidth * 0.5f), new Vector2(0f, LineWidth));
                Edge(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(LineWidth * 0.5f, 0f), new Vector2(LineWidth, 0f));
                Edge(new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-LineWidth * 0.5f, 0f), new Vector2(LineWidth, 0f));
                foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
                    Knot(_frame, corner, new Vector2(corner.x > 0f ? -1f : 1f, corner.y > 0f ? -1f : 1f) * (LineWidth * 0.5f));

                _divider = (RectTransform)new GameObject("Divider", typeof(RectTransform)).transform;
                _divider.SetParent(_frame, false);
                _divider.anchorMin = new Vector2(0f, 1f);
                _divider.anchorMax = new Vector2(1f, 1f);
                _divider.sizeDelta = new Vector2(0f, LineWidth);
                var line = new GameObject("Line", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                line.rectTransform.SetParent(_divider, false);
                line.rectTransform.anchorMin = Vector2.zero;
                line.rectTransform.anchorMax = Vector2.one;
                line.rectTransform.offsetMin = line.rectTransform.offsetMax = Vector2.zero;
                line.color = LineColor;
                line.raycastTarget = false;
                Knot(_divider, new Vector2(0f, 0.5f), new Vector2(LineWidth * 0.5f, 0f));
                Knot(_divider, new Vector2(1f, 0.5f), new Vector2(-LineWidth * 0.5f, 0f));

                // 판 제목은 가로선을 끊고 들어간다 — 글 뒤에 검은 칸(글 폭 + 양옆 8)
                var cut = new GameObject("HeaderCut", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                cut.rectTransform.SetParent(_divider, false);
                cut.color = Color.black;
                cut.raycastTarget = false;
                cut.rectTransform.anchorMin = cut.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                cut.rectTransform.pivot = new Vector2(0f, 0.5f);
                cut.rectTransform.anchoredPosition = new Vector2(Pad, 0f);
                cut.rectTransform.sizeDelta = new Vector2(_header.preferredWidth + 16f, DividerHeight);
                _header.rectTransform.SetParent(_divider, false);
                _header.rectTransform.anchorMin = _header.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                _header.rectTransform.pivot = new Vector2(0f, 0.5f);
                _header.rectTransform.anchoredPosition = new Vector2(Pad + 8f, 0f);
                _header.verticalOverflow = VerticalWrapMode.Overflow;   // 도트 글은 줄 높이가 글 크기보다 커서 24px 칸에선 잘린다
                _header.rectTransform.SetAsLastSibling();

                // 「나 / 적」 탭 — 가로선 오른쪽 끝을 끊고 들어간다(뒤에 검은 칸). 「나」가 왼쪽
                float tabWidth = _tab.rect.width;
                var tabCut = new GameObject("TabCut", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                tabCut.rectTransform.SetParent(_divider, false);
                tabCut.color = Color.black;
                tabCut.raycastTarget = false;
                tabCut.rectTransform.anchorMin = tabCut.rectTransform.anchorMax = tabCut.rectTransform.pivot = new Vector2(1f, 0.5f);
                tabCut.rectTransform.anchoredPosition = new Vector2(-Pad + 8f, 0f);
                tabCut.rectTransform.sizeDelta = new Vector2(tabWidth * 2f + TabGap + 16f, DividerHeight);
                MakeTab(Team.Enemy, "적", -Pad);
                MakeTab(Team.Ally, "나", -Pad - tabWidth - TabGap);
                _frame.SetAsFirstSibling();
            }
            _divider.anchoredPosition = new Vector2(0f, -dividerY);
        }

        // 탭 하나 — 원본 크기 그림 + 도트 24 글, 오른쪽 끝 기준 x에
        private void MakeTab(Team team, string label, float x)
        {
            var back = new GameObject("Tab_" + team, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            back.rectTransform.SetParent(_divider, false);
            back.sprite = _tab;
            back.SetNativeSize();
            back.preserveAspect = true;
            back.rectTransform.anchorMin = back.rectTransform.anchorMax = back.rectTransform.pivot = new Vector2(1f, 0.5f);
            back.rectTransform.anchoredPosition = new Vector2(x, 0f);
            Text text = Line(back.rectTransform, label, NameSize, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            TabButton tab = back.gameObject.AddComponent<TabButton>();
            tab.Init(back, text, _tab, _tabHover, _tabOn);
            tab.Clicked += () => Toggle(team);
            _tabs[(int)team] = tab;
        }

        private void Edge(Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var line = new GameObject("Edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            line.rectTransform.SetParent(_frame, false);
            line.rectTransform.anchorMin = anchorMin;
            line.rectTransform.anchorMax = anchorMax;
            line.rectTransform.anchoredPosition = position;
            line.rectTransform.sizeDelta = size;
            line.color = LineColor;
            line.raycastTarget = false;
        }

        private void Knot(RectTransform parent, Vector2 anchor, Vector2 offset)
        {
            var knot = new GameObject("Knot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            knot.rectTransform.SetParent(parent, false);
            knot.rectTransform.anchorMin = knot.rectTransform.anchorMax = anchor;
            knot.rectTransform.anchoredPosition = offset;
            knot.sprite = _knot;
            knot.SetNativeSize();
            knot.preserveAspect = true;
            knot.raycastTarget = false;
        }

        private float NameWidth(string name)
        {
            _header.fontSize = NameSize;
            var settings = _header.GetGenerationSettings(Vector2.zero);
            return _header.cachedTextGeneratorForLayout.GetPreferredWidth(name, settings) / _header.pixelsPerUnit;
        }

        // 판 제목(도트 글)을 본뜬 한 줄
        private Text Line(RectTransform parent, string content, int size, TextAnchor alignment)
        {
            Text text = Instantiate(_header, parent);
            text.name = "Text";
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        // parent의 왼쪽 위 기준 (x, y)에 너비 · 높이 — y는 아래로 음수
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
