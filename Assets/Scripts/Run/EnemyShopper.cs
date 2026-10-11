using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GnorpWar
{
    // 적의 상점 한 라운드 — 플레이어가 누르는 것과 같은 RunState 메서드(구매·판매·리롤·층 올리기·잠금·자리 바꾸기)만 부른다.
    // RunState.EnemyShop이 적 차례로 바꿔 둔 동안 불리므로 여기서 읽는 RunState.Gold·Field·Offers는 전부 적의 것이다.
    // 판단은 진열 기물에 값을 매겨 가장 높은 것부터 사는 것뿐 — 값은 빌드 방향(EnemyBuildProfile)에서 온다
    public static class EnemyShopper
    {
        // 한 라운드에 하는 행동 수 상한 — 판단이 꼬여도 끝난다
        private const int MaxActions = 60;
        // 싸우는 유닛을 내는 기물이 모자랄 때 생산 기물에 얹는 값 — 무엇보다 먼저 사게(탑에 유닛이 없으면 전투가 안 된다)
        private const float Urgent = 100f;

        // 상점 한 번마다 남기는 로그를 끈다 — 월드맵 시뮬레이션이 영주 수십 명을 수백 판 돌릴 때(Editor/WorldSimMenu)
        public static bool Quiet;

        // 이번 라운드 상점에 들어올 때 탑에 있던 기물 — 방금 산 기물을 같은 라운드에 되팔지 않게
        private static readonly List<OwnedBuilding> Held = new List<OwnedBuilding>();

        public static void Shop(EnemyBuildProfile build)
        {
            Held.Clear();
            Held.AddRange(RunState.Field);
            var log = new StringBuilder();
            int goldAtStart = RunState.Gold, rerolls = 0;
            bool saving = false;
            for (int action = 0; action < MaxActions; action++)
            {
                int offer = BestOffer(build, out saving);
                if (offer >= 0)
                {
                    BuildingDefinition definition = RunState.Offers[offer];
                    if (NeedsRoom(definition))
                    {
                        MakeRoom(build, definition, log);
                        if (NeedsRoom(definition))
                            continue;
                    }
                    log.Append($" 구매 {definition.DisplayName}({RunState.PriceOf(definition)})");
                    RunState.Buy(offer);
                }
                else if (saving)
                    break;   // 원하는 기물이 있는데 골드가 모자란다 — 아껴 둔다
                else if (build.RaisesFloorAhead && FirstEmpty() < 0 && RunState.Floors < RunState.MaxFloors)
                {
                    // 층 올릴 골드가 모자라면 리롤에 쓰지 않고 다음 라운드로 모은다
                    if (!RunState.CanRaiseFloor)
                        break;
                    log.Append($" 층 올리기({RunState.NextFloorCost})");
                    RunState.RaiseFloor();
                }
                // 싸우는 유닛을 내는 기물이 하나도 없으면 나올 때까지 돌린다
                else if (RunState.CanReroll && (Fighters(out _) == 0 || RunState.Gold - RunState.RerollCost >= build.RerollReserve))
                {
                    rerolls++;
                    RunState.Reroll();
                }
                else
                    break;
            }
            // 아껴 둔 기물이 다음 라운드에도 진열에 남게 잠근다
            if (RunState.ShopLocked != saving)
                RunState.ToggleShopLock();
            Arrange();

            log.Append($" 리롤 {rerolls}회 · 탑 {RunState.Floors}층:");
            foreach (OwnedBuilding owned in RunState.Field)
                if (!OwnedBuilding.IsEmpty(owned))
                    log.Append($" {owned.Definition.DisplayName}[{owned.Rarity.DisplayName()}]");
            if (!Quiet)
                Debug.Log($"[Enemy] 라운드 {RunState.Round + 1} · {build.DisplayName} · 골드 {goldAtStart} → {RunState.Gold} ·{log}");
        }

        // 지금 살 수 있는 진열 중 값이 가장 높은 칸(없으면 -1). saving = 원하는 기물(빌드 목록·가진 기물)이 있는데 골드가 모자라다
        private static int BestOffer(EnemyBuildProfile build, out bool saving)
        {
            saving = false;
            int best = -1;
            float bestValue = 0f;
            for (int i = 0; i < RunState.Offers.Length; i++)
            {
                BuildingDefinition definition = RunState.Offers[i];
                if (definition == null)
                    continue;
                float value = OfferValue(build, definition, out bool urgent);
                if (value <= 0f)
                    continue;
                int cost = RunState.PriceOf(definition);
                if (NeedsRoom(definition))
                {
                    if (CanRaiseFor(cost))
                        cost += RunState.NextFloorCost;
                    else
                    {
                        // 최고층이거나 층 올릴 골드가 안 된다 — 가진 것 중 값이 가장 낮은 기물보다 나아야 그것을 팔고 산다
                        int lowest = Lowest(build);
                        if (lowest < 0 || value <= OwnedValue(build, RunState.Field[lowest]))
                            continue;
                        cost -= RunState.SellValue(RunState.Field[lowest]);
                    }
                }
                if (urgent)
                    value += Urgent;
                if (RunState.Gold < cost)
                {
                    saving |= build.WeightOf(definition) > 0f || Owns(definition);
                    continue;
                }
                if (value > bestValue)
                {
                    best = i;
                    bestValue = value;
                }
            }
            return best;
        }

        // 진열에 나온 기물의 값 — ① 싸우는 유닛을 내는 기물이 모자라면 생산 기물이 먼저 ② 가진 기물(합쳐서 등급업)은 값 × 배율 ③ 그 밖에는 빌드 방향의 값
        // urgent = 생산 기물이 모자라 먼저 사야 한다. 싸우는 유닛을 내는 기물이 하나도 없으면 그것 말고는 사지 않는다
        private static float OfferValue(EnemyBuildProfile build, BuildingDefinition definition, out bool urgent)
        {
            float value = BaseValue(build, definition);
            bool owned = Owns(definition);
            int fighters = Fighters(out int filled);
            urgent = IsFighter(definition) && fighters < build.ProducerShare * (filled + 1);
            if (IsFighter(definition) ? !urgent && !owned && build.WeightOf(definition) <= 0f : fighters == 0)
                return 0f;
            return owned ? value * build.OwnedScale : value;
        }

        // 빌드 목록의 값 → 목록에 없으면 생산 기물 값 → 공용 기물 값 → 다른 세력의 기물은 0
        private static float BaseValue(EnemyBuildProfile build, BuildingDefinition definition)
        {
            float weight = build.WeightOf(definition);
            if (weight > 0f)
                return weight;
            if (IsFighter(definition))
                return build.ProducerValue;
            return definition.Nation == BuildingNation.Common ? build.CommonValue : 0f;
        }

        // 가진 기물의 값 — 합쳐진 수만큼 귀하다
        private static float OwnedValue(EnemyBuildProfile build, OwnedBuilding owned) => BaseValue(build, owned.Definition) * owned.Copies;

        // 싸우는 유닛을 쿨다운마다 내는 기물인가 — 회복 전용·공격력 0(사제·건축가)만 내는 탑은 성을 못 부순다
        private static bool IsFighter(BuildingDefinition definition)
            => definition.Unit != null && definition.Cooldown > 0f && definition.Unit.AttackType != AttackType.Heal && definition.Unit.AttackDamage > 0f;

        // 탑에서 싸우는 유닛을 내는 기물 수 · filled = 탑의 기물 수
        private static int Fighters(out int filled)
        {
            int fighters = 0;
            filled = 0;
            foreach (OwnedBuilding owned in RunState.Field)
                if (!OwnedBuilding.IsEmpty(owned))
                {
                    filled++;
                    if (IsFighter(owned.Definition))
                        fighters++;
                }
            return fighters;
        }

        private static bool Owns(BuildingDefinition definition)
        {
            foreach (OwnedBuilding[] row in new[] { RunState.Field, RunState.Storage })
                foreach (OwnedBuilding owned in row)
                    if (!OwnedBuilding.IsEmpty(owned) && owned.Definition == definition)
                        return true;
            return false;
        }

        // 사면 탑에 새 칸이 필요한가 — 합쳐지면 필요 없다
        private static bool NeedsRoom(BuildingDefinition definition) => RunState.FindMergeTarget(definition, 0) == null && FirstEmpty() < 0;

        // 기물 값(price)에 층 올리기 값까지 지금 낼 수 있나 — 못 내면 층을 기다리며 골드를 쌓지 않고 가장 낮은 기물과 바꾼다
        private static bool CanRaiseFor(int price) => RunState.Floors < RunState.MaxFloors && RunState.Gold >= price + RunState.NextFloorCost;

        // 자리가 없으면 층을 올리고, 최고층이거나 층 올릴 골드가 안 되면 값이 가장 낮은 기물을 판다
        private static void MakeRoom(EnemyBuildProfile build, BuildingDefinition definition, StringBuilder log)
        {
            if (CanRaiseFor(RunState.PriceOf(definition)))
            {
                log.Append($" 층 올리기({RunState.NextFloorCost})");
                RunState.RaiseFloor();
                return;
            }
            int lowest = Lowest(build);
            if (lowest < 0)
                return;
            log.Append($" 판매 {RunState.Field[lowest].Definition.DisplayName}(+{RunState.SellValue(RunState.Field[lowest])})");
            RunState.Sell(false, lowest);
        }

        // 팔 기물 — 이번 라운드에 산 것을 빼고 값이 가장 낮은 것. 팔면 생산 기물 비율이 모자라게 되는 동안에는 생산 기물을 팔지 않는다(없으면 -1). 탑이 꽉 찼을 때만 부른다
        private static int Lowest(EnemyBuildProfile build)
        {
            bool keepFighters = Fighters(out int filled) - 1 < build.ProducerShare * filled;
            int lowest = -1;
            for (int i = 0; i < RunState.Field.Length; i++)
                if (Held.Contains(RunState.Field[i]) && !(keepFighters && IsFighter(RunState.Field[i].Definition))
                    && (lowest < 0 || OwnedValue(build, RunState.Field[i]) < OwnedValue(build, RunState.Field[lowest])))
                    lowest = i;
            return lowest;
        }

        private static int FirstEmpty() => System.Array.FindIndex(RunState.Field, OwnedBuilding.IsEmpty);

        // 배치 — 자리에 따라 달라지는 효과(BuildingAction·BuildingPassive.Reaches: 아래 모든 기물·위 모든 기물·바로 위·바로 위아래)가
        // 닿는 기물 수가 늘어나도록 기물 하나를 다른 층에 끼워 넣는다(플레이어가 끌어서 옮기는 것과 같은 RunState.MoveInsert) — 더 늘릴 수 없을 때까지 한 번에 하나씩
        private static void Arrange()
        {
            int filled = FirstEmpty() < 0 ? RunState.Field.Length : FirstEmpty();
            var order = new List<BuildingDefinition>();
            // 옮길 때마다 닿는 수가 늘어나므로 끝난다 — 상한은 안전망
            for (int pass = 0; pass < MaxActions; pass++)
            {
                order.Clear();
                for (int i = 0; i < filled; i++)
                    order.Add(RunState.Field[i].Definition);
                int best = Reached(order), bestFrom = -1, bestTo = -1;
                for (int from = 0; from < filled; from++)
                    for (int to = 0; to < filled; to++)
                    {
                        if (to == from)
                            continue;
                        BuildingDefinition moved = order[from];
                        order.RemoveAt(from);
                        order.Insert(to, moved);
                        int reached = Reached(order);
                        order.RemoveAt(to);
                        order.Insert(from, moved);
                        if (reached > best)
                        {
                            best = reached;
                            bestFrom = from;
                            bestTo = to;
                        }
                    }
                if (bestFrom < 0)
                    return;
                // MoveInsert의 자리는 빼기 전 기준 칸 번호
                RunState.MoveInsert(false, bestFrom, bestTo > bestFrom ? bestTo + 1 : bestTo);
            }
        }

        // 이 순서로 쌓았을 때 자리 효과가 닿는 (주는 기물, 받는 기물) 쌍의 수
        private static int Reached(List<BuildingDefinition> order)
        {
            int count = 0;
            for (int from = 0; from < order.Count; from++)
                for (int to = 0; to < order.Count; to++)
                    if (from != to && Reaches(order[from], from, to, order[to]))
                        count++;
            return count;
        }

        private static bool Reaches(BuildingDefinition source, int slot, int other, BuildingDefinition target)
        {
            foreach (BuildingAction action in source.Actions)
                if (action.Reaches(slot, other, target))
                    return true;
            foreach (BuildingPassive passive in source.Passives)
                if (passive.Reaches(slot, other, target))
                    return true;
            return false;
        }
    }
}
