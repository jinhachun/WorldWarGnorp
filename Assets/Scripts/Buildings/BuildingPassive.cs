using UnityEngine;

namespace GnorpWar
{
    // 기물의 늘 켜진 능력 하나(쿨다운 발동이 아닌 것) — 능력마다 파일 하나, 쓰는 훅만 덮어쓴다.
    // slot = 이 기물의 칸(이웃은 slot±1). 진영 단위로 유닛에게 거는 효과는 UnitEffect
    public abstract class BuildingPassive : ScriptableObject
    {
        // 전투 시작 — 효과가 켜진 뒤
        public virtual void OnBattleStart(BoardRunner board, int slot, int level) { }
        // 런 동안 쌓은 것을 전투 시작 때 유닛 스탯으로 거는 능력(스패너) — 지금 쌓인 만큼 stat에 걸 값. 대상은 이 능력의 IUnitFilter.
        // OnBattleStart가 이 값으로 걸고, 유닛 카드 미리 보기(RunStats.Preview)도 이 값을 읽는다
        public virtual bool BattleStartStat(OwnedBuilding self, UnitStat stat, out StatModifier modifier)
        {
            modifier = default;
            return false;
        }
        // 전투 시작 때 거는 별칭(조립 라인) — unit이 효과를 함께 받는 유닛. 없으면 null
        public virtual UnitDefinition AliasOf(UnitDefinition unit) => null;
        // 다른 칸의 기물이 유닛을 소환한 직후(한 마리마다, 소환 이벤트 뒤) — sourceSlot = 소환한 칸. 어느 칸을 볼지(바로 위·위 전부)는 능력이 정한다
        public virtual void OnSummonedFrom(BoardRunner board, int slot, int sourceSlot, Unit unit) { }
        // 내 타워(본진)의 화살이 적 유닛을 처치한 직후
        public virtual void OnTowerKill(BoardRunner board, int slot, Unit victim) { }
        // 유닛이 죽은 직후 — 어느 진영의 유닛이든(약탈족장 라이칸스로프)
        public virtual void OnUnitDied(BoardRunner board, int slot, Unit victim) { }
        // 데스매치 직전(데스매치 몇 초 전 — BattleConfig) 한 번 — 맘모스 소환의식
        public virtual void OnBeforeDeathmatch(BoardRunner board, int slot, int level) { }
        // 내 작은 타워가 파괴된 직후(사라지기 전) — 어느 타워를 볼지(이 기물이 세운 것 · 전부)는 능력이 정한다(Base.BuiltBy)
        public virtual void OnSmallTowerDestroyed(BoardRunner board, int slot, Base tower) { }

        // 「구매할 때마다,」 — 이 기물을 샀을 때(합쳐졌으면 합쳐진 결과에서) · 영악한 투자가가 다시 발동시킬 때. team = 효과를 받을 진영
        public virtual void OnBuy(OwnedBuilding self, Team team) { }
        // 「구매할 때마다,」 효과가 있나 — 이동식 공장·영악한 투자가가 읽는다
        public virtual bool HasBuyEffect => false;
        // 상점 — 내가 기물을 산 직후(이동식 공장) · 리롤 · 골드를 얻을 때. 필드의 기물만(RunState)
        public virtual void OnAnyBought(OwnedBuilding self, OwnedBuilding bought) { }
        public virtual void OnReroll(OwnedBuilding self) { }
        // 「판매 시,」 — 이 기물을 판 직후(칸은 이미 비었다). floor = 판 층(필드 — 위층이 이미 내려와 그 층에 바로 위 기물이 있다), 보관함에서 팔았으면 -1
        public virtual void OnSell(OwnedBuilding self, int floor) { }
        // 판매가에 더하는 골드(동전 주조기 — 이 한 채에 쌓인 몫). RunState.SellValue가 더한다
        public virtual int SellBonus(OwnedBuilding self) => 0;
        // 턴이 열릴 때(수입을 받는 때) — 내 턴(RunState.BeginTurn)도 AI 영주의 턴 상점(RunState.ShopAs)도. 필드의 기물만(전쟁 금고)
        public virtual void OnTurnStart(OwnedBuilding self) { }
        public virtual void OnGoldGained(OwnedBuilding self) { }
        // 「전투에서 승리할 때마다,」 — 이 기물이 필드에 있는 탑이 실제 전투에서 이긴 직후(RunState.FinishBattle). 그동안 RunState의 상점 규칙은 이 탑에 걸려 있다
        public virtual void OnBattleWon(OwnedBuilding self) { }

        // 자리에 따라 달라지는 효과 — 이 기물이 slot층, target이 other층에 있을 때 효과가 target에 닿는가. 닿는 자리에 놓는 판단(Run/EnemyShopper)이 읽는다
        public virtual bool Reaches(int slot, int other, BuildingDefinition target) => false;
        // 자리 효과(바로 위 · 아래 · 위에 있는 · 아래에 있는 — BoardRunner.Linked)를 쓰는가 — 연방제가 걸리는 기물인지 가린다(UpgradeDefinition.Affects)
        public virtual bool UsesAdjacency => false;
        // 툴팁 대상 표시(BuildingTargets) — field의 slot층 이 기물이 other층 기물에 효과를 주나. 기본은 자리 규칙(Reaches), 연방제면 자리를 안 본다(위 · 아래 어느 쪽이든 닿으면)
        public virtual bool TargetsFloor(OwnedBuilding[] field, int slot, int other, bool federated)
        {
            BuildingDefinition target = field[other].Definition;
            return Reaches(slot, other, target) || (federated && UsesAdjacency && (Reaches(slot, slot + 1, target) || Reaches(slot, slot - 1, target)));
        }

        // 툴팁의 완결 문장 — 지금 등급 값으로 (CLAUDE.md §5-2)
        public abstract string Describe(int level);
        // 신화 값이 전설과 다른 능력(제물의 의식 — 사용자 10/27)만 덮어쓴다. 툴팁(UI/BuildingTooltip.Lines)이 이쪽을 부른다
        public virtual string Describe(int level, bool mythic) => Describe(level);
        // 칸 아래에 보이는 대표 숫자(지금 등급 값) — 없으면 null
        public virtual string Headline(int level) => null;
        // 전투 동안 칸이 값을 쌓는가(처치 발동 등) — 쌓으면 그 계산 방식. 쌓은 값은 board.AddBattleGain으로
        public virtual bool BattleGain(out StatOp op)
        {
            op = default;
            return false;
        }

        public static bool HasBuyEffects(BuildingDefinition building)
        {
            foreach (BuildingPassive passive in building.Passives)
                if (passive.HasBuyEffect)
                    return true;
            return false;
        }

        // 한 기물의 「구매할 때마다,」 효과를 repeats번
        public static void TriggerBuyEffects(OwnedBuilding owned, Team team, int repeats)
        {
            if (HasBuyEffects(owned.Definition))
                OwnedBuilding.NotifyEffect(owned);
            for (int i = 0; i < repeats; i++)
                foreach (BuildingPassive passive in owned.Passives)
                    passive.OnBuy(owned, team);
        }
    }
}
