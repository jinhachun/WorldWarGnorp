# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

> 🔴 **이 문서에는 *규칙*만 적는다.** "언제 이래서 이랬다"는 사례·함정은 적지 말 것 —
> 한 번 고치고 끝난 것이면 **코드 주석**과 git 커밋 메시지에 남는다. 문서를 부풀리는 건 늘 사례 쪽이다.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- **해석이 갈리는 걸 *인지한 순간*이 묻는 시점이다.** 다 만들어 놓고 끝에 단서를 붙이는 건 묻는 게 아니다.
  갈림길이 **손맛·연출**일 때 특히 그렇다 — 검증으로는 안 갈리고, 틀리면 방향이 **정반대**가 된다.
- **요구 목록의 한 줄이 *현상*인지 *목표*인지 갈라 읽을 것.** "~한 느낌"·"~하는 거임" 같은 서술이 위험하다.
  → **지금 값을 먼저 재 본다. 값과 해석이 어긋나면 틀린 건 해석이다.**
- **코드를 고치기 전에 "무엇을 어디에 손대는지"를 한 줄로 선언할 것.** **파일·화면·오브젝트 이름을 대야 틀린 게 드러난다.**
- **UI 코드를 고치기 전에 그 코드가 그리는 화면을 전부 열거할 것.** 열거하면 "어느 화면인가"가 저절로 질문이 된다.
- **"넣어줘"를 받으면 넣을 곳이 아니라 *이미 있는 곳*부터 `grep`으로 센다.** 있으면 **겹치는지·경합하는지**부터 보고할 것.
- **"A를 B로 바꿔줘"를 받으면 B를 짜기 전에 *A에 딸린 것*을 `grep`으로 전부 센다** — 호출부·전용 분기·프리팹 참조.
  죽는 게 있으면 **지울지 남길지 먼저 보고**한다.
- **사용자 아트를 반영할 땐 파일 수정 시각부터 확인할 것.** "내가 배선한 그림"과 "지금 디스크에 있는 그림"이 쉽게 어긋난다.
  **배선 직전에 "한 파일 = 한 그림인가"도 볼 것** — 임포트 기본값이 Multiple이라 자동 슬라이스가 그림을 조각낸다.
- **파일명 접미사(`R1`/`_2` 등)의 뜻은 파일 종류마다 다르다** — 그림을 열어보는 것으로는 안 갈린다. 한 번 물을 것.
- 선택지를 낼 땐 **손잡이 이름(필드명)이 아니라 화면에서 벌어지는 일로** 먼저 한 줄 설명할 것.
- **선택지 한쪽이 "지금 구조를 유지한다"면 그 항목에 *유지되면 남는 불편*을 적을 것.**
- **사용자가 평가 축을 지정해 주면, 재기 전에 "이 축들로 안 잡히는 게 있나"를 한 번 본다.** 모자란 축을 찾으면 덧붙여 재고, 늘렸다고 말한다.
- **목표를 비율(%)로 받으면, 구현 전에 그 %가 가리킬 수 있는 축을 전부 재서 나란히 놓는다**(개수·비용·거리·시간 등).
- **"정할까요?"를 쓰기 전에 그 결정이 이미 내려져 있는지 이력을 볼 것.** 이 문서·`HANDOFF.md`·`git log`가 질문보다 싸다.
- **한 값이 결과 둘을 동시에 움직이면, 고치기 전에 두 결과의 수식을 세운다.**
  트리거 자명 — *같은 파라미터를 고쳤는데 사용자가 **반대 방향 불만**을 말하는 순간.*
- **"구조적 한계/불가능"을 말하기 전에 둘을 본다.** ① 전수 조사를 한 번 한다. ② 그 "~라서"가 **사용자가 준 조건인지 내가 얹은 조건인지** 갈라 본다.
  **손으로 한 계산으로 가능성을 닫지 말 것** — 실측 한 번이 더 싸다.
