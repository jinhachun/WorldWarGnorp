namespace GnorpWar
{
    // AI 턴에 판도에서 벌어진 일 하나 — 지도가 차례로 재생한다(판도 자체는 이미 끝 상태다). UnityEngine을 쓰지 않는다.
    //   침략: Lord가 남의 땅 Region을 침략했다. Won이면 그 땅 하나가 Lord의 것이 된다(지면 판도는 그대로).
    //   반란: Lord가 멸망했다. Regions[i] 땅에 NewLords[i] 영주가 들어선다.
    //   연합: Members 영주들의 땅이 전부 Lord(최후의 연합)의 것이 된다.
    // 턴 시작(내 침략이 끝난 뒤)의 「지역 → 주인」 표에 순서대로 Apply하면 턴 끝의 표가 된다
    public class WorldEvent
    {
        public bool Rebellion;
        public int Lord;
        public int Region;
        public bool Won;
        public int[] Regions;
        public int[] NewLords;
        public bool Coalition;
        public int[] Members;

        public void Apply(int[] owner)
        {
            if (Coalition)
            {
                for (int region = 0; region < owner.Length; region++)
                    if (System.Array.IndexOf(Members, owner[region]) >= 0)
                        owner[region] = Lord;
            }
            else if (Rebellion)
            {
                for (int i = 0; i < Regions.Length; i++)
                    owner[Regions[i]] = NewLords[i];
            }
            else if (Won)
                owner[Region] = Lord;
        }
    }
}
