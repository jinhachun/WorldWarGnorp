using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 직렬화되는 enum — 값은 맨 뒤에만 추가할 것
    public enum UpgradeKind
    {
        UnitStat,    // 대상 병종 유닛의 스탯 +N%(합연산) — 전투 동안
        TowerStat,   // 내 타워들의 스탯 +N%(합연산) — 전투 동안
        UnitEffect,  // 유닛 효과를 켠다 — 합쳐진 값은 효과가 읽는다(UnitEffect.Value)
        Summon,      // 전투 시작 시 유닛 N기 소환
        SmallTower,  // 전투 시작 시 전장 가운데에 작은 타워 — 체력 = 내 본진의 N%
        TurnGold,    // 턴마다 골드 +N
        FreeReroll,  // 턴마다 무료 리롤 +N회
        BuildingCooldown,  // 전투 시작 시 쿨다운이 있는 내 기물 중 무작위 하나의 쿨다운 ×N — 얻은 것마다 따로 뽑는다
        BaseHp,      // 내 본진(작은 타워 아님)의 체력 +N%(합연산)
        Special,     // 지역 특수 업그레이드(신화 등급) — 무엇을 하는지는 SpecialKind
        TopCooldown,     // 전투 시작 시 맨 위층부터 내려가며 쿨다운이 있는 첫 기물의 쿨다운 ×N — 얻은 것마다 따로(축성)
        SmallTowerAttack, // 내 작은 타워의 공격력 +N%(합연산) — 전투 동안(보조 포탑)
        DeathmatchFire,  // 데스매치가 시작될 때 쿨다운이 있는 내 모든 기물을 즉시 N회 발동(야근 수당)
        Interest,        // 턴 시작 시 가진 골드 N마다 골드 1 — 얻은 것마다 따로, 합은 「한 번에 얻는 양의 상한」까지(복리)
        BuyReroll,       // 「구매할 때마다,」 효과가 있는 기물을 구매하면 N% 확률로 무료 리롤 +1회(견본품)
    }

    // 전투 시작 알림(Fx/BattleStartLines)이 업그레이드 효과(UnitEffect)에서 선을 긋는 곳 — 유닛 무리(위 「병종」 · 「유닛」으로 거른다) · 본진 둘레 · 쿨다운이 있는 층 전부
    public enum UpgradeLine { Units, Base, Floors }

    // 지역 특수 업그레이드(아웃게임기획 §4 — 신화 등급, 합치기 후보에 안 나온다). 직렬화되는 enum — 값은 맨 뒤에만 추가할 것.
    // 전투에 거는 것은 UpgradeDefinition.Apply · Spawn, 상점 · 턴 · 승패에 거는 것은 RunState · WorldRun이 TowerState.Special로 묻는다
    public enum SpecialKind
    {
        None,
        Conscription,      // 징병제 — 쿨다운이 있는 내 기물이 유닛을 N기 더 소환
        CategoryAlias,     // 군제 개혁 — 내 A 병종은 B 병종으로도 취급(_categories → _alsoAs)
        MythicBuilding,    // 절대왕정 — 얻을 때 무작위 신화 기물 1개
        Enlightenment,     // 계몽주의 — 전투에서 패배하면 무작위 내 기물 1개의 쿨다운이 영구히 ×N
        Monarchy,          // 군주제 — 내 맨 위층 기물이 2번 발동
        Militarism,        // 군국주의 — 전투 시작 시 쿨다운이 가장 긴 내 기물이 즉시 발동
        Guillotine,        // 단두대 혁명 — N초마다 무작위 적 유닛 1기 즉사
        Republic,          // 공화정 — 합칠 때 나오는 업그레이드 후보 셋을 모두 얻는다
        Mercantilism,      // 중상주의 — 턴마다 얻는 골드 ×N
        Communism,         // 공산주의 — 상점의 모든 기물과 리롤이 N골드
        Theocracy,         // 신정 — 내 유닛은 처음 죽을 때 체력을 모두 회복하고 다시 일어난다(_effect)
        Imperialism,       // 제국주의 — 영주를 이길 때마다 그 영주의 기물 1개를 얻는다
        Nationalism,       // 국가주의 — 내 목숨 1마다 내 유닛의 공격력 +N%(AI 영주는 고정값 — RunState.LivesFor)
        Industrial,        // 산업혁명 — 전투 시작 후 N초 동안 내 모든 기물이 가속
        Federation,        // 연방제 — 내 모든 기물은 서로 인접한 것으로 취급
        NeutralDiplomacy,  // 중립외교 — 진열 칸이 N% 확률로 한 등급 오른 기물
    }

    // 같은 업그레이드를 여러 번 얻었을 때 — 값을 더한다 / 가장 높은 등급의 값 하나만(사냥꾼의 활) / 얻은 것마다 따로 건다(기름칠 — 가진 목록에 등급별로 한 줄씩)
    public enum UpgradeStack { Sum, Strongest, Each }

    // 얻은 업그레이드 한 번 — 종류와 그때의 등급(합친 재료의 등급)
    public struct UpgradeGain
    {
        public UpgradeDefinition Definition;
        public BuildingRarity Rarity;
    }

    // 한 탑이 가진 업그레이드 한 종 — 얻은 횟수 · 얻은 것 중 가장 높은 등급(아이콘 외곽선 색) · 합쳐진 값
    public struct UpgradeHolding
    {
        public UpgradeDefinition Definition;
        public int Count;
        public BuildingRarity Rarity;
        public float Value;
        public string Description => Definition.Describe(Value);
    }

    // 합치기 보상 업그레이드 한 종(아웃게임기획 §4) — 기물을 합칠 때마다 셋 중 하나를 고르고(RunState 「합치기 보상 업그레이드」), 런 동안 그 탑에 남는다.
    // 같은 업그레이드가 등급마다 수치만 다르다. 전투에 거는 곳은 Apply · Spawn(BattleManager.StartBattle), 운영(골드 · 무료 리롤)은 RunState가 값을 읽는다
    [CreateAssetMenu(menuName = "GnorpWar/Upgrade")]
    public class UpgradeDefinition : ScriptableObject, IUnitFilter, ICategoryFilter
    {
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _displayName = "";
        [Tooltip("아이콘(32×32)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private Sprite _icon;
        [Tooltip("효과 글 — {0}에 지금 값이 들어간다(표기 그대로: +N%의 N · ×N의 N · N기 · N초)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _description = "";
        [Tooltip("등급별 값 — 칸 순서: 일반 · 고급 · 희귀 · 영웅 · 전설. 글에 보이는 숫자 그대로(8 = +8% · 0.9 = ×0.9). 0 = 그 등급에는 없다(그 등급 재료를 합쳐도 후보에 안 나온다)")]
        [SerializeField] private float[] _values = new float[5];
        [SerializeField] private UpgradeStack _stack;
        [Tooltip("더한 값의 상한(연사 100) — 0 = 상한 없음")]
        [SerializeField] private float _max;
        [Tooltip("끄면 합치기 보상 후보에 나오지 않는다(조련장 — 중기병을 낳는 기물이 생길 때까지)")]
        [SerializeField] private bool _inPool = true;
        [Tooltip("국가 전용(아웃게임기획 §4 「국가 전용 업그레이드」) — 그 국가의 탑(플레이어가 고른 국가 · AI 영주는 빌드 방향의 국가)에게만 후보로 나온다. 공용 = 누구에게나")]
        [SerializeField] private BuildingNation _nation;
        [Tooltip("Interest — 얻은 것을 다 합쳐 한 번에 얻는 양의 상한(복리 — 턴당 최대 +5골드). 0 = 상한 없음")]
        [SuffixLabel("골드", true)]
        [SerializeField] private float _gainMax;

        [Header("효과")]
        [SerializeField] private UpgradeKind _kind;
        [Tooltip("UnitStat — 이 병종의 유닛에게(유닛 칸과 함께 비우면 전부). UnitEffect — 전투 시작 알림의 선이 가는 무리")]
        [SerializeField] private UnitCategory[] _categories = new UnitCategory[0];
        [Tooltip("UnitStat — 이 유닛에게(기획서가 유닛 이름을 지칭할 때 — 톱니병사 · 미노타우로스). UnitEffect — 전투 시작 알림의 선이 가는 무리")]
        [SerializeField] private UnitDefinition[] _units = new UnitDefinition[0];
        [Tooltip("UnitStat — 올릴 스탯")]
        [SerializeField] private UnitStat _unitStat;
        [Tooltip("UnitStat — 계산 방식(기획서 §6 표기). Flat = +N(톱니 갑옷) · Percent = +N%")]
        [SerializeField] private StatOp _unitOp = StatOp.Percent;
        [Tooltip("UnitStat · 공격력 — 전투 시작 글자를 「회복량」으로 쓴다(성유 — 회복 유닛은 공격력이 회복량). 계산은 그대로")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private bool _healLabel;
        [Tooltip("TowerStat — 올릴 스탯")]
        [SerializeField] private TowerStat _towerStat;
        [Tooltip("UnitEffect — 켤 효과(대상 병종은 효과 에셋에서 고른다). UnitStat도 넣으면 스탯과 함께 켠다(거인 설계도 — 크기 · 뿔 투구 — 밀려나지 않음)")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private UnitEffect _effect;
        [Tooltip("UnitEffect — 전투 시작 알림의 선 색(BuffKind)과 가는 곳")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private BuffKind _lineKind = BuffKind.Stat;
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private UpgradeLine _lineTarget;
        [Tooltip("Summon — 소환할 유닛")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private UnitDefinition _unit;
        [Tooltip("SmallTower — 세우는 자리 · 프리팹을 이 능력(조립식 감시탑)에서 빌린다")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private SmallTowerAtStartPassive _smallTower;
        [Tooltip("Special — 지역 특수 업그레이드의 종류. 값은 「등급별 값」의 첫 칸 하나만 쓴다(칸을 하나만 둔다)")]
        [SerializeField] private SpecialKind _special;
        [Tooltip("Special · CategoryAlias — 위 「병종」의 유닛을 이 병종으로도 취급한다")]
        [SerializeField] private UnitCategory _alsoAs;

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다
        private bool HideNonBalance => BalanceView.HideNonBalance;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public UpgradeKind Kind => _kind;
        public bool InPool => _inPool;
        public BuildingNation Nation => _nation;
        public BuffKind LineKind => _lineKind;
        public UpgradeLine LineTarget => _lineTarget;
        public bool HealLabel => _healLabel;
        public SpecialKind Special => _kind == UpgradeKind.Special ? _special : SpecialKind.None;
        // 타워 스탯에 거는 종류면 그 스탯(전투 시작 알림 Fx/BattleStartLines의 버프 글자)
        public TowerStat TowerStat => _towerStat;

        // 그 등급의 값 — 신화 등급 업그레이드는 없다(전설 둘을 합쳐도 전설 값)
        public float ValueAt(BuildingRarity rarity) => _values[Mathf.Min((int)rarity, _values.Length - 1)];

        // 그 등급에 이 업그레이드가 있나 — 값 칸이 0이면 없다(등급이 올라도 값이 같아지는 아래 등급을 비운다 — 사용자 결정 10/13). 합치기 후보를 뽑을 때 거른다
        public bool AvailableAt(BuildingRarity rarity) => ValueAt(rarity) != 0f;

        // 설명 글 — {0} = 지금 값(CLAUDE.md §5-2 6번). {1}부터 = 등급으로 안 변하는 에셋 값 — 복리의 상한 · 효과 에셋의 숫자(UnitEffect.UpgradeNumbers)
        public string Describe(float value)
        {
            var args = new List<object> { value.ToString("0.##") };
            if (_kind == UpgradeKind.Interest)
                args.Add(_gainMax.ToString("0.##"));
            else if (_effect != null)
                args.AddRange(_effect.UpgradeNumbers());
            return string.Format(_description, args.ToArray());
        }

        // 지역 특수 업그레이드의 병종 칸(군제 개혁의 「원래 병종」)은 대상 고르기가 아니다 — 특수 업그레이드는 유닛 전부에 건다(국가주의)
        public bool AppliesTo(UnitDefinition unit)
            => _kind == UpgradeKind.Special || (_categories.Length == 0 && _units.Length == 0)
               || System.Array.IndexOf(_categories, unit.Category) >= 0 || System.Array.IndexOf(_units, unit) >= 0;
        public bool AppliesTo(UnitCategory category) => _kind != UpgradeKind.Special && System.Array.IndexOf(_categories, category) >= 0;

        // ── 표시용 — 이 업그레이드가 무엇에 걸리는가(유닛 카드 · 기물 툴팁의 「걸린 업그레이드」 — RunState.UpgradesOn). 계산은 Apply · Spawn이 한다.
        // 새 종류(UpgradeKind · SpecialKind)를 넣으면 아래 두 판정에도 한 줄 넣는다 — 안 넣으면 어디에도 안 뜬다

        // 군제 개혁이면 unit이 함께 취급되는 병종, 아니면 null
        public UnitCategory AliasFor(UnitDefinition unit)
            => Special == SpecialKind.CategoryAlias && System.Array.IndexOf(_categories, unit.Category) >= 0 ? _alsoAs : null;

        // 그 유닛에 걸리나 — alsoAs = 그 탑에서 이 유닛이 함께 취급되는 병종들(군제 개혁). 타워 · 본진 · 운영에 거는 것은 유닛에 안 걸린다
        public bool Affects(UnitDefinition unit, List<UnitCategory> alsoAs)
        {
            switch (_kind)
            {
                case UpgradeKind.UnitStat:
                    return AppliesTo(unit) || alsoAs.Exists(category => AppliesTo(category));
                case UpgradeKind.UnitEffect:   // 본진 · 기물에 거는 효과(왕성 수비대 · 증기 압력 등 — 전투 시작 알림이 유닛에 긋지 않는 것)는 유닛에 안 걸린다
                    return _lineTarget == UpgradeLine.Units && EffectAffects(unit, alsoAs);
                case UpgradeKind.Summon:
                    return _unit == unit;   // 그 유닛을 전투 시작 시 소환한다(신병 모집)
                case UpgradeKind.Special:
                    return AliasFor(unit) != null || _special == SpecialKind.Nationalism || (_effect != null && EffectAffects(unit, alsoAs));
                default:
                    return false;
            }
        }

        private bool EffectAffects(UnitDefinition unit, List<UnitCategory> alsoAs)
            => _effect.AppliesTo(unit) || alsoAs.Exists(category => _effect.AppliesTo(category));

        // 그 기물에 걸리나 — owned · field = 탑에 놓인 기물과 그 탑의 필드. 진열 · 보관함의 기물은 field를 null로(자리 · 다른 기물과 견줘야 하는 군주제 · 군국주의는 놓인 기물만).
        // 기물이 소환하는 유닛에 걸리는 것은 넣지 않는다(유닛 카드가 보여 준다). 진열에만 거는 것(공산주의 · 중립외교)은 가격 · 등급에 이미 보여서 뺀다
        public bool Affects(BuildingDefinition building, OwnedBuilding owned, OwnedBuilding[] field)
        {
            bool cooling = building.Cooldown > 0f;
            if (_kind == UpgradeKind.BuildingCooldown || _kind == UpgradeKind.DeathmatchFire
                || (_kind == UpgradeKind.UnitEffect && _lineTarget == UpgradeLine.Floors))
                return cooling;   // 무작위 하나 — 걸릴 수 있는 기물 · 데스매치에 전부 · 쿨다운이 있는 기물을 움직이는 효과(증기 압력 · 피의 공물)
            if (_kind == UpgradeKind.TopCooldown)   // 맨 위층부터 내려가며 쿨다운이 있는 첫 기물
                return cooling && field != null && System.Array.FindLast(field, b => !OwnedBuilding.IsEmpty(b) && b.Definition.Cooldown > 0f) == owned;
            switch (Special)
            {
                case SpecialKind.Conscription:
                    return cooling && building.Unit != null;
                case SpecialKind.Enlightenment:   // 패배하면 무작위 하나 — 걸릴 수 있는 기물
                case SpecialKind.Industrial:
                    return cooling;
                case SpecialKind.Federation:
                    return System.Array.Exists(owned != null ? owned.Actions : building.Actions, action => action.UsesAdjacency)
                           || System.Array.Exists(owned != null ? owned.Passives : building.Passives, passive => passive.UsesAdjacency);
                case SpecialKind.Monarchy:
                    return field != null && System.Array.FindLast(field, b => !OwnedBuilding.IsEmpty(b)) == owned;
                case SpecialKind.Militarism:   // 쿨다운이 가장 긴 기물 — 같으면 그중 무작위라 후보 모두
                    return cooling && field != null
                           && !System.Array.Exists(field, b => !OwnedBuilding.IsEmpty(b) && b.Definition.Cooldown > 0f && b.Cooldown > owned.Cooldown);
                default:
                    return false;
            }
        }

        // 유닛 스탯에 거는 종류면 그 값 — 전투 시작(Apply)과 유닛 카드 미리 보기(RunStats.Preview)가 같이 읽는다
        public bool UnitModifier(float value, out StatModifier modifier)
        {
            modifier = new StatModifier(_unitStat, _unitOp, _unitOp == StatOp.Percent ? value / 100f : value);
            return _kind == UpgradeKind.UnitStat;
        }

        // 전투 시작 ① — 양쪽 보드가 돌기 전에: 스탯과 유닛 효과를 건다(전투 동안). count = 이 줄에 든 개수(Each만 1보다 클 수 있다)
        public void Apply(Team team, float value, int count)
        {
            if (UnitModifier(value, out StatModifier modifier))
            {
                StatBook.AddForBattle(team, this, modifier);
                if (_effect != null)
                    EnableEffect(team, value, count);
            }
            else if (_kind == UpgradeKind.TowerStat)
                TowerBook.AddForBattle(team, new TowerModifier(_towerStat, StatOp.Percent, value / 100f));
            else if (_kind == UpgradeKind.UnitEffect)
                EnableEffect(team, value, count);
            else if (_kind == UpgradeKind.BaseHp && Base.For(team) is Base home)
                home.SetHpBonus(value / 100f);
            else if (_kind == UpgradeKind.SmallTowerAttack)
                TowerBook.AddSmallAttack(team, value / 100f);
            else if (_kind == UpgradeKind.DeathmatchFire)
                BoardRunner.For(team).DeathmatchFires += Mathf.RoundToInt(value);
            else if (_kind == UpgradeKind.Special)
                ApplySpecial(team, value);
        }

        // 효과를 켜고 값을 넘긴다 — 「따로」면 얻은 것 하나하나의 값을 목록에도(효과가 UnitEffect.Values로 읽는다)
        private void EnableEffect(Team team, float value, int count)
        {
            TeamEffects.Entry entry = TeamEffects.Enable(team, _effect, 1);
            entry.Value = value;
            if (_stack == UpgradeStack.Each)
                for (int i = 0; i < count; i++)
                    entry.Values.Add(value);
            _effect.OnEnabled(team);
        }

        // 지역 특수 업그레이드 중 전투에 거는 것 — 보드가 돌기 전에(기물의 전투 시작 능력이 인접 · 병종 취급을 읽는다)
        private void ApplySpecial(Team team, float value)
        {
            BoardRunner board = BoardRunner.For(team);
            if (_effect != null)
                TeamEffects.Enable(team, _effect, 1).Value = value;
            switch (_special)
            {
                case SpecialKind.Conscription: board.ExtraSummons += Mathf.RoundToInt(value); break;
                case SpecialKind.CategoryAlias:
                    foreach (UnitCategory category in _categories)
                        UnitAliases.AddCategory(team, category, _alsoAs);
                    break;
                case SpecialKind.Monarchy: board.TopFiresTwice = true; break;
                case SpecialKind.Federation: board.AllAdjacent = true; break;
                case SpecialKind.Guillotine: board.GuillotineInterval = value; break;
                case SpecialKind.Nationalism:
                    StatBook.AddForBattle(team, this, new StatModifier(UnitStat.Attack, StatOp.Percent, value / 100f * RunState.LivesFor(team)));
                    break;
            }
        }

        // 전투 시작 ② — 양쪽 보드가 돈 뒤에: 유닛 · 작은 타워를 전장에 내고, 기물의 쿨다운을 줄인다. count = 이 줄에 든 개수(Each만 1보다 클 수 있다)
        public void Spawn(Team team, float value, int count)
        {
            if (_kind == UpgradeKind.BuildingCooldown)
                for (int i = 0; i < count; i++)
                {
                    // 본진 → 뽑힌 층(가속 색 + 「×0.97」) — 켠 기물이 없는 효과는 본진에서(사용자 10/20 Q1)
                    int slot = BoardRunner.For(team).ScaleRandomCooldown(value);
                    if (slot >= 0)
                        BuffLinks.FromBaseToSlot(team, slot, BuffKind.Haste, BuffLabel.Cooldown(value));
                }
            else if (_kind == UpgradeKind.TopCooldown)
            {
                // 맨 위층부터 내려가며 쿨다운이 있는 첫 기물(축성 — 총괄 10/21). 전투 중에 바뀌지 않는다
                BoardRunner board = BoardRunner.For(team);
                int slot = -1;
                for (int i = board.Slots.Length - 1; i >= 0 && slot < 0; i--)
                    if (board.Slots[i].Building != null && board.Slots[i].Cooldown > 0f)
                        slot = i;
                if (slot >= 0)
                    for (int i = 0; i < count; i++)
                    {
                        board.ScaleCooldown(slot, value);
                        BuffLinks.FromBaseToSlot(team, slot, BuffKind.Haste, BuffLabel.Cooldown(value));
                    }
            }
            else if (_kind == UpgradeKind.Summon)
            {
                BoardRunner board = BoardRunner.For(team);
                board.Summon(_unit, Mathf.RoundToInt(value), -1);
                // 성문 앞에 기둥 — 업그레이드로 나온 무리(선은 전투 시작 알림 Fx/BattleStartLines가 긋는다)
                if (board.LastSpawned is Unit first)
                    PillarFx.Rise(new Vector2(first.Position.x, first.Feet), first.Top - first.Feet);
            }
            else if (_kind == UpgradeKind.SmallTower)
            {
                Base tower = _smallTower.Build(team);
                if (tower != null)
                    tower.SetHpOfHome(value / 100f);
            }
            else if (Special == SpecialKind.Militarism)
            {
                int slot = BoardRunner.For(team).FireLongest();
                if (slot >= 0)
                    BuffLinks.FromBaseToSlot(team, slot, BuffKind.Trigger);   // 본진 → 즉시 발동한 층
            }
            else if (Special == SpecialKind.Industrial)
                BoardRunner.For(team).HasteAll(value);
        }

        // 얻은 목록을 종류별로 — 처음 얻은 순서. 값 = 더한 값(상한까지) 또는 가장 높은 등급의 값.
        // 얻은 것마다 따로 거는 종류(Each)는 등급별로 한 줄씩이다 — 값 = 그 등급의 값, Count = 그 등급으로 얻은 수(그 수만큼 따로 걸린다)
        public static List<UpgradeHolding> Summarize(List<UpgradeGain> gains)
        {
            var holdings = new List<UpgradeHolding>();
            foreach (UpgradeGain gain in gains)
            {
                UpgradeDefinition definition = gain.Definition;
                int at = holdings.FindIndex(h => h.Definition == definition && (definition._stack != UpgradeStack.Each || h.Rarity == gain.Rarity));
                if (at < 0)
                {
                    holdings.Add(new UpgradeHolding { Definition = definition, Count = 1, Rarity = gain.Rarity, Value = definition.ValueAt(gain.Rarity) });
                    continue;
                }
                UpgradeHolding holding = holdings[at];
                holding.Count++;
                if (definition._stack == UpgradeStack.Sum)
                    holding.Value += definition.ValueAt(gain.Rarity);
                else if (definition._stack == UpgradeStack.Strongest && gain.Rarity > holding.Rarity)
                    holding.Value = definition.ValueAt(gain.Rarity);
                if (gain.Rarity > holding.Rarity)
                    holding.Rarity = gain.Rarity;
                holdings[at] = holding;
            }
            for (int i = 0; i < holdings.Count; i++)
                if (holdings[i].Definition._max > 0f && holdings[i].Value > holdings[i].Definition._max)
                {
                    UpgradeHolding holding = holdings[i];
                    holding.Value = holding.Definition._max;
                    holdings[i] = holding;
                }
            return holdings;
        }

        // 복리 — 가진 골드로 이번 턴에 더 얻는 골드. 얻은 것마다 따로 「gold ÷ N(내림)」을 더하고, 합은 그중 가장 작은 상한까지
        public static int InterestOf(List<UpgradeGain> gains, int gold)
        {
            int total = 0;
            float cap = float.MaxValue;
            foreach (UpgradeHolding holding in Summarize(gains))
                if (holding.Definition._kind == UpgradeKind.Interest && holding.Value > 0f)
                {
                    total += holding.Count * Mathf.FloorToInt(gold / holding.Value);
                    if (holding.Definition._gainMax > 0f)
                        cap = Mathf.Min(cap, holding.Definition._gainMax);
                }
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Min(total, cap)));
        }

        // 얻은 목록에서 그 종류(TurnGold · FreeReroll)의 값을 전부 더한 것
        public static float TotalOf(List<UpgradeGain> gains, UpgradeKind kind)
        {
            float total = 0f;
            foreach (UpgradeHolding holding in Summarize(gains))
                if (holding.Definition._kind == kind)
                    total += holding.Value;
            return total;
        }
    }
}
