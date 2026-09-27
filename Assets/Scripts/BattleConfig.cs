using UnityEngine;

namespace GnorpWar
{
    // 전투 한 판 전체에 걸친 수치 (자원 · 적 소환)
    [CreateAssetMenu(menuName = "GnorpWar/Battle Config")]
    public class BattleConfig : ScriptableObject
    {
        [SerializeField] private float _startResource = 100f;
        [SerializeField] private float _resourcePerSecond = 25f;
        [SerializeField] private float _maxResource = 500f;
        [Tooltip("적 소환 간격 — 시작 값에서 끝 값까지 Ramp 시간 동안 줄어든다. 교착이 길어지면 적이 강해져 판이 반드시 끝난다")]
        [SerializeField] private float _enemySpawnIntervalStart = 2.2f;
        [SerializeField] private float _enemySpawnIntervalEnd = 1.2f;
        [SerializeField] private float _enemySpawnRampSeconds = 300f;

        public float StartResource => _startResource;
        public float ResourcePerSecond => _resourcePerSecond;
        public float MaxResource => _maxResource;
        public float EnemySpawnIntervalAt(float elapsed)
        {
            return Mathf.Lerp(_enemySpawnIntervalStart, _enemySpawnIntervalEnd, elapsed / _enemySpawnRampSeconds);
        }
    }
}
