# 잠재 빈 곳 14~25 확인 결과

- 지시: `README.md` 4절 2항. 빈 곳마다 합성 예제 1개를 지금 엔진에 넣고 "막힘 / 틀림 / 문제없음" 중 하나로 적는다. 막힘·틀림만 카드로 만든다.
- 작성: prototyper, 2026-11-08.
- 엔진: 커밋 `79d0de3`의 `src/Scorecard.Core` 사본. 작업 트리의 진행 중 수정은 섞지 않았다. .NET SDK 10.0.114. 엔진 코드는 읽지 않았다. 공개 API(`classification.md` 4.11, `metrics.md` 4.11, `judgment.md` 4.13, `case-schema.md` 4.12)만 불렀다.
- 도구: `spikes/gap-runner/`(솔루션 밖 콘솔, 머지 대상 아님). `CaseParser.ParseDirectory`(answers 파일은 `CaseParser.Parse`) → `StepClassifier.Classify` → `CompareEngine.CompareDirectory` → `JudgmentEngine.Judge`. JSON은 `ClassificationJson`·`MetricsJson`·`JudgmentJson.Render` 바이트 그대로 `spikes/gap-NN/out/`에 쓴다. 요약 줄은 `spikes/gap-NN/result.txt`.
- 재현: `python3 -I spikes/gap-runner/make-gaps.py` → `spikes/gap-runner/run-gaps.sh <작업폴더>`. 만들기·돌리기를 두 번 해서 파일 197개의 SHA-256이 같았다.
- 합성: 이름·값은 모두 `make-gaps.py`가 지어냈다. 실제 회사 자료·이름·수치가 없다(지시문 2절 8·10). 회사 이름 검사는 5절.
- 사람 입력: `--reviewed`는 "사람이 보고 모두 설명함"(`unexplainedKeys` = 빈 목록)이다. 표시가 없으면 null(아직 안 봄). 시간은 넣지 않았다. 자료가 있는 예제는 모두 `time_must_drop: false`다. `thresholds`가 없는 예제는 자료도 없어 미검증이다.

판정 뜻:

- 막힘: 엔진이 그 구조를 입력으로 받지 못하거나, 필요한 값을 낼 자리가 없다.
- 틀림: 받지만 결과(분류·지표·판정)가 지시문·사례의 기대와 다르다.
- 문제없음: 지금 엔진으로 맞게 나온다.

## 1. 요약

| # | 빈 곳 | 판정 | 한 줄 근거 | README 2절 겹침 |
| --- | --- | --- | --- | --- |
| 14 | 중첩 구조 대조 | 막힘 | 키 하나에 줄이 여럿이면 `duplicate-key`. 머리·줄 두 파일은 `column` 오류. 머리만 대조하면 줄 누락이 보이지 않고 통과 | — |
| 15 | 집합·순위 결과 | 막힘 | `compare`가 없으면 미검증. 정밀도·순위 지표 자리가 없다. (질의, 문서) 키는 `duplicate-key` | 6 |
| 16 | 표본 크기·신뢰구간 | 틀림 | 19/20과 190/200이 같은 통과. 20건의 Wilson 95% 구간은 [76.4, 99.1] | — |
| 17 | 기준 자료 신뢰도 | 막힘 | 기준 두 벌을 같이 넣으면 `duplicate-key` 20건. 따로 재면 사람 간 85%, AI 90%가 문턱 95로 보류 | 2 |
| 18 | 시간에 따른 저하 | 막힘 | 회차마다 따로 판정한다. 추세 자리가 없다. 회차를 `candidate/`에 같이 두면 정확도 0.0% | 8 |
| 19 | 응답 시간·처리량 | 막힘 | 새 답 키는 `unknown-field`. 하루 1,152만 건과 20건이 같은 분류·같은 문장 | 1 |
| 20 | 오류 되돌림 | 막힘 | 새 답 키는 `unknown-field`. 되돌릴 수 있는 단계와 없는 단계가 같은 `GATE` 행 | 1, 13 |
| 21 | 개인정보·규제 플래그 | 막힘 | 최상위 `data_class`는 `unknown-field`. 개인 이름 열이 있는 자료도 경고 없이 통과 | 13 |
| 22 | 단계 간 오류 전파 | 막힘 | `input_from`은 `unknown-field`. 대조가 케이스에 하나라 단계에 붙지 않는다. 2단계만 재면 100% 통과, 끝에서 끝 50% | — |
| 23 | ③ 호출 비용 | 막힘 | 새 답 키는 `unknown-field`. 판정·분류 어디에도 비용 값 자리가 없다 | 3, 7 |
| 24 | 입력 형식 변환 | 막힘 | xlsx 기준 자료는 `encoding` 오류. 분류는 Q3 문장이 표준화 검토를 이미 말한다 | — |
| 25 | 케이스 간 비교 | 막힘 | 입구가 케이스 폴더 하나뿐. 묶음 폴더는 `case.yaml: file: 파일이 없다` | — |

