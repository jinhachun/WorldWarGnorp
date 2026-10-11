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
CameraRig        ← CameraDrag   (좌우 드래그·관성, 맵 끝 x -61~61에서 멈춤, 판은 아군 기지 쪽 화면에서 시작)
 └─ Main Camera  ← Camera(Orthographic, size 20, 로컬 0 · 월드 y=14, 배경 검정) · UniversalAdditionalCameraData · CameraShake   [tag=MainCamera]
Fx               ← FxDirector   (파편·먼지 프리팹 + CameraShake 참조)
Global Light 2D  ← Light2D(Global)
Ground           ← PolygonCollider2D · LineRenderer · Ground   (굽은 땅 x -61~61, 원점·스케일 1 고정. 모양은 판 시작 때 이번 맵(`RunState.Map`)의 지형 스타일로 언덕·고원·골짜기를 무작위로 놓는다 — 실행 순서 100(BattleManager 뒤). 같은 때 맵 뒷배경 겹(`MapDefinition.Backdrop` — `Assets/Sprites/Backdrop/`, 회색 실루엣, 겹마다 `BackdropParallax`)과 땅 속 검은 메시(Fill)를 만든다. 정렬: 뒷배경 -30~ < 땅 속 -10 < 탑 -1 < 유닛 0. 땅 위 소품은 없음. `_flatAnchors` 비어 있음)
Base_Ally        ← SpriteRenderer(`castle.png`, order -1 — 유닛이 성 앞에 그려짐) · BoxCollider2D · Base(Team=Ally) · Tower(망루 사이에서 솟는 하나로 이어진 탑, 층 6×2.5칸 — 층마다 처마선 + 창 안 기물 아이콘, 벽 재질 = 그 층 기물 국가(킹덤 돌벽돌·아치 창 · 티크톡 리벳 철판·톱니 창 · 공용 민 벽 · 빈 층 꺼진 창), 창 불빛 = 쿨다운(전투 중 아래부터 차오름, 쿨 없는 기물·상점은 은은히), 숫자 라벨·게이지 없음. 그림은 전부 코드 생성 · 층·Roof·층별 Flash·Icon·창은 실행 중 생성, `_flag` = 자식 Flag를 지붕 꼭대기로 옮김)   (성 x -53.75, 박스 x -60.75~-46.5 · 높이 7.125 = 그림의 몸통, 성문 x -52.31. 같은 진영 유닛은 몸이 통과. 체력·화살 = `Base_Test.asset`)
 ├─ Muzzle       (꼭대기 가운데, 화살 나가는 자리)
 ├─ BackStop     ← BoxCollider2D   (보이지 않음) 성 뒤 x -61.5~-61, 땅 ~ y 36
 └─ Flag         ← SpriteRenderer(`flag.png`, 진영색)   탑 지붕 꼭대기(Tower가 층 수 따라 옮김)
Base_Enemy       ← (거울 대칭 — 루트 x스케일 -1, 성 x 53.75, 박스 x 46.5~60.75, 성문 x 52.31)
 ├─ Muzzle
 ├─ BackStop
 └─ Flag
