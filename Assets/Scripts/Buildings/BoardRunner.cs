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
            // 이번 프레임에 이미 발동했나 — 차지로 서로 당기는 건물끼리 한 프레임에 끝없이 발동하지 않게
            public bool FiredThisFrame;
        }

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
                slot.Timer += Time.deltaTime;
                TryFire(i);
            }
        }

        private void TryFire(int index)
        {
            Slot slot = _slots[index];
            if (slot.FiredThisFrame || slot.Timer < slot.Cooldown)
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
                Unit.Spawn(prefab, point).NotifySummoned(sourceSlot);
        }
    }
}
