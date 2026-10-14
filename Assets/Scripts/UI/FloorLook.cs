using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 탑 층 그림 한 벌 — 벽 · 창틀 · 창 불빛. 전장 탑(Tower)과 같은 모양(사용자 10/9).
    // 벽 재질·창 모양은 기물 국가: 킹덤 = 돌벽돌 + 아치 창 · 티크톡 = 리벳 철판 + 톱니 둘린 둥근 창 · 트라이브 = 통나무 말뚝 + 뼈 매듭 끈 + 아치 창 · 공용·빈 층 = 민 벽 + 아치 창.
    // 창틀 색(등급)은 부르는 쪽이 칠한다. UI 탑 층(SlotView) · 상점 진열 카드(OfferCard) · 끌기 고스트(ShopPanel)가 같이 쓴다
    public class FloorLook : MonoBehaviour
    {
        // 창 불빛 — 전장 탑(Tower)과 같은 색·은은한 알파
        private static readonly Color LightColor = new Color(1f, 0.7f, 0.2f);
        private const float DimLight = 0.4f;

        [SerializeField] private Image _wall;
        [Tooltip("창틀 — 흰 그림, 등급 색은 부르는 쪽이 칠한다")]
        [SerializeField] private Image _window;
        [Tooltip("창 불빛 — 창 안쪽 그림, Filled(Vertical · 아래 기준)")]
        [SerializeField] private Image _light;
        [Tooltip("벽 — 공용·빈 층 · 킹덤 · 티크톡 · 트라이브")]
        [SerializeField] private Sprite _plainWall;
        [SerializeField] private Sprite _brickWall;
        [SerializeField] private Sprite _ironWall;
        [SerializeField] private Sprite _tribeWall;
        [Tooltip("창틀 — 아치 창 · 톱니 둘린 둥근 창(티크톡)")]
        [SerializeField] private Sprite _archWindow;
        [SerializeField] private Sprite _gearWindow;
        [Tooltip("창 불빛 — 아치 창 · 둥근 창")]
        [SerializeField] private Sprite _archLight;
        [SerializeField] private Sprite _roundLight;

        // 벽의 제 색(처음 그릴 때 읽는다) — 꺼진 층은 여기에 회색을 곱한다
        private Color? _wallColor;

        // building = null이면 빈 층(꺼진 창). charge = 쿨다운이 찬 비율(전투 중 쿨다운 기물 — 창이 아래부터 밝아진다), 음수면 은은히 다 켜짐.
        // stopped = 맵이 작동을 끈 층(Fx/StoppedMark) — 창이 꺼지고 벽이 회색이 된다
        public void Show(BuildingDefinition building, float charge = -1f, bool stopped = false)
        {
            BuildingNation nation = building != null ? building.Nation : BuildingNation.Common;
            bool iron = nation == BuildingNation.TickTock;
            _wall.sprite = iron ? _ironWall : nation == BuildingNation.Kingdom ? _brickWall
                : nation == BuildingNation.Tribe && _tribeWall != null ? _tribeWall : _plainWall;
            _window.sprite = iron ? _gearWindow : _archWindow;
            _light.sprite = iron ? _roundLight : _archLight;
            _light.enabled = building != null && !stopped;
            _wallColor ??= _wall.color;
            _wall.color = stopped ? _wallColor.Value * StoppedMark.Gray : _wallColor.Value;
            _light.fillAmount = charge >= 0f ? Mathf.Clamp01(charge) : 1f;
            _light.color = new Color(LightColor.r, LightColor.g, LightColor.b, charge >= 0f ? 1f : DimLight);
        }
    }
}
