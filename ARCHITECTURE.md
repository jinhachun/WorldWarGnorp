# ARCHITECTURE.md — 코드 구조 지도

> **AI 코딩 도우미용 지도.** grep으로 찾을 수 있는 건 안 적음 (파일 목록·함수 시그니처 X).
> **여기 적는 것**: 폴더 책임, 매니저 호출 관계, 핵심 처리 흐름, "X 추가하려면 어디 손대야 하나".
>
> **유지보수 원칙**: 큰 구조 바뀔 때만 갱신. 함수 추가/리네임 정도로는 손대지 말 것.
> 코드와 어긋난다 싶으면 코드가 정답 — 이 문서를 의심할 것.

---

## 한눈에 보기

**Gnorp War = 사이드뷰 오토배틀러.** 상점에서 건물을 사서 필드 5칸에 놓으면, 전투 동안 건물이 쿨다운마다 유닛을 생산하거나 이웃 건물을 돕는다(조작 없음). 왼쪽 아군 기지와 오른쪽 적 기지 사이 한 줄 레인에서 유닛은 자동으로 전진·교전한다. **유닛은 Physics 2D 몸체라 막히면 점프해 서로의 머리 위에 올라서며 산처럼 쌓인다.** 죽은 유닛은 충돌에서 빠져 화면 밖으로 튕겨나간다. 상대 기지 체력을 먼저 0으로 만들면 승리. (기획은 [GDD.md](GDD.md), 진행 상태는 [HANDOFF.md](HANDOFF.md).)

**기술 스택**: Unity 6 (6000.3.16f1) · URP 2D · uGUI · Input System.

---

## 폴더 책임

루트: `Assets/`

