using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 결과 창의 행복도·땅 줄 — 상점 상태 줄과 같은 아이콘 줄(하트 · 왕관). 큰 글(승리!/패배...)과 요약 글은 BattleManager가 쓴다
    public class ResultStats : MonoBehaviour
    {
        [SerializeField] private Text _lives;
        [SerializeField] private Text _wins;
        [Tooltip("아이콘 줄 아래 회색 한 줄 — 이번 전투 길이(분:초)")]
        [SerializeField] private Text _time;

        private void Update()
        {
            int seconds = (int)RampLabel.LastElapsed;
            _time.text = $"전투 시간 {seconds / 60}:{seconds % 60:00}";
            _time.enabled = RunState.LastOutcome != RunState.Outcome.None;   // 쉬는 턴에는 전투가 없었다
            _lives.text = $"행복도 {RunState.Happiness}/{RunState.HappinessMax}";
            _wins.text = $"땅 {RunState.Lands}/{RunState.RegionCount}";
        }
    }
}
