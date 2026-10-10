using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    public class BaseHealthBar : MonoBehaviour
    {
        [SerializeField] private Base _base;
        [Tooltip("채움 층. Image.type = Filled 여야 fillAmount가 먹는다")]
        [SerializeField] private Image _fill;
        [Tooltip("현재 체력 숫자")]
        [SerializeField] private Text _hpText;

        private int _shownHp = -1;

        private void Update()
        {
            _fill.fillAmount = _base.Hp01;

            // 살아 있으면 1 이상으로 보이게 올림. 죽으면 음수 대신 0
            int hp = Mathf.Max(0, Mathf.CeilToInt(_base.Hp));
            if (hp != _shownHp)
            {
                _shownHp = hp;
                _hpText.text = hp.ToString();
            }
        }
    }
}
