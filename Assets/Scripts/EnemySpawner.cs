using UnityEngine;

namespace GnorpWar
{
    // 적 AI — 지금은 일정 간격으로 무작위 유닛을 소환하는 단순 시간표
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Unit[] _unitPrefabs;
        [SerializeField] private BattleConfig _config;

        private float _timer;

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;

            _timer = _config.EnemySpawnInterval;
            Instantiate(_unitPrefabs[Random.Range(0, _unitPrefabs.Length)], transform.position, Quaternion.identity);
        }
    }
}