| 폴더 | 책임 | 새 코드는 어디로 |
|---|---|---|
| `Assets/Scripts/Core/` | 여러 곳이 같이 쓰는 기본 타입 (`Team` · `IDamageable` · `Pooled`) | 진영·공통 인터페이스·풀 |
| `Assets/Scripts/Units/` | 유닛·공격·화살·분류 (`Unit` · `UnitDefinition` · `UnitCategory` · `AttackType` · `Projectile`) | 유닛 행동·유닛 SO 정의 |
| `Assets/Scripts/Units/Effects/` | 유닛 효과 — 효과마다 파일 하나(`UnitEffect` 상속) + 진영별 켜진 효과 `TeamEffects` | 새 유닛 효과 |
| `Assets/Scripts/Buildings/` | 건물 (`BuildingDefinition` · `BuildingAction`과 액션들 · `OwnedBuilding` · `BoardRunner`) | 건물·발동 액션 |
| `Assets/Scripts/Run/` | 런 상태·상점 규칙 (`RunState`) | 골드·목숨·구매/판매 규칙 |
| `Assets/Scripts/Bases/` | 본진 (`Base` · `BaseDefinition`) — 본진은 화살을 쏘는 타워 | 본진 로직과 SO 정의 |
| `Assets/Scripts/Battle/` | 라운드 흐름·전장 (`BattleManager` · `BattleConfig` · `Ground`) | 라운드 단위 규칙 |
| `Assets/Scripts/UI/` | HUD·상점 화면 (`BoardView` · `SlotView` · `ShopPanel` · `OfferCard` · `RampLabel` · `BaseHealthBar` · `CameraDrag`) | 화면 표시·버튼 |
| `Assets/Scripts/Fx/` | 타격감 연출 (`FxDirector` · `CameraShake`) | 연출 |
| `Assets/Scripts/Dev/` | 측정·디버그 전용 (`StackOrderProbe` · `PerfProbe`) — 게임에 쓰이지 않음 | 개발 도구 |
| `Assets/Editor/` | 에디터 전용 툴 | 에디터 툴·인스펙터 |
| `Assets/Plugins/Sirenix/` | Odin Inspector (사용자가 추가한 **유료 에셋**) — `.gitignore`로 저장소에서 제외 | 손대지 말 것 |
| `Assets/Data/` | 데이터 에셋 (유닛 정의 · `BattleConfig` · 분류 · 물리 재질) · `Buildings/`(건물·액션) · `Effects/`(유닛 효과) | 코드 X, 에셋만 |
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
| `Base` | 진영 본진 = 타워. 사거리(본진 중심 기준) 안 가장 가까운 적 유닛에게 자식 `Muzzle`에서 화살. 체력 0이면 로그 + 비활성화 + `Destroyed` 이벤트. 체력·화살 수치는 `BaseDefinition` SO. 자식 `BackStop`(보이지 않는 높은 콜라이더)이 기지 뒤쪽 끝을 막는다 — 기지가 꺼지면 같이 꺼진다 | `Projectile.Launch` |
| `RunState` | 런 상태(static — 라운드마다 씬을 다시 불러와도 남는다): 골드·목숨·승수·라운드·필드/보관함(`OwnedBuilding[]`)·진열. 상점 규칙(구매 = 가진 것 중 같은 기물·시작 등급이 있으면 합쳐 한 등급 위 — 또 짝이 생기면 이어서 합치고 결과는 먼저 있던 칸에, 없으면 필드 → 보관함 빈 칸 · 판매 = 가격 × 합쳐진 수 × 비율 · 리롤 · 맞바꾸기)도 여기. 상점 골드는 라운드당 한 번(`EnterShop`) | — |
| `BattleManager` | 라운드 흐름(`Battle`에 붙음). 씬 로드 = 상점(`RunState.EnterShop`, 두 보드 `Load`) → `StartBattle`(효과 비우고 두 보드 `Begin`) → 기지 `Destroyed` → `RunState.FinishBattle` + 결과 패널(`timeScale=0`) → 다음 라운드 = 씬 재로드. static `Fighting`·`Elapsed`·`DamageMultiplier`·`ExtraUnits`(가속 — 공격력 배율 · 생산 수 가산, `BattleConfig`) | `Base.Destroyed` 구독 · `BoardRunner` |
| `BoardRunner` | 한 진영의 필드 보드를 전투 동안 돌린다(`AllyBoard`·`EnemyBoard`, `For(team)`). 칸마다 게이지가 0부터 차고, 차면 발동 = ① 생산(유닛 수 × 레벨) ② `BuildingAction`들 ③ 건물 효과 스택 +1. 🔴 **칸당 한 프레임에 한 번만 발동**(서로 발동시키는 기물끼리도 무한 연쇄 없음 — 넘친 시간은 다음 프레임). **`Summon`이 이 진영 모든 소환의 입구** — 기획서 §5 순서대로 효과의 추가 소환 → 변환 → 한 마리씩 소환 이벤트(`Unit.SourceSlot` = 소환한 칸, 기물이 아니면 -1). 자리를 안 주면 성문(넓은 유닛은 큰 유닛 소환 지점) | `Unit.Spawn` · `Unit.NotifySummoned` · `TeamEffects` |
| `BuildingDefinition` · `BuildingAction` · `OwnedBuilding` | SO / 데이터. 건물 = 이름·설명·등급(가격·출현 확률은 `BattleConfig`의 등급 표)·쿨다운·생산 유닛·유닛 수·액션들·효과들(`Assets/Data/Buildings/`). 액션 = 발동 때 하는 일 하나(레벨에 따라 무엇이 커지는지는 액션이 정한다). `OwnedBuilding` = (기물, 시작 등급에서 오른 횟수)만 담는 순수 데이터 — 지금 등급 = 시작 등급 + 오른 횟수(전설이 끝), `Level` = 오른 횟수 + 1 (나중에 비동기 상대 보드로 저장·전송하는 단위) | — |
| `UnitEffect` · `TeamEffects` | 유닛 효과(`Assets/Data/Effects/`). 적용 대상 = 병종 태그 또는 유닛 이름(둘 다 비우면 전부) + 훅(이동 속도·사거리·매 스텝·적 머리 착지·원거리 사격·피격·회복·소환의 추가/변환/이벤트)을 가상 메서드로. 건물이 필드에 있으면 전투 시작부터 그 진영에 켜지고(`TeamEffects.Enable`), 발동마다 스택 +1. 지금 남은 효과는 이중 회복(`DoubleHealEffect`)뿐 | `Unit`의 공개 도우미(`FireProjectile`·`ThrowHeal`·`FindHealTarget` …) |
| `Ground` | 굽은 땅(씬의 `Ground`, `Instance`). 사인 3개를 겹친 비대칭 언덕 높이 함수 `HeightAt(x)` 하나가 원본 — **씬을 불러올 때마다 위상·높이를 새로 뽑아** 충돌(PolygonCollider2D)·그림(LineRenderer)을 만든다. 양 끝(기지·소환 자리)은 평평, `_flatAnchors`(지금은 비어 있음) 밑은 평평한 단을 만들고 밑면을 그 위로 옮긴다 | — |
| `BoardView` · `SlotView` · `ShopPanel` · `OfferCard` · `RampLabel` | UI. `BoardView` = 칸 한 줄(아군 필드·적 필드·보관함, 틀 칸 복제) — 아군 필드는 전투 전엔 `RunState`, 전투 중엔 `BoardRunner`를 본다. `ShopPanel` = The Bazaar식 상점(진열 · 가운데 필드 · 보관함) — **끌어서** 칸 이동/맞바꿈 · 진열을 칸에 놓아 구매(`RunState.BuyInto`) · 판매 버튼에 놓아 판매. 누르기는 선택(설명)·진열 빈 칸 구매. 끌기 이벤트는 `SlotView`·`OfferCard`·`SellDropZone`이 받아 `ShopPanel`로 넘긴다. `RampLabel` = 전투 시간 + 가속(공격력·생산 수) | `RunState` · `BattleManager.StartBattle` |
| `RunInBackgroundInPlayMode` (Editor) | 에디터 플레이모드 진입 시 `Application.runInBackground = true` — 에디터가 뒤에 있어도 게임이 돌게(Claude의 MCP 플레이 검증용). 빌드 설정은 안 건드림 | — |
| `FlameBit` | 화염방사 그림 한 조각(충돌 없음). 피해는 `Unit.BreatheFire`가 띠 판정으로 준다 | — |
| `BaseHealthBar` | 기지 체력 비율을 채움 게이지로 표시 | `Base.Hp01` |
| `FxDirector` | 타격감 창구(씬의 `Fx` 오브젝트, `Instance`로 접근). 파편·사망 먼지(파티클 프리팹 `Assets/Fx/`) · 화면 흔들림 · 히트스톱 | `CameraShake.Add` |
| `DamageNumbers` | 씬 루트의 `DamageNumbers`(원점). 데미지·회복 숫자 전부를 **메시 하나**로 그린다(드로우콜 1 · 프레임 할당 0 · 최대 512개, 넘치면 버림). 같은 대상이 0.2초 안에 또 맞으면 떠 있는 숫자에 더한다. 글꼴은 코드가 만든 3×5 픽셀 숫자, 재질은 `FX_Pixel` 복제 | — (`Unit`·`Base`·`Tower`의 `TakeDamage`, `Unit.Heal`이 부른다) |
| `CameraShake` | 메인 카메라에 붙음. 충격이 쌓였다 잦아드는 흔들림(실제 시간 기준) | — |
| `CameraDrag` | `CameraRig`(카메라 부모)에 붙음. 전장을 끌면 좌우 이동, 놓으면 관성으로 미끄러짐, 맵 끝에서 멈춤. UI 위에서 누르면 무시 | `EventSystem.IsPointerOverGameObject` |
| `Projectile` | 화살·돌·회복 구슬. 회복 모드(`LaunchHeal`)는 적·던진 본인·체력 가득 찬 아군을 통과하고 처음 닿은 다친 아군에게 `Unit.Heal`. 착탄 범위(`UnitDefinition.ProjectileSplashRadius`)가 0보다 크면 닿은 자리 반경 안 적 전부에게 피해 + 바깥·위로 날림(투석). 목표 지점에 떨어지도록 발사 속도를 역산(수평 속도 고정) → 중력 포물선. 트리거 — 아군 통과, 적에게 피해 후 소멸, 바닥·벽에 닿아도 소멸 | `IDamageable.TakeDamage` |

