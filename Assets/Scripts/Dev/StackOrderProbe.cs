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
            foreach (Unit unit in units)
            {
                if (unit.Team != Team.Ally || !unit.IsAlive)
                    continue;
                allies++;
                Bounds body = unit.GetComponent<BoxCollider2D>().bounds;
                maxHeight = Mathf.Max(maxHeight, body.min.y - Ground.Instance.HeightAt(body.center.x));
                int count = unit.GetComponent<Rigidbody2D>().GetContacts(_contacts);
                for (int i = 0; i < count; i++)
                {
                    // 법선이 위를 향하면 상대가 내 발밑
                    if (_contacts[i].normal.y <= 0.5f || !_contacts[i].collider.TryGetComponent(out Unit below)
                        || below.Team != Team.Ally || !below.IsAlive)
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
            Debug.Log($"[Stack] {BattleManager.Elapsed:F0}s allies {allies} · stacked pairs {pairs} · max height {maxHeight:F1} · flipped {flipped} (near front {flippedNearFront}) · front unit {(frontUnit != null ? frontUnit.Definition.name : "-")}{(Ground.Instance == null ? " · RELOADED(Ground.Instance null)" : "")}");
        }
    }
}
