# ARCHITECTURE.md — 코드 구조 지도

> **AI 코딩 도우미용 지도.** grep으로 찾을 수 있는 건 안 적음 (파일 목록·함수 시그니처 X).
> **여기 적는 것**: 폴더 책임, 매니저 호출 관계, 핵심 처리 흐름, "X 추가하려면 어디 손대야 하나".
>
> **유지보수 원칙**: 큰 구조 바뀔 때만 갱신. 함수 추가/리네임 정도로는 손대지 말 것.
> 코드와 어긋난다 싶으면 코드가 정답 — 이 문서를 의심할 것.
>
> ⏳ **아직 코드가 없다.** 아래 표들은 첫 구현 때 채운다.

---

## 한눈에 보기

**Gnorp War = 사이드뷰 라인 디펜스 RTS.** 왼쪽 아군 기지와 오른쪽 적 기지 사이 한 줄 레인에서, 양 진영이 자원으로 유닛을 소환하고 유닛은 자동으로 전진·교전한다. **유닛은 Physics 2D 몸체라 막히면 점프해 서로의 머리 위에 올라서며 산처럼 쌓인다.** 죽은 유닛은 충돌에서 빠져 화면 밖으로 튕겨나간다. 상대 기지 체력을 먼저 0으로 만들면 승리. (기획은 [GDD.md](GDD.md), 진행 상태는 [HANDOFF.md](HANDOFF.md).)

**기술 스택**: Unity 6 (6000.3.16f1) · URP 2D · uGUI · Input System.

---

## 폴더 책임

루트: `Assets/`

| 폴더 | 책임 | 새 코드는 어디로 |
|---|---|---|
| `Assets/Scripts/Core/` | 여러 곳이 같이 쓰는 기본 타입 (`Team` · `IDamageable`) | 진영·공통 인터페이스 |
| `Assets/Scripts/Units/` | 유닛·공격·화살 (`Unit` · `UnitDefinition` · `AttackType` · `Projectile`) | 유닛 행동·유닛 SO 정의 |
| `Assets/Scripts/Bases/` | 기지 (`Base` · `BaseDefinition`) | 기지 로직·기지 SO 정의 |
| `Assets/Scripts/Battle/` | 한 판의 흐름·자원·적 AI (`BattleManager` · `BattleConfig` · `EnemySpawner` · `PlayerWallet`) | 판 단위 규칙·경제 |
| `Assets/Scripts/UI/` | 전투 HUD (`SummonButton` · `IncomeUpgradeButton` · `ResourceLabel` · `BaseHealthBar`) | 화면 표시·버튼 |
| `Assets/Scripts/Fx/` | 타격감 연출 (`FxDirector` · `CameraShake`) | 연출 |
| `Assets/Scripts/Dev/` | 측정·디버그 전용 (`AutoPlayer`) — 게임에 쓰이지 않음 | 개발 도구 |
| `Assets/Editor/` | 에디터 전용 툴 | 에디터 툴·인스펙터 |
| `Assets/Plugins/Sirenix/` | Odin Inspector (사용자가 추가한 **유료 에셋**) — `.gitignore`로 저장소에서 제외 | 손대지 말 것 |
| `Assets/Data/` | 데이터 에셋 (유닛 정의 SO · 물리 재질) | 코드 X, 에셋만 |
| `Assets/Prefabs/` | 프리팹 (유닛 등) | — |
| `Assets/Fx/` | 파티클 프리팹·재질 (`FX_HitSpark` · `FX_DeathPuff` · `FX_Pixel`) | — |
| `Assets/Sprites/` | 스프라이트. 사용자 도트(`gnorp` · `sword` · `shield` · `bow` · `arrow`, PPU 8)와 바닥·기지용 `Square.png`(PPU 32) | 새 그림 임포트 규칙은 CLAUDE.md 「픽셀 크기 컨벤션」 |
| `Assets/Settings/` | URP 설정 (템플릿) | — |

---

## 핵심 오브젝트 카탈로그 (매니저/싱글톤 등)

> 게임의 중심 객체들. "누가 누구를 부르나"가 핵심.