---

## 핵심 루프 — 한 런의 흐름

```
씬 로드 ─► 상점 (RunState.EnterShop: 골드 + 새 진열 · 적 보드 = BattleConfig 라운드 표)
   │  구매·판매·리롤·맞바꾸기 (RunState)
   ▼
"전투 시작" ─► TeamEffects 비움 → 두 BoardRunner.Begin (효과 켜기 · 게이지 0부터)
   │  칸 게이지 참 → 생산 / 액션 / 효과 스택    · 60초부터 10초마다 공격력 +10%(단리)
   ▼
어느 기지 파괴 ─► RunState.FinishBattle (승 +1 / 패 목숨 -1, 라운드 +1) → 결과 패널
   ▼
"다음 라운드" = 씬 재로드 ─► 상점 …   (목숨 0 또는 승리 목표 → 다음 로드에서 새 런)
```

---

## "X 추가하려면 어디 봐야 하나"

| 변경하고 싶은 것 | 손대야 할 파일 |
|---|---|
| **새 유닛 종류** | `Assets/Data/Unit_*.asset` 하나(이름·**분류**·수치·공격 방식·밀치는 힘·화살·**진영별 프리팹 두 칸**) + `Ally_*`/`Enemy_*` 프리팹 두 개(`*_Melee` 복제 → `_definition`·`Visual/Weapon` 그림 교체) + 그 유닛을 만드는 건물. 코드 X |
| **새 생산 건물** | `Assets/Data/Buildings/Building_*.asset`(등급·쿨다운·유닛·유닛 수) + 상점에 내려면 `BattleConfig`의 `Shop Pool`, 적이 쓰면 `Enemy Rounds`. 코드 X |
| **기존 효과를 건물에** | 건물의 `Effects`에 `Effect_*.asset`을 넣는다(적용 분류는 효과 에셋이). 같은 효과를 다른 분류에 쓰려면 효과 에셋을 복제해 분류만 바꾼다. 코드 X — 단 효과마다 전제가 있다: DoubleHeal은 회복 유닛에서만 동작 |
| **새 유닛 효과** | `Units/Effects/`에 `UnitEffect` 상속 클래스 하나(있는 훅만 쓰면 `Unit` 수정 X, 수치는 필드로) + `Assets/Data/Effects/` 에셋. 새 훅이 필요하면 `UnitEffect`에 가상 메서드 + `Unit`의 해당 지점에서 `TeamEffects.For(_team)`을 돌며 부른다 |
| **새 건물 발동 효과(유틸)** | `Buildings/`에 `BuildingAction` 상속 클래스 하나(`Execute(board, slot, level)`, 이웃은 slot±1) + 에셋 + 건물의 `Actions`에 추가. 보드를 바꾸는 동작이 더 필요하면 `BoardRunner`에 공개 메서드 |
| **새 공격 방식** | `AttackType` enum(**맨 뒤에만**) + `Unit.FixedUpdate`의 공격 분기 |
| **새 보스** | `Unit_*.asset`(태그 중기병) + `Ally_*`·`Enemy_*` 프리팹(`*_Dino` 복제) + 생산 건물. 점프 착지 충격은 `UnitDefinition`의 Slam 항목(간격 0이면 없음) |
| 전투·상점 수치 | `Unit_*.asset` · `Building_*.asset` · `BattleConfig.asset`(골드·목숨·가속·적 라운드) · `Base_Test.asset` (코드 X) |

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
- 🔴 **전투 시간은 `BattleManager.Elapsed`**(전투 시작부터, 상점 시간 제외) — `Time.timeSinceLevelLoad`는 상점 시간까지 들어가므로 쓰지 말 것.
- 🔴 **`Time.timeScale`을 쓰는 곳이 넷이다** — 결과 화면(0) · 상점(1, `BattleManager.Awake`) · 전투 배속(`SpeedButton.Current` ×1/2/4 — 전투 시작·버튼 누를 때) · 히트스톱(현재값×0.02). 측정용 배속(8)은 손으로 걸 때만. 히트스톱은 끝날 때 **자기가 건 값일 때만** 되돌린다.
  새로 timeScale을 만지는 코드를 넣으면 이 셋과 부딪히지 않는지 본다.