- 막힘 11, 틀림 1, 문제없음 0.
- 판정은 빈 곳의 본 증상으로 정했다. 같은 예제에서 함께 드러난 틀림은 아래에 "부"로 적었다: 14(머리만 대조하면 통과), 15(순위 열 20%), 17(문턱 > 사람 간 일치율), 18(회차를 섞으면 0.0%), 22(2단계만 재면 통과).

## 2. 대조 쪽 (14, 15, 17, 18, 22, 24)

### 14 중첩 구조 대조 — 막힘

- 예제 `spikes/gap-14/`: 송장 3장(머리)과 품목 7줄. 결과 자료에서 `inv02` 3번째 줄이 빠졌다.
- `nested`(머리 CSV + 품목 CSV, 키 `invoice_id`): `reference/headers.csv:1: column amount: 머리 행에 열이 없다`, `reference/lines.csv:1: column total: 머리 행에 열이 없다`(양쪽 4건).
- `lines-only`(품목만, 키 `invoice_id`): `reference/lines.csv:3:1: duplicate-key invoice_id: 키가 중복된다: inv01 (처음: reference/lines.csv:2)` 외 3건.
- 부(틀림 위험) `headers-only`: `JUDGE verdict=pass rule=PASS`. 정확도 100.0%(3/3). 품목 줄 누락이 보이지 않는다.
- 우회 `flat`(사용자가 `line_id` = `inv02-3`을 만듦): `ITEM inv02-3 missing`, 정확도 85.7%(6/7). 줄 단위는 잡히지만 "줄이 모두 맞은 송장 수"를 낼 자리가 없다.
- 카드에 필요한 것: 복합 키(`inputs.key` 여러 열)와 자식 대조(`compare.children: [{file, parent_key}]`), 머리 단위 셈(줄이 모두 맞은 머리 / 머리). metrics.json `schema_version` 올림.

### 15 집합·순위 결과 대조 — 막힘

- 예제 `spikes/gap-15/`: 기준 5개(`d01`~`d05`), 후보 5개(`d01`, `d03`, `d02`, `d06`, `d07`). 3개 겹침.
- `set-only`(키 열만): `COMPARE status=not_verified reason="no_compare_fields"`. 집합 자체가 결과인 구조는 미검증이 된다.
- 우회 `set-hit`(모든 행에 `listed = y`): 정확도 60.0%(3/5) = 재현율. 추가 2건은 `extra`로 셈에만 있다. 정밀도(3/5)를 낼 자리가 없다.
- 부(틀림) `ranked`(순위 열 정확 일치): 정확도 20.0%(1/5). `d02`·`d03`은 `rank outside 2->3`, `3->2`. 앞 3개가 모두 관련 문서인데(precision@3 = 3/3) 순위가 한 칸 바뀐 것을 값 불일치로 센다.
- `multi-query`(질의 둘): `reference/topk.csv:7:4: duplicate-key doc_id: 키가 중복된다: d01`. (질의, 문서) 짝을 키로 쓸 수 없다.
- 카드에 필요한 것: 키만 대조하는 집합 모드(`compare` 없이 measured), 정밀도·재현율·precision@k를 metrics에, 복합 키(14와 같음). 판정 문턱 키 추가.
- README 2절 겹침: 6(정밀도·재현율 이름. 6은 범주 필드, 15는 집합).

### 17 기준 자료 신뢰도 — 막힘

