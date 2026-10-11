using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 결과 창의 행복도·땅 줄 — 행복도는 위 줄과 같은 하트 줄(UI/HeartRow), 땅은 왕관 아이콘 + 글. 큰 글(승리!/패배...)과 내 결과 한 줄은 BattleManager가 쓴다
    public class ResultStats : MonoBehaviour
    {
        [Tooltip("하트 줄이 놓일 칸 — 왼쪽 가운데부터 채운다")]
        [SerializeField] private RectTransform _hearts;
        [SerializeField] private Sprite _heartIcon;
        [SerializeField] private Text _wins;
        [Tooltip("아이콘 줄 아래 회색 한 줄 — 이번 전투 길이(분:초)")]
        [SerializeField] private Text _time;

        private HeartRow _row;

        private void Update()
        {
            int seconds = (int)RampLabel.LastElapsed;
            _time.text = $"전투 시간 {seconds / 60}:{seconds % 60:00}";
            if (_row == null)
                _row = new HeartRow(_hearts, _heartIcon);
            _row.Show(0f);
            _wins.text = $"땅 {RunState.Lands}/{RunState.RegionCount}";
        }
    }
}
