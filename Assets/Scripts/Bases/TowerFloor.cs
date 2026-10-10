using UnityEngine;

namespace GnorpWar
{
    // 탑의 한 층(Tower가 만든다) — 화면 작업(끌어 놓기·툴팁)이 레이캐스트로 집는 자리. 트리거 콜라이더라 유닛·투사체와는 부딪히지 않는다
    public class TowerFloor : MonoBehaviour
    {
        public Team Team { get; private set; }
        // 0 = 1층(성 바로 위) — 필드 칸 번호와 같다
        public int Index { get; private set; }

        public void Init(Team team, int index)
        {
            Team = team;
            Index = index;
        }
    }
}