| 이름 | 책임 | 누구를 부르나 |
|---|---|---|
| `Unit` | 유닛 한 마리. 사거리 안에 적(`IDamageable`)이 있으면 멈춰서 공격 + 칼 찌르기(점프보다 우선). 없으면 전진, 앞 아군이 멈춰 있고 머리 위가 비고 기지 위가 아니면 점프. 피격 시 맞은 방향으로 밀림·찌그러짐·번쩍. 죽으면 콜라이더 끄고 뒤집혀 맨 앞 레이어로 튀어 떨어진 뒤 3초 후 파괴. 수치는 `UnitDefinition` SO, 연출 상수는 `Unit.cs` 상단 | 사거리 안 `IDamageable.TakeDamage(피해, 맞은 방향)` · 접촉한 `Unit`의 `IsStopped`·`IsHeadFree`·`_onBase` |
| `Base` | 진영 기지. 체력 0이면 로그 + 비활성화 + `Destroyed` 이벤트. 체력은 `BaseDefinition` SO. 자식 `BackStop`(보이지 않는 높은 콜라이더)이 기지 뒤쪽 끝을 막는다 — 기지가 꺼지면 같이 꺼진다 | — |
| `PlayerWallet` | 플레이어 자원. 초당 증가 + 적 처치 보상(`UnitDefinition.KillReward`), 최대치에서 멈춤. **획득 레벨**(`IncomeLevel`, 0부터)이 초당 획득량·최대치를 정한다 — 레벨 표는 `BattleConfig._incomeLevels` | `Unit.Died`(static 이벤트) 구독 |
| `IncomeUpgradeButton` | 획득 레벨 강화 버튼. 레벨·다음 비용(만렙이면 MAX) 표시 | `PlayerWallet.TryUpgradeIncome` |
| `RunInBackgroundInPlayMode` (Editor) | 에디터 플레이모드 진입 시 `Application.runInBackground = true` — 에디터가 뒤에 있어도 게임이 돌게(Claude의 MCP 플레이 검증용). 빌드 설정은 안 건드림 | — |
| `SummonButton` | 소환 버튼 하나. 쿨다운·자원 확인 후 소환 지점에 유닛 프리팹 생성 | `PlayerWallet.TrySpend` |
| `EnemySpawner` | 적 AI(단순 시간표). `BattleConfig.EnemySpawnIntervalAt(경과 시간)` 간격마다 `_unitPrefabs` 중 무작위 생성 — 간격은 시작→끝 값으로 점점 짧아짐 | — |
| `BaseHealthBar` | 기지 체력 비율을 채움 게이지로 표시 | `Base.Hp01` |
| `FxDirector` | 타격감 창구(씬의 `Fx` 오브젝트, `Instance`로 접근). 파편·사망 먼지(파티클 프리팹 `Assets/Fx/`) · 화면 흔들림 · 히트스톱 | `CameraShake.Add` |
| `CameraShake` | 메인 카메라에 붙음. 충격이 쌓였다 잦아드는 흔들림(실제 시간 기준) | — |
| `AutoPlayer` | **측정 전용.** 누를 수 있는 소환 버튼을 무작위로 계속 누름. 씬에 두지 않고 플레이 중에 `Battle`에 붙여 쓴다(HANDOFF 「밸런스」) | `Button.onClick` |
| `Projectile` | 화살·돌. 착탄 범위(`UnitDefinition.ProjectileSplashRadius`)가 0보다 크면 닿은 자리 반경 안 적 전부에게 피해 + 바깥·위로 날림(투석). 목표 지점에 떨어지도록 발사 속도를 역산(수평 속도 고정) → 중력 포물선. 트리거 — 아군 통과, 적에게 피해 후 소멸, 바닥·벽에 닿아도 소멸 | `IDamageable.TakeDamage` |
| `BattleManager` | 승패. 어느 기지든 `Destroyed` 이벤트가 오면 결과 패널 + `timeScale=0`, 다시하기 = 씬 재로드 | `Base.Destroyed` 구독 |

---

## 핵심 루프 — 1판의 흐름

(구현 후 채움)

---

## "X 추가하려면 어디 봐야 하나"

| 변경하고 싶은 것 | 손대야 할 파일 |
|---|---|
| **새 유닛 종류** | `Assets/Data/Unit_*.asset` 하나(수치·공격 방식·밀치는 힘·화살) + `Ally_*`/`Enemy_*` 프리팹 두 개(`*_Melee` 복제 → `_definition`·`Visual/Weapon` 그림 교체) + 위쪽 줄에 소환 버튼 복제 + `EnemySpawner._unitPrefabs`에 추가. 코드 X |
| **새 공격 방식** | `AttackType` enum(**맨 뒤에만**) + `Unit.FixedUpdate`의 공격 분기 |
| 전투 수치 | `Unit_*.asset` · `BattleConfig.asset` · `Base_Test.asset` (코드 X) |

---

## 자주 헷갈리는 것

- 🔴 **이 프로젝트의 아군 전방은 `+x`(`Vector2.right`), 적 전방은 `-x`다.** 아군 기지 왼쪽 · 적 기지 오른쪽.
  진행 방향은 진영(Team)에서 나온다 — 유닛 코드에 방향을 하드코딩하지 말 것.
- 🔴 **쌓기는 물리 엔진이 한다.** 유닛 위치를 `transform`으로 직접 옮기지 말 것 — 이동·점프는 `Rigidbody2D` 속도/힘으로.
  `transform`을 직접 쓰면 머리 위에 선 유닛과의 접촉이 끊겨 산이 무너지거나 겹친다.
