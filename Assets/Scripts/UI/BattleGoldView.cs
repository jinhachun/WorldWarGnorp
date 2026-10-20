using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 전투 화면의 내 골드 칸(사용자 10/20) — 동전 아이콘 + 지금 골드. 전투 중에 얻은 골드(주식시장)는 이미 런 골드에 들어가 있지만,
    // 날아오는 동전(Fx/GoldCoinFx)이 닿을 때 숫자가 오르도록 날아오는 중인 몫을 빼고 보인다
    public class BattleGoldView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _label;

        // 날아오는 중인 골드(아직 숫자에 안 보인다)
        private int _inFlight;
        private int _shown = int.MinValue;

        // 동전이 날아갈 자리(화면 좌표)
        public Vector3 Target => _icon.rectTransform.position;

        public void Launch(int amount) => _inFlight += amount;

        public void Arrive(int amount) => _inFlight = Mathf.Max(0, _inFlight - amount);

        private void LateUpdate()
        {
            if (!RunState.Started)
                return;
            int gold = RunState.Gold - _inFlight;
            if (gold == _shown)
                return;
            _shown = gold;
            _label.text = gold.ToString();
        }
    }
}
