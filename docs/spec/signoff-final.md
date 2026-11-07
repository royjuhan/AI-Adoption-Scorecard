# signoff-final (T-009 스펙: 4번째 공개 예제)

- 상태: 승인됨(2026-11-07). 승인 결과는 문서 끝.
- 출발점: ADR 0003 남은 질문 2(2026-11-07 juhan "제안대로 승인"). 이름 `signoff-final`, T-008 바로 다음 카드. 지시문 2절 11("기대값을 먼저")과 순서가 다른 점은 juhan이 이미 승인했다 [읽음 ADR 0003 남은 질문 승인 결과].
- 반영한 사람 결정: ADR 0004 expected 변경 규칙과 상시 승인 예외("4번째 예제는 그때마다 juhan에게 묻는다") [읽음 ADR 0004].
- 작업 카드: `docs/tasks/T-009-signoff-final.md`
- 작성: prototyper, 2026-11-07
- 확인 대상 커밋: `ce6c4f0`(main `2d952dc` + T-009 카드). 스파이크는 이 커밋을 clone하고 작업 트리의 스펙 문서 넷과 `spikes/T-009/`를 복사해 돌렸다(`summary.txt`의 `repo HEAD` 줄).
- 확인 환경: Ubuntu 24.04 x64, .NET SDK 10.0.114, xUnit 2.9.3. 교차 확인에 Python 3.13.18(표준 라이브러리만).
- 스파이크: `spikes/T-009/` (머지 대상 아님. 머지 전에 빠진다). 실행은 `spikes/T-009/run-spike.sh <작업폴더> <로그폴더>`. 요약 원문은 `spikes/T-009/summary.txt`.

## 0. 표기

- `[읽음]`: 커밋된 파일을 읽어서 확인한 사실.
- `[T9:S0]`~`[T9:S8]`: 스파이크 단계(10절).
- **미확인**: 실험하지 않았거나 이 환경에서 확인할 수 없는 것.
- 블록 본문은 이 문서에 다시 적지 않는다. 본문은 `classification.md` 7.5(`answers.yaml`, `expected/classification.json`), `synthetic-examples.md` 7.5(`case.yaml`, `intake.md`, CSV 2개), `metrics.md` 7.5(`expected/metrics.json`)다. 이 문서는 근거, 요약, 기존 테스트 변경, 지시문 문구, 결과, 질문을 모은다.
- V1~V6: 바꾸는 기존 테스트(5.2). N1: 새 테스트 제안(5.3). J1~J3: juhan 질문. L1~L8: 리드 질문.

## 1. 먼저: 요약

| 단계 | 이름 | 분류(행) |
| --- | --- | --- |
| 1 | 기록 파일 열기·정리 | ② `ELSE` |
| 2 | 기준 구간 고르기(안정 기록) | ② `ELSE` |
| 3 | 기준 구간 고르기(흔들린 기록) | ② `ELSE` + ④ `PARTIAL` |
| 4 | 구간 값 계산·옮기기 | ② `ELSE` |
| 5 | 보고서 양식 채우기 | ② `ELSE` |
| 6 | 결과 해석 문장 쓰기 | ③ `Q2` + ④ `GATE` |
| 7 | 최종 확인·서명·제출 | ④ `SIGNOFF` (한 행, 게이트 없음) |

- 행 9(모델 A). ② 5, ③ 1, ④ 예외 검토 1, ④ 게이트 1, ④ `SIGNOFF` 1. 확인 항목 `measure` 7. AI 과잉 후보 0. 기본값 표시 문장 0 [T9:S2 rows].
- 지시문 12.1의 사람 판단(1·2·4·5 = ②, 3 = ② + 예외는 ④, 6 = ③ 또는 ④, 7 = ④)과 모두 맞는다 [읽음 12.1].
- 검증 자료(안 A, J3): 기준 20 / 결과 20, 일치 16(허용오차 안 차이 2), 값 불일치 3, 누락 1, 추가 1. 정확도 80.0, 누락률 5.0. `case.yaml` 문턱 95·2에 못 미친다 [T9:S2 metrics].

파일(블록에서 옮긴 바이트) [T9:S8]:

| 파일 | SHA-256 | 바이트 | 블록 |
| --- | --- | --- | --- |
| `answers.yaml` | `2ff0c4aa01110f300d2eeb92a1e78b70803b8d5be80e7f12d8aec9843416b7fa` | 2,109 | classification 7.5 |
| `expected/classification.json` | `fe040ce6b8eaafbf7218451b668d232f948a6d907c279edb592be19d4155934f` | 4,201 | classification 7.5 |
| `case.yaml` | `a12835eae7b913d9b0ea855fa01fb47086fd97411ae50bed48f5d01beadf0263` | 2,595 | synthetic-examples 7.5 |
| `intake.md` | `2066d6823ed5c93ab6c36af39534d34cdeb42c8469bcb185d6cdc93ce52a654e` | 1,025 | synthetic-examples 7.5 |
| `reference/records.csv` | `95db6437676d6cdad6d63ad0e5ddd52249ab0a57b29e8c6852f1edf329ddc771` | 633 | synthetic-examples 7.5 |
| `candidate/records.csv` | `5a0a72b75823bf8ea3897adcfe3132b9efa45012ce8363f3926d3650232737f4` | 636 | synthetic-examples 7.5 |
| `expected/metrics.json` | `95546bad43c246a9b0b397fc28e9eb66cb8b3caaaa097b204ff4d055f644cb2b` | 3,913 | metrics 7.5 |

