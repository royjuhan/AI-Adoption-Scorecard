# human-signoff (T-008 스펙: 서명 단계와 추정 답 표시)

- 상태: 승인됨(2026-11-07). 승인 결과는 문서 끝.
- 반영한 사람 결정: ADR 0003 "결정"(2026-11-07 juhan, 후보 1)과 남은 질문 1~5(제안대로 승인). ADR 0004 결정 2(`inferred: [키 목록]`), 질문 2(추정 표시는 TestData로), 질문 16 해석 A(T-008이 `inferred`까지 맡는다) [읽음].
- 작업 카드: `docs/tasks/T-008-human-signoff.md`
- 작성: prototyper, 2026-11-07
- 확인 대상 커밋: `32f7692`(main). 스파이크는 이 커밋을 clone하고 작업 트리의 스펙 세 문서와 `spikes/T-008/`를 복사해 돌렸다(`summary.txt`의 `repo HEAD` 줄).
- 확인 환경: Ubuntu 24.04 x64, .NET SDK 10.0.114, YamlDotNet 18.1.0, xUnit 2.9.3. 보조 스크립트에 Python 3.13.18.
- 스파이크: `spikes/T-008/` (머지 대상 아님. 머지 전에 빠진다). 실행은 `spikes/T-008/run-spike.sh <작업폴더> <로그폴더>`. 요약 원문은 `spikes/T-008/summary.txt`.

## 0. 표기

- `[읽음]`: 커밋된 파일을 읽어서 확인한 사실.
- `[T8:S0]`~`[T8:S8]`: 스파이크 단계(10절). `[T8:P:무리]`: S4 `PROBE <무리>` 줄.
- **미확인**: 실험하지 않았거나 이 환경에서 확인할 수 없는 것.
- 규칙 본문은 이 문서에 다시 적지 않는다. `case-schema.md`와 `classification.md`의 "T-008 개정"이 본문이다. 이 문서는 결과, 기존 테스트 변경, 지시문 문구, 질문을 모은다.

## 1. 먼저: 예제 expected 바이트

- **`expected/classification.json` 3개는 바이트 그대로다.** 예제 `answers.yaml`·`case.yaml`의 모든 단계에 `human_signoff: no`를 넣고 후보 Core로 분류했다. 6개 파일 모두 expected와 `cmp`가 같다 [T8:S3 classify].

| 예제 | expected SHA-256 | 바이트 | `answers.yaml` → | `case.yaml` → |
| --- | --- | --- | --- | --- |
| `table-compare` | `44e64b8d…` | 2,642 | 같음 | 같음 |
| `form-fill` | `b1898a85…` | 2,488 | 같음 | 같음 |
| `mixed-exceptions` | `46bd9f0a…` | 2,464 | 같음 | 같음 |

- `expected/metrics.json` 3개도 바이트 그대로다(바꾼 `case.yaml`로 대조) [T8:S3 classify].
- 키를 넣지 않으면 그대로가 아니다. 지금(HEAD) `answers.yaml`을 후보 Core로 분류하면 `human_signoff`가 기본값이 되어 모든 `step` 행에 기본값 표시가 붙는다. table-compare 2,878바이트(expected 2,642), form-fill 2,690(2,488), mixed-exceptions 2,700(2,464). 셋 다 `step` 행 4개에 표시가 붙는다 [T8:S3 head-answers]. 그래서 예제 파일에 키를 적는 것이 필요하다(ADR 0003 결정이 정한 대로).
- 예제 파일 변경은 `answers` 줄 끝에 `, human_signoff: no`를 더한 것뿐이다. 파일마다 4줄, 모두 `    answers: {` 줄이다. 그 밖의 예제 파일(CSV, `intake.md`, `expected/`)은 바뀌지 않는다 [T8:S3 examples-diff].
- 바뀐 SHA-256(스펙 블록 줄에 반영): `case.yaml` table-compare `451a22ac…`, form-fill `9660f3cb…`, mixed-exceptions `96f04387…`(synthetic-examples.md 7.2~7.4). `answers.yaml`은 SHA-256 줄이 없다(classification.md 7.2~7.4 블록만).

## 2. 범위

카드 수용 기준 1~4를 다룬다. 5(ubuntu·windows CI)는 PR에서 확인한다.

