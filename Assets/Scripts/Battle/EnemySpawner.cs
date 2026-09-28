using UnityEngine;

namespace GnorpWar
{
    // 적 AI — 점점 짧아지는 간격으로 무작위 유닛을 소환하는 단순 시간표
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

            _timer = _config.EnemySpawnIntervalAt(Time.timeSinceLevelLoad);
            Unit.Spawn(_unitPrefabs[Random.Range(0, _unitPrefabs.Length)], transform.position);
        }
    }
}
