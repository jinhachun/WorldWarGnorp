using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 전투 화면의 내 골드 칸(사용자 10/20) — 동전 아이콘 + 지금 골드. 전투 중에 얻은 골드(주식시장)는 이미 런 골드에 들어가 있지만,
    // 날아오는 동전(Fx/GoldCoinFx)이 닿을 때 숫자가 오르도록 날아오는 중인 몫을 빼고 보인다.
    // 자리는 내 업그레이드 줄(UI/BaseUpgradeRow) 바로 아래 — 줄 수가 달라도 겹치지 않게. 그 높이에서 내 타워 UI(UI/BoardView — 탑이 화면 밖일 때 왼쪽 끝에 서는 층 칸)와
    // 겹치면 그 오른쪽으로 민다(사용자 10/22: 8층 탑과 겹침 — 업그레이드 줄과 같은 방식)
    public class BattleGoldView : MonoBehaviour
    {
        // 업그레이드 줄과 띄우는 간격 · 타워 UI와 띄우는 여백(px — BaseUpgradeRow와 같은 12)
        private const float RowGap = 12f;
        private const float TowerGap = 12f;

        [SerializeField] private Image _icon;
        [SerializeField] private Text _label;
        [Tooltip("내 업그레이드 줄 — 이 칸은 그 아래 끝에 붙는다")]
        [SerializeField] private BaseUpgradeRow _row;
        [Tooltip("내 타워 UI — 겹치면 이 칸을 그 오른쪽으로 민다")]
        [SerializeField] private BoardView _tower;

        // 날아오는 중인 골드(아직 숫자에 안 보인다)
        private int _inFlight;
        private int _shown = int.MinValue;
        private float _homeX = float.NaN;

        // 동전이 날아갈 자리(화면 좌표)
        public Vector3 Target => _icon.rectTransform.position;

        public void Launch(int amount) => _inFlight += amount;

        public void Arrive(int amount) => _inFlight = Mathf.Max(0, _inFlight - amount);

        private void LateUpdate()
        {
            Place();
            if (!RunState.Started)
                return;
            int gold = RunState.Gold - _inFlight;
            if (gold == _shown)
                return;
            _shown = gold;
            _label.text = gold.ToString();
        }

        // Overlay 캔버스라 월드 좌표 = 화면 픽셀. 칸의 기준점은 왼쪽 위
        private void Place()
        {
            var rect = (RectTransform)transform;
            // 제자리(씬에 놓인 가로 자리)를 먼저 놓고 거기서 민다
            if (float.IsNaN(_homeX))
                _homeX = rect.anchoredPosition.x;
            rect.anchoredPosition = new Vector2(_homeX, rect.anchoredPosition.y);
            float scale = rect.lossyScale.y;
            float top = _row != null ? _row.ScreenBottom - RowGap * scale : rect.position.y;
            float x = rect.position.x;
            if (_tower != null && _tower.ScreenBounds(out Rect tower))
            {
                float bottom = top - rect.rect.height * scale;
                if (tower.yMax > bottom && tower.yMin < top)
                    x = Mathf.Max(x, tower.xMax + TowerGap * scale);
            }
            rect.position = new Vector3(x, top, rect.position.z);
        }
    }
}
