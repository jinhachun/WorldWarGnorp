using UnityEngine;

namespace GnorpWar
{
    // 내 본진이 피해를 받을 때마다 N% 확률로 그 피해를 받지 않는다 (국가 전용 업그레이드 「수호 기도」 — 킹덤) — N은 업그레이드의 합쳐진 값(상한은 업그레이드 에셋 — 50).
    // 판정 · 막은 연출(본진 둘레 무적 색 고리)은 Base.TakeDamage. 작은 타워는 아니다
    [CreateAssetMenu(menuName = "GnorpWar/Effects/Upgrade Base Block")]
    public class UpgradeBaseBlockEffect : UnitEffect
    {
        public override float BaseBlockChance(Team team) => Value(team) / 100f;
    }
}