- expected 두 파일은 두 길로 확인했다. main Core 출력 = 블록. 스펙 글만 보고 짠 Python 구현 출력 = 블록 [T9:S2 xcheck]. 그래도 블록은 초안이다. 엔진 출력은 정답이 아니다. juhan이 확정한다(J1).
- 기존 예제 3종의 파일과 expected 6개는 바이트 그대로다 [T9:S4].
- Core 변경은 없다. 생성기가 계획을 그대로 받는다. clone의 `src/` diff가 없다 [T9:S2 gen], [T9:S4].
- 기존 테스트 5개가 깨진다. 예제 목록을 고정한 테스트다. 바꾸는 곳은 5파일(V1~V5), 안 A이면 1파일 더(V6). 고치면 전체 1,282개 통과(기준선 1,196, 삭제·Skip 0) [T9:S3a], [T9:S3b].

## 2. 범위

| 카드 수용 기준 | 이 문서 | 질문 |
| --- | --- | --- |
| 1 파일 = 스펙 블록, 생성기·시드 고정, 실제 값 패턴 검사 | 3절, 4.3, 4.5, 7절 | J1, L2 |
| 2 expected는 블록에서 옮김, 골든 바이트, 7단계 ④ `SIGNOFF` 한 행·게이트 없음 | 4.2, 5.3, 8절 | J1 |
| 3 기존 예제 3종·expected 6개 그대로 | 7절 | — |
| 4 지시문 12절·M1 G1 "4종", 12절 표 1행, "답 10개" | 6절 | L5, L6 |
| 5 시작 1,196, 끝 ≥, 삭제·Skip 없음. 목록 고정 테스트 변경은 juhan | 5절 | J2 |
| 6 ubuntu·windows CI 녹색 | 9절(미확인) | — |
| 메모: 검증 자료 구조는 juhan이 정한다 | 4.3 | J3 |
| 메모: 판정 칸은 판정 카드 | 4.3, 6절 | — |

범위 밖:

- 판정(통과·조건부·보류·미검증). 판정 카드가 정한다. 이 예제는 판정 숫자가 없다.
- 12.1 블록과 TestData 합성 사본(`TestData/case-schema/example-12-1/`). 바꾸지 않는다(카드 메모).
- 지시문 고치기. 사람 결정 뒤 리드가 한다(6절은 제안 문구).
- M2 end-to-end(L6), 평가서·`report-outline.json`.

## 3. 문서 배치 (제안, L1)

테스트는 스펙을 이렇게 읽는다 [읽음 `tests/Scorecard.Tests/CaseSchema/SpecDocument.cs`와 각 `*Spec.cs`]:

- `SpecDocument.Table(문서, 머리 행)`: 머리 행이 그 문서에 정확히 한 번 나와야 한다. 표 하나는 문서 하나에서만 읽는다.
- `SpecDocument.CodeBlocks(문서, 제목)`: 그 제목부터 다음 제목까지의 블록. 테스트가 블록 수를 센다(classification 2개, synthetic 7.1 파일 표의 수, metrics 1개).
- `SpecDocument.Line(문서, 앞부분)`: SHA-256 줄이 문서에 한 번.
- 예제별 제목은 테스트 상수 사전(`SpecHeadings`, `ExampleHeadings` 둘)에 있다.

| 안 | 블록·표 행 | 테스트 변경 |
| --- | --- | --- |
| A (제안) | 기존 세 문서에 새 절 "### 7.5 signoff-final"과 표 행(끝에 1행씩). 이 문서는 근거·질문만 | 예제 목록 2곳 + 제목 사전 3곳에 한 줄씩(V1~V5) |
| B | 블록을 모두 이 문서에. 표 행도 이 문서의 같은 머리 행 표에 | 제목 사전이 문서 경로까지 가져야 한다. 표를 두 문서에서 읽어 합치는 코드가 필요하다. 표 6종 × 2문서. 변경이 크다 |

- 안 A에서 바꾼 곳:
  - `classification.md`: 7.1 요약 표 1행, 7.1 항목 8, 새 7.5, 6.3·6.4 글머리, 끝 "T-009 개정".
  - `synthetic-examples.md`: 4.3 계획 표 1행, 4.4 열 표 4행, 4.7 구조 표 1행, 4.8 표 1행, 7.1 파일 표 4행, 4.2·4.7·4.8·7.1 글머리, 새 6.4·7.5, 끝 "T-009 개정".
  - `metrics.md`: 7.1 예제 표 1행, 7.1 항목 10, 새 6.5·7.5, 끝 "T-009 개정".
- 표 행은 모두 끝에 넣었다. 몇 테스트가 표의 예제 순서를 예제 목록 순서와 비교한다. 목록 끝에 `signoff-final`을 넣으므로 맞다 [T9:S3b].
- 새 머리 행(`synthetic-examples.md` 6.4의 `record_kind` 표, 이 문서의 표들)은 각 문서에서 한 번이다. 테스트가 아직 읽지 않는다.

## 4. 예제 설계

### 4.1 단계와 답 (12.1과 같은 모양)

12.1 비공개 예시의 단계 7개와 답 모양을 그대로 따랐다. 이름·문장·값은 새로 합성했다. 업종을 특정하지 않는다 [읽음 12.1, 지시문 12절].

