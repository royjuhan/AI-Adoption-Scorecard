# classification 데이터

`docs/spec/classification.md`의 표·블록·이름표다. 테스트와 oracle이 읽는다. 규칙 글은 본문에 있다.

## 3. 출력

### 3.2 classification.json

```json
{
  "schema_version": 2,
  "case": "<케이스 이름>",
  "steps": [
    {
      "number": 1,
      "name": "<단계 이름, 자유 서술 그대로>",
      "source": "ai | rule",
      "rule_opinion": {
        "assignee": "① | ② | ③ | ④",
        "rule": "SIGNOFF | Q1 | Q2 | Q3 | Q4 | ELSE"
      },
      "differs": false,
      "defaults": ["<IsDefault인 답의 키, case-schema 4.4 표 순서>"],
      "checks": ["<확인 항목 키, 4.7>"],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step | exception_review | human_gate",
          "assignee": "① | ② | ②M | ③ | ④",
          "rule": "AI | SIGNOFF | Q1 | Q2 | Q3 | Q4 | ELSE | PARTIAL | GATE",
          "reason": "<AI 근거 그대로, 또는 근거 문장 4.5. 기본값·추정 표시 포함>"
        }
      ]
    }
  ]
}
```

### 3.3 바이트 규칙

| 항목 | 규칙 | 근거 |
| --- | --- | --- |
| 쓰는 도구 | `System.Text.Json.Utf8JsonWriter`. 리플렉션 직렬화(`JsonSerializer`) 없음 | 후보 코드 |
| 들여쓰기 | 2칸(`Indented = true`) | [S3] |
| 줄바꿈 | `\n`(`JsonWriterOptions.NewLine = "\n"`). CR 0개 | [S3 json-check] |
| 끝 | 마지막 `}` 뒤에 `\n` 하나 | [S3 json-check] |
| 인코딩 | UTF-8, BOM 없음 | [S3 json-check], [P:escape] |
| 이스케이프 | `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` | 아래 표 |

| 단계 이름에 든 글자 | JSON 안 모양 |
| --- | --- |
| ①②③④, 한글 | 그대로 |
| `<>&'+` 와 백틱 | 그대로 |
| 큰따옴표, 역슬래시 | `\"`, `\\` |
| 탭 | `\t` |
| U+0001, U+007F, U+2028 | `\u0001`, `\u007F`, `\u2028` |
| 이모지(U+1F600) | `\uD83D\uDE00` (서로게이트 쌍) |
| NFD 한글(U+1100 U+1161) | 그대로(정규화 안 함) |

## 4. 규칙

### 4.1 결정 트리 (지시문 3.3과 해석)

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

### 4.2 조건 표

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

### 4.4 담당·근거 키·행 종류 값

| 값 | 뜻 |
| --- | --- |
| `①` `②` `③` `④` | 담당. JSON 문자열(U+2460~U+2463). 이스케이프하지 않는다 [P:escape assignee-raw] |
| `②M` | AI 행의 담당(학습 모델, 결정론). 트리는 내지 않는다. 비교 값은 ②(6.6) |
| `AI` | AI 행(`ai`가 있는 단계의 `step` 행)의 근거 키 |
| `ai`, `rule` | 단계의 `source` 값 |
| `SIGNOFF`, `Q1`~`Q4`, `ELSE` | `step` 행의 근거 키 |
| `PARTIAL` | `exception_review` 행의 근거 키 |
| `GATE` | `human_gate` 행의 근거 키 |
| `step`, `exception_review`, `human_gate` | 행 종류 |

### 4.5 근거 문장

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

- 지시문 3.3 예시 문장과 바이트까지 같다: `판단 기준을 글로 쓸 수 있고(yes), 입력이 표 형식이며(table), 매주 반복되므로 규칙 기반 소프트웨어로 분류.`. 그래서 `frequency` 문구에는 값 코드를 붙이지 않았다(예시를 따름).

### 4.6 기본값 표시

