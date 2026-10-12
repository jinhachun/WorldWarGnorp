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
| `Assets/Scripts/Core/` | 여러 곳이 같이 쓰는 기본 타입 (`Team` · `IDamageable` · `Pooled`) · `LordPaint`(그림의 파랑 `#81ABFF`만 진영 영주 색으로 바꾼 스프라이트를 만들어 캐시 — 유닛 몸·시체·툴팁 카드가 같이 쓴다. 진영 → 색은 `RunState.LordColor` 한 곳) · `BalanceView`(밸런싱 창이 그리는 동안만 켜지는 표시 플래그 — 게임은 읽지 않는다) | 진영·공통 인터페이스·풀 |
| `Assets/Scripts/Units/` | 유닛·공격·화살·분류 (`Unit` · `UnitDefinition` · `UnitCategory` · `AttackType` · `Projectile`) | 유닛 행동·유닛 SO 정의 |
| `Assets/Scripts/Units/Effects/` | 유닛 효과 — 효과마다 파일 하나(`UnitEffect` 상속) + 진영별 켜진 효과 `TeamEffects` | 새 유닛 효과 |
| `Assets/Scripts/Buildings/` | 건물 (`BuildingDefinition` · `BuildingAction`과 액션들 · `OwnedBuilding` · `BoardRunner`) | 건물·발동 액션 |
| `Assets/Scripts/Run/` | 런 상태·상점 규칙 (`RunState`) | 골드·목숨·구매/판매 규칙 |
| `Assets/Scripts/World/` | 월드맵 규칙 — 판도·영주·턴 (스펙 `Docs/WorldWarGnorp_아웃게임기획.md`). 규칙 6파일(`WorldGraph` · `Lord` · `LordTemper` · `WorldRules` · `WorldState` · `WorldTurn`)은 **`UnityEngine`을 쓰지 않는다** — Unity 밖에서 수천 판을 돌려 검증하려고(영주의 힘·난수는 밖에서 받는다). SO = `WorldMapDefinition`(손으로 짠 맵 한 장) · `RegionUpgradeDefinition` · `WorldConfig` | 지역·영주·턴 규칙 |
| `Assets/Scripts/Bases/` | 본진 (`Base` · `BaseDefinition`) — 본진은 화살을 쏘는 타워 | 본진 로직과 SO 정의 |
| `Assets/Scripts/Battle/` | 라운드 흐름·전장 (`BattleManager` · `BattleConfig` · `Ground`) | 라운드 단위 규칙 |
| `Assets/Scripts/UI/` | HUD·상점 화면 (`BoardView` · `SlotView` · `ShopPanel` · `OfferCard` · `RampLabel` · `BaseHealthBar` · `CameraDrag`) | 화면 표시·버튼 |
| `Assets/Scripts/Fx/` | 타격감 연출 (`FxDirector` · `CameraShake`) | 연출 |
| `Assets/Scripts/Dev/` | 개발·테스트 전용 — 게임에 쓰이지 않음 (측정 도구 `PyramidProbe`·`StackOrderProbe`·`PerfProbe`는 10/10 정리 때 지움 — 다시 필요하면 커밋 `3494db7`의 `Assets/Scripts/Dev/`에서 꺼낸다). `CheatPanel` = F1 테스트 패널(탭 4개: 기물 · 값 · 전투 · 런 — 값 란은 플레이 중 상태만 바꾸고 에셋은 안 건드림, 숫자 입력은 `Keyboard.current.onTextInput`, 뒤 UI는 패널 크기의 투명 uGUI 판으로 막음. 게임이 켜질 때 스스로 생김, 게임 쪽은 `RunState`·`BattleManager`의 `Cheat*` 훅만 부름, 배포 땐 `Enabled`를 끈다) | 개발 도구 |
| `Assets/Editor/` | 에디터 전용 툴. `BalanceWindow` = 메뉴 GnorpWar → 밸런싱(Odin 메뉴 창 — 유닛 표 · 유닛 · 전투·경제 설정 · 맵 · 적 빌드, 유형으로 에셋을 찾고 바뀌면 바로 저장. 새 유형은 `Groups`에, 표의 칸은 `UnitRow`에 한 줄) · `WorldMapTable`(30지역 표) + `WorldAssetsMenu`(GnorpWar → 월드 에셋 만들기) | 에디터 툴·인스펙터 |
| `Assets/Plugins/Sirenix/` | Odin Inspector (사용자가 추가한 **유료 에셋**) — `.gitignore`로 저장소에서 제외. 밸런싱 창과 데이터 클래스 어트리뷰트가 쓴다(규칙은 CLAUDE.md §5) | 손대지 말 것 |
| `Assets/Data/` | 데이터 에셋. 루트 = `BattleConfig` · 본진 정의 · 물리 재질 / `Units/`(유닛 정의 `Unit_*` · 병종 `Category_*`) / `Buildings/`(기물 `Building_*`) · `Buildings/Actions/` · `Buildings/Passives/` / `Effects/`(유닛 효과) / `Maps/` / `EnemyBuilds/` / `World/`(`WorldConfig` · `WorldMap_Main` · `Upgrades/`) | 코드 X, 에셋만 |
| `Assets/Prefabs/` | 프리팹. `Units/`(`Ally_*` · `Enemy_*`) · `Projectiles/`(화살 · 회복 구슬 · 돌 · 타워 화살) · 루트의 `SmallTower` | — |
| `Assets/Fx/` | 파티클 프리팹·재질 (`FX_HitSpark` · `FX_DeathPuff` · `FX_Pixel`) | — |
| `Assets/Sprites/` | 스프라이트. `Units/`(유닛 몸) · `Weapons/`(무기·소지품) · `World/`(성 · 깃발 · 작은 타워) · `Fx/`(불꽃 · `Square.png` PPU 32) · `Backdrop/` · `Icons/` · `UI/` | 새 그림 임포트 규칙은 CLAUDE.md 「픽셀 크기 컨벤션」 |
| `Assets/Settings/` | URP 설정 (템플릿) | — |

---

## 핵심 오브젝트 카탈로그 (매니저/싱글톤 등)

> 게임의 중심 객체들. "누가 누구를 부르나"가 핵심.

