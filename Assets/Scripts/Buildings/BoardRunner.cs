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
            // 쿨다운이 멈춘 남은 시간(제물의 의식) — 그동안 게이지가 안 차고 가속 시간도 흐르지 않는다
            public float Frozen;
            // 층·칸 표시용(전장 탑 · UI 탑) — 남은 가속(초, 기물이 멈췄거나 쿨다운 없는 기물이면 0) · 지금 쿨다운 ÷ 원래 쿨다운(풍차 ×0.8 → 0.8, 받은 게 없으면 1).
            // 원래 쿨다운 = 그 등급의 쿨다운(CooldownAt) — 등급으로 준 몫은 받은 효과가 아니다
            public float HasteLeft => Cooldown > 0f && !BattleManager.BoardsStopped ? Mathf.Max(0f, Haste) : 0f;
            public float CooldownScale => Building != null && Building.Definition.Cooldown > 0f ? Cooldown / Building.Cooldown : 1f;
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
        // 파견 사무소 — 내 유닛이 성 대신 가장 앞에 있는 내 작은 타워에서 나온다(전투 동안, 패시브가 전투 시작 때 켠다).
        // 신화면 가장 앞 하나가 아니라 내 모든 작은 타워에서 동시에 나온다
        private bool _summonAtSmallTower;
        private bool _summonAtEverySmallTower;

        public Team Team => _team;
        public Slot[] Slots => _slots;
        // 올려 둔 필드(층 순서, 0 = 1층) — 상점에서는 RunState.Field 그 배열이라 사고팔면 바로 바뀐다(탑 그림이 읽는다)
        public OwnedBuilding[] Field { get; private set; }
        // 칸이 발동한 순간 (UI 반짝임 등)
        public event System.Action<int> Triggered;
        // 맵의 「전부 발동」(얼음)이 터진 순간 — 진영. 지형 연출(Fx/TerrainFx)이 탑 창에 얼음 섬광을 띄운다
        public static event System.Action<Team> MapFiredAll;

        public static BoardRunner For(Team team) => ByTeam[(int)team];

        // 자리를 정하지 않은 소환이 나오는 성문 앞(전투 시작 알림 Fx/BattleStartLines가 유닛이 아직 없을 때 선을 긋는 자리)
        public Vector2 SpawnPoint => _spawnPoint.position;

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
                    _slots[i].Cooldown = field[i].Cooldown;
                    InitBattleGain(_slots[i]);
                }
            }
        }

        private static void InitBattleGain(Slot slot)
        {
            StatOp op = default;
            bool gains = false;
            foreach (BuildingAction action in slot.Building.Actions)
                gains = gains || action.BattleGain(out op);
            foreach (BuildingPassive passive in slot.Building.Passives)
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
                    foreach (UnitEffect effect in _slots[i].Building.Effects)
                        TeamEffects.Enable(_team, effect, _slots[i].Building.Level, _slots[i].Building.Definition);
            BuffLinks.ClearStanding(_team);
            _summonAtSmallTower = false;
            _summonAtEverySmallTower = false;
            _doubleWhileHastedSlot = -1;
            FireOnThawSlot = -1;
            for (int i = 0; i < _slots.Length; i++)
                if (IsActive(i))
                    foreach (BuildingPassive passive in _slots[i].Building.Passives)
                        passive.OnBattleStart(this, i, _slots[i].Building.Level);
            _fireAllTimer = 0f;
            _deathmatchFired = false;
            _preDeathmatchFired = false;
            _revivals.Clear();
            _running = true;
        }

        // 다시 일어날 유닛들(노동의 기계화 신화) — 죽은 자리 · 일어날 시각(Time.time). 전투 시작마다 비운다
        private readonly System.Collections.Generic.List<(float at, UnitDefinition unit, Vector2 point, UnitEffect by)> _revivals
            = new System.Collections.Generic.List<(float, UnitDefinition, Vector2, UnitEffect)>();

        // seconds 뒤 point에서 unit이 다시 일어난다 — 소환이 아니다(추가 소환 · 변환 · 소환 이벤트를 지나지 않는다 — 지나면 죽을 때마다 수가 불어난다).
        // 데스매치로 기물이 멈춘 뒤 · 전투가 끝난 뒤에는 일어나지 않는다(Update의 발동 루프와 같은 조건). by = 일으킨 효과(버프 선의 출발 기물)
        public void ReviveLater(UnitDefinition unit, Vector2 point, float seconds, UnitEffect by)
        {
            _revivals.Add((Time.time + seconds, unit, point, by));
            StatusDots.At(new Vector2(point.x, Ground.Instance.HeightAt(point.x)), seconds);   // 기다리는 동안 그 자리에 초록 점
        }

        private void Revive()
        {
            for (int i = _revivals.Count - 1; i >= 0; i--)
                if (Time.time >= _revivals[i].at)
                {
                    var revival = _revivals[i];
                    _revivals.RemoveAt(i);
                    Vector2 ground = new Vector2(revival.point.x, Ground.Instance.HeightAt(revival.point.x));
                    Unit risen = Unit.Spawn(revival.unit.PrefabFor(_team), ground);
                    PillarFx.Rise(ground, risen.Top - ground.y);
                    BuffLinks.EffectToPoint(_team, revival.by, ground, BuffKind.Summon);
                }
        }

        // 이번 맵(양 진영 같다) — 런 밖이면 없음
        private static MapDefinition Map => RunState.Map;

        // 전투에서 효과를 내는 칸인가 — 늪지에서는 쿨다운 없는 기물이 아무것도 하지 않는다
        private bool IsActive(int index)
            => HasBuilding(index) && !StoppedByMap(_slots[index].Building);

        // 이번 맵이 작동을 끈 기물인가(늪지 — 쿨다운 없는 기물). 층의 정지 표시(Fx/StoppedMark)와 유닛 카드의 능력치(Units/Stats)도 이 판정을 읽는다
        public static bool StoppedByMap(OwnedBuilding building)
            => !OwnedBuilding.IsEmpty(building) && Map != null && Map.PassiveBuildingsOff && building.Definition.Cooldown <= 0f;

        public void Stop() => _running = false;

        // ── 지역 특수 업그레이드가 전투 시작 때 켜는 것(UpgradeDefinition.ApplySpecial) — 전투 씬마다 보드가 새로 뜨므로 끄는 곳은 없다
        // 징병제 — 쿨다운마다 유닛을 소환하는 기물이 한 번에 더 소환하는 수
        public int ExtraSummons;
        // 군주제 — 맨 위층(기물이 있는 가장 높은 층) 기물은 발동할 때마다 효과를 2번(맵 · 물레방앗간 신화의 반복과 곱한다)
        public bool TopFiresTwice;
        // 연방제 — 자리 효과(바로 위 · 아래 · 위에 있는 · 아래에 있는)가 내 다른 모든 기물에 걸린다
        public bool AllAdjacent;
        // 단두대 혁명 — 이 간격(초)마다 내 본진이 무작위 적 유닛 1기를 즉사시킨다(0 = 없음)
        public float GuillotineInterval;
        private float _guillotineTimer;
        // 합치기 보상 「야근 수당」 — 데스매치가 시작될 때 쿨다운이 있는 내 모든 기물을 즉시 이만큼 발동(0 = 없음). 한 전투에 한 번
        public int DeathmatchFires;
        private bool _deathmatchFired;
        // 데스매치 직전 훅(BuildingPassive.OnBeforeDeathmatch — 맘모스 소환의식)을 이번 전투에 불렀나
        private bool _preDeathmatchFired;

        // 자리 효과가 slot층의 기물에서 other층의 기물에 닿는가 — byPosition = 그 능력의 원래 자리 규칙(바로 위 · 아래에 있는 …). 연방제면 내 다른 모든 기물
        public bool Linked(int slot, int other, bool byPosition) => other != slot && (AllAdjacent || byPosition);

        public int TopIndex
        {
            get
            {
                for (int i = _slots.Length - 1; i >= 0; i--)
                    if (HasBuilding(i))
                        return i;
                return -1;
            }
        }

        // 쿨다운이 가장 긴 기물을 즉시 times번 발동(군국주의) — 같으면 그중 무작위(트라이브 명세 §2-1). 쿨다운 있는 기물이 없으면 아무 일도 없다. 멈춘 기물은 고르지 않는다(발동이 안 되므로)
        public int FireLongest(int times = 1)
        {
            int longest = -1, ties = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!HasBuilding(i) || _slots[i].Cooldown <= 0f || _slots[i].Frozen > 0f)
                    continue;
                if (longest < 0 || _slots[i].Cooldown > _slots[longest].Cooldown)
                {
                    longest = i;
                    ties = 1;
                }
                else if (_slots[i].Cooldown == _slots[longest].Cooldown && Random.Range(0, ++ties) == 0)
                    longest = i;
            }
            for (int t = 0; t < times; t++)
                FireNow(longest);
            return longest;
        }

        // 제물의 의식 — 칸의 쿨다운을 seconds 동안 멈춘다(그동안 게이지가 안 차고 가속도 안 걸린다). 이미 멈춰 있으면 더 긴 쪽. 멈춘 동안은 즉시 발동(FireNow)도 안 된다(사용자 10/15)
        public void Freeze(int index, float seconds)
        {
            if (HasBuilding(index))
                _slots[index].Frozen = Mathf.Max(_slots[index].Frozen, seconds);
        }

        // 제물의 의식 신화 — 멈췄던 기물이 다시 움직이는 순간 즉시 1회 발동한다(전투 동안, 패시브가 전투 시작 때 켠다). 값 = 켠 기물의 칸(버프 선의 출발), -1 = 꺼짐
        public int FireOnThawSlot = -1;

        public void NotifyUnitDied(Unit victim)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (IsActive(i))
                    foreach (BuildingPassive passive in _slots[i].Building.Passives)
                        passive.OnUnitDied(this, i, victim);
        }

        // 단두대 혁명 — 살아 있는 적 유닛 중 무작위 하나에게 그 체력만큼의 피해(내 본진의 처치). 무적인 유닛은 피해를 받지 않는다(기존 무적 규칙)
        private void Guillotine()
        {
            var targets = new System.Collections.Generic.List<Unit>();
            foreach (Unit unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (unit.IsAlive && unit.Team != _team)
                    targets.Add(unit);
            if (targets.Count == 0)
                return;
            Unit victim = targets[Random.Range(0, targets.Count)];
            Base home = Base.For(_team);
            if (home == null || !home.IsAlive)
                return;
            // 도끼가 날아가 닿는 순간에 피해를 준다(Fx/AxeThrow — 사용자 10/14). 도끼가 보이므로 버프 선은 긋지 않는다
            AxeThrow.Throw(home, victim);
        }

        // 물레방앗간 신화 — 이 진영의 가속 중인 기물이 발동할 때 효과를 2번 낸다(전투 동안, 패시브가 전투 시작 때 켠다). 곱하는 곳은 Fire
        // 켠 기물의 칸(버프 선의 출발), -1 = 꺼짐
        private int _doubleWhileHastedSlot = -1;

        public void DoubleFireWhileHasted(int slot) => _doubleWhileHastedSlot = slot;

        public void SummonAtSmallTower() => _summonAtSmallTower = true;

        // 신화는 §7 능력과 함께 켜지지만, 이 하나로도 작은 타워 소환이 켜지게 둘을 같이 올린다
        public void SummonAtEverySmallTower() => _summonAtSmallTower = _summonAtEverySmallTower = true;

        private void Update()
        {
            // 데스매치 — 정해진 시각부터 기물이 발동하지 않는다. 발동 경로(Fire)는 이 루프에서만 시작된다(FireNow는 전투 시작 때와, 이 루프에서 발동한 기물의 액션 안에서뿐 — 판도라의 상자)
            if (!_running || BattleManager.BoardsStopped)
                return;

            Revive();
            foreach (Slot slot in _slots)
                slot.FiredThisFrame = false;
            // 맵의 쿨다운 진행 배율(얼음 ×0.5 · 정글 ×2)은 가속과 곱한다
            float rate = Map != null ? Map.CooldownRate : 1f;
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (slot.Building == null)
                    continue;
                if (slot.Frozen > 0f)
                {
                    slot.Frozen -= Time.deltaTime;
                    if (slot.Frozen <= 0f && FireOnThawSlot >= 0 && FireNow(i) && FireOnThawSlot != i)
                        BuffLinks.ToSlot(_team, FireOnThawSlot, i, BuffKind.Trigger);
                    continue;
                }
                slot.Timer += Time.deltaTime * (slot.Haste > 0f ? HasteRate : 1f) * rate;
                slot.Haste -= Time.deltaTime;
                TryFire(i);
            }

            // 데스매치 직전(BattleConfig — 데스매치 몇 초 전)에 한 번 — 칸 패시브의 OnBeforeDeathmatch
            if (!_preDeathmatchFired && BattleManager.BeforeDeathmatch)
            {
                _preDeathmatchFired = true;
                for (int i = 0; i < _slots.Length; i++)
                    if (IsActive(i))
                        foreach (BuildingPassive passive in _slots[i].Building.Passives)
                            passive.OnBeforeDeathmatch(this, i, _slots[i].Building.Level);
            }

            // 야근 수당 — 데스매치가 시작된 프레임에 한 번. 본진 → 발동한 층마다 흰 선
            if (DeathmatchFires > 0 && !_deathmatchFired && BattleManager.TowersStopped)
            {
                _deathmatchFired = true;
                for (int i = 0; i < _slots.Length; i++)
                {
                    bool fired = false;
                    for (int t = 0; t < DeathmatchFires; t++)
                        fired |= FireNow(i);
                    if (fired)
                        BuffLinks.FromBaseToSlot(_team, i, BuffKind.Trigger);
                }
            }

            if (GuillotineInterval > 0f)
            {
                _guillotineTimer += Time.deltaTime;
                if (_guillotineTimer >= GuillotineInterval)
                {
                    _guillotineTimer -= GuillotineInterval;
                    Guillotine();
                }
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
                    MapFiredAll?.Invoke(_team);
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
            // 물레방앗간 신화 — 가속 중인 기물은 발동할 때 효과를 2번 낸다(맵의 반복과는 곱한다)
            if (_doubleWhileHastedSlot >= 0 && _slots[index].Haste > 0f)
            {
                repeats *= 2;
                if (_doubleWhileHastedSlot != index)
                    BuffLinks.ToSlot(_team, _doubleWhileHastedSlot, index, BuffKind.Trigger);
            }
            if (TopFiresTwice && index == TopIndex)
                repeats *= 2;
            for (int r = 0; r < repeats; r++)
            {
                bool summons = definition.Unit != null;
                foreach (BuildingAction action in building.Actions)
                    summons &= !action.ReplacesSummon(this, index);
                if (summons)
                    Summon(definition.Unit, definition.SummonCount + ExtraSummons, index);
                foreach (BuildingAction action in building.Actions)
                    action.Execute(this, index, level);
                foreach (UnitEffect effect in building.Effects)
                    TeamEffects.AddStack(_team, effect);
            }

            FloorMultiplier.Show(_team, index, repeats);   // 효과를 여러 번 냈으면 층 옆에 「×2」
            Triggered?.Invoke(index);
            BuffLinks.SlotFired(_team, index);
        }

        // 이 진영의 모든 소환이 거치는 곳 — 기획서 §5 처리 순서: ① 추가 소환 ② 변환. 그다음 한 마리씩 소환 이벤트.
        // sourceSlot = 소환한 기물의 칸(기물이 아니면 -1) · at = 소환 자리(없으면 성문 — 넓은 유닛은 큰 유닛 소환 지점)
        // source = 낳은 기물(Unit.SummonedBy) — 칸에서 소환하면 그 칸의 기물이고, 칸 없이 소환하는 유닛 효과는 자기를 켠 기물을 넘긴다(UnitEffect.Source)
        public void Summon(UnitDefinition unit, int count, int sourceSlot, Vector2? at = null, BuildingDefinition source = null)
        {
            if (source == null && HasBuilding(sourceSlot))
                source = _slots[sourceSlot].Building.Definition;
            int baseCount = count;   // 추가 소환 전 — 더 나온 몫에 기둥(Fx/PillarFx)
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, unit))
                {
                    int extra = e.Effect.ExtraSummons(_team, unit, count, sourceSlot, e.Stacks, e.Level);
                    count += extra;
                    if (extra > 0 && sourceSlot >= 0)
                        BuffLinks.EffectToSlot(_team, e.Effect, sourceSlot, BuffKind.Summon);
                }
            UnitEffect converter = null;
            foreach (TeamEffects.Entry e in TeamEffects.For(_team))
                if (e.Effect.AppliesFor(_team, unit))
                {
                    UnitDefinition converted = e.Effect.ConvertSummon(unit, sourceSlot, e.Stacks);
                    if (converted != unit)
                        converter = e.Effect;
                    if (converted != unit && sourceSlot >= 0)
                        BuffLinks.EffectToSlot(_team, e.Effect, sourceSlot, BuffKind.Summon);
                    unit = converted;
                }

            // 파견 사무소 — 자리를 정하지 않은 소환은 가장 앞에 있는 내 작은 타워의 발치에서(없으면 아래 그대로 성에서)
            Base outpost = at == null && _summonAtSmallTower ? Base.FrontSmallTower(_team) : null;
            // 그 자리에서 나오는 소환(죽은 자리 · 날아가 떨어진 자리 등 — 파견 사무소는 빼고)은 거기서 나온 게 보이게 기둥
            bool inPlace = at != null;
            if (outpost != null)
                at = Feet(outpost);

            Unit prefab = unit.PrefabFor(_team);
            float width = prefab.GetComponent<BoxCollider2D>().size.x;
            Vector2 point = at ?? (Vector2)(width > LargeUnitWidth ? _largeSpawnPoint : _spawnPoint).position;
            // 기물이 소환하면 그 기물의 층에서 나온다(탑 — 큰 유닛도 자기 층에서, 사용자 10/8). 기물이 아닌 소환·자리를 정한 소환은 그대로
            Tower tower = at == null && sourceSlot >= 0 ? Tower.For(_team) : null;
            bool fromFloor = tower != null && sourceSlot < tower.FloorCount;
            SpawnGroup(point);
            // 파견 사무소 신화 — 가장 앞 타워 말고 나머지 내 작은 타워에서도 같은 수가 동시에 나온다
            if (outpost != null && _summonAtEverySmallTower)
                foreach (Base small in Base.SmallTowers())
                    if (small != outpost && small.Team == _team)
                        SpawnGroup(Feet(small));

            void SpawnGroup(Vector2 from)
            {
                for (int i = 0; i < count; i++)
                {
                    Unit spawned = fromFloor ? Unit.SpawnStanding(prefab, tower.SpawnFeet(sourceSlot, i, Mathf.Max(1f, width))) : Unit.Spawn(prefab, from);
                    LastSpawned = spawned;
                    if (converter != null)
                        converter.OnConverted(spawned);
                    // 기둥 — 그 자리 소환의 첫 기 · 추가 소환으로 더 나온 기(대량보급) · 다른 유닛으로 바뀌어 나온 기(교황청 · 노동의 기계화)
                    if ((inPlace && i == 0) || i >= baseCount || converter != null)
                        PillarFx.Rise(new Vector2(spawned.Position.x, spawned.Feet), spawned.Top - spawned.Feet);
                    spawned.NotifySummoned(sourceSlot, source);
                    // 다른 층의 기물이 ~를 소환할 때마다 (군기 · 성당기사단) — 어느 층을 볼지는 능력이 정한다
                    if (sourceSlot >= 0)
                        for (int other = 0; other < _slots.Length; other++)
                            if (other != sourceSlot && IsActive(other))
                                foreach (BuildingPassive passive in _slots[other].Building.Passives)
                                    passive.OnSummonedFrom(this, other, sourceSlot, spawned);
                }
            }
        }

        // 마지막으로 소환한 유닛 — 유닛 효과가 소환한 뒤 버프 선을 그 유닛으로 긋는다(Summon을 부른 바로 뒤에만 읽는다)
        public Unit LastSpawned { get; private set; }

        // 작은 타워의 발치 — 거기서 소환한다(파견 사무소)
        private static Vector2 Feet(Base tower) => new Vector2(tower.transform.position.x, tower.Bounds.min.y);

        public void NotifyTowerKill(Unit victim)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (IsActive(i))
                    foreach (BuildingPassive passive in _slots[i].Building.Passives)
                        passive.OnTowerKill(this, i, victim);
        }

        // 내 작은 타워가 파괴됐다(Base가 부른다 — 사라지기 직전)
        public void NotifySmallTowerDestroyed(Base tower)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (IsActive(i))
                    foreach (BuildingPassive passive in _slots[i].Building.Passives)
                        passive.OnSmallTowerDestroyed(this, i, tower);
        }

        private bool HasBuilding(int index) => index >= 0 && index < _slots.Length && _slots[index].Building != null;

        // 즉시 1회 발동 — 게이지는 그대로(징집 포고문). 쿨다운 없는 기물은 발동하지 않는다(false)
        public bool FireNow(int index)
        {
            if (!HasBuilding(index) || _slots[index].Cooldown <= 0f || _slots[index].Frozen > 0f)
                return false;
            Fire(index);
            return true;
        }

        // 이 유닛 효과를 켠 기물들을 즉시 1회 발동(마구간 신화 「이 기물을 즉시 1회 발동시킵니다」) — 유닛 효과는 자기 칸을 모르므로 켠 칸을 여기서 찾는다.
        // 전투가 끝났거나 데스매치로 기물이 멈춘 뒤에는 발동하지 않는다(Update의 발동 루프와 같은 조건)
        // from = 일으킨 유닛의 자리 — 버프 선이 그 유닛에서 발동한 층으로 간다
        public void FireOwners(UnitEffect effect, Vector2 from)
        {
            if (!_running || BattleManager.BoardsStopped)
                return;
            for (int i = 0; i < _slots.Length; i++)
                if (IsActive(i) && System.Array.IndexOf(_slots[i].Building.Effects, effect) >= 0 && FireNow(i))
                    BuffLinks.EffectFromUnitToSlot(_team, effect, from, i, BuffKind.Trigger);
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
            int index = RandomCooling();
            if (index >= 0)
                Haste(index, seconds);
            return index;
        }

        // 쿨다운이 있는 기물 중 무작위 하나의 쿨다운에 곱한다(합치기 보상 업그레이드 「기름칠」). 고른 칸을 돌려준다(없으면 -1)
        public int ScaleRandomCooldown(float factor)
        {
            int index = RandomCooling();
            ScaleCooldown(index, factor);
            return index;
        }

        // 쿨다운이 있는 기물의 칸 중 무작위 하나(없으면 -1)
        public int RandomCooling()
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
                    return i;
            return -1;
        }

        // 쿨다운이 있는 기물 전부를 가속(수도원 신화) — 쿨다운 없는 기물은 HasteRandom과 같이 뺀다
        public void HasteAll(float seconds)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (HasBuilding(i) && _slots[i].Cooldown > 0f)
                    Haste(i, seconds);
        }

        // 가속 — seconds 동안 게이지가 2배로 찬다. 이미 가속 중이면 더 긴 쪽
        public void Haste(int index, float seconds)
        {
            if (HasBuilding(index))
                _slots[index].Haste = Mathf.Max(_slots[index].Haste, seconds);
        }
    }
}
