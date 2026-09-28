using UnityEngine;

namespace GnorpWar
{
    // 적 AI — 점점 짧아지는 간격으로 무작위 유닛을 소환하는 단순 시간표 + 주기적인 보스(공룡)
    public class EnemySpawner : MonoBehaviour
    {
        // 보스 등장 흔들림 — 최대치(1)로 크게
        private const float BossArrivalShake = 1f;

        [SerializeField] private Unit[] _unitPrefabs;
        [SerializeField] private BattleConfig _config;
        [Tooltip("주기적으로 나오는 보스. 비우면 보스 없음")]
        [SerializeField] private Unit _bossPrefab;
        [Tooltip("보스는 몸이 커서 기지와 겹치지 않도록 따로 조금 앞에서 나온다")]
        [SerializeField] private Transform _bossSpawnPoint;

        private float _timer;
        private float _bossTimer;

        private void Start()
        {
            _bossTimer = _config.BossFirstSeconds;
        }

        private void Update()
        {
            UpdateBoss();

            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;

            _timer = _config.EnemySpawnIntervalAt(Time.timeSinceLevelLoad);
            Unit.Spawn(_unitPrefabs[Random.Range(0, _unitPrefabs.Length)], transform.position);
        }

        // 냥코대전쟁 보스 등장처럼 — 화면이 크게 흔들리고 아군 전체가 뒤(아군 기지 쪽, -x)로 밀려난다
        private void UpdateBoss()
        {
            if (_bossPrefab == null)
                return;

            _bossTimer -= Time.deltaTime;
            if (_bossTimer > 0f)
                return;

            _bossTimer = _config.BossIntervalSeconds;
            Unit.Spawn(_bossPrefab, _bossSpawnPoint.position);
            Unit.ShockwaveAll(Team.Ally, new Vector2(-_config.BossShockwaveSpeed, _config.BossShockwaveLift), _config.BossShockwaveStun);
            if (FxDirector.Instance != null)
                FxDirector.Instance.Shake(BossArrivalShake);
        }
    }
}