| 이름 | 책임 | 누구를 부르나 |
|---|---|---|
| `Unit` | 유닛 한 마리. 사거리 안에 적(`IDamageable`)이 있으면 멈춰서 공격 + 칼 찌르기(점프보다 우선). 없으면 전진, 앞 아군이 멈춰 있고 머리 위가 비고 기지 위가 아니면 점프. 피격 시 맞은 방향으로 밀림·찌그러짐·번쩍. 죽으면 콜라이더 끄고 뒤집혀 맨 앞 레이어로 튀어 떨어진 뒤 3초 후 파괴. 수치는 `UnitDefinition` SO, 연출 상수는 `Unit.cs` 상단 | 사거리 안 `IDamageable.TakeDamage(피해, 맞은 방향)` · 접촉한 `Unit`의 `IsStopped`·`IsHeadFree`·`_onBase` |
| `Base` | 진영 본진 = 타워. 사거리(본진 중심 기준) 안의 적(유닛·적 본진)을 가까운 순으로 — 화살 여러 발이면 한 발씩 나눠, 모자라면 처음부터 — 자식 `Muzzle`에서 쏜다. 화살이 처치하면 `BoardRunner.NotifyTowerKill`(기물 `OnTowerKill`). 체력 0이면 로그 + 비활성화 + `Destroyed` 이벤트. 기본 수치는 `BaseDefinition` SO, 기물 강화는 `TowerBook`(공격력·공속·사거리·화살 수·체력, 전투 동안/영구히 — 최대 체력이 바뀌면 지금 체력도 같은 비율). 데스매치 뒤에는 쏘지 않는다 — `TowerBook.AttacksInDeathmatch`가 켜진 진영만 계속 쏜다(`TowerDeathmatchPassive`, 영구기관 오버드라이브). `HealRatio`로 회복. 자식 `BackStop`(보이지 않는 높은 콜라이더)이 기지 뒤쪽 끝을 막는다 — 기지가 꺼지면 같이 꺼진다 | `Projectile.Launch` |
| `RunState` | 런 상태(static — 라운드마다 씬을 다시 불러와도 남는다): 골드·목숨·승수·라운드·필드/보관함(`OwnedBuilding[]`)·진열. 상점 규칙(구매 = 가진 것 중 같은 기물·시작 등급이 있으면 합쳐 한 등급 위 — 또 짝이 생기면 이어서 합치고 결과는 먼저 있던 칸에, 없으면 필드 → 보관함 빈 칸 · 판매 = 가격 × 합쳐진 수 × 비율 · 리롤 · 맞바꾸기)도 여기. 상점 골드는 라운드당 한 번(`EnterShop`) | — |
| `BattleManager` | 라운드 흐름(`Battle`에 붙음). 씬 로드 = 상점(`RunState.EnterShop`, 두 보드 `Load`) → `StartBattle`(효과 비우고 두 보드 `Begin`) → 기지 `Destroyed` **또는 데스매치 판정**(경과 `BoardStopSeconds` 뒤 양쪽 유닛 0이고 지금 쏠 수 있는 타워에 겨눌 대상이 없으면(`Base.AnyTowerHasTarget`) 본진 체력과 무관하게 아군 승리(사용자 10/11) — 타워가 아직 상대 본진·작은 타워를 깎을 수 있으면 계속 둔다. 마지막 그물 = 교착 판정: 기물 정지 뒤 `BattleConfig._stalemateSeconds` 동안 피해·사망이 없으면(`BattleManager.NoteProgress`를 `Unit.TakeDamage`·`Perish`·`Base.TakeDamage`가 부른다 — 회복·걷기는 진행 아님) 아군 승리 · 한쪽 0이고 상대가 공격 못 하는 유닛(회복 전용·공격력 0)만이면 상대 승리 · 유닛이 낳는 소환은 그 뒤에도 계속) → `Finish` → `RunState.FinishBattle` + 결과 패널(`timeScale=0`) → 다음 라운드 = 씬 재로드. static `Fighting`·`Elapsed`·`DamageMultiplier`·`ExtraUnits`(가속 — 공격력 배율 · 생산 수 가산, `BattleConfig`) · `TowersStopped`(데스매치 — `Base`가 읽어 화살을 멈춤)·`BoardsStopped`(유예 뒤 — `BoardRunner`가 읽어 기물 발동을 멈춤) | `Base.Destroyed` 구독 · `BoardRunner` |
| `BoardRunner` | 한 진영의 필드 보드를 전투 동안 돌린다(`AllyBoard`·`EnemyBoard`, `For(team)`). 칸마다 게이지가 0부터 차고, 차면 발동 = ① 생산(등급과 상관없이 `SummonCount`기 — **등급은 생산 기물의 쿨다운을 줄인다**: `BuildingDefinition.CooldownAt(level)` = 쿨다운 × `BattleConfig._rankCooldownFactor`^(Level−1), 생산 기물만, 사용자 10/10. 풍차·가속·맵 배율은 그 위에 곱, `Slot.CooldownScale`에는 등급 몫 없음. 성당기사단 조건 소환·조립 라인·대량보급 등 "능력 소환"은 등급마다 수가 늚) ② `BuildingAction`들 ③ 건물 효과 스택 +1. 🔴 **칸당 한 프레임에 한 번만 발동**(서로 발동시키는 기물끼리도 무한 연쇄 없음 — 넘친 시간은 다음 프레임). **`Summon`이 이 진영 모든 소환의 입구** — 기획서 §5 순서대로 효과의 추가 소환 → 변환 → 한 마리씩 소환 이벤트(`Unit.SourceSlot` = 소환한 칸, 기물이 아니면 -1). 자리를 안 주면 성문(넓은 유닛은 큰 유닛 소환 지점) | `Unit.Spawn` · `Unit.NotifySummoned` · `TeamEffects` |
| `BuildingDefinition` · `BuildingAction` · `BuildingPassive` · `OwnedBuilding` | SO / 데이터. 기물 = 이름·설명·등급(가격·출현 확률은 `BattleConfig`의 등급 표)·국가·세력·쿨다운(0 = 쿨다운 없음, 발동 안 함)·생산 유닛·유닛 수·액션들·효과들·패시브들 · 설명에 나오는 유닛(`MentionedUnits` — 툴팁 유닛 판 순서, 소환 유닛 포함·병종 태그 제외, **설명 글을 고치면 같이 맞출 것**)(`Assets/Data/Buildings/`). 액션 = 쿨다운 발동 때 하는 일 하나(툴팁은 "N초마다" 뒤 구절). 패시브 = 늘 켜진 기물 능력(전투 시작 · 인접 기물 소환 훅, 툴팁은 완결 문장). 유닛에게 진영 단위로 거는 건 효과(`UnitEffect`). `OwnedBuilding` = (기물, 시작 등급에서 오른 횟수)만 담는 순수 데이터 — 지금 등급 = 시작 등급 + 오른 횟수(전설이 끝), `Level` = 오른 횟수 + 1 (나중에 비동기 상대 보드로 저장·전송하는 단위) | — |
| `UnitEffect` · `TeamEffects` | 유닛 효과(`Assets/Data/Effects/`). 적용 대상 = 병종 태그 또는 유닛 이름(둘 다 비우면 전부) + 훅(이동 속도·사거리·매 스텝·적 머리 착지·원거리 사격·피격·회복·소환의 추가/변환/이벤트)을 가상 메서드로. 건물이 필드에 있으면 전투 시작부터 그 진영에 켜지고(`TeamEffects.Enable`), 발동마다 스택 +1. 지금 남은 효과는 이중 회복(`DoubleHealEffect`)뿐 | `Unit`의 공개 도우미(`FireProjectile`·`ThrowHeal`·`FindHealTarget` …) |
| `Ground` | 굽은 땅(씬의 `Ground`, `Instance`). 사인 3개를 겹친 비대칭 언덕 높이 함수 `HeightAt(x)` 하나가 원본 — **씬을 불러올 때마다 위상·높이를 새로 뽑아** 충돌(PolygonCollider2D)·그림(LineRenderer)을 만든다. 양 끝(기지·소환 자리)은 평평, `_flatAnchors`(지금은 비어 있음) 밑은 평평한 단을 만들고 밑면을 그 위로 옮긴다 | — |
| `BoardView` · `SlotView` · `ShopPanel` · `OfferCard` · `RampLabel` | UI. `BoardView` = 칸 한 줄(아군 필드·적 필드·보관함, 틀 칸 복제) — 아군 필드는 전투 전엔 `RunState`, 전투 중엔 `BoardRunner`를 본다. `ShopPanel` = The Bazaar식 상점(진열 · 가운데 필드 · 보관함) — **끌어서** 칸 이동/맞바꿈 · 진열을 칸에 놓아 구매(`RunState.BuyInto`) · 판매 버튼에 놓아 판매. 누르기는 선택(설명)·진열 빈 칸 구매. 끌기 이벤트는 `SlotView`·`OfferCard`·`SellDropZone`이 받아 `ShopPanel`로 넘긴다. 탑 층 그림(국가별 벽 · 등급 색 창틀 · 창 불빛)은 `FloorLook` 하나 — UI 탑 칸·진열 카드·끌기 고스트가 같이 쓴다(전장 탑은 `Bases/Tower`가 따로 그린다). 보관함은 `StorageBox`(상자 안 조각 배치·낙하, 데이터는 그대로 `RunState.Storage` 6칸) — 상자 빈 곳 놓기는 `ShopPanel.DropOnBox`, 조각 위 놓기는 기존 맞바꿈·합치기. `RampLabel` = 전투 시간 + 데스매치 단계(30초 전 카운트다운 · 생산 종료까지 · 데스매치) | `RunState` · `BattleManager.StartBattle` |
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