- 예제 `spikes/gap-17/`: 20건 불량 판정. 평가자 B는 A와 3건, AI는 A와 2건 다르다.
- `two-refs`(`reference/`에 A·B 두 파일): `reference/rater_b.csv:2:1: duplicate-key item_id: 키가 중복된다: i01 (처음: reference/rater_a.csv:2)` 외 19건.
- 부(틀림) `a-vs-b`: 정확도 85.0%(17/20) = 사람 간 일치율. `a-vs-ai`: 90.0%(18/20)인데 `JUDGE verdict=hold rule=ACCURACY: 정확도 90.0%가 문턱 95%에 못 미치므로 보류.` 기준 자료끼리도 85%만 맞는데 문턱은 95다. 엔진은 두 수치의 관계를 모른다.
- 카드에 필요한 것: 기준 여러 벌(예: `inputs.raters`), 평가자 간 일치율을 metrics에, 판정에 "문턱이 일치율보다 높음" 확인과 `confirm_thresholds`류 할 일.
- README 2절 겹침: 2(사람 점수표 CSV 집계와 같은 "사람 여러 명" 입력).

### 18 시간에 따른 저하 — 막힘

- 예제 `spikes/gap-18/`: 같은 기준 20행, 결과 3회(95% → 90% → 85%), 문턱 90.
- `run1`~`run3`: 통과, 통과, 보류(`정확도 85.0%가 문턱 90%에 못 미치므로 보류.`). 각 판정은 앞 회차를 모른다. 하락 추세를 낼 자리가 없다.
- 부(틀림) `merged`(세 회차를 `candidate/`에 같이): `COUNTS reference=20 candidate=60 match=0 mismatch=20`, 정확도 0.0%, 검토 항목 60건(불일치 20건, 중복 행 40건). 이력을 한 케이스에 두면 틀린 수치가 나온다.
- 카드에 필요한 것: check 이력 저장(엔진 밖, 회차별 metrics.json 보관)과, 이전 회차 결과를 받는 추세 확인(판정 입력 추가). 재검증 주기 제안.
- README 2절 겹침: 8(②M 재검증).

### 22 단계 간 의존·오류 전파 — 막힘

- 예제 `spikes/gap-22/`: 1단계 영수증 금액 읽기(③), 2단계 거래처별 합계(②). 영수증 10장 중 `rc07` 1장을 AI가 틀렸다.
- `chain-key`: `case.yaml:10:5: unknown-field steps[2].input_from: 알 수 없는 필드다`.
- `inputs`·`compare`가 케이스에 하나뿐이다. 어느 단계를 잰 것인지 적을 곳이 없다. 분류 행은 단계별, 판정은 케이스 하나다.
- 부(틀림) 같은 단계 구조로 셋을 따로 쟀다: `step1-only` 90.0%(9/10) 보류, `step2-only` 100.0%(2/2) 통과, `end-to-end` 50.0%(1/2) 보류(`ITEM v-b mismatch total outside 250->259`). 2단계만 재면 통과로 과대평가된다.
- 단계 정확도의 곱(0.9 × 1.0 = 90%)도 끝에서 끝(50%)과 다르다. 2단계가 영수증을 거래처로 모아 셈 단위가 바뀌기 때문이다. README 구현 후보 "체인 정확도 = 곱"은 이 구조에서 맞지 않는다.
- 카드에 필요한 것: 대조에 단계 지정(예: `inputs.step` 또는 단계별 `inputs`·`compare`)과 끝에서 끝 대조를 따로 두기. 판정은 끝에서 끝 수치를 쓰고, 단계별 수치로 병목 단계를 표시.

### 24 입력 형식 변환 단계 — 막힘

- 예제 `spikes/gap-24/`: 손글씨 근무표 집계 한 단계(`input_kind: image`, 기준을 글로 쓸 수 있음).
- `image-step`: `ROW 1 ③ Q3 step: 입력이 이미지이므로(image) 입력 양식 표준화를 먼저 검토하고, 안 되면 실행 시 AI 보조로 분류. 해석만 AI, 계산·저장은 코드로 한다.` 변환 단계 행은 따로 없다. 문장이 "해석만 AI, 계산은 코드"를 이미 말한다(분류는 지시문 3.3대로다).
- `xlsx`(`reference/*.xlsx`): `reference/sheet.xlsx:1: encoding: UTF-8이 아니다 (바이트 16)`. xlsx라서 안 된다는 말이 없다.
- 카드에 필요한 것: xlsx 기준 자료 읽기(첫 시트 → CSV와 같은 행 모델. 프레임워크 압축·XML만 쓰면 G1 문제 없음, **미확인**). 작은 일 먼저: zip 서명(`PK`) 파일이면 "CSV로 저장한다" 메시지. 변환 단계 자동 제안은 분류 행 종류 추가라 따로 묻는다.

