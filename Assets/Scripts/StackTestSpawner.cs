using UnityEngine;

namespace GnorpWar
{
    // 검증용 임시 스포너 — 소환 시스템·적 AI가 생기면 지운다
    public class StackTestSpawner : MonoBehaviour
    {
        [SerializeField] private Unit _unitPrefab;
        [SerializeField] private float _interval = 0.6f;
        [SerializeField] private int _maxCount = 30;

        private float _timer;
        private int _spawned;

        private void Update()
        {
            if (_spawned >= _maxCount)
                return;

            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;

            _timer = _interval;
            Instantiate(_unitPrefab, transform.position, Quaternion.identity);
            _spawned++;
        }
    }
}
