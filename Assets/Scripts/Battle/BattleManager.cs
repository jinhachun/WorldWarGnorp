using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GnorpWar
{
    // 한 라운드의 흐름 — 씬을 불러오면 상점, "전투 시작"으로 전투, 어느 기지든 무너지면 결과 → 다음 라운드(씬 재로드).
    // 런 상태(골드·목숨·보드)는 RunState(static)에 있어 재로드에도 남는다
    public class BattleManager : MonoBehaviour
    {
        private static BattleManager _instance;

        [SerializeField] private BattleConfig _config;
        [SerializeField] private Base _allyBase;
        [SerializeField] private Base _enemyBase;
        [SerializeField] private BoardRunner _allyBoard;
        [SerializeField] private BoardRunner _enemyBoard;
        [SerializeField] private GameObject _shopPanel;
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private Text _resultText;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Text _retryLabel;
        [Tooltip("전투 배속 버튼 — 전투 중에만 보인다")]
        [SerializeField] private GameObject _speedButton;

        private bool _fighting;
        private bool _over;
        private float _battleStart;

        public static bool Fighting => _instance != null && _instance._fighting;
        public static float Elapsed => Fighting ? Time.time - _instance._battleStart : 0f;
        // 공격력 가속 — 모든 유닛 공격에 곱한다
        public static float DamageMultiplier => Fighting ? _instance._config.DamageMultiplierAt(Elapsed) : 1f;
        // 생산 가속 — 모든 건물의 한 번 생산 수에 더한다
        public static int ExtraUnits => Fighting ? _instance._config.ExtraUnitsAt(Elapsed) : 0;

        private void Awake()
        {
            _instance = this;
            // 결과 화면에서 멈춘 채 다음 라운드로 들어왔을 수 있다
            Time.timeScale = 1f;
            if (!RunState.Started || RunState.IsOver)
                RunState.StartNew(_config);
            RunState.EnterShop();

            _allyBoard.Load(RunState.Field);
            _enemyBoard.Load(_config.EnemyRoundAt(RunState.Round).Field);
            _shopPanel.SetActive(true);
            _resultPanel.SetActive(false);
            _speedButton.SetActive(false);
            _allyBase.Destroyed += OnBaseDestroyed;
            _enemyBase.Destroyed += OnBaseDestroyed;
            _retryButton.onClick.AddListener(NextRound);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public void StartBattle()
        {
            _shopPanel.SetActive(false);
            _allyBoard.Load(RunState.Field);
            TeamEffects.Clear();
            StatBook.ClearBattle();
            TowerBook.ClearBattle();
            UnitAliases.Clear();
            _allyBoard.Begin();
            _enemyBoard.Begin();
            _battleStart = Time.time;
            _fighting = true;
            _speedButton.SetActive(true);
            Time.timeScale = SpeedButton.Current;
        }

        private void OnBaseDestroyed(Base destroyed)
        {
            if (_over)
                return;

            _over = true;
            bool won = destroyed.Team == Team.Enemy;
            Debug.Log($"[Battle] 라운드 {RunState.Round + 1} {(won ? "승리" : "패배")} — {Elapsed:F1}초");
            _fighting = false;
            _allyBoard.Stop();
            _enemyBoard.Stop();
            RunState.FinishBattle(won);

            string line = won ? "승리!" : "패배...";
            if (RunState.Cleared)
                line = "런 클리어!";
            else if (RunState.IsOver)
                line = "게임 오버";
            _resultText.text = $"{line}\n<size=48>목숨 {RunState.Lives} · 승리 {RunState.Wins}</size>";
            _retryLabel.text = RunState.IsOver ? "새 런" : "다음 라운드";
            _resultPanel.SetActive(true);
            _speedButton.SetActive(false);
            Time.timeScale = 0f;
        }

        private void NextRound()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
