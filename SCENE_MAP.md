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
| `Assets/Scenes/Battle.unity` | 0 | **한 라운드 전체 — 상점 + 전투** (유일한 게임 씬. 라운드마다 다시 불러온다) |

---

## Battle.unity

```
CameraRig        ← CameraDrag   (좌우 드래그·관성, 맵 끝 x -72~72에서 멈춤, 판은 아군 기지 쪽 화면에서 시작)
 └─ Main Camera  ← Camera(Orthographic, size 20, 로컬 0 · 월드 y=14, 배경 검정) · UniversalAdditionalCameraData · CameraShake   [tag=MainCamera]
Fx               ← FxDirector   (파편·먼지 프리팹 + CameraShake 참조)
Global Light 2D  ← Light2D(Global)
Ground           ← PolygonCollider2D · LineRenderer · Ground   (굽은 땅 x -72~72, 원점·스케일 1 고정. 모양은 판 시작 때 무작위로 만든다 — 에디터 메뉴 Build는 기본 물결만 보여 준다. `_flatAnchors` 비어 있음)
Base_Ally        ← SpriteRenderer(`castle.png`, order -1 — 유닛이 성 앞에 그려짐) · BoxCollider2D · Base(Team=Ally)   (박스 x -72~-63.6 · 8.375×7.5 = 그림의 몸통, 성문 x -66.6. 같은 진영 유닛은 몸이 통과. 체력·화살 = `Base_Test.asset`)
 ├─ Muzzle       (꼭대기 가운데, 화살 나가는 자리)
 ├─ BackStop     ← BoxCollider2D   (보이지 않음) 성 뒤 x -72.5~-72, 땅 ~ y 36
 └─ Flag         ← SpriteRenderer(`flag.png`, 진영색)   가운데 첨탑 끝
Base_Enemy       ← (거울 대칭 — 루트 x스케일 -1, 박스 x 63.6~72, 성문 x 66.6)
 ├─ Muzzle
 ├─ BackStop
 └─ Flag
DamageNumbers    ← MeshFilter · MeshRenderer · DamageNumbers   (원점 고정 — 데미지·회복 숫자를 메시 하나로, 재질 `FX_Pixel` 복제)
Battle           ← BattleManager   (라운드 흐름 허브: 상점 → 전투 → 결과)
AllyBoard        ← BoardRunner(Team=Ally)    (아군 필드 보드 실행기 — 소환 지점 = AllySpawnPoint, 큰 유닛도 같은 자리)
EnemyBoard       ← BoardRunner(Team=Enemy)   (적 보드 — 소환 = EnemySpawnPoint, 큰 유닛 = BossSpawnPoint)
AllySpawnPoint   ← (Transform만) 아군 성문(x -66.6). `Unit.Spawn`이 여기부터 앞쪽 5칸에 퍼뜨린다
EnemySpawnPoint  ← (Transform만) 적 성문(x 66.6)
BossSpawnPoint   ← (Transform만) 적의 넓은 유닛(공룡·코끼리)이 나오는 자리(지금은 적 성문과 같은 x)
EventSystem      ← EventSystem · InputSystemUIInputModule   (새 Input System 전용 — StandaloneInputModule 쓰지 말 것)
Canvas           ← Canvas(Overlay) · CanvasScaler(1920×1080) · GraphicRaycaster
 ├─ BaseBar_Ally · BaseBar_Enemy   ← Image · BaseHealthBar   (맨 윗줄 좌·우, 각 폭 900) → Fill(Filled, 아군은 왼쪽·적은 오른쪽 기준)
 ├─ AllyBoardView  ← BoardView(아군 필드, **전투 중에만**) → SlotTemplate [꺼짐] ← Image · Button · SlotView → Icon(80×80, 건물 에셋의 Icon) · Level · Gauge/Fill(Filled, 전투 중에만 보임)   (칸 x 110부터 130px 간격 → 오른쪽, y -130)
 ├─ EnemyBoardView ← BoardView(적 필드)   → SlotTemplate [꺼짐]   (오른쪽 기준 x -630부터 130px → 오른쪽. 칸 0이 왼쪽)
 ├─ RampLabel      ← Text · RampLabel   (위 가운데 y -110, 전투 중에만: 시간 + 가속(공격력·생산 수), 글자 30 · 폭 540)
 ├─ SpeedButton    ← Image · Button · SpeedButton → Label   (위 가운데 y -190, 전투 중에만 — BattleManager가 켜고 끈다. ×1 → ×2 → ×4)
 ├─ ShopPanel      ← Image(반투명 전체) · ShopPanel   (상점 단계에만 켜짐)
 │   ├─ Status   ← Text
 │   ├─ OfferTemplate [꺼짐] ← Image · Button · OfferCard → Icon · Price   (x 460부터 250px 간격, y -440, 진열 수만큼 복제)
 │   ├─ FieldLabel · FieldView ← BoardView(아군 필드) → SlotTemplate [꺼짐]   (가운데 x 700부터, y -680)
 │   ├─ StorageLabel · StorageView ← BoardView(보관함) → SlotTemplate [꺼짐]   (x 700부터, y -860)
 │   ├─ RerollButton · SellButton(+ SellDropZone: 칸을 끌어 놓으면 판매) · StartButton   ← Image · Button → Label   (y -1010)
 │   └─ DragGhost      ← Image · CanvasGroup(레이캐스트 안 막음) → Icon   (끄는 동안 포인터를 따라다님 — 반드시 ShopPanel의 마지막 자식)
 ├─ ResultPanel    [실행 시 꺼짐] ← Image → ResultText · RetryButton → Label("다음 라운드"/"새 런")
 └─ BuildingTooltip ← BuildingTooltip (켜 둔 채) → Panel(UI_Tooltip 420×300 · CanvasGroup 레이캐스트 안 막음, 판만 켜고 끔) → Title · Body · UnitPanel(UI_Tooltip 420×300, Panel 아래에 매달림 · 생산 건물일 때만) → UnitBody   (칸·진열에 마우스 올리면 그 오른쪽, 자리 없으면 왼쪽 · 윗변은 칸 윗변에 맞추고 화면을 넘으면 안으로 민다 — Canvas 마지막 자식)
```