| 단계 | 12.1과 같은 답 | 12.1과 다른 답 | `human_signoff` |
| --- | --- | --- | --- |
| 1 | `fixed_form`, `yes`, `weekly`, `exception_rate: 5`, `program`, `internal_rework`, `existing_feature: no` | `batch_size` 10 → 20 | `no` |
| 2 | `table`, `yes`, `weekly`, `exception_rate: 10`, `program`, `internal_rework`, `existing_feature: no` | `batch_size` 10 → 14 | `no` |
| 3 | `table`, `partial`, `weekly`, `human`, `internal_rework`, `existing_feature: no` | `batch_size` 3 → 6, `exception_rate` 60 → 50 | `no` |
| 4 | `table`, `yes`, `weekly`, `exception_rate: 0`, `program`, `internal_rework`, `existing_feature: no` | `batch_size` 10 → 20 | `no` |
| 5 | `fixed_form`, `yes`, `weekly`, `exception_rate: 0`, `program`, `internal_rework`, `existing_feature: no` | `batch_size` 10 → 20 | `no` |
| 6 | `free_doc`, `no`, `weekly`, `human`, `external`, `existing_feature: no` | `batch_size` 10 → 20, `exception_rate` 50 → 40 | `no` |
| 7 | `fixed_form`, `no`, `weekly`, `exception_rate: 0`, `human`, `external`, `existing_feature: no` | `batch_size` 10 → 20 | `yes` |

- 다른 값은 분류에 영향이 없다. `batch_size`는 `rare`일 때만, `exception_rate`는 트리가 읽지 않는다(classification 4.1, 4.6) [읽음].
- `batch_size`: 주에 기록 파일 20개. 안정 기록 14 + 흔들린 기록 6 = 20. 자료의 `steady` 14행, `unsteady` 6행과 같다(4.3) [T9:S2 planted].
- 답은 `minutes_per_item` 말고 모두 적었다(`human_signoff` 포함). `minutes_per_item`은 12.1과 다른 예제 3종처럼 적지 않았다. 시간 자료가 없어서다. 그래서 모든 단계에 `measure_minutes_per_item`이 달리고, 기본값 표시 문장은 0이다. 카드의 "답 10개"를 "10번째 답 `human_signoff`까지 명시한다"로 읽었다(**해석**, J1).
- 6단계(③ + ④ 게이트) 다음 7단계(④ `SIGNOFF`)로 사람 확인이 두 번 나온다. ADR 0003 남은 질문 1의 답대로 그대로 둔다 [읽음].
- `task`: `기록 파일에서 기준 구간을 골라 값을 내고, 보고서를 써서 사람이 서명해 제출한다`. 지시문 12절 표 "업무" 칸과 같아야 한다(테스트가 본다, 6절).

### 4.2 분류 (`expected/classification.json`)

- 블록은 `classification.md` 7.5. main Core가 블록 `answers.yaml`과 `case.yaml`에서 같은 바이트를 낸다 [T9:S2 classify].
- 7단계 문장: `사람이 최종 확인·서명하는 단계이므로(yes) 사람이 하는 것으로 분류.` 읽은 답은 `human_signoff` 하나다. `failure_impact: external`이어도 게이트가 없다(classification 4.3) [T9:S2 rows].
- 1~6단계 근거 키가 12.1 합성 사본(`TestData/case-schema/example-12-1`, 바꾸지 않음)의 분류와 같다. 7단계만 `Q2+GATE` → `SIGNOFF`다. N1 테스트로 확인했다 [T9:S3b N1].
- 독립 확인: `spikes/T-009/xcheck/classify.py`. classification.md 4.2·4.5·4.6·4.7 표와 case-schema 4.4 표를 읽고, 트리 순서·부가 행·JSON 바이트 규칙은 글(4.1·4.3·3.2·3.3)대로 짰다. C# 코드를 쓰지 않는다. 예제 4종 모두 블록과 바이트가 같다 [T9:S2 xcheck].

### 4.3 검증 자료 (juhan 결정, J3)

열(`synthetic-examples.md` 4.4)과 `case.yaml` 자료 키(4.8):

| 열 | 종류 | 값 | `compare` |
| --- | --- | --- | --- |
| `record_id` | 키 | `rec01`~`rec21` | `inputs.key` |
| `record_kind` | 선택 | `steady`, `unsteady` | 없음(유형 열, table-compare `signal_type`과 같은 자리) |
| `range_start` | 정수 | `0` ~ `2000` | `tolerance: 0` |
| `range_end` | 정수 | `3000` ~ `6000` | `tolerance: 0` |
| `range_avg` | 소수 | `100.0` ~ `999.9` | `tolerance_pct: 1.0` |

- `compare`와 `thresholds` 숫자는 12.1과 같다(시작·끝 0, 평균 1.0%, `accuracy_min: 95`, `missing_max: 2`). `time_must_drop`은 시간 자료가 없어 `false`다 [읽음 12.1].
- 시작·끝 범위가 겹치지 않아 끝 > 시작이 늘 맞는다. 소수 한 자리는 table-compare(두 자리)와 다른 모양을 보려는 것이다.

안(같은 열, 같은 시드 규칙):

| 안 | 심은 것 | 셈 | 정확도·누락률 | 문턱 95·2 | 기존 테스트 |
| --- | --- | --- | --- | --- | --- |
| A (제안) | 값 불일치 열마다 1(3), 누락 1, 추가 1, 허용오차 안 `range_avg` 2, 몰림 없음 | 20/20, 일치 16, 불일치 3, 누락 1, 추가 1, 안 2 | 80.0 · 5.0 | 둘 다 미달 | V6 필요(문턱 테스트를 좁힌다) |
| B | 허용오차 안 `range_avg` 2만 | 20/20, 일치 20, 안 2 | 100.0 · 0.0 | 충족 | V6 필요 없음 |