한 턴 = 지도에서 시작해 지도로 돌아온다. 자세한 단계는 아래 「씬 둘과 턴 흐름」·「월드맵 런」.

```
World 씬 ─► BeginTurn (AI 영주 전원 상점 + 내 수입 = 18 + 땅 × 2)
   │  땅 고르기(Preview) · 「상점」은 지도 위 팝업 — 몇 번이든
   ▼
"침략" ─► March (내 영주가 그 땅까지 걸어감) ─► Battle 씬
   │  두 BoardRunner.Begin (효과 켜기 · 게이지 0부터) · TeamEffects 비움
   │  칸 게이지 참 → 생산 / 액션 / 효과 스택
   │  3:00 본진 화살 정지 → 3:10부터 남은 유닛끼리(데스매치, BattleConfig)
   ▼
어느 기지 파괴 ─► RunState.FinishBattle (이기면 그 영주의 땅 전부 + 행복도 +1,
   │                                      지면 상대 땅 수에 따라 −1/−2/−3)
   ▼
World 씬 ─► EndTurn (AI 침략 → 정산) → 사건 목록을 지도에서 재생 → 다음 턴
            (행복도 0 → 게임 오버 · 30지역 통일 → 클리어, 둘 다 지도에서)
```

---

## "X 추가하려면 어디 봐야 하나"

| 변경하고 싶은 것 | 손대야 할 파일 |
|---|---|
| **새 유닛 종류** | `Assets/Data/Units/Unit_*.asset` 하나(이름·**분류**·수치·공격 방식·밀치는 힘·화살·**진영별 프리팹 두 칸**) + `Ally_*`/`Enemy_*` 프리팹 두 개(`*_Melee` 복제 → `_definition`·`Visual/Weapon` 그림 교체) + 그 유닛을 만드는 건물. 코드 X |
| **새 생산 건물** | `Assets/Data/Buildings/Building_*.asset`(등급·쿨다운·유닛·유닛 수) + 상점에 내려면 `BattleConfig`의 `Shop Pool`, 적은 같은 `Shop Pool`에서 산다(빌드 방향의 핵심 기물로 넣으려면 `Assets/Data/EnemyBuilds/EnemyBuild_*.asset`). 코드 X |
| **기물의 신화 효과** | 기물 에셋의 `_mythicActions`/`_mythicEffects`/`_mythicPassives`에 능력 에셋을 넣는다(효과 모양대로 셋 중 하나). **코드 X** — `OwnedBuilding`이 등급을 보고 켠다. 글은 §7 줄들 아래에 붙는다. 문구 원본은 기획서 §7-1 |
| **기존 효과를 건물에** | 건물의 `Effects`에 `Effect_*.asset`을 넣는다(적용 분류는 효과 에셋이). 같은 효과를 다른 분류에 쓰려면 효과 에셋을 복제해 분류만 바꾼다. 코드 X — 단 효과마다 전제가 있다: DoubleHeal은 회복 유닛에서만 동작 |
| **새 유닛 효과** | `Units/Effects/`에 `UnitEffect` 상속 클래스 하나(있는 훅만 쓰면 `Unit` 수정 X, 수치는 필드로) + `Assets/Data/Effects/` 에셋. 새 훅이 필요하면 `UnitEffect`에 가상 메서드 + `Unit`의 해당 지점에서 `TeamEffects.For(_team)`을 돌며 부른다 |
| **새 기물 발동 효과("N초마다")** | `Buildings/`에 `BuildingAction` 상속 클래스 하나(`Execute(board, slot, level)`, 이웃은 slot±1) + 에셋 + 기물의 `Actions`에 추가. 보드 조작은 `BoardRunner`의 `FireNow`(즉시 발동) · `ScaleCooldown` · `Haste`(가속) · `Summon` |
| **새 기물 늘 켜진 능력** | `Buildings/`에 `BuildingPassive` 상속 클래스 하나(쓰는 훅만 — `OnBattleStart` · `OnNeighborSummoned`, 새 훅이 필요하면 `BuildingPassive`에 가상 메서드 + `BoardRunner`의 그 지점) + 에셋 + 기물의 `Passives`에 추가 |
| **스탯을 올리는 기물** | 진영 단위 = `StatBook.AddForBattle`/`AddForRun`(대상 고르기는 `IUnitFilter` — 예: `AddBattleStatAction`) · 유닛 하나 = `Unit.AddModifier`(예: `NeighborSummonStatPassive`) |
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
  큰 유닛(톱니거인)은 루트가 아니라 **박스 크기 + `Visual` 스케일**로 키운다 — 찌그러짐은 프리팹의 `Visual` 스케일에 곱해진다(`Unit._visualScale`).
