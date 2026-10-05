using Unity.Profiling;
using UnityEngine;

namespace GnorpWar
{
    // 측정 전용 — 2초(실제 시간)마다 프레임당 평균 시간을 구간별로 콘솔 [Perf]에 남긴다.
    // 씬에는 두지 않는다. 플레이 중 Battle에 붙인다
    public class PerfProbe : MonoBehaviour
    {
        private const float Period = 2f;
        // 재는 구간(Unity 내부 표시 이름) — 스크립트 FixedUpdate · 2D 물리 · 스크립트 Update · 그리기
        private static readonly string[] Markers = { "FixedBehaviourUpdate", "Physics2D.Simulate", "BehaviourUpdate", "Unit.Contacts", "Unit.Scan", "Unit.Support", "Unit.Walk" };

        private ProfilerRecorder[] _recorders;
        private float _timer;
        private int _frames;
        private int _fixedSteps;
        private float _worstFrame;
        private readonly double[] _sums = new double[Markers.Length];

        private void OnEnable()
        {
            _recorders = new ProfilerRecorder[Markers.Length];
            for (int i = 0; i < Markers.Length; i++)
                _recorders[i] = ProfilerRecorder.StartNew(Markers[i].StartsWith("Unit.") ? ProfilerCategory.Scripts : ProfilerCategory.Internal, Markers[i]);
        }

        private void OnDisable()
        {
            foreach (ProfilerRecorder r in _recorders)
                r.Dispose();
        }

        private void FixedUpdate() => _fixedSteps++;

        private void Update()
        {
            _frames++;
            _worstFrame = Mathf.Max(_worstFrame, Time.unscaledDeltaTime);
            for (int i = 0; i < _recorders.Length; i++)
                _sums[i] += _recorders[i].LastValue * 1e-6; // ns → ms

            _timer += Time.unscaledDeltaTime;
            if (_timer < Period)
                return;

            int allies = 0, enemies = 0;
            foreach (Unit unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.Team == Team.Ally) allies++; else enemies++;
            var line = new System.Text.StringBuilder();
            line.Append($"[Perf] {Time.timeSinceLevelLoad:F0}s units {allies}+{enemies} · fps {_frames / _timer:F1} · worst {_worstFrame * 1000f:F0}ms · physSteps/frame {(float)_fixedSteps / _frames:F1}");
            for (int i = 0; i < Markers.Length; i++)
                line.Append(_recorders[i].Valid ? $" · {Markers[i]} {_sums[i] / _frames:F1}ms" : $" · {Markers[i]} (못 찾음)");
            Debug.Log(line.ToString());

            _timer = 0f;
            _frames = 0;
            _fixedSteps = 0;
            _worstFrame = 0f;
            System.Array.Clear(_sums, 0, _sums.Length);
        }
    }
}
