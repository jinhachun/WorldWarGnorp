using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 건물에 마우스를 올리면 뜨는 설명 창 (The Bazaar식) — 칸 오른쪽에, 오른쪽에 자리가 없으면 왼쪽에 뜬다.
    // 칸·카드는 마우스가 올라가 있는 동안 매 프레임 Show로 내용을 다시 넣는다(레벨·판매가가 바뀌어도 맞게).
    // 이 오브젝트는 켜 둔 채 판(_panel)만 켜고 끈다 — 꺼 두면 Awake가 안 돈다
    public class BuildingTooltip : MonoBehaviour
    {
        private const float Gap = 8f;

        [Tooltip("레이캐스트를 막지 않아야 한다 — 막으면 칸에서 마우스가 빠진 걸로 잡혀 깜빡인다")]
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Text _title;
        [SerializeField] private Text _body;
        [Tooltip("생산 건물일 때만 켜지는 유닛 판 — Panel 아래에 붙는다(Panel의 자식, 위쪽 가운데 기준)")]
        [SerializeField] private RectTransform _unitPanel;
        [SerializeField] private Text _unitBody;

        private static BuildingTooltip _instance;
        private readonly Vector3[] _corners = new Vector3[4];
        private Component _owner;

        private const string NumberColor = "#FFD95A";
        private const string NoteColor = "#9A9AA8";
        private const string EnemyColor = "#FF7A6E";
        // 유닛 판 글자 크기 — 나머지 줄은 _unitBody의 글자 크기
        private const int UnitNameSize = 26;
        private const int UnitEffectSize = 16;

        // 효과 줄 — 늘 켜진 것 먼저, 그다음 "N초마다 ~". cooldown = 지금 간격(전투 중엔 줄어든 값). 글 규칙은 CLAUDE.md §5-2
        public static string Describe(BuildingDefinition building, int level, float cooldown)
        {
            var lines = new List<string>();
            foreach (UnitEffect effect in building.Effects)
                if (!string.IsNullOrEmpty(effect.Description))
                    lines.Add(effect.Description);
            string every = $"{cooldown:0.#}초마다 ";
            if (building.Unit != null)
                lines.Add(every + $"{building.Unit.DisplayName} {building.UnitCount * level + BattleManager.ExtraUnits}마리를 내보낸다.");
            foreach (BuildingAction action in building.Actions)
                lines.Add(every + action.Describe(level));
            if (!string.IsNullOrEmpty(building.Description))
                lines.Add(every + building.Description);
            return ColorNumbers(string.Join("\n", lines));
        }

        // 효과가 아닌 상태 줄(경험치·판매가) — 회색, 맨 아래
        public static string Note(string text) => $"\n<color={NoteColor}>{text}</color>";

        public static string Title(BuildingDefinition building, bool enemy, int level)
        {
            string title = $"{building.DisplayName}  {building.Rarity.DisplayName()}";
            if (level > 0)
                title += $"  레벨 {level}";
            return enemy ? $"<color={EnemyColor}>{title} (적)</color>" : title;
        }

        // 생산 건물의 유닛 판 — 이름 · 병종 · (그 아래 작은 글씨로) board의 건물이 이 유닛에 켜 준 효과 · 능력치.
        // 크기·속도는 숫자 대신 말로. 생산하지 않는 건물이면 null
        public static string UnitInfo(BuildingDefinition building, OwnedBuilding[] board)
        {
            UnitDefinition unit = building.Unit;
            if (unit == null)
                return null;
            var lines = new List<string> { $"<size={UnitNameSize}>{unit.DisplayName}</size>", unit.Category.DisplayName };
            foreach (OwnedBuilding owned in board)
                if (!OwnedBuilding.IsEmpty(owned))
                    foreach (UnitEffect effect in owned.Definition.Effects)
                        if (effect.AppliesTo(unit) && !string.IsNullOrEmpty(effect.Description))
                            lines.Add($"<size={UnitEffectSize}>{ColorNumbers(effect.Description)}</size>");

            lines.Add(ColorNumbers($"체력 {unit.MaxHp:0}"));
            if (unit.AttackType == AttackType.Heal)
                lines.Add(ColorNumbers($"회복 {unit.HealAmount:0}"));
            else if (unit.AttackType == AttackType.Flame)
                lines.Add(ColorNumbers($"공격력 초당 {unit.AttackDamage / unit.AttackInterval:0}"));
            else
                lines.Add(ColorNumbers($"공격력 {unit.AttackDamage:0}"));
            lines.Add((unit.AttackType == AttackType.Heal ? "회복 거리 " : "공격 거리 ") + RangeWord(unit.AttackRange));
            // 적 전용 유닛(투석기)은 아군 프리팹이 없다
            Unit prefab = unit.PrefabFor(Team.Ally) != null ? unit.PrefabFor(Team.Ally) : unit.PrefabFor(Team.Enemy);
            lines.Add("크기 " + SizeWord(prefab.GetComponent<BoxCollider2D>().size.y));
            lines.Add("속도 " + SpeedWord(unit.MoveSpeed));

            if (unit.ChargeDamageMultiplier > 1f)
                lines.Add(ColorNumbers($"달려와 처음 때릴 때 피해가 {unit.ChargeDamageMultiplier:0.#}배다."));
            if (unit.ProjectileSplashRadius > 0f)
                lines.Add("맞은 자리 주변의 적도 피해를 입는다.");
            if (unit.AttackType == AttackType.Flame)
                lines.Add("앞에 있는 적을 한꺼번에 불태운다.");
            if (unit.AttackType == AttackType.Sweep)
                lines.Add("앞에 있는 적을 한꺼번에 친다.");
            if (unit.SlamInterval > 0f)
                lines.Add(ColorNumbers($"{unit.SlamInterval:0.#}초마다 뛰어올라 주변 적을 띄운다."));
            return string.Join("\n", lines);
        }

        private static string ColorNumbers(string text) => Regex.Replace(text, @"\d+(\.\d+)?%?", $"<color={NumberColor}>$0</color>");

        // 키(충돌 박스 높이) — 보병 1 · 기사 1.5 · 공룡 3 · 코끼리 4
        private static string SizeWord(float height) => height <= 1f ? "소형" : height <= 2f ? "중형" : "대형";

        // 근접 0.8 · 코끼리 3 · 공룡 5 · 사제 6 · 궁수 7 · 투석기 10
        private static string RangeWord(float range) => range <= 1f ? "가까움" : range <= 5f ? "보통" : "멂";

        private static string SpeedWord(float speed)
            => speed < 1.5f ? "아주 느림" : speed < 2.5f ? "느림" : speed < 4.5f ? "보통" : speed < 6f ? "빠름" : "아주 빠름";

        // 다음 레벨까지 — 같은 건물을 몇 개 더 사야 하나
        public static string LevelNote(OwnedBuilding building)
        {
            (int have, int need) = building.Progress();
            return Note($"{need - have}개 더 사면 레벨 {building.Level + 1}");
        }

        private void Awake()
        {
            _instance = this;
            _panel.gameObject.SetActive(false);
        }

        // unitInfo = UnitInfo(...) — null이면 유닛 판을 끈다
        public static void Show(Component owner, string title, string body, string unitInfo)
        {
            if (_instance != null)
                _instance.Place(owner, title, body, unitInfo);
        }

        public static void Hide(Component owner)
        {
            if (_instance != null && _instance._owner == owner)
            {
                _instance._owner = null;
                _instance._panel.gameObject.SetActive(false);
            }
        }

        private void Place(Component owner, string title, string body, string unitInfo)
        {
            _owner = owner;
            _title.text = title;
            _body.text = body;
            _unitBody.text = unitInfo ?? "";
            _unitPanel.gameObject.SetActive(unitInfo != null);
            _panel.gameObject.SetActive(true);

            // Overlay 캔버스라 월드 좌표 = 화면 픽셀. 유닛 판은 Panel 밑에 매달려 있어 높이에 더한다
            ((RectTransform)owner.transform).GetWorldCorners(_corners);   // 0 왼아래 · 1 왼위 · 2 오른위
            float scale = _panel.lossyScale.y;
            Vector2 size = _panel.rect.size * scale;
            float below = unitInfo == null ? 0f : (_unitPanel.rect.height - _unitPanel.anchoredPosition.y) * scale;
            float gap = Gap * scale;
            bool right = _corners[2].x + gap + size.x <= Screen.width;
            // 윗변을 칸 윗변에 맞추고, 화면 위아래를 넘으면 안으로 민다
            float top = Mathf.Clamp(_corners[1].y, size.y + below + gap, Screen.height - gap);
            _panel.pivot = new Vector2(right ? 0f : 1f, 1f);
            _panel.position = new Vector3(right ? _corners[2].x + gap : _corners[0].x - gap, top, 0f);
        }
    }
}
