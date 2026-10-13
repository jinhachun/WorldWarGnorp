using UnityEngine;

namespace GnorpWar
{
    // 건물이 발동할 때 하는 일 하나(생산 외). 액션마다 파일 하나 — 레벨에 따라 무엇이 커지는지는 액션이 정한다
    public abstract class BuildingAction : ScriptableObject
    {
        public abstract void Execute(BoardRunner board, int slot, int level);
        // 이번 발동에서 이 기물의 유닛 소환을 대신하나(대족장의 분노 — 이미 있으면 소환 대신 강화) — 발동마다 소환 직전에 한 번 묻는다
        public virtual bool ReplacesSummon(BoardRunner board, int slot) => false;
        // 툴팁에서 "N초마다" 뒤에 붙는 구절 — 지금 레벨 값으로, 기호 없이, 마침표로 끝낸다 (CLAUDE.md §5-2)
        public abstract string Describe(int level);
        // 칸 아래에 보이는 대표 숫자(지금 등급 값, "1초"·"10%") — 없으면 null
        public virtual string Headline(int level) => null;
        // 자리에 따라 달라지는 효과 — 이 기물이 slot층, target이 other층에 있을 때 효과가 target에 닿는가. 닿는 자리에 놓는 판단(Run/EnemyShopper)이 읽는다
        public virtual bool Reaches(int slot, int other, BuildingDefinition target) => false;
        // 전투 동안 칸이 값을 쌓는가(허수아비 등) — 쌓으면 그 계산 방식. 쌓은 값은 Execute에서 board.AddBattleGain으로
        public virtual bool BattleGain(out StatOp op)
        {
            op = default;
            return false;
        }
    }
}
