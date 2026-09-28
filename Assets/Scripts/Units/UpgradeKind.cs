namespace GnorpWar
{
    // 유닛 업그레이드 종류 (지금은 인게임에서 사는 테스트용 — 원래는 아웃게임 요소)
    // 직렬화되는 enum — 값은 맨 뒤에만 추가할 것
    public enum UpgradeKind
    {
        None,
        SwordEscort,          // 다른 유닛을 소환하면 확률로 Sword가 함께 소환
        BowDoubleShot,        // 한 번에 서로 다른 적 둘에게 화살
        ShieldReflectCharge,  // 돌격형에게 맞으면 공격자가 튕겨남
        KnightVaultToArchers, // 원거리 적이 앞에 있으면 다른 적을 뛰어넘어 감
        PriestDoubleHeal,     // 한 번에 다친 아군 둘을 회복
    }
}
