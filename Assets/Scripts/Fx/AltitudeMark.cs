using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 고도 배율 글자 — 고도로 강해지는 유닛(미노타우로스) 머리 위에 지금 공격력 배율 「×1.3」(사용자 10/22). 고도 0(×1.0)이면 안 보인다.
    // 고도로 스탯이 바뀌는 유닛이 처음 고도를 받는 순간 Unit이 Watch를 부르고, 그다음은 Unit.AltitudeAttackScale을 읽기만 한다.
    // 글꼴은 데미지 숫자(DamageNumbers.Glyphs)의 3×5 + 「×」 · 「.」, 검은 1px 테두리. 글자 전부를 메시 하나로(PixelMesh). FxDirector가 만든다.
    // 붉은 기운(사용자 10/23 — 원판 대신 빨간 입자): 몸 둘레(양옆 · 머리)에서 2px 빨간 점이 위로 피어오르며 1px로 줄고 어두워진다. 배율이 클수록 조금 더 자주. 입자 전부를 메시 하나로(상한 · 유닛이 많으면 솎는다)
    public class AltitudeMark : MonoBehaviour
    {
        private const int Capacity = 128;
        private const int Lift = 2;            // 머리 위 띄움(px)
        private const int SortingOrder = 165;  // 상태 점(160) 위 · 숫자(200) 아래
        private const string Times = "000101010101000";
        private static readonly Color Fill = new Color(1f, 0.55f, 0.5f);
        // 입자 — 초당 수 = 기본 + 배율 오른 몫 × 비율(상한). 유닛이 ThinFrom기보다 많으면 그 비율만큼 줄인다
        private const int EmberCapacity = 512;
        private const int EmberSortingOrder = 155;   // 버프 선(150) 위 · 회복(158) · 상태 점(160) 아래
        private const float EmberRate = 6f;
        private const float EmberRatePerBonus = 12f;
        private const float EmberRateMax = 16f;
        private const int ThinFrom = 24;
        private const float EmberLifeMin = 0.6f;
        private const float EmberLifeMax = 0.9f;
        private const float EmberRiseMin = 1f;      // 칸/초
        private const float EmberRiseMax = 1.6f;
        private const float EmberDrift = 0.15f;
        private const float EmberShrink = 0.6f;     // 수명의 이 몫까지 2px, 그 뒤 1px
        // 고도로 커 보이는 그림 ≈ 충돌 박스 키 × 이 값(Unit.AltitudeLook) — 입자는 그 둘레(양옆 · 머리)에서 난다(몸 위에 점이 박혀 보이지 않게)
        private const float LookScale = 1.4f;
        private static readonly Color EmberHot = new Color(1f, 0.3f, 0.2f);
        private static readonly Color EmberCool = new Color(0.6f, 0.05f, 0.05f);

        private struct Watched
        {
            public Unit Unit;
            public int Life;
            public float Spawn;   // 다음 입자까지(초)
        }

        private struct Ember
        {
            public Vector2 Point;
            public Vector2 Velocity;
            public float Age;
            public float Life;
        }

        private static AltitudeMark _instance;
        private PixelMesh _mesh;
        private PixelMesh _embersMesh;
        private readonly List<Watched> _units = new List<Watched>();
        private readonly HashSet<Unit> _watching = new HashSet<Unit>();
        private readonly List<Ember> _embers = new List<Ember>();

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "AltitudeMark", material, SortingOrder, Capacity * 60);
            _embersMesh = new PixelMesh(transform, "AltitudeEmbers", material, EmberSortingOrder, EmberCapacity);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // 이 유닛의 스탯이 고도로 오르기 시작했다 — 죽거나 풀로 돌아갈 때까지 배율을 그린다
        public static void Watch(Unit unit)
        {
            if (_instance == null || _instance._units.Count >= Capacity || !_instance._watching.Add(unit))
                return;
            _instance._units.Add(new Watched { Unit = unit, Life = unit.Life });
        }

        private void LateUpdate()
        {
            _mesh.Begin();
            for (int i = _units.Count - 1; i >= 0; i--)
            {
                Unit unit = _units[i].Unit;
                if (unit == null || !unit.isActiveAndEnabled || unit.Life != _units[i].Life || !unit.IsAlive)
                {
                    _watching.Remove(unit);
                    _units.RemoveAt(i);
                    continue;
                }
                // 소수 한 자리 — ×1.0이면(고도 0) 그리지 않는다
                int tenths = Mathf.Min(Mathf.RoundToInt(unit.AltitudeAttackScale * 10f), 99);
                if (tenths <= 10)
                    continue;
                // 폭 = × 3 + 1 + 정수 3 + 1 + 점 1 + 1 + 소수 3 = 13px, 머리 가운데에
                int x = PixelMesh.ToGrid(unit.Position.x) - 6, y = PixelMesh.ToGrid(unit.Top) + Lift;
                Write(x, y, Times);
                Write(x + 4, y, DamageNumbers.Glyphs[tenths / 10]);
                Dot(x + 8, y);
                Write(x + 10, y, DamageNumbers.Glyphs[tenths % 10]);
                Spawn(i, unit);
            }
            _mesh.End();
            DrawEmbers();
        }

        // 붉은 입자 — 이 유닛 몫의 시간이 되면 몸 둘레(옆 · 위) 아무 데서 하나
        private void Spawn(int index, Unit unit)
        {
            Watched w = _units[index];
            w.Spawn -= Time.deltaTime;
            float bonus = unit.AltitudeAttackScale - 1f;
            if (w.Spawn <= 0f && _embers.Count < EmberCapacity)
            {
                float rate = Mathf.Min(EmberRate + bonus * EmberRatePerBonus, EmberRateMax) * Mathf.Min(1f, (float)ThinFrom / _units.Count);
                w.Spawn += Random.Range(0.5f, 1.5f) / rate;
                if (w.Spawn < 0f)
                    w.Spawn = 0f;
                // 셋 중 둘은 양옆 가장자리, 하나는 머리 위
                float look = (unit.Top - unit.Feet) * LookScale;
                Vector2 point = Random.value < 2f / 3f
                    ? new Vector2(unit.Position.x + (Random.value < 0.5f ? -1f : 1f) * (look * 0.5f + Random.Range(-1f, 2f) * PixelMesh.Pixel), unit.Feet + Random.Range(0.1f, 0.9f) * look)
                    : new Vector2(unit.Position.x + Random.Range(-0.4f, 0.4f) * look, unit.Feet + look);
                _embers.Add(new Ember
                {
                    Point = point,
                    Velocity = new Vector2(Random.Range(-EmberDrift, EmberDrift), Random.Range(EmberRiseMin, EmberRiseMax)),
                    Life = Random.Range(EmberLifeMin, EmberLifeMax),
                });
            }
            _units[index] = w;
        }

        private void DrawEmbers()
        {
            float dt = Time.deltaTime;
            _embersMesh.Begin();
            for (int i = _embers.Count - 1; i >= 0; i--)
            {
                Ember e = _embers[i];
                e.Age += dt;
                if (e.Age >= e.Life)
                {
                    _embers.RemoveAt(i);
                    continue;
                }
                e.Point += e.Velocity * dt;
                _embers[i] = e;
                float t = e.Age / e.Life;
                int size = t < EmberShrink ? 2 : 1;
                _embersMesh.Rect(PixelMesh.ToGrid(e.Point.x), PixelMesh.ToGrid(e.Point.y), size, size, Color.Lerp(EmberHot, EmberCool, t));
            }
            _embersMesh.End();
        }

        // 3×5 글자 하나 — 테두리(검정) 먼저, 그 위에 글자. (x, y) = 왼쪽 아래
        private void Write(int x, int y, string glyph)
        {
            for (int pass = 0; pass < 2; pass++)
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        if (glyph[row * 3 + col] != '1')
                            continue;
                        int px = x + col, py = y + 4 - row;
                        if (pass == 0)
                            _mesh.Rect(px - 1, py - 1, 3, 3, Color.black);
                        else
                            _mesh.Rect(px, py, 1, 1, Fill);
                    }
        }

        private void Dot(int x, int y)
        {
            _mesh.Rect(x - 1, y - 1, 3, 3, Color.black);
            _mesh.Rect(x, y, 1, 1, Fill);
        }
    }
}