- 🔴 **카메라를 옮길 땐 부모 `CameraRig`를 움직인다**(`CameraDrag`). `CameraShake`는 자식 카메라의 localPosition을 Awake 때 값으로 되돌리므로, 카메라 자체를 옮기면 흔들릴 때 원래 자리로 튄다.
- 🔴 **땅은 평평하지 않고 전투마다 바뀐다 — 땅 높이가 필요하면 `Ground.Instance.HeightAt(x)`를 쓴다.** y를 상수로 박지 말 것. 밑이 평평해야 하는 새 구조물은 `Ground._flatAnchors`에 넣는다(그 구조물의 y는 `Ground`가 판 시작 때 맞춘다 — 씬의 y는 의미 없음).
- 🔴 **맵 길이를 바꾸면 씬 값을 같이 옮긴다** — `Ground._minX/_maxX`(·평평 구간 `_flatFrom`) · 두 기지 · `AllySpawnPoint`·`EnemySpawnPoint`·`BossSpawnPoint` · `CameraDrag._minX/_maxX`.
- 🔴 **유닛 키는 가변이다 — `BoxCollider2D` 높이에서 읽는다**(`Unit._halfHeight`). 머리 위 칸·발밑 칸·소환 공간·찌그러짐 발 고정·**점프 높이(= 키 + 1, 유닛 정의에 값 없음)**가 전부 이 값 기준.
  키 큰 유닛(기사)의 그림은 `Visual` 아래 칸별 자식(`Horse`·`Rider`)으로 두고 `Visual` 자체엔 SpriteRenderer를 두지 않는다. 층 교환은 키가 같을 때만.