- 🔴 **유닛 프리팹은 "몸체 루트(물리) + `Visual` 자식(그림·무기)"으로 나뉜다.** 찌그러짐·번쩍임 같은 연출은 `Visual`에만 건다.
  루트 스케일을 건드리면 충돌 박스가 같이 변해 쌓인 산이 흔들린다. (적의 좌우 반전만은 예외 — 루트 x스케일 -1, 박스 크기는 안 변한다)
- 🔴 **칸·사거리 검사는 트리거를 무시한다**(`Unit.SolidOnly`). 화살이 트리거라서, 무시하지 않으면 날아가는 화살 때문에 "머리 위가 막혔다"로 오판한다.
  새 물리 검사를 추가할 때도 같은 필터를 쓸 것.
- 🔴 **죽은 유닛은 즉시 충돌에서 빠져야 한다.** 튕겨나가는 동안 다른 유닛을 밀거나 받치면 안 된다(그래야 위의 산이 무너진다).
- 🔴 **`Time.timeScale`을 쓰는 곳이 셋이다** — 결과 화면(0) · 히트스톱(현재값×0.02) · 측정 배속(8). 히트스톱은 끝날 때 **자기가 건 값일 때만** 되돌린다.
  새로 timeScale을 만지는 코드를 넣으면 이 셋과 부딪히지 않는지 본다.
- 🔴 **카메라 위치를 코드로 옮기면 `CameraShake`가 흔들릴 때 원래 위치로 되돌린다**(Awake에서 기준 위치를 기억). 카메라 이동 기능을 넣으면 흔들림 기준도 같이 옮길 것.
- 🔴 **유닛 키는 가변이다 — `BoxCollider2D` 높이에서 읽는다**(`Unit._halfHeight`). 머리 위 칸·발밑 칸·소환 공간·찌그러짐 발 고정·점프 가능 높이가 전부 이 값 기준.
  키 큰 유닛(기사)의 그림은 `Visual` 아래 칸별 자식(`Horse`·`Rider`)으로 두고 `Visual` 자체엔 SpriteRenderer를 두지 않는다. 층 교환은 키가 같을 때만.
- 🔴 **유닛은 `Unit.Spawn(prefab, 바닥 지점)`으로만 만든다**(소환 버튼·적 스포너 모두). 소환 칸이 차 있으면 한 층씩 올라가 빈 가장 낮은 층에 만든다 — `Instantiate`로 바로 만들면 1층에 끼인다.
- **피격 경직 = 0.15초 × 밀치는 힘(최소 1)** — 그동안 `Unit.FixedUpdate`가 속도를 안 덮어써서 끝까지 날아간다. 돌격(`Charge*`)·방패·돌의 "날아감"은 전부 이 규칙에 기댄다.
- 🔴 **`Unit.Died`는 static 이벤트다 — 구독자는 `OnEnable`에서 걸고 `OnDisable`에서 반드시 푼다.** 안 풀면 씬을 다시 시작할 때 파괴된 구독자가 남는다.
- 🔴 **위치 교환(넘어가기)은 임시 받침대로 한다.** 구현은 `Unit.SwapDownWith`(위 유닛이 주도, 교환 중 두 유닛은 콜라이더 끔 + Kinematic). 두 유닛이 충돌을 끄고 넘어가는 동안, 둘이 있던 자리에
  보이지 않는 받침 콜라이더(높이 2)를 세워 위의 산을 받친다. 끝나면 받침대를 치우고 충돌을 다시 켠다.
  - 교환 중인 유닛은 **잠가서** 다른 교환에 끼지 못하게 한다.
  - 교환 중 한쪽이 죽으면 **받침대도 같이 정리**한다 — 안 하면 산이 허공에 뜬다.
- 🔴 **새 `SerializeField`를 추가할 때 클래스 기본값을 믿지 말 것.** 이미 임포트된 에셋·프리팹은 그 필드가 YAML에 없어도
  임포트 캐시의 옛 값을 쓴다. 그래서:
  1. 필드를 추가하면 **그 즉시 모든 기존 에셋에 값을 명시적으로 써 넣는다.**
  2. **불리언 기본값은 언제나 "기존 동작과 같은 쪽"으로.**
  3. 쓴 뒤 되읽어 확인한다.
- 🔴 **직렬화되는 enum은 맨 뒤에만 추가할 것** — 중간에 끼우면 기존 에셋의 정수 직렬화가 밀린다.

---

## 의도적으로 안 적은 것 (grep으로 찾을 것)

- 클래스의 public 메서드 목록 / 시그니처 → 파일 열어보면 됨
- 매니저별 SerializeField 필드 → Inspector 또는 코드 상단 보면 됨
- 밸런스 수치 → ScriptableObject 에셋
- UI 텍스트
