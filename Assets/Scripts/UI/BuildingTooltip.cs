using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 건물에 마우스를 올리면 뜨는 설명 창 (The Bazaar식) — 칸 오른쪽에, 오른쪽에 자리가 없으면 왼쪽에 뜬다.
    // 칸·카드는 마우스가 올라가 있는 동안 매 프레임 Show로 내용을 다시 넣는다(레벨·판매가가 바뀌어도 맞게).
    // 이 오브젝트는 켜 둔 채 판(_panel)만 켜고 끈다 — 꺼 두면 Awake가 안 돈다.
    // 두 판의 높이는 글에 맞춰 늘고 준다(§5-1의 툴팁 예외 — 테두리 1px 단색 판이라 세로로 늘려도 안 깨진다)
    public class BuildingTooltip : MonoBehaviour
    {
        private const float Gap = 8f;
        // 판 안쪽 위아래 여백 · 글 덩어리 사이 간격(px)
        private const float Pad = 16f;
        private const float InnerGap = 8f;
        // 판 왼쪽 여백 — 글 칸들(sizeDelta.x -40)과 같다
        private const float Side = 20f;
        // 유닛 그림 칸 — 이 크기 안에 들어가는 가장 큰 정수 배율로 픽셀 그대로 그린다
        private const float IconBox = 64f;

        [Tooltip("레이캐스트를 막지 않아야 한다 — 막으면 칸에서 마우스가 빠진 걸로 잡혀 깜빡인다")]
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Text _title;
        [SerializeField] private Text _body;
        [Tooltip("생산 건물일 때만 켜지는 유닛 판 — Panel 아래에 붙는다(Panel의 자식, 위쪽 가운데 기준)")]
        [SerializeField] private RectTransform _unitPanel;
        [Tooltip("유닛 그림 칸(왼위 기준) — 프리팹의 그림 부품을 Image로 다시 짜 넣는다")]
        [SerializeField] private RectTransform _unitIcon;
        [Tooltip("그림 오른쪽 — 이름 · 병종")]
        [SerializeField] private Text _unitName;
        [SerializeField] private Text _unitBody;

        private static BuildingTooltip _instance;
        private readonly Vector3[] _corners = new Vector3[4];
        private Component _owner;
        private UnitDefinition _iconUnit;

        private const string NumberColor = "#FFD95A";
        private const string NoteColor = "#9A9AA8";
        private const string EnemyColor = "#FF7A6E";
        // 글자 크기 — 중요한 것일수록 크게. 능력치 줄은 _unitBody의 글자 크기
        private const int SubtitleSize = 18;
        private const int UnitNameSize = 28;
        private const int UnitCategorySize = 18;
        private const int UnitTraitSize = 18;
        private const int UnitEffectSize = 16;

        // 효과 줄 — "N초마다 ~" 먼저(기획서: 쿨다운 효과는 첫 줄), 그다음 늘 켜진 것. cooldown = 지금 간격(전투 중엔 줄어든 값). 글 규칙은 CLAUDE.md §5-2
        public static string Describe(BuildingDefinition building, int level, float cooldown)
        {
            var lines = new List<string>();
            string every = $"{cooldown:0.#}초마다 ";
            if (building.Unit != null)
                lines.Add(every + $"{building.Unit.DisplayName} {building.UnitCount * level + BattleManager.ExtraUnits}기를 소환합니다.");
            foreach (BuildingAction action in building.Actions)
                lines.Add(every + action.Describe(level));
            if (!string.IsNullOrEmpty(building.Description))
                lines.Add(every + building.Description);
            foreach (BuildingPassive passive in building.Passives)
                lines.Add(passive.Describe(level));
            foreach (UnitEffect effect in building.Effects)
                if (!string.IsNullOrEmpty(effect.Description))
                    lines.Add(effect.Describe(level));
            return Highlight(string.Join("\n", lines));
        }

        // 칸 아래 대표 숫자 — 툴팁 첫 숫자와 같은 순서(생산 수 · 액션 · 패시브 · 효과)로 처음 나오는 것. 없으면 ""
        public static string Headline(BuildingDefinition building, int level)
        {
            if (building.Unit != null)
                return $"{building.UnitCount * level + BattleManager.ExtraUnits}기";
            foreach (BuildingAction action in building.Actions)
                if (action.Headline(level) is string a)
                    return a;
            foreach (BuildingPassive passive in building.Passives)
                if (passive.Headline(level) is string p)
                    return p;
            foreach (UnitEffect effect in building.Effects)
                if (effect.Headline(level) is string e)
                    return e;
            return "";
        }

        // 효과가 아닌 상태 줄(경험치·판매가) — 회색, 맨 아래
        public static string Note(string text) => $"\n<color={NoteColor}>{text}</color>";

        // 첫 줄 이름(크게) · 둘째 줄 등급 · 국가(작게). 세력은 쓰지 않는다(사용자 결정)
        public static string Title(BuildingDefinition building, bool enemy, BuildingRarity rarity)
        {
            string name = $"<b>{building.DisplayName}{(enemy ? " (적)" : "")}</b>";
            if (enemy)
                name = $"<color={EnemyColor}>{name}</color>";
            return $"{name}\n<size={SubtitleSize}>{rarity.DisplayName()} · {building.Nation.DisplayName()}</size>";
        }

        // 유닛 판 그림 오른쪽 글 — 이름(크게) · 병종(작게)
        private static string UnitName(UnitDefinition unit)
            => $"<b><size={UnitNameSize}>{unit.DisplayName}</size></b>\n<size={UnitCategorySize}>{unit.Category.DisplayName}</size>";

        // 생산 건물의 유닛 판 본문 — 능력치(체력 · 공격력 · 사거리) / 특성 / board의 건물이 이 유닛에 켜 준 효과 순으로,
        // 뒤로 갈수록 작은 글씨. 생산하지 않는 건물이면 null
        public static string UnitInfo(BuildingDefinition building, OwnedBuilding[] board)
        {
            UnitDefinition unit = building.Unit;
            if (unit == null)
                return null;
            var lines = new List<string> { ColorNumbers($"체력 {unit.MaxHp:0}") };
            if (unit.AttackType == AttackType.Heal)
                lines.Add(ColorNumbers($"회복 {unit.HealAmount:0}"));
            else if (unit.AttackType == AttackType.Flame)
                lines.Add(ColorNumbers($"공격력 초당 {unit.AttackDamage / unit.AttackInterval:0}"));
            else
                lines.Add(ColorNumbers($"공격력 {unit.AttackDamage:0}"));
            lines.Add("사거리 " + RangeWord(unit.AttackRange));

            var traits = new List<string>();
            if (unit.ChargeDamageMultiplier > 1f)
                traits.Add(ColorNumbers($"달려와 처음 때리는 공격은 피해 ×{unit.ChargeDamageMultiplier:0.#}입니다."));
            if (unit.ProjectileSplashRadius > 0f)
                traits.Add("맞은 자리 주변의 적도 피해를 입습니다.");
            if (unit.AttackType == AttackType.Flame)
                traits.Add("앞에 있는 적을 한꺼번에 불태웁니다.");
            if (unit.AttackType == AttackType.Sweep)
                traits.Add("앞에 있는 적을 한꺼번에 칩니다.");
            if (unit.SlamInterval > 0f)
                traits.Add(ColorNumbers($"{unit.SlamInterval:0.#}초마다 뛰어올라 주변 적을 띄웁니다."));
            AddGroup(lines, traits, UnitTraitSize);

            var effects = new List<string>();
            foreach (OwnedBuilding owned in board)
                if (!OwnedBuilding.IsEmpty(owned))
                    foreach (UnitEffect effect in owned.Definition.Effects)
                        if (effect.AppliesTo(unit) && !string.IsNullOrEmpty(effect.Description))
                            effects.Add(Highlight(effect.Describe(owned.Level)));
            AddGroup(lines, effects, UnitEffectSize);
            return string.Join("\n", lines);
        }

        // group을 size 글씨로 붙인다 — 묶음은 글자 크기로만 갈린다(빈 줄은 legacy Text가 본문 크기로 잡아 너무 벌어진다)
        private static void AddGroup(List<string> lines, List<string> group, int size)
        {
            foreach (string line in group)
                lines.Add($"<size={size}>{line}</size>");
        }

        private static string ColorNumbers(string text) => Regex.Replace(text, @"\d+(\.\d+)?%?", $"<color={NumberColor}>$0</color>");

        // 효과 글 — 숫자는 노랑, 키워드(기획서 §5 낱말 · 병종 태그)는 굵게. 색은 늘리지 않는다(CLAUDE.md §5-2 9번).
        // 「관통탄 특허국」 같은 기물 이름 속 낱말은 건너뛴다
        private static readonly Regex Keyword =
            new Regex("「[^」]*」|인접 기물(들|이)|전투 동안|영구히|가속|무적|고도|크기|스택|관통(?!탄)|경보병|중보병|궁병|경기병|중기병|지원병");

        private static string Highlight(string text) => ColorNumbers(Keyword.Replace(text, "<b>$0</b>"));

        // 근접 0.8 · 코끼리 3 · 공룡 5 · 사제 6 · 궁수 7 · 투석기 10
        private static string RangeWord(float range) => range <= 1f ? "가까움" : range <= 5f ? "보통" : "멂";

        // 다음 등급 — 같은 기물·같은 등급 하나와 합치면 오른다
        public static string UpgradeNote(OwnedBuilding building)
        {
            if (!building.CanUpgrade)
                return "";
            return Note($"같은 등급 하나를 더 모으면 {(building.Rarity + 1).DisplayName()} 등급이 됩니다.");
        }

        private void Awake()
        {
            _instance = this;
            _panel.gameObject.SetActive(false);
        }

        // unitInfo = UnitInfo(...) — null이면 유닛 판을 끈다
        public static void Show(Component owner, string title, string body, UnitDefinition unit, string unitInfo)
        {
            if (_instance != null)
                _instance.Place(owner, title, body, unit, unitInfo);
        }

        public static void Hide(Component owner)
        {
            if (_instance != null && _instance._owner == owner)
            {
                _instance._owner = null;
                _instance._panel.gameObject.SetActive(false);
            }
        }

        private void Place(Component owner, string title, string body, UnitDefinition unit, string unitInfo)
        {
            _owner = owner;
            _title.text = title;
            _body.text = body;
            // 위에서부터 쌓는다 — Title · Body는 판 위쪽 기준(pivot 위)
            float titleHeight = Fit(_title, -Pad);
            float bodyHeight = Fit(_body, -(Pad + titleHeight + InnerGap));
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, Pad + titleHeight + InnerGap + bodyHeight + Pad);

            _unitPanel.gameObject.SetActive(unitInfo != null);
            if (unitInfo != null)
            {
                if (_iconUnit != unit)
                    BuildIcon(unit);
                _unitName.text = UnitName(unit);
                _unitBody.text = unitInfo;
                // 이름 · 병종은 그림 높이 가운데에
                float nameHeight = _unitName.preferredHeight;
                float header = Mathf.Max(_unitIcon.sizeDelta.y, nameHeight);
                _unitIcon.anchoredPosition = new Vector2(_unitIcon.anchoredPosition.x, -(Pad + (header - _unitIcon.sizeDelta.y) / 2f));
                Fit(_unitName, -(Pad + (header - nameHeight) / 2f));
                float unitBodyHeight = Fit(_unitBody, -(Pad + header + InnerGap));
                _unitPanel.sizeDelta = new Vector2(_unitPanel.sizeDelta.x, Pad + header + InnerGap + unitBodyHeight + Pad);
            }
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

        // 글 높이에 맞춰 text를 판 위쪽에서 y만큼 내려 놓는다 — 높이를 돌려준다
        private static float Fit(Text text, float y)
        {
            float height = text.preferredHeight;
            RectTransform rect = text.rectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            return height;
        }

        // 프리팹의 그림 부품(몸통 · 무기 · 말 …)을 같은 배치 · 색 · 그리는 순서로 Image에 옮긴다.
        // 배율은 IconBox에 들어가는 가장 큰 정수배(PPU 8 그림 1픽셀 = 화면 배율 px)
        private void BuildIcon(UnitDefinition unit)
        {
            _iconUnit = unit;
            for (int i = _unitIcon.childCount - 1; i >= 0; i--)
                Destroy(_unitIcon.GetChild(i).gameObject);

            // 적 전용 유닛(투석기)은 아군 프리팹이 없다
            Unit prefab = unit.PrefabFor(Team.Ally) != null ? unit.PrefabFor(Team.Ally) : unit.PrefabFor(Team.Enemy);
            var parts = new List<SpriteRenderer>();
            foreach (SpriteRenderer part in prefab.GetComponentsInChildren<SpriteRenderer>())
                if (part.sprite != null)
                    parts.Add(part);
            parts.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));

            // 부품 네 귀퉁이로 전체 범위(유닛 좌표)
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (SpriteRenderer part in parts)
            {
                Bounds b = part.sprite.bounds;
                Matrix4x4 m = toRoot * part.transform.localToWorldMatrix;
                foreach (Vector3 corner in new[] { b.min, b.max, new Vector3(b.min.x, b.max.y), new Vector3(b.max.x, b.min.y) })
                {
                    Vector2 p = m.MultiplyPoint3x4(corner);
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            }
            Vector2 extent = max - min;
            float scale = 8f * Mathf.Max(1, Mathf.FloorToInt(IconBox / (Mathf.Max(extent.x, extent.y) * 8f)));
            _unitIcon.sizeDelta = extent * scale;
            _unitIcon.anchoredPosition = new Vector2(Side + (IconBox - _unitIcon.sizeDelta.x) / 2f, _unitIcon.anchoredPosition.y);

            foreach (SpriteRenderer part in parts)
            {
                Matrix4x4 m = toRoot * part.transform.localToWorldMatrix;
                var image = new GameObject(part.name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.sprite = part.sprite;
                image.color = part.color;
                RectTransform rect = image.rectTransform;
                rect.SetParent(_unitIcon, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = part.sprite.pivot / part.sprite.rect.size;
                rect.sizeDelta = part.sprite.rect.size / part.sprite.pixelsPerUnit * scale * (Vector2)m.lossyScale;
                rect.anchoredPosition = ((Vector2)m.GetColumn(3) - min) * scale;
                rect.localRotation = m.rotation;
            }
        }
    }
}