| 카드 수용 기준·메모 | 본문 | 이 문서 | 승인 질문 |
| --- | --- | --- | --- |
| 1 `SIGNOFF` 분기, `yes` 단계에 게이트 없음, 트리 표·문장 틀 표·읽은 답 표 | classification.md 4.1~4.6, 6.1 | 3.2, 7절 | 리드 3, 4 |
| 2 예제 3종에 `human_signoff: no`, expected 그대로 | classification.md 7.1·7.2~7.4, synthetic-examples.md 7.1~7.4 | 1절 | juhan 3 |
| 3 `inferred` 파싱 오류·추정 표시(TestData). 예제에는 없음 | case-schema.md 4.13·6.7, classification.md 4.12·6.5 | 3.3, 5절 | 리드 1, 5, 6, 7 |
| 4 시작 전 1066, 끝난 뒤 그 이상, 삭제·Skip 없음 | — | 4절, 7절 | juhan 1 |
| 메모: 공개 API 변경, 기존 테스트 변경은 juhan 직접 | case-schema.md 4.12, classification.md 4.11 | 4절 | juhan 1 |
| 메모: 지시문 2절 6과 "늘 명시" | case-schema.md 4.4 글머리 | 6절 | juhan 2 |
| 지시문 3.1·3.2·3.3(리드가 고침) | — | 6절 | 리드 8 |

범위 밖:

- 4번째 예제 `signoff-final`(12.1 구조). 다음 카드(ADR 0003 남은 질문 2).
- 명령(`start`의 서명 단계 질문 화면, `--answers`), 스킬 `/scorecard`. M2.
- 스코어카드에서 추정·서명을 어떻게 보일지. `report.md`.
- classification.json에 `inferred`를 쓰는 안. ADR 0004 "엔진이 추정 답을 다루는 법"의 추천대로 쓰지 않는다(리드 5).

## 3. 바뀌는 규칙 (가리키기)

### 3.1 `human_signoff` (case-schema 4.4)

- 4.4 `answers` 표 끝에 1행: `human_signoff`, "사람이 최종 확인·서명하는 단계인가", 열거, `yes / no`, 기본값 `no`.
- 파서 규칙은 다른 열거 답과 같다. 오류 코드 수(17)는 그대로다.
- `StepAnswers` 10번째 위치 매개변수 `HumanSignoff`. 공개 API 변경이다.

### 3.2 `SIGNOFF` (classification 4.1~4.6)

- 트리 맨 앞. `human_signoff == yes`면 ④ `step` 행 하나로 멈춘다. 예외 검토·게이트 행이 없다.
- 근거 문장: `사람이 최종 확인·서명하는 단계이므로(yes) 사람이 하는 것으로 분류.`(ADR 0003 초안 그대로).
- 읽은 답: `SIGNOFF`는 `human_signoff` 하나. `Q1`~`ELSE`는 지금 목록에 `human_signoff`를 더한다. `PARTIAL`·`GATE`는 그대로 키 하나다.
- 확인 항목은 그대로 달린다. AI 과잉 후보가 아니다.
- 4.5 조합: 2,160개 → 4,320개, 행 3,768개 → 5,928개 [T8:S2b].

### 3.3 `inferred` (case-schema 4.13, classification 4.12)

- 단계 항목의 선택 키 `inferred: [답 키 목록]`. 원소는 4.4 답 키 10개 중 하나(`enum`). 중복은 `duplicate`. 적은 키의 답은 `answers`에 값이 있어야 한다(`required`). 목록이 아니면 `type`.
- 목록의 답만 `Answer<T>.IsInferred = true`. `IsInferred`는 선택 매개변수(기본 false)라 기존 2인자 생성은 그대로 컴파일된다.
- 분류는 바뀌지 않는다. 읽은 답 중 하나라도 추정이면 문장 끝에 ` (AI 추정, 확인 필요)`. 기본값 표시가 같이 붙으면 그 뒤.
- `StepClassification.Inferred`(Defaults 바로 뒤). classification.json에는 쓰지 않는다.

### 3.4 예제와 스펙 블록

