using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 몸 상태 점(전투 시각화 F5) — 상태가 붙은 동안 머리 위 2×2px 점이 깜빡인다. 불탐 = 주황(+ 가끔 불꽃 한 조각) · 감속 = 하늘 · 부활 대기 = 초록(쓰러진 자리).
    // 상태가 붙는 순간 게임 코드가 Watch(유닛) · At(자리)를 부르고, 그다음은 유닛의 상태를 읽기만 한다(IsBurning · IsSlowed) — 상태가 없어지면 뺀다.
    // 점 전부를 메시 하나로(PixelMesh). FxDirector가 만든다
    public class StatusDots : MonoBehaviour
    {
        private const int Capacity = 512;
        private const int Size = 2;
        private const int Gap = 1;
        // 머리 위 띄움(px) · 깜빡임 주기 · 켜진 몫
        private const int Lift = 2;
        private const float BlinkPeriod = 0.5f;
        private const float BlinkOn = 0.6f;
        // 불타는 유닛에서 불꽃 한 조각이 오르는 간격
        private const float EmberGap = 0.4f;
        private const int SortingOrder = 160;   // 버프 선(150) 위 · 숫자(200) 아래

        private static readonly Color Burn = new Color(1f, 0.45f, 0.15f);

        private struct Watched
        {
            public Unit Unit;
            public int Life;
            public float Phase;
            public float Ember;
        }

        private struct Spot
        {
            public Vector2 Point;
            public float Until;
            public Color Color;
        }

        private static StatusDots _instance;
        private PixelMesh _mesh;
        private readonly List<Watched> _units = new List<Watched>();
        private readonly HashSet<Unit> _watching = new HashSet<Unit>();
        private readonly List<Spot> _spots = new List<Spot>();

        public void Init(Material material)
        {
            _instance = this;
            _mesh = new PixelMesh(transform, "StatusDots", material, SortingOrder, Capacity * 3);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // 이 유닛에 상태가 붙었다(불탐 · 감속) — 상태가 다 없어질 때까지 점을 그린다
        public static void Watch(Unit unit)
        {
            if (_instance == null || _instance._units.Count >= Capacity || !_instance._watching.Add(unit))
                return;
            _instance._units.Add(new Watched { Unit = unit, Life = unit.Life, Phase = Random.value * BlinkPeriod });
        }

        // 몸이 없는 자리의 상태(부활 대기) — seconds 동안 그 자리 땅 위에 초록 점
        public static void At(Vector2 point, float seconds)
        {
            if (_instance != null && _instance._spots.Count < Capacity)
                _instance._spots.Add(new Spot { Point = point, Until = Time.time + seconds, Color = BuffLinkFx.ColorOf(BuffKind.Summon) });
        }

        private void LateUpdate()
        {
            _mesh.Begin();
            float now = Time.time, dt = Time.deltaTime;
            for (int i = _units.Count - 1; i >= 0; i--)
            {
                Watched w = _units[i];
                Unit unit = w.Unit;
                bool burning = unit != null && unit.isActiveAndEnabled && unit.Life == w.Life && unit.IsAlive && unit.IsBurning;
                bool slowed = unit != null && unit.isActiveAndEnabled && unit.Life == w.Life && unit.IsAlive && unit.IsSlowed;
                if (!burning && !slowed)
                {
                    _watching.Remove(unit);
                    _units.RemoveAt(i);
                    continue;
                }
                Vector2 head = new Vector2(unit.Position.x, unit.Top);
                if (burning)
                {
                    w.Ember -= dt;
                    if (w.Ember <= 0f)
                    {
                        w.Ember = EmberGap;
                        if (FxDirector.Instance != null)
                            FxDirector.Instance.Ember(head);
                    }
                }
                _units[i] = w;
                if (Mathf.Repeat(now + w.Phase, BlinkPeriod) > BlinkPeriod * BlinkOn)
                    continue;
                int count = (burning ? 1 : 0) + (slowed ? 1 : 0);
                int x = PixelMesh.ToGrid(head.x) - (count * Size + (count - 1) * Gap) / 2, y = PixelMesh.ToGrid(head.y) + Lift;
                if (burning)
                {
                    _mesh.Rect(x, y, Size, Size, Burn);
                    x += Size + Gap;
                }
                if (slowed)
                    _mesh.Rect(x, y, Size, Size, BuffLinkFx.ColorOf(BuffKind.Haste));
            }
            for (int i = _spots.Count - 1; i >= 0; i--)
            {
                Spot s = _spots[i];
                if (now >= s.Until)
                {
                    _spots.RemoveAt(i);
                    continue;
                }
                if (Mathf.Repeat(now, BlinkPeriod) <= BlinkPeriod * BlinkOn)
                    _mesh.Rect(PixelMesh.ToGrid(s.Point.x) - Size / 2, PixelMesh.ToGrid(s.Point.y) + Lift, Size, Size, s.Color);
            }
            _mesh.End();
        }
    }
}