- 규칙: 행을 정할 때 트리가 읽은 답(아래 표) 중 하나라도 `IsDefault`면, 그 행 문장 끝에 공백 하나와 `(기본값 사용, 확인 필요)`를 붙인다. 카드 수용 기준 2.

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

### 4.7 확인 항목

| 키 | 조건 | 뜻 |
| --- | --- | --- |
| `measure_minutes_per_item` | `minutes_per_item` 값이 null | 건당 시간을 잰다(지시문 3.2 "측정 권고로 표시") |

### 4.10 결정론·원본 불변·G2·G3

| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |
| --- | --- | --- |
| `classifier-case-valid` | `case-valid` (기존) | `CaseParser.ParseDirectory` → `StepClassifier.Classify` → `ClassificationJson.Render` → 출력 폴더의 `classification.json` 한 파일 |

### 4.11 Core 공개 API (제안)

```csharp
public static class StepClassifier
{
    public const string KindStep = "step";
    public const string KindExceptionReview = "exception_review";
    public const string KindHumanGate = "human_gate";
    public const string SourceAi = "ai";
    public const string SourceRule = "rule";

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
// M2-2: Name 뒤에 Source(ai·rule), RuleOpinion(트리 step 행), Differs(4.13).
public sealed record StepClassification(
    int Number, string Name, string Source, RuleOpinion RuleOpinion, bool Differs,
    IReadOnlyList<string> Defaults, IReadOnlyList<string> Inferred, IReadOnlyList<string> Checks,
    bool AiOveruseCandidate, IReadOnlyList<ClassificationRow> Rows);

public sealed record RuleOpinion(string Assignee, string Rule);

// Used: 트리가 읽은 답(4.6 표). AI 행은 빈 목록. JSON에는 쓰지 않는다.
public sealed record ClassificationRow(
    string Kind, string Assignee, string Rule, IReadOnlyList<string> Used, string Reason);
```

### 4.12 추정 표시 (T-008)

- 추정 표시 규칙: 행을 정할 때 읽은 답(4.6 표) 중 하나라도 `IsInferred`면, 그 행 문장 끝에 공백 하나와 `(AI 추정, 확인 필요)`를 붙인다.
- 기본값 표시와 같이 붙으면 기본값 표시 뒤에 둔다: `… 분류. (기본값 사용, 확인 필요) (AI 추정, 확인 필요)`.

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

## 5. 예외

| 상황 | 결과 | 근거 |
| --- | --- | --- |
| `definition`이 null | `ArgumentNullException` | 후보 코드 |
| 파싱 오류(`CaseParseResult.Case`가 null) | 분류하지 않는다. 부르는 쪽이 오류를 보인다. `Classify`는 모델만 받는다 | 4.11 |
| 열거 값이 선택지 밖 | 생기지 않는다. 파서가 `enum` 오류로 막는다(case-schema 4.6). 후보는 `ArgumentOutOfRangeException` | [읽음], 후보 코드 |
| 답 없는 단계(10개 모두 기본값, T-008) | `ELSE` ② + `PARTIAL` ④, 두 행 모두 기본값 표시, 확인 항목 `measure_minutes_per_item`. `human_signoff` 기본값 `no`라 `SIGNOFF`가 아니다 | [P:mark parser-all-defaults], [T8:P:mark 답 없는 단계] |
| `human_signoff: yes`(T-008) | 다른 답과 상관없이 ④ `SIGNOFF` 한 행. 게이트·예외 검토 행이 없다. 확인 항목은 그대로 달린다. AI 과잉 후보가 아니다 | [T8:P:signoff] |
| `human_signoff`를 적지 않은 단계(T-008) | `no`로 분류하고 `Q1`·`Q2`·`Q3`·`Q4`·`ELSE` 행에 기본값 표시. 부가 행에는 붙지 않는다(4.6) | [T8:P:mark human_signoff=-] |
| `human_signoff` 값이 `Yes`·`true`·빈 문자열·`unknown`(T-008) | 생기지 않는다. 파서가 `enum` 오류로 막는다(case-schema 4.9) | [T8:P:mark] |
| `inferred`가 있는 단계(T-008) | 분류는 같다. 읽은 답이 추정이면 문장 끝에 추정 표시(4.12). classification.json의 속성은 그대로다 | [T8:P:inferred], [T8:P:json] |
| Q4인데 `current_actor: program` | ④로 나온다. "그대로 두기"가 실제로는 프로그램이다. 문장은 같다 | [P:order Q4 ca=program] → 승인 질문 3 |
| `rule_expressible: no`이고 입력이 자유 형식 | Q2. 입력 양식 표준화 제안이 나오지 않는다 | [P:order Q2>Q3] |
| `ai`가 있는 단계(M2-2) | `step` 행만 AI 행(근거 그대로, 표시 없음). 규칙 의견·부가 행은 트리 것. `differs`는 비교 값으로 | ADR 0013 |
| `ai` 담당이 ④가 아닌 `SIGNOFF` 단계(M2-2) | `GATE` 조건이면 ④ `GATE`를 붙인다. `PARTIAL`은 붙지 않는다 | ADR 0013 결정 2 |
| 단계 이름에 제어 문자·이모지 | JSON 이스케이프(3.3). 이름은 바꾸지 않는다 | [P:escape] |
| Windows 줄바꿈으로 받은 `answers.yaml` | 같은 결과 | [P:eol], [S7] |
| publish 단일 exe에서 `System.Text.Json` 동작 | **미확인** | — |

