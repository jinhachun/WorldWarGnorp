using UnityEngine;

namespace GnorpWar
{
    // 건물이 발동할 때 하는 일 하나(생산 외). 액션마다 파일 하나 — 레벨에 따라 무엇이 커지는지는 액션이 정한다
    public abstract class BuildingAction : ScriptableObject
    {
        public abstract void Execute(BoardRunner board, int slot, int level);
        // 툴팁에서 "N초마다" 뒤에 붙는 구절 — 지금 레벨 값으로, 기호 없이, 마침표로 끝낸다 (CLAUDE.md §5-2)
        public abstract string Describe(int level);
    }
}
