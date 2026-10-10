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
        // 데스매치 — 이 시각부터 양쪽 본진이 화살을 멈춘다
        public static bool TowersStopped => Fighting && Elapsed >= _instance._config.DeathmatchSeconds;
        // 데스매치 — 이 시각부터 모든 기물이 발동하지 않는다(생산 포함)
        public static bool BoardsStopped => Fighting && Elapsed >= _instance._config.BoardStopSeconds;
        // 유닛 크기 상한(1 = 기본) · 밟기 피해(초당 공격력 비율) — 전투 씬 밖이면 상한 없음 · 0
        public static float MaxUnitSize => _instance != null ? _instance._config.MaxUnitSize : float.MaxValue;
        public static float StompDamagePerSecond => _instance != null ? _instance._config.StompDamagePerSecond : 0f;
        // 등반 — 모든 등반 점프 뒤 쉬는 초 · 앞 아군이 이 비율보다 느려야 오른다 · 점프 높이 = 키 + 이 칸. 전투 씬 밖이면 기본값
        public static float ClimbRest => _instance != null ? _instance._config.ClimbRest : 0f;
        public static float ClimbSlowerThan => _instance != null ? _instance._config.ClimbSlowerThan : 0.5f;
        public static float ClimbJumpExtra => _instance != null ? _instance._config.ClimbJumpExtra : 2f;
        // 데스매치 시각(전투 경과 초) — 화면 표시용. 전투 씬 밖이면 0
        public static float DeathmatchAt => _instance != null ? _instance._config.DeathmatchSeconds : 0f;
        public static float BoardStopAt => _instance != null ? _instance._config.BoardStopSeconds : 0f;
        // 전투력 계수(CombatPower가 읽는다) — 전투 씬 밖이면 null
        public static BattleConfig Config => _instance != null ? _instance._config : null;

        // ── 치트 훅(Dev/CheatPanel만 부른다) — 상점 단계인가 · 전투 경과 시간을 이 초로 · 전투를 바로 끝낸다
        public static bool InShop => _instance != null && !_instance._fighting && !_instance._over;

        public static void CheatSkipTo(float elapsed)
        {
            if (Fighting)
                _instance._battleStart = Time.time - elapsed;
        }

        public static void CheatFinish(bool won)
        {
            if (Fighting)
                _instance.Finish(won);
        }

        private void Awake()
        {
            _instance = this;
            // 결과 화면에서 멈춘 채 다음 라운드로 들어왔을 수 있다
            Time.timeScale = 1f;
            if (!RunState.Started || RunState.IsOver)
                RunState.StartNew(_config);
            RunState.EnterShop();

            _allyBoard.Load(RunState.Field);
            _enemyBoard.Load(OwnedBuilding.Compacted(RunState.CheatEnemyField ?? _config.EnemyRoundAt(RunState.Round).Field));
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
            // 맵의 유닛 수치(숲 · 초원) — 양 진영에 똑같이, 전투 동안
            if (RunState.Map != null)
                foreach (MapDefinition.UnitRule rule in RunState.Map.UnitRules)
                {
                    StatBook.AddForBattle(Team.Ally, rule, rule.Modifier);
                    StatBook.AddForBattle(Team.Enemy, rule, rule.Modifier);
                }
            _allyBoard.Begin();
            _enemyBoard.Begin();
            _battleStart = Time.time;
            _fighting = true;
            _speedButton.SetActive(true);
            Time.timeScale = SpeedButton.Current;
        }

        // 데스매치 — 기물까지 멈춘 뒤 양쪽 유닛이 다 사라지면 본진 체력 비율이 높은 쪽이 이긴다. 같으면 아군 승리(사용자 결정).
        // 한쪽만 남았는데 남은 유닛이 전부 공격 못 하는 유닛(회복 전용 · 공격력 0)이면 본진을 못 깨므로 남은 쪽 승리(사용자 결정)
        private void Update()
        {
            // 상점에서 층을 올리면 RunState.Field가 새 배열이 된다 — 아군 보드(탑 그림이 읽는다)를 다시 올린다
            if (!_fighting && _allyBoard.Field != RunState.Field)
                _allyBoard.Load(RunState.Field);
            if (!BoardsStopped || _over)
                return;
            int allies = 0, enemies = 0, allyAttackers = 0, enemyAttackers = 0;
            foreach (Unit unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
            {
                if (!unit.IsAlive)
                    continue;
                // 회복 전용은 공격력과 상관없이 적을 치지 않는다(Unit의 전투 분기). 공격력은 버프가 들어간 지금 값
                bool attacker = unit.Definition.AttackType != AttackType.Heal && unit.AttackDamage > 0f;
                if (unit.Team == Team.Ally)
                {
                    allies++;
                    if (attacker)
                        allyAttackers++;
                }
                else
                {
                    enemies++;
                    if (attacker)
                        enemyAttackers++;
                }
            }
            if (allies == 0 && enemies == 0)
            {
                Debug.Log($"[Battle] 데스매치 — 양쪽 유닛 0 · 본진 체력 아군 {_allyBase.Hp01:P0} · 적 {_enemyBase.Hp01:P0}");
                Finish(_allyBase.Hp01 >= _enemyBase.Hp01);
            }
            else if (enemies == 0 && allyAttackers == 0)
            {
                Debug.Log($"[Battle] 데스매치 — 적 유닛 0 · 아군은 공격 못 하는 유닛 {allies}기만");
                Finish(true);
            }
            else if (allies == 0 && enemyAttackers == 0)
            {
                Debug.Log($"[Battle] 데스매치 — 아군 유닛 0 · 적은 공격 못 하는 유닛 {enemies}기만");
                Finish(false);
            }
        }

        private void OnBaseDestroyed(Base destroyed)
        {
            if (!_over)
                Finish(destroyed.Team == Team.Enemy);
        }

        private void Finish(bool won)
        {
            _over = true;
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
            _resultText.text = line;   // 목숨·승리는 결과 창 아이콘 줄(UI)이 그린다
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