## 3. 판정 쪽 (16)

### 16 표본 크기·신뢰구간 — 틀림

- 예제 `spikes/gap-16/`: `n20` 19/20, `n200` 190/200(같은 95%), `n20-80` 16/20. 문턱 95, 사람이 모두 설명함(`--reviewed`).
- `n20`: `JUDGE verdict=pass rule=PASS`, `정확도 95.0%(19/20)가 문턱 95% 이상이다.` `n200`도 같은 판정·같은 문장(셈만 다름).
- 엔진 밖 계산(`spikes/gap-16/wilson.txt`): `n20` [76.4, 99.1] 폭 22.7, `n200` [91.0, 97.3] 폭 6.2. 20건 통과는 문턱 95를 확신할 수 없다.
- 지시문 3.4 규칙대로는 맞다. README 3절의 기대("폭이 넓으면 표본 부족")와 다르다. 그래서 틀림으로 적었다.
- 부: `n20-80`(16/20)은 보류다. 구간 상한 91.9가 95보다 낮아 보류가 맞다. README 증상 문장의 "20건 80% 보류"는 문제가 아니고, 문제는 소표본 통과 쪽이다.
- 카드에 필요한 것: 판정에 구간 확인 하나(예: `SAMPLE`, 결과 `enough`/`too_few`)와 표시 문구. 문턱 t가 구간 안인지는 (k − n·t)² ≤ z²·n·t(1 − t)로 제곱근 없이 정확히 셀 수 있다(k 일치, n 기준 행, z² = 3.8416. 엔진 밖 계산으로만 확인). 구간을 글자로 보이려면 decimal 제곱근 방법을 정해야 한다. judgment.json `schema_version` 올림.

## 4. 질문·스키마·명령 쪽 (19, 20, 21, 23, 25)

### 19 응답 시간·처리량 요건 — 막힘

- 예제 `spikes/gap-19/`: 하루 1,152만 건(분당 8천 건) 단계와 하루 20건 단계. 다른 답은 같다.
- `volume`: 두 단계 모두 `② ELSE` + `④ GATE`, 문장도 같다(`매일 반복되므로 규칙 기반 소프트웨어로 분류.`). `batch_size`는 `rare`일 때만 문장에 나온다.
- `answers-latency.yaml`: `unknown-field steps[1].answers.max_latency_ms: 알 수 없는 필드다`.
- 카드에 필요한 것: 질문(예: 최대 처리량 건/분, 건당 허용 시간)과 분류 결과에 납품 형태(배치 exe / 상주 서비스) 자리. 질문 수 상한(README 4절 4)에 걸린다(이 문서 6절).
- README 2절 겹침: 1(하루 건수가 크면 표본 게이트).

### 20 오류 되돌림 가능성 — 막힘

- 예제 `spikes/gap-20/`: 안내 메시지 보내기(되돌릴 수 있음)와 환급 송금 실행(되돌릴 수 없음)이 둘 다 `external`, 계정 차단은 `safety_legal`.
- `reversible`: 세 단계 모두 `② ELSE` + `④ GATE human_gate`. 1·2단계는 문장까지 같다(`틀리면 외부에 영향이 있으므로(external) 이 단계 뒤에 사람 게이트를 둔다.`).
- `answers-reversible.yaml`: `unknown-field steps[1].answers.reversible: 알 수 없는 필드다`.
- 카드에 필요한 것: 질문 `reversible`(yes/no)과 게이트 행 세분(건별/표본/이관, 1번), 되돌릴 수 없는 단계의 안전망 행(13번). 1번 카드와 같이 여는 것이 낫다.
- README 2절 겹침: 1(게이트 종류), 13(규칙 안전망).

### 21 개인정보·규제 자료 플래그 — 막힘

- 예제 `spikes/gap-21/`: 합성 회원 이름 열(`합성회원01` 등)이 있는 방문 기록.
- `flag-key`: `case.yaml:5:1: unknown-field data_class: 알 수 없는 필드다`.
- `personal`: `JUDGE verdict=pass rule=PASS`. 자료 등급을 적을 곳도, 경고할 곳도 없다.
- 카드에 필요한 것: `case.yaml` 최상위 `data_class`(단계 질문이 아니라 케이스 키)와 평가서 경고·번들 제한. 번들 내보내기가 아직 없어(`build-request.md` 미작성) 그 부분은 **미확인**.
- README 2절 겹침: 13(동의 조건 안전망).

