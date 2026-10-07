using UnityEngine;

namespace GnorpWar
{
    // 측정 전용 — 5초마다 아군끼리 위아래로 맞닿은 쌍을 세고, 그중 층 순서가 뒤집힌 쌍(아래층 역할이 위)을 콘솔 [Stack]에 남긴다.
    // 씬에는 두지 않는다. 플레이 중 Battle에 붙인다
    public class StackOrderProbe : MonoBehaviour
    {
        private const float Period = 5f;
        // 아군 맨 앞 유닛에서 이 거리 안이면 "전선 앞" — 앞이 적으로 막혀 내려갈 곳이 없는 자리
        private const float NearFront = 2f;
        private readonly ContactPoint2D[] _contacts = new ContactPoint2D[16];
        private float _timer;
        // 산 밑변 — 몸(가로 범위)끼리 이 빈틈 이하면 한 산
        private const float PileGap = 0.3f;
        private readonly System.Collections.Generic.List<Vector2> _pileSpans = new System.Collections.Generic.List<Vector2>();

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;
            _timer = Period;

            Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            float front = float.MinValue;
            Unit frontUnit = null;
            foreach (Unit unit in units)
                if (unit.Team == Team.Ally && unit.IsAlive && unit.transform.position.x > front)
                {
                    front = unit.transform.position.x;
                    frontUnit = unit;
                }

            int pairs = 0, flipped = 0, flippedNearFront = 0, allies = 0;
            // 산 높이 — 땅에서 발까지 가장 높이 올라간 아군(칸)
            float maxHeight = 0f;
            float topX = 0f;
            // 산 — 유닛 위에 서 있는 아군과 그 발판만 센다(넉백·점프로 떠 있는 유닛은 빼고).
            // 높이는 그중 가장 높은 발, 밑변 폭은 그 꼭대기와 이어진(몸 사이 빈틈 PileGap 이하) 무리의 가로 길이
            _pileSpans.Clear();
            foreach (Unit unit in units)
            {
                if (unit.Team != Team.Ally || !unit.IsAlive)
                    continue;
                allies++;
                Bounds body = unit.GetComponent<BoxCollider2D>().bounds;
                int count = unit.GetComponent<Rigidbody2D>().GetContacts(_contacts);
                for (int i = 0; i < count; i++)
                {
                    // 법선이 위를 향하면 상대가 내 발밑
                    if (_contacts[i].normal.y <= 0.5f || !_contacts[i].collider.TryGetComponent(out Unit below) || !below.IsAlive)
                        continue;
                    float height = body.min.y - Ground.Instance.HeightAt(body.center.x);
                    if (height > maxHeight)
                    {
                        maxHeight = height;
                        topX = body.center.x;
                    }
                    Bounds belowBody = _contacts[i].collider.bounds;
                    _pileSpans.Add(new Vector2(body.min.x, body.max.x));
                    _pileSpans.Add(new Vector2(belowBody.min.x, belowBody.max.x));
                    if (below.Team != Team.Ally)
                        continue;
                    pairs++;
                    if (unit.Definition.StackRank < below.Definition.StackRank)
                    {
                        flipped++;
                        if (unit.transform.position.x >= front - NearFront)
                            flippedNearFront++;
                    }
                }
            }
            // 왼쪽 끝 순으로 이어 붙여 꼭대기가 든 덩어리를 찾는다
            _pileSpans.Sort((a, b) => a.x.CompareTo(b.x));
            float width = 0f;
            for (int i = 0; i < _pileSpans.Count;)
            {
                float left = _pileSpans[i].x, right = _pileSpans[i].y;
                for (i++; i < _pileSpans.Count && _pileSpans[i].x <= right + PileGap; i++)
                    right = Mathf.Max(right, _pileSpans[i].y);
                if (topX >= left && topX <= right)
                    width = right - left;
            }
            width = Mathf.Max(width, 1f);
            Debug.Log($"[Stack] {BattleManager.Elapsed:F0}s allies {allies} · stacked pairs {pairs} · max height {maxHeight:F1} · pile width {width:F1} · h/w {maxHeight / width:F2} · flipped {flipped} (near front {flippedNearFront}) · front unit {(frontUnit != null ? frontUnit.Definition.name : "-")}{(Ground.Instance == null ? " · RELOADED(Ground.Instance null)" : "")}");
        }
    }
}