- 🔴 **칸·사거리 검사는 트리거를 무시한다**(`Unit.SolidOnly`). 화살이 트리거라서, 무시하지 않으면 날아가는 화살 때문에 "머리 위가 막혔다"로 오판한다.
  새 물리 검사를 추가할 때도 같은 필터를 쓸 것.
- 🔴 **죽은 유닛은 즉시 충돌에서 빠져야 한다.** 튕겨나가는 동안 다른 유닛을 밀거나 받치면 안 된다(그래야 위의 산이 무너진다).
- 🔴 **전투 시간은 `BattleManager.Elapsed`**(전투 시작부터, 상점 시간 제외) — `Time.timeSinceLevelLoad`는 상점 시간까지 들어가므로 쓰지 말 것.
- 🔴 **`Time.timeScale`을 쓰는 곳이 넷이다** — 결과 화면(0) · 상점(1, `BattleManager.Awake`) · 전투 배속(`SpeedButton.Current` ×1/2/4 — 전투 시작·버튼 누를 때) · 히트스톱(현재값×0.02). 측정용 배속(8)은 손으로 걸 때만. 히트스톱은 끝날 때 **자기가 건 값일 때만** 되돌린다.
  새로 timeScale을 만지는 코드를 넣으면 이 셋과 부딪히지 않는지 본다.
- 🔴 **카메라를 옮길 땐 부모 `CameraRig`를 움직인다**(`CameraDrag`). `CameraShake`는 자식 카메라의 localPosition을 Awake 때 값으로 되돌리므로, 카메라 자체를 옮기면 흔들릴 때 원래 자리로 튄다.
- 🔴 **땅은 평평하지 않고 전투마다 바뀐다 — 땅 높이가 필요하면 `Ground.Instance.HeightAt(x)`를 쓴다.** y를 상수로 박지 말 것. 밑이 평평해야 하는 새 구조물은 `Ground._flatAnchors`에 넣는다(그 구조물의 y는 `Ground`가 판 시작 때 맞춘다 — 씬의 y는 의미 없음).
- **맵(전장 종류) = `MapDefinition` 에셋(`Assets/Data/Maps/`, `BattleConfig._maps` — 0번이 1라운드)**, 이번 판은 `RunState.Map`(상점 진입 때 뽑음). 효과가 걸리는 곳은 셋: 유닛 스탯 = `BattleManager.StartBattle`의 `StatBook.AddForBattle` · 기물 쿨다운 배율·전체 발동·늪지 = `BoardRunner` · 지형과 뒷배경 = `Ground`(`MapDefinition.Backdrop` 겹 — 회색 실루엣, 겹마다 `BackdropParallax` · 땅 속 검은 메시. 정렬: 뒷배경 -30~ < 땅 속 -10 < 탑 -1 < 유닛 0). 양 진영 같게.
- **회복 훅**: `UnitEffect.OnHealed` = 사제가 구슬을 던진 직후(성수 우물) · `OnHealLanded` = 구슬이 아군에 닿아 실제로 체력이 찼을 때(수도원 — 사용자 10/10 "닿으면 걸리게") · `OnHealReceived` = 받는 쪽, 회복량 > 0(순례지). `Unit.Heal`은 실제로 찬 양을 돌려준다.
- **전장 전투력**(`Battle/CombatPower`, static, 0.25초마다 다시 셈 · 상점에선 0) — 유닛 값 = √(지금 체력 × 초당 피해) × 표시 배율. 초당 피해 = `AttackDamage × DamageScale ÷ AttackInterval`(화염·휩쓸기는 × 범위 배율), 회복 유닛은 회복량 ÷ 간격 × 회복 계수. 계수는 `BattleConfig` 「전투력」. `For(team)` 합계 · `Count(team)` 수 · `Of(unit)` 하나 · `SpawnPerSecond(team)` = 최근 `BattleConfig._spawnRateWindowSeconds`(10)초에 전장에 나온 유닛 수의 평균(`Unit.OnEnable`에서 셈). **화면 표시(`UI/CombatPowerView`)는 유닛 수 + 초당 생산 수만**(사용자 10/10 "전투력이 비직관적") — 전투력 합계는 밸런스 계산용.
- **버프 선**(사용자 10/10 — 누가 누구에게 무엇을 주는지 보이게): 기물·유닛 효과가 다른 대상에게 효과를 거는 자리에서 `Buildings/BuffLinks`를 부른다(`ToSlot`·`ToPoint`·`ToTowers`·`ToUnits`·`Standing` — **새 효과를 만들면 건 자리에서 부를 것**). 그리는 쪽은 `BuffLinks.Raised`를 구독(타워 대상 선은 그 타워의 화살 발사점 `Base.Muzzle`으로, 타워마다 하나씩 — 사용자 10/10): 모양·시간 = `Fx/BuffLinkShape`(전장·UI 공용 — 무작위 굴곡, 머리 굵고 꼬리 가늘게, 0.45초) · 전장 = `Fx/BuffLinkFx`(FxDirector가 만듦, 메시 하나에 1/8칸 격자 네모, 색 `ColorOf(BuffKind)`), UI 탑 = `UI/UiBuffLinkLines`(BoardView가 세로 탑 줄마다 실행 중 자식 하나 생성, 대상이 기물인 것만, 탑이 숨으면 안 그림). 게임 타이밍은 안 바꾼다(곡선은 즉시, 실제 시간 기준). `BoardRunner.FireNow`는 발동했는지(bool), `HasteRandom`은 고른 칸(int)을 돌려준다.
- **오버드라이브 번개 표시**(사용자 10/10): 데스매치 동안, 효과가 켜진 진영에서 `TowerDeathmatchPassive`를 가진 기물의 층에 가속 배지와 같은 자리로 노란 번개 아이콘(숫자 없음, 전투 끝까지) — 판정·색은 `Fx/OverdriveMark` 하나, 그리는 쪽은 전장 `Bases/Tower`(`TowerBadge.ShowIcon`)와 UI 탑 `UI/SlotView`(가속 표시를 실행 중 복제 — 씬에 오브젝트 없음, 아이콘은 `_overdriveIcon` 필드).
- **죽은 유닛 = 날아가는 시체**(사용자 10/10): `Unit.Die`가 그림을 숨기고 `FxDirector.Corpse` → `Fx/CorpseFx`가 그 모습을 본뜬 시체를 자기 성 쪽 비스듬히 위로 날려 보낸다(대난투식 — 방향 변주는 작게, 뒤로 구르는 한 방향 회전. Transform + SpriteRenderer뿐 — 물리·충돌·Unit 없음, 풀 고정 개수, 관리자 하나가 일괄 갱신, 위치는 던진 뒤 흐른 게임 시간의 식 → 배속·히트스톱을 따라감, 게임 난수 안 씀). 유닛 수·표적·판정에 안 잡힌다. 수치는 `BattleConfig` 「시체」.
- **효과 받은 기물 표시**(사용자 10/10): 기물이 받는 지속 효과는 가속과 쿨다운 배율 둘 — `BoardRunner.Slot.HasteLeft`(남은 가속 초, 쿨다운 없는 기물·기물 정지 뒤엔 0) · `CooldownScale`(지금 쿨다운 ÷ 원래, 1이면 없음). 전장 탑 층 = `Bases/TowerBadge`(날개 + 「0.7」 · 시계 + 「×0.8」), UI 탑 칸 = `SlotView`의 HasteMark·CooldownMark(칸 옆, 전투 중에만). 아이콘 `UI_IconHaste`·`UI_IconCooldown`.
- **유닛 카드의 영구 강화분**(「공격력 11+1」, 사용자 10/10): UI는 `RunStats.Preview(unit, stat)`(`.Base`·`.Now`·`.Bonus`)만 읽는다 — 전투의 유닛 스탯 계산과 같은 경로(`StatBook.AccumulateRun`). 들어가는 것: 런에 쌓이는 강화(`StatBook.AddForRun`) + 필드 기물이 전투 시작 때 거는 스탯(`BuildingPassive.BattleStartStat` — 스패너) + 별칭(`AliasOf` — 조립 라인의 톱니거인). 🔴 **런 동안 쌓아 전투 시작 때 유닛 스탯을 거는 새 기물은 `OnBattleStart`에서 직접 걸지 말고 `BattleStartStat`으로** — 안 그러면 카드에 안 보인다. 전투 중 버프·맵 효과는 안 넣는다.
- **상점 잠금**(`RunState.ShopLocked`): 잠긴 채 상점에 들어오면 진열 유지(산 칸만 새로 뽑음) · 리롤·새 런이면 풀림.
- 🔴 **맵 길이를 바꾸면 씬 값을 같이 옮긴다**(지금 ±61) — `Ground._minX/_maxX`(·평평 구간 `_flatFrom`) · 두 기지 · `AllySpawnPoint`·`EnemySpawnPoint`·`BossSpawnPoint` · `CameraDrag._minX/_maxX`.
- 🔴 **유닛 키는 가변이다 — `BoxCollider2D` 높이에서 읽는다**(`Unit._halfHeight`). 머리 위 칸·발밑 칸·소환 공간·찌그러짐 발 고정·**점프 높이(= 키 + 1, 유닛 정의에 값 없음)**가 전부 이 값 기준.
  키 큰 유닛(기사)의 그림은 `Visual` 아래 칸별 자식(`Horse`·`Rider`)으로 두고 `Visual` 자체엔 SpriteRenderer를 두지 않는다. 층 교환은 키가 같을 때만.