- A의 이유: 공개 예제 넷이 판정 넷의 모양을 하나씩 갖는다. table-compare "조건부"(몰림), form-fill "통과", mixed-exceptions "미검증", signoff-final "정확도 미달"(지시문 3.4 "보류 = 정확도 미달 또는 설명 안 되는 불일치"의 모양). 12.1의 엄한 문턱(95·2)이 실제로 막는 경우를 보인다. 불일치 4건이 `unsteady` 3, `steady` 1로 한 유형에 몰리지 않는다.
- A의 비용: 기존 테스트 `CaseYamlTests.Structure_meets_thresholds`가 자료 예제 모두에 "문턱 충족"을 요구한다. signoff-final을 빼야 한다(V6, 테스트 범위를 좁히는 변경). 대신 새 테스트가 "문턱 미달"을 본다(N1). 판정 카드가 이 예제를 "보류"로 낼지는 그 카드에서 정한다.
- B의 이유: 기존 테스트를 좁히지 않는다. B는 form-fill과 같은 "통과" 모양이라 새로 보이는 것이 적다. B도 시드 0~1999 모두 생성되고 셈이 하나다(스파이크 밖 확인, 스파이크 도구로 4.3 행만 바꿔 실행).
- 판정 숫자는 어느 안이든 12절 표에 쓰지 않는다(카드 메모). A이면 "문턱 미달"까지만 쓴다(6절).

심은 행(A, `synthetic-examples.md` 6.4) [T9:S2 planted]:

- 누락 `rec14`(steady). 값 불일치 `rec04` `range_start` 345 → 358, `rec18` `range_end` 3416 → 3400, `rec20` `range_avg` 785.9 → 762.3(3.00%). 추가 `rec21`. 허용오차 안 `rec06` 392.2 → 391.8(0.10%), `rec11` 582.6 → 583.8(0.21%).

시드 규칙(L2):

- 시드 `47`: 기준 20행 중 `unsteady`가 6행(3단계 `batch_size`)인 가장 작은 시드 [T9:S2 seed]. table-compare 규칙(`irregular`가 2단계 `exception_rate` 40%인 가장 작은 시드)과 같은 방식이다.
- 시드 0~1999 모두 생성에 성공하고, 셈(20/20/16/3/1/1/2, 80.0/5.0)이 하나다 [T9:S2 seeds]. 기존 `Every_seed_below_2000_keeps_the_structure`가 V3 뒤 이 예제도 돈다 [T9:S3b].
- 소수 범위 뒤집기: 시드 0~1999에서 값 불일치 행 55개가 범위 끝 때문에 부호를 뒤집는다. 허용오차 안 행은 0개다. 시드 47에는 뒤집는 행이 없다 [T9:S2 seeds]. 그래서 `StructureTests.Decimal_planted_values_stay_in_column_range_for_every_seed_below_2000`에 이 예제를 넣지 않는다. 넣으면 "허용오차 안 뒤집기 행이 하나 이상"이라는 빈 통과 방지 검사에 걸린다. 규칙은 table-compare와 G2 계획이 이미 본다(L7).

생성기와 Core:

- main Core 생성기가 계획을 그대로 받는다(정수 2열, 소수 1자리 1열, 선택 1열, 값 불일치 3열). Core 변경이 없다 [T9:S2 gen], [T9:S4].
- 독립 확인: T-005 Python 생성기에 T-005 리뷰 뒤 생긴 "소수 열 범위를 벗어나면 부호를 뒤집는다"(synthetic 4.5의 4·5)를 더했다(`spikes/T-009/xcheck/generate.py`). signoff-final CSV 2개, table-compare·form-fill CSV 4개가 모두 같은 바이트다 [T9:S2 xcheck].
- 대조 독립 확인: T-006 Python 대조(`spikes/T-009/xcheck/compare.py`, 그대로 가져옴). 예제 4종 metrics.json이 블록과 같다 [T9:S2 xcheck].

### 4.4 `intake.md`

- synthetic 4.9 규칙대로 썼다: 한글 짧은 문장, 문단만, 업종·이름·코드 모양 값 없음 [읽음].
- 12.1 구조를 말로 적었다: 주 20개 파일, 정리·안정 기록 구간·값 계산·보고서는 지금 매크로가 한다(기존 프로그램에 기능 없음), 6개쯤 흔들려 사람이 고르고 그중 절반쯤 애매함, 해석 문장은 기준이 글로 없고 밖으로 나감, 마지막에 담당자가 확인·서명·제출, 비교할 AI 결과가 있음.
- 테스트는 내용을 해석하지 않는다(블록 = 파일, SHA-256, 패턴 검사만).

### 4.5 실제 값 패턴

- `ExamplePatternTests`가 V1·V3 뒤 새 파일도 본다. 걸린 것이 없다 [T9:S3b]. 키는 `rec` + 2자리라 `part-letters-digits`(숫자 4자리 이상)에 걸리지 않는다.

## 5. 기존 테스트 변경 (juhan 직접 승인, J2)

### 5.1 바꾸지 않으면 깨지는 것 [T9:S3a]

예제 파일·문서·지시문 문구를 넣고 HEAD 테스트로 돌렸다. 1,200개 중 5개 실패. 모두 "예제 목록 = 문서·폴더 목록"을 고정한 테스트다.

| 테스트 | 왜 |
| --- | --- |
| `ExampleFileTests.Examples_root_has_only_gitattributes_and_example_folders` | `examples/` 폴더 목록 = `ClassificationInput.Examples` |
| `ExampleFileTests.File_table_covers_the_three_examples` | synthetic 7.1 파일 표 예제 = 목록 |
| `PlanTableTests.Plan_examples_are_the_examples_with_csv` | synthetic 4.3 계획 표 = `DataExamples` |
| `StructureTests.Structure_table_examples_are_data_examples` | synthetic 4.7 구조 표 = `DataExamples` |
| `MetricsGoldenTests.Example_table_covers_the_three_examples` | metrics 7.1 예제 표 = 목록 |

