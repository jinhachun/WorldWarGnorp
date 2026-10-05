using UnityEngine;

namespace GnorpWar
{
    // 적용 분류의 유닛이 생산되면 확률로 호위 유닛이 함께 나온다 (호위 유닛 자신이 생산될 땐 제외)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Escort")]
    public class EscortEffect : UnitEffect
    {
        [SerializeField] private UnitDefinition _escort;
        [Range(0f, 1f)]
        [SerializeField] private float _chance = 0.25f;

        public override void OnProduced(Unit unit, int stacks)
        {
            if (unit.Definition == _escort || Random.value >= _chance)
                return;
            Unit.Spawn(_escort.PrefabFor(unit.Team), unit.transform.position);
        }
    }
}
