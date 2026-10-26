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
| `Assets/Scenes/World.unity` | 0 | 월드맵 — 국가 고르기 · 지도 · 상점 팝업 · 턴 (플레이 시작) |
| `Assets/Scenes/Battle.unity` | 1 | 전투만 (침략마다 불러온다) |

---

## Battle.unity

```
CameraRig        ← CameraDrag   (좌우 드래그·관성, 맵 끝 x -61~61에서 멈춤, 판은 아군 기지 쪽 화면에서 시작. **끌기는 좌우만 — 세로 이동 없음**(사용자 10/12) · 스크롤 줌은 화면 아래 기준, 가장 당긴 크기는 `_closestSize`)
 └─ Main Camera  ← Camera(Orthographic, size 20, 로컬 0 · 월드 y=14, 배경 검정) · UniversalAdditionalCameraData · CameraShake   [tag=MainCamera]
Fx               ← FxDirector   (파편·먼지 프리팹 + CameraShake 참조)
Global Light 2D  ← Light2D(Global)
Ground           ← PolygonCollider2D · LineRenderer · Ground   (굽은 땅 x -61~61, 원점·스케일 1 고정. 모양은 판 시작 때 이번 맵(`RunState.Map`)의 지형 스타일로 언덕·고원·골짜기를 무작위로 놓는다 — 실행 순서 100(BattleManager 뒤). 같은 때 맵 뒷배경 겹(`MapDefinition.Backdrop` — `Assets/Sprites/Backdrop/`, 회색 실루엣, 겹마다 `BackdropParallax`)과 땅 속 검은 메시(Fill)를 만든다. 정렬: 뒷배경 -30~ < 땅 속 -10 < 탑 -1 < 유닛 0. 땅 위 소품은 없음. `_flatAnchors` 비어 있음)
TerrainFx        ← Fx/TerrainFx (재질 FX_Pixel — 지형 연출: 땅 장식 · 하늘 입자 · 탑 창 표시, 맵 종류 = `MapDefinition.Look` — 10/20)
Base_Ally        ← SpriteRenderer(성 그림 = 그 진영 국가의 성 — `WorldConfig._castles`: 킹덤·공용 `castle.png`(사용자 그림) · 티크톡 `castle_ticktock` · 트라이브 `castle_tribe`(몸통은 castle.png 그대로 + 망루 둘과 성문 실루엣만 국가별, 회색 없음, 영주 색 띠 한 곳 — 킹덤 그림 밀도에 맞춤, 10/22), `Tower.Start`가 `RunState.NationOf(Team)`로 바꿈(10/22), order -1 — 유닛이 성 앞에 그려짐) · BoxCollider2D · Base(Team=Ally) · Tower(망루 사이에서 솟는 하나로 이어진 탑, 층 6×2.5칸 — 층마다 처마선 + 창 안 기물 아이콘 + 창틀(`Frame`, 그 층 기물 등급 테두리 색 · 빈 층 흰색 · 꺼진 층 ×회색 — 10/20), 벽 재질 = 그 층 기물 국가(킹덤 돌벽돌·아치 창 · 티크톡 리벳 철판·톱니 창 · 트라이브 통나무 말뚝·뼈 모서리 네모 창 · 공용 민 벽 · 빈 층 꺼진 창), 창 불빛 = 쿨다운(전투 중 아래부터 차오름, 쿨 없는 기물·상점은 은은히), 숫자 라벨·게이지 없음. 그림은 전부 코드 생성 · 층·Roof·층별 Flash·Icon·창은 실행 중 생성, `_flag` = 자식 Flag를 지붕 꼭대기로 옮김)   (성 x -53.75, 박스 x -60.75~-46.5 · 높이 7.125 = 그림의 몸통, 성문 x -52.31. 같은 진영 유닛은 몸이 통과. 체력·화살 = `Base_Test.asset`)
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
Canvas           ← Canvas(Overlay) · CanvasScaler(1920×1080, Expand) · GraphicRaycaster
 │   (10/22부터 화면 UI는 전부 `Canvas/Screen` 아래 — 가운데 기준 1920×1080, RectMask2D 없음(전장은 카메라가 화면 전체에 그리고, 동전·전장 탑 툴팁은 판 밖에도 뜬다). ResultPanel 덮개만 sizeDelta (3840,1080)으로 판보다 크게 — 어떤 화면비에서도 화면 전체를 덮는다. 아래 트리는 Screen 아래 기준, 「화면 왼쪽/오른쪽 끝」은 판(1920)의 끝)
 ├─ BaseBar_Ally · BaseBar_Enemy   ← Image · BaseHealthBar   (맨 윗줄 좌·우, 각 폭 900) → Fill(Filled, 아군은 왼쪽·적은 오른쪽 기준) · HpText(현재 체력 숫자, 바 가운데 24px · Outline)
 ├─ AllyBoardView  ← BoardView(아군 필드, **전투 중에만**) → SlotTemplate [꺼짐] ← Image(탑 층 벽 `UI_Floor*` 120×120) · Button · SlotView → Light(66×66 Filled 세로·아래 기준 — 창 불빛 = 쿨다운) · Icon(×0.6) · RarityFrame(창틀 `UI_Window*`, 등급 색) · Level(창 아래 15pt)   (늪지에서 꺼진 기물의 층은 밝기 ×0.4로 회색이 되고 가속 표시 자리에 빨간 금지 표시 `UI_IconStopped` — 판정은 `BoardRunner.StoppedByMap`, 그림 칸은 `SlotView`·`FloorLook`·`Tower`의 `_stoppedIcon`)
 ├─ EnemyBoardView ← BoardView(적 필드)   → SlotTemplate [꺼짐]   (오른쪽 기준 x -630부터 130px → 오른쪽. 칸 0이 왼쪽)
 │   (탑 칸 3곳 — FieldView · AllyBoardView · EnemyBoardView의 SlotTemplate — 은 전장 탑과 같은 층 모양(`SlotView.ShowFloor` — 벽 재질 = 기물 국가: 킹덤 `UI_FloorBrick`+아치 창 · 티크톡 `UI_FloorIron`+톱니 창 · 트라이브 `UI_FloorTribe`+뼈 모서리 네모 창 · 공용·빈 `UI_FloorPlain`). 보관함 칸은 그대로)
 │   (아군·적 줄과 상점 FieldView는 세로 탑(`_stack`) — 칸 0(1층) 바닥이 y -1000에 고정되고 위로 자란다 — 지붕 `Roof`(`UI_Roof.png` 임시, 진영색)가 맨 위 층을 따라간다(높이 비교용, 사용자 10/8). 아군 = 화면 왼쪽 끝 · 적 = 오른쪽 끝. 전투 중엔 그 진영 탑 가로 가운데가 화면 안이면 숨는다)
 ├─ CombatPower    ← CombatPowerView → Content → Bar(`UI_PowerBar` — EnemyFill · AllyFill(Filled) · Tick, 유닛 수 비율) · AllyPower/EnemyPower(Icon `UI_IconUnitAlly`/`UI_IconUnitEnemy` · Text 「N기 · 초당 x기」)   (BaseBar_Ally 다음 형제, 위 가운데 y -72, 전투 중에만 — `CombatPower.For/Count`, 막대는 비율을 0.5초쯤에 따라감)
 │   (맵 아이콘: `MapLabel._icons` = 맵 ↔ 아이콘 표의 원본(`UI_Map*` 32×32 임시) — 전투 `MapLabel/Icon`, 상점 `ShopPanel/MapIcon`이 `MapLabel.IconFor`로 읽음. 버튼 7개(잠금·리롤·층 올리기·판매·전투 시작·SpeedButton·RetryButton)에 `ButtonHover` — 평소 한 단계 어둡고 올리면 제 밝기)
 ├─ MapLabel       ← MapLabel   (전투 중 위 가운데, 배속 버튼 아래 맵 이름 — 올리면 BuildingTooltip에 설명)
 ├─ TowerHover     ← TowerHover   (마우스가 전장 탑 층(`TowerFloor`)에 있으면 `BuildingTooltip.ShowOwned` — 전투 중에만, UI가 우선)
 ├─ UnitHover      ← UI/UnitHover → Card(UnitPanel 복제 — 검은 판 · NationFrame, 폭 420: 그림 · 이름 · 체력 줄 + UI_PowerBar 막대(영주 색) · 공격 줄 · 상태 줄)   (전투 중 전장 유닛에 마우스 — 지금 체력·공격력·상태, 고른 유닛 둘레 꺾쇠 Fx/HoverMark. 설명 창이 떠 있거나 UI 위면 안 뜸 — 10/23)
 ├─ RampLabel      ← Text · RampLabel   (위 가운데 (65, -110) — 두 보드 사이 빈 곳 가운데라 배속 버튼보다 오른쪽, 전투 중에만: 시간 + 데스매치 단계, 글자 30 · 폭 540)
 ├─ SpeedButton    ← Image · Button · SpeedButton → Label   (위 가운데 y -190, 전투 중에만 — BattleManager가 켜고 끈다. ×1 → ×2 → ×4)
 ├─ TerrainBanner  ← CanvasGroup · UI/TerrainBanner → Title(도트 48) · Body(도트 24)   (전투 시작 때 가운데 지형 이름 + 효과 글 1.6초 → 0.4초 사라짐, 클릭 안 막음 — 10/20)
 ├─ BattleGold     ← UI/BattleGoldView   (왼쪽 위 내 체력바 아래 — 전투 중 골드: 동전 아이콘 + 노란 숫자, 주식시장 골드가 닿을 때 오른다. 형제 순서는 BuildingTooltip 바로 앞 — 툴팁이 위에 그려진다. 자리는 매 프레임 내 업그레이드 줄 바로 아래, 타워 UI와 겹치면 그 오른쪽으로(10/22))
 ├─ GoldCoinLayer  ← Fx/GoldCoinFx가 그리는 판(맨 위 — 죽은 적 자리에서 튄 동전이 BattleGold로 날아감)
 ├─ 〈ShopPanel〉   🔴 **이 아래 트리는 Battle 씬이 아니라 프리팹 `Assets/Prefabs/UI/ShopPopup`의 것이다**(10/11 상점이 World 씬 팝업으로 옮겨감 — `Battle.unity`에 `ShopPanel`은 없다). 아래 「World 씬」 절과 같이 볼 것
 │   ├─ Status   ← Text
 │   ├─ MapLabel ← Text   (위 가운데 — 이번 라운드 맵 이름·설명)
 │   ├─ OfferTemplate [꺼짐] ← Image(투명 — 레이캐스트 230×260) · Button · OfferCard · CanvasGroup(살 수 없으면 — `RunState.CanBuy` 거짓 — 알파 0.4) → Floor(층 조각 120×120 + FloorLook: Light · Icon ×0.6 · RarityFrame = 창틀 · Cap `UI_FloorCap`) · Name · PriceRow · Locked(잠김 표시, 회색, 가격 아래 -132) · Glow(`UI_CardGlow` 156×156, 층 조각 뒤 — 보유 = 흰 숨쉬기 · RANK UP = 금색) · RankUp(「RANK UP!」 +118, 튐 — 판정 `RunState.FindMergeTarget`)   (x 460부터 250px 간격, y -440, 진열 수만큼 복제)
 │   ├─ FieldLabel · FieldView ← BoardView(아군 필드) → SlotTemplate [꺼짐]   (가운데 x 700부터, y -680)
 │   ├─ StorageLabel(「보관함 n/6」, 상자 왼쪽 위) · OfferChances(등급별 등장 확률 다섯 숫자, 같은 줄 오른쪽 끝 — `ShopPanel._chances`) · StorageView ← Image(`UI_Crate` 804×276) · BoardView(보관함) · StorageBox → SlotTemplate [꺼짐] = 층 조각   (상자 (960, -778). 백팩배틀즈식 — 끌던 속도로 던져져 돌며 구르다 멈춤(`ShopPanel.DropOnBox`) — 물리는 런타임에 만든 `StorageBoxPhysics…` 씬의 전용 2D 물리 공간(플레이 중 하이어라키에 씬이 하나 더 보이는 게 정상), 자리·각도는 정적 변수로 라운드 넘어 유지, 빈 칸은 안 보임)
 │   (상점 배치 10/8 UI 정리: 판 불투명 검정 · 필드 탑 = 전투 아군 탑 자리(x 75, 바닥 y -1000) · 상태 줄 y -90 · MapBox 1200×130 · 진열 x 440부터 260 간격(OfferTemplate/Name 기물 이름) · 보관함 y -680 · 버튼 y -990, 「전투 시작」만 주 버튼(`UI_ButtonMain`) · FieldLabel 꺼짐.
 │    톤: 판·칸·버튼·툴팁 = `UI_*Dark` 판·버튼 · 툴팁 판 = 그림 없는 검은 Image + NationFrame 테두리(10/25 확인 — `UI_TooltipWide`·`UI_MapBox`는 어디서도 안 씀). 버튼 · 탑 층 · 지붕 · 업그레이드 카드 · 국가 줄 · 진열 카드 · 유닛 카드는 `Assets/Prefabs/UI/Parts/` 부품 프리팹의 인스턴스 — 원본 목록 `Docs/UI_부품목록.md`. 형제 순서 …ShopPanel → BaseBar ×2 → EnemyBoardView → ResultPanel → TowerHover → BuildingTooltip — 체력 바·적 탑은 상점 판 위)
 │   (StatusLine = Round·Gold·Lives·Wins 네 칸, 각 아이콘 + 글 · 버튼 글은 `LabelRow`(아이콘 + Label) 안 · OfferTemplate/PriceRow = 동전 + 가격 · 아이콘은 `UI_Icon*` 32×32 임시)
 │   ├─ InsertLine  (끄는 동안 층 경계 노랑 선 — `ShopPanel.FindInsert`가 층 위·아래 1/4 구역이면 끼워 넣기, 가운데면 맞바꿈·합치기)
 │   ├─ LockButton(`RunState.ToggleShopLock`) · RerollButton · RaiseButton(층 올리기 NG / 최고층) · SellButton(+ SellDropZone: 칸을 끌어 놓으면 판매) · StartButton   ← Image · Button → Label   (y -1010)
 │   └─ DragGhost      ← Image(층 조각, 벽 금색 틴트) · FloorLook · CanvasGroup(레이캐스트 안 막음) → Light · Icon · Window(등급 색) · Cap   (끄는 동안 포인터를 따라다님 — 반드시 ShopPanel의 마지막 자식)
 ├─ ResultPanel    [실행 시 꺼짐] ← Image → ResultText(한 줄) · ResultStats(`ResultStats` — 목숨·승리 아이콘 줄, y -10) · ResultTime(회색 「전투 시간 분:초」, y -50 — `RampLabel.LastElapsed`) · RetryButton(y -134) → Label("다음 라운드"/"새 런")
 │   (10/21 딜 미터기: Board(검은 속 + 회색 2px 선 + 네 모서리 매듭 `UI_FrameKnot`, 크기는 줄 수에 맞춰 코드가 정함) 아래 ResultText(도트 72) · ResultSummary(하트) · ResultStats · ResultTime · SummaryHeader「준 피해」 · SummaryCells(기물 줄 — 아이콘 64 · 이름 · `UI_MeterBar` 막대 · 숫자, 많은 순 최대 8 + 타워 · 기타) · RetryButton(UI_CmdButton). 채우기·움직임은 `UI/BattleSummary`, 숫자는 `Battle/DamageMeter`, 「준 피해」 선 오른쪽에 「나 / 적」 탭(`UI/TabButton`, `UI_Tab*` 88×40 — 10/22))
 └─ BuildingTooltip ← BuildingTooltip (켜 둔 채) → Panel(검은 Image + NationFrame · CanvasGroup 레이캐스트 안 막음, 판만 켜고 끔) → Title · Body · UnitPanel(UI_UnitCard 프리팹, Panel 아래에 매달림 · 설명에 나오는 유닛마다 한 장 — 이 판이 틀, 둘째 장부터 실행 중 복제해 아래로 6px 간격으로 쌓음) → UnitBody   (상점이 열려 있으면 상점 안 고정 자리 `TooltipDock`에 그 폭으로, 상점 밖에선 칸 옆 + 유닛 판을 옆으로 펼침 · 유닛 판 능력치 줄 앞 색 아이콘 — Canvas 마지막 자식)
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
- Canvas(10/22부터 화면 UI는 전부 `Canvas/Screen` 아래 — 가운데 기준 1920×1080 + RectMask2D, CanvasScaler = Expand라 16:9가 아니면 검은 띠. 아래 경로는 Screen 아래 기준): `TopBar`(아이콘 + 숫자 — 달력 턴 · 동전 골드 · 하트 행복도 · 왕관 땅 n/30(10/21) · 그 오른쪽에 **내 업그레이드 줄** — 가로 `ScrollRect`, 스크롤바 없음) · `SidePanel` · `Map`(영토 지도 — `UI/WorldMapView`가 땅 번호 텍스처를 칠하고, 땅마다 지형 아이콘 · 지역 특수 표시 · 내 땅이면 그 위에 왕관(`UI_IconCrown`)을 띄운다. 땅 번호 글자는 없다(사용자 10/15). 내 땅 덩어리 국경은 흰 테두리 + 안쪽 밝은 띠 + 바깥 검은 그림자로 굵게(4X식, 사용자 10/15). 남의 땅 속 = 지형색(`MapDefinition` 「지도 색」 `_mapColor`, 침략 가능 땅 100% · 나머지 72%) · 영토(같은 주인의 땅 전부) 바깥 둘레에만 영주 색 띠 4칸 · 영주 사이 국경 · 해안선 = 검은 선 · 같은 영주의 땅 사이 = 가는 반투명 검은 선 1칸(10/26 — 지형 무늬 도장은 색맹 보조로 남김). 내 영토는 내 색 채움 + 바깥 둘레에만 흰 굵은 테두리. 이벤트 땅은 어느 영토도 아니다. 땅을 안 골라도 탑 기둥에 내 탑) · `SkipReplay` · `BuildingTooltip`. 10/21 꾸미기: `Frame`(`UI_WorldFrame` 1920×1080 화면 테두리 — 흰색 6px 막대(10/23 — 2중 선을 채움, 사용자) · 이음매 매듭, 바깥 막대는 가장자리 8px 안 · 그 밖은 검정 띠(매듭이 그림 밖으로 안 나가게 — 10/23), ShopButton 다음 형제, 레이캐스트 끔) · 명령 버튼 셋(침략 · 쉬기 · 상점) = `UI_CmdButton*` 220×80 Sprite Swap 네 상태 · ButtonHover `_pressDepth` 4 · SidePanel 가로 구분선 = `UI_SideDivider`(6px 흰 막대) · 탑 기둥 세로선도 6px 막대. 막대 옆 여백 · 탑 기둥 탑 자리(1층이 바닥 막대 위) · 지도 표시 자리(y 67)는 `UI/FrameBars`가 정함(10/23). 지도는 375×249칸 · `Map` 1500×996(1칸 4px) — 바닥 막대에 안 걸림(10/23 다시 굽기). 떠 있는 기물 툴팁은 World에서 막대 안쪽 16px 안에만 뜬다(`BuildingTooltip.Framed` — `WorldSidePanel`이 넣고, Battle은 비어 있어 원래 자리 계산).
- **`SidePanel`(10/12 — 워크래프트2식 초상화 배치, `UI/WorldSidePanel`)**: 기둥 둘이 세로 선(2px, 명령을 받는 동안만)으로 갈린다.
  - **정보 기둥** — 고른 영주 그림(깃발 포함, 몸 64px · 몸 중심을 이름 글 중심에) · 이름 · 국가 · 그 영주의 업그레이드 줄(합치기 보상 — 등급 색 테두리 칸 + 「×N」) · 구분선 · 「지형 :」 · 「패배시」 + 하트. 고른 땅이 없으면 나 + 안내 글, 상점이 보이는 동안은 나 + 「내 영지」 + 국가만(지형 줄은 빈다). 고른 땅에 지역 특수 업그레이드가 있으면 「지형 :」 아래에 아이콘 + 이름 + 효과 글.
  - **`Canvas/UpgradePicker`**(Dim · Cards · Header · SkipButton, `BuildingTooltip` 바로 앞 — 상점 팝업 밖) — 업그레이드 고르기 창. 스스로 `RunState.PendingUpgrades`를 보고 지도든 상점이든 화면 전체를 막고 뜬다(합치기 보상 · 멸망 보상). AI 턴 재생 중에는 안 뜬다. 국가 위기 고르기(`UpgradeOffer.Crisis`)면 Header가 「국가 위기」, 그 아래 실행 중 생기는 `CrisisBody` 글, 건너뛰기 없음(10/23). 위 줄 하트(`UI/HeartRow`)는 목숨 — 위기 중엔 빈 하트 하나가 빨갛게 두근거림(`UI/HeartPulse` — 글자 없음, 사용자 10/23). `SkipButton`(「건너뛰기 +N골드」)은 멸망 보상(지역 특수) 고르기에만 보인다 — N은 `WorldConfig` 「건너뛰면 받는 골드」.
  - 지도의 땅 표시 = 지형 아이콘 + (특수 지역이면) 오른쪽에 빨강 테두리 칸.
  - `NationPanel`(국가 고르기, 화면 전체 · B안 10/21) — 왼쪽 `Title`·`Divider`·`RowTemplate`(UI/NationRow, 국가 수만큼 복제)·`Hint` / 탑 `Floor0~2`(FloorLook)·`Roof` / 무대 `StageTitle`(Emblem·Name·Line)·`Backdrop`(RectMask2D)·`Landmark`·`Ground`·`Units`·`Path0~2`(Icon·Name·Line) / `NationStart`(명령 버튼)·`KeyHint` / 맨 위 `Frame`(UI_WorldFrame). 채우기·움직임은 `UI/NationCard`(`Chosen` 이벤트 — 상태 변경은 `WorldManager`), 순서 = `RunState.NationChoices`, 내용은 `WorldConfig` 「국가 고르기 화면의 무대」(`NationShowcase`).
  - `NicknamePanel`(닉네임 묻기, 화면 전체 검은 바탕 · 꺼 둠, NationPanel 다음 형제 — 10/23) — `Box`(720×400, 흰 6px 막대 `BarTop/Bottom/Left/Right`) · `Title`(「닉네임」 도트 48) · `Line`(회색 안내 도트 24) · `Row/Field`(InputField 도트 48, 9자 — 남의 판 패널 이름 칸 폭 기준) · `Row/Underline` · `Error`(빈 값 안내, 꺼 둠) · `Confirm`(명령 버튼) · `KeyHint`. 여는 쪽은 `UI/NationCard.Open`(새 런마다 한 번 — 사용자 10/23, 지난 값 미리 채움), 값은 `RunState.PlayerName` + PlayerPrefs `PlayerName`.
  - **탑 기둥**(x 348) — 그 영주의 탑. 상점 팝업이 켜지는 동안은 숨고, 그 자리에 아래 `ShopTower`가 선다.
  - 「침략」「쉬기」「상점」 버튼 · 요약/통일/게임 오버 글.
- **씬에 없고 실행 중에 생기는 것 둘** — 씬 파일에서 찾지 말 것.
  - **`ShopTower`(10/12)**: `WorldManager`가 패널 바로 위에 만드는 빈 판. 상점 팝업이 켜질 때 팝업 안의 탑 판(`FieldView`)·끄는 조각·끼울 자리 선을 **이 판으로 옮겨** 패널 탑 기둥 자리에 세운다(부모만 바뀐다 — 상점 로직·프리팹은 그대로). 팝업이 미끄러지는 동안에도 제자리고, 다 닫히면 사라진다. 그래서 상점 중에 보이는 내 탑은 **하나**다.
  - **지도 위 영주 그림**: `UI/WorldLords`가 만든다(어슬렁 · 침략 걸음 · 이름표 · 죽어 날아감 · 구름). 이름표는 마우스가 올라간 땅의 주인 한 명만 켠다. 깃발 = `WorldMapView` · `WorldSidePanel`의 `_lordFlag` = `flag_square.png`(네모 깃발 8×12 — 장대 아래 끝은 몸 중간, 천 7×7은 머리 위 몸 바깥쪽 옆, 영주 색 테두리 1칸 + 가운데 사진 5×5. 사진 = `WorldLords.PhotoOf`: 나 = `WorldConfig` 「플레이어 임시 프로필 사진」 `profile_player.png` · AI = 「임시 프로필 사진」 `profile_temp.png`, 칠하지 않음 — 나중에 스팀 아바타, 사용자 10/23).
- **땅 호버 카드(10/26)**: `Canvas/Screen/LandHover`(UI/LandHover, 자식 `LandCard` = `Parts/UI_LandCard` 인스턴스, EventVisit 다음 형제) — 땅에 올리면 주인 · 국가 · 땅 수 · 지형 + 효과 · 지역 특수 · 패배시. 명령 중에만, 상점 · 방문 장면 · 다른 설명 창이 떠 있으면 숨는다. **지도의 땅 표시 = (지역 특수면) 빨강 테두리 칸 또는 (이벤트 땅이면) 이벤트 칸 하나 + 내 땅 왕관 — 지형 아이콘은 없다**(땅 카드가 보여 준다). 이벤트 칸은 마우스를 받지 않음(땅 카드가 대신).
- **이벤트 방문 장면(10/25)**: `Canvas/Screen/EventVisit`(UI/EventVisitPopup, SkipReplay 다음 형제) — 실행 중 Dim · Sheet(Panel/Stage/Frame · Title · Line · TooltipDock · Things · Action) · Skip을 스스로 만든다. `WorldManager._visit`로 연다. 지도의 이벤트 땅 = 지형 아이콘 + 흰 테두리 이벤트 칸(Land##/Event, `UI_UpgradeCell_Hover`) · 열림/방문 가능/닫힘 세 상태. 소품 · 아이콘 그림 = `Assets/Sprites/World/Event/`(임시 — 같은 이름으로 덮어쓰면 반영).
- **상점 = 지도 위 팝업(10/11)**: World 씬 Canvas의 `ShopSlot/ShopPopup`(프리팹 `Assets/Prefabs/UI/ShopPopup`)과 위 줄 뒤의 검은 띠 `TopBack`. 팝업 안에는 `LineSide` · `LineTop`(국가 테두리 회색 눈금 — 10/23부터 꺼 둠, 흰 막대만) · `MapButton`(「지도로」)이 있고, 옛 `StatusLine` · `MapBox`류 · `StartButton`은 없다.
  - **진열 = 짐꾼 무대(10/23, 사용자)**: `ShopPanel`이 실행 중 `Stage`(RectMask2D, 내 국가 `NationShowcase.Backdrop` ×1 회색) · `Ground`(흰 4px)를 Back 바로 뒤에 만든다. `OfferCard`는 Glow · Floor · RankUp을 실행 중 `Carry`로 묶고 그 아래 `Porter`(기물 국가의 유닛 그림 ×6, 흰색 · 영주 색 없음 — `ShopPanel._porters` 공용 병사 · 킹덤 방패수 · 티크톡 톱니병사 · 트라이브 원시 졸병, 무기 뺌)를 세운다. 잠기면 층 조각을 땅에 내려놓고 옆에 선다. 걷기는 Carry · Porter만 옮기고 카드 판(누를 자리)은 고정.
- Battle 씬에서 달라진 것: 결과 버튼 = 「지도로」, `ShopPanel`·`InvadePanel`은 없다. 꺼진 `Templates/UpgradeCountTemplate`(체력바 아래 업그레이드 줄의 글자 틀)이 있다.
