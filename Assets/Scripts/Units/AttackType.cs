namespace GnorpWar
{
    // 직렬화되는 enum — 값은 맨 뒤에만 추가할 것
    public enum AttackType
    {
        Melee,
        Ranged,
        Heal,   // 적을 공격하지 않고, 다친 아군에게 회복 투사체를 던진다(프리스트)
        Flame,  // 입(무기 자리)에서 앞으로 뻗는 띠 안의 적 전부에게 계속 피해(공룡 보스)
    }
}
