using UnityEngine;

namespace GnorpWar
{
    // 측정 전용 — 판 진행을 콘솔에 남긴다(처치 보상으로 자원이 튀는 순간 · 보스 등장 · 10초마다 요약).
    // 씬에는 두지 않는다. 측정할 때만 플레이 중에 붙인다(AutoPlayer와 같이)
    public class BattleRecorder : MonoBehaviour
    {
        private const float SummaryInterval = 10f;

        private PlayerWallet _wallet;
        private BattleConfig _config;
        private float _nextSummary;
        private int _bossesSeen;

        private void Awake()
        {
            _wallet = FindAnyObjectByType<PlayerWallet>();
            _config = (BattleConfig)typeof(PlayerWallet)
                .GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(_wallet);
        }

        private void OnEnable() => Unit.Died += OnDied;
        private void OnDisable() => Unit.Died -= OnDied;

        // 지갑이 먼저 구독돼 있어 여기서 읽는 값은 보상이 들어간 뒤다
        private void OnDied(Unit unit)
        {
            if (unit.Team != Team.Enemy)
                return;
            float reward = unit.Definition.MaxHp * _config.KillRewardPerHp;
            bool capped = _wallet.Current >= _wallet.Max - 0.01f;
            Debug.Log($"[Rec] {Time.timeSinceLevelLoad:F0}s kill {unit.Definition.name} +{reward:F0} → {_wallet.Current:F0}/{_wallet.Max:F0}{(capped ? " (CAPPED)" : "")}");
        }

        private void Update()
        {
            int bosses = 0, ally = 0, enemy = 0;
            float allyFront = -999f, enemyFront = 999f;
            foreach (Unit unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
            {
                if (!unit.IsAlive)
                    continue;
                if (unit.Definition.AttackType == AttackType.Flame)
                    bosses++;
                if (unit.Team == Team.Ally) { ally++; allyFront = Mathf.Max(allyFront, unit.transform.position.x); }
                else { enemy++; enemyFront = Mathf.Min(enemyFront, unit.transform.position.x); }
            }
            if (bosses > _bossesSeen)
                Debug.Log($"[Rec] {Time.timeSinceLevelLoad:F0}s BOSS appears (ally {ally} vs enemy {enemy}, wallet {_wallet.Current:F0}/{_wallet.Max:F0})");
            _bossesSeen = bosses;

            if (Time.timeSinceLevelLoad >= _nextSummary)
            {
                _nextSummary += SummaryInterval;
                Debug.Log($"[Rec] {Time.timeSinceLevelLoad:F0}s wallet {_wallet.Current:F0}/{_wallet.Max:F0} Lv{_wallet.IncomeLevel + 1} · ally {ally} vs enemy {enemy} · front {allyFront:F0}/{enemyFront:F0}");
            }
        }
    }
}