## 6. 예시

### 6.1 분기 예시

기준 답: `input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 10, exception_rate: 0, current_actor: human, minutes_per_item: 1, failure_impact: internal_rework, existing_feature: no, human_signoff: no` (모두 직접 쓴 값). 바꾼 답만 적는다. T-008에서 `human_signoff: no`를 더했다. 그 전 행의 결과는 그대로다.

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

### 6.2 기본값 표시 예시

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
| `existing_feature`(`unknown`) | `step` 행에 표시 |
| `human_signoff`(`no`) | `step` 행에 표시 |
| `human_signoff`, `failure_impact: external` | `step` 행에 표시. 게이트 행에는 없다(게이트 행은 `failure_impact`만 읽는다) |
| `human_signoff`, `rule_expressible: partial` | `step` 행에 표시. 예외 검토 행에는 없다 |
| `human_signoff`, `existing_feature: yes`(Q1) | `step` 행에 표시 |
| `human_signoff`, `frequency: rare, batch_size: 3`(Q4) | `step` 행에 표시 |

파서를 거친 "답 없는 단계":

```
② ELSE: 판단 기준을 일부만 글로 쓸 수 있고(partial), 입력이 고정 양식이며(fixed_form), 매주 반복되므로 규칙 기반 소프트웨어로 분류. (기본값 사용, 확인 필요)
④ PARTIAL: 판단 기준을 일부만 글로 쓸 수 있으므로(partial) 예외 건은 사람이 검토한다. (기본값 사용, 확인 필요)
defaults: 10개 모두. checks: measure_minutes_per_item
```

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

분류 결과. 줄 모양은 `<단계 번호> <담당> <근거 키>: <근거 문장>`, 끝 네 줄은 단계별 `Inferred`(4.4 순서):

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

### 6.6 AI 판단 장면 (M2-2)

| AI 담당 | 비교 값 | 뜻 |
| --- | --- | --- |
| `①` | `①` | 기존 기능·설정 |
| `②` | `②` | 규칙 기반 소프트웨어 |
| `②M` | `②` | 학습 모델(결정론). 미리 학습해 고정한 모델을 규칙 SW처럼 돌린다. 실행 때 AI를 부르지 않고 같은 입력이면 같은 결과다 |
| `③` | `③` | 실행 시 AI 보조 |
| `④` | `④` | 사람 |

AI 판단 장면. 기준 답은 6.1 줄이다. "AI 담당"이 `(없음)`이면 `ai`를 쓰지 않은 단계다. "행"은 `rows`의 담당과 근거 키다. `human_signoff: yes` 행은 본문 4.13 규칙 5다: AI 담당의 비교 값이 ④가 아니면 `GATE` 조건일 때 ④ `GATE`가 붙는다. `PARTIAL`은 붙지 않는다.

