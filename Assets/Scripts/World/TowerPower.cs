using UnityEngine;

namespace GnorpWar
{
    // 탑 전투력 — 전투 없이 탑(기물 줄)만 보고 내는 힘의 어림(기획 §6). AI 영주끼리의 승패 주사위(WorldTurn.WinChance)가 읽는다.
    //   생산 항 = 쿨다운마다 유닛을 내는 기물마다 (유닛 전투력 × 한 번에 소환하는 수 ÷ 그 등급의 쿨다운)
    //   그 밖의 기물 = 쓴 골드(구매가 × 합쳐진 수) × WorldConfig.PowerPerGold
    // 유닛 전투력은 전장 표시(Battle/CombatPower)와 같은 식을 유닛의 기본 수치에 쓴다.
    // 안 잡히는 것: 조건으로 소환하는 기물의 유닛 · 유닛 강화(효과 · 영구 강화 · 스택) · 자리 효과 · 맵 · 지역 업그레이드 — 실제 전투와 얼마나 맞는지는 재지 않았다
    public static class TowerPower
    {
        public static float Of(OwnedBuilding[] field, BattleConfig battle, WorldConfig world)
            => Production(field, battle) + SupportGold(field, battle) * world.PowerPerGold;

        public static float Production(OwnedBuilding[] field, BattleConfig battle)
        {
            float power = 0f;
            foreach (OwnedBuilding owned in field)
                if (IsProducer(owned))
                {
                    BuildingDefinition definition = owned.Definition;
                    power += UnitPower(definition.Unit, battle) * definition.UnitCount / (definition.Cooldown * battle.RankCooldownScale(owned.CooldownLevel));
                }
            return power;
        }

        // 생산 항에 안 잡히는 기물에 쓴 골드
        public static int SupportGold(OwnedBuilding[] field, BattleConfig battle)
        {
            int gold = 0;
            foreach (OwnedBuilding owned in field)
                if (!OwnedBuilding.IsEmpty(owned) && !IsProducer(owned))
                    gold += battle.PriceOf(owned.Definition.Rarity) * owned.Copies;
            return gold;
        }

        private static bool IsProducer(OwnedBuilding owned)
            => !OwnedBuilding.IsEmpty(owned) && owned.Definition.Unit != null && owned.Definition.Cooldown > 0f;

        // √(체력 × 초당 피해) × 표시 배율 — 범위 공격은 범위 배율, 회복 유닛은 초당 회복량 × 회복 계수(CombatPower.Of와 같은 갈래)
        public static float UnitPower(UnitDefinition unit, BattleConfig battle)
        {
            float perSecond;
            if (unit.AttackType == AttackType.Heal)
                perSecond = unit.HealAmount / unit.AttackInterval * battle.PowerHealScale;
            else
            {
                perSecond = unit.AttackDamage / unit.AttackInterval;
                if (unit.AttackType == AttackType.Flame || unit.AttackType == AttackType.Sweep)
                    perSecond *= battle.PowerAreaScale;
            }
            return Mathf.Sqrt(unit.MaxHp * Mathf.Max(0f, perSecond)) * battle.PowerDisplayScale;
        }
    }
}
