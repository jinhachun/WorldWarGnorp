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
Main Camera      ← Camera(Orthographic, size 20, y=14) · UniversalAdditionalCameraData   [tag=MainCamera]
Global Light 2D  ← Light2D(Global)
Ground           ← SpriteRenderer · BoxCollider2D   (윗면 y=-4, x -36~36)
Base_Ally        ← SpriteRenderer · BoxCollider2D · Base(Team=Ally)    x=-34.5
 └─ BackStop     ← BoxCollider2D   (보이지 않음) 기지 뒤쪽 끝 ~ 화면 위
Base_Enemy       ← SpriteRenderer · BoxCollider2D · Base(Team=Enemy)   x=+34.5
 └─ BackStop     ← BoxCollider2D
Spawner_Ally     ← StackTestSpawner   [검증용 임시] `Unit_Ally` 프리팹
Spawner_Enemy    ← StackTestSpawner   [검증용 임시] `Unit_Enemy` 프리팹
```

- **임시 오브젝트**(`Spawner_*`)는 소환 버튼·적 AI가 생기면 지운다.

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
