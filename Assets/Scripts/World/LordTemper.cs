namespace GnorpWar
{
    // 영주의 성향 — 누구를 침략하는가(기획 §6). 직렬화되는 enum — 값은 맨 뒤에만 추가할 것
    public enum LordTemper
    {
        Raider,     // 약탈자 — 이어진 AI 영주 중 탑 전투력이 가장 낮은 영주
        Ambitious,  // 야심가 — 이어진 AI 영주 중 땅이 가장 많은 영주
        Reckless,   // 무모한 자 — 이어진 AI 영주 중 무작위
        Cautious,   // 안전주의자 — 늘 쉰다(상점만 본다)
    }
}