| 바꾼 답 | AI 담당 | 규칙 의견 | differs | AI 과잉 후보 | 행 |
| --- | --- | --- | --- | --- | --- |
| (없음) | (없음) | ② `ELSE` | false | false | ② `ELSE` |
| (없음) | `②` | ② `ELSE` | false | false | ② `AI` |
| (없음) | `②M` | ② `ELSE` | false | false | ②M `AI` |
| (없음) | `①` | ② `ELSE` | true | false | ① `AI` |
| (없음) | `③` | ② `ELSE` | true | false | ③ `AI` |
| `existing_feature: yes` | `①` | ① `Q1` | false | false | ① `AI` |
| `existing_feature: yes` | `②` | ① `Q1` | true | false | ② `AI` |
| `rule_expressible: no` | `③` | ③ `Q2` | false | false | ③ `AI` |
| `input_kind: free_doc` | `②M` | ③ `Q3` | true | false | ②M `AI` |
| `frequency: rare, batch_size: 4` | `④` | ④ `Q4` | false | false | ④ `AI` |
| `frequency: rare, batch_size: 4, failure_impact: external` | `④` | ④ `Q4` | false | false | ④ `AI` + ④ `GATE` |
| `rule_expressible: partial, failure_impact: external` | `②M` | ② `ELSE` | false | false | ②M `AI` + ④ `PARTIAL` + ④ `GATE` |
| `rule_expressible: partial, failure_impact: external` | `③` | ② `ELSE` | true | false | ③ `AI` + ④ `PARTIAL` + ④ `GATE` |
| `input_kind: free_doc, failure_impact: external` | `④` | ③ `Q3` | true | false | ④ `AI` + ④ `GATE` |
| `human_signoff: yes` | `④` | ④ `SIGNOFF` | false | false | ④ `AI` |
| `human_signoff: yes` | `③` | ④ `SIGNOFF` | true | false | ③ `AI` |
| `human_signoff: yes, failure_impact: external` | `②` | ④ `SIGNOFF` | true | false | ② `AI` + ④ `GATE` |
| `human_signoff: yes, failure_impact: safety_legal` | `③` | ④ `SIGNOFF` | true | false | ③ `AI` + ④ `GATE` |
| `human_signoff: yes, failure_impact: safety_legal` | `①` | ④ `SIGNOFF` | true | false | ① `AI` + ④ `GATE` |
| `human_signoff: yes, failure_impact: external` | `②M` | ④ `SIGNOFF` | true | false | ②M `AI` + ④ `GATE` |
| `human_signoff: yes, failure_impact: external` | `④` | ④ `SIGNOFF` | false | false | ④ `AI` |
| `human_signoff: yes, failure_impact: internal_rework` | `③` | ④ `SIGNOFF` | true | false | ③ `AI` |
| `human_signoff: yes, rule_expressible: partial, failure_impact: external` | `②` | ④ `SIGNOFF` | true | false | ② `AI` + ④ `GATE` |
| `current_actor: ai_each_time` | (없음) | ② `ELSE` | false | true | ② `ELSE` |
| `current_actor: ai_each_time` | `②M` | ② `ELSE` | false | true | ②M `AI` |
| `current_actor: ai_each_time` | `③` | ② `ELSE` | true | false | ③ `AI` |
| `current_actor: ai_each_time, input_kind: image` | `②` | ③ `Q3` | true | true | ② `AI` |

### 6.7 AI 판단 표본 (M2-2)


TestData 표본. 실제 값 없음. 파일은 블록과 바이트까지 같다. AI 담당이 규칙 의견과 같은 단계, `②M`, 다른 단계(부가 행 남음), `ai`가 없는 단계, 서명 단계를 한 파일에 넣었다.

AI 판단 표본 `tests/Scorecard.Tests/TestData/classification/ai-judgment/answers.yaml`:

