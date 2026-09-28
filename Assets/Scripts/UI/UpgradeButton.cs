using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GnorpWar
{
    // 유닛 업그레이드 구매 버튼 (소환 버튼 아래 — UP 줄, 그 아래 UP2 줄). 판 안에서 자원으로 한 번 산다.
    // 마우스를 올리면 효과 설명 툴팁이 뜬다
    [RequireComponent(typeof(Button))]
    public class UpgradeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Unit _unitPrefab;
        [SerializeField] private PlayerWallet _wallet;
        [SerializeField] private Text _label;
        [Tooltip("켜면 유닛 정의의 두 번째 업그레이드(UP2)를 판다")]
        [SerializeField] private bool _second;

        private Button _button;

        private UnitDefinition Definition => _unitPrefab.Definition;
        private UpgradeKind Kind => _second ? Definition.Upgrade2 : Definition.Upgrade;
        private float Cost => _second ? Definition.Upgrade2Cost : Definition.UpgradeCost;
        private string Description => _second ? Definition.Upgrade2Description : Definition.UpgradeDescription;
        private bool Owned => UpgradeState.Instance.Has(Kind);

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Buy);
        }

        private void Update()
        {
            string name = _second ? "UP2" : "UP";
            _label.text = Owned ? $"{name} OK" : $"{name} {Mathf.FloorToInt(Cost)}";
            _button.interactable = !Owned && _wallet.Current >= Cost;
        }

        private void Buy()
        {
            if (Owned || !_wallet.TrySpend(Cost))
                return;
            UpgradeState.Instance.Buy(Kind);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UpgradeTooltip.Instance != null)
                UpgradeTooltip.Instance.Show(Description, (RectTransform)transform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (UpgradeTooltip.Instance != null)
                UpgradeTooltip.Instance.Hide();
        }
    }
}