- **병목을 코드로 지목하기 전에 *이미 쌓인 로그·측정 데이터*에 그 축이 있는지 먼저 본다.**
  코드 정독은 "한 번에 비싼 것"을 찾고, 로그는 "실제로 자주 도는 것"을 찾는다.
- **진단용 출력이 서로 다른 값을 같은 문자열로 뭉개지 않는지 볼 것.** 폭이 걱정되면 값을 줄이지 말고 **표본 수**를 줄인다.
- If a simpler approach exists, say so. Push back when warranted.
- **대안으로 도구·기능을 꺼낼 땐 꺼내는 그 자리에서 그 문서를 연다.**
- **절차를 제시하기 전에 *1번 단계가 그 상황에서 실행 가능한지* 본다.**
- If something is unclear, stop. Name what's confusing. Ask.
- 🔴 **정정하는 순간이 두 번째 오류의 자리다.** "아니다 / 틀렸다 / 정정합니다"를 쓰려는 순간, **정정 내용을 원 주장과 같은 강도로 재고 나서** 말한다.
- **외부 도구(MCP·API)에 한글을 넘길 땐 유니코드 이스케이프를 손으로 만들지 말고 한글을 그대로 쓸 것.**
- **메모리·문서에는 규칙만 적고, 변하는 것(현재 값·구현 상태·목록)은 "어디서 확인하는지"만 적을 것.**

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

🔴 **"검증 완료"를 보고하기 전에, 세운 항목들 옆에 *사용자가 말한 그 문장*을 놓고 대조한다.**
**요청 문장을 직접 재는 항목이 하나라도 있나?** 없으면 아직 검증 안 한 것이다.

🔴 **자동으로 반복되는 것을 만들면 *종료 조건*을 먼저 쓴다.** 기억도 판단도 필요 없는 상한을 하나 넣는다(시각·횟수).

---

## 5. 개발 환경 & 패키지

- **Unity 6** (6000.3.16f1), URP 17.3 — **2D 사이드뷰**. (템플릿이 3D라 2D Renderer 전환이 남아 있다 → `ROADMAP.md`)
- **입력**: Input System (`Assets/InputSystem_Actions.inputactions`). 구 Input Manager(`Input.GetKey` 등) 쓰지 말 것.
- **UI 시스템**: uGUI (미정 시 기본값. 필요 시 UI Toolkit으로 전환하고 여기 갱신)
- **테스트**: Unity Test Framework
- **서드파티 플러그인**:
  - **Unity MCP** (CoplayDev, `com.coplaydev.unity-mcp` v10) — Claude Code가 Unity 에디터를 직접 조작하기 위한 MCP 브릿지. §6 참고.

### 🎨 픽셀 크기 컨벤션
**PPU 8 — 유닛 그림 8×8px = 월드 1유닛 = 유닛 키 1.** 무기·아이콘도 같은 PPU로 원본 크기 그대로 쓴다.
- ⚠️ 새 PNG는 임포트 기본값(PPU 100 · Bilinear · 압축 · **NPOT 크기 보정**)으로 들어온다. 크기 보정 때문에 5×3 같은 그림이 **4×4로 늘어난 채 들어온다.**
  → Sprite · Single · PPU 8 · Point · 압축 없음 · `npotScale = None` · 밉맵 끔으로 바꾼다. 동종 스프라이트의 `.meta`와 대조할 것.
- 그림은 **오른쪽(아군 전방)을 보고** 그린다. 적은 `Unit`이 진영에서 방향을 읽어 좌우를 뒤집는다.
- 유닛 색은 흰 그림에 `SpriteRenderer.color`로 입힌다(아군 파랑 · 적 빨강).

