using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 한 번에 여러 발 — 첫 발은 바로, 나머지는 짧은 시차(BattleConfig 「화살 연발 간격」)로 연달아 나간다(사용자 10/22: "그냥 쏘는데 여러 발 나갔으면").
    // 대상 · 피해 · 판정은 쏘는 쪽이 첫 발 때 정한 그대로 담아 둔다 — 떠나는 자리만 나갈 때의 발사점. 쏘는 쪽(Base · Unit)이 하나씩 갖고 FixedUpdate에서 Tick을 부른다
    public class Volley
    {
        public struct Shot
        {
            public Projectile Prefab;
            public Vector2 Target;
            public float Damage;
            public float Push;
            public float Arc;
            public float Splash;
            public bool Pierce;
            public float PierceBonus;
            public bool HighGround;
            public bool Boosted;
            public bool Extra;
        }

        private readonly List<Shot> _queue = new List<Shot>();
        private float _timer;

        // index번째 발 — 0번은 바로 쏘고, 그 뒤는 줄을 세운다(간격이 0이면 전부 바로)
        public void Fire(Shot shot, int index, Team team, Vector2 origin, IDamageable shooter)
        {
            float interval = BattleManager.VolleyInterval;
            if (index == 0 || interval <= 0f)
            {
                Launch(shot, team, origin, shooter);
                return;
            }
            if (_queue.Count == 0)
                _timer = interval;
            _queue.Add(shot);
        }

        public void Tick(float dt, Team team, Vector2 origin, IDamageable shooter)
        {
            if (_queue.Count == 0)
                return;
            _timer -= dt;
            while (_timer <= 0f && _queue.Count > 0)
            {
                Launch(_queue[0], team, origin, shooter);
                _queue.RemoveAt(0);
                _timer += Mathf.Max(BattleManager.VolleyInterval, 0.001f);
            }
        }

        public void Clear() => _queue.Clear();

        private static void Launch(Shot s, Team team, Vector2 origin, IDamageable shooter)
        {
            Projectile shot = Pooled.Get(s.Prefab, origin, Quaternion.identity);
            MuzzleFlash.At(origin);
            // 소리 자리(J6): 활 · 투석 발사
            shot.Launch(team, s.Damage, s.Push, s.Target, s.Arc, s.Splash, shooter);
            if (s.Pierce)
                shot.Pierce(s.PierceBonus);
            if (s.HighGround)
                shot.HighGround(s.Boosted);
            if (s.Extra)
                shot.MarkExtra();
        }
    }
}
