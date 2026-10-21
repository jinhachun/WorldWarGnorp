using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 건물에 마우스를 올리면 뜨는 설명 창 (The Bazaar식) — 칸 오른쪽에, 오른쪽에 자리가 없으면 왼쪽에 뜬다.
    // 칸·카드는 마우스가 올라가 있는 동안 매 프레임 Show로 내용을 다시 넣는다(레벨·판매가가 바뀌어도 맞게).
    // 이 오브젝트는 켜 둔 채 판(_panel)만 켜고 끈다 — 꺼 두면 Awake가 안 돈다.
    // 두 판의 높이는 글에 맞춰 늘고 준다(§5-1의 툴팁 예외 — 검은 단색 판이라 세로로 늘려도 안 깨지고, 테두리는 UI/NationFrame이 그 크기로 새로 그린다)
    public class BuildingTooltip : MonoBehaviour
    {
        private const float Gap = 8f;
        // 판 안쪽 위아래 여백 · 글 덩어리 사이 간격(px) — 종족 테두리(12px, UI/NationFrame)와 글 사이가 16px 되게(사용자 10/15)
        private const float Pad = 24f;
        private const float InnerGap = 8f;
        // 판 왼쪽 여백 — 글 칸들(sizeDelta.x -56)과 같다
        private const float Side = 28f;
        // 유닛 그림 칸 — 이 크기 안에 들어가는 가장 큰 정수 배율로 픽셀 그대로 그린다
        private const float IconBox = 64f;

        [Tooltip("레이캐스트를 막지 않아야 한다 — 막으면 칸에서 마우스가 빠진 걸로 잡혀 깜빡인다")]
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Text _title;
        [SerializeField] private Text _body;
        [Tooltip("설명에 나오는 유닛(BuildingDefinition.MentionedUnits)마다 한 장 — Panel 아래로 세로로 쌓는다(Panel의 자식, 위쪽 가운데 기준). " +
                 "이 판이 첫 장이자 틀이고, 둘째 장부터는 복제한다")]
        [SerializeField] private RectTransform _unitPanel;
        [Tooltip("유닛 그림 칸(왼위 기준) — 프리팹의 그림 부품을 Image로 다시 짜 넣는다")]
        [SerializeField] private RectTransform _unitIcon;
        [Tooltip("그림 오른쪽 — 이름 · 병종")]
        [SerializeField] private Text _unitName;
        [SerializeField] private Text _unitBody;
        [Header("칸 문법 · 스탯 아이콘 (사용자 10/20)")]
        [Tooltip("등급 보석(8×8) — 일반 · 고급 · 희귀 · 영웅 · 전설 · 신화 순. 제목 아래 등급 칩에 선다(칸에는 붙이지 않는다 — 등급은 창틀 색으로 보인다, 사용자 10/20)")]
        [SerializeField] private Sprite[] _gems;
        [Tooltip("유닛 판 능력치 줄 앞 아이콘 — 흰 그림, 색은 코드가 칠한다(StatColors). 체력 · 회복은 하트")]
        [SerializeField] private Sprite _hpIcon;
        [SerializeField] private Sprite _attackIcon;
        [SerializeField] private Sprite _rangeIcon;

        private static BuildingTooltip _instance;

        // 등급 보석 그림(없으면 null)
        public static Sprite GemOf(BuildingRarity rarity)
            => _instance != null && _instance._gems != null && (int)rarity < _instance._gems.Length ? _instance._gems[(int)rarity] : null;
        // 상점이 열려 있으면 설명 창이 서는 자리(UI/ShopPanel이 넣고 뺀다) — 화면 안에 있을 때만 쓴다
        public static RectTransform Dock { get; set; }
        private readonly Vector3[] _corners = new Vector3[4];
        // 판 · 유닛 판의 원래 폭(씬 값) — 상점 자리에 설 때만 그 자리 폭으로 바꾼다
        private float _panelWidth, _cardWidth;
        // 유닛 판끼리 · 기물 판과 유닛 판 사이(px) · 능력치 아이콘 칸의 폭(글이 이 뒤에서 시작한다)
        private const float CardGap = 6f;
        private const float StatIconSize = 32f;
        private Component _owner;

        // 유닛 판 한 장 — 그림은 유닛·진영(영주 색)이 바뀔 때만 다시 짠다
        private class UnitCard
        {
            public RectTransform Panel;
            public RectTransform Icon;
            public Text Name;
            public Text Body;
            public UnitDefinition IconUnit;
            public bool IconEnemy;
            public Color IconColor;
            // 본문을 마지막으로 쓴 때의 유닛 · 진영 · 스탯 판(StatBook.Version) — 전투 중에는 값이 바뀔 때만 글을 다시 짠다
            public UnitDefinition BodyUnit;
            public bool BodyEnemy;
            public int BodyVersion = -1;
            // 그 유닛에 걸린 업그레이드 줄들(본문 아래)
            public UpgradeLines Upgrades;
            // 능력치 줄(아이콘 + 글) — 이름 아래, 본문 위. 처음 채울 때 만든다
            public RectTransform Stats;
            public readonly List<(Image Icon, Text Text)> Rows = new List<(Image, Text)>();
            public List<StatLine> StatLines;
        }

        // 능력치 한 줄 — 아이콘(없으면 null) · 아이콘 색 · 글
        private struct StatLine
        {
            public Sprite Icon;
            public Color Color;
            public string Text;
        }

        // 능력치 아이콘 칸의 색(사용자 10/20 — UI 스탯 아이콘은 색 허용. 숫자 글자 색은 §5-2-9 그대로)
        private static readonly Color HpIconColor = new Color(0.42f, 0.85f, 0.42f);
        private static readonly Color HealIconColor = new Color(1f, 0.55f, 0.75f);
        private static readonly Color AttackIconColor = new Color(0.95f, 0.35f, 0.3f);
        private static readonly Color RangeIconColor = new Color(0.55f, 0.72f, 1f);
        // 능력치 줄 — 아이콘과 글 사이 · 줄 사이(px)
        private const float StatIconGap = 8f;
        private const float StatRowGap = 2f;

        private readonly List<UnitCard> _cards = new List<UnitCard>();
        // 기물에 걸린 업그레이드 줄들(본문 아래) · 이번에 띄운 유닛 판마다 걸린 업그레이드
        private UpgradeLines _upgrades;
        private InlineIcons _inline;
        private readonly List<List<UpgradeHolding>> _unitUpgrades = new List<List<UpgradeHolding>>();
        // 제목 아래 칩 둘(사용자 10/20) — 등급 = 보석 + 이름(테두리 = 등급 테두리 색) · 국가 = 국가 기호(WorldConfig.NationIcon) + 이름(회색 테두리, 공용은 글자만).
        // 칩 글자는 흰색 도트 — 등급 색은 테두리에만(글자 색 체계와 별개). 칩 줄 아래 얇은 구분선
        private const int ChipTextSize = 24;
        private const float ChipPad = 6f;
        private const float ChipIconGap = 6f;
        private const float ChipGap = 10f;
        private const float ChipHeight = 32f;
        private const float ChipBorder = 2f;
        // 도트 폰트는 글자 위쪽 여백이 커서 가운데 맞춤일 때 올린다(캡처로 맞춘 값)
        private const float ChipTextLift = 5f;
        private const float RuleHeight = 2f;

        private class Chip
        {
            public RectTransform Root;
            public Image Box;
            public Outline Border;
            public Image Icon;
            public Text Label;
        }

        private RectTransform _tags;
        private Chip _rarityChip;
        private Chip _nationChip;
        private Image _rule;

        public const string NumberColor = "#FFD95A";
        public const string NoteColor = "#9A9AA8";
        public const string EnemyColor = "#FF7A6E";
        // 유닛 판 능력치의 영구 강화분(「공격력 11+1」의 +1) · RANK UP 미리보기의 바뀐 숫자 — 버프 선의 체력 초록(BuffLinkFx)보다 밝은 초록
        private const string BonusColor = "#5AE66E";
        // 글자 크기 — 중요한 것일수록 크게. 능력치 줄은 _unitBody의 글자 크기.
        // 제목 · 유닛 이름 칸은 도트 폰트(12px)라 12의 정수배만 쓰고 굵게(<b>)를 쓰지 않는다 — 합성 굵기가 픽셀을 뭉갠다
        private const int UnitNameSize = 36;
        private const int UnitCategorySize = 24;
        private const int UnitTraitSize = 18;
        private const int UnitEffectSize = 16;

        // 효과 줄 — "N초마다 ~" 먼저(기획서: 쿨다운 효과는 첫 줄), 그다음 늘 켜진 것. cooldown = 지금 간격(전투 중엔 줄어든 값). 글 규칙은 CLAUDE.md §5-2
        // mythic = 신화 등급이면 기획서 §7-1의 신화 줄을 §7 줄들 아래에 덧붙인다(구분 표시 없이)
        public static string Describe(BuildingDefinition building, int level, float cooldown, bool mythic = false, int extraSummons = 0)
            => Highlight(string.Join("\n", Lines(building, level, cooldown, mythic, extraSummons)));

        private static List<string> Lines(BuildingDefinition building, int level, float cooldown, bool mythic, int extraSummons = 0)
        {
            var lines = new List<string>();
            string every = $"{cooldown:0.#}초마다 ";
            if (building.Unit != null)
                lines.Add(every + $"{building.Unit.DisplayName} {building.SummonCount + extraSummons}기를 소환합니다.");
            foreach (BuildingAction action in building.Actions)
                lines.Add(every + action.Describe(level));
            if (!string.IsNullOrEmpty(building.Description))
                lines.Add(every + building.Description);
            foreach (BuildingPassive passive in building.Passives)
                lines.Add(passive.Describe(level));
            foreach (UnitEffect effect in building.Effects)
                if (!string.IsNullOrEmpty(effect.Description))
                    lines.Add(effect.Describe(level));
            if (!mythic)
                return lines;
            foreach (BuildingAction action in building.MythicActions)
                lines.Add(every + action.Describe(level));
            foreach (BuildingPassive passive in building.MythicPassives)
                lines.Add(passive.Describe(level));
            foreach (UnitEffect effect in building.MythicEffects)
                if (!string.IsNullOrEmpty(effect.Description))
                    lines.Add(effect.Describe(level));
            return lines;
        }

        // RANK UP 진열 카드 — 합쳐진 뒤(afterLevel, next 등급)의 효과 줄 중 지금(level)과 달라지는 줄만. 바뀐 숫자는 BonusColor
        public static string RankUpPreview(BuildingDefinition building, int level, BuildingRarity next, int extraSummons = 0)
        {
            // next는 한 등급 위이므로 지금 등급은 next - 1 — 전설 → 신화에서만 after에 신화 줄이 늘어난다.
            // 합친 뒤의 레벨은 OwnedBuilding과 같은 상한을 받는다 — 신화는 수치가 전설과 같고 신화 줄만 늘어난다. 쿨다운만은 신화에서도 한 단계 더 준다(OwnedBuilding.CooldownLevelOf)
            int upgrades = (int)next - (int)building.Rarity;
            int afterLevel = OwnedBuilding.LevelOf(building, upgrades);
            List<string> now = Lines(building, level, building.CooldownAt(OwnedBuilding.CooldownLevelOf(building, upgrades - 1)), next - 1 == BuildingRarity.Mythic, extraSummons);
            List<string> after = Lines(building, afterLevel, building.CooldownAt(OwnedBuilding.CooldownLevelOf(building, upgrades)), next == BuildingRarity.Mythic, extraSummons);
            string text = $"\n<color={NumberColor}>{next.DisplayName()} 등급이 되면</color>";
            bool any = false;
            for (int i = 0; i < after.Count; i++)
            {
                if (i < now.Count && now[i] == after[i])
                    continue;
                any = true;
                text += "\n" + MarkChanges(i < now.Count ? now[i] : "", after[i]);
            }
            return any ? text : text + Note("효과는 그대로입니다.");
        }

        // after 줄을 효과 글처럼 꾸미되, before와 값이 다른 숫자만 BonusColor로(숫자 개수가 다르면 전부)
        private static string MarkChanges(string before, string after)
        {
            MatchCollection old = Number.Matches(before);
            bool aligned = old.Count == Number.Matches(after).Count;
            int n = 0;
            return Number.Replace(Keyword.Replace(after, "<b>$0</b>"), m =>
            {
                bool same = aligned && old[n].Value == m.Value;
                n++;
                return $"<color={(same ? NumberColor : BonusColor)}>{m.Value}</color>";
            });
        }

        // 칸 아래 대표 숫자 — 툴팁 첫 숫자와 같은 순서(생산 수 · 액션 · 패시브 · 효과)로 처음 나오는 것. 없으면 ""
        public static string Headline(BuildingDefinition building, int level)
        {
            if (building.Unit != null)
                return $"{building.SummonCount}기";
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

        // 효과가 아닌 상태 줄(스택) — 회색, 맨 아래
        private static string Note(string text) => $"\n<color={NoteColor}>{text}</color>";

        // 첫 줄 이름(크게) · 둘째 줄 등급 · 국가(작게). 세력은 쓰지 않는다(사용자 결정)
        public static string Title(BuildingDefinition building, bool enemy, BuildingRarity rarity)
        {
            string name = $"{building.DisplayName}{(enemy ? " (적)" : "")}";
            if (enemy)
                name = $"<color={EnemyColor}>{name}</color>";
            return name;   // 등급 · 국가는 제목 아래 칩(Show의 tags)
        }

        // 유닛 판 그림 오른쪽 글 — 이름(크게) · 병종(작게)
        private static string UnitName(UnitDefinition unit)
            => $"<size={UnitNameSize}>{unit.DisplayName}</size>\n<size={UnitCategorySize}>{unit.Category.DisplayName}</size>";

        // 유닛 판 능력치 줄(체력 · 공격력 · 사거리) — 줄마다 앞에 색 아이콘 칸(Fill이 그린다)
        private List<StatLine> StatLines(UnitDefinition unit, bool enemy)
        {
            var lines = new List<StatLine> { new StatLine { Icon = _hpIcon, Color = HpIconColor, Text = Stat("체력", unit, UnitStat.MaxHp, enemy) } };
            if (unit.AttackType == AttackType.Build)
                lines.Add(new StatLine { Text = "공격 안 함" });   // 싸우지 않는 유닛(건축가) — 사거리도 뜻이 없어 뺀다
            else if (unit.AttackType == AttackType.Heal)
                lines.Add(new StatLine { Icon = _hpIcon, Color = HealIconColor, Text = Stat("회복", unit, UnitStat.Attack, enemy) });   // 회복량 = 공격력
            else if (unit.AttackType == AttackType.Flame)
                lines.Add(new StatLine { Icon = _attackIcon, Color = AttackIconColor, Text = Stat("공격력 초당", unit, UnitStat.Attack, enemy, 1f / unit.AttackInterval) });
            else
                lines.Add(new StatLine { Icon = _attackIcon, Color = AttackIconColor, Text = Stat("공격력", unit, UnitStat.Attack, enemy) });
            if (unit.AttackType != AttackType.Build)
                lines.Add(new StatLine { Icon = _rangeIcon, Color = RangeIconColor, Text = "사거리 " + RangeWord(unit.AttackRange) });
            return lines;
        }

        // 유닛 판 본문(능력치 줄 아래) — 특성 / board의 건물이 이 유닛에 켜 준 효과 순으로, 뒤로 갈수록 작은 글씨
        private static string UnitInfo(UnitDefinition unit, OwnedBuilding[] board)
        {
            var lines = new List<string>();

            var traits = new List<string>();
            if (unit.ChargeDamageMultiplier > 1f)
                traits.Add(ColorNumbers($"달려와 처음 때리는 공격은 피해 ×{unit.ChargeDamageMultiplier:0.#}입니다."));
            if (unit.ProjectileSplashRadius > 0f)
                traits.Add("맞은 자리 주변의 적도 피해를 입습니다.");
            if (unit.AttackType == AttackType.Flame)
                traits.Add("앞에 있는 적을 한꺼번에 불태웁니다.");
            if (unit.AttackType == AttackType.Sweep)
                traits.Add("앞에 있는 적을 한꺼번에 칩니다.");
            if (unit.AttackType == AttackType.Build)
                traits.Add("가까운 땅에 작은 타워를 설치합니다.");
            if (unit.SlamInterval > 0f)
                traits.Add(ColorNumbers($"{unit.SlamInterval:0.#}초마다 뛰어올라 주변 적을 띄웁니다."));
            // 타고난 특성(UnitDefinition.TraitLines) — 그 유닛이면 늘 가진 효과. 숫자는 에셋 값으로 채워져 온다
            foreach (string line in unit.TraitLines)
                traits.Add(ColorNumbers(line));
            AddGroup(lines, traits, UnitTraitSize);

            var effects = new List<string>();
            foreach (OwnedBuilding owned in board)
                if (!OwnedBuilding.IsEmpty(owned))
                    foreach (UnitEffect effect in owned.Effects)
                        if (effect.AppliesTo(unit) && !unit.HasTrait(effect) && !string.IsNullOrEmpty(effect.Description))   // 타고난 특성으로 이미 쓴 것은 건너뛴다
                            effects.Add(Highlight(effect.Describe(owned.Level)));
            AddGroup(lines, effects, UnitEffectSize);
            return string.Join("\n", lines);
        }

        // 능력치 한 줄 — 기본값(노랑) 뒤에 달라진 만큼을 정수로: 늘면 「+7」(BonusColor), 줄면 「-2」(빨강). scale = 표시 단위 환산(초당 피해).
        // 달라진 만큼 = 반올림한 지금 값 − 반올림한 기본값 — 두 숫자를 더하면 화면의 최종값(지금 값의 반올림)이 된다
        //   전투 중: 그 진영에서 지금 막 소환되면 갖는 값(Unit.SpawnStat — 영구히 · 전투 동안 · 맵 · 지역 업그레이드, 유닛의 계산과 같은 길). 적 기물이면 적 진영의 값
        //   전투 밖: 전투가 시작될 때의 값(RunStats.Preview — 영구히 · 지역 업그레이드). 남의 탑이면 그 영주의 값
        private static string Stat(string label, UnitDefinition unit, UnitStat stat, bool enemy, float scale = 1f)
        {
            float baseValue = Unit.BaseStat(unit, stat);
            float now = BattleManager.Fighting ? Unit.SpawnStat(enemy ? Team.Enemy : Team.Ally, unit, stat) : RunStats.Preview(unit, stat, enemy).Now;
            int shown = Whole(baseValue * scale), bonus = Whole(now * scale) - shown;
            string text = $"{label} <color={NumberColor}>{shown}</color>";
            if (bonus != 0)
                text += $"<color={(bonus > 0 ? BonusColor : EnemyColor)}>{bonus:+0;-0}</color>";
            return text;
        }

        private static int Whole(float value) => (int)System.Math.Round(value, System.MidpointRounding.AwayFromZero);

        // group을 size 글씨로 붙인다 — 묶음은 글자 크기로만 갈린다(빈 줄은 legacy Text가 본문 크기로 잡아 너무 벌어진다)
        private static void AddGroup(List<string> lines, List<string> group, int size)
        {
            foreach (string line in group)
                lines.Add($"<size={size}>{line}</size>");
        }

        private static readonly Regex Number = new Regex(@"\d+(\.\d+)?%?");

        public static string ColorNumbers(string text) => Number.Replace(text, $"<color={NumberColor}>$0</color>");

        // 효과 글 — 숫자는 노랑, 키워드(기획서 §5 낱말 · 병종 태그)는 굵게. 색은 늘리지 않는다(CLAUDE.md §5-2 9번).
        // 「관통탄 특허국」 같은 기물 이름 속 낱말은 건너뛴다
        private static readonly Regex Keyword =
            new Regex("「[^」]*」|아래에 있는 모든 기물|위에 있는 모든 기물|바로 위·아래 기물들|바로 위 기물|전투 동안|영구히|가속|무적|고도|크기|스택|관통(?!탄)|경보병|중보병|궁병|경기병|중기병|지원병");

        public static string Highlight(string text) => ColorNumbers(Keyword.Replace(text, "<b>$0</b>"));

        // 근접 0.8 · 코끼리 3 · 공룡 5 · 사제 6 · 궁수 7 · 투석기 10
        private static string RangeWord(float range) => range <= 1f ? "가까움" : range <= 5f ? "보통" : "멂";

        private void Awake()
        {
            _instance = this;
            _panel.gameObject.SetActive(false);
            _panelWidth = _panel.rect.width;
            _cardWidth = _unitPanel.rect.width;
            _upgrades = NewLines(_panel);
            _inline = _body.gameObject.AddComponent<InlineIcons>();
            _tags = (RectTransform)new GameObject("Tags", typeof(RectTransform)).transform;
            _tags.SetParent(_panel, false);
            _tags.anchorMin = _tags.anchorMax = _tags.pivot = new Vector2(0f, 1f);
            _rarityChip = NewChip(_tags, "Rarity");
            _nationChip = NewChip(_tags, "Nation");
            ColorUtility.TryParseHtmlString(NoteColor, out Color gray);
            _rule = new GameObject("Rule", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _rule.rectTransform.SetParent(_panel, false);
            _rule.rectTransform.anchorMin = _rule.rectTransform.anchorMax = _rule.rectTransform.pivot = new Vector2(0f, 1f);
            _rule.color = gray;
            _rule.raycastTarget = false;
            _cards.Add(new UnitCard { Panel = _unitPanel, Icon = _unitIcon, Name = _unitName, Body = _unitBody, Upgrades = NewLines(_unitPanel) });
        }

        // units = 설명에 나오는 유닛(설명 순서) — 한 장씩 판 아래로 쌓는다. board = 유닛 판의 "켜 준 효과"를 찾을 보드
        // 가진 기물(필드·보관함 칸, 전장 탑 층) — running = 전투 중 돌고 있는 칸(상점에선 null)
        public static void ShowOwned(Component owner, OwnedBuilding building, BoardRunner.Slot running, bool enemy)
        {
            float cooldown = running != null ? running.Cooldown : building.Cooldown;
            // 징병제(지역 특수 업그레이드) — 쿨다운마다 유닛을 소환하는 기물의 소환 수에 더해 보인다(숫자는 지금 실제 값 하나)
            int extra = building.Definition.Unit != null && building.Definition.Cooldown > 0f ? RunState.ExtraSummonsFor(building) : 0;
            string body = Describe(building.Definition, building.Level, cooldown, building.Rarity == BuildingRarity.Mythic, extra);
            // 효과가 아닌 줄은 스택뿐 — 다음 등급 · 판매가는 쓰지 않는다(사용자 10/10: 글 최소화. 판매가는 판매 버튼에 나온다)
            if (!enemy && !BattleManager.Fighting && building.Stacks > 0)
                body += Note($"스택 {building.Stacks}");
            // 수를 세어 발동하는 효과(라이칸스로프 · 승천의 전당 · 미궁의 입구) — 전투 중에만, 그 진영이 센 값으로 남은 수(사용자 10/14). 전투마다 0부터라 상점에서는 쓰지 않는다
            if (BattleManager.Fighting)
            {
                Team side = enemy ? Team.Enemy : Team.Ally;
                foreach (BuildingPassive passive in building.Passives)
                    if (passive is DeathCountCooldownPassive deathCount)
                        body += Note($"다음 쿨다운 감소까지 {deathCount.RemainingOf(side)}기");
                    else if (passive is TowerKillBuyPassive towerKill)
                        body += Note($"다음 발동까지 {towerKill.RemainingOf(side)}기");
                foreach (UnitEffect effect in building.Effects)
                    if (effect is AscensionEffect ascension)
                        body += Note($"다음 발동까지 {ascension.RemainingOf(side)}기");
                    else if (effect is DeathCountSummonEffect deathCount)
                        body += Note(deathCount.RemainingOf(side) > 0 ? $"발동까지 {deathCount.RemainingOf(side)}기" : "이번 전투 발동 끝");
            }
            // 적 탑 — 전투 씬에서는 돌고 있는 보드, 지도 씬에서는 미리 보는 영주의 탑
            BoardRunner runner = BoardRunner.For(Team.Enemy);
            OwnedBuilding[] board = !enemy ? RunState.Field : runner != null ? System.Array.ConvertAll(runner.Slots, s => s.Building) : RunState.EnemyField;
            // 그 기물에 걸린 업그레이드(군주제의 「2번」 등)는 본문 아래의 업그레이드 줄이 보여 준다
            Show(owner, Title(building.Definition, enemy, building.Rarity), body, building.Definition.MentionedUnits, board, enemy, RunState.UpgradesOn(building),
                (building.Rarity, building.Definition.Nation));
        }

        // upgrades = 본문 아래에 아이콘과 함께 보일 「걸려 있는 업그레이드」(없으면 null). 유닛 판의 것은 유닛마다 스스로 찾는다(RunState.UpgradesOn)
        // tags = 제목 아래 등급 · 국가 칩(기물일 때만 — 맵 설명 등은 null)
        public static void Show(Component owner, string title, string body, UnitDefinition[] units, OwnedBuilding[] board, bool enemy = false, List<UpgradeHolding> upgrades = null,
            (BuildingRarity Rarity, BuildingNation Nation)? tags = null)
        {
            if (_instance != null)
                _instance.Place(owner, title, body, units, board, enemy, upgrades, tags);
        }

        // 칩 하나 — 검은 판(테두리 = Outline) · 아이콘(원본 크기) · 흰 도트 글. 내용은 SetChip이 채운다
        private Chip NewChip(RectTransform parent, string name)
        {
            var chip = new Chip { Root = (RectTransform)new GameObject(name, typeof(RectTransform)).transform };
            chip.Root.SetParent(parent, false);
            chip.Root.anchorMin = chip.Root.anchorMax = chip.Root.pivot = new Vector2(0f, 1f);
            chip.Box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            chip.Box.rectTransform.SetParent(chip.Root, false);
            chip.Box.rectTransform.anchorMin = Vector2.zero;
            chip.Box.rectTransform.anchorMax = Vector2.one;
            chip.Box.rectTransform.offsetMin = chip.Box.rectTransform.offsetMax = Vector2.zero;
            chip.Box.color = Color.black;
            chip.Box.raycastTarget = false;
            chip.Border = chip.Box.gameObject.AddComponent<Outline>();
            chip.Border.effectDistance = new Vector2(ChipBorder, -ChipBorder);
            chip.Icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            chip.Icon.rectTransform.SetParent(chip.Root, false);
            chip.Icon.rectTransform.anchorMin = chip.Icon.rectTransform.anchorMax = chip.Icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            chip.Icon.preserveAspect = true;
            chip.Icon.raycastTarget = false;
            chip.Label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            chip.Label.rectTransform.SetParent(chip.Root, false);
            chip.Label.rectTransform.anchorMin = chip.Label.rectTransform.anchorMax = chip.Label.rectTransform.pivot = new Vector2(0f, 0.5f);
            chip.Label.font = _title.font;
            chip.Label.fontSize = ChipTextSize;
            chip.Label.color = Color.white;
            chip.Label.raycastTarget = false;
            chip.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            chip.Label.verticalOverflow = VerticalWrapMode.Overflow;
            return chip;
        }

        // 칩을 채우고 폭을 돌려준다 — icon(원본 크기)이 없으면 글자만
        private static float SetChip(Chip chip, Sprite icon, string label, Color border, float x)
        {
            float left = ChipPad;
            chip.Icon.enabled = icon != null;
            if (icon != null)
            {
                chip.Icon.sprite = icon;
                chip.Icon.SetNativeSize();
                chip.Icon.rectTransform.anchoredPosition = new Vector2(left, 0f);
                left += chip.Icon.rectTransform.sizeDelta.x + ChipIconGap;
            }
            chip.Label.text = label;
            chip.Label.rectTransform.sizeDelta = new Vector2(chip.Label.preferredWidth, ChipTextSize);
            chip.Label.rectTransform.anchoredPosition = new Vector2(left, ChipTextLift);
            float width = left + chip.Label.preferredWidth + ChipPad;
            chip.Border.effectColor = border;
            chip.Root.sizeDelta = new Vector2(width, ChipHeight);
            chip.Root.anchoredPosition = new Vector2(x, -ChipBorder);   // 테두리가 칩 줄 밖으로 삐지지 않게
            return width;
        }

        private UpgradeLines NewLines(RectTransform panel) => new UpgradeLines(panel, Side, panel.rect.width - Side * 2f, _title.font);

        // i번째 유닛 판 — 모자라면 첫 장을 복제한다
        private UnitCard Card(int i)
        {
            while (_cards.Count <= i)
            {
                RectTransform panel = Instantiate(_unitPanel, _unitPanel.parent);
                // 첫 장의 능력치 줄 · 업그레이드 줄들까지 복제됐다 — 버리고 이 장의 것을 새로 만든다(능력치 줄이 나중에 생겨 뒤에 있으니 먼저 지운다)
                if (_cards[0].Stats != null)
                    DestroyImmediate(panel.GetChild(_cards[0].Stats.GetSiblingIndex()).gameObject);
                DestroyImmediate(panel.Find("Upgrades").gameObject);
                _cards.Add(new UnitCard
                {
                    Upgrades = NewLines(panel),
                    Panel = panel,
                    Icon = (RectTransform)panel.Find(_unitIcon.name),
                    Name = panel.Find(_unitName.name).GetComponent<Text>(),
                    Body = panel.Find(_unitBody.name).GetComponent<Text>(),
                    IconUnit = _cards[0].IconUnit,   // 그림 부품도 함께 복제됐다
                    IconEnemy = _cards[0].IconEnemy,
                    IconColor = _cards[0].IconColor,
                });
            }
            return _cards[i];
        }

        public static void Hide(Component owner)
        {
            if (_instance != null && _instance._owner == owner)
            {
                _instance._owner = null;
                _instance._panel.gameObject.SetActive(false);
            }
        }

        // 놓는 자리(사용자 10/20 U5) — 둘 중 하나:
        //   상점이 열려 있으면 상점의 빈 자리(Dock — 진열 · 확률 · 보관함을 덮지 않는 곳)에 그 폭으로, 유닛 판은 기물 판 아래로
        //   그 밖에는 칸 오른쪽(자리 없으면 왼쪽)에, 유닛 판은 기물 판 옆으로 한 장씩 — 화면을 넘으면 앞 판 아래로
        private void Place(Component owner, string title, string body, UnitDefinition[] units, OwnedBuilding[] board, bool enemy, List<UpgradeHolding> upgrades,
            (BuildingRarity Rarity, BuildingNation Nation)? tags)
        {
            _owner = owner;
            float scale = _panel.lossyScale.y;
            float gap = Gap * scale;
            // 폭은 상점 자리 폭 — 자리는 아래에서 고른다
            bool docked = Docked(out Rect dock);
            float width = docked ? dock.width / scale : _panelWidth;
            float cardWidth = docked ? width : _cardWidth;
            SetWidth(_panel, width);
            _upgrades.Width = width - Side * 2f;
            _title.text = title;
            _inline.SetText(body);   // 스탯 낱말 앞 줄 안 아이콘(UI/InlineIcons) — 자리 글자를 넣은 글로 높이를 잰다
            // 위에서부터 쌓는다 — Title · Body는 판 위쪽 기준(pivot 위)
            float titleHeight = Fit(_title, -Pad);
            // 칩 줄 · 구분선 — 제목 아래에 쌓고, 본문 자리를 그만큼 내린다(titleHeight에 더한다)
            _tags.gameObject.SetActive(tags != null);
            _rule.gameObject.SetActive(tags != null);
            if (tags != null)
            {
                ColorUtility.TryParseHtmlString(NoteColor, out Color gray);
                BuildingRarity rarity = tags.Value.Rarity;
                BuildingNation nation = tags.Value.Nation;
                Sprite icon = RunState.Config != null && RunState.Config.World != null ? RunState.Config.World.NationIcon(nation) : null;
                float x = SetChip(_rarityChip, GemOf(rarity), rarity.DisplayName(), rarity.FrameColor(), ChipBorder);
                SetChip(_nationChip, icon, nation.DisplayName(), gray, ChipBorder + x + ChipGap);
                _tags.anchoredPosition = new Vector2(Side, -(Pad + titleHeight + InnerGap));
                titleHeight += InnerGap + ChipHeight + ChipBorder * 2f;
                _rule.rectTransform.anchoredPosition = new Vector2(Side, -(Pad + titleHeight + InnerGap));
                _rule.rectTransform.sizeDelta = new Vector2(width - Side * 2f, RuleHeight);
                titleHeight += InnerGap + RuleHeight;
            }
            float bodyHeight = Fit(_body, -(Pad + titleHeight + InnerGap));
            _inline.Place();
            _unitUpgrades.Clear();
            foreach (UnitDefinition unit in units)
                _unitUpgrades.Add(RunState.UpgradesOn(unit, enemy));
            _panel.gameObject.SetActive(true);

            // 기물 판의 업그레이드 — 본문 아래에 아이콘만(사용자 10/14)
            float height = Pad + titleHeight + InnerGap + bodyHeight;
            float lines = _upgrades.Show(upgrades, -(height + InnerGap));
            if (lines > 0f)
                height += InnerGap + lines;
            height += Pad;
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, height);
            for (int i = 0; i < Mathf.Max(units.Length, _cards.Count); i++)
            {
                bool used = i < units.Length;
                UnitCard card = Card(i);
                card.Panel.gameObject.SetActive(used);
                if (!used)
                    continue;
                RectTransform rect = card.Panel;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(cardWidth, rect.sizeDelta.y);
                card.Upgrades.Width = cardWidth - Side * 2f;
                Fill(card, units[i], board, enemy, _unitUpgrades[i]);
            }

            // Overlay 캔버스라 월드 좌표 = 화면 픽셀. 판 왼쪽 위(화면)와, 유닛 판이 놓이는 방향
            ((RectTransform)owner.transform).GetWorldCorners(_corners);   // 0 왼아래 · 1 왼위 · 2 오른위
            bool column = docked;
            int direction = 1;
            float left, wantTop;
            if (docked)
            {
                left = dock.xMin;
                wantTop = dock.yMax;
            }
            else
            {
                bool right = _corners[2].x + gap + width * scale <= Screen.width;
                direction = right ? 1 : -1;
                left = right ? _corners[2].x + gap : _corners[0].x - gap - width * scale;
                wantTop = _corners[1].y;
            }

            // 유닛 판 자리 — 기물 판 왼쪽 위 기준(로컬). 앞 판 = 기물 판에서 시작
            float lowest = -height;
            float prevX = 0f, prevY = 0f, prevWidth = width, prevHeight = height;
            bool afterPanel = true;
            for (int i = 0; i < units.Length; i++)
            {
                RectTransform rect = _cards[i].Panel;
                float h = rect.sizeDelta.y;
                float x = prevX, y = prevY - prevHeight - CardGap;
                if (!column)
                {
                    float side = direction > 0 ? prevX + prevWidth + CardGap : prevX - CardGap - cardWidth;
                    float screenLeft = left + side * scale;
                    if (screenLeft >= gap && screenLeft + cardWidth * scale <= Screen.width - gap)
                    {
                        x = side;
                        y = prevY;
                    }
                    else if (afterPanel && direction < 0)
                        x = width - cardWidth;   // 왼쪽으로 펼치던 판은 아래로 내릴 때 오른쪽 끝을 맞춘다
                }
                rect.anchoredPosition = new Vector2(x, y);
                lowest = Mathf.Min(lowest, y - h);
                prevX = x;
                prevY = y;
                prevWidth = cardWidth;
                prevHeight = h;
                afterPanel = false;
            }

            // 윗변을 칸 윗변(또는 자리 윗변)에 맞추고, 화면 위아래를 넘으면 안으로 민다
            float top = Mathf.Clamp(wantTop, -lowest * scale + gap, Screen.height - gap);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.position = new Vector3(left, top, 0f);
        }

        // 상점의 빈 자리 — 화면 안에 다 들어와 있을 때만(팝업이 미끄러지는 중 · 닫힘이면 칸 옆에 띄운다)
        private bool Docked(out Rect dock)
        {
            dock = default;
            if (Dock == null || !Dock.gameObject.activeInHierarchy)
                return false;
            var corners = new Vector3[4];
            Dock.GetWorldCorners(corners);
            dock = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            return dock.xMin >= -1f && dock.yMin >= -1f && dock.xMax <= Screen.width + 1f && dock.yMax <= Screen.height + 1f;
        }

        private static void SetWidth(RectTransform rect, float width) => rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

        // 글 높이에 맞춰 text를 판 위쪽에서 y만큼 내려 놓는다 — 높이를 돌려준다
        private static float Fit(Text text, float y)
        {
            float height = text.preferredHeight;
            RectTransform rect = text.rectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            return height;
        }

        // 유닛 판 한 장을 채우고 높이를 돌려준다 — 위에서부터 그림 · 이름 / 능력치 줄 / 본문(특성 · 켜 준 효과) / 업그레이드
        private float Fill(UnitCard card, UnitDefinition unit, OwnedBuilding[] board, bool enemy, List<UpgradeHolding> upgrades)
        {
            // 적 진영의 영주가 바뀌면(지도에서 다른 땅을 고름) 그림의 영주 색도 다시 칠한다
            if (card.IconUnit != unit || card.IconEnemy != enemy || card.IconColor != RunState.LordColor(enemy ? Team.Enemy : Team.Ally))
                BuildIcon(card, unit, enemy);
            card.Name.text = UnitName(unit);
            // 전투 중에는 보드가 안 바뀐다 — 스탯이 바뀌었을 때만 다시(가리키는 동안 매 프레임 불린다). 전투 밖에서는 상점에서 보드가 바뀌므로 늘 다시
            if (!BattleManager.Fighting || card.BodyUnit != unit || card.BodyEnemy != enemy || card.BodyVersion != StatBook.Version)
            {
                card.StatLines = StatLines(unit, enemy);
                card.Body.text = UnitInfo(unit, board);
                card.BodyUnit = unit;
                card.BodyEnemy = enemy;
                card.BodyVersion = BattleManager.Fighting ? StatBook.Version : -1;
            }
            // 이름 · 병종은 그림 높이 가운데에
            float nameHeight = card.Name.preferredHeight;
            float header = Mathf.Max(card.Icon.sizeDelta.y, nameHeight);
            card.Icon.anchoredPosition = new Vector2(card.Icon.anchoredPosition.x, -(Pad + (header - card.Icon.sizeDelta.y) / 2f));
            Fit(card.Name, -(Pad + (header - nameHeight) / 2f));
            float height = StatRows(card, Pad + header + InnerGap);
            bool hasBody = !string.IsNullOrEmpty(card.Body.text);
            card.Body.enabled = hasBody;
            if (hasBody)
                height += InnerGap + Fit(card.Body, -(height + InnerGap));
            // 그 유닛에 걸린 업그레이드 — 본문(능력치 · 특성 · 기물이 켜 준 효과) 아래에 늘 아이콘만(사용자 10/14 — 유닛 효과가 묻히지 않게)
            float lines = card.Upgrades.Show(upgrades, -(height + InnerGap));
            if (lines > 0f)
                height += InnerGap + lines;
            height += Pad;
            card.Panel.sizeDelta = new Vector2(card.Panel.sizeDelta.x, height);
            return height;
        }

        // 능력치 줄 — 판 위에서 y만큼 내려와 한 줄씩(아이콘 칸 + 글). 마지막 줄 아래 끝의 y를 돌려준다
        private float StatRows(UnitCard card, float y)
        {
            if (card.Stats == null)
            {
                card.Stats = (RectTransform)new GameObject("Stats", typeof(RectTransform)).transform;
                card.Stats.SetParent(card.Panel, false);
                card.Stats.anchorMin = card.Stats.anchorMax = card.Stats.pivot = new Vector2(0f, 1f);
                card.Stats.anchoredPosition = Vector2.zero;
                card.Stats.sizeDelta = Vector2.zero;
            }
            List<StatLine> lines = card.StatLines;
            for (int i = 0; i < Mathf.Max(lines.Count, card.Rows.Count); i++)
            {
                if (i >= card.Rows.Count)
                    card.Rows.Add(NewStatRow(card));
                (Image icon, Text text) = card.Rows[i];
                bool used = i < lines.Count;
                icon.gameObject.SetActive(used && lines[i].Icon != null);
                text.gameObject.SetActive(used);
                if (!used)
                    continue;
                if (i > 0)
                    y += StatRowGap;
                StatLine line = lines[i];
                float iconHeight = 0f;
                if (line.Icon != null)
                {
                    if (icon.sprite != line.Icon)
                    {
                        icon.sprite = line.Icon;
                        icon.SetNativeSize();
                    }
                    icon.color = line.Color;
                    iconHeight = icon.rectTransform.sizeDelta.y;
                }
                text.text = line.Text;
                float textHeight = text.preferredHeight;
                float row = Mathf.Max(iconHeight, textHeight);
                icon.rectTransform.anchoredPosition = new Vector2(Side, -(y + (row - iconHeight) / 2f));
                text.rectTransform.anchoredPosition = new Vector2(Side + StatIconSize + StatIconGap, -(y + (row - textHeight) / 2f));
                text.rectTransform.sizeDelta = new Vector2(text.preferredWidth, textHeight);
                y += row;
            }
            return y;
        }

        // 능력치 줄 한 줄 — 아이콘(원본 크기) + 본문과 같은 글꼴의 글
        private static (Image, Text) NewStatRow(UnitCard card)
        {
            var icon = new GameObject("StatIcon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.rectTransform.SetParent(card.Stats, false);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0f, 1f);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            var text = new GameObject("StatText", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.rectTransform.SetParent(card.Stats, false);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0f, 1f);
            text.font = card.Body.font;
            text.fontSize = card.Body.fontSize;
            text.color = card.Body.color;
            text.lineSpacing = card.Body.lineSpacing;
            text.supportRichText = true;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return (icon, text);
        }

        // 프리팹의 그림 부품(몸통 · 무기 · 말 …)을 같은 배치 · 색 · 그리는 순서로 Image에 옮긴다.
        // 배율은 IconBox에 들어가는 가장 큰 정수배(PPU 8 그림 1픽셀 = 화면 배율 px)
        private static void BuildIcon(UnitCard card, UnitDefinition unit, bool enemy)
        {
            card.IconUnit = unit;
            card.IconEnemy = enemy;
            card.IconColor = RunState.LordColor(enemy ? Team.Enemy : Team.Ally);
            RectTransform icon = card.Icon;
            for (int i = icon.childCount - 1; i >= 0; i--)
                Destroy(icon.GetChild(i).gameObject);

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
            icon.sizeDelta = extent * scale;
            icon.anchoredPosition = new Vector2(Side + (IconBox - icon.sizeDelta.x) / 2f, icon.anchoredPosition.y);

            foreach (SpriteRenderer part in parts)
            {
                Matrix4x4 m = toRoot * part.transform.localToWorldMatrix;
                var image = new GameObject(part.name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.sprite = LordPaint.Of(part.sprite, enemy ? Team.Enemy : Team.Ally);   // 그 진영 영주의 색으로
                image.color = part.color;
                RectTransform rect = image.rectTransform;
                rect.SetParent(icon, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = part.sprite.pivot / part.sprite.rect.size;
                rect.sizeDelta = part.sprite.rect.size / part.sprite.pixelsPerUnit * scale * (Vector2)m.lossyScale;
                rect.anchoredPosition = ((Vector2)m.GetColumn(3) - min) * scale;
                rect.localRotation = m.rotation;
            }
        }
    }
}
