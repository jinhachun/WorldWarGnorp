using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    public class BaseHealthBar : MonoBehaviour
    {
        [SerializeField] private Base _base;
        [Tooltip("채움 층. Image.type = Filled 여야 fillAmount가 먹는다")]
        [SerializeField] private Image _fill;

        private void Update()
        {
            _fill.fillAmount = _base.Hp01;
        }
    }
}