- 🔴 **효과는 진영 단위로 켜진다**(`TeamEffects.For(team)`) — 유닛 정의(`Unit_*.asset`)는 양 진영이 같이 쓰므로 효과를 정의에 달지 말 것.
- 🔴 **효과·스탯이 유닛에게 적용되는지는 `AppliesTo`가 아니라 `AppliesFor(진영, 유닛)`으로 본다** — "A는 B에게 적용되는 효과를 함께 받는다"(조립 라인: 톱니거인 → 톱니병사)를 `UnitAliases`가 전투마다 건다. 효과의 유닛별 타이머는 `Unit.EffectReady`/`SetEffectCooldown`(생애마다 비움). 전장에서 유닛을 없애되 죽음이 아닌 것(소모)은 `Unit.Consume`.
- 🔴 **유닛 죽음은 `Unit.Perish(처치자)`로만** — 처치 효과(처치자 진영) → 사망 효과(`OnDied`, 내 진영) 순. 무적(`GrantInvulnerable`)이면 `TakeDamage`가 통째로 무시되고, 충정의 맹세(`FirstDeathDelay`)는 첫 죽음을 무적 N초 뒤로 미룬다. 생애에 한 번 쓰는 효과는 `Unit.UseOnce`.
- 🔴 **유닛 공격 피해는 `Unit.AttackDamage × Unit.DamageScale`이다**(스탯 계산 × 전투 공격력 가속). 새 공격을 만들 때 빠뜨리면 기물 강화·가속이 안 먹어 판이 안 끝날 수 있다. 본진 화살은 가속 대상이 아니다.
- **밟기 피해**는 `Unit` 안에서 — 적 머리 위(`enemyBelow`)에 서 있는 동안 0.5초마다 `AttackDamage × DamageScale × 비율`(`BattleConfig`). 처치로 치고(처치 효과 발동) 공격 효과는 안 탄다. **크기 상한**은 `BattleConfig`의 `MaxUnitSize`(`Unit.Grow`가 자른다).
- 🔴 **물리 고정 스텝은 1/30초**(`ProjectSettings/TimeManager`). 공격 쿨다운은 `Unit.NextCooldown`으로 그 스텝에 넘친 시간을 이월한다 — 새 쿨다운을 만들 때도 이걸 쓸 것(안 쓰면 간격이 스텝 길이의 배수로 늘어난다).
- 🔴 **등반 검사(`ClimbTarget`, 물리 검사)는 비싸다** — 접촉 루프에서는 후보만 `_climbCandidates`에 모으고, 실제로 뛰는 분기에서만 `CanClimb()`로 검사한다. 접촉 루프에 물리 검사를 다시 넣지 말 것.
- **등반 규칙** — 맨 앞줄(`blockedByEnemy`)·받침(`allyAbove`)은 등반 점프를 안 한다. 그 밖은 등반 뒤 `_climbRest`초 쉰다. 손잡이 `_climbRest`·`_climbSlowerThan`·`_climbJumpExtra`는 `BattleConfig`.
- **넘쳐흐르기 문** — 위층 근접이 발밑 적을 밟고 넘는 건 내 진영 전선 산이 적보다 `PileLeadToCross`칸 넘게 높을 때만. 산 높이는 유닛이 물리 스텝마다 보고하는 정적 값(물리 검사 없음, 한 스텝 늦음). 위층은 아래 계단 규칙이 먼저 막는다.
- **계단 피라미드**(`Unit.StairAdvance`, 사용자 10/9) — 땅을 안 딛은 유닛은 ① 땅 선두에서 층 × 반 칸(`StairStep`) 뒤 ② 발밑 가장 앞 아군에서 반 칸 뒤 중 가까운 쪽까지만 간다 — **앞으로 가는 속도만 줄인다(유닛은 스스로 뒤로 걷지 않는다 — 사용자 10/9 "갑자기 뒤로 가는 거 어색")**, 한계까지 남은 거리 ÷ `StairBrakeTime`으로 서서히 멈춤. 반 칸 자리는 등반 점프가 뛰기 전에 착지점을 정해 맞춘다(`StartClimb`·`ClimbVx`, 공중 가로 속도는 앞으로만). 앞 아군을 밀어붙이지 않는다(`NoPress` — 맞닿은 앞 아군의 실제 속도 + `PressSpeed`까지만: 짓눌려 뒤로 튀는 걸 막음) · 머리 위가 막혔으면 등반 점프 안 함(`HeadClear`). **넉백(경직)이 아닌 살아 있는 유닛은 물리 스텝 직후 뒤로 밀린 만큼 x를 되돌리고 뒤쪽 속도를 0으로 자른다(`Unit.HoldBack` — 사용자 10/10 "뒤로 점프는 아예 하지마", 건축가 귀환 제외).** **앞에 맞닿은 아군이 없는 유닛(앞면)은 이 상한을 받지 않고 적과 맞붙을 때까지 걷는다(사용자 10/10 "제일 앞에 있는 애들은 전진해도 되는거 아니야?").** 층 = 받침 관계(`_stackLayer`, 땅 0 — 지형 기울기에 흔들리지 않게 높이 대신). 데스매치 뒤·내 산이 `StairHoldLead`(2칸) 넘게 앞설 때는 푼다 — 안 풀면 판이 교착한다. 측정 도구는 지움(위 「폴더 책임」 Dev 줄).
- **성 최대 체력** = 성 에셋 체력 + `BattleConfig._baseHpPerRound` × 라운드(0부터) → 그 위에 기물 보너스(고정 → % → ×). 현재 체력은 `Base.Hp`.
- **씬 둘과 턴 흐름(10/11)** — `World` 씬(매니저 `World/WorldManager`, 지도 `UI/WorldMapView`, 왼쪽 패널 `UI/WorldSidePanel`)과 `Battle` 씬(`BattleManager`). 씬이 뜰 때 매니저가 `RunState.Phase`(Map · Shop · Battle · Aftermath)를 보고 할 일을 정한다. 턴: World에서 `BeginTurn`(AI 영주 전원 상점 + 내 수입, 턴당 한 번) → 땅 고름(`Preview`) → 「상점」(Battle 씬, `OpenShop` — 「지도로」로 돌아옴, 몇 번이든) / 「침략」(`WorldManager` 단계 `March` — 내 영주 그림이 고른 땅까지 걸어간 뒤에야 침략이 확정되고 Battle 씬으로 넘어간다, 걷는 동안 버튼·땅 고르기를 막는다) / 「쉬기」 → 전투 결과(`FinishBattle`) → World로 돌아와 `EndTurn`이 준 사건 목록(`World/WorldEvent`)을 지도에서 차례로 재생(판도는 이미 끝 상태 — 건너뛰어도 결과가 같다) → 다음 턴. 통일·게임 오버는 지도에서. **지도 = 영토 그림**: 땅 번호 텍스처(`WorldMap_Main_Regions.png` 375×252, 4배 확대)를 판도가 바뀔 때마다 다시 칠한다(어두운 영주 색 채움 · 침략 가능 = 빗금 · 고른 땅 = 노란 테두리), 누르기 = 화면 좌표 → 땅 번호 조회. 이웃 = 그림에서 국경을 6칸 이상 맞댄 땅(64쌍) — `Editor/WorldMapBaker`(UnityEngine 없음, 러너와 같이 쓴다)가 그림·이웃 표·표시 자리를 한꺼번에 굽는다.
- **월드맵 런(10/11 — 아래 「적 빌드」 줄의 "런당 적 하나"를 대체. 씬 전환 순서는 위 줄이 최신)** — 새 런 = 새 월드(`RunState.StartNew` → `World/WorldRun`: 판도 `WorldState` + AI 영주마다 탑 `Run/TowerState`). 탑마다 「영구히」 강화를 따로 들고(`RunBook`), 진영별 목록은 "지금 쓰는 탑의 것"을 갈아 끼운다(`RunBook.Use`). 상점 한 번 = `RunState.ShopAs(tower, build, gold, round)` — 플레이어·AI 영주가 같은 규칙. 턴: 상점(`EnterShop` — AI 영주 전원 상점 + 내 수입 18 + 땅 × 2) → `Preview(region)`(보기만) → `Invade(region)`(상대·지형·영주 색 확정, 씬 재로드 → `Phase.Battle`로 바로 전투) 또는 `Rest()` → `FinishBattle(won)`(땅 흡수·행복도) → `EndTurn()`(AI 침략 `WorldTurn.AiPhase` → 정산 `Settle`). `EnemyField`·`LordColor(적)`·`Map` = 고른 상대의 탑·색·침략한 땅의 지형(고르기 전엔 빈 탑 `NoOpponent`·기본 지형). `Round` = 턴 − 1(등급 확률·성 체력이 읽는다). AI끼리의 승패 = `World/TowerPower`(생산 항 + 쓴 골드 보정 — 실제 전투와 대조 안 함). 월드 수치는 `WorldConfig`(`BattleConfig`가 가리킨다). 규칙을 고치면 Unity 밖 러너·씬 없는 자동 플레이로 불변식을 다시 잰다.
- **적 빌드 = 적도 상점을 돈다**(사용자 10/10 "적도 사람답게 빌드를 짰으면") — `RunState`의 골드·탑·보관함·진열·잠금·층 올린 횟수는 진영별 묶음(`Side`)이고, 기존 static 프로퍼티는 "지금 차례인 쪽"을 가리킨다(`ActingTeam`). 상점에 들어갈 때 적이 플레이어와 같은 메서드(Buy·Sell·Reroll·RaiseFloor·잠금·끼워 넣기)로 그 라운드 쇼핑을 한 번 한다(`Run/EnemyShopper`, 라운드당 한 번 — 상점을 다시 불러도 재쇼핑 없음). 런 시작 때 빌드 방향 하나를 고른다(`EnemyBuildProfile` SO — 핵심 기물과 값·생산 기물 비율·리롤 성향, `BattleConfig._enemyBuilds`; 근거는 기획서 「아키타입별 흐름」). 「구매할 때마다,」·영구 강화(`StatBook`·`TowerBook`의 "영구히")도 진영별이라 적에게 똑같이 걸린다. 자리 효과는 `BuildingAction`/`BuildingPassive.Reaches`로 읽어 닿는 대상이 늘어나는 쪽으로 배치. 난이도 손잡이 = `BattleConfig._enemyGoldScale`. 옛 고정 라운드 표는 지움 — 투석기·공룡·코끼리는 에셋만 남고 게임에 안 나온다. 전투 시작 때 `TowerBook.ClearBattle`(지난 전투의 "전투 동안" 타워 효과가 상점까지 남지 않게).
- **작은 타워 기물**(사용자 10/10): 작은 타워를 세우는 길은 `Base.Build`/`BuildOnGround` 하나(세울 때 본진 깃발 색 `Tower.FlagColor`로 깃발을 칠한다). 건축가는 내 성문 앞에서 적 성 앞면 직전까지 가까운 순으로 빈 평지를 찾고(`Unit.FindBuildSite`), 걷다 땅 꼭짓점에 걸려 못 나아가면 살짝 뛴다(건축가 · 조립식 감시탑 `SmallTowerAtStartPassive`). 걷기 = `TowerBook.FollowRule` + `Base.Follow`(`SmallTowerFollowPassive` · 움직이는 성 — 내 작은 타워가 내 유닛과 무관하게 적 쪽으로만 걷는다. 적 유닛·적 타워·적 성에 닿거나 앞선 내 타워와 간격이 좁으면 선다. Rigidbody 없이 Transform을 옮긴다). 걷는 진영의 작은 타워는 기계 다리 위에 올라선다(`Base.SetLegs` — 루트를 다리 그림 높이만큼 올리고 충돌 박스를 아래로 그만큼 늘려 아래 끝은 땅에 둔다, 본체·깃발·`Muzzle`이 같이 오름), 소환 자리 = `Base.FrontSmallTower` + `BoardRunner.SummonAtSmallTower`(`SummonAtSmallTowerPassive` — 자리를 정하지 않은 소환만). 수치는 각 패시브 에셋.
- **필드 = 탑**(사용자 10/8) — `RunState.Field` 칸 i = (i+1)층(0 = 성 바로 위). **중간 층은 비우지 않는다**: 빼면 위층이 내려오고, 놓으면 끼워 넣는다. 층 사이에 놓기 = `CanBuyInsert`/`BuyInsert`·`CanMoveInsert`/`MoveInsert`(`at` = 넣은 뒤 칸 번호, 탑 안 이동은 빼기 전 기준 · 빈 층 없으면 거부, 탑 안 이동은 허용 · 합치지 않음), 층 가운데 놓기 = 맞바꿈·합치기(사용자 10/8). 층 수는 `RunState.Floors`(층 올리기 `RaiseFloor`, 비용·시작·최대는 `BattleConfig`), 적 층 수 = 라운드 표 `_field` 길이. 전장 그림은 `Bases/Tower`·`TowerFloor`(양 진영 같은 코드) — 층 바닥 판은 그 진영 유닛만 위에서 딛고 적·화살은 통과. 기물이 소환한 유닛은 **자기 층에서** 나와 앞으로 걸어 나가 떨어진다(기물이 아닌 소환은 성문). 위치 효과는 소환 훅 `OnSummonedFrom(board, slot, sourceSlot, unit)`을 모든 층이 받고 각 능력이 방향(아래 전부·위 전부·바로 위·바로 위·아래)을 가린다.
- **유닛 위 표시**(무적 노란 원 · 공격력이 오른 순간 빨간 원판)는 `Unit.MakeHalo` — 처음 필요할 때 렌더러 하나를 만들어 재사용, 그림은 전 유닛 공유.
- **지도 위 영주 그림은 `UI/WorldLords`가 전부 그린다**(어슬렁 · 침략 걸음 · 이름표 · 죽어 날아감 · 구름 트레일). 지도는 UI라 1칸 = 24px로 환산하는 자리가 한 곳뿐이고, 전투 연출 값(`BattleConfig`의 시체 값)을 그대로 가져다 쓴다. **재생 흐름의 길이를 늘리지 않는다** — 연출은 `EndTurn`이 준 사건 재생 안에서만 돈다.
- **유닛이 뒤를 보는 것은 그림만 뒤집는다**(`Unit.LookToward` — 뒤쪽 대상을 치거나 회복할 때, 건축가가 성으로 돌아갈 때). 판정·투사체 출발 자리는 **앞을 볼 때 그대로**다 — 화살·회복 구슬은 그림과 어긋나 나간다.
- **늪지에서 기물이 꺼졌는지는 `BoardRunner.StoppedByMap` 하나로 판정한다**(전장 탑 · UI 탑 · `Stats`의 전투 효과 집계가 모두 이것을 부른다). 꺼짐 조건을 바꿀 때 이 메서드만 고치면 세 곳이 따라온다.
- **칸 UI가 읽는 것** — 전투 중 칸 숫자 = `BoardRunner.Slot.BattleGainText`(새 칸 누적 기물은 `BattleGain()`을 덮어쓰고 `BoardRunner.AddBattleGain`을 부를 것). 칸 튐 = 쿨다운 발동 `BoardRunner.Triggered` + 상점·전투 시작 효과 `OwnedBuilding.EffectFired`(정적 — 구독하면 OnDestroy에서 해제).
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
  주변 검색(적·회복 대상·기지 위 유닛)은 `TargetScanInterval`(0.1초)마다만 새로 하고 그 사이엔 캐시를 검증만 한다. 적 찾기는 진영 레이어(`AllyUnit`/`EnemyUnit`, `Unit.Awake`가 지정)로 거른 `_enemyFilter`, 아군 찾기는 `_allyFilter`. 충돌 규칙은 대부분 켜져 있다 — 예외(`Base.SetupLayers`가 실행 시 건다): 같은 진영 유닛·건축가 × 같은 진영 작은 타워, 유닛 × 같은 진영 건축가는 충돌하지 않는다(레이어 10~13 `AllyBuilder`·`EnemyBuilder`·`AllyTower`·`EnemyTower`).