```yaml
# 합성 예시. 실제 업무·값 없음. AI 판단(ai)과 규칙 의견(M2-2)을 섞었다.
name: ai-judgment
task: 합성 문의를 접수해 유형을 고르고 답변을 써서 사람이 확인해 보낸다
steps:
  - name: 접수 기록
    answers: {input_kind: table, rule_expressible: yes, frequency: daily, batch_size: 30, exception_rate: 0, current_actor: human, minutes_per_item: 1, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
    ai:
      assignee: ②
      reason: 표의 칸을 정해진 자리로 옮기는 일이라 규칙으로 된다.
  - name: 유형 고르기
    answers: {input_kind: table, rule_expressible: partial, frequency: daily, batch_size: 30, exception_rate: 15, current_actor: ai_each_time, minutes_per_item: 2, failure_impact: external, existing_feature: no, human_signoff: no}
    ai:
      assignee: ②M
      reason: 지난 기록으로 학습한 분류 모델을 고정해 쓴다. 실행할 때 AI를 부르지 않는다.
  - name: 답변 초안 쓰기
    answers: {input_kind: free_doc, rule_expressible: partial, frequency: daily, batch_size: 30, exception_rate: 30, current_actor: human, minutes_per_item: 6, failure_impact: external, existing_feature: no, human_signoff: no}
    ai:
      assignee: ④
      reason: 문의마다 사정이 달라 담당자가 직접 쓴다.
  - name: 월말 집계
    answers: {input_kind: table, rule_expressible: yes, frequency: monthly, batch_size: 1, exception_rate: 0, current_actor: human, failure_impact: internal_rework, human_signoff: no}
  - name: 최종 확인·발송
    answers: {input_kind: free_doc, rule_expressible: no, frequency: daily, batch_size: 30, exception_rate: 10, current_actor: human, minutes_per_item: 2, failure_impact: external, existing_feature: no, human_signoff: yes}
    ai:
      assignee: ④
      reason: 사람이 읽고 서명해 보낸다.
```

AI 판단 표본 classification.json (SHA-256 `a6e77a081ca5e19b061c1e3f99a1cd6db686a7dc398a875a11bf09787ff4e46e`, 3,428바이트):

```json
{
  "schema_version": 2,
  "case": "ai-judgment",
  "steps": [
    {
      "number": 1,
      "name": "접수 기록",
      "source": "ai",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
      "defaults": [],
      "checks": [],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "AI",
          "reason": "표의 칸을 정해진 자리로 옮기는 일이라 규칙으로 된다."
        }
      ]
    },
    {
      "number": 2,
      "name": "유형 고르기",
      "source": "ai",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
      "defaults": [],
      "checks": [],
      "ai_overuse_candidate": true,
      "rows": [
        {
          "kind": "step",
          "assignee": "②M",
          "rule": "AI",
          "reason": "지난 기록으로 학습한 분류 모델을 고정해 쓴다. 실행할 때 AI를 부르지 않는다."
        },
        {
          "kind": "exception_review",
          "assignee": "④",
          "rule": "PARTIAL",
          "reason": "판단 기준을 일부만 글로 쓸 수 있으므로(partial) 예외 건은 사람이 검토한다."
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
      "number": 3,
      "name": "답변 초안 쓰기",
      "source": "ai",
      "rule_opinion": {
        "assignee": "③",
        "rule": "Q3"
      },
      "differs": true,
      "defaults": [],
      "checks": [],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "④",
          "rule": "AI",
          "reason": "문의마다 사정이 달라 담당자가 직접 쓴다."
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
      "name": "월말 집계",
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
      "defaults": [
        "minutes_per_item",
        "existing_feature"
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
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 표 형식이며(table), 매월 반복되므로 규칙 기반 소프트웨어로 분류. (기본값 사용, 확인 필요)"
        }
      ]
    },
    {
      "number": 5,
      "name": "최종 확인·발송",
      "source": "ai",
      "rule_opinion": {
        "assignee": "④",
        "rule": "SIGNOFF"
      },
      "differs": false,
      "defaults": [],
      "checks": [],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "④",
          "rule": "AI",
          "reason": "사람이 읽고 서명해 보낸다."
        }
      ]
    }
  ]
}
```

