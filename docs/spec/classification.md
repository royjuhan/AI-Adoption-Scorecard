# classification (T-004 스펙)

- 상태: 승인됨(2026-11-07, 15문항 모두 제안대로). 7절 예제 초안도 제안대로 확정. 승인 결과는 문서 끝.
- T-008 개정(승인됨, 2026-11-07. 승인 결과는 `docs/spec/human-signoff.md` 끝): 트리 맨 앞 `SIGNOFF`(ADR 0003)와 추정 표시(ADR 0004 결정 2). 바뀐 곳은 문서 끝 "T-008 개정"에 모았다. 질문과 스파이크 결과는 `docs/spec/human-signoff.md`. `expected/classification.json` 3개는 바이트 그대로다 [T8:S3].
- T-009 개정(초안, 사람 승인 전, 2026-11-07): 4번째 공개 예제 `signoff-final`(ADR 0003 남은 질문 2). 7.1 항목 8·요약 표 1행, 6.3·6.4 글머리, 새 7.5. 규칙(4절)과 기존 블록은 그대로다. 문서 끝 "T-009 개정". 근거·질문·스파이크 결과는 `docs/spec/signoff-final.md`. `expected/classification.json` 3개는 바이트 그대로다 [T9:S4].
- 작업 카드: `docs/tasks/T-004-classification.md`
- 작성: prototyper, 2026-11-07
- 확인 대상 커밋: `cbbfdb6`
- 확인 환경: Ubuntu 24.04 x64, .NET SDK 10.0.114, YamlDotNet 18.1.0, xUnit 2.9.3
- 스파이크: `spikes/T-004/` (머지 대상 아님. 머지 전에 빠진다). 실행은 `spikes/T-004/run-spike.sh <작업폴더> <로그폴더>`. 요약 원문은 `spikes/T-004/summary.txt`. 이 문서의 문장·JSON은 스파이크 후보(`spikes/T-004/core/Classification/`)가 실제로 낸 것이다.

## 0. 표기

- `[읽음]`: 커밋된 파일을 읽어서 확인한 사실.
- `[S0]`~`[S8]`: 스파이크 단계(10절). `[P:무리 이름]`: S4·S8 `PROBE` 줄.
- **미확인**: 실험하지 않았거나 이 환경에서 확인할 수 없는 것.
- 담당: ① 기존 기능·설정, ② 규칙 기반 소프트웨어, ③ 실행 시 AI 보조, ④ 사람.
- 행 모델: **A** 예외 검토·사람 게이트 모두 부가 행, **B** 게이트만 부가 행, **C** 둘 다 단계 속성(4.3).
- 질문 9개의 키·선택지·기본값은 `docs/spec/case-schema.md` 4.4 표에만 있다(T-003 승인 결정 5). 이 문서는 그 표를 가리킨다. 다시 적지 않는다. T-008 뒤에는 10번째 답 `human_signoff`까지 같은 표에 있다.
- `[T8:S0]`~`[T8:S8]`, `[T8:P:무리]`: T-008 스파이크(`spikes/T-008/`) 단계와 S4 `PROBE <무리>` 줄. 단계 목록은 `human-signoff.md` 10절.
- G1·G2·G3는 `docs/spec/guard-tests.md`, Y1은 `docs/spec/case-schema.md` 4.2의 이름이다.
- `[T9:S0]`~`[T9:S8]`: T-009 스파이크(`spikes/T-009/`) 단계. 단계 목록은 `signoff-final.md` 10절.

## 1. 범위

카드 수용 기준 1~3과 카드 메모 6건을 다룬다. 수용 기준 4(CI 녹색)는 PR에서 확인한다.

| 카드 메모 | 이 문서 | 승인 질문 |
| --- | --- | --- |
| 질문 9개·기본값 표는 case-schema.md 4.4에만. 이 문서는 가리킨다 | 0절, 2절 | — |
| 파서는 `CaseParser`, 기본값은 `Answer<T>.IsDefault` | 2절, 4.6 | 6 |
| 분류 숫자(`batch_size < 5`)를 코드에 박지 않는다. 어디서 읽을지 | 4.9 | 9 |
| 근거 문장도 Core에서 | 4.5, 4.11 | 5, 15 |
| 예제 3종 `answers.yaml`·`expected/classification.json`을 이 카드에서 만들지, 어떻게 확정할지 | 7절 | 13, 14 |
| T-003에서 넘어온 항목은 범위 밖 | 아래 | — |

범위 밖:

- `scorecard classify`·`interview --answers` 명령, 결과 파일을 어디에 쓸지. M2(cli-dev).
- 확인 항목·측정 권고의 화면·평가서 문구. 평가서 카드(`report.md`).
- 대조·지표·판정(정확도, 통과·조건부). 판정 카드(`metrics.md`). AI 과잉 후보만 예외(4.8, 승인 질문 8).
- 입력 SHA-256 기록. 평가서 카드(지시문 2절 2는 "평가서에 기록"). classification.json에는 넣지 않는다.
- 12.1 비교에서 나온 트리 변경. ADR로 따로 묻는다(6.3, 승인 질문 4).
- T-003에서 넘어온 항목(오류 상한·제어 문자·테스트 보강).

## 2. 입력

- `CaseDefinition` 하나(`CaseParser` 결과). 분류기는 파일을 읽지 않는다. 파싱 오류가 있으면 분류하지 않는다(5절).
- 답 9개는 `StepAnswers`. 값은 YAML 문자열 그대로(`"fixed_form"`), 숫자는 `int`·`decimal`. 기본값이 들어간 답은 `IsDefault = true` [읽음 `CaseModel.cs`].
- T-008: 답은 10개다(`human_signoff`). 단계의 `inferred` 목록에 있는 답은 `IsInferred = true`다(case-schema 4.13). 공개 예제 `answers.yaml`의 단계 항목은 `name`, `answers`뿐이다(`inferred`는 예제에 쓰지 않는다. ADR 0004 결정 2).
- 공개 예제의 `answers.yaml`은 `case.yaml`과 같은 스키마의 부분이다: `name`, `task`, `steps`(각 `name`, `answers`). 다른 최상위 키는 쓰지 않는다. `CaseParser.Parse(bytes, "answers.yaml")`로 읽힌다. 초안 3종이 오류 0건으로 읽혔다 [S3]. → 승인 질문 13.
- `answers.yaml`의 LF·CRLF·CR·BOM 변형은 같은 classification.json 바이트를 낸다 [P:eol][S7].

## 3. 출력

### 3.1 메모리 결과

`ClassificationResult` 하나(4.11). 단계(사용자가 준 단계)마다 `StepClassification` 하나, 그 안에 행 1~3개.

### 3.2 classification.json

Core가 바이트를 만든다(`ClassificationJson.Render`, 4.11). 모양:

```json
{
  "schema_version": 1,
  "case": "<케이스 이름>",
  "steps": [
    {
      "number": 1,
      "name": "<단계 이름, 자유 서술 그대로>",
      "defaults": ["<IsDefault인 답의 키, case-schema 4.4 표 순서>"],
      "checks": ["<확인 항목 키, 4.7>"],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step | exception_review | human_gate",
          "assignee": "① | ② | ③ | ④",
          "rule": "SIGNOFF | Q1 | Q2 | Q3 | Q4 | ELSE | PARTIAL | GATE",
          "reason": "<근거 문장, 4.5. 기본값 표시 포함>"
        }
      ]
    }
  ]
}
```

- 위 블록은 모양 설명이다. 실제 바이트는 7절 블록처럼 배열 원소도 한 줄에 하나다.
- 속성 순서는 위와 같다. 단계 순서는 `case.yaml` 순서, 행 순서는 4.3.
- `rule`이 근거 키다(지시문 5절 "분류·근거 키").
- 트리가 읽은 답(`Used`, 4.6)은 쓰지 않는다. 근거 키로 정해지기 때문이다. 메모리 결과에는 있다.
- 추정 답 키 목록(`Inferred`, 4.12)도 쓰지 않는다(T-008, ADR 0004 질문 2). 메모리 결과와 스코어카드 결과 객체에만 둔다. 추정 표시는 `reason` 문장 끝에만 나타난다 [T8:P:json].
- 경로를 쓰지 않는다. 문자열은 케이스 이름, 단계 이름, 고정 값, 근거 문장뿐이다.
- 단계 수(지시문 12절 "단계 수")는 모든 단계의 `rows` 원소 수 합이다(행 모델 A, 승인 질문 1).

### 3.3 바이트 규칙

| 항목 | 규칙 | 근거 |
| --- | --- | --- |
| 쓰는 도구 | `System.Text.Json.Utf8JsonWriter`. 리플렉션 직렬화(`JsonSerializer`) 없음 | 후보 코드 |
| 들여쓰기 | 2칸(`Indented = true`) | [S3] |
| 줄바꿈 | `\n`(`JsonWriterOptions.NewLine = "\n"`). CR 0개 | [S3 json-check] |
| 끝 | 마지막 `}` 뒤에 `\n` 하나 | [S3 json-check] |
| 인코딩 | UTF-8, BOM 없음 | [S3 json-check], [P:escape] |
| 이스케이프 | `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` | 아래 표 |

이스케이프 결과 [P:escape]:

| 단계 이름에 든 글자 | JSON 안 모양 |
| --- | --- |
| ①②③④, 한글 | 그대로 |
| `<>&'+` 와 백틱 | 그대로 |
| 큰따옴표, 역슬래시 | `\"`, `\\` |
| 탭 | `\t` |
| U+0001, U+007F, U+2028 | `\u0001`, `\u007F`, `\u2028` |
| 이모지(U+1F600) | `\uD83D\uDE00` (서로게이트 쌍) |
| NFD 한글(U+1100 U+1161) | 그대로(정규화 안 함) |

- 같은 입력이면 같은 바이트다. 확인: 프로세스 두 번, 다른 경로 사본, `LANG=de_DE.UTF-8` 프로세스 [S3 det-cmp], 문화권 6종(Invariant, de-DE, tr-TR, ar-SA, ko-KR, fr-CH) [P:culture].
- 숫자는 첫 키 `schema_version`(정수 1, T-007·ADR 0004)과 근거 문장 안의 `batch_size`뿐이다. `InvariantCulture`로 쓴다. 건수 1234567을 넣은 결과도 문화권 6종에서 SHA-256이 같았다 [P:culture].
- → 승인 질문 10.

## 4. 규칙

### 4.1 결정 트리 (지시문 3.3과 해석)

원문 [읽음 지시문 3.3]:

```
Q1 existing_feature == yes            → ① 기존 기능·설정으로 해결
Q2 rule_expressible == no             → ③ 실행 시 AI 보조 + 사람 확인 (자동 승인 금지)
Q3 input_kind in {free_doc, image, conversation}
                                      → "입력 양식 표준화 먼저 검토, 안 되면 ③" (해석만 AI, 계산·저장은 코드)
Q4 frequency == rare and batch_size < 5
                                      → "그대로 두거나 일회성 AI 보조" (개발비가 더 큼)
그 외                                  → ② 규칙 기반 소프트웨어 (AI로 만들고 실행 시 AI 없음)
  rule_expressible == partial 이면    → ② + 예외 건은 사람 검토 단계 추가
항상: failure_impact in {external, safety_legal} 이면 → 해당 단계 뒤에 ④ 사람 게이트 추가
```

- T-008: 위 블록은 T-004 때 읽은 원문이다. ADR 0003 결정으로 맨 앞에 `SIGNOFF` 줄이 생기고 "항상" 줄에 `SIGNOFF` 예외가 생긴다. 지시문은 리드가 `human-signoff.md` 6절 문구로 고쳤다.

해석(제안):

