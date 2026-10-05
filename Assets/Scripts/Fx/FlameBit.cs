using UnityEngine;

namespace GnorpWar
{
    // 화염방사 그림 한 조각 — 앞으로 날아가며 커지고 흐려진 뒤 사라진다. 충돌 없음(피해는 Unit이 띠로 준다)
    [RequireComponent(typeof(SpriteRenderer))]
    public class FlameBit : MonoBehaviour
    {
        private const float GrowTo = 2.2f;

        private SpriteRenderer _renderer;
        private Vector2 _velocity;
        private float _life;
        private float _age;
        private Color _color;
        private Vector3 _startScale;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _startScale = transform.localScale;
        }

        public void Launch(Vector2 velocity, float life, Color color)
        {
            _velocity = velocity;
            _life = life;
            _age = 0f;   // 풀에서 다시 꺼낸 조각일 수 있다(Pooled)
            _color = color;
            _renderer.color = color;
            _renderer.flipX = velocity.x < 0f;   // 그림은 오른쪽으로 번지게 그려져 있다
            transform.localScale = _startScale;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / _life;
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }
            transform.position += (Vector3)(_velocity * Time.deltaTime);
            transform.localScale = _startScale * Mathf.Lerp(1f, GrowTo, t);
            _renderer.color = new Color(_color.r, _color.g * (1f - 0.5f * t), _color.b, 1f - t);
        }
    }
}