- 늘어난 4개(`ExampleFileTests.File_hash_matches_spec_line`의 signoff-final 파일 4개)는 파일 표에서 데이터를 만들어 통과한다.
- 나머지 예제별 테스트(골든, 블록 = 파일, 결정론, 줄바꿈 변형 등)는 목록에 없어서 signoff-final을 돌지 않는다. 그래서 목록을 고쳐야 카드 수용 기준 1·2가 테스트된다.

### 5.2 바꾸는 곳

| | 파일 | 바꾸는 것 | 약화인가 |
| --- | --- | --- | --- |
| V1 | `Classification/ClassificationInput.cs` | `Examples`에 `"signoff-final"`(끝). 주석 "3종" → "4종" | 아니오. 예제별 테스트가 하나 더 돈다 |
| V2 | `Classification/ExampleGoldenTests.cs` | `SpecHeadings`에 `["signoff-final"] = "### 7.5 signoff-final"` | 아니오 |
| V3 | `Synthetic/SyntheticInput.cs` | `DataExamples`에 `"signoff-final"`(끝) | 아니오 |
| V4 | `Synthetic/SyntheticSpec.cs` | `ExampleHeadings`에 7.5 | 아니오 |
| V5 | `Comparison/MetricsSpec.cs` | `ExampleHeadings`에 7.5 | 아니오 |
| V6 (안 A만) | `Synthetic/CaseYamlTests.cs` | `Structure_meets_thresholds`의 데이터를 `DataExamples`에서 "문턱을 충족하는 예제"(`signoff-final` 뺌)로. 미달은 N1이 본다 | 범위를 좁힌다. 같은 예제의 반대 사실을 새 테스트가 본다. juhan이 판단 |

- 스파이크의 변형 코드는 `spikes/T-009/tests-variant/apply.py`다(문자열 바꾸기, 위 6곳).
- V6 없이(V1~V5 + N1) 돌리면 `Structure_meets_thresholds(example: "signoff-final")` 1개만 실패한다 [T9:S3c].
- 이름에 "three"가 있는 테스트 셋(`Examples_are_the_three_spec_examples`, `File_table_covers_the_three_examples`, `Example_table_covers_the_three_examples`)은 이름을 그대로 둔다(제안, L4). T-008이 `…_seven_branches`를 그대로 둔 것과 같다. 이름을 바꾸면 trx 이름이 바뀌어 통과 수 비교가 번거롭다.
- 바꾸지 않는 예제별 테스트: `ExampleGoldenTests.Instructions_expectations_hold_for_examples`, `MetricsGoldenTests.Instructions_expectations_hold`, table-compare·form-fill 전용 테스트. 세 예제 값을 직접 적은 테스트다. signoff-final 값은 N1이 본다.
- `StructureTests.Decimal_planted_values_stay_in_column_range_for_every_seed_below_2000`의 `InlineData`는 그대로다(4.3, L7).

### 5.3 새 테스트 (test-engineer, N1)

스파이크 제안 파일 `spikes/T-009/tests-variant/SignoffFinalExampleTests.cs`. 5개, 모두 통과 [T9:S3b].

| 테스트 | 확인 | 카드 수용 기준 |
| --- | --- | --- |
| `Step_seven_is_a_single_signoff_row_without_gate` | 7단계 행이 ④ `SIGNOFF` `step` 하나. 다른 단계에 `SIGNOFF` 없음. 행 9 | 2 |
| `Steps_one_to_six_match_the_12_1_copy` | 1~6단계 근거 키 = TestData 12.1 합성 사본. 7단계 `Q2+GATE` → `SIGNOFF` | 2 |
| `Every_step_writes_all_ten_answers_except_minutes` | 모든 단계 `defaults` = `minutes_per_item`뿐. 기본값 표시 없음 | 4 |
| `Metrics_are_below_thresholds` (안 A) | 80.0·5.0, 문턱 95·2·false, 셈으로도 미달 | 1 |
| `Seed_is_the_smallest_with_unsteady_count_equal_to_step_three_batch` | 시드 47 = 규칙(4.3) | 1 |

- 폴더·이름은 test-engineer가 정한다. 안 B이면 네 번째 테스트를 "문턱 충족"으로 바꾸고 V6을 뺀다.

### 5.4 통과 수

- 기준선(HEAD `ce6c4f0`) 1,196 [T9:S1]. V1~V6 + N1 뒤 1,282, 실패 0, 없어진 테스트 0, Skip 0 [T9:S3b].
- 늘어난 86개: 예제 목록·표에서 데이터를 만드는 기존 이론 테스트가 signoff-final로 81개(분류 골든·블록·줄바꿈 변형, 대조 골든·결정론·줄바꿈 변형, 합성 CSV·구조·시드 0~1999·결정론 등), N1 5개.
- G1·G2·G3와 가드 대상 테스트 111개 통과 [T9:S3b guards]. `dotnet format --verify-no-changes` 통과 [T9:S3b format].

## 6. 지시문 문구 (리드가 사람 결정 뒤 고친다, L5·L6)

테스트가 읽는 지시문: 12절 표(`| 예제 | 업무 | 단계 수 | 기대 분류(요지) | 검증 자료 |`), 3.2 질문 표, 12.1 블록, 3.3 예시 문장 [읽음 `SpecDocumentTests`, `ExampleGoldenTests`, `StructureTests`, `ReasonSentenceTests`]. "3종"·"9개" 글은 어떤 테스트도 읽지 않는다.

12절 표 행을 읽는 테스트(V1·V3 뒤):