- 예제 6개 파일(`examples/*/answers.yaml`, `examples/*/case.yaml`): 모든 `answers` 줄 끝에 `, human_signoff: no`.
- 스펙 블록: classification.md 7.2~7.4 `answers.yaml` 블록, synthetic-examples.md 7.2~7.4 `case.yaml` 블록·SHA-256 줄. `spikes/T-008/fill-blocks.py`가 바꾼 예제 파일로 채웠다.
- 바꾸지 않는 블록: classification.md `expected/classification.json` 블록·SHA-256 줄, case-schema 6.1 블록(= `TestData/case-schema/valid-full` = `TestData/guard/case-valid`).

## 4. 기존 테스트 변경 (juhan 직접 승인)

### 4.1 바꾸지 않으면 깨지는 것

- 후보 Core + 기존 테스트: 컴파일 오류 3건(CS7036) [T8:S2a].
  - `CaseSchema/CaseApiShapeTests.cs`(51행): `StepAnswers`에 `HumanSignoff` 인자가 없다.
  - `Classification/ClassificationApiShapeTests.cs`(64행): `StepClassification`에 `Inferred` 인자가 없다.
  - `Classification/ClassificationInput.cs`(167행): `StepAnswers` 시작 값에 10번째 인자가 없다.
- 컴파일만 고치고 문서·예제를 HEAD 그대로 두면 1,066개 중 40개가 실패한다 [T8:S2a]. 골든(`ExampleGoldenTests` 18개, `CaseYamlTests` 3개)이 기본값 표시 때문에 깨진다. 기준 답(6.1)에 `human_signoff`가 없어 기본값 표시가 붙으므로 `DefaultMarkTests` 15개, `ReasonSentenceTests` 2개, `ClassificationJsonTests` 1개, `ClassifierGuardTargetTests` 1개도 깨진다. 이것이 예제 파일과 6.1 기준 답을 같이 바꾸는 이유다.

### 4.2 바꾸는 테스트 10파일

모두 `tests/Scorecard.Tests/` 아래. 스파이크 변형은 `spikes/T-008/tests-variant/`에 있다. 지운 테스트·Skip·기대값을 느슨하게 한 곳은 없다.

| 파일 | 바꾸는 곳 | 왜 약화가 아닌가 |
| --- | --- | --- |
| `CaseSchema/CaseApiShapeTests.cs` | `StepAnswers` 생성에 `HumanSignoff: new Answer<string>("no", true)` | 컴파일. 새 위치 매개변수의 이름·순서를 같이 검사하게 된다(강화) |
| `CaseSchema/CaseDefaultsTests.cs` | 답 목록에 `human_signoff` 줄, 주석 9 → 10 | 검사 대상이 하나 늘었다. 기대값은 스펙 4.4 표에서 읽는다 |
| `CaseSchema/SpecDocumentTests.cs` | 키 목록에 `human_signoff`, 형식 목록에 `열거` 하나, 이름 `Defaults_table_lists_nine_…` → `…_ten_…` | 표가 10행이 됐다. 이름은 뜻을 맞춘 것이다. 지시문 3.2 표 대조(`Defaults_table_matches_instructions_3_2`)는 리드가 지시문 3.2에 행을 더해야 통과한다(6절) |
| `Classification/BranchExampleTests.cs` | 분기 목록에 `SIGNOFF` | 6.1 표가 덮어야 할 분기가 7 → 8(강화). 테스트 이름 `…_seven_branches`는 그대로 두었다(바꿀지는 juhan 1) |
| `Classification/ClassificationApiShapeTests.cs` | `StepClassification` 생성에 `Inferred: []` | 컴파일. 위치 매개변수 순서를 같이 검사(강화) |
| `Classification/ClassificationInput.cs` | `With`에 `human_signoff`, 조합에 서명 2값(2,160 → 4,320), 시작 값 10개 | 조합이 두 배가 됐다. 기준 답은 스펙 6.1 줄에서 읽는다 |
| `Classification/ClassifierGuardTargetTests.cs` | 기본값 표시 행 수 3 → 5 | G2·G3 표본 `case-valid`(= case-schema 6.1 블록)를 바꾸지 않아서다. 표본에 `human_signoff`가 없어 1·2단계 `ELSE` 행에도 표시가 붙는다. 숫자는 더 커졌고 같은 `Assert.Equal`이다. 대안은 juhan 1 |
| `Classification/ConditionTableTests.cs` | 조건 표 근거 키 목록 맨 앞에 `SIGNOFF` | 표 행이 하나 늘었다 |
| `Classification/DefaultMarkTests.cs` | 행 수 3,768 → 5,928, 블록 문구 `9개` → `10개`, YAML 한 줄에 `human_signoff: no` | 조합이 늘어 행 수가 늘었다. YAML은 "직접 쓴 값이면 표시가 없다"는 테스트라 모든 답을 직접 써야 뜻이 맞다 |
| `Classification/ReasonSentenceTests.cs` | 조합 4,320·행 5,928, 틀 목록 맨 앞 `SIGNOFF`, 이름 `Template_table_has_seven_rules` → `…_eight_rules` | 틀이 8개가 됐다. 이름은 뜻을 맞춘 것이다 |

