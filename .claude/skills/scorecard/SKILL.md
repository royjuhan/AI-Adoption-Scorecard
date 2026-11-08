---
name: scorecard
description: 사용자의 업무를 단계로 나누고 단계마다 담당(①~④, ②M)을 판단해 스코어카드를 만들 때. /scorecard 입구
---
# /scorecard

사용자의 업무를 질문으로 단계화하고, 단계마다 담당을 판단한다. 엔진(`scorecard` 명령)이 규칙 의견을 같이 적고, 검증 숫자와 판정을 낸다.
근거: ADR 0010 A(AI 분류 + 규칙 검증), ADR 0013(classification.json schema 2), `docs/spec/classification.md` 4.13, `docs/spec/case-schema.md` 4.4·4.13·4.14, `docs/spec/json-output.md`.

## 지킬 것

- 숫자를 스킬이 셈하지 않는다. 정확도·누락률·시간·판정·행 수는 명령의 JSON(`classification.json`, `metrics.json`, `judgment.json`)에 있는 값만 옮긴다. 엔진 숫자·판정을 고치거나 다시 셈하지 않는다.
- 엔진은 묻지 않는다. 묻는 것은 이 스킬 대화뿐이다. 명령은 파일 입력과 옵션만 받는다.
- 사용자 확인(맞음/바꾸기) 전에는 `answers.yaml`을 쓰지 않고 명령을 부르지 않는다.
- 어떤 자료를 AI에 보낼지는 사용자가 정한다. 처음에 아래 안내를 그대로 보여 준다.

> AI에 보내면 안 되는 자료는 넣지 마세요. 구조만 설명해도 분류는 됩니다.

- 케이스 폴더는 `cases/<케이스 이름>/`이다. `cases/`는 커밋하지 않는다. 케이스 이름은 소문자·숫자·`-`만 쓴다(64자까지).
- 입력 파일은 읽기만 한다. 결과는 출력 폴더(기본 `<케이스>/out`)에만 쓴다.

## 1. 업무 설명 받기

1. 사용자에게 업무를 자유롭게 적어 달라고 한다. 무엇을 받아서, 무엇을 하고, 결과를 어디로 보내는지.
2. 받은 글을 고치지 않고 `cases/<케이스>/intake.md`에 그대로 저장한다.
3. 모르는 것이 있으면 한 번에 몰아서 짧게 묻는다. 답이 없어도 다음으로 간다.

## 2. 단계 나누기와 판단

1. 업무를 단계로 나눈다. 단계 이름은 사용자의 말로 짧게 쓴다. 케이스 안에서 같은 이름을 두 번 쓰지 않는다.
2. 단계마다 담당을 판단하고 근거를 한두 문장으로 쓴다(`ai: {assignee, reason}`).
   - ① 기존 기능·설정으로 된다.
   - ② 규칙 기반 소프트웨어. AI로 만들고 실행 때 AI가 없다.
   - ②M 학습 모델(결정론). 미리 학습해 고정한 모델을 규칙 SW처럼 돌린다. 실행 때 AI를 부르지 않고 같은 입력이면 같은 결과다. ②의 한 갈래다(`classification.md` 4.13).
   - ③ 실행 시 AI 보조. 사람이 확인한다. 자동 승인은 하지 않는다.
   - ④ 사람이 한다.
3. 단계마다 질문 10개 체크리스트를 채운다(`answers`). 키와 선택지는 `case-schema.md` 4.4 표다: `input_kind`, `rule_expressible`, `frequency`, `batch_size`, `exception_rate`, `current_actor`, `minutes_per_item`, `failure_impact`, `existing_feature`, `human_signoff`.
   - 모르면 그 키를 비워 둔다(적지 않는다). 엔진이 기본값을 넣고 "기본값 사용, 확인 필요"를 붙인다.
   - 설명에서 AI가 짐작한 답은 그 단계의 `inferred` 목록에 키를 적는다. 짐작한 답은 `answers`에 값이 있어야 한다.
   - `minutes_per_item`은 재지 않았으면 비운다. 측정 권고가 붙는다.