- **작은 타워 = `Base` 코드 그대로**(`Base.Build`, 배율은 `BaseDefinition` `_smallAttackScale`·`_smallHpScale`) — '내 타워' 효과·데스매치 화살 정지·처치 알림이 본진과 같은 경로. `Base.For`는 본진만 돌려준다(체력 바·패배 판정). 건축가 = `AttackType.Build`, 흐름은 `Unit.BuildStep`(빈 평지로 → 짓기 → 성 앞에서 사라짐, 죽음 아님).
  성능을 다시 볼 땐 Unity 프로파일러에서 `Unit.Contacts/Scan/Support/Walk` 구간을 본다. 물리 몰아 돌리기 상한은 `Maximum Allowed Timestep` 0.1(프로젝트 설정).
- 🔴 **static 상태는 씬 재로드(= 다음 라운드)에도 남는다** — `RunState`는 그걸 일부러 쓰고, `TeamEffects`는 전투 시작마다 비운다. 새 static을 만들면 라운드마다 비울지 먼저 정할 것.
- 🔴 **층 정렬은 자리 맞바꾸기가 아니라 "애초에 뒤집혀 오르지 않기"로 한다**(사용자 결정 9/29). `Unit.BelongsBelow`(`StackRank`) 하나로 세 곳이 판정한다:
  ① 등반 — 나보다 위층 역할 아군 등에는 뛰어오르지 않고 뒤에서 기다린다 ② 소환(`Unit.Spawn`) — 위층 칸은 바로 아래가 나보다 아래·같은 역할일 때만 ③ 이미 뒤집혀 탔으면(넉백·낙하) 위 유닛이 싸움보다 먼저 앞으로 걸어 내려가고 밑 유닛은 멈춘다.
  사방이 막힌 산 안쪽은 정렬되지 않는다(실전 약 15% 뒤집힘 — 옛 측정값).