| 순서 | 근거 키 | 결과 담당 | 행 종류 | 해석 |
| --- | --- | --- | --- | --- |
| 0 | `SIGNOFF` | ④ | `step` | T-008(ADR 0003). `human_signoff == yes`면 맨 앞에서 멈춘다. 부가 행(`PARTIAL`·`GATE`)을 붙이지 않는다 [T8:P:signoff] |
| 1 | `Q1` | ① | `step` | 원문 그대로 |
| 2 | `Q2` | ③ | `step` | "사람 확인"은 ③의 뜻에 든다. 행을 더하지 않는다. 자동 승인 금지는 문장에 적는다 |
| 3 | `Q3` | ③ | `step` | "입력 양식 표준화 먼저 검토"는 문장에 적는다. 따로 필드를 두지 않는다. 지시문 12절 "③(입력 양식 표준화 제안)"과 같다 → 승인 질문 2 |
| 4 | `Q4` | ④ | `step` | "그대로 두거나 일회성 AI 보조"는 ①~④ 어디에도 딱 맞지 않는다. ④(사람이 계속 한다, 필요하면 일회성 AI)로 두고 문장에 적는다 → 승인 질문 3 |
| 5 | `ELSE` | ② | `step` | 원문 그대로 |
| 5 뒤 | `PARTIAL` | ④ | `exception_review` | `ELSE`일 때만. Q3·Q4에서 멈추면 붙지 않는다(원문 들여쓰기) [P:order Q3 re=partial, Q4 re=partial] |
| 항상 | `GATE` | ④ | `human_gate` | 모든 잎 뒤에 붙는다(Q1~Q4, ELSE) [P:order Q1+GATE … Q4+GATE]. T-008: `SIGNOFF` 뒤에는 붙지 않는다(`failure_impact`가 `external`·`safety_legal`이어도) [T8:P:signoff] |

- 처음 걸리는 곳에서 멈춘다. 순서 확인 [P:order]: `existing_feature: yes` + `rule_expressible: no` → Q1. `no` + `free_doc` → Q2(표준화 제안 없음). `image` + `rare`·1건 → Q3.
- T-008: `human_signoff: yes`는 다른 답과 상관없이 `SIGNOFF`다. `existing_feature: yes`, `rule_expressible: no`·`partial`, `input_kind: image`, `rare`·1건과 같이 있어도 `SIGNOFF` 한 행이다 [T8:P:signoff].
- `existing_feature: unknown`은 Q1에 걸리지 않는다. 아래로 내려가 분류하고 확인 항목을 단다(4.7). 지시문 3.2 "unknown (확인 항목으로 남김)" [읽음].
- `exception_rate`, `current_actor`, `minutes_per_item`은 트리가 쓰지 않는다(3.3 원문에 없다). 예외 비율이 60%여도 `rule_expressible: yes`면 예외 검토 행이 없다. `current_actor`는 AI 과잉 후보(4.8)에만 쓴다.

### 4.2 조건 표

테스트가 이 표를 읽는다(8절). 표 모양을 바꾸지 않는다. 같은 근거 키의 행은 모두 참이어야 걸린다(그리고). 비교는 서수(대소문자 구분).

| 근거 키 | 키 | 비교 | 값 |
| --- | --- | --- | --- |
| `SIGNOFF` | `human_signoff` | `=` | `yes` |
| `Q1` | `existing_feature` | `=` | `yes` |
| `Q2` | `rule_expressible` | `=` | `no` |
| `Q3` | `input_kind` | `in` | `free_doc`, `image`, `conversation` |
| `Q4` | `frequency` | `=` | `rare` |
| `Q4` | `batch_size` | `<` | `5` |
| `PARTIAL` | `rule_expressible` | `=` | `partial` |
| `GATE` | `failure_impact` | `in` | `external`, `safety_legal` |
| `OVERUSE` | `current_actor` | `=` | `ai_each_time` |
| `CHECK_EXISTING` | `existing_feature` | `=` | `unknown` |

- `OVERUSE`는 4.8, `CHECK_EXISTING`은 4.7의 조건이다. 둘 다 행을 만들지 않는다.
- 스파이크가 이 표를 위 규칙으로 읽고, 행마다 선택지 전부(`<`는 4·5·1)를 후보에 넣어 걸림 여부가 표와 같음을 확인했다. 차이 0건 [P:spec cond].
- 경계: `rare`·4건 → Q4, `rare`·5건 → ELSE, `weekly`·1건 → ELSE, `monthly`·4건 → ELSE [P:branch boundary].
- T-008: `SIGNOFF` 행을 맨 위에 더했다. 걸리면 다른 근거 키(`OVERUSE` 포함)를 보지 않는다. `CHECK_EXISTING`(확인 항목)은 트리 밖이라 `SIGNOFF` 단계에도 달린다 [T8:P:signoff].

### 4.3 행 만들기

- 단계마다 `step` 행 하나. `ELSE`이고 `PARTIAL` 조건이면 `exception_review` 행. `GATE` 조건이면 `human_gate` 행.
- T-008: `SIGNOFF` 단계는 `step` 행 하나뿐이다. 사람이 서명하는 단계 뒤에 사람 게이트를 또 두지 않는다(ADR 0003 결정). 앞 단계의 게이트와 이어져 사람 확인이 두 번 나와도 그대로 둔다. 트리는 단계마다 따로 정한다(ADR 0003 남은 질문 1).
- 순서: `step` → `exception_review` → `human_gate`. 게이트는 원문대로 "해당 단계 뒤", 예외 검토까지 끝난 뒤다 [P:order PARTIAL+GATE].
- 부가 행의 `number`·`name`은 부모 단계 것이다(같은 `steps` 원소 안에 있다). 이름을 새로 짓지 않는다.
- `defaults`·`checks`·`ai_overuse_candidate`는 단계에 하나씩이다. 부가 행에는 없다.

행 모델 비교. 지시문 12절은 `최종 확인 = ④`, `발송 = ④`처럼 ④만 맡는 단계를 적는다 [읽음]. 트리에서 사용자 단계 자체가 ④가 되는 잎은 Q4(`rare`이고 5건 미만)뿐이다. 그래서 이 ④들은 게이트 행이어야 12절과 맞는다(6.4). T-008 뒤에는 `SIGNOFF`도 사용자 단계 자체가 ④인 잎이다. 공개 예제 3종은 모든 단계가 `human_signoff: no`라서 아래 비교는 그대로다(ADR 0003 결정).

| 모델 | 예외 검토 | 게이트 | 12절 "최종 확인·발송 = ④" | table-compare 6 | 스파이크 행 수 |
| --- | --- | --- | --- | --- | --- |
| A (제안) | 행 | 행 | 게이트 행으로 맞음 | 12절 요지의 항목만으로 6 (단계 4 + 예외 1 + 게이트 1) | 초안 4단계 → 6 [S3] |
| B | 단계 속성 | 행 | 게이트 행으로 맞음 | 요지에 없는 단계 1개가 더 있어야 6 | 4단계 → 5, 5단계 변형 → 6 [S3] |
| C | 단계 속성 | 단계 속성 | 못 맞춤. "최종 확인"을 단계로 넣으면 ②·③으로 나온다 | 6단계가 필요하고 그중 ④가 없다 | 4단계 → 4 [S3] |

- form-fill·mixed-exceptions 초안에는 `partial` 단계가 없어 A·B 행 수가 같다(5) [S3].
- 지시문 3.3 원문이 두 경우 모두 "단계 추가"라고 쓴다 [읽음]. A는 둘을 같은 방식으로 다룬다.
- → 승인 질문 1.

### 4.4 담당·근거 키·행 종류 값

| 값 | 뜻 |
| --- | --- |
| `①` `②` `③` `④` | 담당. JSON 문자열(U+2460~U+2463). 이스케이프하지 않는다 [P:escape assignee-raw] |
| `SIGNOFF`, `Q1`~`Q4`, `ELSE` | `step` 행의 근거 키 |
| `PARTIAL` | `exception_review` 행의 근거 키 |
| `GATE` | `human_gate` 행의 근거 키 |
| `step`, `exception_review`, `human_gate` | 행 종류 |

### 4.5 근거 문장

문장 틀. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| 근거 키 | 담당 | 행 종류 | 문장 틀 |
| --- | --- | --- | --- |
| `SIGNOFF` | ④ | `step` | `사람이 최종 확인·서명하는 단계이므로(yes) 사람이 하는 것으로 분류.` |
| `Q1` | ① | `step` | `기존 프로그램에 같은 기능이 있으므로(yes) 기존 기능·설정으로 분류.` |
| `Q2` | ③ | `step` | `판단 기준을 글로 쓸 수 없으므로(no) 실행 시 AI 보조 + 사람 확인으로 분류. 자동 승인은 하지 않는다.` |
| `Q3` | ③ | `step` | `입력이 {input_kind 문구}({input_kind}) 입력 양식 표준화를 먼저 검토하고, 안 되면 실행 시 AI 보조로 분류. 해석만 AI, 계산·저장은 코드로 한다.` |
| `Q4` | ④ | `step` | `드물게 반복되고 한 번에 {batch_size}건이므로 그대로 두거나 일회성 AI 보조로 분류. 개발비가 더 크다.` |
| `ELSE` | ② | `step` | `{rule_expressible 문구}({rule_expressible}), 입력이 {input_kind 문구}({input_kind}), {frequency 문구} 규칙 기반 소프트웨어로 분류.` |
| `PARTIAL` | ④ | `exception_review` | `판단 기준을 일부만 글로 쓸 수 있으므로(partial) 예외 건은 사람이 검토한다.` |
| `GATE` | ④ | `human_gate` | `틀리면 {failure_impact 문구}({failure_impact}) 이 단계 뒤에 사람 게이트를 둔다.` |

문구. `{키 문구}`는 아래 표의 문구, `{키}`는 답의 값(숫자는 `InvariantCulture`)으로 바꾼다. 문구 안의 `{batch_size}`도 바꾼다.

| 키 | 값 | 문구 |
| --- | --- | --- |
| `input_kind` | `table` | 표 형식이며 |
| `input_kind` | `fixed_form` | 고정 양식이며 |
| `input_kind` | `free_doc` | 자유 형식 문서이므로 |
| `input_kind` | `image` | 이미지이므로 |
| `input_kind` | `conversation` | 대화이므로 |
| `rule_expressible` | `yes` | 판단 기준을 글로 쓸 수 있고 |
| `rule_expressible` | `partial` | 판단 기준을 일부만 글로 쓸 수 있고 |
| `frequency` | `daily` | 매일 반복되므로 |
| `frequency` | `weekly` | 매주 반복되므로 |
| `frequency` | `monthly` | 매월 반복되므로 |
| `frequency` | `rare` | 드물게 반복되지만 한 번에 {batch_size}건이므로 |
| `failure_impact` | `external` | 외부에 영향이 있으므로 |
| `failure_impact` | `safety_legal` | 안전·법규에 영향이 있으므로 |

- 문장에는 그 잎을 정한 답만 쓴다. Q1: `existing_feature`. Q2: `rule_expressible`. Q3: `input_kind`. Q4: `frequency`·`batch_size`. ELSE: `rule_expressible`·`input_kind`·`frequency`(`rare`면 `batch_size`도). 앞 분기에서 "아니다"로 넘어온 답과 `failure_impact`는 문장에 쓰지 않는다. 기본값 표시 판단(4.6)에는 들어간다.
- T-008: `SIGNOFF` 문장은 `human_signoff`만 쓴다. 값은 늘 `yes`라 틀에 글자로 적었다(ADR 0003 남은 질문 5 문구). `human_signoff: no`는 "아니다"로 넘어온 답이라 다른 문장에 쓰지 않는다. 그래서 예제 문장이 그대로다 [T8:S3].
- 지시문 3.3 예시 문장과 바이트까지 같다: `판단 기준을 글로 쓸 수 있고(yes), 입력이 표 형식이며(table), 매주 반복되므로 규칙 기반 소프트웨어로 분류.` [P:sentence example-3.3 equal=True]. 그래서 `frequency` 문구에는 값 코드를 붙이지 않았다(예시를 따름).
- 스파이크가 두 표를 읽어 만든 문장이 후보 분류기 문장과 같았다. 조합 2,160개(입력 5 × 기준 3 × 빈도 4 × 건수 1·4·5·12 × 영향 3 × 기존 기능 3)의 행 3,768개, 차이 0건 [P:spec summary].
- T-008: 조합에 `human_signoff`(yes·no)를 곱해 4,320개, 행 5,928개다(no 쪽 3,768 + yes 쪽 `SIGNOFF` 한 행씩 2,160). 고친 테스트(`ReasonSentenceTests`)가 이 문서 표로 대조해 차이 0건이다 [T8:S5].
- 문장은 Core가 만든다(CLAUDE.md). 틀·문구 문자열은 Core 한 파일에 두고 테스트가 이 표와 대조한다(4.9).
- → 승인 질문 5.

