using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 유닛 업그레이드 구매 버튼 (소환 버튼 바로 아래). 판 안에서 자원으로 한 번 산다
    [RequireComponent(typeof(Button))]
    public class UpgradeButton : MonoBehaviour
    {
        [SerializeField] private Unit _unitPrefab;
        [SerializeField] private PlayerWallet _wallet;
        [SerializeField] private Text _label;

        private Button _button;

        private UnitDefinition Definition => _unitPrefab.Definition;
        private bool Owned => UpgradeState.Instance.Has(Definition);

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Buy);
        }

        private void Update()
        {
            _label.text = Owned ? "UP OK" : $"UP {Mathf.FloorToInt(Definition.UpgradeCost)}";
            _button.interactable = !Owned && _wallet.Current >= Definition.UpgradeCost;
        }

        private void Buy()
        {
            if (Owned || !_wallet.TrySpend(Definition.UpgradeCost))
                return;
            UpgradeState.Instance.Buy(Definition);
        }
    }
}