- `Answers_use_only_name_task_and_steps`: "업무" 칸 = `answers.yaml` `task`.
- `Row_count_equals_instructions_step_count`: "단계 수" = 행 수 합(9).
- `Structure_matches_instructions_section_12`: "검증 자료" 칸의 `reference N건 / candidate M건`, `정확도 N%`, `불일치 N건·누락 M건`(N = 값 불일치 + 누락), `누락률 N%`.
- 아래 제안 행으로 셋 다 통과했다 [T9:S3b].

제안 행(12절 표 끝):

```text
| `signoff-final` | 기록 파일에서 기준 구간을 골라 값을 내고, 보고서를 써서 사람이 서명해 제출한다 | 9 | 파일 정리·안정 기록 구간·값 계산·보고서 채우기 = ②, 흔들린 기록 구간 = ②+④, 해석 문장 = ③+④, 최종 확인·서명·제출 = ④(서명 단계, `human_signoff: yes`, 게이트 없음) | reference 20건 / candidate 20건, 불일치 4건·누락 1건 심어 둠 → 정확도 80%, 누락률 5% (문턱 95%·2% 미달. 판정은 판정 카드에서) |
```

| 곳 | 지금 | 제안 |
| --- | --- | --- |
| 5절 폴더 트리 `examples/` 줄 | `공개 합성 예제 3종 = 테스트의 정답 (12절)` | `공개 합성 예제 4종 = …` |
| 5절 폴더 트리 예제 줄 | `│  ├─ form-fill/` `│  └─ mixed-exceptions/` | `│  ├─ form-fill/` `│  ├─ mixed-exceptions/` `│  └─ signoff-final/` |
| 9절 M1 G1 | ``G1: 공개 예제 3종의 출력이 `expected/`와 일치.`` | `… 4종 …` |
| 12절 제목 | ``## 12. 공개 예제 3종 (`examples/`) — 테스트의 정답`` | `… 4종 …` |
| 12절 첫 문단 | `세 예제는 구조가 서로 다르게 고른다.` | `네 예제는 …` |
| 12절 첫 문단 | `` `answers.yaml`(질문 9개 답) `` | `` `answers.yaml`(답 10개) `` |
| 12절 표 | 3행 | 위 제안 행을 끝에 |
| 12절 표 아래 | ``예제 3종의 `expected/`에는 …`` | `예제 4종의 …` |

- 스파이크가 이 8곳을 clone의 지시문에 넣고 테스트했다(`spikes/T-009/instructions-variant.py`) [T9:S2 instructions].
- "답 10개": 카드 문구를 따랐다. `minutes_per_item`처럼 적지 않은 답은 기본값이다(case-schema 4.4).
- "검증 자료" 칸의 괄호는 안 A 문구다. 안 B이면 `reference 20건 / candidate 20건, 전부 일치 → 정확도 100%`가 된다(테스트 규칙상 "전부 일치"면 불일치 문구가 없어야 한다).
- 고치지 않는 곳: 12.1 블록(카드 메모), 11절 T-004~T-006 카드 문구와 14절 8("T-005에서 예제 3종의")는 그때의 기록이다. 3.1 표 "질문 9개를 번호로 고른다" 다음에 서명 단계 질문이 따로 있어 맞다.
- M2 G2 `예제 3종에서 start --answers→open→build까지 end-to-end`: 리드 질문(L6). 제안: "4종"으로 고친다. 서명 단계를 만드는 문답(지시문 3.1 끝 질문)을 end-to-end로 보는 공개 예제는 signoff-final뿐이다.
- 다른 카드·ADR의 "3종"(T-004~T-008 카드, ADR 0004)은 그때의 기록이라 고치지 않는다. `README.md`에는 예제 수가 없다 [읽음].

## 7. 스파이크 결과

- 기준선: Release `-warnaserror` 빌드, 전체 1,196 통과 [T9:S1].
- 블록 → 파일: 7개 파일을 블록에서 옮겼다. SHA-256 줄 6개가 블록 바이트와 같다(`answers.yaml`은 줄이 없다, 기존 예제와 같음) [T9:S2 extract].
- 생성: main Core 생성기가 4.3·4.4 표와 블록 `case.yaml`로 낸 CSV = 블록. Python 생성기도 같다 [T9:S2 gen], [T9:S2 xcheck].
- 분류: main Core가 블록 `answers.yaml`·`case.yaml`에서 낸 바이트 = 블록. Python 분류기도 같다. Python 분류기는 기존 3종 expected와도 같다 [T9:S2 classify], [T9:S2 xcheck].
- 대조: main Core 대조 = 블록. Python 대조도 같다. 기존 3종도 같다 [T9:S2 metrics], [T9:S2 xcheck].
- 결정론: 다른 경로 사본, `LANG=de_DE.UTF-8` 프로세스(문화권을 바꾸지 않음)에서 분류·대조·생성 바이트가 같다 [T9:S2 det].
- 깨지는 기존 테스트 5개(5.1) [T9:S3a]. V1~V5 + N1이면 문턱 테스트 1개만 실패 [T9:S3c]. V6까지 넣으면 1,282 통과, 형식·G1·G2·G3 통과 [T9:S3b].
- 기존 예제 3종: clone의 `examples/table-compare`, `form-fill`, `mixed-exceptions`, `.gitattributes`가 HEAD와 같다. expected 6개 SHA-256이 HEAD와 같다. `src/` diff 없음 [T9:S4].
- 음성 대조 [T9:S5]:
  - Core가 `SIGNOFF` 뒤에도 게이트를 붙이게 바꾸면 30개 실패. 그중 signoff-final 골든·요약 표·행 수·줄바꿈 변형·`case.yaml` 분류 8개와 N1 2개.
  - 예제 7단계를 `human_signoff: no`로 바꾸면(expected 그대로) 13개 실패(골든, 행 수, 블록 = 파일, SHA-256 줄, N1).
  - 계획 시드를 48로 바꾸면(CSV 그대로) 6개 실패(생성 CSV = 파일, 열 모양, 키, 심은 구조 = 대조, N1 시드 규칙).