- 바뀐 뒤 전체 테스트 1,082개 통과(기준선 1,066보다 16개 많다. 표에서 읽는 Theory 행이 늘었다: 6.1 표 7, 6.2 표 5, 4.2 조건 표 1, case-schema 4.4 열거·null 행 3) [T8:S2b]. 새 테스트(test-engineer, 8절)는 이 수에 들어 있지 않다.
- 이름을 바꾸는 테스트 2개는 xUnit에서 "지운 테스트 + 새 테스트"로 보인다. 같은 검사를 하므로 삭제로 보지 않는다. PR 본문에 적는다.

## 5. TestData 표본 (새, test-engineer가 옮긴다)

| 표본 | 블록 | 내는 것 | 확인 |
| --- | --- | --- | --- |
| `TestData/case-schema/inferred-errors/case.yaml` | case-schema.md 6.7 | `enum` 3, `duplicate` 1, `required` 3, `type` 1 | 블록의 오류 8줄 = 후보 파서 [T8:S8 inferred-errors] |
| `TestData/classification/signoff-inferred/answers.yaml` | classification.md 6.5 | `SIGNOFF`, 두 표시가 같이 붙는 행, 부가 행 추정 표시, 서명 단계 뒤 게이트 없음, `human_signoff` 기본값, AI 과잉 후보 | 블록의 행 10줄 = 후보 분류 [T8:S8 signoff-inferred]. JSON에 `inferred` 없음, `inferred`를 뺀 파일과 `reason` 4줄만 다름 [T8:S8 json-diff] |

- `TestData/classification/`은 새 폴더다. 표본은 실제 값이 없는 합성 값이다.
- G2·G3 대상은 더하지 않는다(리드 7). `classifier-case-valid`가 같은 분류기를 이미 돈다.

## 6. 지시문 문구 (리드가 사람 결정 뒤 고친다)

스파이크가 clone의 지시문에 아래 문구를 넣고 기존 테스트를 돌렸다. 통과했다 [T8:S2b instructions]. 지시문 3.2 표 대조와 3.3 근거 문장 줄 테스트가 이 문구를 읽는다.

### 6.1 2절 6 (juhan 직접, 승인 질문 juhan 2)

지금: "정해진 질문으로 답을 받고, 모르면 기본값을 넣고 '기본값 사용, 확인 필요'로 표시한다. … 모르는 답은 비워 두어 기본값 표시가 남게 한다" [읽음]. ADR 0003 결정은 "`human_signoff`는 기본값으로 남기지 않는다"다. 둘이 맞부딪친다.

- 안 A(추천): 2절 6 끝에 한 문장을 더한다.
  - `단, `human_signoff`는 비워 두지 않는다. 서명 단계를 한 번 묻고 모든 단계에 `yes`/`no`를 적는다. 아무 단계도 고르지 않으면 모두 `no`다(2026-11-07 사람 결정: ADR 0003).`
  - CLAUDE.md 절대 원칙의 같은 줄("엔진·exe는 자유 서술을 저장만, …")에도 짧게 더한다: `human_signoff는 서명 단계를 한 번 물어 모든 단계에 적는다(ADR 0003).` CLAUDE.md는 리드가 고친다.
- 안 B: 2절 6은 그대로 둔다. "서명 단계가 없다"도 답이라 "모르는 답"이 아니라고 읽는다. 3.1·3.2 문구만 고친다. 원칙 글과 실제 동작 사이의 해석이 남는다.
- 어느 안이든 엔진 동작은 같다. 키가 없는 파일은 기본값 `no` + 기본값 표시다(case-schema 4.4).

