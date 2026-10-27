using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 목책(사용자 10/27) — 성 앞에 세우는 울타리. 적 유닛만 막는다: 같은 진영 유닛 · 건축가는 몸이 통과하고(작은 타워 레이어 — Base.SetupLayers),
    // 적 유닛은 부딪혀 멈추고 부숴야 지나간다(유닛은 IDamageable이면 무엇이든 친다). 같은 진영 투사체도 지나간다(Projectile — 같은 진영은 통과).
    // 쏘지 않고 '내 타워' 효과도 받지 않는다(타워가 아니다 — Base.SmallTowers에 안 잡힌다). 체력 · 가시는 세운 기물이 정한다(PalisadeAtStartPassive).
    // 전투 씬마다 새로 뜨므로 지우는 곳은 없다
    [RequireComponent(typeof(BoxCollider2D))]
    public class Palisade : MonoBehaviour, IDamageable
    {
        private const float GroundSampleStep = 0.25f;
        private static readonly List<Palisade> All = new List<Palisade>();

        private Team _team;
        private float _hp;
        // 받은 피해 중 공격한 쪽에 돌려주는 비율(신화) — 0 = 없음
        private float _reflect;
        private BoxCollider2D _box;

        public Team Team => _team;
        public bool IsAlive => _hp > 0f;

        private void Awake() => _box = GetComponent<BoxCollider2D>();
        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        // team의 성 앞에 세운다 — 성 앞면에서 gap 띄우고, 내 목책이 이미 있으면 가장 앞 목책에서 spacing 앞으로. 밑면은 폭 안에서 가장 낮은 땅. 본진이 없으면 null
        public static Palisade Build(Palisade prefab, Team team, float hp, float reflect, float gap, float spacing)
        {
            Base home = Base.For(team);
            if (home == null || Ground.Instance == null)
                return null;
            float forward = team == Team.Ally ? 1f : -1f;
            var box = prefab.GetComponent<BoxCollider2D>();
            float half = box.size.x * 0.5f;
            float x = (forward > 0f ? home.Bounds.max.x : home.Bounds.min.x) + forward * (gap + half);
            foreach (Palisade other in All)
                if (other._team == team && (other.transform.position.x + forward * spacing - x) * forward > 0f)
                    x = other.transform.position.x + forward * spacing;
            float feet = float.MaxValue;
            for (float s = x - half; s <= x + half + 0.001f; s += GroundSampleStep)
                feet = Mathf.Min(feet, Ground.Instance.HeightAt(s));
            Vector2 bottom = new Vector2(box.offset.x, box.offset.y - box.size.y * 0.5f);
            Palisade palisade = Instantiate(prefab, new Vector2(x, feet) - bottom, Quaternion.identity);
            palisade._team = team;
            palisade._hp = hp;
            palisade._reflect = reflect;
            palisade.gameObject.layer = Base.TowerLayer(team);
            if (team == Team.Enemy)
                palisade.transform.localScale = Vector3.Scale(palisade.transform.localScale, new Vector3(-1f, 1f, 1f));
            return palisade;
        }

        public void TakeDamage(float amount, Vector2 hitDirection, float push, IDamageable attacker)
        {
            if (!IsAlive)
                return;
            DamageMeter.Credit(attacker, _team, Mathf.Min(amount, _hp));   // 실제로 깎인 몫만
            _hp -= amount;
            if (amount > 0f)
                BattleManager.NoteProgress();
            DamageNumbers.Damage(this, _team, new Vector2(_box.bounds.center.x, _box.bounds.max.y), amount);
            if (_reflect > 0f)
                DamageReflect.Back(attacker, amount * _reflect, hitDirection, this);
            if (IsAlive)
                return;
            if (FxDirector.Instance != null)
                FxDirector.Instance.DeathPuff(_box.bounds.center);
            Destroy(gameObject);
        }
    }
}
