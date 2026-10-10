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
            // 층·칸 표시용(전장 탑 · UI 탑) — 남은 가속(초, 기물이 멈췄거나 쿨다운 없는 기물이면 0) · 지금 쿨다운 ÷ 원래 쿨다운(풍차 ×0.8 → 0.8, 받은 게 없으면 1).
            // 원래 쿨다운 = 그 등급의 쿨다운(CooldownAt) — 등급으로 준 몫은 받은 효과가 아니다
            public float HasteLeft => Cooldown > 0f && !BattleManager.BoardsStopped ? Mathf.Max(0f, Haste) : 0f;
            public float CooldownScale => Building != null && Building.Definition.Cooldown > 0f ? Cooldown / Building.Definition.CooldownAt(Building.Level) : 1f;
            // 이번 프레임에 이미 발동했나 — 서로 발동시키는 기물끼리 한 프레임에 끝없이 발동하지 않게
            public bool FiredThisFrame;
            // 전투 동안 이 칸이 쌓은 값(허수아비 · 화살 강화기 · 처치 발동) — 아직 안 터졌으면 0(+0% · ×1)
            public bool HasBattleGain;
            public StatOp BattleGainOp;
            public float BattleGainValue;
            // 기획서 §6 표기("+20%" · "×1.33") — 전투 동안 쌓는 기물이 아니면 null
            public string BattleGainText => HasBattleGain ? new StatModifier(UnitStat.Attack, BattleGainOp, BattleGainValue).ValueText : null;
        }

        // 기획서 §5 "가속" — 쿨다운이 2배 속도로 돈다
        private const float HasteRate = 2f;

        private static readonly BoardRunner[] ByTeam = new BoardRunner[2];

        [SerializeField] private Team _team;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Transform _largeSpawnPoint;

        private Slot[] _slots = new Slot[0];
        private bool _running;
        // 얼음 — 다음 "모든 기물 발동"까지 지난 시간
        private float _fireAllTimer;

        public Team Team => _team;
        public Slot[] Slots => _slots;
        // 올려 둔 필드(층 순서, 0 = 1층) — 상점에서는 RunState.Field 그 배열이라 사고팔면 바로 바뀐다(탑 그림이 읽는다)
        public OwnedBuilding[] Field { get; private set; }
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
            Field = field;
            _slots = new Slot[field.Length];
            for (int i = 0; i < field.Length; i++)
            {
                _slots[i] = new Slot();
                if (!OwnedBuilding.IsEmpty(field[i]))
                {
                    _slots[i].Building = field[i];
                    _slots[i].Cooldown = field[i].Definition.CooldownAt(field[i].Level);
                    InitBattleGain(_slots[i]);
                }
            }
        }

        private static void InitBattleGain(Slot slot)
        {
            StatOp op = default;
            bool gains = false;
            foreach (BuildingAction action in slot.Building.Definition.Actions)
                gains = gains || action.BattleGain(out op);
            foreach (BuildingPassive passive in slot.Building.Definition.Passives)
                gains = gains || passive.BattleGain(out op);
            slot.HasBattleGain = gains;
            slot.BattleGainOp = op;
            slot.BattleGainValue = op == StatOp.Multiply ? 1f : 0f;
        }

        // 칸이 전투 동안 쌓은 값에 더한다(+는 더하고 ×는 곱한다) — 칸의 계산 방식과 다르면 무시
        public void AddBattleGain(int index, StatOp op, float value)
        {
            Slot slot = _slots[index];
            if (!slot.HasBattleGain || slot.BattleGainOp != op)
                return;
            slot.BattleGainValue = op == StatOp.Multiply ? slot.BattleGainValue * value : slot.BattleGainValue + value;
        }

        // 전투 시작 — 효과를 켜고 게이지를 돌린다. TeamEffects는 호출하는 쪽이 먼저 비운다
        public void Begin()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (IsActive(i))
                    foreach (UnitEffect effect in _slots[i].Building.Definition.Effects)
                        TeamEffects.Enable(_team, effect, _slots[i].Building.Level);
            BuffLinks.ClearStanding(_team);
            for (int i = 0; i < _slots.Length; i++)
                if (IsActive(i))
                    foreach (BuildingPassive passive in _slots[i].Building.Definition.Passives)
                        passive.OnBattleStart(this, i, _slots[i].Building.Level);
            _fireAllTimer = 0f;
            _running = true;
        }

        // 이번 맵(양 진영 같다) — 런 밖이면 없음
        private static MapDefinition Map => RunState.Map;

        // 전투에서 효과를 내는 칸인가 — 늪지에서는 쿨다운 없는 기물이 아무것도 하지 않는다
        private bool IsActive(int index)
            => HasBuilding(index) && !(Map != null && Map.PassiveBuildingsOff && _slots[index].Building.Definition.Cooldown <= 0f);

        public void Stop() => _running = false;

        private void Update()
        {
            // 데스매치 — 정해진 시각부터 기물이 발동하지 않는다. 발동 경로(Fire)는 이 루프에서만 시작된다(FireNow는 전투 시작 때와, 이 루프에서 발동한 기물의 액션 안에서뿐 — 판도라의 상자)
            if (!_running || BattleManager.BoardsStopped)
                return;

            foreach (Slot slot in _slots)
                slot.FiredThisFrame = false;
            // 맵의 쿨다운 진행 배율(얼음 ×0.5 · 정글 ×2)은 가속과 곱한다
            float rate = Map != null ? Map.CooldownRate : 1f;
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (slot.Building == null)
                    continue;
                slot.Timer += Time.deltaTime * (slot.Haste > 0f ? HasteRate : 1f) * rate;
                slot.Haste -= Time.deltaTime;
                TryFire(i);
            }

            // 얼음 — 정해진 간격마다 쿨다운이 있는 기물을 전부 발동(게이지는 그대로)
            if (Map != null && Map.FireAllInterval > 0f)
            {
                _fireAllTimer += Time.deltaTime;
                if (_fireAllTimer >= Map.FireAllInterval)
                {
                    _fireAllTimer -= Map.FireAllInterval;
                    for (int i = 0; i < _slots.Length; i++)
                        if (FireNow(i))
                            BuffLinks.ToSlot(_team, -1, i, BuffKind.Trigger);
                }
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

            // 늪지 — 한 번 발동에 효과를 여러 번
            int repeats = Map != null ? Map.FireRepeats : 1;
            for (int r = 0; r < repeats; r++)
            {
                if (definition.Unit != null)
                    Summon(definition.Unit, definition.SummonCount, index);
                foreach (BuildingAction action in definition.Actions)
                    action.Execute(this, index, level);
                foreach (UnitEffect effect in definition.Effects)
                    TeamEffects.AddStack(_team, effect);
            }

            Triggered?.Invoke(index);
            BuffLinks.SlotFired(_team, index);
        }

        // 이 진영의 모든 소환이 거치는 곳 — 기획서 §5 처리 순서: ① 추가 소환 ② 변환. 그다음 한 마리씩 소환 이벤트.
        // sourceSlot = 소환한 기물의 칸(기물이 아니면 -1) · at = 소환 자리(없으면 성문 — 넓은 유닛은 큰 유닛 소환 지점)
        public void Summon(UnitDefinition unit, int count, int sourceSlot, Vector2? at = null)
        {
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, unit))
                {
                    int extra = e.Effect.ExtraSummons(unit, count, sourceSlot, e.Stacks, e.Level);
                    count += extra;
                    if (extra > 0 && sourceSlot >= 0)
                        BuffLinks.EffectToSlot(_team, e.Effect, sourceSlot, BuffKind.Trigger);
                }
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, unit))
                {
                    UnitDefinition converted = e.Effect.ConvertSummon(unit, sourceSlot, e.Stacks);
                    if (converted != unit && sourceSlot >= 0)
                        BuffLinks.EffectToSlot(_team, e.Effect, sourceSlot, BuffKind.Trigger);
                    unit = converted;
                }

            Unit prefab = unit.PrefabFor(_team);
            float width = prefab.GetComponent<BoxCollider2D>().size.x;
            Vector2 point = at ?? (Vector2)(width > LargeUnitWidth ? _largeSpawnPoint : _spawnPoint).position;
            // 기물이 소환하면 그 기물의 층에서 나온다(탑 — 큰 유닛도 자기 층에서, 사용자 10/8). 기물이 아닌 소환·자리를 정한 소환은 그대로
            Tower tower = at == null && sourceSlot >= 0 ? Tower.For(_team) : null;
            bool fromFloor = tower != null && sourceSlot < tower.FloorCount;
            for (int i = 0; i < count; i++)
            {
                Unit spawned = fromFloor ? Unit.SpawnStanding(prefab, tower.SpawnFeet(sourceSlot, i, Mathf.Max(1f, width))) : Unit.Spawn(prefab, point);
                spawned.NotifySummoned(sourceSlot);
                // 다른 층의 기물이 ~를 소환할 때마다 (군기 · 성당기사단) — 어느 층을 볼지는 능력이 정한다
                if (sourceSlot >= 0)
                    for (int other = 0; other < _slots.Length; other++)
                        if (other != sourceSlot && IsActive(other))
                            foreach (BuildingPassive passive in _slots[other].Building.Definition.Passives)
                                passive.OnSummonedFrom(this, other, sourceSlot, spawned);
            }
        }

        public void NotifyTowerKill(Unit victim)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (IsActive(i))
                    foreach (BuildingPassive passive in _slots[i].Building.Definition.Passives)
                        passive.OnTowerKill(this, i, victim);
        }

        private bool HasBuilding(int index) => index >= 0 && index < _slots.Length && _slots[index].Building != null;

        // 즉시 1회 발동 — 게이지는 그대로(징집 포고문). 쿨다운 없는 기물은 발동하지 않는다(false)
        public bool FireNow(int index)
        {
            if (!HasBuilding(index) || _slots[index].Cooldown <= 0f)
                return false;
            Fire(index);
            return true;
        }

        // 쿨다운에 곱한다(풍차 ×0.8)
        public void ScaleCooldown(int index, float factor)
        {
            if (HasBuilding(index))
                _slots[index].Cooldown *= factor;
        }

        // 쿨다운이 있는 기물 중 무작위 하나를 가속(수도원 — 사용자 결정: 쿨다운 없는 기물은 고르지 않는다). 고른 칸을 돌려준다(없으면 -1)
        public int HasteRandom(float seconds)
        {
            int count = 0;
            for (int i = 0; i < _slots.Length; i++)
                if (HasBuilding(i) && _slots[i].Cooldown > 0f)
                    count++;
            if (count == 0)
                return -1;
            int pick = Random.Range(0, count);
            for (int i = 0; i < _slots.Length; i++)
                if (HasBuilding(i) && _slots[i].Cooldown > 0f && pick-- == 0)
                {
                    Haste(i, seconds);
                    return i;
                }
            return -1;
        }

        // 가속 — seconds 동안 게이지가 2배로 찬다. 이미 가속 중이면 더 긴 쪽
        public void Haste(int index, float seconds)
        {
            if (HasBuilding(index))
                _slots[index].Haste = Mathf.Max(_slots[index].Haste, seconds);
        }
    }
}