- 🔴 **효과는 진영 단위로 켜진다**(`TeamEffects.For(team)`) — 유닛 정의(`Unit_*.asset`)는 양 진영이 같이 쓰므로 효과를 정의에 달지 말 것.
- 🔴 **유닛 공격 피해는 `Unit.AttackDamage × Unit.DamageScale`이다**(스탯 계산 × 전투 공격력 가속). 새 공격을 만들 때 빠뜨리면 기물 강화·가속이 안 먹어 판이 안 끝날 수 있다. 본진 화살은 가속 대상이 아니다.
- 🔴 **유닛 스탯(공격력·최대 체력·이동속도·공격속도)은 `_definition`에서 바로 읽지 말고 `Unit`의 계산값(`AttackDamage`·`MaxHp`·`MoveSpeed`·`AttackInterval`)을 읽는다.** 계산 = 기획서 §6 `(기본 + 고정 합) × (1 + % 합) × 곱들`(`StatSum`).
  변경을 거는 곳은 셋: 진영 단위 전투 동안 `StatBook.AddForBattle` · 영구히(아군만, 런 동안) `StatBook.AddForRun` · 한 유닛만 `Unit.AddModifier`. 대상은 건 효과의 `AppliesTo`. 유닛은 `StatBook.Version`이 바뀔 때만 다시 계산한다. 최대 체력이 바뀌면 지금 체력도 같은 비율로.
- **피해 없이 밀기는 `Unit.Shove`**. `TakeDamage(0, …)`로 밀면 파편·번쩍·경직 규칙까지 따라온다.
- **화염(`AttackType.Flame`)은 무기 자리(`Visual/Weapon`)를 입으로 쓴다** — 그래서 화염 유닛은 찌르기 동작을 하지 않는다(입이 움직이면 불이 따라 흔들림).
- 🔴 **`TakeDamage`의 `attacker`는 피해를 준 쪽이다**(유닛 — 근접·투사체·화염 모두, 또는 본진). 처치자 판정(`UnitEffect.OnKill`)이 이걸 읽으므로 새 공격도 반드시 넘길 것.
  투사체는 쏜 유닛의 생애 번호(`Unit.Life`)를 같이 기억한다 — 날아가는 사이 그 유닛이 죽어 풀에서 다른 유닛으로 다시 쓰이면 처치자 없음.