### 4.6 기본값 표시

- 규칙: 행을 정할 때 트리가 읽은 답(아래 표) 중 하나라도 `IsDefault`면, 그 행 문장 끝에 공백 하나와 `(기본값 사용, 확인 필요)`를 붙인다. 카드 수용 기준 2.
- `defaults`에는 단계의 답 9개(T-008 뒤 10개) 중 `IsDefault`인 키를 모두 적는다(case-schema 4.4 표 순서). 문장 표시와 상관없다. 그래서 기본값이 화면에서 사라지지 않는다(지시문 2절 6). T-008 뒤에는 답 10개 중이다.

읽은 답. 테스트가 이 표를 읽는다. 키 순서는 case-schema 4.4 표 순서다.

| 근거 키 | 읽은 답 |
| --- | --- |
| `SIGNOFF` | `human_signoff` |
| `Q1` | `failure_impact`, `existing_feature`, `human_signoff` |
| `Q2` | `rule_expressible`, `failure_impact`, `existing_feature`, `human_signoff` |
| `Q3` | `input_kind`, `rule_expressible`, `failure_impact`, `existing_feature`, `human_signoff` |
| `Q4` | `input_kind`, `rule_expressible`, `frequency`, `batch_size`, `failure_impact`, `existing_feature`, `human_signoff` |
| `ELSE` | `input_kind`, `rule_expressible`, `frequency`, `failure_impact`, `existing_feature`, `human_signoff` |
| `ELSE (frequency = rare)` | `input_kind`, `rule_expressible`, `frequency`, `batch_size`, `failure_impact`, `existing_feature`, `human_signoff` |
| `PARTIAL` | `rule_expressible` |
| `GATE` | `failure_impact` |

- `failure_impact`는 늘 읽는다("항상"). 기본값(`internal_rework`)이면 게이트를 안 두는 판단이 기본값에 기댄 것이라 `step` 행에 표시한다 [P:mark only fi default].
- `batch_size`는 `frequency`가 `rare`일 때만 읽는다(그리고의 앞이 거짓이면 뒤를 보지 않는다) [P:mark bs default weekly, bs default rare].
- `exception_rate`, `current_actor`, `minutes_per_item`은 트리가 읽지 않는다. 기본값이어도 문장에 표시하지 않는다 [P:mark only er/ca/mpi default]. `defaults`에는 나온다.
- 스파이크가 이 표와 후보의 `Used`를 같은 행 3,768개에서 비교했다. 차이 0건 [P:spec summary].
- T-008: `human_signoff`는 맨 먼저 읽는다. 그래서 `SIGNOFF`가 아닌 잎(Q1~Q4, ELSE)은 모두 읽은 답에 `human_signoff`가 있다(ADR 0003 규칙). `human_signoff`가 기본값(키 없음)이면 모든 `step` 행에 표시가 붙는다 [T8:P:mark]. 문답이 모든 단계에 값을 적는 것(ADR 0003 결정)이 이 표시를 막는다.
- T-008: `SIGNOFF`는 게이트를 보지 않으므로 `failure_impact`를 읽지 않는다(ADR 0003 규칙의 **해석**). `existing_feature` 등 다른 답이 기본값이어도 `SIGNOFF` 문장에는 표시가 없다. `defaults`·확인 항목에는 남는다 [T8:P:signoff].
- T-008: `PARTIAL`·`GATE`의 읽은 답은 지금처럼 키 하나다. 게이트가 `SIGNOFF`가 아닐 때만 붙지만, 그 판단은 `step` 행이 이미 `human_signoff`를 읽어 표시한다. `PARTIAL`이 `ELSE`일 때만 붙는데 `existing_feature`를 읽은 답에 넣지 않은 것과 같은 방식이다(`human-signoff.md` 승인 질문).
- T-008: 고친 테스트(`DefaultMarkTests`)가 조합 4,320개의 행 5,928개에서 이 표와 `Used`를 대조해 차이 0건이다 [T8:S5].
- → 승인 질문 6.

### 4.7 확인 항목

| 키 | 조건 | 뜻 |
| --- | --- | --- |
| `measure_minutes_per_item` | `minutes_per_item` 값이 null | 건당 시간을 잰다(지시문 3.2 "측정 권고로 표시") |
| `confirm_existing_feature` | `existing_feature` = `unknown` | 기존 프로그램에 같은 기능이 있는지 확인한다. 있으면 ①(지시문 3.2 "확인 항목으로 남김") |

- 값만 본다. 직접 쓴 `unknown`·`null`도, 기본값도 같다 [P:branch ef=unknown, mpi=null].
- 순서는 위 표 순서(관련 키의 4.4 표 순서).
- 화면·평가서 문구는 평가서 카드에서 정한다.
- T-008: 확인 항목은 트리 밖이라 `SIGNOFF` 단계에도 그대로 달린다. `human_signoff`에는 확인 항목이 없다 [T8:P:signoff].
- → 승인 질문 7.

### 4.8 AI 과잉 후보

- 지시문 3.4: "AI 과잉 후보 = `current_actor == ai_each_time`인데 분류가 ②" [읽음].
- `ai_overuse_candidate`: `current_actor` 값이 `ai_each_time`이고 `step` 행의 담당이 ②. 부가 행(④)은 보지 않는다. 그래서 ② + 예외 검토도 후보다 [P:overuse].
- Q1·Q2·Q3·Q4는 후보가 아니다 [P:overuse].
- T-008: `SIGNOFF`(④)도 후보가 아니다. `current_actor: ai_each_time`이어도 false다(ADR 0003 규칙) [T8:P:signoff].
- 측정 없이 분류만으로 정해진다. 지시문 12절 form-fill 기대값("AI 과잉 후보 1단계")이 있다 [읽음]. 카드 수용 기준에는 없다.
- → 승인 질문 8.

### 4.9 숫자·값을 어디서 읽나

- 지시문 13절: "분류·판정 숫자를 코드에 박지 않는다. case.yaml의 thresholds와 docs/spec/의 기본값 표에서 읽는다" [읽음].
- 제안: T-003 승인 결정 5와 같은 방식. Core는 4.2 조건 표의 값(`5` 포함)과 4.5 문장 틀·문구를 한 파일에만 상수로 둔다. 테스트가 이 문서의 표를 파일에서 읽어 Core 동작과 대조한다. 스펙 표를 바꾸면 테스트가 실패하고, 그때 Core 상수를 고친다.
- 실행 중에 이 문서를 읽지 않는다. 배포 exe(지시문 4절 단일 파일)에 docs/가 없다.
- `case.yaml`의 `thresholds`에 분류 숫자를 넣지 않는다. 승인된 스키마(case-schema 4.5)가 바뀌기 때문이다.
- 스파이크 S8이 이 방식으로 표 4개(조건, 문장 틀, 문구, 읽은 답)를 읽어 후보와 대조했다(차이 0건) [P:spec].
- T-008: `SIGNOFF` 조건 값·문장 틀과 추정 표시 문구(4.12)도 같은 한 파일(`ClassificationRules.cs`)에 둔다.
- → 승인 질문 9.

### 4.10 결정론·원본 불변·G2·G3

- 분류기는 순수 함수다. 난수·시각·환경 변수·현재 폴더·파일을 쓰지 않는다. 비교는 서수, 숫자는 `InvariantCulture`.
- 해시 코드·`Dictionary` 열거 순서를 출력 순서에 쓰지 않는다. 순서는 단계 순서, 행 순서(4.3), 키는 case-schema 4.4 표 순서.
- G1: 후보 3파일을 Core에 넣고 Release `-warnaserror` 빌드, G1 테스트 61개 통과 [S2]. 기준선 Core에 없던 AssemblyRef는 `System.Text.Json`, `System.Text.Encodings.Web` 둘이다 [S3 base-core-refs, core-refs]. 둘 다 런타임 폴더의 프레임워크 어셈블리라 N6 대상이 아니다(G1 통과로 확인). Y1 범위 밖 YamlDotNet 타입은 없다 [S3 core-refs]. `dotnet format --verify-no-changes` 통과, 전체 테스트 457개 통과 [S2].
- G2·G3 등록(제안):

| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |
| --- | --- | --- |
| `classifier-case-valid` | `case-valid` (기존) | `CaseParser.ParseDirectory` → `StepClassifier.Classify` → `ClassificationJson.Render` → 출력 폴더의 `classification.json` 한 파일 |

- 결과 [S5]: `classifier-case-valid` G2·G3 차이 0건.
- 기존 테스트 하나가 깨진다 [S5]: `GuardTargetAdapterTests.Real_targets_use_case_samples_not_basic`가 샘플 목록을 `["case-invalid", "case-valid"]`로 고정해 둬서, 같은 샘플을 쓰는 대상이 늘면 `["case-invalid", "case-valid", "case-valid"]`가 된다. 이 테스트의 뜻은 "`guard/basic`을 실제 대상이 쓰지 않는다"이다. 중복을 뺀 목록으로 비교하거나 "`basic`이 없다"로 바꾼다. 약화가 아니라 새 대상을 반영하는 변경이다. PR 본문에 적는다. → 승인 질문 12.
- 음성 대조 [S6]: 어댑터가 케이스 이름 자리에 입력 폴더 절대 경로를 넣으면 G2가 `G2 R3 다른 경로: out/classification.json SHA-256 다름`으로 잡는다.
- 정상 표본 `case-valid`는 분류에 ②·③·④(예외 검토·게이트)와 기본값 표시 3행, 확인 항목 2종을 모두 낸다 [S3 classify-case-valid]. 오류 표본은 분류할 것이 없어 등록하지 않는다.
- T-008: 표본 `case-valid`는 `human_signoff`를 적지 않았다(case-schema 6.1 블록과 같아야 해서 바꾸지 않는다). 그래서 1·2단계 `ELSE` 행에도 기본값 표시가 붙어 3행 → 5행이다 [T8:S4 guard]. `ClassifierGuardTargetTests`의 숫자 3을 5로 고친다(`human-signoff.md` 승인 결과 juhan 1·3). G2·G3는 같은 실행 안의 비교라 그대로 통과한다 [T8:S5]. `SIGNOFF`·`inferred`를 쓰는 G2·G3 표본은 더하지 않는다(제안, 같은 질문).

### 4.11 Core 공개 API (제안)

네임스페이스 `Scorecard.Core.Classification`. 스파이크 후보와 같은 모양이다.

```csharp
public static class StepClassifier
{
    public const string KindStep = "step";
    public const string KindExceptionReview = "exception_review";
    public const string KindHumanGate = "human_gate";

    // 순수 함수. 파일을 읽지 않는다.
    public static ClassificationResult Classify(CaseDefinition definition);
}

public static class ClassificationJson
{
    public const string FileName = "classification.json";

    // 3.3 바이트 규칙. UTF-8(BOM 없음), LF, 끝에 "\n".
    public static byte[] Render(ClassificationResult result);
}

public sealed record ClassificationResult(string Case, IReadOnlyList<StepClassification> Steps);

// T-008: Defaults 뒤에 Inferred(추정 답 키, case-schema 4.4 순서, 4.12). JSON에는 쓰지 않는다.
public sealed record StepClassification(
    int Number, string Name,
    IReadOnlyList<string> Defaults, IReadOnlyList<string> Inferred, IReadOnlyList<string> Checks,
    bool AiOveruseCandidate, IReadOnlyList<ClassificationRow> Rows);

// Used: 트리가 읽은 답(4.6 표). JSON에는 쓰지 않는다.
public sealed record ClassificationRow(
    string Kind, string Assignee, string Rule, IReadOnlyList<string> Used, string Reason);
```

