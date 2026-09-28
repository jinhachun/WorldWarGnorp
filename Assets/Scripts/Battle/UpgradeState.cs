using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 이번 판에 산 유닛 업그레이드. 씬 오브젝트라 다시하기(씬 재로드)로 초기화된다.
    // 업그레이드는 아군에게만 적용된다 — 유닛 정의는 적과 같이 쓰므로 진영으로 거른다
    public class UpgradeState : MonoBehaviour
    {
        [Tooltip("Sword 업그레이드로 함께 소환되는 유닛(아군 Sword 프리팹)")]
        [SerializeField] private Unit _escortPrefab;

        private readonly HashSet<UnitDefinition> _bought = new HashSet<UnitDefinition>();

        public static UpgradeState Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool Has(UnitDefinition definition) => _bought.Contains(definition);

        public void Buy(UnitDefinition definition) => _bought.Add(definition);

        public static bool IsActive(Unit unit, UpgradeKind kind)
        {
            return unit.Team == Team.Ally && Instance != null
                   && unit.Definition.Upgrade == kind && Instance.Has(unit.Definition);
        }

        // 플레이어가 소환 버튼으로 유닛을 낸 직후 — Sword 업그레이드: 다른 유닛이면 확률로 Sword가 같이 나온다
        public void OnPlayerSummon(UnitDefinition summoned, Vector2 groundPoint)
        {
            foreach (UnitDefinition definition in _bought)
            {
                if (definition.Upgrade == UpgradeKind.SwordEscort && definition != summoned && Random.value < definition.UpgradeValue)
                    Unit.Spawn(_escortPrefab, groundPoint);
            }
        }
    }
}