- 🔴 **게임 규칙상의 소환은 `BoardRunner.For(team).Summon(…)`으로 한다**(기물 생산·죽은 자리 소환 모두 — 그래야 추가 소환·변환·소환 이벤트를 거친다). 그 안에서 유닛은 `Unit.Spawn(prefab, 바닥 지점)`으로만 만든다. 소환 칸이 차 있으면 한 층씩 올라가 빈 가장 낮은 층에 만든다 — `Instantiate`로 바로 만들면 1층에 끼인다.
- **피격 경직 = 0.15초 × 밀치는 힘(최소 1)** — 그동안 `Unit.FixedUpdate`가 속도를 안 덮어써서 끝까지 날아간다. 돌격(`Charge*`)·방패·돌의 "날아감"은 전부 이 규칙에 기댄다.
  **밀치는 힘이 0인 공격은 밀림·경직이 아예 없다**(화염처럼 짧은 간격으로 계속 맞는 공격이 대상을 영구 경직시키지 않게).
- 🔴 **유닛·투사체·이펙트는 풀(`Pooled`)에서 꺼낸다 — `Instantiate`/`Destroy` 대신 `Pooled.Get`/`SetActive(false)`.** 꺼지는 순간 스스로 풀로 돌아간다.
  재사용되므로 **한 생애의 상태는 `OnEnable`에서 처음 값으로 되돌린다**(`Unit.OnEnable` · `Projectile.OnEnable` · `FlameBit.Launch`). 새 상태 필드를 추가하면 거기에도 넣을 것. 죽은 유닛은 파괴되지 않으므로 **죽음은 `IsAlive`로 판단**(참조가 null인지로 보지 말 것).
- 🔴 **유닛이 수백이 되는 게임이라 `Unit.FixedUpdate`가 병목이다**(9/29 측정: 프레임의 약 90%). 매 스텝 도는 코드에 물리 검색·컴포넌트 조회를 늘리지 말 것.
  주변 검색(적·회복 대상·기지 위 유닛)은 `TargetScanInterval`(0.1초)마다만 새로 하고 그 사이엔 캐시를 검증만 한다. 적 찾기는 진영 레이어(`AllyUnit`/`EnemyUnit`, `Unit.Awake`가 지정)로 거른 `_enemyFilter`, 아군 찾기는 `_allyFilter`. 충돌 규칙은 레이어와 무관하게 전부 켜져 있다.
  성능을 다시 볼 땐 `PerfProbe`(Dev)를 붙인다 — `Unit.Contacts/Scan/Support/Walk` 프로파일러 구간을 읽는다. 물리 몰아 돌리기 상한은 `Maximum Allowed Timestep` 0.1(프로젝트 설정).
- 🔴 **static 상태는 씬 재로드(= 다음 라운드)에도 남는다** — `RunState`는 그걸 일부러 쓰고, `TeamEffects`는 전투 시작마다 비운다. 새 static을 만들면 라운드마다 비울지 먼저 정할 것.
- 🔴 **층 정렬은 자리 맞바꾸기가 아니라 "애초에 뒤집혀 오르지 않기"로 한다**(사용자 결정 9/29). `Unit.BelongsBelow`(`StackRank`) 하나로 세 곳이 판정한다:
  ① 등반 — 나보다 위층 역할 아군 등에는 뛰어오르지 않고 뒤에서 기다린다 ② 소환(`Unit.Spawn`) — 위층 칸은 바로 아래가 나보다 아래·같은 역할일 때만 ③ 이미 뒤집혀 탔으면(넉백·낙하) 위 유닛이 싸움보다 먼저 앞으로 걸어 내려가고 밑 유닛은 멈춘다.
  사방이 막힌 산 안쪽은 정렬되지 않는다(실전 약 15% 뒤집힘 — 측정 `StackOrderProbe`).
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
