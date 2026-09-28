using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 자원 획득 레벨 강화 버튼
    [RequireComponent(typeof(Button))]
    public class IncomeUpgradeButton : MonoBehaviour
    {
        [SerializeField] private PlayerWallet _wallet;
        [SerializeField] private Text _levelText;
        [SerializeField] private Text _costText;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(() => _wallet.TryUpgradeIncome());
        }

        private void Update()
        {
            _levelText.text = $"Lv {_wallet.IncomeLevel + 1}";
            _costText.text = _wallet.CanUpgradeIncome ? Mathf.FloorToInt(_wallet.IncomeUpgradeCost).ToString() : "MAX";
            _button.interactable = _wallet.CanUpgradeIncome && _wallet.Current >= _wallet.IncomeUpgradeCost;
        }
    }
}