DamageNumbers    ← MeshFilter · MeshRenderer · DamageNumbers   (원점 고정 — 데미지·회복 숫자를 메시 하나로, 재질 `FX_Pixel` 복제)
Battle           ← BattleManager   (라운드 흐름 허브: 상점 → 전투 → 결과)
AllyBoard        ← BoardRunner(Team=Ally)    (아군 필드 보드 실행기 — 소환 지점 = AllySpawnPoint, 큰 유닛도 같은 자리)
EnemyBoard       ← BoardRunner(Team=Enemy)   (적 보드 — 소환 = EnemySpawnPoint, 큰 유닛 = BossSpawnPoint)
AllySpawnPoint   ← (Transform만) 아군 성문(x -52.31). `Unit.Spawn`이 여기부터 앞쪽 5칸에 퍼뜨린다
EnemySpawnPoint  ← (Transform만) 적 성문(x 52.31)
BossSpawnPoint   ← (Transform만) 적의 넓은 유닛(공룡·코끼리)이 나오는 자리(지금은 적 성문과 같은 x)
EventSystem      ← EventSystem · InputSystemUIInputModule   (새 Input System 전용 — StandaloneInputModule 쓰지 말 것)
Canvas           ← Canvas(Overlay) · CanvasScaler(1920×1080) · GraphicRaycaster
 ├─ BaseBar_Ally · BaseBar_Enemy   ← Image · BaseHealthBar   (맨 윗줄 좌·우, 각 폭 900) → Fill(Filled, 아군은 왼쪽·적은 오른쪽 기준) · HpText(현재 체력 숫자, 바 가운데 24px · Outline)
 ├─ AllyBoardView  ← BoardView(아군 필드, **전투 중에만**) → SlotTemplate [꺼짐] ← Image(탑 층 벽 `UI_Floor*` 120×120) · Button · SlotView → Light(66×66 Filled 세로·아래 기준 — 창 불빛 = 쿨다운) · Icon(×0.6) · RarityFrame(창틀 `UI_Window*`, 등급 색) · Level(창 아래 15pt)
 ├─ EnemyBoardView ← BoardView(적 필드)   → SlotTemplate [꺼짐]   (오른쪽 기준 x -630부터 130px → 오른쪽. 칸 0이 왼쪽)
 │   (탑 칸 3곳 — FieldView · AllyBoardView · EnemyBoardView의 SlotTemplate — 은 전장 탑과 같은 층 모양(`SlotView.ShowFloor` — 벽 재질 = 기물 국가: 킹덤 `UI_FloorBrick`+아치 창 · 티크톡 `UI_FloorIron`+톱니 창 · 공용·빈 `UI_FloorPlain`). 보관함 칸은 그대로)
 │   (아군·적 줄과 상점 FieldView는 세로 탑(`_stack`) — 칸 0(1층) 바닥이 y -1000에 고정되고 위로 자란다 — 지붕 `Roof`(`UI_Roof.png` 임시, 진영색)가 맨 위 층을 따라간다(높이 비교용, 사용자 10/8). 아군 = 화면 왼쪽 끝 · 적 = 오른쪽 끝. 전투 중엔 그 진영 탑 가로 가운데가 화면 안이면 숨는다)
 ├─ CombatPower    ← CombatPowerView → Content → Bar(`UI_PowerBar` — EnemyFill · AllyFill(Filled) · Tick, 유닛 수 비율) · AllyPower/EnemyPower(Icon `UI_IconUnitAlly`/`UI_IconUnitEnemy` · Text 「N기 · 초당 x기」)   (BaseBar_Ally 다음 형제, 위 가운데 y -72, 전투 중에만 — `CombatPower.For/Count`, 막대는 비율을 0.5초쯤에 따라감)
 │   (맵 아이콘: `MapLabel._icons` = 맵 ↔ 아이콘 표의 원본(`UI_Map*` 32×32 임시) — 전투 `MapLabel/Icon`, 상점 `ShopPanel/MapIcon`이 `MapLabel.IconFor`로 읽음. 버튼 7개(잠금·리롤·층 올리기·판매·전투 시작·SpeedButton·RetryButton)에 `ButtonHover` — 평소 한 단계 어둡고 올리면 제 밝기)
 ├─ MapLabel       ← MapLabel   (전투 중 위 가운데, 배속 버튼 아래 맵 이름 — 올리면 BuildingTooltip에 설명)
 ├─ TowerHover     ← TowerHover   (마우스가 전장 탑 층(`TowerFloor`)에 있으면 `BuildingTooltip.ShowOwned` — 전투 중에만, UI가 우선)
 ├─ RampLabel      ← Text · RampLabel   (위 가운데 (65, -110) — 두 보드 사이 빈 곳 가운데라 배속 버튼보다 오른쪽, 전투 중에만: 시간 + 데스매치 단계, 글자 30 · 폭 540)
 ├─ SpeedButton    ← Image · Button · SpeedButton → Label   (위 가운데 y -190, 전투 중에만 — BattleManager가 켜고 끈다. ×1 → ×2 → ×4)
 ├─ ShopPanel      ← Image(반투명 전체) · ShopPanel   (상점 단계에만 켜짐)
 │   ├─ Status   ← Text
 │   ├─ MapLabel ← Text   (위 가운데 — 이번 라운드 맵 이름·설명)
 │   ├─ OfferTemplate [꺼짐] ← Image(투명 — 레이캐스트 230×260) · Button · OfferCard · CanvasGroup(살 수 없으면 — `RunState.CanBuy` 거짓 — 알파 0.4) → Floor(층 조각 120×120 + FloorLook: Light · Icon ×0.6 · RarityFrame = 창틀 · Cap `UI_FloorCap`) · Name · PriceRow · Locked(잠김 표시, 회색, 가격 아래 -132) · Glow(`UI_CardGlow` 156×156, 층 조각 뒤 — 보유 = 흰 숨쉬기 · RANK UP = 금색) · RankUp(「RANK UP!」 +118, 튐 — 판정 `RunState.FindMergeTarget`)   (x 460부터 250px 간격, y -440, 진열 수만큼 복제)
 │   ├─ FieldLabel · FieldView ← BoardView(아군 필드) → SlotTemplate [꺼짐]   (가운데 x 700부터, y -680)
 │   ├─ StorageLabel(「보관함 n/6」, 상자 왼쪽 위) · OfferChances(등급별 등장 확률 다섯 숫자, 같은 줄 오른쪽 끝 — `ShopPanel._chances`) · StorageView ← Image(`UI_Crate` 804×276) · BoardView(보관함) · StorageBox → SlotTemplate [꺼짐] = 층 조각   (상자 (960, -778). 백팩배틀즈식 — 끌던 속도로 던져져 돌며 구르다 멈춤(`ShopPanel.DropOnBox`) — 물리는 런타임에 만든 `StorageBoxPhysics…` 씬의 전용 2D 물리 공간(플레이 중 하이어라키에 씬이 하나 더 보이는 게 정상), 자리·각도는 정적 변수로 라운드 넘어 유지, 빈 칸은 안 보임)
 │   (상점 배치 10/8 UI 정리: 판 불투명 검정 · 필드 탑 = 전투 아군 탑 자리(x 75, 바닥 y -1000) · 상태 줄 y -90 · MapBox 1200×130 · 진열 x 440부터 260 간격(OfferTemplate/Name 기물 이름) · 보관함 y -680 · 버튼 y -990, 「전투 시작」만 주 버튼(`UI_ButtonMain`) · FieldLabel 꺼짐.
 │    톤: 판·칸·버튼·툴팁 = `UI_*Dark`·`UI_MapBox`·`UI_TooltipWide`(검은 속 + 흰 3px, 코드로 만든 임시 그림). 형제 순서 …ShopPanel → BaseBar ×2 → EnemyBoardView → ResultPanel → TowerHover → BuildingTooltip — 체력 바·적 탑은 상점 판 위)
 │   (StatusLine = Round·Gold·Lives·Wins 네 칸, 각 아이콘 + 글 · 버튼 글은 `LabelRow`(아이콘 + Label) 안 · OfferTemplate/PriceRow = 동전 + 가격 · 아이콘은 `UI_Icon*` 32×32 임시)
 │   ├─ InsertLine  (끄는 동안 층 경계 노랑 선 — `ShopPanel.FindInsert`가 층 위·아래 1/4 구역이면 끼워 넣기, 가운데면 맞바꿈·합치기)
 │   ├─ LockButton(`RunState.ToggleShopLock`) · RerollButton · RaiseButton(층 올리기 NG / 최고층) · SellButton(+ SellDropZone: 칸을 끌어 놓으면 판매) · StartButton   ← Image · Button → Label   (y -1010)
 │   └─ DragGhost      ← Image(층 조각, 벽 금색 틴트) · FloorLook · CanvasGroup(레이캐스트 안 막음) → Light · Icon · Window(등급 색) · Cap   (끄는 동안 포인터를 따라다님 — 반드시 ShopPanel의 마지막 자식)
 ├─ ResultPanel    [실행 시 꺼짐] ← Image → ResultText(한 줄) · ResultStats(`ResultStats` — 목숨·승리 아이콘 줄, y -10) · ResultTime(회색 「전투 시간 분:초」, y -50 — `RampLabel.LastElapsed`) · RetryButton(y -134) → Label("다음 라운드"/"새 런")
 └─ BuildingTooltip ← BuildingTooltip (켜 둔 채) → Panel(UI_TooltipWide · CanvasGroup 레이캐스트 안 막음, 판만 켜고 끔) → Title · Body · UnitPanel(UI_TooltipWide, Panel 아래에 매달림 · 설명에 나오는 유닛마다 한 장 — 이 판이 틀, 둘째 장부터 실행 중 복제해 아래로 6px 간격으로 쌓음) → UnitBody   (칸·진열에 마우스 올리면 그 오른쪽, 자리 없으면 왼쪽 · 윗변은 칸 윗변에 맞추고 화면을 넘으면 안으로 민다 — Canvas 마지막 자식)
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
| 새 유닛 종류 | `Assets/Prefabs/Units/`에 프리팹(`Unit` + `Rigidbody2D` + `BoxCollider2D` + 마찰 0 재질) + `Assets/Data/Units/`에 `UnitDefinition` 에셋. 씬 구조 변경 없음 |
| 기지 체력 | `Assets/Data/Base_Test.asset` (씬 X) |
| 골드·목숨·가속·적 라운드·상점 목록 | `Assets/Data/BattleConfig.asset` (씬 X) |
| 새 건물 | 씬 X — `Assets/Data/Buildings/` 에셋 + `BattleConfig` 목록 |
| 필드 칸 수 | `BattleConfig`의 `Field Slots` (씬 X). 🔴 **6칸부터 아군 칸이 가운데 가속 표시(x 700~)와 겹친다** — 늘리기 전에 칸 간격·자리를 다시 계산할 것 |
| 라운드 시작 시 도는 신규 로직 | `Battle` 오브젝트에 컴포넌트 추가 |