### 6.2 3.1 (`scorecard start` 행)

- 지금: `끝에 "사람이 최종 확인·서명하는 단계가 어디인가"를 한 번 묻는다.` [읽음].
- 제안: 그 뒤에 `고른 단계는 `human_signoff: yes`, 나머지는 `no`로 적는다. 아무 단계도 고르지 않으면 모두 `no`다.`를 더한다. "단계마다 질문 9개"는 그대로다(10번째는 단계마다 묻지 않는다).

### 6.3 3.2

- 제목: `### 3.2 단계마다 적는 답 10개와 기본값`.
- 표 끝 행: `| `human_signoff` | 사람이 최종 확인·서명하는 단계인가 | yes / no | no (단계마다 묻지 않는다. 3.1의 서명 단계 질문으로 채운다) |`. 질문 칸·선택지·기본값 첫 낱말이 case-schema 4.4와 같아야 테스트가 통과한다.
- 표 아래: `앞의 9개는 단계마다 묻는다. `human_signoff`는 3.1의 서명 단계 질문으로 한 번 묻고 모든 단계에 값을 적는다(2026-11-07 사람 결정: ADR 0003).`

### 6.4 3.3

- 트리 블록 첫 줄: `SIGNOFF human_signoff == yes         → ④ 사람이 최종 확인·서명 (뒤에 게이트를 붙이지 않는다)`.
- 끝 줄: `항상(SIGNOFF 제외): failure_impact in {external, safety_legal} 이면 → 해당 단계 뒤에 ④ 사람 게이트 추가`.
- 근거 문장 줄(`분류마다 근거 문장을 템플릿으로 만든다.`로 시작)은 바꾸지 않는다. 테스트가 이 줄의 큰따옴표 두 쌍을 읽는다.
- 그 아래 새 문단: `AI가 추정한 답(단계의 `inferred`)이 쓰인 근거 문장 끝에는 `(AI 추정, 확인 필요)`를 붙인다. 기본값 표시가 같이 있으면 그 뒤에 둔다(2026-11-07 사람 결정: ADR 0004).` 큰따옴표를 쓰지 않는다.

### 6.5 고치지 않는 곳

- 12.1 블록(비공개 예시). `SpecDocumentTests.Example_12_1_sample_differs_from_instructions_only_in_names_and_sentences`가 TestData 합성 사본과 줄마다 같은지 본다. 블록에 `human_signoff`를 넣으면 사본도 같이 바꿔야 한다. 12.1 구조는 다음 카드 `signoff-final`이 공개 예제로 만든다. 그때 같이 정한다.
- 12절 표(`| 예제 | 업무 | 단계 수 | …`). 예제 3종의 단계 수·분류가 그대로다.

## 7. 스파이크 결과

| 단계 | 결과 |
| --- | --- |
| S1 기준선 | Release `-warnaserror` 빌드 0 경고, 전체 1,066개 통과 |
| S2a 후보 Core + 기존 테스트 | 컴파일 오류 3건. 컴파일만 고치면 40개 실패(4.1) |
| S2b 후보 + 테스트 변형 + 개정 문서 + 지시문 제안 + 예제 | 빌드 성공(`-warnaserror`, 경고 0). G1 61개 통과. `dotnet format --verify-no-changes` 변경 없음. 전체 1,082개 통과(실패 0, Skip 0). G2·G3 32개 통과 |
| S3 예제 | 1절. 다른 경로 사본, `LANG=de_DE.UTF-8` 프로세스에서도 같은 바이트. `answers.yaml`의 `name:` 줄부터 끝이 `case.yaml` 끝과 같다(synthetic-examples 4.8) |
| S4 메모리 실험 | `PROBE` 줄. 12.1 합성 사본 7단계 `yes`: 7단계가 ④ `SIGNOFF` 한 행, 1~6단계는 classification.md 6.3 표와 같다. 행 10 → 9. 키가 없는 HEAD 사본은 `step` 행 7개 모두 기본값 표시. guard `case-valid`: 행 7, 기본값 표시 5행(T-004 때 3행). 문화권 7종 SHA-256 하나 |
| S5 음성 대조 | 4종(아래). 되돌린 뒤 전체 1,082개, G2·G3 32개 통과 |
| S8 스펙 블록 | case-schema 6.7 오류 8줄 = 파서. classification 6.5 행 10줄 = 분류기. JSON 차이는 `reason` 4줄 |

