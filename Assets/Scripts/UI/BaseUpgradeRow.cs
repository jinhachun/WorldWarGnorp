using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 전투 화면 — 본진 체력바 아래에 그 진영 영주의 이름 · 국가 한 줄(지도 왼쪽 패널과 같은 글, 사용자 10/14)과 그 아래 가진 합치기 보상 업그레이드 아이콘 줄(UI/UpgradeRow).
    // 내 것은 왼쪽부터, 적의 것은 오른쪽부터 서로 마주 보게. 업그레이드가 없어도 이름 줄은 나온다.
    // 그 진영의 타워 UI(탑이 화면 밖일 때 가장자리에 뜨는 칸 줄 — UI/BoardView)와 겹치면 시작 쪽 끝을 그 바깥으로 민다(내 것은 오른쪽으로, 적의 것은 왼쪽으로).
    // 체력바의 자식이라 상점 동안에는 체력바와 같이 숨는다. 전투 한 판 동안 가진 업그레이드가 바뀌지 않으므로 처음 한 번만 그린다
    public class BaseUpgradeRow : MonoBehaviour
    {
        // 한 칸의 폭(px) · 한 줄의 칸 수 — 한 줄이 가운데의 유닛 수 표시에 닿지 않게(타워 UI에 밀렸을 때도). 넘치면 아랫줄
        private const float CellWidth = 64f;
        private const int Columns = 6;
        // 이름과 국가 사이 · 이름 줄과 아이콘 줄 사이 · 밀 때 타워 UI와 띄우는 여백(px)
        private const float NationGap = 12f;
        private const float LineGap = 4f;
        private const float TowerGap = 12f;
        // 국가 글자 크기 — 지도 왼쪽 패널의 국가 줄과 같다(깔끔 폰트)
        private const int NationSize = 20;

        [SerializeField] private Team _team;
        [Tooltip("「×N」 · 영주 이름 글의 본 — 도트 폰트 글 하나(같은 폰트로 본뜬다)")]
        [SerializeField] private Text _labelTemplate;
        [Tooltip("국가 이름의 폰트 — 지도 왼쪽 패널의 국가 줄과 같은 깔끔 폰트")]
        [SerializeField] private Font _nationFont;
        [Tooltip("이 진영의 타워 UI — 겹치면 이 줄을 그 바깥으로 민다")]
        [SerializeField] private BoardView _tower;

        private RectTransform _content;
        private float _height;
        private readonly Vector3[] _corners = new Vector3[4];

        private bool FromRight => _team == Team.Enemy;

        // 이 줄(이름 줄 + 아이콘 줄)의 아래 끝(화면 y) — 전투 골드 칸(UI/BattleGoldView)이 그 아래에 붙는다
        public float ScreenBottom => transform.position.y - _height * transform.lossyScale.y;

        private void Start()
        {
            Lord lord = _team == Team.Ally ? RunState.World.State.Player : RunState.Opponent;
            if (lord == null)
                return;
            _content = NewRect("Content", (RectTransform)transform);

            // 이름 · 국가 — 패널과 같은 글(나는 「내 영지」, 연합은 「연합」, AI 영주는 닉네임)
            bool mine = lord == RunState.World.State.Player;
            Text name = Instantiate(_labelTemplate, _content);
            name.name = "LordName";
            name.enabled = true;
            name.text = mine ? "내 영지" : lord == RunState.World.State.Coalition ? "연합" : RunState.NameOf(lord);
            Text nation = new GameObject("Nation", typeof(RectTransform)).AddComponent<Text>();
            nation.transform.SetParent(_content, false);
            nation.font = _nationFont;
            nation.fontSize = NationSize;
            nation.color = Color.white;
            nation.text = (mine ? RunState.Nation : RunState.World.NationOf(lord)).DisplayName();
            float lineHeight = Mathf.Max(name.preferredHeight, nation.preferredHeight);
            float nameWidth = Fit(name, lineHeight), nationWidth = Fit(nation, lineHeight);
            // 읽는 순서는 양쪽 다 「이름 국가」 — 적은 줄 전체를 오른쪽 끝에 맞춘다
            Place(name.rectTransform, FromRight ? -(nationWidth + NationGap + nameWidth) : 0f);
            Place(nation.rectTransform, FromRight ? -nationWidth : nameWidth + NationGap);

            var row = new UpgradeRow(_content, _labelTemplate, CellWidth, Columns, FromRight);
            float rowHeight = row.Show(RunState.UpgradesOf(lord));
            row.Root.anchoredPosition = new Vector2(0f, -(lineHeight + LineGap));
            _height = lineHeight + (rowHeight > 0f ? LineGap + rowHeight : 0f);
        }

        private static RectTransform NewRect(string name, RectTransform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        // 글 하나를 한 줄 높이에 맞춰 세로 가운데로 — 폭을 돌려준다
        private static float Fit(Text text, float height)
        {
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            float width = text.preferredWidth;
            text.rectTransform.sizeDelta = new Vector2(width, height);
            return width;
        }

        private static void Place(RectTransform rect, float x)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        // 타워 UI가 보이고 이 줄의 높이 범위에 걸치면, 시작 쪽 끝을 타워 UI 바깥 + 여백으로 민다. 안 보이면 제자리
        private void LateUpdate()
        {
            if (_content == null)
                return;
            float shift = 0f;
            if (_tower != null && _tower.ScreenBounds(out Rect tower))
            {
                // Overlay 캔버스라 월드 좌표 = 화면 픽셀
                ((RectTransform)transform).GetWorldCorners(_corners);
                float scale = transform.lossyScale.x;
                float top = _corners[1].y, bottom = top - _height * scale;
                float start = _corners[1].x;   // 줄의 시작점(적의 줄은 여기서 왼쪽으로 늘어선다)
                if (tower.yMax > bottom && tower.yMin < top)
                    shift = FromRight
                        ? Mathf.Min(0f, tower.xMin - TowerGap * scale - start)
                        : Mathf.Max(0f, tower.xMax + TowerGap * scale - start);
                shift /= scale;
            }
            _content.anchoredPosition = new Vector2(shift, 0f);
        }
    }
}
