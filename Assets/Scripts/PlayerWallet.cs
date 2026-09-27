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

        private void Update()
        {
            Current = Mathf.Min(Current + _config.ResourcePerSecond * Time.deltaTime, Max);
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
