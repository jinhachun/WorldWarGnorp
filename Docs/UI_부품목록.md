# UI 부품 목록 (10/25)

> 자주 쓰는 UI 부품을 프리팹으로 모아 둔 곳: **`Assets/Prefabs/UI/Parts/UI_<부품>.prefab`**(변형은 `UI_<부품>_<변형>`).
> AI로 시안을 뽑을 때, 새 화면을 만들 때 **여기 있는 부품부터 쓴다**(UIUX.md 5단계 「이미 있는 컴포넌트를 먼저」).
> 그림 · 글자 규칙의 원본은 CLAUDE.md §5-1(그림 = Simple + Native Size) · §5-2(글자)다 — 여기 옮겨 적지 않는다.
> 크기 · 그림은 바뀔 수 있다 — **값의 원본은 프리팹**이다. 이 표는 "무엇이 있나 · 어디에 쓰나"를 찾는 지도다.

## 쓰는 법

- **코드가 실행 중에 만드는 부품**: 그 컴포넌트의 `[SerializeField]`(인스펙터 「부품 프리팹」 묶음)에 프리팹이 걸려 있고, 코드는 `Instantiate`한 뒤 자리 · 글 · 색만 채운다.
- **씬에 놓인 부품**: 씬 · ShopPopup 안의 사본은 이 프리팹의 **인스턴스**다. 사본마다 다른 값(자리 · 글 · 아이콘 · 색)은 덮어쓰기로 남아 있다.
  프리팹을 고치면 모든 인스턴스가 따라 바뀐다. **한 곳만 바꾸려면 인스턴스에서 고친다**(덮어쓰기).
- 🔴 **씬의 오브젝트를 프리팹 인스턴스로 바꿀 때(ConvertToPrefabInstance)**: 사본에 없고 프리팹에 있는 자식은 **사본에 새로 붙는다**.
  바탕은 가장 단순한 사본으로 잡고, 바꾼 뒤 참조 · 자식을 전후로 대조한다.
- 캡처(`Captures/Parts/UI_<부품>.png`)는 실제 게임 화면에서 그 부품 하나를 2배로 잘라낸 것이다. 화면 전체 캡처는 `Captures/Parts/after_s*.png`(닉네임 · 국가 · 지도 · 툴팁 · 땅 고름 · 상점 · 고르기 창 · 전투 · 결과 나/적)에 있다.

## 글 스타일

| 프리팹 | 폰트 · 크기 | 쓰는 곳 |
|---|---|---|
| `UI_Text_Dot12` | 도트(FusionPixel) 12 · 흰색 · 넘침 허용 · 레이캐스트 끔 | 업그레이드 칸의 「×N」 |
| `UI_Text_Dot24` | 도트 24 | 툴팁 칩 글 · 탭 글 · 딜 미터기 이름/숫자(숫자는 코드가 36/24로) |
| `UI_Text_Noto20` | 깔끔(NotoSansCJK) 20 | 전투 체력바 아래 국가 이름(`UI/BaseUpgradeRow`) |
| `UI_Text_Noto22` | 깔끔 22 | 툴팁 유닛 판의 스탯 줄(줄 간격은 판 본문을 따른다) |

- 도트 36 · 48 · 72와 깔끔 18은 아직 씬 안 Text를 본으로 복제한다(`_title` · `_header` · `bar` 등) — 프리팹이 아니다.

## 부품

