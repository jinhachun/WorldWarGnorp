using UnityEngine;

namespace GnorpWar
{
    // 적 AI — 지금은 일정 간격으로 소환하는 단순 시간표
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Unit _unitPrefab;
        [SerializeField] private BattleConfig _config;

        private float _timer;

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;

            _timer = _config.EnemySpawnInterval;
            Instantiate(_unitPrefab, transform.position, Quaternion.identity);
        }
    }
}