## World 씬 (10/11 — 월드맵, 빌드 0번)
`Assets/Scenes/World.unity`. 오브젝트는 전부 Canvas(uGUI) — 지도도 UI다. 트리·참조의 원본은 씬 파일이고, 여기엔 덩어리만 적는다.
- `WorldManager`(`World/WorldManager`) — 턴 시작 · 씬 전환 · AI 턴 재생.
- Canvas: `TopBar`(턴 · 골드 · 행복도 · 땅) · `SidePanel`(내 영지 요약 · 고른 땅·영주 · 영주 탑 · 「침략」「쉬기」「상점」 · 요약/통일/게임 오버 글) · `Map`(영토 지도 — `UI/WorldMapView`가 땅 번호 텍스처를 칠한다) · `SkipReplay` · `BuildingTooltip`.
- Battle 씬에서 달라진 것: 상점의 시작 버튼 = 「지도로」, 결과 버튼 = 「지도로」, `ShopPanel/InvadePanel`은 없다.
- **상점도 같은 틀(10/11)**: Battle 씬 `ShopPanel` 아래에 `LineSide` · `LineTop` · `TopBar` · `SidePanel`(지도와 같은 `WorldSidePanel`, 탑 기둥 없음) · `MapButton`(「지도로」). 상점 판이 켜진 동안 본진 체력바 둘과 상대 UI 탑은 꺼진다(`ShopPanel._hiddenInShop`). 옛 `StatusLine` · `MapBox`류 · `StartButton`은 없다.
- **상점 = 지도 위 팝업(10/11 — 바로 위 「상점도 같은 틀」 줄을 대체)**: World 씬 Canvas의 `ShopSlot/ShopPopup`(프리팹 `Assets/Prefabs/UI/ShopPopup`)과 위 줄 뒤의 검은 띠 `TopBack`. Battle 씬에는 `ShopPanel`이 없고, 꺼진 `Templates/UpgradeCountTemplate`(체력바 아래 업그레이드 줄의 글자 틀)이 있다.
