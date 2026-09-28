using UnityEngine;

namespace GnorpWar
{
    // 플레이어 자원 — 시간에 따라 쌓이고 소환에 쓴다. 획득 레벨을 올리면 초당 획득량·최대치가 는다
    public class PlayerWallet : MonoBehaviour
    {
        [SerializeField] private BattleConfig _config;

        public float Current { get; private set; }
        // 0부터 시작 (화면 표시는 +1)
        public int IncomeLevel { get; private set; }
        public float Max => Level.Max;
        public bool CanUpgradeIncome => IncomeLevel < _config.IncomeLevelCount - 1;
        public float IncomeUpgradeCost => Level.UpgradeCost;

        private IncomeLevel Level => _config.GetIncomeLevel(IncomeLevel);

        private void Awake()
        {
            Current = _config.StartResource;
        }

        private void OnEnable()
        {
            Unit.Died += OnUnitDied;
        }

        private void OnDisable()
        {
            Unit.Died -= OnUnitDied;
        }

        private void Update()
        {
            Current = Mathf.Min(Current + Level.PerSecond * Time.deltaTime, Max);
        }

        public bool TrySpend(float amount)
        {
            if (Current < amount)
                return false;

            Current -= amount;
            return true;
        }

        public bool TryUpgradeIncome()
        {
            if (!CanUpgradeIncome || !TrySpend(IncomeUpgradeCost))
                return false;

            IncomeLevel++;
            return true;
        }

        // 냥코대전쟁처럼 적을 처치하면 자원을 받는다 — 튼튼한 적일수록 많이(체력 비례). 최대치에서 잘린다
        private void OnUnitDied(Unit unit)
        {
            if (unit.Team == Team.Enemy)
                Current = Mathf.Min(Current + unit.Definition.MaxHp * _config.KillRewardPerHp, Max);
        }
    }
}