- 조건 값·문장 틀·문구는 `internal` 상수 한 파일(예: `ClassificationRules.cs`)에 둔다(4.9).
- 질문 키 순서는 Core의 `StepQuestions.Keys`를 쓴다(T-003). 새로 적지 않는다.
- `FileName`은 스파이크 후보에 없다. 테스트·Cli가 같은 이름을 쓰게 하려고 더했다. 구현과 테스트(`ClassificationApiShapeTests`)로 확인했다.
- → 승인 질문 15.
- T-008: 공개 타입·메서드는 그대로다. `StepClassification`에 위치 매개변수 `Inferred` 하나가 늘어 기존 생성 코드(`ClassificationApiShapeTests`)는 고쳐야 컴파일된다. `ClassificationJson.Render`는 바꾸지 않는다 [T8:S2].

### 4.12 추정 표시 (T-008)

ADR 0004 결정 2와 질문 2(제안대로 승인)에서 왔다 [읽음]. 분류는 값으로만 한다. 추정 여부는 문장 표시만 바꾼다.

- 추정 표시 규칙: 행을 정할 때 읽은 답(4.6 표) 중 하나라도 `IsInferred`면, 그 행 문장 끝에 공백 하나와 `(AI 추정, 확인 필요)`를 붙인다.
- 기본값 표시와 같이 붙으면 기본값 표시 뒤에 둔다: `… 분류. (기본값 사용, 확인 필요) (AI 추정, 확인 필요)` [T8:P:inferred].
- 한 답이 기본값이면서 추정일 수는 없다(case-schema 4.13). 두 표시가 같이 붙는 것은 읽은 답 가운데 하나는 기본값, 다른 하나는 추정일 때다.
- `step`·`exception_review`·`human_gate` 행 모두 같은 규칙이다. 부가 행은 읽은 답이 키 하나라서 그 키가 추정일 때만 붙는다.
- `Inferred`: 단계의 `IsInferred` 키(case-schema 4.4 표 순서). 쓴 순서와 상관없다. 메모리 결과에만 둔다. classification.json에는 쓰지 않는다(3.2) [T8:P:json].
- 분류(담당·근거 키·행), `defaults`, 확인 항목, AI 과잉 후보는 추정과 상관없다.
- 공개 예제 3종에는 `inferred`가 없다(ADR 0004 결정 2). 그래서 예제 문장에 추정 표시가 없고 `expected/classification.json`이 그대로다. 테스트는 TestData 표본(6.5)으로 한다. 지시문 2절 11("examples/ 먼저")과의 관계는 ADR 0004 질문 2에서 juhan이 "표시라서 TestData로" 승인했다 [읽음 ADR 0004 승인 결과].

예시. 기준 답(6.1)에서 바꾼 답과 `inferred` [T8:P:inferred]:

| 추정인 답 | 문장 표시 |
| --- | --- |
| `frequency` | `step` 행 |
| `failure_impact`, `failure_impact: external` | `step`·게이트 두 행 |
| `rule_expressible`, `rule_expressible: partial` | `step`·예외 검토 두 행 |
| `exception_rate` / `current_actor` / `minutes_per_item` | 없음(트리가 읽지 않는다). `Inferred`에는 남는다 |
| `batch_size`, `frequency: weekly` | 없음 |
| `batch_size`, `frequency: rare, batch_size: 10`(ELSE) / `3`(Q4) | `step` 행 |
| `input_kind`, `existing_feature: yes`(Q1) | 없음(Q1은 `input_kind`를 읽지 않는다) |
| `human_signoff`, `human_signoff: yes` | `SIGNOFF` 행 |
| `frequency`·`failure_impact`, `human_signoff: yes` | 없음(`SIGNOFF`는 `human_signoff`만 읽는다) |
| `frequency`, `existing_feature` 키 없음 | `step` 행에 두 표시(기본값 뒤 추정) |
| `failure_impact`, `human_signoff` 키 없음, `failure_impact: external` | `step` 행에 두 표시, 게이트 행에 추정 표시만 |

- 위 표는 테스트가 읽는 표가 아니다(머리 행이 문서에 한 번뿐이라 test-engineer가 읽게 할 수는 있다).

## 5. 예외

| 상황 | 결과 | 근거 |
| --- | --- | --- |
| `definition`이 null | `ArgumentNullException` | 후보 코드 |
| 파싱 오류(`CaseParseResult.Case`가 null) | 분류하지 않는다. 부르는 쪽이 오류를 보인다. `Classify`는 모델만 받는다 | 4.11 |
| 열거 값이 선택지 밖 | 생기지 않는다. 파서가 `enum` 오류로 막는다(case-schema 4.6). 후보는 `ArgumentOutOfRangeException` | [읽음], 후보 코드 |
| 답 없는 단계(10개 모두 기본값, T-008) | `ELSE` ② + `PARTIAL` ④, 두 행 모두 기본값 표시, 확인 항목 2종. `human_signoff` 기본값 `no`라 `SIGNOFF`가 아니다 | [P:mark parser-all-defaults], [T8:P:mark 답 없는 단계] |
| `human_signoff: yes`(T-008) | 다른 답과 상관없이 ④ `SIGNOFF` 한 행. 게이트·예외 검토 행이 없다. 확인 항목은 그대로 달린다. AI 과잉 후보가 아니다 | [T8:P:signoff] |
| `human_signoff`를 적지 않은 단계(T-008) | `no`로 분류하고 `Q1`·`Q2`·`Q3`·`Q4`·`ELSE` 행에 기본값 표시. 부가 행에는 붙지 않는다(4.6) | [T8:P:mark human_signoff=-] |
| `human_signoff` 값이 `Yes`·`true`·빈 문자열·`unknown`(T-008) | 생기지 않는다. 파서가 `enum` 오류로 막는다(case-schema 4.9) | [T8:P:mark] |
| `inferred`가 있는 단계(T-008) | 분류는 같다. 읽은 답이 추정이면 문장 끝에 추정 표시(4.12). classification.json의 속성은 그대로다 | [T8:P:inferred], [T8:P:json] |
| Q4인데 `current_actor: program` | ④로 나온다. "그대로 두기"가 실제로는 프로그램이다. 문장은 같다 | [P:order Q4 ca=program] → 승인 질문 3 |
| `rule_expressible: no`이고 입력이 자유 형식 | Q2. 입력 양식 표준화 제안이 나오지 않는다 | [P:order Q2>Q3] |
| 단계 이름에 제어 문자·이모지 | JSON 이스케이프(3.3). 이름은 바꾸지 않는다 | [P:escape] |
| Windows 줄바꿈으로 받은 `answers.yaml` | 같은 결과 | [P:eol], [S7] |
| publish 단일 exe에서 `System.Text.Json` 동작 | **미확인** | — |

## 6. 예시

### 6.1 분기 예시

기준 답: `input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 10, exception_rate: 0, current_actor: human, minutes_per_item: 1, failure_impact: internal_rework, existing_feature: no, human_signoff: no` (모두 직접 쓴 값). 바꾼 답만 적는다 [P:branch][P:order]. T-008에서 `human_signoff: no`를 더했다. 그 전 행의 결과는 그대로다 [T8:S2b].

| 바꾼 답 | 행 |
| --- | --- |
| (없음) | ② `ELSE` |
| `existing_feature: yes` | ① `Q1` |
| `rule_expressible: no` | ③ `Q2` |
| `input_kind: free_doc` / `image` / `conversation` | ③ `Q3` |
| `frequency: rare, batch_size: 4` | ④ `Q4` |
| `frequency: rare, batch_size: 5` | ② `ELSE` |
| `rule_expressible: partial` | ② `ELSE` + ④ `PARTIAL` |
| `failure_impact: external` / `safety_legal` | ② `ELSE` + ④ `GATE` |
| `existing_feature: yes, rule_expressible: no` | ① `Q1` |
| `rule_expressible: no, input_kind: free_doc` | ③ `Q2` |
| `input_kind: image, frequency: rare, batch_size: 1` | ③ `Q3` |
| `input_kind: free_doc, rule_expressible: partial` | ③ `Q3` (예외 검토 없음) |
| `frequency: rare, batch_size: 1, rule_expressible: partial` | ④ `Q4` (예외 검토 없음) |
| `existing_feature: yes, failure_impact: external` | ① `Q1` + ④ `GATE` |
| `rule_expressible: no, failure_impact: external` | ③ `Q2` + ④ `GATE` |
| `rule_expressible: partial, failure_impact: external` | ② `ELSE` + ④ `PARTIAL` + ④ `GATE` |
| `human_signoff: yes` | ④ `SIGNOFF` |
| `human_signoff: yes, existing_feature: yes` | ④ `SIGNOFF` (Q1보다 먼저) |
| `human_signoff: yes, rule_expressible: no` | ④ `SIGNOFF` (Q2보다 먼저) |
| `human_signoff: yes, frequency: rare, batch_size: 1` | ④ `SIGNOFF` (Q4보다 먼저) |
| `human_signoff: yes, failure_impact: external` | ④ `SIGNOFF` (게이트 없음) |
| `human_signoff: yes, failure_impact: safety_legal` | ④ `SIGNOFF` (게이트 없음) |
| `human_signoff: yes, rule_expressible: partial, failure_impact: external` | ④ `SIGNOFF` (예외 검토·게이트 없음) |

- 표의 마지막 7행은 T-008이다 [T8:P:signoff]. `SIGNOFF`는 늘 한 행이다. 바꾼 답 칸의 ` / ` 대안은 키 하나일 때만 쓴다(테스트가 그렇게 읽는다).

### 6.2 기본값 표시 예시

기준 답에서 해당 키만 `IsDefault = true`로 둔다(값은 그대로) [P:mark].

| 기본값인 답 | 문장 표시 |
| --- | --- |
| `minutes_per_item`(null) | 없음. 확인 항목 `measure_minutes_per_item` |
| `exception_rate` | 없음 |
| `current_actor` | 없음 |
| `failure_impact` | `step` 행에 표시 |
| `batch_size`, `frequency: weekly` | 없음 |
| `batch_size`, `frequency: rare`(10건 → ELSE) | `step` 행에 표시 |
| `frequency` | `step` 행에 표시 |
| `rule_expressible`(`partial`) | `step`·`exception_review` 두 행에 표시 |
| `input_kind`, `existing_feature: yes`(Q1) | 없음(Q1은 `input_kind`를 읽지 않는다) |
| `input_kind`, ELSE | `step` 행에 표시 |
| `existing_feature`(`unknown`) | `step` 행에 표시. 확인 항목 `confirm_existing_feature` |
| `human_signoff`(`no`) | `step` 행에 표시 |
| `human_signoff`, `failure_impact: external` | `step` 행에 표시. 게이트 행에는 없다(게이트 행은 `failure_impact`만 읽는다) |
| `human_signoff`, `rule_expressible: partial` | `step` 행에 표시. 예외 검토 행에는 없다 |
| `human_signoff`, `existing_feature: yes`(Q1) | `step` 행에 표시 |
| `human_signoff`, `frequency: rare, batch_size: 3`(Q4) | `step` 행에 표시 |

- `human_signoff` 5행은 T-008이다 [T8:P:mark]. 기본값은 `no`뿐이라 기본값이 `SIGNOFF` 행에 붙는 일은 없다.

파서를 거친 "답 없는 단계" [P:mark parser-all-defaults]:

