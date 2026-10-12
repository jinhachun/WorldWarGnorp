using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 건축가 한 기가 작은 타워를 N개까지 짓는다 (건축설계조합 신화: 3개 — 기획서 §7-1, 사용자 10/13). 건축가마다 따로 센다.
    // 다 지으면 §7처럼 본진으로 돌아가고, 지을 자리가 없어도 돌아간다. 흐름은 Unit.BuildStep
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Builder Tower Limit")]
    public class BuilderTowerLimitEffect : UnitEffect
    {
        [SuffixLabel("개", true)]
        [SerializeField] private int _towers = 3;

        public override int BuilderTowerLimit => _towers;

        public override string Describe(int level) => string.Format(Description, _towers);
    }
}