음성 대조 [T8:S5]. 기존 테스트(변형 포함)가 무엇을 잡는지 본 것이다. 못 잡는 것은 test-engineer의 새 테스트가 맡는다(8절).

| 변형 | 실패한 기존 테스트 | 새 테스트가 필요한가 |
| --- | --- | --- |
| `SIGNOFF` 뒤에도 게이트 | 5개: `BranchExampleTests` 6.1 행 3개(게이트 없음 행), `ReasonSentenceTests.All_combinations_match_templates_and_phrases`(행 수), `DefaultMarkTests.Used_matches_spec_table_for_all_combinations`(행 수) | 아니오. 기존 표 대조가 잡는다 |
| `existing_feature: yes`면 Q1이 `SIGNOFF`보다 먼저 | 3개: `BranchExampleTests` 6.1 행 1개(`human_signoff: yes, existing_feature: yes`), 위 두 조합 테스트 | 아니오 |
| 추정 표시를 기본값 표시 앞에 | 0개 | 예. 기존 테스트에는 `inferred`를 쓰는 입력이 없다. 8절 추정 표시 테스트가 막는다 |
| `SIGNOFF`가 `failure_impact`도 읽음 | 2개: `DefaultMarkTests.Used_matches_spec_table_for_all_combinations`, `DefaultMarkTests.Mark_appears_exactly_on_rows_that_read_the_default_answer` | 아니오 |

## 8. 테스트로 옮기는 방법 (test-engineer 참고)

- 시작 시점: 이 스펙 승인 뒤(juhan 1·2·3 포함). 그 전에는 테스트·구현·`examples/`·TestData 파일을 만들지 않는다.
- 기존 테스트 변경은 4.2 표대로. 스파이크 변형(`spikes/T-008/tests-variant/`)을 출발점으로 쓸 수 있다.
- 새 테스트(제안). 폴더는 기존 것(`CaseSchema/`, `Classification/`).

| 테스트 | 확인 | 카드 수용 기준 |
| --- | --- | --- |
| `SIGNOFF` 분기 | classification.md 6.1 마지막 7행(기존 Theory가 표에서 읽는다). `yes`이면 다른 답 9개를 모두 바꿔도 ④ `SIGNOFF` 한 행, `Used` = `[human_signoff]`, 게이트·예외 검토 없음 | 1 |
| 표 대조 | 4.2·4.5·4.6 표의 `SIGNOFF` 행(기존 표 대조 테스트가 그대로 읽는다) | 1 |
| `inferred` 오류 | case-schema.md 6.7 표본 = 블록, 오류 줄 = 블록. 4.13 글머리마다 하나 이상 | 3 |
| `IsInferred` | 목록의 답만 true. 값·`IsDefault` 그대로. 쓴 순서와 상관없음 | 3 |
| 추정 표시 | classification.md 4.12 규칙. 표시 문자열 ` (AI 추정, 확인 필요)`가 문장 끝에 한 번, 기본값 표시 뒤. 4.12 예시 표의 행마다. 6.5 표본 = 블록, 분류 행 = 결과 블록 | 3 |
| JSON | `inferred`가 있어도 classification.json에 `inferred` 속성이 없다. 6.5 표본에서 `inferred`를 뺀 파일과 `reason`만 다르다 | 3 |
| 예제 | 7.2~7.4 블록 = `examples/` 파일, 골든 바이트(기존 테스트가 그대로 본다) | 2 |

- 실패 테스트 먼저: 기존 테스트의 컴파일 오류(4.1)와 새 테스트를 "실패하는 테스트"로 보고, 같은 브랜치에서 core-dev가 구현한다(T-003 승인 결정 14와 같음). 스파이크 후보(`spikes/T-008/core/`)는 출발점일 뿐이다.
- 끝난 뒤 통과 수는 1,066 이상이어야 한다(카드 4). S2b가 1,082이므로 새 테스트를 더하면 그 이상이다.

## 9. 미확인

