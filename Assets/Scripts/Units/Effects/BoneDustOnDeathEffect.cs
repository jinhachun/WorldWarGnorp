using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 적용 대상 유닛이 죽으면 그 자리에 뼛가루가 쏟아져 뼛가루 언덕이 쌓인다 (원시 졸병 — 트라이브 명세 §2-2 · §3).
    // 쌓이고 흘러내리는 계산과 충돌은 Battle/Ground(「뼛가루 언덕」)
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Bone Dust On Death")]
    public class BoneDustOnDeathEffect : UnitEffect
    {
        [Tooltip("한 번에 쏟아지는 뼛가루의 양 — 옆에서 본 넓이(1 = 가로 1칸 × 세로 1칸)")]
        [SuffixLabel("칸²", true)]
        [SerializeField] private float _area = 0.25f;
        [Tooltip("더미가 흘러내리는 기울기 상한(세로 ÷ 가로) — 이보다 가파르면 옆으로 흐른다. 맵의 언덕 기울기 상한(0.4~0.6)보다 크면 유닛이 못 오른다")]
        [SerializeField] private float _maxSlope = 0.5f;
        [Tooltip("다 쏟아지는 데 걸리는 시간 — 더미가 이 시간에 걸쳐 자란다")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _pourSeconds = 0.5f;

        // 죽음으로 남기는 가루에는 그 진영에 켜진 배율(뼈무덤 신화 — 2배)이 곱해진다
        public override void OnDied(Unit unit, Vector2 at, int stacks)
        {
            float factor = 1f;
            foreach (TeamEffects.Entry e in TeamEffects.For(unit.Team))
                factor *= e.Effect.BoneDustFactor(unit.Team);
            Pour(at.x, factor);
        }

        // x 자리에 이 효과의 1기분 × factor만큼 쏟는다 — 죽음 말고 다른 계기로 남기는 가루(대족장의 분노 신화)도 이것을 부른다
        public void Pour(float x, float factor = 1f)
        {
            if (Ground.Instance != null)
                Ground.Instance.AddBoneDust(x, _area * factor, _maxSlope, _pourSeconds);
        }
    }
}
