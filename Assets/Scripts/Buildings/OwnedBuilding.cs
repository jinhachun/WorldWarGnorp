using UnityEngine;

namespace GnorpWar
{
    // 가진 건물 한 채 — 건물 종류와 경험치만 담는 순수 데이터(나중에 비동기 상대 보드로 저장·전송할 단위).
    // 같은 건물을 또 사면 경험치 +1. 레벨업에 필요한 경험치는 2부터 1씩 늘어난다(2 → 3 → 4 …), 최대 레벨 없음
    [System.Serializable]
    public class OwnedBuilding
    {
        [SerializeField] private BuildingDefinition _definition;
        [SerializeField] private int _exp;

        public OwnedBuilding(BuildingDefinition definition)
        {
            _definition = definition;
        }

        public BuildingDefinition Definition => _definition;
        public int Exp => _exp;
        public int Level => LevelFor(_exp);

        public void AddExp() => _exp++;

        public static bool IsEmpty(OwnedBuilding building) => building == null || building._definition == null;

        public static int LevelFor(int exp)
        {
            int level = 1;
            for (int need = 2; exp >= need; need++)
            {
                exp -= need;
                level++;
            }
            return level;
        }

        // 다음 레벨까지 (지금 칸에서 쌓은 경험치, 그 칸의 필요 경험치)
        public (int have, int need) Progress()
        {
            int exp = _exp;
            int need = 2;
            for (; exp >= need; need++)
                exp -= need;
            return (exp, need);
        }
    }
}