1. ubuntu·windows CI 녹색(카드 5).
2. 지시문 6절 문구가 리드의 실제 수정과 같은지. 스파이크는 제안 문구로만 돌렸다.
3. 스킬·exe 마법사가 서명 단계 질문을 실제로 어떻게 묻는지(M2).
4. 스코어카드가 서명 단계·추정 표시를 어떻게 보이는지(`report.md`).
5. 12.1 합성 사본 7단계 `yes`의 결과(S4)는 공개 예제가 아니다. `signoff-final` 카드에서 다시 본다.

## 10. 스파이크 단계 목록

`spikes/T-008/run-spike.sh <작업폴더> <로그폴더>`. 커밋된 HEAD를 clone하고 `spikes/T-008/`를 복사한다. 저장소의 `src/`, `tests/`, `examples/`, 지시문은 건드리지 않는다.

| 단계 | 한 일 |
| --- | --- |
| S0 | clone, 스파이크 파일 복사 |
| S1 | 기준선 Release `-warnaserror` 빌드, 전체 테스트(HEAD 문서) |
| S2a | 후보 Core 6파일 → 기존 테스트로 빌드(컴파일 오류 확인) → 컴파일만 고친 3파일 → 전체 테스트(HEAD 문서·예제) |
| S2b | 테스트 변형 10파일, 개정 스펙 세 문서, 지시문 제안 문구(`instructions-variant.py`), 예제 6파일(`add-signoff.py`) → 빌드 → G1 → `dotnet format --verify-no-changes` → 전체 → G2·G3 |
| S3 | 도구 빌드. 예제 `answers.yaml`·`case.yaml` 분류, 대조 → expected와 `cmp`. HEAD `answers.yaml` 분류. 다른 경로, de-DE 프로세스. SHA-256, 스펙 SHA-256 줄, `case.yaml` 포함 관계, 예제 diff |
| S4 | `PROBE`(서명, 기본값 표시, 추정, 추정 오류, JSON, 문화권 7종). 12.1 합성 사본(HEAD, 7단계 `yes`). guard `case-valid` |
| S5 | 음성 대조 4종(`mutate.py`) → 빌드 → 전체. 되돌려 전체, G2·G3 |
| S8 | case-schema 6.7·classification 6.5 블록을 꺼내(`extract-block.py`) 파서·분류기 출력과 `cmp`. JSON 차이 |

- S2a의 컴파일 오류(`rc=1`)와 40개 실패, S5 변형의 실패는 기대한 실패다.

---

## 사람 승인 필요

질문마다 prototyper의 제안을 적었다. "제안대로"로 답할 수 있다.

### juhan 직접 승인 필요

상시 승인 예외(기존 테스트 변경, 2절 원칙 변경, expected·예제 변경)라 juhan이 직접 답한다.

1. **기존 테스트 10파일 변경(4.2).** 컴파일 3건, 표가 늘어 생기는 숫자(조합 4,320, 행 5,928, 키 10개, 분기·틀 8개), 직접 쓴 값 YAML에 `human_signoff: no`, guard 표본 기본값 표시 3 → 5, 이름 2개(`nine` → `ten`, `seven_rules` → `eight_rules`).
   - 제안: 4.2 표대로 바꾼다. 지우거나 Skip하는 테스트가 없고 기대값을 느슨하게 하지 않는다. PR 본문에 4.2 표를 붙이고 reviewer가 확인한다.
   - guard 3 → 5의 대안: `TestData/guard/case-valid`에 `human_signoff: no`를 넣어 3을 지킨다. 그러면 case-schema 6.1 블록과 `valid-full`까지 바꿔야 한다(T-003 테스트가 셋이 같은지 본다). 바꾸는 파일이 늘어 추천하지 않는다.
   - `Branch_examples_cover_all_seven_branches`는 이름을 그대로 둔다(분기 수가 8이 됐지만 이름만 틀린다). 바꾸길 원하면 `…_all_branches`로 바꾼다.
2. **지시문 2절 6과 "`human_signoff`는 늘 명시"(6.1).**
   - 제안: 안 A. 2절 6 끝에 "단, `human_signoff`는 비워 두지 않는다. …"를 더하고, CLAUDE.md 같은 줄에도 짧게 더한다. 리드가 고친다. 엔진 동작은 안 A·B가 같다.
