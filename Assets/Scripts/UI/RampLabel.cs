using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 화면 위 가운데 — 전투 시간과 데스매치 단계. 단계가 넘어갈 때마다 잠깐 커졌다 돌아온다
    [RequireComponent(typeof(Text))]
    public class RampLabel : MonoBehaviour
    {
        private const float PulseScale = 1.4f;
        private const float PulseDuration = 0.3f;
        private const float DeathmatchWarning = 30f; // 데스매치 몇 초 전부터 남은 초를 보일지 (표시용)

        private Text _text;
        private int _lastPhase;
        private float _pulse = PulseDuration;

        private void Awake() => _text = GetComponent<Text>();

        private void Update()
        {
            if (!BattleManager.Fighting)
            {
                _text.text = "";
                return;
            }

            float elapsed = BattleManager.Elapsed;

            // 0 평소 · 1 데스매치 카운트다운 · 2 데스매치(기물은 아직 발동) · 3 데스매치(기물 멈춤)
            // 남은 초는 올림 — 내림한 시계와 더하면 그 시각이 되고(2:41 + 19초 = 3:00), 0초는 단계가 넘어가 안 보인다
            float toDeathmatch = BattleManager.DeathmatchAt - elapsed;
            float toBoardStop = BattleManager.BoardStopAt - elapsed;
            int phase = toBoardStop <= 0f ? 3 : toDeathmatch <= 0f ? 2 : toDeathmatch <= DeathmatchWarning ? 1 : 0;
            if (phase > _lastPhase)
                _pulse = 0f;
            _lastPhase = phase;
            string deathmatch = phase switch
            {
                1 => $"   데스매치까지 {Mathf.CeilToInt(toDeathmatch)}초",
                2 => $"   데스매치 · 생산 종료까지 {Mathf.CeilToInt(toBoardStop)}초",
                3 => "   데스매치",
                _ => "",
            };

            // 3:00~3:10 줄은 길어서 시계를 빼야 두 보드 사이에 다 보인다 (사용자 결정)
            string time = phase == 2 ? "" : $"{(int)elapsed / 60}:{(int)elapsed % 60:00}";
            _text.text = (time + deathmatch).Trim();

            _pulse += Time.deltaTime;
            float t = Mathf.Clamp01(_pulse / PulseDuration);
            transform.localScale = Vector3.one * Mathf.Lerp(PulseScale, 1f, t);
        }
    }
}
