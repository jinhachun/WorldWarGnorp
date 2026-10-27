using System;

namespace GnorpWar
{
    // 이벤트 땅의 종류(아웃게임기획 §3-1 「첫 묶음 8종」 + 10/28 물레방아 · 피난민) — 데이터는 EventLandDefinition(WorldConfig), 후보 · 효과는 Run/RunState.Events.
    // 에셋은 번호로 저장하므로 새 종류는 끝에 붙인다
    public enum EventLandKind { Forge, Temple, Mercenary, Training, BloodAltar, Agitator, Shrine, Treasury, Watermill, Refugee }

    // 이벤트 땅(아웃게임기획 §3-1) — 자리는 지도에 고정(산맥 · 강 띠 — 월드 지도 굽기, Region.Event), 종류만 런마다 섞는다(WorldState가 부른다).
    // UnityEngine을 쓰지 않는다(Unity 밖 러너 · 테스트가 같은 코드를 읽는다)
    public static class EventLands
    {
        // 이벤트 땅마다의 종류 — 모든 종류를 한 곳씩 섞어 놓고, 남는 곳은 무작위(겹칠 수 있음). 이벤트 땅이 종류 수보다 적으면 섞은 앞쪽만
        public static EventLandKind[] Deal(int count, Random random)
        {
            var kinds = (EventLandKind[])Enum.GetValues(typeof(EventLandKind));
            for (int i = kinds.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
            }
            var dealt = new EventLandKind[count];
            for (int i = 0; i < count; i++)
                dealt[i] = i < kinds.Length ? kinds[i] : kinds[random.Next(kinds.Length)];
            return dealt;
        }

        // 로그 · 치트용 이름(기획서 표의 이름) — 화면에 보일 이름 · 툴팁은 EventLandDefinition
        public static string Name(EventLandKind kind)
        {
            switch (kind)
            {
                case EventLandKind.Forge: return "대장간";
                case EventLandKind.Temple: return "신전";
                case EventLandKind.Mercenary: return "용병 상인";
                case EventLandKind.Training: return "훈련장";
                case EventLandKind.BloodAltar: return "피의 제단";
                case EventLandKind.Agitator: return "선동가";
                case EventLandKind.Shrine: return "지형 성소";
                case EventLandKind.Watermill: return "물레방아";
                case EventLandKind.Refugee: return "피난민";
                default: return "보물 창고";
            }
        }
    }
}