```
② ELSE: 판단 기준을 일부만 글로 쓸 수 있고(partial), 입력이 고정 양식이며(fixed_form), 매주 반복되므로 규칙 기반 소프트웨어로 분류. (기본값 사용, 확인 필요)
④ PARTIAL: 판단 기준을 일부만 글로 쓸 수 있으므로(partial) 예외 건은 사람이 검토한다. (기본값 사용, 확인 필요)
defaults: 10개 모두. checks: measure_minutes_per_item, confirm_existing_feature
```

### 6.3 지시문 12.1 비공개 예시와 비교

T-003의 합성 사본 `tests/Scorecard.Tests/TestData/case-schema/example-12-1/case.yaml`(12.1과 답이 같고 이름·문장만 합성)을 분류했다 [S3 classify-example-12-1]. 실제 케이스는 쓰지 않았다.

| 단계 | 12.1 사람 판단 | 트리 결과 | 같은가 |
| --- | --- | --- | --- |
| 1 측정 파일 열기·정리 | ② | ② `ELSE` | 같음 |
| 2 분석 구간 선택(정상 신호) | ② | ② `ELSE` | 같음 |
| 3 분석 구간 선택(불규칙 신호) | ② + 예외는 ④ | ② `ELSE` + ④ `PARTIAL` | 같음 |
| 4 값 계산·복사 | ② | ② `ELSE` | 같음 |
| 5 보고서 채우기 | ② | ② `ELSE` | 같음 |
| 6 결과 해석 문장 작성 | ③ 또는 ④ | ③ `Q2` + ④ `GATE` | 같다고 본다(③과 ④가 모두 있다). 입력이 `free_doc`이지만 Q2가 먼저라 표준화 제안은 없다 |
| 7 최종 확인·제출 | ④ | ③ `Q2` + ④ `GATE` | **다름**. 단계 자체가 ③(AI 보조)으로 나온다 |

- 행 수: 모델 A 10, B 9, C 7 [S3].
- 7단계가 다른 이유: 사용자 단계가 ④만 되는 잎은 Q4뿐이다. `rule_expressible: no`면 늘 Q2(③)다. "사람이 책임지고 확인·제출하는 단계"를 가르는 질문이 9개 중에 없다.
- 지시문 12.1: "다르면 예제를 고치는 게 아니라, 어떤 질문·규칙이 빠졌는지 ADR로 적고 `examples/`에 그 구조를 가진 합성 예제를 추가한다" [읽음]. 12.1은 통과 기준이 아니다. 그래서 T-004는 3.3 트리를 그대로 구현하고, 이 차이는 ADR로 묻는다. 무엇을 더할지(질문, 규칙)는 사람 판단이다(**미확인**). → 승인 질문 4.
- T-008: ADR 0003(승인됨)이 이 차이를 질문 `human_signoff`와 규칙 `SIGNOFF`로 메웠다 [읽음]. 합성 사본에 1~6단계 `human_signoff: no`, 7단계 `yes`를 넣으면 7단계가 ④ `SIGNOFF` 한 행이 되고 행 수는 10 → 9다. 1~6단계 결과는 위 표와 같다 [T8:S4 example-12-1]. TestData 합성 사본 파일은 바꾸지 않는다(case-schema 테스트가 쓴다). 공개 예제는 다음 카드(`signoff-final`)에서 더한다.
- T-009: 그 공개 예제가 7.5 `signoff-final`이다. 답의 모양은 12.1과 같고 이름·문장·값은 새로 합성했다. 7단계만 `human_signoff: yes`다. 1~6단계 근거 키가 합성 사본과 같고, 7단계는 ④ `SIGNOFF` 한 행이다. 행 수 9 [T9:S2 rows], [T9:S3b N1].

### 6.4 지시문 12절 표와 3.3 트리

| 12절 기대 분류(요지) | 트리로 낼 수 있나 | 7절 초안에서 |
| --- | --- | --- |
| table-compare: 정상 신호 구간 선택·계산·표 채우기 = ② | 예 | `ELSE` 3행 |
| table-compare: 불규칙 신호 = ②+④ | 예. `partial` → `ELSE` + `PARTIAL`. 12.1의 같은 단계도 `partial`·`internal_rework`다 | 2단계 |
| table-compare: 최종 확인 = ④ | 사용자 단계로는 아니오(②·③이 된다). 게이트 행으로는 예 | 결과표 채우기(`external`) 뒤 `GATE` |
| form-fill: 양식 읽기·집계·보고서 = ② | 예 | 2·3·4단계 |
| form-fill: 발송 = ④ | 게이트 행으로만 | 보고서(`external`) 뒤 `GATE` |
| form-fill: `existing_feature: unknown` 1건 → 확인 항목 | 예 | 1단계(키를 안 적어 기본값) |
| form-fill: AI 과잉 후보 1단계 | 예(4.8) | 2단계 `ai_each_time` |
| mixed-exceptions: 접수 기록 = ② | 예 | 1단계 |
| mixed-exceptions: 분류·답변 = ③(입력 양식 표준화 제안) | 예. 단 `rule_expressible`이 `no`가 아닐 때만(Q2가 먼저) | 2·3단계 `free_doc`·`partial` → `Q3` |
| mixed-exceptions: 발송 = ④ | 게이트 행으로만 | 답변 초안(`external`) 뒤 `GATE` |
| 단계 수 6 / 5 / 5 | 모델 A에서: table-compare는 요지의 항목만으로 6. form-fill·mixed-exceptions는 요지 항목만으로 4라서, 요지에 이름이 없는 단계가 하나씩 더 있어야 5 | 아래 |

- 요지에 없는 단계(제안): form-fill 1단계 "주문서 파일 모으기"(확인 항목이 달리는 단계로 씀), mixed-exceptions 4단계 "처리 결과 기록"(②). 사람이 확정해야 한다. → 승인 질문 14.
- 12절 예제 셋 모두 Q1·Q4에 걸리는 단계가 없다(요지에 ①·Q4가 없다). Q1·Q4는 단위 테스트로만 확인된다.
- 어긋남(그대로 적음): 12절이 ④만 맡는 단계를 "발송", "최종 확인"이라 부르지만, 트리에는 그런 단계를 사용자 단계로 만드는 잎이 없다. 행 모델 A(게이트 = 부가 행)로 읽으면 맞고, 사용자 단계로 읽으면 맞지 않는다. → 승인 질문 1·4.
- T-009: `signoff-final`의 "최종 확인·서명·제출"이 사용자 단계 ④(`SIGNOFF`)로 나오는 첫 공개 예제다(7.5). 예제 3종은 그대로다.
- T-008: `SIGNOFF`가 생겨 "최종 확인", "발송"을 사용자 단계 ④로 낼 수 있게 됐다. 그래도 예제 3종은 바꾸지 않는다. 모든 단계가 `human_signoff: no`이고 위 표의 결과·단계 수가 그대로다(ADR 0003 결정, 7.1).

### 6.5 서명 단계와 추정 표시 (T-008)

TestData 표본(제안) `tests/Scorecard.Tests/TestData/classification/signoff-inferred/answers.yaml`. 실제 값 없음. `SIGNOFF`, 두 표시가 같이 붙는 행, 부가 행의 추정 표시, 서명 단계 뒤 게이트 없음, `human_signoff` 기본값을 한 파일에 넣었다.

```yaml
# 합성 예시. 실제 업무·값 없음. 서명 단계(SIGNOFF)와 추정 답 표시(inferred)를 섞었다.
name: signoff-inferred
task: 합성 보고서를 만들고 사람이 서명해 보낸다
steps:
  - name: 자료 모으기
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 10, exception_rate: 0, current_actor: human, minutes_per_item: 2, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
    inferred: [batch_size, frequency]
  - name: 예외 건 고르기
    answers: {input_kind: table, rule_expressible: partial, frequency: weekly, batch_size: 10, exception_rate: 30, current_actor: human, minutes_per_item: 3, failure_impact: external, human_signoff: no}
    inferred: [rule_expressible, failure_impact]
  - name: 최종 확인·서명
    answers: {input_kind: fixed_form, rule_expressible: no, frequency: weekly, batch_size: 1, exception_rate: 0, current_actor: human, minutes_per_item: 5, failure_impact: external, existing_feature: no, human_signoff: yes}
    inferred: [failure_impact]
  - name: 월간 정리
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: monthly, batch_size: 10, exception_rate: 0, current_actor: ai_each_time, minutes_per_item: 1, failure_impact: internal_rework, existing_feature: no}
    inferred: [current_actor]
```

분류 결과. 줄 모양은 `<단계 번호> <담당> <근거 키>: <근거 문장>`, 끝 네 줄은 단계별 `Inferred`(4.4 순서) [T8:S8]:

```text
1 ② ELSE: 판단 기준을 글로 쓸 수 있고(yes), 입력이 표 형식이며(table), 매주 반복되므로 규칙 기반 소프트웨어로 분류. (AI 추정, 확인 필요)
2 ② ELSE: 판단 기준을 일부만 글로 쓸 수 있고(partial), 입력이 표 형식이며(table), 매주 반복되므로 규칙 기반 소프트웨어로 분류. (기본값 사용, 확인 필요) (AI 추정, 확인 필요)
2 ④ PARTIAL: 판단 기준을 일부만 글로 쓸 수 있으므로(partial) 예외 건은 사람이 검토한다. (AI 추정, 확인 필요)
2 ④ GATE: 틀리면 외부에 영향이 있으므로(external) 이 단계 뒤에 사람 게이트를 둔다. (AI 추정, 확인 필요)
3 ④ SIGNOFF: 사람이 최종 확인·서명하는 단계이므로(yes) 사람이 하는 것으로 분류.
4 ② ELSE: 판단 기준을 글로 쓸 수 있고(yes), 입력이 고정 양식이며(fixed_form), 매월 반복되므로 규칙 기반 소프트웨어로 분류. (기본값 사용, 확인 필요)
inferred 1: frequency, batch_size
inferred 2: rule_expressible, failure_impact
inferred 3: failure_impact
inferred 4: current_actor
```

- 1단계: `batch_size`는 `weekly`라 읽지 않는다. `frequency`만으로 추정 표시가 붙는다. `Inferred`는 쓴 순서가 아니라 4.4 순서다.
- 2단계: `existing_feature`를 적지 않아 기본값(`unknown`) 표시가 먼저, 추정 표시가 뒤다. 확인 항목 `confirm_existing_feature`가 달린다. 부가 행은 각자 읽은 키 하나가 추정이라 추정 표시만 붙는다.
- 3단계: `failure_impact`가 `external`이고 추정이지만 `SIGNOFF`는 그것을 읽지 않는다. 게이트 행도 없다. 문장에 표시가 없다.
- 4단계: `human_signoff`를 적지 않아 기본값 표시가 붙는다. `current_actor`는 트리가 읽지 않아 추정 표시가 없다. AI 과잉 후보다(`ai_each_time`, ②).
- 이 표본의 classification.json에는 `inferred` 속성이 없다. 같은 답에서 `inferred` 줄만 뺀 파일과 `reason` 네 줄(1단계 1행, 2단계 3행)만 다르다 [T8:S8 json-diff].

## 7. 공개 예제 3종 초안 (사람 확정 필요)

### 7.1 만드는 방법과 확정 절차 (제안)

