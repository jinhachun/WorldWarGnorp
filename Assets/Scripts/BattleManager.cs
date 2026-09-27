using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GnorpWar
{
    // 승패 판정 — 어느 기지든 먼저 무너지면 판이 끝난다
    public class BattleManager : MonoBehaviour
    {
        [SerializeField] private Base _allyBase;
        [SerializeField] private Base _enemyBase;
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private Text _resultText;
        [SerializeField] private Button _retryButton;

        private bool _over;

        private void Awake()
        {
            // 결과 화면에서 멈춘 채 다시하기로 들어왔을 수 있다
            Time.timeScale = 1f;
            _resultPanel.SetActive(false);
            _allyBase.Destroyed += OnBaseDestroyed;
            _enemyBase.Destroyed += OnBaseDestroyed;
            _retryButton.onClick.AddListener(Retry);
        }

        private void OnBaseDestroyed(Base destroyed)
        {
            if (_over)
                return;

            _over = true;
            Debug.Log($"[Battle] {(destroyed.Team == Team.Enemy ? "승리" : "패배")} — {Time.timeSinceLevelLoad:F1}초");
            _resultText.text = destroyed.Team == Team.Enemy ? "승리!" : "패배...";
            _resultPanel.SetActive(true);
            Time.timeScale = 0f;
        }

        private void Retry()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
