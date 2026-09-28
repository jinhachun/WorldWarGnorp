using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 지금까지 모은 아웃게임 코인(판이 끝나도 남는 누적값)
    [RequireComponent(typeof(Text))]
    public class CoinLabel : MonoBehaviour
    {
        private Text _text;

        private void Awake()
        {
            _text = GetComponent<Text>();
        }

        private void Update()
        {
            _text.text = $"코인 {CoinField.Total}";
        }
    }
}
