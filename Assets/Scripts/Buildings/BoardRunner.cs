using UnityEngine;

namespace GnorpWar
{
    // 한 진영의 필드 보드를 전투 동안 돌린다 — 칸마다 쿨다운 게이지가 0부터 차오르고, 차면 발동.
    // 아군·적이 같은 코드(진영과 소환 지점만 다르다)
    public class BoardRunner : MonoBehaviour
    {
        // 이 폭보다 넓은 유닛(보스)은 기지와 겹치지 않게 큰 유닛 소환 지점에서 나온다
        private const float LargeUnitWidth = 1.5f;

        public class Slot
        {
            public OwnedBuilding Building;
            public float Cooldown;
            public float Timer;
            // 가속이 남은 시간 — 그동안 게이지가 2배로 찬다
            public float Haste;
            // 이번 프레임에 이미 발동했나 — 서로 발동시키는 기물끼리 한 프레임에 끝없이 발동하지 않게
            public bool FiredThisFrame;
        }

        // 기획서 §5 "가속" — 쿨다운이 2배 속도로 돈다
        private const float HasteRate = 2f;

        private static readonly BoardRunner[] ByTeam = new BoardRunner[2];

        [SerializeField] private Team _team;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Transform _largeSpawnPoint;

        private Slot[] _slots = new Slot[0];
        private bool _running;

        public Team Team => _team;
        public Slot[] Slots => _slots;
        // 칸이 발동한 순간 (UI 반짝임 등)
        public event System.Action<int> Triggered;

        public static BoardRunner For(Team team) => ByTeam[(int)team];

        private void Awake() => ByTeam[(int)_team] = this;

        private void OnDestroy()
        {
            if (ByTeam[(int)_team] == this)
                ByTeam[(int)_team] = null;
        }

        // 전투 전 — 보드를 올려 두기만 한다(상점에서 보이게). 발동은 Begin부터
        public void Load(OwnedBuilding[] field)
        {
            _slots = new Slot[field.Length];
            for (int i = 0; i < field.Length; i++)
            {
                _slots[i] = new Slot();
                if (!OwnedBuilding.IsEmpty(field[i]))
                {
                    _slots[i].Building = field[i];
                    _slots[i].Cooldown = field[i].Definition.Cooldown;
                }
            }
        }

        // 전투 시작 — 효과를 켜고 게이지를 돌린다. TeamEffects는 호출하는 쪽이 먼저 비운다
        public void Begin()
        {
            foreach (Slot slot in _slots)
                if (slot.Building != null)
                    foreach (UnitEffect effect in slot.Building.Definition.Effects)
                        TeamEffects.Enable(_team, effect);
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Building != null)
                    foreach (BuildingPassive passive in _slots[i].Building.Definition.Passives)
                        passive.OnBattleStart(this, i, _slots[i].Building.Level);
            _running = true;
        }

        public void Stop() => _running = false;

        private void Update()
        {
            if (!_running)
                return;

            foreach (Slot slot in _slots)
                slot.FiredThisFrame = false;
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (slot.Building == null)
                    continue;
                slot.Timer += Time.deltaTime * (slot.Haste > 0f ? HasteRate : 1f);
                slot.Haste -= Time.deltaTime;
                TryFire(i);
            }
        }

        private void TryFire(int index)
        {
            Slot slot = _slots[index];
            // 쿨다운 0 = 쿨다운 없는 기물(늘 켜진 효과만) — 발동하지 않는다
            if (slot.Cooldown <= 0f || slot.FiredThisFrame || slot.Timer < slot.Cooldown)
                return;

            // 넘친 시간은 다음 게이지로 이월된다 — 한 프레임에 한 번만 발동하므로 더 넘치면 다음 프레임에
            slot.Timer -= slot.Cooldown;
            slot.FiredThisFrame = true;
            Fire(index);
        }

        private void Fire(int index)
        {
            OwnedBuilding building = _slots[index].Building;
            BuildingDefinition definition = building.Definition;
            int level = building.Level;

            if (definition.Unit != null)
                Summon(definition.Unit, definition.UnitCount * level + BattleManager.ExtraUnits, index);
            foreach (BuildingAction action in definition.Actions)
                action.Execute(this, index, level);
            foreach (UnitEffect effect in definition.Effects)
                TeamEffects.AddStack(_team, effect);

            Triggered?.Invoke(index);
        }

        // 이 진영의 모든 소환이 거치는 곳 — 기획서 §5 처리 순서: ① 추가 소환 ② 변환. 그다음 한 마리씩 소환 이벤트.
        // sourceSlot = 소환한 기물의 칸(기물이 아니면 -1) · at = 소환 자리(없으면 성문 — 넓은 유닛은 큰 유닛 소환 지점)
        public void Summon(UnitDefinition unit, int count, int sourceSlot, Vector2? at = null)
        {
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesTo(unit))
                    count += e.Effect.ExtraSummons(unit, count, sourceSlot, e.Stacks);
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesTo(unit))
                    unit = e.Effect.ConvertSummon(unit, sourceSlot, e.Stacks);

            Unit prefab = unit.PrefabFor(_team);
            Vector2 point = at ?? (Vector2)(prefab.GetComponent<BoxCollider2D>().size.x > LargeUnitWidth ? _largeSpawnPoint : _spawnPoint).position;
            for (int i = 0; i < count; i++)
            {
                Unit spawned = Unit.Spawn(prefab, point);
                spawned.NotifySummoned(sourceSlot);
                // 인접 기물이 ~를 소환할 때마다 (군기 등)
                if (sourceSlot >= 0)
                    foreach (int neighbor in new[] { sourceSlot - 1, sourceSlot + 1 })
                        if (HasBuilding(neighbor))
                            foreach (BuildingPassive passive in _slots[neighbor].Building.Definition.Passives)
                                passive.OnNeighborSummoned(this, neighbor, spawned);
            }
        }

        private bool HasBuilding(int index) => index >= 0 && index < _slots.Length && _slots[index].Building != null;

        // 즉시 1회 발동 — 게이지는 그대로(징집 포고문). 쿨다운 없는 기물은 발동하지 않는다
        public void FireNow(int index)
        {
            if (!HasBuilding(index) || _slots[index].Cooldown <= 0f)
                return;
            Fire(index);
        }

        // 쿨다운에 곱한다(풍차 ×0.8)
        public void ScaleCooldown(int index, float factor)
        {
            if (HasBuilding(index))
                _slots[index].Cooldown *= factor;
        }

        // 가속 — seconds 동안 게이지가 2배로 찬다. 이미 가속 중이면 더 긴 쪽
        public void Haste(int index, float seconds)
        {
            if (HasBuilding(index))
                _slots[index].Haste = Mathf.Max(_slots[index].Haste, seconds);
        }
    }
}
