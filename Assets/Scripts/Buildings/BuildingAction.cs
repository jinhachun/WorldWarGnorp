using UnityEngine;

namespace GnorpWar
{
    // 건물이 발동할 때 하는 일 하나(생산 외). 액션마다 파일 하나 — 레벨에 따라 무엇이 커지는지는 액션이 정한다
    public abstract class BuildingAction : ScriptableObject
    {
        public abstract void Execute(BoardRunner board, int slot, int level);
        // 툴팁에서 "N초마다" 뒤에 붙는 구절 — 지금 레벨 값으로, 기호 없이, 마침표로 끝낸다 (CLAUDE.md §5-2)
        public abstract string Describe(int level);
        // 칸 아래에 보이는 대표 숫자(지금 등급 값, "1초"·"10%") — 없으면 null
        public virtual string Headline(int level) => null;
        // 전투 동안 칸이 값을 쌓는가(허수아비 등) — 쌓으면 그 계산 방식. 쌓은 값은 Execute에서 board.AddBattleGain으로
        public virtual bool BattleGain(out StatOp op)
        {
            op = default;
            return false;
        }
    }
}
