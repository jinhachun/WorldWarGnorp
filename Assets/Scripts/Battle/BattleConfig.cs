using UnityEngine;

namespace GnorpWar
{
    // 자원 획득 레벨 한 칸 (냥코 일꾼 레벨 / 카툰워즈 마나 업그레이드)
    [System.Serializable]
    public struct IncomeLevel
    {
        [SerializeField] private float _perSecond;
        [SerializeField] private float _max;
        [Tooltip("이 레벨에서 다음 레벨로 올리는 비용. 마지막 레벨은 쓰지 않는다")]
        [SerializeField] private float _upgradeCost;

        public float PerSecond => _perSecond;
        public float Max => _max;
        public float UpgradeCost => _upgradeCost;
    }

    // 전투 한 판 전체에 걸친 수치 (자원 · 적 소환)
    [CreateAssetMenu(menuName = "GnorpWar/Battle Config")]
    public class BattleConfig : ScriptableObject
    {
        [SerializeField] private float _startResource = 100f;
        [Tooltip("0번이 시작 레벨. 강화 버튼으로 한 칸씩 올라간다")]
        [SerializeField] private IncomeLevel[] _incomeLevels;
        [Tooltip("적 처치 보상 = 그 적의 최대 체력 × 이 값. 강한(튼튼한) 적일수록 자원이 확 뛴다(냥코대전쟁식)")]
        [SerializeField] private float _killRewardPerHp = 0.25f;
        [Tooltip("적 소환 간격 — 시작 값에서 끝 값까지 Ramp 시간 동안 줄어든다. 교착이 길어지면 적이 강해져 판이 반드시 끝난다")]
        [SerializeField] private float _enemySpawnIntervalStart = 2.2f;
        [SerializeField] private float _enemySpawnIntervalEnd = 1.2f;
        [SerializeField] private float _enemySpawnRampSeconds = 300f;
        [Tooltip("보스(공룡) — 판 시작 후 처음 나오는 시각, 이후 간격")]
        [SerializeField] private float _bossFirstSeconds = 40f;
        [SerializeField] private float _bossIntervalSeconds = 60f;
        [Tooltip("보스 등장 충격파 — 아군 전체가 뒤로 밀려나는 속도(가로)·솟는 속도(세로)·조종 불능 시간")]
        [SerializeField] private float _bossShockwaveSpeed = 8f;
        [SerializeField] private float _bossShockwaveLift = 4f;
        [SerializeField] private float _bossShockwaveStun = 0.6f;

        public float KillRewardPerHp => _killRewardPerHp;
        public float BossFirstSeconds => _bossFirstSeconds;
        public float BossIntervalSeconds => _bossIntervalSeconds;
        public float BossShockwaveSpeed => _bossShockwaveSpeed;
        public float BossShockwaveLift => _bossShockwaveLift;
        public float BossShockwaveStun => _bossShockwaveStun;

        public float StartResource => _startResource;
        public int IncomeLevelCount => _incomeLevels.Length;
        public IncomeLevel GetIncomeLevel(int level) => _incomeLevels[level];
        public float EnemySpawnIntervalAt(float elapsed)
        {
            return Mathf.Lerp(_enemySpawnIntervalStart, _enemySpawnIntervalEnd, elapsed / _enemySpawnRampSeconds);
        }
    }
}
