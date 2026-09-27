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
        [SerializeField] private float _enemySpawnInterval = 2.5f;

        public float StartResource => _startResource;
        public float ResourcePerSecond => _resourcePerSecond;
        public float MaxResource => _maxResource;
        public float EnemySpawnInterval => _enemySpawnInterval;
    }
}