1. 이 절의 `answers.yaml`·`expected/classification.json` 블록은 prototyper 초안이다. `expected` 블록은 스파이크 후보가 초안 `answers.yaml`에서 실제로 낸 바이트다 [S3].
2. 사람이 이 문서를 승인할 때 블록 내용을 확정한다(고칠 곳은 승인 답에 적는다). 고치면 prototyper가 스파이크를 다시 돌려 블록을 맞춘다.
3. 승인 뒤 test-engineer가 블록을 바이트 그대로 `examples/<예제>/answers.yaml`, `examples/<예제>/expected/classification.json`으로 옮긴다. 테스트가 블록과 파일이 같은지 본다(T-003 `SpecDocumentTests` 방식). 카드 폴더 범위에 `examples/`가 있다 [읽음 카드]. CLAUDE.md 에이전트 폴더 제한에는 `examples/`가 없다 [읽음]. → 승인 질문 14.
4. `examples/.gitattributes`(`* -text`)를 같이 넣는다. 없으면 `core.autocrlf=true` clone에서 `expected/classification.json`이 2,619바이트에서 2,712바이트로(T-004 당시. T-007 뒤 table-compare는 2,642바이트), `answers.yaml`이 1,083바이트에서 1,095바이트로 바뀐다. 있으면 그대로다 [S7]. 골든 테스트가 바이트를 비교하므로 필요하다. T-002 승인 결정 10은 이 결정을 T-005로 미뤘다 [읽음]. 예제 파일을 처음 넣는 카드가 T-004가 됐다. → 승인 질문 11.
5. `examples/`의 다른 파일(`case.yaml`, `intake.md`, `reference/`, `candidate/`)은 T-005가 만든다. T-005의 `case.yaml` 단계·답은 이 `answers.yaml`과 같아야 한다(T-005에서 테스트).
6. 지시문 2절 10 "examples/는 합성 생성기로만 만든다"는 자료(`reference/`, `candidate/`)에 대한 것으로 본다. `answers.yaml`은 사람이 답한 질문지이고 `expected/`는 사람이 확정한 정답이다(지시문 12절) → 승인 질문 14.
7. T-008 개정(승인됨): 아래 `answers.yaml` 블록 셋은 모든 단계의 `answers` 줄 끝에 `, human_signoff: no`를 더한 것이다. 그 밖의 줄(주석, 단계 이름, 다른 답)은 바이트 그대로다. `expected/classification.json` 블록 셋과 SHA-256 줄은 바꾸지 않았다. 스파이크 후보가 바꾼 `answers.yaml`에서 낸 바이트가 이 블록·SHA-256과 같았다 [T8:S3 classify]. 키를 더하지 않으면(지금 파일) 후보는 모든 `step` 행에 기본값 표시를 붙여 바이트가 달라진다 [T8:S3 head-answers]. 그래서 `answers.yaml`을 같이 바꾼다. 블록 = 파일 테스트 때문에 `examples/*/answers.yaml` 세 파일도 블록대로 바꿔야 한다(`human-signoff.md` 승인 결과 juhan 1·3).
8. T-009 개정(초안): 7.5 `signoff-final` 블록 둘은 prototyper 초안이다. `answers.yaml`은 prototyper가 썼다. `expected/classification.json` 블록은 main Core가 그 `answers.yaml`에서 낸 바이트다. 이 문서 표만 보고 따로 짠 Python 분류기(`spikes/T-009/xcheck/classify.py`)도 같은 바이트를 냈다 [T9:S2 xcheck]. 엔진 출력은 정답이 아니다. juhan이 블록을 확정한 뒤 test-engineer가 블록을 바이트 그대로 `examples/signoff-final/`로 옮긴다(`signoff-final.md` 8절). 7.2~7.4 블록은 그대로다.

요약 [S3]:

| 예제 | 사용자 단계 | 행(모델 A) | ② | ③ | ④ 예외 검토 | ④ 게이트 | 확인 항목 | AI 과잉 후보 | 기본값 표시 문장 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| table-compare | 4 | 6 | 4 | 0 | 1 | 1 | `measure` 4 | 0 | 0 |
| form-fill | 4 | 5 | 4 | 0 | 0 | 1 | `measure` 4, `confirm` 1 | 1 (2단계) | 1 (1단계) |
| mixed-exceptions | 4 | 5 | 2 | 2 | 0 | 1 | `measure` 4 | 0 | 0 |
| signoff-final | 7 | 9 | 5 | 1 | 1 | 1 | `measure` 7 | 0 | 0 |

- T-009: `signoff-final`도 `minutes_per_item`을 적지 않았다. 7단계 모두 `measure`가 달린다. 기본값 표시 문장은 0이다(나머지 답 9개를 모두 적었다).
- `minutes_per_item`은 세 예제 모두 적지 않았다(12.1 예시와 같음). 그래서 모든 단계에 `measure_minutes_per_item`이 달리고 `defaults`에 `minutes_per_item`이 있다. 평가서 카드가 시간 값을 원하면 그때 `answers.yaml`과 `expected`를 같이 바꾼다.
- `batch_size`는 12절 검증 자료 건수(30·20)에 맞췄다. form-fill 주간 보고서는 1건, mixed-exceptions는 자료가 없어 15로 정했다. 분류에는 영향이 없다(`rare`인 단계가 없다).

### 7.2 table-compare

`examples/table-compare/answers.yaml`:

```yaml
# 공개 합성 예제. 실제 업무·값 없음. 지시문 12절 table-compare.
name: table-compare
task: 센서 로그에서 구간을 골라 평균을 내고 결과표를 채운다
steps:
  - name: 정상 신호 구간 선택
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 30, exception_rate: 5, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 불규칙 신호 구간 선택
    answers: {input_kind: table, rule_expressible: partial, frequency: weekly, batch_size: 30, exception_rate: 40, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 구간 평균 계산
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 30, exception_rate: 0, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 결과표 채우기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 30, exception_rate: 0, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: no}
```

`examples/table-compare/expected/classification.json` (SHA-256 `44e64b8dad5182a193f2fd364fb587a02104c1868cac83c74a737586e17e3ea5`):

```json
{
  "schema_version": 1,
  "case": "table-compare",
  "steps": [
    {
      "number": 1,
      "name": "정상 신호 구간 선택",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 표 형식이며(table), 매주 반복되므로 규칙 기반 소프트웨어로 분류."
        }
      ]
    },
    {
      "number": 2,
      "name": "불규칙 신호 구간 선택",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 일부만 글로 쓸 수 있고(partial), 입력이 표 형식이며(table), 매주 반복되므로 규칙 기반 소프트웨어로 분류."
        },
        {
          "kind": "exception_review",
          "assignee": "④",
          "rule": "PARTIAL",
          "reason": "판단 기준을 일부만 글로 쓸 수 있으므로(partial) 예외 건은 사람이 검토한다."
        }
      ]
    },
    {
      "number": 3,
      "name": "구간 평균 계산",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 표 형식이며(table), 매주 반복되므로 규칙 기반 소프트웨어로 분류."
        }
      ]
    },
    {
      "number": 4,
      "name": "결과표 채우기",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 고정 양식이며(fixed_form), 매주 반복되므로 규칙 기반 소프트웨어로 분류."
        },
        {
          "kind": "human_gate",
          "assignee": "④",
          "rule": "GATE",
          "reason": "틀리면 외부에 영향이 있으므로(external) 이 단계 뒤에 사람 게이트를 둔다."
        }
      ]
    }
  ]
}
```

### 7.3 form-fill

`examples/form-fill/answers.yaml`:

```yaml
# 공개 합성 예제. 실제 업무·값 없음. 지시문 12절 form-fill.
# 1단계는 existing_feature를 적지 않았다(모름 → 기본값 unknown → 확인 항목).
name: form-fill
task: 고정 양식 주문서를 받아 품목·수량을 집계하고 주간 보고서를 만든다
steps:
  - name: 주문서 파일 모으기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 20, exception_rate: 0, current_actor: human, failure_impact: internal_rework, human_signoff: no}
  - name: 주문서 양식 읽기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 20, exception_rate: 5, current_actor: ai_each_time, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 품목·수량 집계
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 20, exception_rate: 0, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 주간 보고서 만들기
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 1, exception_rate: 0, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: no}
```

`examples/form-fill/expected/classification.json` (SHA-256 `b1898a856862e821c9df1f2d3570220b3f909246890d2aa6555ff4376b1249bc`):

```json
{
  "schema_version": 1,
  "case": "form-fill",
  "steps": [
    {
      "number": 1,
      "name": "주문서 파일 모으기",
      "defaults": [
        "minutes_per_item",
        "existing_feature"
      ],
      "checks": [
        "measure_minutes_per_item",
        "confirm_existing_feature"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 고정 양식이며(fixed_form), 매주 반복되므로 규칙 기반 소프트웨어로 분류. (기본값 사용, 확인 필요)"
        }
      ]
    },
    {
      "number": 2,
      "name": "주문서 양식 읽기",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": true,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 고정 양식이며(fixed_form), 매주 반복되므로 규칙 기반 소프트웨어로 분류."
        }
      ]
    },
    {
      "number": 3,
      "name": "품목·수량 집계",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 표 형식이며(table), 매주 반복되므로 규칙 기반 소프트웨어로 분류."
        }
      ]
    },
    {
      "number": 4,
      "name": "주간 보고서 만들기",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 표 형식이며(table), 매주 반복되므로 규칙 기반 소프트웨어로 분류."
        },
        {
          "kind": "human_gate",
          "assignee": "④",
          "rule": "GATE",
          "reason": "틀리면 외부에 영향이 있으므로(external) 이 단계 뒤에 사람 게이트를 둔다."
        }
      ]
    }
  ]
}
```

### 7.4 mixed-exceptions

`examples/mixed-exceptions/answers.yaml`:

```yaml
# 공개 합성 예제. 실제 업무·값 없음. 지시문 12절 mixed-exceptions.
name: mixed-exceptions
task: 자유 형식 메일을 접수해 분류하고 답변 초안을 쓴다
steps:
  - name: 메일 접수 기록
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: daily, batch_size: 15, exception_rate: 5, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 문의 유형 분류
    answers: {input_kind: free_doc, rule_expressible: partial, frequency: daily, batch_size: 15, exception_rate: 30, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 답변 초안 쓰기
    answers: {input_kind: free_doc, rule_expressible: partial, frequency: daily, batch_size: 15, exception_rate: 30, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: no}
  - name: 처리 결과 기록
    answers: {input_kind: table, rule_expressible: yes, frequency: daily, batch_size: 15, exception_rate: 0, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
```

`examples/mixed-exceptions/expected/classification.json` (SHA-256 `46bd9f0ab27cea2952fbfb90d22342824a2dca325886a4a91c56a1f0105749df`):

```json
{
  "schema_version": 1,
  "case": "mixed-exceptions",
  "steps": [
    {
      "number": 1,
      "name": "메일 접수 기록",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 고정 양식이며(fixed_form), 매일 반복되므로 규칙 기반 소프트웨어로 분류."
        }
      ]
    },
    {
      "number": 2,
      "name": "문의 유형 분류",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "③",
          "rule": "Q3",
          "reason": "입력이 자유 형식 문서이므로(free_doc) 입력 양식 표준화를 먼저 검토하고, 안 되면 실행 시 AI 보조로 분류. 해석만 AI, 계산·저장은 코드로 한다."
        }
      ]
    },
    {
      "number": 3,
      "name": "답변 초안 쓰기",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "③",
          "rule": "Q3",
          "reason": "입력이 자유 형식 문서이므로(free_doc) 입력 양식 표준화를 먼저 검토하고, 안 되면 실행 시 AI 보조로 분류. 해석만 AI, 계산·저장은 코드로 한다."
        },
        {
          "kind": "human_gate",
          "assignee": "④",
          "rule": "GATE",
          "reason": "틀리면 외부에 영향이 있으므로(external) 이 단계 뒤에 사람 게이트를 둔다."
        }
      ]
    },
    {
      "number": 4,
      "name": "처리 결과 기록",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 표 형식이며(table), 매일 반복되므로 규칙 기반 소프트웨어로 분류."
        }
      ]
    }
  ]
}
```

### 7.5 signoff-final

T-009 초안(사람 확정 필요). 만드는 방법은 7.1 항목 8, 근거와 질문은 `docs/spec/signoff-final.md`.

`examples/signoff-final/answers.yaml`:

