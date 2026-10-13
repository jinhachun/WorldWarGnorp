using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GnorpWar
{
    // 전투 씬 — 전투만. 지도 씬(World/WorldManager)에서 「침략」으로 오면 바로 전투다(상점은 지도 씬의 팝업 — UI/ShopPanel).
    // 어느 기지든 무너지면 결과 → 「지도로」(AI 영주들의 턴은 지도에서 돈다). 런 상태(골드·보드·판도)는 RunState(static)에 있어 씬을 오가도 남는다
    public class BattleManager : MonoBehaviour
    {
        private static BattleManager _instance;

        [SerializeField] private BattleConfig _config;
        [SerializeField] private Base _allyBase;
        [SerializeField] private Base _enemyBase;
        [SerializeField] private BoardRunner _allyBoard;
        [SerializeField] private BoardRunner _enemyBoard;
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private Text _resultText;
        [SerializeField] private Button _retryButton;
        [Tooltip("결과 창의 읽는 글 — 내 결과 한 줄(이기면 쓰러뜨린 영주의 글 · 지면 행복도 -N)")]
        [SerializeField] private Text _resultSummary;
        [Tooltip("패배 시 내 결과 한 줄(「-1」) 바로 왼쪽에 뜨는 행복도 하트")]
        [SerializeField] private Image _resultHeart;
        [Tooltip("전투 배속 버튼 — 전투 중에만 보인다")]
        [SerializeField] private GameObject _speedButton;

        private const float ResultHeartGap = 6f;

        private bool _fighting;
        private bool _over;
        private float _battleStart;
        // 마지막으로 누가 피해를 받거나 죽은 시각(유닛 · 본진 · 작은 타워) — 교착 판정
        private float _lastProgress;

        // 피해·사망이 있었다(Unit · Base가 부른다)
        public static void NoteProgress()
        {
            if (_instance != null)
                _instance._lastProgress = Time.time;
        }

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

        // ── 치트 훅(Dev/CheatPanel만 부른다) — 전투 경과 시간을 이 초로 · 전투를 바로 끝낸다
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
            // 지난 전투의 "전투 동안" 타워 효과(사거리 등)가 남아 있지 않게
            TowerBook.ClearBattle();
            if (!RunState.Started || RunState.IsOver)
                RunState.StartNew(_config);
            // 이 씬은 지도에서 「침략」으로만 온다(Phase = Battle) — 땅(Ground) · 적 탑 · 영주 색이 이미 그 땅 · 그 영주 것으로 뜬다.
            // 이 씬만 직접 플레이했으면 싸울 상대가 없다 — 지도 씬으로 넘긴다(국가 고르기 · 상점 · 침략은 거기서)
            bool battle = RunState.Phase == RunState.RunPhase.Battle;

            _allyBoard.Load(RunState.Field);
            _enemyBoard.Load(RunState.CheatEnemyField ?? RunState.EnemyField);
            _resultPanel.SetActive(false);
            _speedButton.SetActive(false);
            _allyBase.Destroyed += OnBaseDestroyed;
            _enemyBase.Destroyed += OnBaseDestroyed;
            _retryButton.onClick.AddListener(ToMap);
            if (battle)
                StartCoroutine(BeginNextFrame());
            else
                SceneManager.LoadScene(WorldManager.SceneName);
        }

        // 다른 오브젝트들의 Start(탑이 보드에 붙는 것 등)가 끝난 뒤에 전투를 연다
        private System.Collections.IEnumerator BeginNextFrame()
        {
            yield return null;
            StartBattle();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // 전투를 연다 — 전투 씬이 뜬 다음 프레임(BeginNextFrame)
        private void StartBattle()
        {
            _allyBoard.Load(RunState.Field);
            TeamEffects.Clear();
            StatBook.ClearBattle();
            TowerBook.ClearBattle();
            UnitAliases.Clear();
            // 맵의 「전부 발동」(얼음) — 양쪽 성 근처에 출발점 그림을 띄운다
            if (RunState.Map != null && RunState.Map.FireAllInterval > 0f)
                foreach (Team team in new[] { Team.Ally, Team.Enemy })
                    MapMarker.Spawn(team, RunState.Map.FireAllMarker);
            // 맵의 유닛 수치(숲 · 초원) — 양 진영에 똑같이, 전투 동안
            if (RunState.Map != null)
                foreach (MapDefinition.UnitRule rule in RunState.Map.UnitRules)
                {
                    StatBook.AddForBattle(Team.Ally, rule, rule.Modifier);
                    StatBook.AddForBattle(Team.Enemy, rule, rule.Modifier);
                }
            // 합치기 보상 업그레이드 — 양 진영이 각자 자기 탑의 것을, 전투 동안. 스탯 · 효과는 보드가 돌기 전에, 소환 · 작은 타워는 돈 뒤에
            RunState.ApplyUpgrades(Team.Ally, false);
            RunState.ApplyUpgrades(Team.Enemy, false);
            _allyBoard.Begin();
            _enemyBoard.Begin();
            RunState.ApplyUpgrades(Team.Ally, true);
            RunState.ApplyUpgrades(Team.Enemy, true);
            _battleStart = Time.time;
            _fighting = true;
            _speedButton.SetActive(true);
            Time.timeScale = SpeedButton.Current;
        }

        // 데스매치 — 기물까지 멈춘 뒤 양쪽 유닛이 다 사라지고 아무것도 더 죽을 수 없으면(쏠 수 있는 타워에 겨눌 대상이 없다) 아군 승리(사용자 결정 10/11).
        // 한쪽만 남았는데 남은 유닛이 전부 공격 못 하는 유닛(회복 전용 · 공격력 0)이면 본진을 못 깨므로 남은 쪽 승리(사용자 결정).
        // 그 밖에 끝나지 않는 판(양쪽 다 공격 못 하는 유닛만 · 땅에 걸린 유닛)은 교착 판정이 끝낸다
        private void Update()
        {
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
            // 오버드라이브 타워가 적 본진·작은 타워를 아직 깎을 수 있으면 판을 계속 둔다 — 본진 파괴로 끝난다
            if (allies == 0 && enemies == 0 && !Base.AnyTowerHasTarget())
            {
                Debug.Log($"[Battle] 데스매치 — 양쪽 유닛 0 · 겨눌 대상이 있는 타워 없음 → 아군 승리 (본진 체력 아군 {_allyBase.Hp01:P0} · 적 {_enemyBase.Hp01:P0})");
                Finish(true);
            }
            else if (enemies == 0 && allies > 0 && allyAttackers == 0)
            {
                Debug.Log($"[Battle] 데스매치 — 적 유닛 0 · 아군은 공격 못 하는 유닛 {allies}기만");
                Finish(true);
            }
            else if (allies == 0 && enemies > 0 && enemyAttackers == 0)
            {
                Debug.Log($"[Battle] 데스매치 — 아군 유닛 0 · 적은 공격 못 하는 유닛 {enemies}기만");
                Finish(false);
            }
            // 교착 — 위 어디에도 안 걸리고 남은 판. 기물 정지 뒤로 이 시간 동안 피해도 사망도 없으면 아군 승리(사용자 결정 10/11)
            else if (Time.time - Mathf.Max(_lastProgress, _battleStart + _config.BoardStopSeconds) >= _config.StalemateSeconds)
            {
                Debug.Log($"[Battle] 데스매치 — {_config.StalemateSeconds:0}초 동안 피해·사망 없음 → 아군 승리 (유닛 아군 {allies} · 적 {enemies})");
                Finish(true);
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
            int happiness = RunState.Happiness;
            Lord opponent = RunState.Opponent;
            RunState.FinishBattle(won);
            // 결과 창 — 큰 글 · 내 결과 한 줄(이기면 쓰러뜨린 영주의 글, 지면 잃은 행복도) · 「지도로」. 행복도 · 땅은 결과 창 아이콘 줄(UI/ResultStats)이 그린다.
            // AI 영주들의 턴 · 통일 · 게임 오버는 지도에서 보여 준다
            _resultText.text = won ? "승리!" : "패배...";
            _resultSummary.text = won ? WinLine(opponent) : BuildingTooltip.Highlight($"-{happiness - RunState.Happiness}");
            // 잃은 행복도는 하트 + 숫자로 — 하트(글의 자식)는 글의 바로 왼쪽. 하트와 글이 한 덩어리로 가운데에 오게 글을 하트 몫의 절반만큼 오른쪽으로
            _resultHeart.enabled = !won;
            RectTransform heart = _resultHeart.rectTransform, summary = _resultSummary.rectTransform;
            summary.anchoredPosition = new Vector2(won ? 0f : (heart.sizeDelta.x + ResultHeartGap) * 0.5f, summary.anchoredPosition.y);
            heart.anchoredPosition = new Vector2(-(_resultSummary.preferredWidth + heart.sizeDelta.x) * 0.5f - ResultHeartGap, heart.anchoredPosition.y);
            _resultPanel.SetActive(true);
            _speedButton.SetActive(false);
            Time.timeScale = 0f;
        }

        // 승리 글 한 줄(사용자 10/12) — 쓰러뜨린 영주의 닉네임으로. 최후의 연합은 여러 번 이겨야 멸망한다 — 그 전에는 물러났다고 하고 남은 승리 수를 붙인다
        private static string WinLine(Lord opponent)
        {
            WorldState state = RunState.World.State;
            if (opponent != state.Coalition)
                return $"\"{RunState.NameOf(opponent)}\"의 세력은 멸망하였습니다!";
            return opponent.Alive ? "최후의 연합이 물러났습니다! " + BuildingTooltip.Highlight($"앞으로 {state.CoalitionWinsLeft}번") : "최후의 연합은 멸망하였습니다!";
        }

        // 지도 씬으로 — 결과 창의 「지도로」. 전투가 끝났으므로(Aftermath) 지도가 AI 턴을 돌린다
        public static void ToMap()
        {
            SceneManager.LoadScene(WorldManager.SceneName);
        }
    }
}
