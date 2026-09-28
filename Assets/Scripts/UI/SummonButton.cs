using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    [RequireComponent(typeof(Button))]
    public class SummonButton : MonoBehaviour
    {
        [SerializeField] private Unit _unitPrefab;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private PlayerWallet _wallet;
        [Tooltip("쿨다운 동안 덮는 층. Image.type = Filled 여야 fillAmount가 먹는다")]
        [SerializeField] private Image _cooldownFill;
        [SerializeField] private Text _costText;

        private Button _button;
        private float _cooldown;

        private UnitDefinition Definition => _unitPrefab.Definition;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Summon);
            _costText.text = Mathf.FloorToInt(Definition.Cost).ToString();
        }

        private void Update()
        {
            _cooldown -= Time.deltaTime;
            _cooldownFill.fillAmount = Mathf.Clamp01(_cooldown / Definition.SummonCooldown);
            _button.interactable = _cooldown <= 0f && _wallet.Current >= Definition.Cost;
        }

        private void Summon()
        {
            if (_cooldown > 0f || !_wallet.TrySpend(Definition.Cost))
                return;

            Unit.Spawn(_unitPrefab, _spawnPoint.position);
            _cooldown = Definition.SummonCooldown;
        }
    }
}