- UI 그림은 **Claude가 만든 임시 PNG**(`Assets/Sprites/UI/UI_*.png` — 칸 120×120 · 게이지 120×12 · 카드 230×260 · 버튼 220×80 · 패널 1920×1080, PPU 100). §5-1대로 Simple + Native Size — 사용자 그림이 오면 같은 이름으로 교체.
- 🔴 **전투 중 보이는 UI는 화면 위쪽에만 둔다**(사용자 결정 9/27 — 전장이 화면 아래쪽이라 겹치지 않게). 위에서부터 y: ① 기지 체력바 -20~-50 ② 두 보드 칸 -70~-190(게이지 포함, 아군 x 50~690 · 적 1230~1870) ③ 가운데 가속 표시 y -110(x 700~1220) ④ 가운데 배속 버튼 y -150~-230(x 850~1070). **새 요소를 넣기 전에 이 구간과 겹치는지 계산할 것.**

**핵심 배선:**
- **`BackStop`은 성 뒤에 붙어 있다.** 성 위치·크기를 바꾸면 BackStop·Flag·소환 지점(성문)도 다시 맞출 것.
  에디트 모드에서 옮긴 뒤 좌표를 읽을 땐 `Physics2D.SyncTransforms()` 먼저 — 안 하면 옛 bounds가 나온다.
- 렌더러는 `Assets/Settings/Renderer2D.asset`(2D Renderer). PC·Mobile 두 `*_RPAsset` 모두 이것 하나만 쓴다.

---

## "X를 씬에 추가하려면 어디"

| 하려는 것 | 씬 작업 |
|---|---|
| 새 유닛 종류 | `Assets/Prefabs/`에 프리팹(`Unit` + `Rigidbody2D` + `BoxCollider2D` + 마찰 0 재질) + `Assets/Data/`에 `UnitDefinition` 에셋. 씬 구조 변경 없음 |
| 기지 체력 | `Assets/Data/Base_Test.asset` (씬 X) |
| 골드·목숨·가속·적 라운드·상점 목록 | `Assets/Data/BattleConfig.asset` (씬 X) |
| 새 건물 | 씬 X — `Assets/Data/Buildings/` 에셋 + `BattleConfig` 목록 |
| 필드 칸 수 | `BattleConfig`의 `Field Slots` (씬 X). 🔴 **6칸부터 아군 칸이 가운데 가속 표시(x 700~)와 겹친다** — 늘리기 전에 칸 간격·자리를 다시 계산할 것 |
| 라운드 시작 시 도는 신규 로직 | `Battle` 오브젝트에 컴포넌트 추가 |
