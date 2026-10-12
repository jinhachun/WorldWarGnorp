using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 N초마다 유닛을 소환한다 — 자기 바로 뒤에 (철인공장 콜로서스: 톱니거인이 톱니병사를 — 간격은 에셋)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Periodic Summon")]
    public class PeriodicSummonEffect : UnitEffect
    {
        [SuffixLabel("초", true)]
        [SerializeField] private float _interval = 3f;
        [SerializeField] private UnitDefinition _summon;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;
        [Tooltip("소환 자리 — 내 뒤로 이만큼")]
        [SuffixLabel("칸", true)]
        [SerializeField] private float _behind = 1.5f;

        public override void Tick(Unit unit, int stacks)
        {
            if (!unit.EffectReady(this, _interval))
                return;
            unit.SetEffectCooldown(this, _interval);
            float x = unit.Position.x - unit.Forward * _behind;
            BoardRunner.For(unit.Team).Summon(_summon, _count, -1, new Vector2(x, Ground.Instance.HeightAt(x)), Source(unit.Team));
        }

        // 설명의 {0} = 간격(초) · {1} = 소환 수 — 글의 숫자가 에셋 값에서 나오게
        public override string Describe(int level) => string.Format(Description, $"{_interval:0.#}", _count);
    }
}
