using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 골드 동전 — 골드를 얻고 내는 곳 전부가 이 동전 하나를 쓴다(사용자 11/1 — 인크리멘탈 게임식): 전투(이 컴포넌트가 GoldDrops를 듣는다) ·
    // 상점 판매 · 이벤트 방문 · 나라 소식 · 결과 장면의 건너뛰기(UI/WorldSidePanel.GainCoins · SpendCoins가 부른다).
    //   얻음(Gain): 나오는 곳에서 사방으로 튀어 올랐다 그 둘레 원 안에 떨어져 한 번 통통 → 잠깐 머묾(동전마다 엇갈려) → 점점 빨라지며 살짝 휜 길로 골드 칸에 빨려 들어감. 닿을 때마다 arrived(그 몫)
    //   냄(Spend): 골드 칸에서 하나씩 튀어나가고(나갈 때마다 left(그 몫)) → 목적지로 빨려 감
    // 보이는 것만이다 — 골드는 이미 들어가(빠져) 있다. 숫자는 받는 쪽이 날아가는 중인 몫을 빼고 보인다. 값은 BattleConfig 「손맛 — 골드 동전」, 시간은 실제 시간(Juice.RealDeltaTime)
    public class GoldCoinFx : MonoBehaviour
    {
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
            Gain(this, transform, _coin, Camera.main.WorldToScreenPoint(at), amount, () => _gold.Target, 0f, _gold.Arrive, null);
        }

        // UI 픽셀 한 칸(원본 1px = 화면 3px)
        private const float Cell = 3f;
        // 통통 — 튀어 오른 시간에 대한 비율
        private const float BounceTime = 0.5f;
        // 터짐 반지름이 그대로인 동전 수 — 넘으면 동전 수의 제곱근만큼 넓어진다
        private const float SpreadCoins = 6f;
        private static readonly System.Random Random = new System.Random(20261101);

        // 골드 → 동전 수 — 골드가 두 배가 될 때마다 「배마다 동전」개씩 늘고(로그), 상한에서 멈춘다. 골드보다 많지는 않다
        public static int Count(int gold)
        {
            BattleConfig config = RunState.Config;
            if (gold <= 0)
                return 0;
            int coins = 1 + Mathf.FloorToInt(config.CoinsPerDouble * Mathf.Log(gold, 2f));
            return Mathf.Clamp(coins, 1, Mathf.Min(gold, Mathf.Max(1, config.CoinsMax)));
        }

        // k번째 동전의 몫 — 고르게 나누고 남는 몫은 앞 동전부터 하나씩
        private static int Share(int gold, int coins, int k) => gold / coins + (k < gold % coins ? 1 : 0);

        // 얻음 — from(월드 좌표)에서 gold를 터뜨려 target으로. delay초 뒤 시작. 마지막 동전이 닿으면 done. 돌려주는 값 = 마지막 동전이 닿기까지의 시간(초, delay 포함)
        public static float Gain(MonoBehaviour host, Transform layer, Sprite sprite, Vector3 from, int gold, System.Func<Vector3> target, float delay, System.Action<int> arrived, System.Action done)
        {
            int coins = Count(gold);
            if (coins == 0 || host == null || !host.isActiveAndEnabled)
            {
                done?.Invoke();
                return 0f;
            }
            BattleConfig config = RunState.Config;
            float last = 0f;
            for (int k = 0; k < coins; k++)
            {
                int share = Share(gold, coins, k);
                bool end = k == coins - 1;
                // 머무는 시간이 동전마다 「간격」씩 늘어 차례로 빨려 간다
                float hold = config.CoinHold + k * config.CoinGap;
                float height = Range(config.CoinBurstHeightMin, config.CoinBurstHeightMax);
                // 앉는 자리 — 나온 곳을 둘러싼 원 안 아무 데나(가로만 퍼지면 한 줄로 늘어선다, 사용자 11/1). 루트로 원 안을 고르게.
                // 동전이 많으면 원도 넓힌다(동전 하나가 차지하는 넓이가 같게 — SpreadCoins개까지는 그대로)
                float angle = Range(0f, 2f * Mathf.PI);
                float radius = config.CoinBurstSpread * Mathf.Sqrt(Mathf.Max(1f, coins / SpreadCoins)) * Mathf.Sqrt((float)Random.NextDouble());
                float side = Mathf.Cos(angle) * radius, rise = Mathf.Sin(angle) * radius;
                last = Mathf.Max(last, delay + config.CoinBurstSeconds * (1f + BounceTime) + hold + config.CoinFlySeconds);
                host.StartCoroutine(Fly(layer, sprite, from, side, height, rise, true, hold, target, delay, null,
                    () => { arrived?.Invoke(share); if (end) done?.Invoke(); }, k % 2 == 0 ? 1f : -1f));
            }
            return last;
        }

        // 냄 — source(골드 칸, 월드 좌표)에서 동전이 「간격」마다 하나씩 튀어나가(나갈 때 left(그 몫)) to(k번째 동전의 목적지)로. 돌려주는 값 = 마지막 동전이 닿기까지(초, delay 포함)
        public static float Spend(MonoBehaviour host, Transform layer, Sprite sprite, System.Func<Vector3> source, int gold, System.Func<int, Vector3> to, float delay, System.Action<int> left, System.Action done)
        {
            int coins = Count(gold);
            if (coins == 0 || host == null || !host.isActiveAndEnabled)
            {
                left?.Invoke(gold);
                done?.Invoke();
                return 0f;
            }
            BattleConfig config = RunState.Config;
            float last = 0f;
            for (int k = 0; k < coins; k++)
            {
                int share = Share(gold, coins, k), index = k;
                bool end = k == coins - 1;
                float start = delay + k * config.CoinGap;
                // 아래로 떨어지며 튀어나간다(골드 칸은 화면 맨 위) — 오른 높이의 절반만큼 아래에 앉는다
                float height = Range(config.CoinBurstHeightMin, config.CoinBurstHeightMax) * 0.5f;
                float side = Range(-config.CoinBurstSpread, config.CoinBurstSpread) * 0.5f;
                last = Mathf.Max(last, start + config.CoinBurstSeconds + config.CoinFlySeconds);
                host.StartCoroutine(Fly(layer, sprite, source(), side, height, -height, false, 0f, () => to(index), start,
                    () => left?.Invoke(share), () => { if (end) done?.Invoke(); }, k % 2 == 0 ? 1f : -1f));
            }
            return last;
        }

        // 금액과 무관한 동전 하나(골드 칸에 닿지 않는 길 — 이주민이 통행세 상자에 넣는 동전)
        public static void Move(MonoBehaviour host, Transform layer, Sprite sprite, Vector3 from, Vector3 to, float delay)
        {
            BattleConfig config = RunState.Config;
            if (host == null || !host.isActiveAndEnabled)
                return;
            host.StartCoroutine(Fly(layer, sprite, from, 0f, config.CoinBurstHeightMin * 0.5f, 0f, false, 0f, () => to, delay, null, null, 1f));
        }

        private static float Range(float min, float max) => min + (max - min) * (float)Random.NextDouble();

        // 동전 하나 — delay 뒤 launched → from에서 (side칸 옆, height칸 위로) 튀었다 drop칸 아래에 앉고(bounce면 한 번 통통) → hold초 머묾 → target으로 점점 빨라지며 휜 길(bend = 휘는 쪽) → arrived
        private static IEnumerator Fly(Transform layer, Sprite sprite, Vector3 from, float side, float height, float drop, bool bounce, float hold,
            System.Func<Vector3> target, float delay, System.Action launched, System.Action arrived, float bend)
        {
            BattleConfig config = RunState.Config;
            for (float t = 0f; t < delay; t += Juice.RealDeltaTime)
                yield return null;
            launched?.Invoke();
            var coin = new GameObject("Coin", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            coin.raycastTarget = false;
            coin.sprite = sprite;
            coin.preserveAspect = true;
            // 노란 동전 — 색은 이펙트에만(아트 톤). 골드 숫자와 같은 노랑
            ColorUtility.TryParseHtmlString(BuildingTooltip.NumberColor, out Color yellow);
            coin.color = yellow;
            coin.rectTransform.SetParent(layer, false);
            coin.rectTransform.SetAsLastSibling();
            coin.SetNativeSize();
            // 칸 → 월드 길이(캔버스 배율) · 움직임은 세기 배율을 곱한다
            float cell = Cell * layer.lossyScale.x * config.JuiceIntensity;
            Vector3 land = from + new Vector3(side, drop, 0f) * cell;
            float up = height * cell, burst = Mathf.Max(0.01f, config.CoinBurstSeconds);
            // 튀어 오름 — 포물선으로 옆으로 퍼지며 land에 앉는다
            for (float t = 0f; t < burst && coin != null; t += Juice.RealDeltaTime)
            {
                float k = t / burst;
                Place(coin, Vector3.Lerp(from, land, k) + Vector3.up * (4f * up * k * (1f - k)));
                yield return null;
            }
            // 통통 — 한 번 작게
            if (bounce)
                for (float t = 0f; t < burst * BounceTime && coin != null; t += Juice.RealDeltaTime)
                {
                    float k = t / (burst * BounceTime);
                    Place(coin, land + Vector3.up * (4f * up * config.CoinBounce * k * (1f - k)));
                    yield return null;
                }
            if (coin != null)
                Place(coin, land);
            for (float t = 0f; t < hold; t += Juice.RealDeltaTime)
                yield return null;
            // 빨려 들어감 — 점점 빨라지고(ease-in), 길의 가운데가 옆으로 휜다
            float fly = Mathf.Max(0.01f, config.CoinFlySeconds);
            for (float t = 0f; t < fly && coin != null; t += Juice.RealDeltaTime)
            {
                float k = t / fly;
                Vector3 to = target();
                Vector3 path = to - land;
                Vector3 normal = new Vector3(-path.y, path.x, 0f) * (config.CoinCurve * config.JuiceIntensity * bend);
                Place(coin, Vector3.LerpUnclamped(land, to, k * k) + normal * Mathf.Sin(Mathf.PI * k));
                yield return null;
            }
            arrived?.Invoke();
            if (coin != null)
                Destroy(coin.gameObject);
        }

        // 화면 픽셀에 맞춘다(점이 번지지 않게)
        private static void Place(Image coin, Vector3 at) => coin.rectTransform.position = new Vector3(Mathf.Round(at.x), Mathf.Round(at.y), at.z);
    }
}