### 코드 컨벤션
- 네임스페이스 `GnorpWar`. 클래스/메서드/프로퍼티 PascalCase, private 필드 `_camelCase`, 인스펙터 노출은 `[SerializeField] private`.
- **새 스크립트는 `Assets/Scripts/` 아래 역할 폴더에**(Core · Units · Bases · Battle · UI · Fx · Dev — 책임은 ARCHITECTURE.md 「폴더 책임」). 루트에 두지 않는다. 폴더가 달라도 네임스페이스는 `GnorpWar` 하나.
- 스크립트를 옮길 땐 `.cs`와 `.meta`를 **짝으로** `git mv` — GUID가 바뀌면 씬·프리팹의 스크립트 참조가 끊긴다.
- **수치 데이터(체력·공격력·비용·쿨다운 등)는 코드가 아니라 ScriptableObject 에셋에.**
- **아군/적군은 같은 유닛 코드를 쓴다.** 진영(Team) 값과 진행 방향으로만 구분하고, 진영 전용 분기를 만들지 않는다.

---

## 5-1. 🔴 UI 이미지는 **무조건 Simple + Set Native Size** (사용자 결정, 예외 없음)

**칸 크기를 정해두고 거기 그림을 맞추지 말 것. 스프라이트 원본 크기가 칸 크기를 정하고, 텍스트·레이아웃을 그 안쪽에 맞춘다.**

- `Image Type` = **`Simple`**, 크기는 **`Set Native Size`**(코드로는 `img.SetNativeSize()`)로 원본 픽셀 그대로.
- 🚫 **`Sliced`(9-slice)를 쓰지 않는다.**
- **`Preserve Aspect`는 켠다.** `localScale`은 가급적 쓰지 않는다(쓰면 그 Image는 무조건 `Simple + Preserve Aspect`).
- **칸에 맞는 원본 크기 그림이 없으면 늘려 쓰지 말고 그 크기의 그림을 요청**한다.
- **게이지 채움 층만 예외 — `Image.type`이 반드시 `Filled`**여야 한다. `Simple`이면 `fillAmount`가 통째로 무시된다(경고도 없다).

**검증 — 값이 아니라 캡처로 한다.** rect 값은 그림자 두께·투명 여백·눌린 비율·실제 색을 못 잡는다.
한 화면 고칠 때마다 캡처해서 눈으로 볼 것.

---

## 6. Unity MCP — Claude가 씬을 직접 조작함

**이 프로젝트는 Claude Code가 Unity 에디터에 직접 연결된다.** `unityMCP` 서버(로컬, `http://localhost:8080/mcp`)를 통해
GameObject 생성·컴포넌트 부착/수정·프리팹 생성·씬 저장·플레이모드 진입까지 Claude가 직접 한다.
**"에디터 작업은 사용자가, 코드는 Claude가"가 아니다** — 오브젝트 배치·참조 연결도 Claude가 MCP로 처리한다.

- 연결 설정: `.mcp.json` (프로젝트 루트). **Unity 에디터가 열려 있고 에디터 쪽 MCP 서버가 켜져 있어야** 연결된다.
- 새 세션에서 MCP 툴이 안 보이면: Claude Code 재시작해야 `.mcp.json` 변경이 반영된다.
- 🔴 **사용자가 에디터에서 저장 안 한 편집이 있을 수 있다.** 씬을 다시 열거나 전환하기 전에 현재 씬의 dirty 여부부터 본다.
- 🔴 **모달 창을 띄우는 에디터 API(`EditorUtility.DisplayDialog` 등)를 부르지 말 것** — Unity가 멈추고 MCP가 끊긴다.

---

## 7. 세션 시작 루틴

**세션 첫 턴에 아래를 읽기 전용으로 돌리고 브리핑한다** (사용자가 안 시켜도):
1. `HANDOFF.md` 읽기 — North Star · 현재 상태 · 미해결 · ❓확인 대기
2. **코드 대조** — HANDOFF가 말하는 현황을 코드·에셋으로 1건 이상 확인 (문서는 낡는다)
3. `ROADMAP.md`의 다음 할 일 확인
4. 브리핑: 지금 상태 · 이번 세션 제안 작업 · 확인 대기 항목

- 씬 구조는 `SCENE_MAP.md`, 코드 구조는 `ARCHITECTURE.md`, 코딩 전 전제는 `SESSION_ZERO.md`.

### 세션 내내 유효한 규칙

