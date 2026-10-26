using System.Collections;
using UnityEngine;

namespace GnorpWar
{
    // 전투 시작 알림(전투 시각화 F9) — 전투가 열리고 조금 뒤, 양 진영 본진의 화살 발사점에서 업그레이드 · 지역 특수가 건 대상으로 버프 선을 긋고 버프 글자를 띄운다
    // (사용자 10/20 Q1 (가) — 켠 기물이 없는 효과는 본진에서). 효과는 이미 걸려 있다 — 업그레이드 목록을 읽기만 한다(RunState.HoldingsFor).
    // 같은 종류의 선은 짧은 간격 안에 솎이므로(BuffLinkFx) 한 줄씩 사이를 두고 긋는다. FxDirector가 만든다
    public class BattleStartLines : MonoBehaviour
    {
        // 전투가 열린 뒤 첫 줄까지(게임 시간) · 줄 사이(실제 시간 — 솎기가 실제 시간이다)
        private const float Delay = 0.5f;
        private const float LineGap = 0.2f;

        private bool _shown;

        private void Update()
        {
            if (!BattleManager.Fighting)
            {
                _shown = false;
                return;
            }
            if (_shown || BattleManager.Elapsed < Delay)
                return;
            _shown = true;
            StartCoroutine(Show(Team.Ally));
            StartCoroutine(Show(Team.Enemy));
        }

        private IEnumerator Show(Team team)
        {
            foreach (UpgradeHolding holding in RunState.HoldingsFor(team))
                if (Draw(team, holding))
                    yield return new WaitForSecondsRealtime(LineGap);
        }

        // 한 줄 — 그은 게 있으면 true
        private static bool Draw(Team team, UpgradeHolding holding)
        {
            UpgradeDefinition upgrade = holding.Definition;
            BoardRunner board = BoardRunner.For(team);
            if (board == null)
                return false;
            switch (upgrade.Kind)
            {
                case UpgradeKind.UnitStat:
                    if (!upgrade.UnitModifier(holding.Value, out StatModifier modifier))
                        return false;   // 다른 지형의 지형 성소
                    BuffLinks.FromBase(team, UnitsOr(team, upgrade, board), BuffKind.Stat, upgrade.HealLabel ? BuffLabel.Heal(modifier) : BuffLabel.Of(modifier));
                    return true;
                case UpgradeKind.UnitEffect:
                    return EffectLine(team, upgrade, board);
                case UpgradeKind.SmallTowerAttack:
                    bool built = false;
                    BuffLabel small = BuffLabel.Of(new TowerModifier(TowerStat.Attack, StatOp.Percent, holding.Value / 100f));
                    foreach (Base tower in Base.SmallTowers())
                        if (tower.Team == team && tower.IsAlive)
                        {
                            BuffLinks.FromBase(team, tower.Muzzle, BuffKind.Stat, small);
                            built = true;
                        }
                    return built;
                case UpgradeKind.Summon:
                    BuffLinks.FromBase(team, board.SpawnPoint, BuffKind.Summon);
                    return true;
                case UpgradeKind.TowerStat:
                    TowerLabel(team, new TowerModifier(upgrade.TowerStat, StatOp.Percent, holding.Value / 100f));
                    return true;
                case UpgradeKind.BaseHp:
                    TowerLabel(team, new TowerModifier(TowerStat.MaxHp, StatOp.Percent, holding.Value / 100f), false);
                    return true;
            }
            switch (upgrade.Special)
            {
                case SpecialKind.Nationalism:
                    var attack = new StatModifier(UnitStat.Attack, StatOp.Percent, holding.Value / 100f * RunState.LivesFor(team));
                    BuffLinks.FromBase(team, UnitsOr(team, null, board), BuffKind.Stat, BuffLabel.Of(attack));
                    return true;
                case SpecialKind.Monarchy:
                    if (board.TopIndex < 0)
                        return false;
                    BuffLinks.FromBaseToSlot(team, board.TopIndex, BuffKind.Trigger);
                    return true;
                case SpecialKind.Industrial:
                case SpecialKind.Conscription:
                    bool any = false;
                    for (int i = 0; i < board.Slots.Length; i++)
                    {
                        OwnedBuilding building = board.Slots[i].Building;
                        if (OwnedBuilding.IsEmpty(building) || board.Slots[i].Cooldown <= 0f)
                            continue;
                        if (upgrade.Special == SpecialKind.Conscription && building.Definition.Unit == null)
                            continue;
                        BuffLinks.FromBaseToSlot(team, i, upgrade.Special == SpecialKind.Industrial ? BuffKind.Haste : BuffKind.Summon);
                        any = true;
                    }
                    return any;
            }
            return false;
        }

        // 유닛 효과를 켠 업그레이드 — 에셋의 선 색 · 가는 곳(유닛 무리 · 본진 둘레 · 쿨다운이 있는 층 전부)
        private static bool EffectLine(Team team, UpgradeDefinition upgrade, BoardRunner board)
        {
            switch (upgrade.LineTarget)
            {
                case UpgradeLine.Base:
                    if (!(Base.For(team) is Base home) || !home.IsAlive)
                        return false;
                    RingFx.Pulse(home.Bounds.center, home.Bounds.extents.y, BuffLinkFx.ColorOf(upgrade.LineKind), home);
                    return true;
                case UpgradeLine.Floors:
                    bool any = false;
                    for (int i = 0; i < board.Slots.Length; i++)
                        if (!OwnedBuilding.IsEmpty(board.Slots[i].Building) && board.Slots[i].Cooldown > 0f)
                        {
                            BuffLinks.FromBaseToSlot(team, i, upgrade.LineKind);
                            any = true;
                        }
                    return any;
                default:
                    BuffLinks.FromBase(team, UnitsOr(team, upgrade, board), upgrade.LineKind);
                    return true;
            }
        }

        // 맞는 내 유닛 무리 가운데 — 아직 없으면 성문 앞(곧 나올 자리)
        private static Vector2 UnitsOr(Team team, IUnitFilter filter, BoardRunner board)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Unit unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team == team && (filter == null || filter.AppliesTo(unit.Definition)))
                {
                    sum += unit.Position;
                    count++;
                }
            return count > 0 ? sum / count : board.SpawnPoint;
        }

        // 타워 스탯 — 작은 타워에는 본진에서 선을 긋고, 본진 자신에게는 글자만(선이 제자리라). small = 작은 타워도 받는 스탯인가
        private static void TowerLabel(Team team, TowerModifier modifier, bool small = true)
        {
            BuffLabel label = BuffLabel.Of(modifier);
            float forward = team == Team.Ally ? 1f : -1f;
            if (Base.For(team) is Base home && home.IsAlive)
                BuffTexts.Show(home.Muzzle, label, forward);
            if (!small)
                return;
            foreach (Base tower in Base.SmallTowers())
                if (tower.Team == team && tower.IsAlive)
                    BuffLinks.FromBase(team, tower.Muzzle, BuffKind.Stat, label);
        }
    }
}