- 1단계: AI ②, 규칙 ② `ELSE`. 같다.
- 2단계: AI `②M`, 규칙 ② `ELSE`. 비교 값이 ②라 같다. 트리의 `PARTIAL`·`GATE` 행이 남는다. `ai_each_time`이고 비교 값이 ②라 AI 과잉 후보다.
- 3단계: AI ④, 규칙 ③ `Q3`. 다르다(`differs: true`). 트리의 `GATE` 행이 남는다.
- 4단계: `ai`가 없다. `source: rule`. `existing_feature`를 적지 않아 기본값 표시와 `defaults`는 남고, 확인 항목은 `measure_minutes_per_item`뿐이다.
- 5단계: AI ④, 규칙 ④ `SIGNOFF`. 같다. 부가 행이 없다.
- AI 행의 `reason`은 `ai.reason` 그대로다. 표시가 붙지 않는다.

## 7. 공개 예제

### 7.1 요약

| 예제 | 사용자 단계 | 행(모델 A) | ② | ③ | ④ 예외 검토 | ④ 게이트 | 확인 항목 | AI 과잉 후보 | 기본값 표시 문장 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| table-compare | 4 | 6 | 4 | 0 | 1 | 1 | `measure` 4 | 0 | 0 |
| form-fill | 4 | 5 | 4 | 0 | 0 | 1 | `measure` 4 | 1 (2단계) | 1 (1단계) |
| mixed-exceptions | 4 | 5 | 2 | 2 | 0 | 1 | `measure` 4 | 0 | 0 |
| signoff-final | 7 | 9 | 5 | 1 | 1 | 1 | `measure` 7 | 0 | 0 |

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

`examples/table-compare/expected/classification.json` (SHA-256 `84f6f31e2529546392f781f1249fdfd4a0039c3ad9ffb22d6debf05d324699c2`, 3,166바이트):

```json
{
  "schema_version": 2,
  "case": "table-compare",
  "steps": [
    {
      "number": 1,
      "name": "정상 신호 구간 선택",
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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

`examples/form-fill/expected/classification.json` (SHA-256 `e5f1a350b35ba1690024c081476494d3b9c36af6d265f46b754e59e9df262014`, 2,976바이트):

```json
{
  "schema_version": 2,
  "case": "form-fill",
  "steps": [
    {
      "number": 1,
      "name": "주문서 파일 모으기",
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
      "defaults": [
        "minutes_per_item",
        "existing_feature"
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
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 고정 양식이며(fixed_form), 매주 반복되므로 규칙 기반 소프트웨어로 분류. (기본값 사용, 확인 필요)"
        }
      ]
    },
    {
      "number": 2,
      "name": "주문서 양식 읽기",
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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

`examples/mixed-exceptions/expected/classification.json` (SHA-256 `ef0b506ec3f443828e68ff67c8c3bac58491bc64dcca9f2c7c3322d43582e4aa`, 2,984바이트):

```json
{
  "schema_version": 2,
  "case": "mixed-exceptions",
  "steps": [
    {
      "number": 1,
      "name": "메일 접수 기록",
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "③",
        "rule": "Q3"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "③",
        "rule": "Q3"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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

`examples/signoff-final/expected/classification.json` (SHA-256 `fcda03d5b931ee3cb0ae256f10f875e1de9dacc260e0ca175dddecc632cdd279`, 5,119바이트):

```json
{
  "schema_version": 2,
  "case": "signoff-final",
  "steps": [
    {
      "number": 1,
      "name": "기록 파일 열기·정리",
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "②",
        "rule": "ELSE"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "③",
        "rule": "Q2"
      },
      "differs": false,
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
      "source": "rule",
      "rule_opinion": {
        "assignee": "④",
        "rule": "SIGNOFF"
      },
      "differs": false,
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