- `core.autocrlf=true` clone: `examples/.gitattributes`(`* -text`)가 있으면 새 파일 7개 바이트 그대로. 없으면 7개 모두 바뀐다(예: `expected/classification.json` 4,201 → 4,352, `expected/metrics.json` 3,913 → 4,093) [T9:S6]. 기존 `.gitattributes`가 새 폴더에도 걸린다. 새 파일이 필요 없다.

## 8. 테스트로 옮기는 방법 (test-engineer 참고)

- 시작: 이 스펙과 J1~J3 승인 뒤. 승인 전에는 `examples/`·`tests/` 파일을 만들지 않는다.
- 옮기기: 블록을 바이트 그대로 `examples/signoff-final/<파일>`로(블록 줄마다 LF, 끝에 LF). 엔진 출력에서 복사하지 않는다. 스파이크 `extract-example.py`가 같은 규칙으로 옮기고 SHA-256 줄을 확인했다.
- 커밋 모양(제안, J1. metrics.md 7.1 9와 같은 방식):
  1. test-engineer: V1~V6, N1, 예제 파일 5개(`answers.yaml`, `case.yaml`, `intake.md`, CSV 2개). 이때 expected가 없어 골든 테스트가 실패해야 한다(빈 통과 금지).
  2. `expected migration:`으로 시작하는 커밋. `examples/signoff-final/expected/classification.json`, `expected/metrics.json` 2개만. 스펙 블록·SHA-256 줄은 이 스펙 PR에 이미 있다.
  3. 리드: 지시문 6절 문구.
  4. PR 본문: 시작 전 main 통과 수 1,196, 끝난 뒤 통과 수(스파이크 1,282), 삭제·Skip 없음. V1~V6 이유. reviewer가 `git diff --stat`과 expected diff 전문을 붙인다(ADR 0004 expected 변경 규칙).
- Core 구현이 없다. core-dev 단계가 없다. 지시문 2절 11 순서 차이는 ADR 0003에서 승인됐다.
- CLAUDE.md 에이전트 폴더 제한에 `examples/`가 없다. 카드 폴더 범위에는 있다 [읽음]. T-004~T-006처럼 이 카드에 한해 test-engineer가 옮기는 것을 J1에서 함께 묻는다.

## 9. 미확인

1. GitHub ubuntu·windows 러너에서 녹색인지(카드 수용 기준 6). windows의 `core.autocrlf`는 `examples/.gitattributes`가 막는다(S6은 Linux에서만).
2. Windows에서 만든 파일의 SHA-256이 1절과 같은지. 같은 바이트면 같아야 한다.
3. 판정 카드가 안 A 자료를 "보류"로 낼지. "설명 안 되는 불일치"의 정의도 그 카드다.
4. M2 end-to-end(L6)에서 `start --answers`가 이 `answers.yaml` 모양을 그대로 쓰는지.
5. 평가서·`report-outline.json`이 `SIGNOFF` 단계를 어떻게 보일지(M2-3).

## 10. 스파이크 단계 목록

`spikes/T-009/run-spike.sh <작업폴더> <로그폴더>`. 커밋된 HEAD를 작업 폴더에 clone하고 `spikes/T-009/`와 작업 트리의 스펙 문서 넷을 복사해 변형을 넣는다. 저장소의 `src/`, `tests/`, `examples/`, 지시문은 건드리지 않는다. `summary.txt`는 마지막 실행이다.

| 단계 | 한 일 |
| --- | --- |
| S0 | clone, 스파이크 파일 복사 |
| S1 | 기준선 restore, Release `-warnaserror` 빌드, 전체 테스트(trx) |
| S2 | 문서 복사. 블록 → `examples/signoff-final/`(SHA-256 줄 확인). 지시문 제안 문구. 스파이크 도구 빌드. 생성·심은 행·시드 규칙·시드 0~1999. 분류(`answers.yaml`, `case.yaml`)·행·대조. Python 생성·분류·대조(예제 4종). 결정론(다른 경로, de-DE 프로세스) |
| S3a | HEAD 테스트로 빌드·전체 테스트. 깨지는 테스트, 기준선과 이름 차이 |
| S3c | V1~V5 + N1 → 빌드·전체 테스트 |
| S3b | V6 → 빌드·`dotnet format --verify-no-changes`·전체 테스트·이름 차이·G1·G2·G3와 가드 대상 |
| S4 | 기존 예제 3종·expected 6개 = HEAD, clone의 바뀐 파일 목록, `src/` diff |
| S5 | 음성 대조 3종(clone 사본마다 하나) → 빌드·전체 테스트 |
| S6 | 작업 폴더 안 임시 git 저장소에 새 예제를 넣고 `core.autocrlf=true` clone(`.gitattributes` 있음·없음) |
| S8 | 새 파일 SHA-256·바이트 |

- 스파이크 파일: `SignoffFinalSpike/`(Core 공개 API만 쓰는 도구: `gen`, `planted`, `seed`, `seeds`, `classify`, `metrics`, `rows`), `xcheck/`(Python 셋), `drafts/signoff-final/`(prototyper가 쓴 `answers.yaml`·`case.yaml`·`intake.md` 초안), `fill-blocks.py`(파일 → 문서 7.5 절), `extract-example.py`(문서 블록 → 파일), `instructions-variant.py`, `tests-variant/`, `mutate.py`.
- `summary.txt`의 S3a(5개), S3c(1개), S5(30·13·6개) 실패는 기대한 실패다.

