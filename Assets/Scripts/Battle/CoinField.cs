using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 아웃게임 코인 — 적(유닛·기지)이 맞을 때마다 피해량에 비례해 튀어나와 바닥에 떨어지고,
    // 땅에 선 아군이 닿으면 줍는다. 주운 코인은 판이 끝나도 남는다(PlayerPrefs).
    // 코인이 많이 나오므로 물리 몸체를 쓰지 않는다 — 이 컴포넌트 하나가 전부 움직이고(포물선 → 바닥에서 멈춤),
    // 오브젝트는 풀로 돌리고, 줍기는 가로 칸(버킷)으로 찾는다. 겹쳐 떨어진 코인은 하나로 합친다(값은 더함).
    public class CoinField : MonoBehaviour
    {
        private const string SaveKey = "Coins";
        // 튀어나오는 속도 — 소닉 링처럼 위쪽 부채꼴로 흩어진다
        private const float PopSpeedMin = 3f;
        private const float PopSpeedMax = 7f;
        private const float PopHalfAngle = 70f;
        private const float Gravity = 20f;
        // 가로 칸 폭 = 합치는 거리. 코인 그림 폭과 같아서 합쳐도 보이는 모습이 거의 같다
        private const float BucketWidth = 0.375f;
        // 줍기 검사 띠의 높이 — 바닥에 선 몸만 닿는다
        private const float PickupBandHeight = 0.2f;
        private static readonly ContactFilter2D SolidOnly = new ContactFilter2D { useTriggers = false };

        [SerializeField] private BattleConfig _config;
        [SerializeField] private Sprite _sprite;
        [Tooltip("코인이 떨어져 멈추는 높이(바닥 윗면)")]
        [SerializeField] private float _groundY = -4f;
        [Tooltip("코인이 떨어질 수 있는 가로 범위 — 두 기지 앞면 사이")]
        [SerializeField] private float _minX = -33.5f;
        [SerializeField] private float _maxX = 33.5f;

        private class Coin
        {
            public Transform Transform;
            public Vector2 Position;
            public Vector2 Velocity;
            public int Value;
        }

        private readonly List<Coin> _flying = new List<Coin>();
        private readonly Stack<Coin> _pool = new Stack<Coin>();
        // 가로 칸마다 누운 코인 하나(없으면 null)
        private Coin[] _resting;
        private readonly List<Collider2D> _touching = new List<Collider2D>();
        private float _halfSize;
        private float _pending;

        public static CoinField Instance { get; private set; }
        public static int Total { get; private set; }

        private void Awake()
        {
            Instance = this;
            Total = PlayerPrefs.GetInt(SaveKey, 0);
            _halfSize = _sprite.bounds.extents.y;
            _resting = new Coin[Mathf.CeilToInt((_maxX - _minX) / BucketWidth) + 1];
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // 피해를 입은 쪽이 적이면 피해량 × 비율만큼 코인이 튀어나온다. 소수점은 다음 타격으로 넘긴다
        public void OnDamaged(Team team, Vector2 position, float amount)
        {
            if (team != Team.Enemy || amount <= 0f)
                return;

            _pending += amount * _config.CoinsPerDamage;
            int count = Mathf.FloorToInt(_pending);
            _pending -= count;
            for (int i = 0; i < count; i++)
            {
                Coin coin = Rent();
                coin.Position = position;
                coin.Value = 1;
                float angle = (90f + Random.Range(-PopHalfAngle, PopHalfAngle)) * Mathf.Deg2Rad;
                coin.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(PopSpeedMin, PopSpeedMax);
                coin.Transform.position = position;
                _flying.Add(coin);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float restY = _groundY + _halfSize;
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                Coin coin = _flying[i];
                coin.Velocity.y -= Gravity * dt;
                coin.Position += coin.Velocity * dt;
                coin.Position.x = Mathf.Clamp(coin.Position.x, _minX, _maxX);
                if (coin.Position.y <= restY)
                {
                    coin.Position.y = restY;
                    _flying.RemoveAt(i);
                    Land(coin);
                }
                coin.Transform.position = coin.Position;
            }
        }

        private void FixedUpdate()
        {
            // 바닥 바로 위 띠에 닿은 몸 중 살아 있는 아군만 — 그 몸의 가로 범위에 누운 코인을 줍는다
            Vector2 center = new Vector2((_minX + _maxX) * 0.5f, _groundY + PickupBandHeight * 0.5f);
            Vector2 size = new Vector2(_maxX - _minX, PickupBandHeight);
            Physics2D.OverlapBox(center, size, 0f, SolidOnly, _touching);
            int picked = 0;
            foreach (Collider2D col in _touching)
            {
                if (!col.TryGetComponent(out Unit unit) || unit.Team != Team.Ally || !unit.IsAlive)
                    continue;
                Bounds bounds = col.bounds;
                int from = BucketOf(bounds.min.x - _halfSize);
                int to = BucketOf(bounds.max.x + _halfSize);
                for (int b = from; b <= to; b++)
                {
                    Coin coin = _resting[b];
                    if (coin == null || coin.Position.x + _halfSize < bounds.min.x || coin.Position.x - _halfSize > bounds.max.x)
                        continue;
                    picked += coin.Value;
                    _resting[b] = null;
                    Return(coin);
                }
            }
            if (picked > 0)
            {
                Total += picked;
                PlayerPrefs.SetInt(SaveKey, Total);
            }
        }

        // 같은 칸에 이미 누운 코인이 있으면 합친다 — 누운 코인 수가 전장 폭 / 칸 폭을 넘지 않는다
        private void Land(Coin coin)
        {
            int b = BucketOf(coin.Position.x);
            if (_resting[b] != null)
            {
                _resting[b].Value += coin.Value;
                Return(coin);
                return;
            }
            _resting[b] = coin;
        }

        private int BucketOf(float x)
        {
            return Mathf.Clamp(Mathf.FloorToInt((x - _minX) / BucketWidth), 0, _resting.Length - 1);
        }

        private Coin Rent()
        {
            if (_pool.Count > 0)
            {
                Coin pooled = _pool.Pop();
                pooled.Transform.gameObject.SetActive(true);
                return pooled;
            }
            var go = new GameObject("Coin");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            // 유닛(몸 0 · 무기 1) 앞에 그린다 — 산을 통과해 튀는 동안에도 보여야 한다
            sr.sortingOrder = 2;
            return new Coin { Transform = go.transform };
        }

        private void Return(Coin coin)
        {
            coin.Transform.gameObject.SetActive(false);
            _pool.Push(coin);
        }
    }
}
