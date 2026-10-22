using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GnorpWar
{
    // 전투 중 전장 유닛에 마우스를 올리면 그 유닛의 지금 값 카드(사용자 10/23) — 그림 · 이름 · 병종 / 체력(숫자 + 막대) / 공격력(기본 + 달라진 몫) / 상태 줄.
    // 유닛 판(UI/BuildingTooltip의 UnitPanel)과 같은 문법 — 같은 판 · 테두리 · 폰트 · 색 아이콘 · 「기본+증가분」 표기.
    // 고르기: PickInterval마다 마우스 둘레 원 하나(Physics2D.OverlapCircle — 유닛 층만)에서 가장 가까운 살아 있는 유닛. 사이 프레임엔 고른 유닛을 그대로 읽는다.
    // 설명 창(탑 층 TowerHover · 칸 · 맵 이름)이 떠 있거나 마우스가 UI 위면 안 뜬다 — 설명 창이 먼저다. 뷰 — 유닛을 읽기만 한다. 고른 유닛의 꺾쇠는 Fx/HoverMark
    public class UnitHover : MonoBehaviour
    {
        private const float PickInterval = 0.1f;
        // 고르는 반경 — 화면 px(줌과 상관없이 같은 손맛), 최소 월드 칸
        private const float PickPixels = 28f;
        private const float PickMinWorld = 0.4f;
        // 판 문법 — BuildingTooltip과 같은 여백(테두리 12px + 16px)
        private const float Width = 420f;
        private const float Pad = 24f;
        private const float Side = 28f;
        private const float InnerGap = 8f;
        private const float IconBox = 64f;
        private const float NameGap = 16f;
        private const float StatIconSize = 32f;
        private const float StatIconGap = 8f;
        private const float BarGap = 6f;
        // 마우스에서 카드까지 · 화면 끝 여백(px)
        private const float Offset = 24f;
        private const float Margin = 8f;
        private const int NameSize = 36;
        private const int CategorySize = 24;

        [Tooltip("판(검은 Image · UI/NationFrame) — 왼위 기준. 레이캐스트를 막지 않아야 한다")]
        [SerializeField] private RectTransform _card;
        [Tooltip("유닛 그림 칸(왼위 기준) — BuildingTooltip.DrawUnitIcon이 부품 그림을 짜 넣는다")]
        [SerializeField] private RectTransform _icon;
        [Tooltip("이름(36) · 병종(24) — 도트 폰트")]
        [SerializeField] private Text _name;
        [SerializeField] private Image _hpIcon;
        [SerializeField] private Text _hpText;
        [Tooltip("체력 막대 틀(원본 크기) — 채움은 그 자식")]
        [SerializeField] private RectTransform _bar;
        [Tooltip("Image.type = Filled 여야 fillAmount가 먹는다 — 색은 그 진영 영주 색(본진 체력 막대와 같은 문법)")]
        [SerializeField] private Image _barFill;
        [SerializeField] private Image _attackIcon;
        [SerializeField] private Text _attackText;
        [Tooltip("상태 줄(회색) — 무적 · 불타는 중 · 느려짐, 한 줄에 하나")]
        [SerializeField] private Text _state;
        [SerializeField] private Sprite _heartSprite;
        [SerializeField] private Sprite _swordSprite;

        private readonly Collider2D[] _hits = new Collider2D[32];
        private readonly System.Collections.Generic.List<RaycastResult> _uiHits = new System.Collections.Generic.List<RaycastResult>();
        private PointerEventData _pointerData;
        private ContactFilter2D _filter;
        private float _nextPick;
        private Unit _unit;
        private int _life;
        // 마지막으로 쓴 값 — 바뀔 때만 글을 다시 짠다
        private UnitDefinition _shownDefinition;
        private Team _shownTeam;
        private Color _shownLord;
        private int _shownHp = int.MinValue, _shownMax, _shownAttack, _shownState = -1;

        private void Awake()
        {
            int mask = 0;
            foreach (string layer in new[] { "AllyUnit", "EnemyUnit", "AllyBuilder", "EnemyBuilder" })
                mask |= 1 << LayerMask.NameToLayer(layer);
            _filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = mask };
            _card.gameObject.SetActive(false);
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            Camera cam = Camera.main;
            Vector2 mouse = pointer != null ? pointer.position.ReadValue() : Vector2.zero;
            if (!BattleManager.Fighting || pointer == null || cam == null || BuildingTooltip.IsOpen || OverUi(mouse))
            {
                Pick(null);
                return;
            }
            if (Time.unscaledTime >= _nextPick)
            {
                _nextPick = Time.unscaledTime + PickInterval;
                Pick(Nearest(cam, mouse));
            }
            if (_unit != null && (!_unit.isActiveAndEnabled || !_unit.IsAlive || _unit.Life != _life))
                Pick(null);
            if (_unit == null)
                return;
            Fill(_unit);
            Place(mouse);
        }

        private Unit Nearest(Camera cam, Vector2 mouse)
        {
            Vector2 world = cam.ScreenToWorldPoint(mouse);
            float radius = Mathf.Max(PickMinWorld, PickPixels * cam.orthographicSize * 2f / Screen.height);
            int count = Physics2D.OverlapCircle(world, radius, _filter, _hits);
            Unit best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Rigidbody2D body = _hits[i].attachedRigidbody;
                if (body == null || !body.TryGetComponent(out Unit unit) || !unit.IsAlive)
                    continue;
                float distance = (_hits[i].ClosestPoint(world) - world).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = unit;
                }
            }
            return best;
        }

        private void Pick(Unit unit)
        {
            if (unit == _unit)
                return;
            _unit = unit;
            _life = unit != null ? unit.Life : 0;
            _shownHp = int.MinValue;
            HoverMark.Show(unit);
            _card.gameObject.SetActive(unit != null);
        }

        private bool OverUi(Vector2 position)
        {
            if (EventSystem.current == null)
                return false;
            _pointerData ??= new PointerEventData(EventSystem.current);
            _pointerData.position = position;
            _uiHits.Clear();
            EventSystem.current.RaycastAll(_pointerData, _uiHits);
            return _uiHits.Count > 0;
        }

        // 값이 바뀐 것만 다시 쓴다 — 막대는 매 프레임
        private void Fill(Unit unit)
        {
            UnitDefinition definition = unit.Definition;
            bool enemy = unit.Team == Team.Enemy;
            Color lord = RunState.LordColor(unit.Team);
            _barFill.fillAmount = unit.MaxHp > 0f ? Mathf.Clamp01(unit.Hp / unit.MaxHp) : 0f;

            bool redraw = definition != _shownDefinition || unit.Team != _shownTeam || lord != _shownLord;
            if (redraw)
            {
                _shownDefinition = definition;
                _shownTeam = unit.Team;
                _shownLord = lord;
                _barFill.color = lord;
                BuildingTooltip.DrawUnitIcon(_icon, definition, enemy);
                string name = enemy ? $"<color={BuildingTooltip.EnemyColor}>{definition.DisplayName}</color>" : definition.DisplayName;
                _name.text = $"<size={NameSize}>{name}</size>\n<size={CategorySize}>{definition.Category.DisplayName}</size>";
            }

            // 살아 있으면 1 이상으로 보이게 올림 — 본진 체력 막대(UI/BaseHealthBar)와 같다
            int hp = Mathf.Max(1, Mathf.CeilToInt(unit.Hp));
            int max = BuildingTooltip.Whole(unit.MaxHp);
            float scale = definition.AttackType == AttackType.Flame ? 1f / definition.AttackInterval : 1f;
            int attack = BuildingTooltip.Whole(unit.AttackDamage * scale);
            int state = (unit.IsInvulnerable ? 1 : 0) | (unit.IsBurning ? 2 : 0) | (unit.IsSlowed ? 4 : 0);
            if (!redraw && hp == _shownHp && max == _shownMax && attack == _shownAttack && state == _shownState)
                return;
            _shownHp = hp;
            _shownMax = max;
            _shownAttack = attack;
            _shownState = state;

            _hpText.text = $"체력 {Number(hp)} 최대 {Stat(definition, UnitStat.MaxHp, max, 1f)}";
            _attackIcon.gameObject.SetActive(definition.AttackType != AttackType.Build);
            if (definition.AttackType == AttackType.Build)
                _attackText.text = "공격 안 함";
            else if (definition.AttackType == AttackType.Heal)
            {
                _attackIcon.sprite = _heartSprite;
                _attackIcon.color = BuildingTooltip.HealIconColor;
                _attackText.text = $"회복 {Stat(definition, UnitStat.Attack, attack, 1f)}";
            }
            else
            {
                _attackIcon.sprite = _swordSprite;
                _attackIcon.color = BuildingTooltip.AttackIconColor;
                _attackText.text = $"{(definition.AttackType == AttackType.Flame ? "공격력 초당" : "공격력")} {Stat(definition, UnitStat.Attack, attack, scale)}";
            }
            string lines = "";
            if ((state & 1) != 0)
                lines += "무적";
            if ((state & 2) != 0)
                lines += (lines.Length > 0 ? "\n" : "") + "불타는 중";
            if ((state & 4) != 0)
                lines += (lines.Length > 0 ? "\n" : "") + "느려짐";
            _state.text = lines.Length > 0 ? $"<color={BuildingTooltip.NoteColor}>{lines}</color>" : "";
            Layout();
        }

        private static string Number(int value) => $"<color={BuildingTooltip.NumberColor}>{value}</color>";

        // 「기본+증가분」 — 기본(노랑) 뒤에 달라진 몫을 정수로(늘면 초록 · 줄면 빨강). 둘을 더하면 지금 값(now)의 반올림이다 — 유닛 판(BuildingTooltip.Stat)과 같은 표기
        private static string Stat(UnitDefinition definition, UnitStat stat, int now, float scale)
        {
            int shown = BuildingTooltip.Whole(Unit.BaseStat(definition, stat) * scale), bonus = now - shown;
            string text = Number(shown);
            if (bonus != 0)
                text += $"<color={(bonus > 0 ? BuildingTooltip.BonusColor : BuildingTooltip.EnemyColor)}>{bonus:+0;-0}</color>";
            return text;
        }

        // 위에서부터 그림 · 이름 / 체력 줄 / 막대 / 공격력 줄 / 상태 줄 — 판 높이를 글에 맞춘다
        private void Layout()
        {
            float nameHeight = _name.preferredHeight;
            float header = Mathf.Max(_icon.sizeDelta.y, nameHeight);
            _icon.anchoredPosition = new Vector2(_icon.anchoredPosition.x, -(Pad + (header - _icon.sizeDelta.y) / 2f));
            Put(_name.rectTransform, Side + IconBox + NameGap, Pad + (header - nameHeight) / 2f, Width - Side * 2f - IconBox - NameGap, nameHeight);
            float y = Pad + header + InnerGap;
            y = Row(_hpIcon, _hpText, y) + BarGap;
            _bar.anchoredPosition = new Vector2((Width - _bar.sizeDelta.x) / 2f, -y);
            y += _bar.sizeDelta.y + InnerGap;
            y = Row(_attackIcon, _attackText, y);
            bool hasState = _state.text.Length > 0;
            _state.enabled = hasState;
            if (hasState)
            {
                float h = _state.preferredHeight;
                y += InnerGap;
                Put(_state.rectTransform, Side, y, Width - Side * 2f, h);
                y += h;
            }
            _card.sizeDelta = new Vector2(Width, y + Pad);
        }

        // 아이콘 칸 + 글 한 줄 — 줄 아래 끝의 y를 돌려준다
        private static float Row(Image icon, Text text, float y)
        {
            float iconHeight = icon.gameObject.activeSelf ? icon.rectTransform.sizeDelta.y : 0f;
            float textHeight = text.preferredHeight;
            float row = Mathf.Max(iconHeight, textHeight);
            icon.rectTransform.anchoredPosition = new Vector2(Side, -(y + (row - iconHeight) / 2f));
            Put(text.rectTransform, Side + StatIconSize + StatIconGap, y + (row - textHeight) / 2f, text.preferredWidth, textHeight);
            return y + row;
        }

        private static void Put(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        // 마우스 오른쪽 아래 — 오른쪽이 모자라면 왼쪽, 아래가 모자라면 위로 민다(Overlay 캔버스라 월드 좌표 = 화면 픽셀)
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