---

## 사람 승인 필요

질문마다 prototyper의 제안을 적었다. "제안대로"로 답할 수 있다.

### juhan 직접 승인 필요 (상시 승인 예외: expected 추가, 기존 테스트 변경, 검증 자료 구조)

J1. **예제 파일과 expected 블록 확정.** `classification.md` 7.5, `synthetic-examples.md` 7.5, `metrics.md` 7.5의 블록 7개(1절 표).
- 확인할 점: 단계 7개와 분류(1절 표), 7단계 ④ `SIGNOFF` 한 행·게이트 없음, 행 9, `measure` 7, 기본값 표시 0. 정확도 80.0, 누락률 5.0(J3 안 A).
- `minutes_per_item`은 적지 않았다(12.1·기존 3종과 같음, 측정 권고가 남는다). 카드의 "답 10개"를 "`human_signoff`까지 명시"로 읽었다(4.1).
- 근거: expected 두 파일은 main Core와 Python 독립 구현이 따로 낸 바이트가 같다. 그래도 정답은 juhan이 정한다.
- 옮기기: test-engineer가 블록을 바이트 그대로 옮긴다(CLAUDE.md 폴더 제한 예외, 이 카드 한정). 커밋은 8절 모양(`expected migration:` 커밋에 expected 2개만).
- 제안: 위 초안대로 확정한다. 고칠 곳(단계 이름, 답, 문장)을 적어 주면 prototyper가 스파이크를 다시 돌려 블록·SHA-256을 맞춘다.

J2. **예제 목록을 고정한 기존 테스트 변경(5.2).**
- V1~V5: 목록 2곳과 제목 사전 3곳에 signoff-final 한 줄씩. 없으면 5개 실패하고, 새 예제의 골든·블록 테스트가 돌지 않는다.
- V6(안 A만): `Structure_meets_thresholds`에서 signoff-final을 뺀다. 같은 예제의 "문턱 미달"을 새 테스트(N1)가 본다. 범위를 좁히는 변경이라 따로 묻는다.
- 제안: V1~V5 승인. V6은 J3이 안 A이면 승인. PR 본문에 이유를 쓰고 reviewer가 본다.

J3. **검증 자료 구조(4.3).**
- 제안: 안 A. 20/20, 값 불일치 3(열마다 1)·누락 1·추가 1·허용오차 안 2, 몰림 없음 → 정확도 80.0, 누락률 5.0. 12.1 문턱(95·2)에 못 미치는 첫 공개 예제. 공개 예제 넷이 판정 넷의 모양을 하나씩 갖는다. 판정 자체는 판정 카드가 정한다.
- 대안: 안 B. 허용오차 안 2만 → 100.0·0.0, 문턱 충족, V6 불필요. form-fill과 모양이 겹친다.

### 리드 추천값 승인 (ADR·지시문 2절 원칙·expected·기존 테스트를 바꾸지 않는 질문)

L1. 문서 배치(3절). 제안: 안 A. 기존 세 문서에 7.5와 표 행, 이 문서에 근거·질문.

L2. 시드 규칙(4.3). 제안: `unsteady` 수 = 3단계 `batch_size`(6)인 가장 작은 시드 `47`.

L3. 단계 이름·`task`·`intake.md` 문장·열 이름(4.1, 4.3, 4.4). 제안: 초안대로. 업종을 특정하지 않는 합성 문장이다. 단 J1에서 juhan이 고치면 그것을 따른다.

L4. 이름에 "three"가 있는 테스트 셋(5.2). 제안: 이름은 그대로, 주석만 "4종"으로.

L5. 지시문 문구(6절 8곳)와 metrics.md 7절 제목. 제안: 6절 표대로. metrics.md 7절 제목 "예제 3종"은 승인 뒤 "4종"으로 고쳐도 된다(테스트가 읽지 않는다).

L6. M2 G2 "예제 3종에서 end-to-end". 제안: "4종". 서명 단계 문답을 끝까지 보는 공개 예제가 signoff-final뿐이다. 지시문 2절 원칙이 아니라 9절 마일스톤 문구다. 리드가 2절에 걸린다고 보면 juhan에게 올린다.

L7. 소수 범위 테스트(`Decimal_planted_values_stay_in_column_range_for_every_seed_below_2000`)에 signoff-final을 넣지 않는다(4.3). 제안: 넣지 않는다. 허용오차 안 뒤집기 행이 시드 0~1999에 없어 빈 통과 방지 검사에 걸린다.

L8. classification.md 7.1 요약 표에 `SIGNOFF` 열을 더하지 않는다(classification.md "T-009 개정"). 제안: 더하지 않는다. `SIGNOFF` 한 행은 N1이 본다.

## 승인 결과

- 2026-11-07.
- J1·J2·J3: juhan이 결정 카드에서 "안 A로 모두 승인"을 직접 골랐다. 블록 7개를 정답으로 확정한다(`minutes_per_item`은 적지 않음). 이 카드에 한해 test-engineer가 블록을 바이트 그대로 `examples/signoff-final/`로 옮긴다. 커밋은 8절 모양. V1~V6 승인. 검증 자료는 안 A(정확도 80.0, 누락률 5.0).
- L1~L8: juhan의 상시 승인("끝날때까지 추천값으로 머지하고 진행해 계속")에 따라 리드가 제안대로 승인했다. 지시문은 6절 8곳과 M2 G2("4종")를 고쳤다.
