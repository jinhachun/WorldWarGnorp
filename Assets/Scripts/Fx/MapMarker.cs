using UnityEngine;

namespace GnorpWar
{
    // 맵 효과의 출발점 — 성 근처에 둥둥 떠 있는 그림(얼음 맵의 얼음 덩이, 사용자 10/11). 맵의 「전부 발동」이 터질 때 버프 선이 여기서 그 진영의 층들로 나간다(Fx/BuffLinkFx).
    // 진영마다 하나씩 자기 탑 앞에 뜬다. 그 맵 효과가 있는 전투에서만 BattleManager가 만든다(씬이 내려가면 같이 사라진다)
    public class MapMarker : MonoBehaviour
    {
        // 1층 앞에서 전방으로 · 위로 떨어진 자리(유닛 칸) — 탑이 몇 층이든 같은 높이라 늘 화면 안이다
        private const float Ahead = 1.75f;
        private const float Above = 3.5f;
        // 위아래로 흔들리는 폭(유닛) · 한 번 오르내리는 시간(초)
        private const float BobHeight = 0.15f;
        private const float BobSeconds = 2.4f;
        // 유닛(0) 위 · 버프 선(150) 아래
        private const int SortingOrder = 140;

        private static readonly MapMarker[] ByTeam = new MapMarker[2];

        private Vector2 _home;

        public static MapMarker For(Team team) => ByTeam[(int)team];

        public Vector2 Position => transform.position;

        public static void Spawn(Team team, Sprite sprite)
        {
            Tower tower = Tower.For(team);
            if (tower == null || sprite == null)
                return;
            var go = new GameObject("MapMarker_" + team, typeof(SpriteRenderer), typeof(MapMarker));
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = SortingOrder;
            var marker = go.GetComponent<MapMarker>();
            float forward = team == Team.Ally ? 1f : -1f;
            marker._home = tower.FloorFront(0) + new Vector2(forward * Ahead, Above);
            go.transform.position = marker._home;
            ByTeam[(int)team] = marker;
        }

        private void Update()
            => transform.position = _home + new Vector2(0f, Mathf.Sin(Time.time * Mathf.PI * 2f / BobSeconds) * BobHeight);

        private void OnDestroy()
        {
            for (int i = 0; i < ByTeam.Length; i++)
                if (ByTeam[i] == this)
                    ByTeam[i] = null;
        }
    }
}