```yaml
# 공개 합성 예제. 실제 업무·값 없음. 지시문 12절 signoff-final(12.1과 같은 구조).
# 7단계만 사람이 최종 확인·서명하는 단계다(human_signoff: yes). 나머지 단계는 no.
name: signoff-final
task: 기록 파일에서 기준 구간을 골라 값을 내고, 보고서를 써서 사람이 서명해 제출한다
steps:
  - name: 기록 파일 열기·정리
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 20, exception_rate: 5, current_actor: program, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 기준 구간 고르기(안정 기록)
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 14, exception_rate: 10, current_actor: program, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 기준 구간 고르기(흔들린 기록)
    answers: {input_kind: table, rule_expressible: partial, frequency: weekly, batch_size: 6, exception_rate: 50, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 구간 값 계산·옮기기
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 20, exception_rate: 0, current_actor: program, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 보고서 양식 채우기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 20, exception_rate: 0, current_actor: program, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 결과 해석 문장 쓰기
    answers: {input_kind: free_doc, rule_expressible: no, frequency: weekly, batch_size: 20, exception_rate: 40, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: no}
  - name: 최종 확인·서명·제출
    answers: {input_kind: fixed_form, rule_expressible: no, frequency: weekly, batch_size: 20, exception_rate: 0, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: yes}
```

`examples/signoff-final/expected/classification.json` (SHA-256 `fe040ce6b8eaafbf7218451b668d232f948a6d907c279edb592be19d4155934f`):

```json
{
  "schema_version": 1,
  "case": "signoff-final",
  "steps": [
    {
      "number": 1,
      "name": "기록 파일 열기·정리",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 고정 양식이며(fixed_form), 매주 반복되므로 규칙 기반 소프트웨어로 분류."
        }
      ]
    },
    {
      "number": 2,
      "name": "기준 구간 고르기(안정 기록)",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 표 형식이며(table), 매주 반복되므로 규칙 기반 소프트웨어로 분류."
        }
      ]
    },
    {
      "number": 3,
      "name": "기준 구간 고르기(흔들린 기록)",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 일부만 글로 쓸 수 있고(partial), 입력이 표 형식이며(table), 매주 반복되므로 규칙 기반 소프트웨어로 분류."
        },
        {
          "kind": "exception_review",
          "assignee": "④",
          "rule": "PARTIAL",
          "reason": "판단 기준을 일부만 글로 쓸 수 있으므로(partial) 예외 건은 사람이 검토한다."
        }
      ]
    },
    {
      "number": 4,
      "name": "구간 값 계산·옮기기",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 표 형식이며(table), 매주 반복되므로 규칙 기반 소프트웨어로 분류."
        }
      ]
    },
    {
      "number": 5,
      "name": "보고서 양식 채우기",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 고정 양식이며(fixed_form), 매주 반복되므로 규칙 기반 소프트웨어로 분류."
        }
      ]
    },
    {
      "number": 6,
      "name": "결과 해석 문장 쓰기",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "③",
          "rule": "Q2",
          "reason": "판단 기준을 글로 쓸 수 없으므로(no) 실행 시 AI 보조 + 사람 확인으로 분류. 자동 승인은 하지 않는다."
        },
        {
          "kind": "human_gate",
          "assignee": "④",
          "rule": "GATE",
          "reason": "틀리면 외부에 영향이 있으므로(external) 이 단계 뒤에 사람 게이트를 둔다."
        }
      ]
    },
    {
      "number": 7,
      "name": "최종 확인·서명·제출",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "④",
          "rule": "SIGNOFF",
          "reason": "사람이 최종 확인·서명하는 단계이므로(yes) 사람이 하는 것으로 분류."
        }
      ]
    }
  ]
}
```

## 8. 테스트로 옮기는 방법 (test-engineer 참고)

- 폴더 `tests/Scorecard.Tests/Classification/`, 네임스페이스 `Scorecard.Tests.Classification`. Trait `Spec` = `classification`.
- 표 읽기는 기존 `SpecDocument.Table(relativePath, header)`를 쓴다(경로 상수 `docs/spec/classification.md` 추가). 스파이크 S8이 같은 규칙으로 이 문서의 표 4개를 읽었다 [P:spec tables].

| 테스트 | 확인 | 카드 수용 기준 |
| --- | --- | --- |
| 조건 표(4.2) | 머리 행 `\| 근거 키 \| 키 \| 비교 \| 값 \|`. 행마다 선택지 전부로 걸림·안 걸림. `<` 행은 값−1 걸림, 값 안 걸림. 앞 분기를 피한 기준 답(6.1)에서 키 하나만 바꾼다 | 1, 지시문 13절 |
| 분기 예시(6.1) | 표의 행마다 기대 행. 분기 7개(Q1~Q4, ELSE, PARTIAL, GATE) 모두 1개 이상 | 1 |
| 순서 | 6.1 아래쪽 8행(처음 걸리는 곳에서 멈춤, 부가 행 순서) | 1 |
| 문장 틀(4.5) | 머리 행 `\| 근거 키 \| 담당 \| 행 종류 \| 문장 틀 \|`, `\| 키 \| 값 \| 문구 \|`. 조합 2,160개 행 3,768개의 문장·담당·종류가 표로 만든 것과 같다 | 2 |
| 3.3 예시 문장 | 지시문 3.3의 `예: "…"` 문장과 같다 | 2 |
| 기본값 표시(4.6) | 머리 행 `\| 근거 키 \| 읽은 답 \|`. `Used`가 표와 같다. 6.2 표 각 행. 답 없는 단계(6.2 블록). 표시 문자열은 정확히 ` (기본값 사용, 확인 필요)`이고 문장 끝에 한 번 | 2 |
| 확인 항목(4.7), AI 과잉 후보(4.8) | 4.2 `CHECK_EXISTING`·`OVERUSE` 행, `minutes_per_item` null | — |
| JSON 바이트(3.3) | BOM 없음, CR 없음, 끝 `\n`, 속성 순서, 이스케이프 표 | — |
| 골든 | `examples/<예제>/answers.yaml` → `CaseParser.Parse(bytes, "answers.yaml")` → `Classify` → `Render`가 `expected/classification.json`과 바이트까지 같다. 예제 3종 | 3 |
| 스펙 블록 = 예제 파일 | 7.2~7.4 블록과 `examples/` 파일이 바이트까지 같다 | 3 |
| 예제 줄바꿈 | `examples/` 텍스트 파일에 `\r`이 없다(`TestData`의 기존 테스트와 같은 방식). `examples/.gitattributes` 존재 | 3 |
| 문화권 | de-DE로 바꿔도 같은 바이트(`try/finally`) | — |
| G2·G3 | `GuardTargets`에 `classifier-case-valid`(4.10). `Real_targets_use_case_samples_not_basic` 수정 | — |
| T-008 서명 단계 | 4.2·4.5·4.6 표의 `SIGNOFF` 행, 6.1 마지막 7행, 6.2 `human_signoff` 5행. 분기 8개 모두 1개 이상. 조합 4,320개 행 5,928개. `SIGNOFF` 뒤 게이트·예외 검토 행 없음 | T-008 |
| T-008 추정 표시 | 4.12 규칙. 표시 문자열은 정확히 ` (AI 추정, 확인 필요)`이고 기본값 표시 뒤. 6.5 표본(TestData) 블록 = 파일, 분류 결과 = 6.5 결과 블록. JSON에 `inferred` 속성 없음 | T-008 |
| T-008 예제 | 7.2~7.4 `answers.yaml` 블록 = `examples/` 파일(`human_signoff: no`). 골든 바이트 그대로 | T-008 |

- 표 이름(머리 행)이 이 문서에 정확히 한 번 나와야 한다. `SpecDocument.Table`이 그렇게 검사한다 [읽음].
- 실패 테스트 먼저: test-engineer 단계에서는 `Scorecard.Core.Classification`이 없어 새 테스트가 컴파일되지 않는다. T-003 승인 결정 14와 같이 이 상태를 "실패하는 테스트"로 보고, 같은 브랜치에서 core-dev가 구현한다.
- 골든 테스트는 `examples/` 파일이 있어야 돈다. 파일이 없으면 실패해야 한다(빈 통과 금지).

## 9. 미확인 목록

1. GitHub ubuntu·windows 러너에서 분류·골든·G2·G3 테스트가 녹색인지. windows 러너의 `core.autocrlf`(T-002 미확인 그대로).
2. publish 단일 exe(`win-x64`)에서 `System.Text.Json`·`JavaScriptEncoder` 동작.
3. (해결) `ClassificationJson.FileName` 상수(4.11). 구현·테스트로 확인.
4. 12.1 7단계 차이를 메울 질문·규칙(6.3). 사람 판단.
5. M2 `interview --answers`가 이 `answers.yaml` 모양을 그대로 쓸 수 있는지. 지시문 3.1은 "입력은 무엇, 거기서 무엇을, 결과는 어디로"로 단계를 만든다고 한다 [읽음]. 그 질문의 답을 파일에 둘지는 M2에서 정한다.
6. 평가서가 확인 항목·기본값을 어떤 문구로 보일지.
7. Windows에서 만든 classification.json의 SHA-256이 7.2~7.4에 적은 값과 같은지. 같은 바이트면 같아야 한다.
8. (T-008) 평가서·화면이 추정 표시와 서명 단계를 어떤 문구로 보일지. 평가서 카드(`report.md`).
9. (T-008) 스킬 `/scorecard`가 `inferred`를 실제로 어떻게 채우는지. 스킬 카드(M2-7).

## 10. 스파이크 단계 목록

`spikes/T-004/run-spike.sh <작업폴더> <로그폴더>`. 커밋된 HEAD를 작업 폴더에 clone하고 `spikes/T-004/`를 복사해 변형을 넣는다. 저장소의 `src/`, `tests/`, `examples/`는 건드리지 않는다. 같은 스크립트를 두 번 돌려 결과 해시가 같았다. `summary.txt`는 마지막 실행이다.

| 단계 | 한 일 |
| --- | --- |
| S0 | clone, 스파이크 파일 복사 |
| S1 | 기준선 Release `-warnaserror` 빌드, 전체 테스트 |
| S2 | Core에 분류 후보 3파일 → 빌드 → G1 → `dotnet format --verify-no-changes` → 전체 테스트 |
| S3 | 스파이크 도구 빌드. 기준선·후보 Core의 참조 목록. 예제 초안 3종, table-compare 5단계 변형, 12.1 합성 사본, guard `case-valid` 분류. 결정론(다른 프로세스, 다른 경로, de-DE 프로세스) |
| S4 | 메모리 안 실험 `PROBE`: 분기, 순서, 기본값 표시, 문장, AI 과잉 후보, 이스케이프, 문화권 6종, 줄바꿈 |
| S5 | `GuardTargets`에 `classifier-case-valid` → G2·G3 → 전체 테스트 |
| S6 | 분류 어댑터가 절대 경로를 쓰게 바꿈 → G2 |
| S7 | `core.autocrlf=true` clone에서 예제 파일 바이트(`.gitattributes` 유무), CRLF `answers.yaml` 분류 |
| S8 | 이 문서(작업 트리)의 조건·문장 틀·문구·읽은 답 표를 읽어 후보와 대조 |

- `summary.txt`의 S5(`Real_targets_use_case_samples_not_basic` 1건)와 S6(음성 대조)의 `rc=1`은 기대한 실패다(4.10).
- T-008 스파이크는 `spikes/T-008/run-spike.sh`다. 단계는 `human-signoff.md` 10절.

---

## 사람 승인 필요

질문마다 prototyper의 제안을 적었다. "제안대로"로 답할 수 있다. 질문 14는 예제 정답 확정이라 사람이 직접 봐야 한다.

1. **행 모델: ④ 게이트·예외 검토는 "분류"인가 "부가 단계"인가.** 12절의 "최종 확인 = ④", "발송 = ④"는 트리로 사용자 단계에서 낼 수 없다. 부가 행으로 두면 12절과 맞는다(4.3, 6.4).
   - 제안: 모델 A. `PARTIAL`(예외 건 사람 검토)과 `GATE`(사람 게이트)를 부모 단계 바로 뒤의 부가 행(④)으로 둔다. 순서는 단계 → 예외 검토 → 게이트. 12절 "단계 수"는 행 수로 센다. Q2의 "사람 확인"은 ③의 뜻에 들어 있어 행을 더하지 않는다.
