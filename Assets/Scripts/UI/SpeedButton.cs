using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 전투 배속 — 누를 때마다 ×1 → ×2 → ×4 → ×1. 고른 배속은 런 동안(라운드가 바뀌어도) 유지된다.
    // 전투 중에만 보인다(BattleManager가 켜고 끈다)
    [RequireComponent(typeof(Button))]
    public class SpeedButton : MonoBehaviour
    {
        private static readonly float[] Speeds = { 1f, 2f, 4f };
        private static int _index;

        [SerializeField] private Text _label;

        public static float Current => Speeds[_index];

        // 플레이 모드를 새로 켤 때 ×1부터
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _index = 0;

        private void Awake() => GetComponent<Button>().onClick.AddListener(Next);

        private void OnEnable() => _label.text = $"×{Current:0}";

        private void Next()
        {
            _index = (_index + 1) % Speeds.Length;
            _label.text = $"×{Current:0}";
            if (BattleManager.Fighting)
                Time.timeScale = Current;
        }
    }
}
