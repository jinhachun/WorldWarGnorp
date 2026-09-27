using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    [RequireComponent(typeof(Text))]
    public class ResourceLabel : MonoBehaviour
    {
        [SerializeField] private PlayerWallet _wallet;

        private Text _text;

        private void Awake()
        {
            _text = GetComponent<Text>();
        }

        private void Update()
        {
            _text.text = $"{Mathf.FloorToInt(_wallet.Current)} / {Mathf.FloorToInt(_wallet.Max)}";
        }
    }
}