2. **Q3 결과 표현.** 원문은 "입력 양식 표준화 먼저 검토, 안 되면 ③"이다(4.1).
   - 제안: 담당 ③, 근거 키 `Q3`. "입력 양식 표준화를 먼저 검토하고, 안 되면 실행 시 AI 보조"는 근거 문장에 적는다. 따로 필드를 두지 않는다. 12절 "③(입력 양식 표준화 제안)"과 같은 뜻이다.
3. **Q4 결과 표현.** "그대로 두거나 일회성 AI 보조"는 ①~④ 어디에도 딱 맞지 않는다. `current_actor: program`이어도 ④가 된다(5절). 예제 3종·12.1에 Q4 단계는 없다.
   - 제안: 담당 ④, 근거 키 `Q4`. 문장에 "그대로 두거나 일회성 AI 보조로 분류. 개발비가 더 크다."를 적는다. ①~④ 밖의 새 값(예: "현행 유지")은 만들지 않는다.
4. **12절·12.1과 트리의 어긋남.** (a) 12절에서 ④만 맡는 단계("최종 확인", "발송")는 게이트 행으로만 나온다(질문 1). (b) 12.1 7단계 "최종 확인·제출"은 사람 판단 ④, 트리 ③ + ④ 게이트다(6.3). 9개 질문에 "사람이 책임지고 확인·제출하는 단계"를 가를 질문이 없다.
   - 제안: T-004는 3.3 트리를 그대로 구현한다. (a)는 질문 1의 해석으로 맞춘다. (b)는 12.1 규정대로 예제를 고치지 않는다. prototyper가 `docs/decisions/0003-…md` ADR 초안(상태: 제안)에 차이와 후보(새 질문 또는 규칙)를 적어 사람에게 묻는다. 트리를 바꾸게 되면 별도 카드에서 공개 합성 예제를 더한다.
5. **근거 문장.** 4.5의 문장 틀 7개와 문구 표 13행. 문장에는 그 잎을 정한 답만 쓴다. 지시문 3.3 예시 문장이 바이트까지 그대로 나온다(그래서 빈도 문구에는 값 코드를 붙이지 않았다).
   - 제안: 4.5 그대로. 문구를 바꾸려면 이 표와 예제 `expected`를 같이 고치고 PR 본문에 이유를 쓴다.
6. **기본값 표시 범위.** 카드 수용 기준 2 "기본값이 쓰인 답". 9개 중 하나라도 기본값이면 표시하는 안은, `minutes_per_item`을 안 적은 12.1·예제의 모든 문장에 표시가 붙는다.
   - 제안: 4.6 그대로. 그 행을 정할 때 트리가 읽은 답(4.6 표. `failure_impact` 포함, `batch_size`는 `rare`일 때만) 중 기본값이 있으면 문장 끝에 ` (기본값 사용, 확인 필요)`를 붙인다. 9개 중 기본값인 키는 모두 `defaults`에 남긴다.
7. **확인 항목.**
   - 제안: `existing_feature`가 `unknown`이면(직접 썼든 기본값이든) `confirm_existing_feature`, `minutes_per_item`이 null이면 `measure_minutes_per_item`. 화면·평가서 문구는 평가서 카드에서 정한다.
8. **AI 과잉 후보를 T-004에 넣는가.** 카드 수용 기준에는 없다(지시문 13절 "카드에 없는 기능을 만들지 않는다"). 분류만으로 정해지고, 12절 form-fill 기대값에 있다.
   - 제안: 넣는다. 단계마다 `ai_overuse_candidate`(`current_actor: ai_each_time`이고 단계 행이 ②). 판정 카드는 이 값을 "AI가 꼭 필요했나"에 쓴다. 리드가 카드 수용 기준에 한 줄 더한다.
9. **분류 숫자·값을 어디서 읽나.**
   - 제안: T-003 승인 결정 5와 같은 방식. Core 상수 한 파일 + 테스트가 이 문서의 4.2 조건 표, 4.5 문장 틀·문구 표, 4.6 읽은 답 표를 읽어 대조한다. 실행 중에는 문서를 읽지 않는다. `case.yaml` `thresholds`에 분류 숫자를 넣지 않는다.
10. **classification.json 형식.**
    - 제안: 3.2·3.3 그대로. Core가 `Utf8JsonWriter`로 바이트를 만든다(`System.Text.Json`은 프레임워크 어셈블리라 G1 통과). 근거 키와 근거 문장을 모두 넣는다. 경로·입력 해시·`Used`는 넣지 않는다. 골든 테스트는 바이트로 비교한다.
11. **`examples/.gitattributes`.** T-002 승인 결정 10이 T-005로 미뤘지만, 예제 파일을 처음 넣는 카드가 T-004가 됐다(7.1).
    - 제안: T-004에서 `examples/.gitattributes`(`* -text`)를 넣고, `examples/` 텍스트 파일에 `\r`이 없는지 보는 테스트를 더한다.
12. **G2·G3 등록과 기존 테스트 한 건.**
    - 제안: `classifier-case-valid`(샘플 `case-valid`, 출력 `classification.json`)를 `GuardTargets`에 더한다. `Real_targets_use_case_samples_not_basic`는 샘플 목록을 중복 없이 비교하도록 고친다. 뜻("실제 대상은 `guard/basic`을 쓰지 않는다")은 그대로다. PR 본문에 적고 reviewer가 확인한다.
13. **`answers.yaml` 형식.**
    - 제안: `case.yaml` 스키마의 부분(`name`, `task`, `steps`). 골든 테스트는 `CaseParser.Parse(bytes, "answers.yaml")`로 읽는다. M2 `interview --answers`가 다른 모양을 원하면 그 카드에서 바꾸고 예제를 같이 고친다.
14. **예제 3종 `answers.yaml`·`expected/classification.json` 확정 (사람이 직접 확인).** 7.2~7.4 블록은 초안이다. 확인할 점:
    - table-compare: 사용자 단계 4개(정상 신호 구간 선택, 불규칙 신호 구간 선택 `partial`, 구간 평균 계산, 결과표 채우기 `external`) → 행 6. "최종 확인 = ④"는 결과표 채우기 뒤 게이트 행이다.
    - form-fill: 단계 4개. 1단계 "주문서 파일 모으기"는 12절 요지에 이름이 없다. `existing_feature`를 적지 않아 기본값(unknown) → 확인 항목과 기본값 표시. 2단계 `current_actor: ai_each_time` → AI 과잉 후보. "발송 = ④"는 주간 보고서 뒤 게이트 행 → 행 5.
    - mixed-exceptions: 단계 4개. 4단계 "처리 결과 기록"(②)은 12절 요지에 없다. 분류·답변은 `free_doc`·`partial` → Q3. "발송 = ④"는 답변 초안 뒤 게이트 행 → 행 5.
    - 셋 다 `minutes_per_item`을 적지 않았다(모든 단계에 측정 권고).
    - 옮기기: 확정 뒤 test-engineer가 블록을 바이트 그대로 `examples/<예제>/answers.yaml`, `expected/classification.json`으로 옮긴다. CLAUDE.md 에이전트 폴더 제한에 `examples/`가 없어 이 카드에 한해 허용이 필요하다. 테스트가 블록과 파일이 같은지 본다.
    - 제안: 위 초안대로 확정하고, test-engineer가 옮기는 것을 허용한다. 고칠 곳이 있으면 단계·답을 적어 주면 prototyper가 스파이크를 다시 돌려 블록을 맞춘다.
15. **Core 공개 API와 진행 순서.**
    - 제안: 4.11 그대로(`StepClassifier.Classify`, `ClassificationJson.Render`, 결과 레코드 3개, 상수는 `internal` 한 파일). test-engineer 단계의 컴파일 실패를 "실패하는 테스트"로 보고, 같은 브랜치에서 core-dev가 구현한다(T-003 승인 결정 14와 같음). PR 본문에 적는다.

## 승인 결과

- 2026-11-07. juhan의 상시 승인("끝날때까지 추천값으로 머지하고 진행해 계속")에 따라 1~15 모두 제안대로.
- 1: 행 모델 A. 2·3: Q3 = ③, Q4 = ④, 설명은 근거 문장에. 4: 트리는 3.3 그대로. 12.1 7단계 차이는 ADR 0003 초안(상태: 제안)으로 묻는다.
- 5·6·7: 4.5·4.6·4.7 그대로. 8: AI 과잉 후보를 넣는다. 리드가 카드 수용 기준 5를 더했다.
- 9·10: Core 상수 한 파일 + 스펙 표 대조 테스트. classification.json은 3.2·3.3 그대로.
- 11: `examples/.gitattributes`(`* -text`)를 T-004에서 넣는다.
- 12: `classifier-case-valid` 등록. `Real_targets_use_case_samples_not_basic`는 중복 없는 목록 비교로 고친다. 뜻은 같다. PR 본문에 적는다.
- 13: `answers.yaml`은 case.yaml 스키마의 부분.
- 14: 7.2~7.4 블록을 정답으로 확정. 이 카드에 한해 test-engineer가 `examples/`에 블록을 바이트 그대로 옮긴다.
- 15: 4.11 API 그대로. test-engineer 단계의 컴파일 실패를 실패 테스트로 본다.

## T-008 개정 (승인됨, 2026-11-07)

- 작업 카드 `docs/tasks/T-008-human-signoff.md`. 결정 출발점: ADR 0003 "결정"·"규칙"·"결정이 같이 바꾸는 곳", ADR 0004 결정 2·질문 2 [읽음].
- 확인 대상 커밋 `32f7692`. 스파이크 `spikes/T-008/`(머지 대상 아님). 결과와 질문은 `docs/spec/human-signoff.md`.
- 바꾼 곳: 0절·2절 글머리, 3.2(`rule` 값, `Inferred` 글머리), 4.1(해석 표 `SIGNOFF` 행, `GATE` 행), 4.2(조건 표 첫 행 `SIGNOFF`), 4.3·4.4, 4.5(문장 틀 표 첫 행), 4.6(읽은 답 표 `SIGNOFF` 행, Q1~ELSE 행에 `human_signoff`), 4.7·4.8·4.9·4.10·4.11 글머리, 새 4.12, 5절 표 5행, 6.1 기준 답 줄·표 6행, 6.2 표 5행·블록 `10개`, 6.3·6.4 글머리, 새 6.5, 7.1 항목 7, 7.2~7.4 `answers.yaml` 블록, 8절·9절.
- 바꾸지 않은 곳: 7.2~7.4 `expected/classification.json` 블록·SHA-256 줄, 7.1 요약 표, 3.3 바이트 규칙, 4.5 문구 표, 행 모델 A.
- 위 승인 결과 1~15는 그대로다. `SIGNOFF`는 승인 결과 4("12.1 7단계 차이는 ADR로")의 답이다(ADR 0003 승인됨).

## T-009 개정 (초안, 사람 승인 전)

- 작업 카드 `docs/tasks/T-009-signoff-final.md`. 출발점: ADR 0003 남은 질문 2(이름 `signoff-final`, T-008 다음 카드. 지시문 2절 11과의 순서 차이는 juhan 승인) [읽음].
- 확인 대상 커밋 `ce6c4f0`. 스파이크 `spikes/T-009/`(머지 대상 아님). 근거·질문·결과는 `docs/spec/signoff-final.md`.
- 바꾼 곳: 머리 글머리, 0절 표기, 6.3·6.4 글머리, 7.1 항목 8·요약 표 1행·글머리, 새 7.5.
- 바꾸지 않은 곳: 규칙(1~5절 표와 글), 6.1·6.2·6.5, 7.2~7.4 블록·SHA-256 줄, 8절 표, 행 모델 A.
- 요약 표에 `SIGNOFF` 열을 더하지 않았다. 머리 행을 바꾸면 테스트 상수와 기존 3행이 같이 바뀐다. `SIGNOFF` 한 행은 새 테스트가 본다(`signoff-final.md` 5.3).