- `GDD.md`는 `grep`으로 필요한 섹션만 조각내어 읽을 것 (`cat GDD.md` 금지)
- **밸런스 수치는 문서가 아니라 에셋이 정답** (유닛·스테이지 ScriptableObject)
- ⚠️ **"코드에 스위치가 있다" ≠ "게임에 그 기능이 있다".** 에셋이 켜야 도는 것은 **그 에셋을 `grep`으로 한 번** 확인한다.
- 🔴 **거꾸로도 틀린다 — enum·에셋에 남아 있다고 게임에 있는 게 아니다. *진입점 배열*에서 센다.**
- 🔴 **"아무도 안 부른다"를 말하기 전에 *에디터 전용 호출부*(`Assets/Editor/`)를 따로 센다.**
- 🔴 **사용자가 쓴 문서와 구현이 어긋나 보이면, 고치기 전에 "내가 틀렸을 경우"를 먼저 친다.** 어긋남의 기본값은 내 오독이다.
- 🔴 **문서에 적힌 건 전부 낡는다 — 인용하기 전에 코드·에셋으로 1건 확인한다.** (주장·`[x]` 체크·`파일:라인` 포인터·해법·예시 모두)
- 🔴 **"아직 안 돼 있다"·"비어 있다"를 말하기 전에 그 에셋·파일을 한 번 연다.**
- 🔴 **설정 파일에 "왜 이렇게 했는지" 주석이 달려 있으면 그건 결정문이다 — 다시 꺼내지 말 것.**
- ⚠️ **같은 사실의 사본을 두 곳에 두지 말 것.** 원본 한 곳만 두고 나머지는 포인터만 남긴다.

---

## 7-1. 문서의 "이건 못 한다"는 인용하기 전에 한 번 해 볼 것

불가능 주장은 **시도 자체를 없애서 틀려도 영원히 안 드러난다.** 한 번 시도해 보고 말한다.

---

## 8. grep 사용

**코드 파일 (.cs) 수정 시:**
- 수정 전 `grep`으로 관련 클래스/함수의 정확한 위치(라인) 먼저 파악
- 불필요한 전체 파일 cat 금지, 해당 라인만 Read

## 8-1. 스크립트 파일은 헤어독으로 쓰지 말 것

🔴 **`.js`·`.ps1` 같은 스크립트를 `cat > file <<EOF` 로 만들면 백슬래시가 먹혀 정규식이 조용히 무력화된다.** → **Write 툴로 쓴다.**
- 인라인 `node -e "…"`·`powershell -Command "…"` 인자도 같다 — 정규식·`\uXXXX`·경로가 들면 파일로 쓰고 실행한다.
- 한 `sed` 호출에 삭제(`d`)와 삽입/치환(`i`/`c`)을 섞지 말 것 — 라인 번호가 밀린다. 치환·삽입은 Edit 툴로.
- 검산 스크립트라면 **입력에 검사 대상이 실제로 들어갔는지 개수를 같이 센다.**

---

## 9. 세션 종료

사용자가 마무리를 요청하면:
1. 이번 세션에서 한 일 정리
2. `ROADMAP.md` 체크박스 갱신
3. `HANDOFF.md` 갱신 — **추가 + 삭제 둘 다**(완료·검증된 것은 지운다)
4. 커밋 — **사용자가 시킬 때만.** Claude가 임의로 커밋하지 않는다.

## 10. 프로젝트 종료 루틴

플레이 빌드 배포 후 또는 프로토타입 중단 결정 후 `RETROSPECTIVE.md` 작성.
— **철저히 사용자 중심으로.** "구현된 피쳐"·"기술 스택" 같은 사실 나열만 AI가 채우고, 나머지(What Went Well/Wrong·과정 개선안·게임 개선방안)는
플레이테스트 여부/결과부터 묻고 섹션별로 사용자 입장을 질문해서 그 답변으로 채운다.
— What Went Well/Wrong은 **워크플로 수준**으로. 코드 버그 목록 아님.
