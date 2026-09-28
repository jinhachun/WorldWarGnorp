using System.Collections;
using UnityEngine;

namespace GnorpWar
{
    // 타격감 연출 창구 — 파편 · 사망 먼지 · 화면 흔들림 · 히트스톱. 씬에 하나
    public class FxDirector : MonoBehaviour
    {
        // 히트스톱 동안의 시간 배율. 0이 아닌 값이라야 "누가 멈췄나"를 구분할 수 있다
        private const float HitStopFactor = 0.02f;

        [SerializeField] private ParticleSystem _hitSpark;
        [SerializeField] private ParticleSystem _deathPuff;
        [SerializeField] private CameraShake _cameraShake;
        [SerializeField] private FlameBit _flameBit;

        private bool _hitStopping;

        public static FxDirector Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // 맞은 지점에서 맞은 방향으로 부채꼴로 튄다 (프리팹 shape의 arc가 +X부터 반시계로 펼쳐지므로 절반만큼 돌린다)
        public void HitSpark(Vector2 position, Vector2 direction, Color color)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - _hitSpark.shape.arc * 0.5f;
            ParticleSystem spark = Instantiate(_hitSpark, position, Quaternion.Euler(0f, 0f, angle));
            ParticleSystem.MainModule main = spark.main;
            main.startColor = color;
            spark.Play();
        }

        public void DeathPuff(Vector2 position)
        {
            Instantiate(_deathPuff, position, Quaternion.identity).Play();
        }

        // 화염방사 — 입에서 앞으로 불꽃 조각을 뿜는다(한 번 부를 때 두 조각)
        private const float FlameSpeed = 9f;
        private const int FlameBitsPerPuff = 2;
        private const float FlameSpreadY = 0.35f;
        private static readonly Color FlameHot = new Color(1f, 0.85f, 0.3f);
        private static readonly Color FlameCool = new Color(1f, 0.45f, 0.15f);

        public void Flame(Vector2 mouth, float forward, float length)
        {
            for (int i = 0; i < FlameBitsPerPuff; i++)
            {
                FlameBit bit = Instantiate(_flameBit, mouth + new Vector2(0f, Random.Range(-FlameSpreadY, FlameSpreadY)), Quaternion.identity);
                bit.Launch(new Vector2(forward * FlameSpeed, Random.Range(-0.5f, 0.5f)), length / FlameSpeed, Color.Lerp(FlameHot, FlameCool, Random.value));
            }
        }

        public void Shake(float amount)
        {
            _cameraShake.Add(amount);
        }

        public void HitStop(float seconds)
        {
            // 판이 끝나 멈춘 상태(timeScale 0)는 건드리지 않는다
            if (_hitStopping || Time.timeScale <= 0f)
                return;
            StartCoroutine(HitStopRoutine(seconds));
        }

        private IEnumerator HitStopRoutine(float seconds)
        {
            _hitStopping = true;
            float saved = Time.timeScale;
            float frozen = saved * HitStopFactor;
            Time.timeScale = frozen;
            yield return new WaitForSecondsRealtime(seconds);
            // 그 사이 다른 누군가(결과 화면 등)가 시간을 바꿨으면 되돌리지 않는다
            if (Mathf.Approximately(Time.timeScale, frozen))
                Time.timeScale = saved;
            _hitStopping = false;
        }
    }
}
