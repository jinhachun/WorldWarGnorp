---
name: testResultReview
description: 가장 최근 플레이테스트 결과(Docs/testResult/YYYYMMDD_testResult.md)를 읽고 오늘의 할 일 목록(Docs/TodoList)을 쓴다. 사용자가 "/testResultReview"라고 부를 때. 작업이 하나 끝날 때마다 Docs/Record에 기록하는 규칙도 여기 있다.
---

# 테스트 결과 → 오늘의 할 일 → 기록 (사용자 결정 10/10)

사용자는 플레이한 날마다 `Docs/testResult/YYYYMMDD_testResult.md`를 쓴다. 이 스킬은 그걸 할 일 목록으로 바꾸고, 일이 끝날 때마다 기록을 남긴다. 총괄 세션이 한다(역할 없는 세션이면 그 세션이).

## 폴더 (원본은 여기 하나)

| 폴더 | 누가 쓰나 | 파일 |
|---|---|---|
| `Docs/testResult/` | **사용자만** — 고치지 않는다 | `YYYYMMDD_testResult.md` |
| `Docs/TodoList/` | 총괄 | `YYYYMMDD_TodoList.md` — 날짜는 **근거가 된 testResult의 날짜** |
| `Docs/Record/` | 총괄 | `YYYYMMDD_Record.md` — 날짜는 **일이 끝난 날** |

## `/testResultReview` 절차

1. `Docs/testResult/`에서 파일 이름 날짜가 가장 늦은 것을 읽는다. 같은 날짜의 TodoList가 이미 있으면 새로 만들지 말고 그 파일을 갱신한다(testResult에 줄이 늘었으면 항목만 추가).
2. 이전 TodoList에서 안 끝난 항목(`[ ]`)을 훑어, 새 testResult와 겹치면 합치고 아니면 「이월」로 옮겨 적는다.
3. testResult의 줄마다 할 일 하나로 쓴다 — **사용자 문장을 그대로 인용**하고 그 아래에:
   - 현상인지 목표인지(CLAUDE.md §1) · 지금 값을 재 봐야 하는 것은 "먼저 잴 것"
   - 담당(역할 표: `.claude/skills/role/SKILL.md`) · 손댈 파일 · 완료 조건(사용자 문장을 직접 재는 것)
   - **갈림길**(손맛·연출·범위) — 사용자에게 물을 질문. 묻기 전에 이력(HANDOFF·기획서·git log)에 답이 있는지 본다
4. 순서를 정한다: 버그 → 사용자가 "빠르게"라고 한 것 → 측정이 필요한 것 → 갈림길 답이 필요한 것. 에디터를 같이 쓰는 일은 겹치지 않게.
5. 사용자에게 목록을 한 화면으로 보고하고, 갈림길 질문을 한 번에 모아 묻는다. **답을 받기 전에는 갈림길 없는 항목만 착수한다.**

## TodoList 형식

```
# YYYY-MM-DD 할 일 (근거: Docs/testResult/YYYYMMDD_testResult.md)

## 1. <한 줄 제목>  [ ] 대기 | [~] 진행 | [x] 끝 | [?] 사용자 답 대기
> 사용자: "원문 그대로"
- 담당 · 손댈 곳:
- 완료 조건:
- 갈림길 / 답:
- 결과: (끝나면 Record 파일 이름과 한 줄 요약)

## 이월
```

## Record — 일이 하나 끝날 때마다

담당의 `[보고]`를 받아 **총괄이 직접 확인한 뒤**(캡처·수치) `Docs/Record/YYYYMMDD_Record.md`에 덧붙인다. 한 항목 = 한 일.

```
## HH:MM <제목> — 시각은 `date`로 확인한 값만 (TodoList YYYYMMDD #번호)
- 요청: "사용자 원문"
- 한 것:
- 확인: (무엇을 어떻게 쟀나 — 사용자 문장과 대조) / 못 본 것:
- 정한 것(사용자 확인 필요):
- 바꾼 파일:
```

- Record는 **끝난 일의 사실 기록**이다 — 규칙·함정은 CLAUDE.md/ARCHITECTURE로, 다음에 할 일은 TodoList·HANDOFF로. 같은 내용을 두 곳에 풀어 쓰지 않는다(HANDOFF에는 포인터만).
- Record를 쓴 뒤 TodoList의 그 항목을 `[x]`로 바꾸고 「결과」에 한 줄.
- testResult에 없던 일(대화 중 요청)도 끝나면 Record에 남긴다 — TodoList 번호 자리에 `대화 요청`.
