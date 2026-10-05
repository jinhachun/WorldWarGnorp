using UnityEngine;

namespace GnorpWar
{
    // 유닛 효과 하나 — 전투 동안 한 진영에 켜지고(TeamEffects), 적용 분류에 드는 그 진영 유닛의 훅 지점에서 불린다.
    // 효과마다 파일 하나. 이미 있는 훅만 쓰는 새 효과는 Unit을 고치지 않고 클래스·에셋만 추가하면 된다.
    // stacks = 켜 준 건물이 이번 전투에서 발동한 횟수(켜진 직후 0) — 쓰는 효과만 쓴다
    public abstract class UnitEffect : ScriptableObject, IUnitFilter
    {
        [Tooltip("이 병종 태그의 유닛에게 — 유닛 칸과 함께 비우면 전부")]
        [SerializeField] private UnitCategory[] _categories = new UnitCategory[0];
        [Tooltip("이 유닛에게(기획서가 유닛 이름을 지칭할 때) — 병종 칸과 함께 비우면 전부")]
        [SerializeField] private UnitDefinition[] _units = new UnitDefinition[0];
        [TextArea]
        [SerializeField] private string _description = "";

        public string Description => _description;

        public bool AppliesTo(UnitDefinition unit)
            => (_categories.Length == 0 && _units.Length == 0)
               || System.Array.IndexOf(_categories, unit.Category) >= 0
               || System.Array.IndexOf(_units, unit) >= 0;

        // 소환 처리 ① — 이 유닛을 count기 소환할 때 더 소환할 수(BoardRunner.Summon). sourceSlot = 소환한 기물 칸(아니면 -1)
        public virtual int ExtraSummons(UnitDefinition unit, int count, int sourceSlot, int stacks) => 0;
        // 소환 처리 ② — 대신 소환할 유닛(그대로면 unit)
        public virtual UnitDefinition ConvertSummon(UnitDefinition unit, int sourceSlot, int stacks) => unit;

        // 매 물리 스텝 — 경직 중이 아닐 때
        public virtual void Tick(Unit unit, int stacks) { }
        // 적 머리 위에 새로 내려앉은 순간
        public virtual void OnLandedOnEnemy(Unit unit, Unit enemy, int stacks) { }
        // 원거리 공격 한 발을 쏜 직후
        public virtual void OnRangedShot(Unit unit, IDamageable target, int stacks) { }
        // 맞은 직후(살아 있을 때). true면 이번 피격의 밀림·경직을 받지 않는다
        public virtual bool OnHit(Unit unit, IDamageable attacker, Vector2 hitDirection, float push, int stacks) => false;
        // 이 유닛이 적 유닛을 처치한 직후(근접·투사체·화염 모두)
        public virtual void OnKill(Unit killer, Unit victim, int stacks) { }
        // 회복 투사체 하나를 던진 직후
        public virtual void OnHealed(Unit healer, Unit patient, int stacks) { }
        // 소환 처리 ③ — 이 유닛이 소환된 직후(추가·변환이 끝난 뒤, 한 마리마다)
        public virtual void OnSummoned(Unit unit, int sourceSlot, int stacks) { }
    }
}
