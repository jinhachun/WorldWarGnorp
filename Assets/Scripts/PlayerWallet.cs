using UnityEngine;

namespace GnorpWar
{
    // 플레이어 자원 — 시간에 따라 쌓이고 소환에 쓴다
    public class PlayerWallet : MonoBehaviour
    {
        [SerializeField] private BattleConfig _config;

        public float Current { get; private set; }
        public float Max => _config.MaxResource;

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
            Current = Mathf.Min(Current + _config.ResourcePerSecond * Time.deltaTime, Max);
        }

        // 냥코대전쟁처럼 적을 처치하면 자원을 받는다
        private void OnUnitDied(Unit unit)
        {
            if (unit.Team == Team.Enemy)
                Current = Mathf.Min(Current + unit.Definition.KillReward, Max);
        }

        public bool TrySpend(float amount)
        {
            if (Current < amount)
                return false;

            Current -= amount;
            return true;
        }
    }
}
