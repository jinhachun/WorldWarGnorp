namespace GnorpWar
{
    // 승천의 전당 신화 — 승천의 전당이 발동할 때 아군이 체력을 잃지 않는다.
    // 훅이 없는 표시용 효과다: 체력을 잃는 자리가 AscensionEffect 한 곳이라 거기서 이 효과가 켜져 있는지 본다.
    // §7(전설까지)의 동작이 함께 바뀌지 않게 AscensionEffect를 복제하지 않고 이렇게 가른다
    [UnityEngine.CreateAssetMenu(menuName = "GnorpWar/Effects/Ascension Keep Hp")]
    public class AscensionKeepHpEffect : UnitEffect
    {
    }
}
