using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 유닛 효과 하나 — 전투 동안 한 진영에 켜지고(TeamEffects), 적용 분류에 드는 그 진영 유닛의 훅 지점에서 불린다.
    // 효과마다 파일 하나. 이미 있는 훅만 쓰는 새 효과는 Unit을 고치지 않고 클래스·에셋만 추가하면 된다.
    // stacks = 켜 준 건물이 이번 전투에서 발동한 횟수(켜진 직후 0) — 쓰는 효과만 쓴다
    public abstract class UnitEffect : ScriptableObject, IUnitFilter, ICategoryFilter
    {
        [Tooltip("이 병종 태그의 유닛에게 — 유닛 칸과 함께 비우면 전부")]
        [SerializeField] private UnitCategory[] _categories = new UnitCategory[0];
        [Tooltip("이 유닛에게(기획서가 유닛 이름을 지칭할 때) — 병종 칸과 함께 비우면 전부")]
        [SerializeField] private UnitDefinition[] _units = new UnitDefinition[0];
        [TextArea]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _description = "";
        [Tooltip("유닛 카드의 특성 줄 — 이 효과가 한 유닛의 타고난 능력일 때(UnitDefinition의 「타고난 효과」) 그 유닛 카드에 쓰는 글. 주어 없이, {0}에 이 효과의 숫자")]
        [TextArea]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _trait = "";

        // 밸런싱 창(Editor/BalanceWindow)의 「밸런스와 무관한 값 숨기기」 — 그 창이 그리는 동안만 켜진다. 일반 인스펙터에서는 늘 전부 보인다(하위 효과의 표시용 글 칸도 읽는다)
        protected bool HideNonBalance => BalanceView.HideNonBalance;

        public string Description => _description;
        // 툴팁 글 — 등급으로 크는 효과는 설명의 {0}에 지금 등급 값을 넣는다
        public virtual string Describe(int level) => _description;
        // 유닛 카드의 특성 줄(없으면 빈 글) — 숫자는 효과가 제 값에서 넣는다
        public string Trait => string.Format(_trait, TraitNumbers());
        protected virtual object[] TraitNumbers() => new object[0];
        // 칸 아래에 보이는 대표 숫자(지금 등급 값) — 없으면 null
        public virtual string Headline(int level) => null;
        // 이 효과를 켠 기물의 등급(같은 효과를 여러 기물이 켜면 가장 높은 것) — 훅 안에서 수치를 읽을 때
        protected int Level(Team team) => TeamEffects.LevelOf(team, this);
        // 이 효과를 켠 기물 — 이 효과가 유닛을 소환할 때 출처로 넘기고(BoardRunner.Summon), 「이 기물이 소환한 ~」을 가릴 때 Unit.SummonedBy와 견준다
        protected BuildingDefinition Source(Team team) => TeamEffects.SourceOf(team, this);
        // 이 효과를 켠 합치기 보상 업그레이드의 합쳐진 값(글에 보이는 숫자 그대로 — 10 = 10%) — 업그레이드용 효과가 읽는다
        protected float Value(Team team) => TeamEffects.ValueOf(team, this);

        public bool AppliesTo(UnitDefinition unit)
            => (_categories.Length == 0 && _units.Length == 0)
               || System.Array.IndexOf(_categories, unit.Category) >= 0
               || System.Array.IndexOf(_units, unit) >= 0;

        public bool AppliesTo(UnitCategory category) => System.Array.IndexOf(_categories, category) >= 0;

        // 소환 처리 ① — 이 유닛을 count기 소환할 때 더 소환할 수(BoardRunner.Summon). sourceSlot = 소환한 기물 칸(아니면 -1)
        public virtual int ExtraSummons(UnitDefinition unit, int count, int sourceSlot, int stacks, int level) => 0;
        // 소환 처리 ② — 대신 소환할 유닛(그대로면 unit)
        public virtual UnitDefinition ConvertSummon(UnitDefinition unit, int sourceSlot, int stacks) => unit;
        // 이 효과가 바꿔서 소환한 유닛이 나온 직후(한 마리마다, 소환 이벤트 앞)
        public virtual void OnConverted(Unit unit) { }

        // 매 물리 스텝 — 경직 중이 아닐 때
        public virtual void Tick(Unit unit, int stacks) { }
        // 맞은 직후(살아 있을 때). true면 이번 피격의 밀림·경직을 받지 않는다
        public virtual bool OnHit(Unit unit, IDamageable attacker, Vector2 hitDirection, float push, int stacks) => false;
        // 이 유닛이 적 유닛을 처치한 직후(근접·투사체·화염 모두)
        public virtual void OnKill(Unit killer, Unit victim, int stacks) { }
        // 이 유닛이 회복받은 직후 — 실제로 찬 양이 있을 때만(순례지)
        public virtual void OnHealReceived(Unit unit, float amount, int stacks) { }
        // 이 유닛의 크기가 실제로 커진 직후(Unit.Grow — 상한에 막혀 그대로면 안 불린다)
        public virtual void OnGrew(Unit unit, int stacks) { }
        // 이 유닛이 죽은 직후(처치 효과 뒤) — at = 죽은 자리
        public virtual void OnDied(Unit unit, Vector2 at, int stacks) { }
        // 적 유닛이 죽은 직후(위 사망 효과 뒤) — team = 이 효과를 켠 진영, at = 죽은 자리. 적용 분류와 상관없이 불린다(대상은 적이다 — 가릴 것은 효과가 가린다)
        public virtual void OnEnemyDied(Team team, Unit victim, Vector2 at, int stacks) { }
        // 이 유닛이 공격한 직후(근접·사격·화염·휩쓸기 — 주 대상 하나)
        public virtual void OnAttack(Unit attacker, IDamageable target, int stacks) { }
        // 사거리 안에 이 병종의 적이 있으면 그쪽을 먼저 노린다(꼭두각시 왕)
        public virtual UnitCategory PreferredTargetCategory => null;
        // 무적 상태인 동안 공격력 +비율(합연산, 공주 루니카)
        public virtual float InvulnerableAttackBonus => 0f;
        // 이 유닛이 받는 무적의 지속시간에 더하는 초(충정의 맹세 신화)
        public virtual float InvulnerableBonusSeconds => 0f;
        // 무적으로 막은 피해를 공격한 쪽에 그대로 돌려주는가(공주 루니카 신화) — attacker = 때린 쪽
        public virtual bool ReflectsBlockedDamage(IDamageable attacker) => false;
        // 처음 죽을 때 대신 이 시간만큼 무적, 그 뒤 쓰러진다(충정의 맹세). 0이면 없음
        public virtual float FirstDeathDelay => 0f;
        // 관통 — 적용 대상 유닛의 화살 · 이 진영 타워의 화살이 적을 맞혀도 멈추지 않는다(관통탄 특허국)
        public virtual bool ProjectilesPierce => false;
        public virtual bool TowerArrowsPierce => false;
        // 관통한 화살이 적을 맞힐 때마다 그 화살의 피해에 곱하는 값(관통탄 특허국 신화) — 유닛의 화살은 적용 분류로 거르고, 타워의 화살은 거르지 않고 읽는다
        public virtual float PierceHitDamageFactor => 1f;
        // 적용 대상 유닛이 공격 한 번에 더 쏘는 투사체 수(궁술수련관 신화) — 짧은 간격으로 잇달아 나가고 발마다 다른 적을 노린다(Unit.Burst). 아래는 그 발 사이 간격(초)
        public virtual int ExtraProjectiles => 0;
        public virtual float ExtraProjectileInterval => 0f;
        // 적용 대상 건축가 한 기가 짓는 작은 타워 수(건축설계조합 신화 3) — 기본 1
        public virtual int BuilderTowerLimit => 1;
        // 높은 고도에서 준 피해가 적 유닛에 들어간 직후(기름투하대 신화) — 직접 타격은 때린 유닛의 적용 분류로 거르고, 화살(유닛 · 타워)은 거르지 않는다. damage = 그 피해
        public virtual void OnHighGroundHit(Unit victim, float damage, IDamageable attacker) { }
        // 상대보다 높은 고도에서 주는 피해 +비율(기름투하대) — 타워는 늘 높은 고도
        public virtual float HighGroundDamageBonus(int level) => 0f;
        // 지금 공격한 적보다 높은 고도였으면 그 공격 뒤의 간격에 얹는 공격속도 +비율(합연산, 고지 점령)
        public virtual float HighGroundAttackSpeedBonus(Team team) => 0f;
        // 받은 피해 중 공격한 쪽에 돌려주는 비율(가시 갑옷) — 죽는 타격도 돌려준다
        public virtual float DamageReflectRatio(Team team) => 0f;
        // 이 생애의 첫 공격에 얹는 피해 +비율(돌격 나팔)
        public virtual float FirstAttackDamageBonus(Team team) => 0f;
        // 적용 대상 유닛이 쏜 화살이 적 유닛에 피해를 준 직후(사냥꾼의 활) — team = 쏜 쪽. 타워의 화살은 안 불린다
        public virtual void OnArrowHit(Team team, Unit victim) { }
        // 고도 1(1칸)마다 그 스탯에 얹는 +비율(합연산, 미노타우로스 — 0.1 = 고도 1당 +10%) — 지금 고도로 센다(Unit.Altitude)
        public virtual float AltitudeBonus(UnitStat stat) => 0f;
        // 이 유닛이 유닛에게 피해를 준 직후(근접 · 화살 · 범위 · 반사 모두, 아군에게 준 것도) — amount = 그 피해(원시 의식 제단)
        public virtual void OnDealtDamage(Unit dealer, Unit victim, float amount) { }
        // 죽을 때마다 그 자리에서 다시 일어날 확률(0~1, 죽지 않는 전사) — 여러 효과면 더한다. 사망 시 효과 · 처치 효과는 먼저 돈다
        public virtual float ReviveChance => 0f;
        // 이 유닛이 죽었다가 그 자리에서 다시 일어난 직후(죽지 않는 전사 신화)
        public virtual void OnRevived(Unit unit) { }
        // 이 진영의 유닛이 죽으며 남기는 뼛가루의 양에 곱하는 값(뼈무덤 신화 2 — 적용 분류와 상관없이 읽는다)
        public virtual float BoneDustFactor => 1f;
        // 처음 죽을 때 죽는 대신 최대 체력의 이 비율을 회복한다(신정 — 1 = 100%). 0이면 없음. 충정의 맹세(FirstDeathDelay)보다 먼저 쓰인다
        public virtual float FirstDeathHealRatio(Team team) => 0f;
        // 회복 투사체 하나를 던진 직후
        public virtual void OnHealed(Unit healer, Unit patient, int stacks) { }
        // 회복 투사체가 아군에 닿아 실제로 체력이 찬 직후(구슬마다, 빗나간 구슬은 안 불린다) — team = 던진 쪽. 적용 분류는 던진 유닛 기준.
        // overflow = 최대 체력을 넘어 버려진 회복량(성수 우물 신화) — amount + overflow가 던진 회복량이다
        public virtual void OnHealLanded(Team team, Unit patient, float amount, float overflow, int stacks) { }
        // 소환 처리 ③ — 이 유닛이 소환된 직후(추가·변환이 끝난 뒤, 한 마리마다)
        public virtual void OnSummoned(Unit unit, int sourceSlot, int stacks) { }
    }
}