3. **예제 6파일과 스펙 블록 변경(1절, 3.4).** `examples/*/answers.yaml`·`examples/*/case.yaml`의 `answers` 줄 끝에 `, human_signoff: no`. classification.md 7.2~7.4 `answers.yaml` 블록, synthetic-examples.md 7.2~7.4 `case.yaml` 블록·SHA-256 줄. expected 6개(분류 3, 지표 3)는 바이트 그대로다.
   - 제안: 위대로 확정한다. test-engineer가 블록을 바이트 그대로 `examples/`에 옮긴다(이 카드에 한해 폴더 제한 예외, T-004·T-005와 같음). expected 파일은 건드리지 않으므로 `expected migration:` 커밋이 없다.

### 리드 추천값 승인

ADR·2절 원칙·expected를 바꾸지 않는다. 기존 테스트를 지우거나 느슨하게 하지 않는다.

1. **`inferred` 오류(case-schema 4.13, 4.9).** 목록 아님 `type`, null 원소 `required`, 답 키 아님 `enum`(가능한 값 = 키 10개), 중복 `duplicate`(`추정 답 키가 중복된다 (처음: …)`), 값 없는 답 `required`(`inferred에 적은 답은 answers에 값이 있어야 한다`, 경로 `steps[n].answers.<키>`, 위치는 원소). 오류 코드는 늘지 않는다.
   - 제안: 4.13 그대로.
2. **`answers`가 매핑이 아닐 때.** `answers`의 `type` 오류와 `inferred` 원소마다 `required`가 같이 난다(값이 없으므로).
   - 제안: 그대로 둔다. 둘 다 사실이고 사용자가 한 번에 고칠 수 있다.
3. **읽은 답(classification 4.6).** `SIGNOFF` = `human_signoff` 하나(ADR 0003 해석). `Q1`~`ELSE`에 `human_signoff`를 더한다. `PARTIAL`·`GATE`는 키 하나 그대로라 `human_signoff` 기본값 표시가 부가 행에는 붙지 않는다.
   - 제안: 4.6 표 그대로.
4. **`SIGNOFF` 근거 문장.** `사람이 최종 확인·서명하는 단계이므로(yes) 사람이 하는 것으로 분류.`(ADR 0003 초안).
   - 제안: 그대로 확정. 4.5 문장 틀 표 첫 행.
5. **추정 표시와 API(classification 4.12, 4.11, case-schema 4.12).** 문구 ` (AI 추정, 확인 필요)`, 기본값 표시 뒤. `StepClassification.Inferred`(Defaults 바로 뒤, 4.4 순서), JSON에는 쓰지 않음. `Answer<T>(T Value, bool IsDefault, bool IsInferred = false)`.
   - 제안: 그대로. ADR 0004 "엔진이 추정 답을 다루는 법" 추천과 같다.
6. **TestData 표본 두 개(5절).** `TestData/case-schema/inferred-errors/case.yaml`(6.7), `TestData/classification/signoff-inferred/answers.yaml`(6.5, 새 폴더).
   - 제안: 그대로. 블록 = 파일 테스트를 둔다.
7. **G2·G3 대상을 더하지 않는다.** 서명·추정 경로는 같은 분류기라 `classifier-case-valid`가 결정론·원본 불변을 이미 본다.
   - 제안: 더하지 않는다. 필요하면 `signoff-inferred` 표본으로 M2에서 더한다.
8. **지시문 3.1·3.2·3.3 문구(6.2~6.4).**
   - 제안: 6절 문구 그대로. 12.1 블록·12절 표는 고치지 않는다(6.5).

## 승인 결과

- 2026-11-07.
- juhan 1: 기존 테스트 10파일 변경을 4.2 표대로 직접 승인했다("제안대로 승인", 결정 카드). guard 표본 기본값 표시는 3 → 5. `Branch_examples_cover_all_seven_branches` 이름은 그대로 둔다.
- juhan 2: 안 A를 직접 골랐다(결정 카드). 리드가 지시문 2절 6 끝과 CLAUDE.md 같은 줄에 문장을 더했다.
- juhan 3: 예제 6파일(`answers.yaml`·`case.yaml`)과 스펙 블록 변경을 직접 승인했다(질문 1과 같은 카드). expected 6개는 바이트 그대로다.
- 4~8: juhan의 상시 승인("끝날때까지 추천값으로 머지하고 진행해 계속")에 따라 리드가 제안대로 승인했다. 지시문 3.1·3.2·3.3은 6절 문구 그대로 고쳤다.
