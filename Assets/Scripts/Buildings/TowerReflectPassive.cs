using UnityEngine;

namespace GnorpWar
{
    // 이 기물이 필드에 있으면 내 타워들(본진 + 작은 타워)이 받은 피해를 공격한 쪽에 그대로 돌려준다 (성채 신화 — 기획서 §7-1).
    // 돌려주는 곳은 Base.TakeDamage, 계산은 Core/DamageReflect(반사가 반사를 부르지 않는다)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Tower Reflect")]
    public class TowerReflectPassive : BuildingPassive
    {
        public override void OnBattleStart(BoardRunner board, int slot, int level) => TowerBook.ReflectDamage(board.Team);

        public override string Describe(int level) => "내 타워들이 피해를 받을 때마다, 공격한 적에게 같은 피해를 돌려줍니다.";
    }
}
