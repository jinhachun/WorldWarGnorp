# SCENE_MAP.md — 씬 구조 지도

> **AI(MCP)용 씬 지도.** ARCHITECTURE.md가 코드 지도라면, 이건 씬에 실제로 배치된 오브젝트·컴포넌트·핵심 배선 지도다.
> 매 세션이 콜드 스타트 — "어디에 뭐가 붙어 있나"를 미리 알려 MCP 왕복을 줄인다.
>
> **적는 것**: 매니저/싱글톤 오브젝트 위치, load-bearing 스크립트, Canvas/UI 루트, "새 오브젝트 어디 붙이나".
> **안 적는 것**(MCP 쿼리로 즉시 확인): 좌표·스케일·색상, 개별 위젯, SerializeField 수치, 프리팹 내부, 밸런스값(→ 에셋).
>
> **유지 원칙**: 오브젝트 추가/이동/스크립트 재배치 때만 갱신. 씬과 어긋나면 씬이 정답 — 이 문서를 의심할 것.

---

## 씬 목록

| 씬 | buildIndex | 역할 |
|---|---|---|
| `Assets/Scenes/Battle.unity` | 0 | **전투 한 판 전체** (유일한 게임 씬) |

---

## Battle.unity

```
Main Camera      ← Camera(Orthographic, size 20, y=14) · UniversalAdditionalCameraData · CameraShake   [tag=MainCamera]
Fx               ← FxDirector   (파편·먼지 프리팹 + CameraShake 참조)
Global Light 2D  ← Light2D(Global)
Ground           ← SpriteRenderer · BoxCollider2D   (윗면 y=-4, x -36~36)
Base_Ally        ← SpriteRenderer · BoxCollider2D · Base(Team=Ally)    x=-34.5
 └─ BackStop     ← BoxCollider2D   (보이지 않음) 기지 뒤쪽 끝 ~ 화면 위
Base_Enemy       ← SpriteRenderer · BoxCollider2D · Base(Team=Enemy)   x=+34.5
 └─ BackStop     ← BoxCollider2D
Battle           ← PlayerWallet · BattleManager · UpgradeState   (전투 로직 허브)
AllySpawnPoint   ← (Transform만) 소환 버튼이 아군을 내는 자리
EnemySpawner     ← EnemySpawner                      (적 AI: `BattleConfig` 간격마다 `Enemy_Tank/Melee/Ranged/Knight/Catapult` 중 무작위)
EventSystem      ← EventSystem · InputSystemUIInputModule   (새 Input System 전용 — StandaloneInputModule 쓰지 말 것)
Canvas           ← Canvas(Overlay) · CanvasScaler(1920×1080) · GraphicRaycaster
 ├─ ResourceLabel        ← Text · ResourceLabel        (위쪽 줄 맨 왼쪽 "현재 / 최대")
 ├─ SummonButton_Shield · _Sword · _Bow · _Knight · _Priest   ← Image · Button · SummonButton   (위쪽 줄, 자원 표시 오른쪽부터 180px 간격) → Name · Cost · CooldownFill(Filled)
 ├─ UpgradeButton_Shield · _Sword · _Bow · _Knight · _Priest   ← Image · Button · UpgradeButton   (각 소환 버튼 바로 아래, 보라색) → Label
 ├─ IncomeUpgradeButton  ← Image · Button · IncomeUpgradeButton   (소환 줄, Priest 오른쪽, 노란색) → Name(레벨) · Cost
 ├─ BaseBar_Ally · BaseBar_Enemy   ← Image · BaseHealthBar   (맨 윗줄 좌·우, 각 폭 900) → Fill(Filled, 아군은 왼쪽·적은 오른쪽 기준)
 └─ ResultPanel          [실행 시 꺼짐] ← Image          → ResultText · RetryButton
```

- UI는 **임시 모양**(단색 사각형 + 레거시 Text)이다. 아트가 오면 CLAUDE.md §5-1 규칙대로 바꾼다.
- 🔴 **전투 UI는 전부 화면 위쪽 줄에 둔다**(사용자 결정 9/27 — 전장이 화면 아래쪽이라 겹치지 않게). 새 버튼도 위쪽 줄 오른쪽으로 이어 붙인다.
  현재 **세 줄**(기준 1920×1080, 위에서부터 y): ① 기지 체력바 -20~-50 (아군 x 40~940 · 적 980~1880)
  ② 자원 40~440 · 소환 480~1360 · 획득량 강화 1400~1560 (y -70~-230) ③ UP 버튼 = 각 소환 버튼과 같은 x (y -240~-300).
  소환 줄 오른쪽 1560~1880이 비어 있다. **새 요소를 넣기 전에 이 구간과 겹치는지 계산할 것.**

**핵심 배선:**
- **`BackStop`은 기지 스케일(1.5×3)의 자식이라 localScale·localPosition이 그 역수로 보정돼 있다.** 기지 크기를 바꾸면 BackStop도 다시 맞출 것.
  에디트 모드에서 옮긴 뒤 좌표를 읽을 땐 `Physics2D.SyncTransforms()` 먼저 — 안 하면 옛 bounds가 나온다.
- 렌더러는 `Assets/Settings/Renderer2D.asset`(2D Renderer). PC·Mobile 두 `*_RPAsset` 모두 이것 하나만 쓴다.

---

## "X를 씬에 추가하려면 어디"

| 하려는 것 | 씬 작업 |
|---|---|
| 새 유닛 종류 | `Assets/Prefabs/`에 프리팹(`Unit` + `Rigidbody2D` + `BoxCollider2D` + 마찰 0 재질) + `Assets/Data/`에 `UnitDefinition` 에셋. 씬 구조 변경 없음 |
| 기지 체력 | `Assets/Data/Base_Test.asset` (씬 X) |
| 자원·적 소환 간격 | `Assets/Data/BattleConfig.asset` (씬 X) |
| 새 소환 버튼 | 기존 버튼을 복제해 `_unitPrefab`과 `Name` 글자를 바꾼다. 🔴 **복제본의 `_cooldownFill`·`_costText`는 원본 자식을 가리키므로 자기 자식으로 다시 연결.** 비용·쿨다운은 그 유닛의 `UnitDefinition` |
| 판 시작 시 도는 신규 로직 | `Battle` 오브젝트에 컴포넌트 추가 |