| 프리팹 | 원본 크기 | 그림 · 글 | 상태 | 쓰는 곳 | 캡처 |
|---|---|---|---|---|---|
| `UI_UpgradeCell` | 36×36(+「×N」) | 그림 없는 판(등급 색) · 검은 속 32 · 아이콘 32 · `UI_Text_Dot12` | 평소 | 툴팁 「걸려 있는 업그레이드」 줄(`UI/UpgradeLines`) | `UI_UpgradeCell.png` |
| `UI_UpgradeCell_Hover` (변형) | 같음 | 같음 + 아이콘에 `UpgradeIcon` | 평소 · 올림(툴팁) | 위 줄 내 업그레이드 · 왼쪽 패널 영주 줄 · 전투 체력바 아래(`UI/UpgradeRow`) · 지도 특수 땅(`UI/WorldMapView`, 「×N」 끔) | `UI_UpgradeCell_Hover.png` |
| `UI_IconLabel` | 내용에 맞춤 | 아이콘 32 + 도트 글(HorizontalLayout · ContentSizeFitter) | — | 모든 버튼의 글 줄 | `UI_IconLabel.png` |
| `UI_CmdButton` | 220×80 | `UI_CmdButton` · Hover · Pressed · Disabled(Sprite Swap) + `UI_IconLabel` · ButtonHover(누름 4px) | 평소 · 올림 · 누름 · 꺼짐 | 침략 · 쉬기 · 상점 · 국가 「시작」 · 닉네임 「확인」 · 결과 「지도로」 | `UI_CmdButton.png` |
| `UI_ButtonDark` | 220×80 | `UI_ButtonDark`(색 틴트) + `UI_IconLabel` · ButtonHover(평소 0.65) | 평소 · 올림 · 누름 | 상점 리롤 · 잠금 · 층 올리기 · 판매 · 업그레이드 건너뛰기 · 전투 배속 | `UI_ButtonDark.png` |
| `UI_ButtonMain` | 300×96 | `UI_ButtonMain`(밝은 회색 채움, 검은 글 36) + `UI_IconLabel` | 평소 · 올림 · 누름 | 상점 「지도로」 · 「다음 턴」 | `UI_ButtonMain.png` |
| `UI_TowerSlot` | 120×120 | 벽 `UI_Floor*` · 창 불빛(Filled) · 기물 아이콘 80(×0.6) · 창틀(등급 색) · 글 「N기」 + SlotView · FloorLook · Button | 평소 · 올림 · 끌기 · 꺼짐(늪) · 비어 있음 | 지도 패널 탑 · 상점 필드/보관함 · 전투 아군/적 탑(가속 · 쿨 표시와 보관함의 Cap은 인스턴스에 붙어 있다) | `UI_TowerSlot.png` |
| `UI_TowerFloor` | 120×120 | 벽 · 창 불빛 · 창틀(FloorLook만, 누를 수 없음) | — | 국가 선택 탑 3층 · 끌기 고스트(아이콘 · Cap · CanvasGroup 붙음) | `UI_TowerFloor.png` |
| `UI_Roof` | 128×40 | `UI_Roof`(진영색) | — | 탑 5곳의 지붕 | `UI_Roof.png` |
| `UI_Heart` | 32×32 | `UI_IconHeart`(색은 코드 — 남은 목숨 · 빈 하트 · 위기 두근거림) | 찬 · 빈 · 위기 | 위 줄 목숨 · 결과 창(`UI/HeartRow`) | `UI_Heart.png` |
| `UI_Chip` | 글에 맞춤 × 32 | 검은 판 + Outline 2(등급/국가 색) · 아이콘(원본 크기) · `UI_Text_Dot24` | — | 툴팁 제목 아래 등급 · 국가 칩 | `UI_Chip.png` |
| `UI_StatLine` | 글에 맞춤 | 스탯 아이콘 32(스탯 색) + `UI_Text_Noto22` | — | 툴팁 유닛 판 체력 · 공격력 · 사거리 줄 | `UI_StatLine.png` |
| `UI_UnitCard` | 700×300(높이는 코드가 글에 맞춤) | 검은 판 + NationFrame 테두리 · 유닛 그림 칸 64 · 이름(도트) · 본문(깔끔 22) | — | 기물 툴팁의 유닛 판(World · Battle) | `UI_UnitCard.png` |
| `UI_LandCard` | 420 × 글에 맞춤 | 검은 판 + NationFrame · 그림 칸 64(영주 그림 ×4 / 이벤트 64 아이콘) · 이름(도트 36 + 한 줄 24) · 본문(깔끔 22) · 패배시 줄(하트 + 글) · 레이캐스트 안 막음 | — | 지도 땅 호버 카드(`UI/LandHover` — 주인 · 국가 · 땅 수 · 지형 + 효과 · 지역 특수 · 패배시) | (캡처 `Captures/LandCard/land_*.png`) |
| `UI_PowerBar` | 360×22 | `UI_PowerBar` 틀 + `UI_PowerBarFill`(Filled, 영주 색) | — | 전투 유닛 호버 카드 체력 막대 | (호버 때만 보임 — 캡처 없음) |
| `UI_PowerBarVersus` | 360×22 | 틀 + 적 채움 + 아군 채움(Filled) + 가운데 눈금 | — | 전투 위 가운데 유닛 수 비교 | `UI_PowerBarVersus.png` |
| `UI_Frame` | 판에 꽉 참 | 회색(#A6A6A6) 2px 단색 변 넷 + `UI_FrameKnot` 14 매듭 넷 | — | 결과 창 판(스타일 가이드 「판 선」) | `UI_Frame.png` |
| `UI_FrameDivider` | 판 너비 × 2 | 회색 가로선 + 양 끝 매듭 + 제목 뒤 · 탭 뒤 검은 끊김 칸 | — | 결과 창 「준 피해」 선 | `UI_FrameDivider.png` |
| `UI_Tab` | 88×40 | `UI_Tab` · Hover · On + `UI_Text_Dot24` + TabButton | 평소 · 올림 · 고름(흰 바탕 · 검은 글) | 결과 창 「나 / 적」 | `UI_Tab.png` |
| `UI_MeterRow` | 줄 높이 64/32 | 아이콘 64/32 · 이름 `UI_Text_Dot24` · `UI_MeterBar` 600×24(Filled) · 숫자 | 1위(영주 색) · 나머지(어둡게) · 적 이름 빨강 | 결과 창 딜 미터기(`UI/BattleSummary`) | `UI_MeterRow.png` |
| `UI_UpgradeCard` | 420×280 | 등급 색 판 + 검은 속 · NationFrame · 아이콘 32 · 이름(도트 36) · 효과(깔끔 22) | 평소 · 올림 · 누름 | 업그레이드 고르기 창 카드 3장 | `UI_UpgradeCard.png` |
| `UI_NationRow` | 254×104 | `UI_NationRow` · Hover · On + 엠블럼 32 · 이름(도트 36) · 한 줄(깔끔 16) | 평소 · 올림 · 고름 | 국가 선택 목록 | `UI_NationRow.png` |
| `UI_OfferCard` | 230×260 | 층 조각(창틀 이름 RarityFrame) · 빛 · 이름 · 가격 줄 · 잠김 · RANK UP + OfferCard(짐꾼은 실행 중에 생긴다) | 평소 · 못 삼(알파 0.4) · 잠김 · 팔림 · RANK UP | 상점 진열(ShopPopup 안 중첩) | `UI_OfferCard.png` |

## 후보 (아직 프리팹 아님)

- **영주 그림**(깃발 + 몸 + 검은 테두리 + 사진 — `UI/WorldLords.Figure`): 지도 영주 · 왼쪽 패널 초상화 · 쓰러진 영주.
- **지도 땅 표시**(검은 칸 36 + 지형 아이콘 + 특수 칸 + 왕관 — `UI/WorldMapView`)와 영주 머리 위 이름표(도트 12 + 검은 Outline 1).
- **기물 툴팁 판 통째**(`BuildingTooltip`, World · Battle에 같은 것이 한 벌씩).
- **유닛 호버 카드**(`UnitHover/Card`): `UI_UnitCard`와 구조가 다르다(아이콘 · 이름 · 체력 줄 + `UI_PowerBar` · 공격 줄).
- 위 줄 숫자 칸(아이콘 + 도트 36)은 아직 TopBar 글을 복제한다.