4. 체크리스트는 판단을 돕는 참고다. 담당은 AI가 정한다. 엔진은 같은 답으로 규칙 트리를 돌려 "규칙 의견"을 따로 낸다.

## 3. 서명 단계 묻기

1. "이 업무에서 사람이 최종 확인·서명하는 단계가 어디인가요?"를 한 번만 묻는다. 없으면 없다고 받는다.
2. 그 단계는 `human_signoff: yes`, 나머지 모든 단계는 `human_signoff: no`로 적는다. 단계마다 따로 묻지 않는다(ADR 0003).

## 4. 사용자 확인

1. 단계마다 보여 준다: 단계 이름, AI 담당과 근거, 채운 답, 짐작한 답(`inferred`), 비운 답.
2. 단계마다 사용자가 고른다.
   - 맞음: 그대로 둔다.
   - 바꾸기: 새 담당(또는 답)과 이유 한 줄을 받는다. 이유는 근거(`reason`)에 반영한다.
3. 모든 단계가 확인된 뒤에만 다음으로 간다.

## 5. answers.yaml 쓰기와 start

1. `cases/<케이스>/answers.yaml`을 쓴다. 모양은 `case.yaml`의 부분(`name`, `task`, `steps`)이다. 단계 항목은 `name`, `answers`, `inferred`(있을 때), `ai`다.

```yaml
name: <케이스 이름>
task: <업무 한 줄>
steps:
  - name: <단계 이름>
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 20, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
    inferred: [frequency]
    ai:
      assignee: ②
      reason: <확인된 근거>
```

2. 분류를 부른다. 저장소 안에서는 `dotnet run --project src/Scorecard.Cli --`가 `scorecard`다.

```text
scorecard start cases/<케이스> --answers cases/<케이스>/answers.yaml --json
```

3. 종료 코드가 2면 오류 JSON의 `errors`(`source`, `line`, `column`, `path`, `message`)를 사용자에게 그대로 보여 주고, 고친 뒤 다시 부른다. 1이면 내부 오류로 알린다.
4. 성공(0)이면 결과 객체의 `files`에 있는 `classification.json`을 출력 폴더에서 읽는다.

## 6. 결과 보여 주기

1. 단계마다 AI 판단(`rows[0]`의 담당·근거)과 규칙 의견(`rule_opinion`의 담당·근거 키)을 나란히 보여 준다.
2. `differs`가 true인 단계는 "AI 판단과 규칙 의견이 다름"으로 표시한다. 어느 쪽이 맞는지 사용자와 다시 볼 수 있다. 바꾸면 4절로 돌아가 `answers.yaml`을 다시 쓴다.
3. 부가 행(④ 예외 검토 `PARTIAL`, ④ 사람 게이트 `GATE`)은 엔진이 붙인 대로 보여 준다. 빼지 않는다.
4. 기본값 표시·추정 표시, `checks`(측정 권고), `ai_overuse_candidate`는 파일에 적힌 대로 옮긴다.

## 7. 검증 안내 (check)

1. AI를 적용한 단계의 결과를 검증하려면 자료 두 벌이 필요하다고 안내한다.
   - `cases/<케이스>/reference/`: 사람이 만든 결과(기준).
   - `cases/<케이스>/candidate/`: AI 적용 후 결과.
2. 자료는 사용자가 직접 넣는다. 스킬은 자료를 만들거나 고치지 않는다.
3. `cases/<케이스>/case.yaml`에 `inputs`(`reference`, `candidate`, `key`), `compare`, `thresholds`를 적는다. 모양은 `case-schema.md` 4.5다. 단계는 `answers.yaml`과 같게 둔다.
4. 대조와 판정을 부른다.

```text
scorecard check cases/<케이스> --json
```

5. 출력 폴더의 `metrics.json`·`judgment.json`에 있는 숫자와 판정을 그대로 보여 준다. 자료가 없으면 판정은 "미검증"이고, 무엇을 재면 되는지도 파일에 있다.