- 🔴 **새 `SerializeField`를 추가할 때 클래스 기본값을 믿지 말 것.** 이미 임포트된 에셋·프리팹은 그 필드가 YAML에 없어도
  임포트 캐시의 옛 값을 쓴다. 그래서:
  1. 필드를 추가하면 **그 즉시 모든 기존 에셋에 값을 명시적으로 써 넣는다.**
  2. **불리언 기본값은 언제나 "기존 동작과 같은 쪽"으로.**
  3. 쓴 뒤 되읽어 확인한다.
- 🔴 **직렬화되는 enum은 맨 뒤에만 추가할 것** — 중간에 끼우면 기존 에셋의 정수 직렬화가 밀린다.
- 🔴 **기물의 켜진 효과는 `owned.Definition.Actions/Effects/Passives`가 아니라 `owned.Actions/Effects/Passives`로 읽는다**(신화 10/12) — 신화 등급이면 정의의 `_mythicActions`/`_mythicEffects`/`_mythicPassives` 묶음이 §7 묶음 **뒤에 더해진다**(대체가 아니다). 판정은 `Rarity == Mythic`이지 **`Level`이 아니다** — `Level = 오른 횟수 + 1`이라 시작 등급마다 신화에 닿는 Level이 다르다(일반 6 · 전설 2).
  신화 효과를 더하는 것은 **에셋 작업이다** — 기물 에셋의 `_mythic*`에 능력 에셋을 넣으면 코드는 더 고칠 것이 없다. 신화 능력이 등급으로 값을 키우지 않는 것은 **코드가 보장한다** — `Level`이 전설에서 멈추므로(아래 「신화에서는 수치가 오르지 않는다」) `perTier`가 붙은 능력도 전설 값으로 고정된다.
  아직 정의(시작 등급)만 보는 곳 셋: `BuildingTooltip.Headline`(칸 아래 대표 숫자) · `BuildingPassive.HasBuyEffects` (신화에서만 생기는 「구매할 때마다,」가 칸을 튀게 하지 못한다) · `EnemyShopper`의 자리 판단 `Reaches`(AI는 신화로 늘어난 범위를 모른다).