### 23 ③ 호출 비용 입력 — 막힘

- 예제 `spikes/gap-23/`: 하루 5만 건 문의 분류(③ `Q3`)와 주문 번호 확인(②, `current_actor: ai_each_time`).
- `volume`: `STEP 2 주문 번호 확인 | … overuse=true`. AI 과잉 후보는 나오지만 비용·손익분기 건수는 없다.
- `answers-cost.yaml`: `unknown-field steps[1].answers.ai_cost_per_item: 알 수 없는 필드다`.
- 카드에 필요한 것: 질문 `ai_cost_per_item`과 완료당 비용 계산. 지시문 3.4의 완료당 비용은 지금 판정 범위 밖이다(`judgment.md` 1절). 같은 카드에서 정한다.
- README 2절 겹침: 3·7(단순 기준 vs AI. "AI가 꼭 필요했나").

### 25 케이스 간 비교 — 막힘

- 예제 `spikes/gap-25/multi/`: 케이스 3개. a(② 2단계, 대조 100%), b(③ + ②, 대조 80%), c(자료 없음).
- `multi`(묶음 폴더): `case.yaml: file: 파일이 없다`. 하나씩은 `pass`, `hold`, `not_verified`로 나온다. 모아 볼 입구와 출력이 없다.
- 카드에 필요한 것: 여러 케이스 요약(케이스별 ② 비율·건수·판정)을 Core에서 만들고 `scorecard list`가 보인다. M2 명령 틀 뒤.

## 5. 회사 이름 검사

- 목록: `README.md` 1절에서 읽는다. 표 "사례" 칸의 앞 영문 이름과 "출처:" 줄의 이름이다. 이 파일과 검사 도구에는 이름을 적지 않았다.
- 대상: `spikes/gap-*` 아래 모든 파일(예제·결과·JSON·도구, xlsx는 안의 파일까지)과 이 파일. 영문 대소문자 무시, 낱말 단위.
- 도구: `python3 -I spikes/gap-runner/check-names.py`(저장소 루트에서). 맞는 것이 있으면 종료 1.
- 결과: `rows 50, names 53, files 203, hits 0, sdk-id skipped 1`. 이름 53개는 사례 50행의 앞 영문 이름, 출처 줄 이름, 여러 낱말 이름의 첫 낱말이다. 맞은 것 0건. 뺀 1건은 `spikes/gap-runner/GapRunner.csproj`의 `.NET.Sdk` SDK 식별자다(도구 이름, 사례 아님).

## 6. 우선순위에 끼울 자리 (README 4절 3항 제안)

- 16(틀림 확인): 지금 자리(12 수정 기록 다음) 그대로.
- 22(막힘 확인): 지금 자리(16 다음) 그대로. 구현 후보는 "곱"이 아니라 "단계별 대조 + 끝에서 끝 대조"로 바꾼다(이 문서 2절 22).
- 17: 16 바로 뒤로 당긴다(제안). 문턱이 사람 간 일치율보다 높으면 보류가 AI 탓인지 기준 자료 탓인지 가를 수 없다. 16과 같은 "판정 신뢰" 묶음이다.
- 14·15: 복합 키를 먼저 한 카드로 묶는다(14 `lines-only`, 15 `multi-query`가 같은 이유로 막힌다). 그 뒤 14 자식 대조, 15 집합 지표. 자리는 지금 14 자리.
- 20: 1 게이트 종류와 같은 카드로 묶는다(지금 1 → 20 순서와 맞다).
- 19·20·21·23 질문 수: 지금 단계 답 10개(`case-schema.md` 4.4). 20(+1) = 11, 23(+1) = 12, 19(+1~2) = 13~14라 19에서 4절 4의 12개 상한을 넘는다. 21은 케이스 키라 단계 질문에 들지 않는다. "고급 질문" 묶음 카드를 19 앞에 둔다.
- 24: xlsx 오류 문구만 먼저 작은 카드로. xlsx 읽기는 지금 자리 그대로.
- 18·25: 엔진 밖 저장·명령이 필요하다. 지금 자리(뒤쪽, M2 뒤) 그대로. 18의 "회차를 `candidate/`에 섞으면 0.0%"는 사용 안내 문서로 먼저 막는다.
