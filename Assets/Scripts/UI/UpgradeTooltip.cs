using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 업그레이드 설명 툴팁 — UP 버튼에 마우스를 올린 동안 그 버튼 바로 아래에 뜬다. 씬에 하나
    public class UpgradeTooltip : MonoBehaviour
    {
        [SerializeField] private Text _text;
        [Tooltip("버튼 아래쪽 끝과 툴팁 사이 간격(px)")]
        [SerializeField] private float _gap = 10f;

        public static UpgradeTooltip Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Show(string description, RectTransform button)
        {
            _text.text = description;
            var rt = (RectTransform)transform;
            // 버튼의 아래쪽 가운데(월드 좌표)에 툴팁 위쪽 가운데를 맞춘다
            Vector3[] corners = new Vector3[4];
            button.GetWorldCorners(corners);
            rt.position = (corners[0] + corners[3]) * 0.5f + Vector3.down * _gap * rt.lossyScale.y;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