- 🔴 **등급 배열의 길이가 일부러 어긋나 있다**(신화 10/12) — `BuildingRarity`는 여섯이지만 `BattleConfig`의 **가격 `_rarityPrices`는 여섯 칸, 진열 확률 `_rarityWeightsFirst`/`_rarityWeightsLast`는 다섯 칸**이다.
  **신화 등급의 기물 *정의*는 만들지 않는다** — `OwnedBuilding.Rarity = 정의 등급 + 오른 횟수`라 신화는 합치기로만 도달하고, 신화 효과도 정의의 등급을 올리지 않고 `_mythic*` 묶음으로 붙는다. 그래서 **진열 확률은 영구히 다섯 칸이면 된다.** 정의를 신화로 만드는 순간 `RarityWeight`가 `(int)rarity`로 다섯 칸 배열을 읽어 `IndexOutOfRangeException`이다 — 그래서 밸런싱 창의 등급 드롭다운에서도 신화를 뺐다(`Editor/BalanceWindow.Rarities`).
  가격을 읽는 모든 경로는 **정의**의 등급을 넘기므로(`PriceOf`), 여섯째 칸 80골드는 읽히지 않는다. 신화 한 채의 판매가도 정의(시작 등급)가 × 합친 개수로 나온다.
- 🔴 **신화에서는 수치가 오르지 않는다**(기획서 §1) — 상한은 **`OwnedBuilding.Level`(= `LevelOf`) 한 곳**에 있다. `Level`을 쓰는 곳은 모두 수치를 키우는 용도라(쿨다운 · 효과 세기 · 소환 수 · `TeamEffects` 등급 · `TowerPower`) 여기서 막으면 전부 따라온다. 상한은 고정값이 아니라 **「`Rarity`가 전설인 선의 Level」**이다 — 시작 등급마다 다르다(일반 5 · 전설 1).
  `Copies`(합친 개수)는 `_upgrades`를 직접 읽어 신화에서도 2배로 늘어난다 — **판매가는 계속 올라야 하므로 맞다.**
  🔴 **상한 규칙의 사본을 만들지 말 것** — 툴팁의 RANK UP 미리보기도 같은 `LevelOf`를 부른다. `level + 1`로 다음 등급을 계산하면 상한을 넘어가 「신화가 되면 더 좋아진다」고 거짓말을 한다.

---

## 의도적으로 안 적은 것 (grep으로 찾을 것)

- 클래스의 public 메서드 목록 / 시그니처 → 파일 열어보면 됨
- 매니저별 SerializeField 필드 → Inspector 또는 코드 상단 보면 됨
- 밸런스 수치 → ScriptableObject 에셋
- UI 텍스트
