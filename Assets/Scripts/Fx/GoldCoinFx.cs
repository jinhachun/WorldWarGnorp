using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 전투 중 내가 골드를 얻으면(GoldDrops — 주식시장) 그 자리에서 노란 동전이 톡 튀어 올랐다가 화면 위 골드 칸(UI/BattleGoldView)으로 날아가 닿는다.
    // 닿는 순간 골드 숫자가 오른다. 보이는 것만이다 — 골드는 이미 들어가 있다. 작게(프로토타입)
    public class GoldCoinFx : MonoBehaviour
    {
        private const float PopSeconds = 0.25f;
        private const float PopHeight = 40f;
        private const float FlySeconds = 0.4f;

        [SerializeField] private BattleGoldView _gold;
        [Tooltip("동전 그림 — 원본 크기")]
        [SerializeField] private Sprite _coin;

        private void OnEnable() => GoldDrops.Dropped += OnDropped;
        private void OnDisable() => GoldDrops.Dropped -= OnDropped;

        private void OnDropped(Team team, Vector2 at, int amount)
        {
            if (team != Team.Ally || Camera.main == null)
                return;
            _gold.Launch(amount);
            // 골드 하나에 동전 하나(주식시장 신화 3 — 시차를 두고) · 많으면 마지막 동전이 남은 몫을 가져간다
            int coins = Mathf.Clamp(amount, 1, MaxCoins);
            Vector3 from = Camera.main.WorldToScreenPoint(at);
            for (int k = 0; k < coins; k++)
                StartCoroutine(Fly(from, k < coins - 1 ? 1 : amount - (coins - 1), k * CoinGap));
        }

        private const int MaxCoins = 5;
        private const float CoinGap = 0.08f;

        private IEnumerator Fly(Vector3 from, int amount, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
            var coin = new GameObject("Coin", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            coin.raycastTarget = false;
            coin.sprite = _coin;
            coin.preserveAspect = true;
            // 노란 동전 — 색은 이펙트에만(아트 톤). 골드 숫자와 같은 노랑
            ColorUtility.TryParseHtmlString(BuildingTooltip.NumberColor, out Color yellow);
            coin.color = yellow;
            coin.rectTransform.SetParent(transform, false);
            coin.SetNativeSize();
            coin.rectTransform.position = from;
            // 톡 — 위로 튀었다 살짝 내려앉는다
            for (float t = 0f; t < PopSeconds && coin != null; t += Time.unscaledDeltaTime)
            {
                float k = t / PopSeconds;
                coin.rectTransform.position = from + Vector3.up * (PopHeight * 4f * k * (1f - k) + PopHeight * 0.5f * k);
                yield return null;
            }
            // 골드 칸으로 — 점점 빨라진다
            Vector3 start = coin.rectTransform.position;
            for (float t = 0f; t < FlySeconds && coin != null; t += Time.unscaledDeltaTime)
            {
                float k = t / FlySeconds;
                coin.rectTransform.position = Vector3.Lerp(start, _gold.Target, k * k);
                yield return null;
            }
            _gold.Arrive(amount);
            if (coin != null)
                Destroy(coin.gameObject);
        }
    }
}
