# judgment 데이터

`docs/spec/judgment.md`의 표·블록·이름표다. 테스트와 oracle이 읽는다. 규칙 글은 본문에 있다.

## 0. 표기

- 판정 값: `pass`(통과), `conditional`(조건부), `hold`(보류), `not_verified`(미검증). 지시문 3.4의 넷이다.

## 3. 출력

### 3.2 judgment.json

```json
{
  "schema_version": 2,
  "case": "<케이스 이름>",
  "verdict": "pass | conditional | hold | not_verified",
  "rule": "NOT_VERIFIED | ACCURACY | MISSING | UNEXPLAINED | TIME | FOCUS | PASS",
  "reason": "<판정 문장, 4.3. 기본값 표시 포함>",
  "thresholds": {"accuracy_min": 0, "missing_max": 0, "time_must_drop": true},
  "defaults": ["<기본값을 쓴 문턱 키, 4.2 표 순서>"],
  "checks": [{"rule": "ACCURACY", "result": "met", "reason": "<확인 문장, 4.4>"}],
  "todo": [{"key": "<다음 할 일 키, 4.7>", "text": "<문구>"}]
}
```

| 속성 | 형식 | 뜻 |
| --- | --- | --- |
| `schema_version` | 정수 | 파일 모양 버전. 지금 `2`. 늘 첫 키 |
| `case` | 문자열 | `case.yaml`의 `name` |
| `verdict` | 문자열 | `pass`, `conditional`, `hold`, `not_verified` |
| `rule` | 문자열 | 판정 근거 키(4.3) |
| `reason` | 문자열 | 판정 문장(4.3 틀). 기본값 표시(4.6) 포함 |
| `thresholds` | 객체 | 판정에 쓴 문턱. 기본값을 쓴 것도 값으로 적는다 |
| `thresholds.accuracy_min` | 숫자 | `case.yaml` 값 그대로(자릿수 유지) 또는 기본값(4.2) |
| `thresholds.missing_max` | 숫자 | 같음 |
| `thresholds.time_must_drop` | 불리언 | 같음 |
| `defaults` | 문자열 배열 | 기본값을 쓴 문턱 키. 4.2 표 순서. 없으면 `[]` |
| `checks` | 객체 배열 | 확인 다섯(4.4 순서). 미검증이면 `[]` |
| `checks[]` | 객체 | 확인 하나 |
| `checks[].rule` | 문자열 | `ACCURACY`, `MISSING`, `UNEXPLAINED`, `TIME`, `FOCUS` |
| `checks[].result` | 문자열 | 4.4 표의 결과 |
| `checks[].reason` | 문자열 | 확인 문장(4.4 틀). 기본값 표시(4.6) 포함 |
| `todo` | 객체 배열 | 다음 할 일(4.7 순서). 없으면 `[]` |
| `todo[]` | 객체 | 할 일 하나 |
| `todo[].key` | 문자열 | 4.7 표의 키 |
| `todo[].text` | 문자열 | 4.7 표의 문구 |

## 4. 규칙

### 4.1 개요

```text
판정(케이스의 `thresholds`로 숫자 조정): 통과 = 정확도 ≥ `accuracy_min` 그리고 누락률 ≤ `missing_max` 그리고 (처리+검토 시간) < 적용 전 시간. 조건부 = 정확도는 충족하나 시간이 줄지 않았거나 특정 유형에만 불일치가 몰림. 보류 = 정확도 미달 또는 설명 안 되는 불일치. 미검증 = 자료 없음(무엇을 재면 되는지 적는다).
```

### 4.2 문턱 기본값

| 키 | 기본값 | 근거 |
| --- | --- | --- |
| `accuracy_min` | `95` | 지시문 12.1 비공개 케이스 예시의 값. 지시문에 있는 유일한 값이다 |
| `missing_max` | `2` | 같음 |
| `time_must_drop` | `true` | 같음. 지시문 3.4 통과 조건에 시간이 들어 있다 |

### 4.3 판정 표

| 순서 | 근거 키 | 확인 결과 | 판정 | 판정 문장 틀 |
| --- | --- | --- | --- | --- |
| 1 | `NOT_VERIFIED` | `no_inputs` | `not_verified` | `검증 자료(case.yaml inputs)가 없어 미검증.` |
| 1 | `NOT_VERIFIED` | `no_compare_fields` | `not_verified` | `대조할 열(case.yaml compare)이 없어 미검증.` |
| 2 | `ACCURACY` | `not_met` | `hold` | `정확도 {accuracy_pct}%가 문턱 {accuracy_min}%에 못 미치므로 보류.` |
| 3 | `MISSING` | `not_met` | `hold` | `누락률 {missing_rate_pct}%가 문턱 {missing_max}%를 넘으므로 보류.` |
| 4 | `UNEXPLAINED` | `found` | `hold` | `사람이 설명할 수 없다고 적은 검토 항목 키가 {unexplained}개 있으므로 보류.` |
| 5 | `TIME` | `not_measured` | `conditional` | `정확도·누락률은 문턱을 충족하나 시간을 재지 않아 줄었는지 알 수 없으므로 조건부.` |
| 5 | `TIME` | `not_met` | `conditional` | `정확도·누락률은 문턱을 충족하나 처리·검토 시간 {total}분이 적용 전 {before}분보다 짧지 않으므로 조건부.` |
| 6 | `FOCUS` | `found` | `conditional` | `정확도·누락률은 문턱을 충족하나 불일치 {bad}건이 모두 {group} = {value}인 행이므로 조건부.` |
| 7 | `UNEXPLAINED` | `not_reviewed` | `conditional` | `정확도·누락률은 문턱을 충족하나 검토할 항목 {items}건({breakdown})을 사람이 아직 보지 않았으므로 조건부.` |
| 8 | `PASS` | — | `pass` | `정확도·누락률이 문턱을 충족하고 조건부·보류 조건에 걸리지 않으므로 통과.` |

### 4.4 확인 다섯

| 확인 | 결과 | 걸리는 조건 | 확인 문장 틀 |
| --- | --- | --- | --- |
| `ACCURACY` | `met` | `match × 100 ≥ accuracy_min × reference` | `정확도 {accuracy_pct}%({match}/{reference})가 문턱 {accuracy_min}% 이상이다.` |
| `ACCURACY` | `not_met` | 그 밖 | `정확도 {accuracy_pct}%({match}/{reference})가 문턱 {accuracy_min}%에 못 미친다.` |
| `MISSING` | `met` | `missing × 100 ≤ missing_max × reference` | `누락률 {missing_rate_pct}%({missing}/{reference})가 문턱 {missing_max}% 이하다.` |
| `MISSING` | `not_met` | 그 밖 | `누락률 {missing_rate_pct}%({missing}/{reference})가 문턱 {missing_max}%를 넘는다.` |
| `UNEXPLAINED` | `none` | 검토 항목 0건 | `검토할 항목(불일치·추가 행·중복 행)이 없다.` |
| `UNEXPLAINED` | `not_reviewed` | 검토 항목 1건 이상, 사람 입력 없음(null) | `검토할 항목 {items}건({breakdown})이 설명되는지 사람이 아직 보지 않았다.` |
| `UNEXPLAINED` | `explained` | 사람 입력이 빈 목록 | `검토할 항목 {items}건({breakdown})을 사람이 보고 모두 설명했다.` |
| `UNEXPLAINED` | `found` | 사람 입력에 키가 1개 이상 | `검토할 항목 {items}건({breakdown}) 중 키 {unexplained}개를 사람이 설명할 수 없다고 적었다.` |
| `TIME` | `off` | `time_must_drop`이 `false` | `시간 조건을 쓰지 않는다(time_must_drop: false).` |
| `TIME` | `not_measured` | `time_must_drop`이 `true`, 시간 없음(null) | `시간을 재지 않았다.` |
| `TIME` | `met` | `process + review < before` | `처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧다.` |
| `TIME` | `not_met` | 그 밖(같으면 줄지 않았다) | `처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧지 않다.` |
| `FOCUS` | `few` | 불일치가 `focus_min`(4.5)보다 적음 | `불일치가 {bad}건이라 몰림을 보지 않는다.` |
| `FOCUS` | `off` | `inputs.group`을 적지 않음 | `case.yaml inputs.group에 유형 열을 적지 않아 불일치 {bad}건이 한 유형에 몰렸는지 보지 않는다.` |
| `FOCUS` | `one_value` | 그 열이 기준 자료에서 값 하나뿐 | `{group} 열이 기준 자료에서 값이 하나뿐이라 몰림을 볼 수 없다.` |
| `FOCUS` | `found` | 불일치가 모두 그 열의 한 값(4.8) | `불일치 {bad}건이 모두 {group} = {value}인 행이다({value} {rows_in}행 중 {bad}행, 다른 값 {rows_out}행 중 0행).` |
| `FOCUS` | `spread` | 그 밖 | `불일치 {bad}건이 {group} 열의 한 값에 몰리지 않았다.` |

### 4.5 문장 값과 숫자

| 이름 | 값 | 뜻 |
| --- | --- | --- |
| `focus_min` | `2` | 몰림을 보는 최소 불일치 수. 1건은 "몰림"이 아니다 |

### 4.6 기본값 표시

- 표시 글자는 `classification.md` 4.6과 같다: 공백 하나 + `(기본값 사용, 확인 필요)`. 문장 끝에 한 번.
- 확인 문장: 그 확인이 쓰는 문턱이 기본값일 때. `ACCURACY` → `accuracy_min`, `MISSING` → `missing_max`, `TIME` → `time_must_drop`. `UNEXPLAINED`·`FOCUS`는 문턱을 쓰지 않는다.

| 순서 | 근거 키 | 읽은 문턱 |
| --- | --- | --- |
| 1 | `NOT_VERIFIED` | — |
| 2 | `ACCURACY` | `accuracy_min` |
| 3 | `MISSING` | `accuracy_min`, `missing_max` |
| 4 | `UNEXPLAINED` | `accuracy_min`, `missing_max` |
| 5 | `TIME` | `accuracy_min`, `missing_max`, `time_must_drop` |
| 6 | `FOCUS` | `accuracy_min`, `missing_max`, `time_must_drop` |
| 7 | `UNEXPLAINED` | `accuracy_min`, `missing_max`, `time_must_drop` |
| 8 | `PASS` | `accuracy_min`, `missing_max`, `time_must_drop` |

### 4.7 다음 할 일

| 다음 할 일 | 나오는 때 | 문구 |
| --- | --- | --- |
| `add_data` | 미검증 `no_inputs` | `기준 자료(사람이 만든 결과)를 reference/에, 결과 자료(AI 적용 후 결과)를 candidate/에 넣고 case.yaml inputs에 파일 패턴과 키 열을 적는다.` |
| `add_compare` | 미검증(둘 다) | `case.yaml compare에 대조할 열과 허용 오차를 적는다.` |
| `measure_time` | `TIME` = `not_measured`, 또는 미검증이고 `time_must_drop`이 `true` | `건당 처리 시간·검토 시간과 적용 전 시간(분)을 잰다.` |
| `review_items` | `UNEXPLAINED` = `not_reviewed` | `검토할 항목 {items}건({breakdown})을 보고 설명이 되는지 적는다.` |
| `add_group` | `FOCUS` = `off` | `불일치가 한 유형에 몰렸는지 보려면 case.yaml inputs.group에 기준 자료의 유형 열 이름을 적는다.` |
| `confirm_thresholds` | 기본값을 쓴 문턱이 있음 | `문턱에 기본값을 썼다({defaults}). 맞는지 확인하고 case.yaml thresholds에 적는다.` |

### 4.9 "설명 안 되는 불일치"와 검토 항목

| 검토 항목 | 항목 상태 | {breakdown} 이름 |
| --- | --- | --- |
| 불일치 | `mismatch`, `missing` | 불일치 |
| 추가 행 | `extra` | 추가 행 |
| 중복 행 | `duplicate` | 중복 행 |

- `{items}`: 검토 항목 수. `{breakdown}`: 위 표의 이름마다 수를 세어 0이 아닌 것만 `이름 N건`으로 쓰고 `, `로 잇는다. 예: `불일치 3건, 추가 행 1건`.

### 4.12 결정론·원본 불변·G1·G2·G3

| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |
| --- | --- | --- |
| `judge-case-valid` | `case-valid` (기존. 바꾸지 않는다) | `CaseParser.ParseDirectory` → `CompareEngine.CompareDirectory` → `JudgmentEngine.Judge`(시간·사람 입력 null) → 출력 폴더의 `judgment.json` 한 파일 |

- 출력 바이트가 바뀐다(SHA-256 `c955e9e236ba726db289aafe0729d880fbc98c988a30c2daf62ea91086412fb1`). G2·G3는 같은 실행 안의 비교라 그대로 통과한다(T-012b에서 확인).

### 4.13 Core 공개 API (제안)

```csharp
public static class JudgmentEngine
{
    public const string VerdictPass = "pass";
    public const string VerdictConditional = "conditional";
    public const string VerdictHold = "hold";
    public const string VerdictNotVerified = "not_verified";
    public const string RuleNotVerified = "NOT_VERIFIED";
    public const string RuleAccuracy = "ACCURACY";
    public const string RuleMissing = "MISSING";
    public const string RuleUnexplained = "UNEXPLAINED";
    public const string RuleTime = "TIME";
    public const string RuleFocus = "FOCUS";
    public const string RulePass = "PASS";

    // 순수 함수. times: 재지 않았으면 null. unexplainedKeys: 사람이 아직 보지 않았으면 null.
    public static JudgmentResult Judge(CaseDefinition definition, CompareMetrics metrics, JudgmentTimes? times = null, IReadOnlyList<string>? unexplainedKeys = null);
}

public static class JudgmentJson
{
    public const string FileName = "judgment.json";
    public const int SchemaVersion = 2;

    // 3.2·3.3 바이트 규칙.
    public static byte[] Render(JudgmentResult result);
}

public sealed record JudgmentTimes(decimal BeforeMinutes, decimal ProcessMinutes, decimal ReviewMinutes);
public sealed record JudgmentThresholds(decimal AccuracyMin, decimal MissingMax, bool TimeMustDrop);
public sealed record JudgmentCheck(string Rule, string Result, string Reason);
public sealed record JudgmentTodo(string Key, string Text);
public sealed record JudgmentResult(
    string Case, string Verdict, string Rule, string Reason, JudgmentThresholds Thresholds,
    IReadOnlyList<string> Defaults, IReadOnlyList<JudgmentCheck> Checks, IReadOnlyList<JudgmentTodo> Todo);
```

## 5. 예외

| 상황 | 결과 | 근거 |
| --- | --- | --- |
| `definition`·`metrics`가 null | `ArgumentNullException` | [T11:P:args null] |
| `metrics.Case`가 `definition.Name`과 다름 | `ArgumentException` | [T11:P:args name-mismatch] |
| 시간 값이 음수 | `ArgumentOutOfRangeException` | [T11:P:args negative-time] |
| 사람 입력 키가 검토 항목의 키가 아니거나 중복 | `ArgumentException` | 4.9 |
| 대조 결과가 정의에서 나온 것이 아님(아래 표) | `ArgumentException` | 12.2 |
| 대조 오류(`CompareResult.Metrics`가 null) | 판정하지 않는다. 부르는 쪽이 대조 오류를 보인다(종료 코드 2) | 2절 |
| `inputs` 없음 / `compare` 빔 | 미검증. `checks` = `[]`. 다음 할 일 4.7 | 4.3 |
| `thresholds` 일부만 적음 | 빈 키만 기본값. 그 키만 `defaults`·표시 | 4.2, 4.6 |
| 시간 합이 decimal 범위를 넘음 | `TIME` = `not_met` | [T11:P:time overflow] |
| 시간을 받았는데 `time_must_drop: false` | 시간을 보지 않는다(`off`) | 4.4 |
| 유형 열 값에 큰따옴표·이모지·제어 문자 | 문장에는 `CompareRules.Show` 표시, JSON은 3.3 이스케이프 | [T11:P:escape] |
| 유형 열 값이 빈 칸 | `(빈 칸)`으로 보인다. 빈 칸끼리 같은 값 | 6.3 |
| 불일치는 있는데 추가 행만 다른 유형 | 추가 행은 보지 않는다 | 4.8 규칙 3 |

| 정의 | 대조 결과 | Judge |
| --- | --- | --- |
| `inputs` 없음 | 미검증 `no_inputs` | 판정 |
| `inputs` 없음 | 미검증 `no_compare_fields` | 거절 |
| `inputs` 없음 | 대조함 | 거절 |
| `compare` 빔 | 미검증 `no_compare_fields` | 판정 |
| `compare` 빔 | 미검증 `no_inputs` | 거절 |
| `compare` 빔 | 대조함 | 거절 |
| `inputs`·`compare` 있음 | 대조함(같은 정의) | 판정 |
| `inputs`·`compare` 있음 | 미검증 `no_inputs` | 거절 |
| `inputs`·`compare` 있음 | 대조함, 키 열이 다름 | 거절 |
| `inputs`·`compare` 있음 | 대조함, `compare` 필드가 다름 | 거절 |
| `inputs.group` 있음 | 대조함(같은 정의) | 판정 |
| `inputs.group` 있음 | 대조함, 기준 행 머리 행에 그 열이 없음 | 거절 |
| (이름) | `metrics.Case`가 `definition.Name`과 다름 | 거절 |

## 6. 예시

### 6.1 문턱 경계

| 기준 행 | 일치 | 누락 | 표시 정확도(%) | accuracy_min | ACCURACY | 표시 누락률(%) | missing_max | MISSING |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 30 | 27 | 1 | 90.0 | 85 | `met` | 3.3 | 5 | `met` |
| 30 | 27 | 1 | 90.0 | 90 | `met` | 3.3 | 3.3 | `not_met` |
| 30 | 27 | 1 | 90.0 | 90.1 | `not_met` | 3.3 | 3.4 | `met` |
| 2000 | 1799 | 0 | 90.0 | 90 | `not_met` | 0.0 | 0 | `met` |
| 2000 | 1800 | 0 | 90.0 | 90 | `met` | 0.0 | 0 | `met` |
| 20 | 19 | 1 | 95.0 | 95 | `met` | 5.0 | 5 | `met` |
| 3 | 2 | 0 | 66.7 | 66.7 | `not_met` | 0.0 | 0 | `met` |
| 3 | 2 | 0 | 66.7 | 66.6 | `met` | 0.0 | 0 | `met` |
| 1 | 1 | 0 | 100.0 | 100 | `met` | 0.0 | 0 | `met` |
| 20 | 16 | 1 | 80.0 | 95 | `not_met` | 5.0 | 2 | `not_met` |
| 3000 | 2999 | 1 | 100.0 | 100 | `not_met` | 0.0 | 0 | `not_met` |
| 30 | 30 | 0 | 100.0 | 100 | `met` | 0.0 | 0 | `met` |

### 6.2 시간

| time_must_drop | 적용 전 | 처리 | 검토 | TIME |
| --- | --- | --- | --- | --- |
| `true` | 10 | 6 | 3 | `met` |
| `true` | 10 | 7 | 3 | `not_met` |
| `true` | 10 | 7.5 | 2.4999 | `met` |
| `true` | 0 | 0 | 0 | `not_met` |
| `true` | 5 | 0 | 0 | `met` |
| `true` | 1.5 | 1 | 0.5 | `not_met` |
| `true` | 12.25 | 10 | 2.24 | `met` |
| `true` | 없음 | 없음 | 없음 | `not_measured` |
| `false` | 10 | 20 | 20 | `off` |
| `false` | 없음 | 없음 | 없음 | `off` |

### 6.3 몰림

| 기준 행 유형 | 불일치 행 유형 | 추가 행 유형 | inputs.group | FOCUS |
| --- | --- | --- | --- | --- |
| `a×5`, `b×5` | (없음) | (없음) | `grp` | `few` |
| `a×5`, `b×5` | `a` | (없음) | `grp` | `few` |
| `a×5`, `b×5` | `a`, `a` | (없음) | 없음 | `off` |
| `a×5`, `b×5` | `a`, `a` | (없음) | `grp` | `found` |
| `a×10` | `a`, `a` | (없음) | `grp` | `one_value` |
| `a×10` | `a`, `a` | (없음) | 없음 | `off` |
| `a×5`, `b×5` | `a`, `b` | (없음) | `grp` | `spread` |
| `a×5`, `b×5` | `a×5` | (없음) | `grp` | `found` |
| `a×9`, `b×1` | `a×9` | (없음) | `grp` | `found` |
| `(빈 칸)×5`, `b×5` | `(빈 칸)`, `(빈 칸)` | (없음) | `grp` | `found` |
| `a×5`, `A×5` | `a`, `A` | (없음) | `grp` | `spread` |
| `a×5`, `b×5` | `a`, `a` | `b`, `b`, `b` | `grp` | `found` |
| `a×5`, `b×5` | `a`, `b` | `a`, `a`, `a` | `grp` | `spread` |
| `(빈 칸)×39`, `x×1` | `(빈 칸)`, `(빈 칸)` | (없음) | 없음 | `off` |
| `(빈 칸)×39`, `x×1` | `(빈 칸)`, `(빈 칸)` | (없음) | `grp` | `found` |

| 열 값(행 1~6) | inputs.group | 불일치 행 | FOCUS | 걸린 값 |
| --- | --- | --- | --- | --- |
| `p=aaabbb` | `p` | `1,2` | `found` | `p = a` |
| `p=aaabbb` | `p` | `1,4` | `spread` | — |
| `p=aaaaaa` | `p` | `1,2` | `one_value` | — |
| `q=aaabbb`, `p=ababab` | `p` | `1,2` | `spread` | — |
| `q=aaabbb`, `p=ababab` | `q` | `1,2` | `found` | `q = a` |
| `q=aaabbb`, `p=ababab` | 없음 | `1,2` | `off` | — |
| `p=aaaaaa`, `q=bbbbbb` | `q` | `1,2` | `one_value` | — |
| `b=aabbcc`, `B=xxyyzz` | `b` | `1,2` | `found` | `b = a` |
| `b=aabbcc`, `B=xxyyzz` | `B` | `1,2` | `found` | `B = x` |
| `p=__abab` | `p` | `1,2` | `found` | `p = (빈 칸)` |
| `p=aaabbb` | `q` | `1,2` | 대조 오류 `column` | — |
| `p=aaabbb`, `p=aaabbb` | `p` | `1,2` | 대조 오류 `duplicate-column` | — |
| `p=aaabbb` | `id` | `1,2` | 파서 오류 `conflict` | — |
| `p=aaabbb` | `v` | `1,2` | 파서 오류 `conflict` | — |

### 6.4 설명 안 되는 검토 항목

| 불일치 | 추가 행 | 중복 행 | 사람 입력 | UNEXPLAINED |
| --- | --- | --- | --- | --- |
| 0 | 0 | 0 | 없음 | `none` |
| 0 | 0 | 0 | `[]` | `none` |
| 3 | 0 | 0 | 없음 | `not_reviewed` |
| 3 | 0 | 0 | `[]` | `explained` |
| 3 | 0 | 0 | 불일치 1개 | `found` |
| 3 | 0 | 0 | 불일치 3개 | `found` |
| 0 | 2 | 0 | 없음 | `not_reviewed` |
| 0 | 2 | 0 | `[]` | `explained` |
| 0 | 2 | 0 | 추가 행 1개 | `found` |
| 1 | 0 | 1 | 없음 | `not_reviewed` |
| 1 | 0 | 1 | 중복 행 1개 | `found` |
| 2 | 1 | 1 | 없음 | `not_reviewed` |
| 2 | 1 | 1 | 불일치 1개, 추가 행 1개, 중복 행 1개 | `found` |
| 0 | 0 | 0 | 일치 1개 | 거절 |
| 3 | 0 | 0 | 불일치 1개 두 번 | 거절 |

`compare-duplicate` judgment.json (schema_version 2, SHA-256 `c7a3d29b9827af25ab1369cf886e06fe9099898aa67d69b5d74c6f507e84be3b`):

```json
{
  "schema_version": 2,
  "case": "compare-duplicate",
  "verdict": "conditional",
  "rule": "UNEXPLAINED",
  "reason": "정확도·누락률은 문턱을 충족하나 검토할 항목 5건(불일치 2건, 추가 행 1건, 중복 행 2건)을 사람이 아직 보지 않았으므로 조건부.",
  "thresholds": {
    "accuracy_min": 50,
    "missing_max": 50,
    "time_must_drop": false
  },
  "defaults": [],
  "checks": [
    {
      "rule": "ACCURACY",
      "result": "met",
      "reason": "정확도 50.0%(2/4)가 문턱 50% 이상이다."
    },
    {
      "rule": "MISSING",
      "result": "met",
      "reason": "누락률 25.0%(1/4)가 문턱 50% 이하다."
    },
    {
      "rule": "UNEXPLAINED",
      "result": "not_reviewed",
      "reason": "검토할 항목 5건(불일치 2건, 추가 행 1건, 중복 행 2건)이 설명되는지 사람이 아직 보지 않았다."
    },
    {
      "rule": "TIME",
      "result": "off",
      "reason": "시간 조건을 쓰지 않는다(time_must_drop: false)."
    },
    {
      "rule": "FOCUS",
      "result": "spread",
      "reason": "불일치 2건이 kind 열의 한 값에 몰리지 않았다."
    }
  ],
  "todo": [
    {
      "key": "review_items",
      "text": "검토할 항목 5건(불일치 2건, 추가 행 1건, 중복 행 2건)을 보고 설명이 되는지 적는다."
    }
  ]
}
```

### 6.5 우선순위

| ACCURACY | MISSING | UNEXPLAINED | TIME | FOCUS | 판정 | 근거 키 | 다음 할 일 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `met` | `met` | `none` | `off` | `few` | `pass` | `PASS` | — |
| `met` | `met` | `none` | `met` | `few` | `pass` | `PASS` | — |
| `met` | `met` | `none` | `not_measured` | `few` | `conditional` | `TIME` | `measure_time` |
| `met` | `met` | `none` | `not_met` | `few` | `conditional` | `TIME` | — |
| `met` | `met` | `not_reviewed` | `off` | `few` | `conditional` | `UNEXPLAINED` | `review_items` |
| `met` | `met` | `not_reviewed` | `off` | `spread` | `conditional` | `UNEXPLAINED` | `review_items` |
| `met` | `met` | `not_reviewed` | `off` | `off` | `conditional` | `UNEXPLAINED` | `review_items`, `add_group` |
| `met` | `met` | `not_reviewed` | `off` | `one_value` | `conditional` | `UNEXPLAINED` | `review_items` |
| `met` | `met` | `not_reviewed` | `off` | `found` | `conditional` | `FOCUS` | `review_items` |
| `met` | `met` | `not_reviewed` | `not_measured` | `found` | `conditional` | `TIME` | `measure_time`, `review_items` |
| `met` | `met` | `explained` | `met` | `found` | `conditional` | `FOCUS` | — |
| `met` | `met` | `explained` | `met` | `spread` | `pass` | `PASS` | — |
| `met` | `met` | `explained` | `off` | `off` | `pass` | `PASS` | `add_group` |
| `met` | `met` | `found` | `off` | `few` | `hold` | `UNEXPLAINED` | — |
| `met` | `met` | `found` | `not_met` | `found` | `hold` | `UNEXPLAINED` | — |
| `met` | `not_met` | `not_reviewed` | `off` | `spread` | `hold` | `MISSING` | `review_items` |
| `met` | `not_met` | `found` | `not_measured` | `found` | `hold` | `MISSING` | `measure_time` |
| `not_met` | `met` | `not_reviewed` | `off` | `found` | `hold` | `ACCURACY` | `review_items` |
| `not_met` | `not_met` | `found` | `not_met` | `found` | `hold` | `ACCURACY` | — |

### 6.6 탐침 재현

| 탐침 | 장면 | 사람 입력 | T-011 | T-012 |
| --- | --- | --- | --- | --- |
| `P1` | 기준 20행, 결과 = 기준 20행 그대로 + 지어낸 키 30행 | 없음 | `pass` `PASS` | `conditional` `UNEXPLAINED` |
| `P1` | 같음 | `[]` | `pass` `PASS` | `pass` `PASS` |
| `P1` | 같음 | 추가 행 키 전부 | 거절 | `hold` `UNEXPLAINED` |
| `P2` | 기준 40행(열 `memo`: 39행 빈 칸, 1행 `확인`), 빈 칸 행 2개 불일치, `inputs.group` 없음 | 없음 | `conditional` `FOCUS` | `conditional` `UNEXPLAINED` |
| `P2` | 같음 | `[]` | `conditional` `FOCUS` | `pass` `PASS` |
| `P2g` | `P2`에 `inputs.group: memo` | 없음 | 거절 | `conditional` `FOCUS` |
| `P3` | 기준 20행, 결과 = 기준 20행 + 추가 1행(`x000`) | `x000` | 거절 | `hold` `UNEXPLAINED` |
| `P4` | 기준 20행, 결과 = 기준 20행 + `r0003` 한 행 더 | 없음 | 거절 | `conditional` `UNEXPLAINED` |
| `P4` | 같음 | `r0003` | 거절 | `hold` `UNEXPLAINED` |

## 7. 예제 4종 `expected/judgment.json`

### 7.1 요약

| 예제 | 더하는 줄 | 지금 SHA-256 | 더한 뒤 SHA-256 | 바이트 |
| --- | --- | --- | --- | --- |
| `table-compare` | `  group: signal_type` | `451a22acd60c6b650aaf86a424acc72fe1884838592b9ca6819dd2491e764ce2` | `e99d669ab16b6eada60e31c794efaa8cd5baa23db34b2500a68b731914f8e5b1` | 1,640 → 1,661 |
| `signoff-final` | `  group: record_kind` | `a12835eae7b913d9b0ea855fa01fb47086fd97411ae50bed48f5d01beadf0263` | `e11e7b68274fa2688027d671ab240be59cbe9ff874be8ffb5214fb8165dfe348` | 2,595 → 2,616 |

| 예제 | inputs.group | 판정 | 근거 키 | 확인 다섯 | 다음 할 일 | 검토 항목 |
| --- | --- | --- | --- | --- | --- | --- |
| `table-compare` | `signal_type` | `conditional` | `FOCUS` | `met`, `met`, `not_reviewed`, `off`, `found` | `review_items` | 불일치 3건, 추가 행 1건 |
| `form-fill` | 없음 | `pass` | `PASS` | `met`, `met`, `none`, `off`, `few` | — | — |
| `mixed-exceptions` | 없음 | `not_verified` | `NOT_VERIFIED` | — | `add_data`, `add_compare`, `measure_time`, `confirm_thresholds` | — |
| `signoff-final` | `record_kind` | `hold` | `ACCURACY` | `not_met`, `not_met`, `not_reviewed`, `off`, `spread` | `review_items` | 불일치 4건, 추가 행 1건 |

| 파일 | 지금 SHA-256 | 새 SHA-256 | 바뀐 줄 |
| --- | --- | --- | --- |
| `examples/table-compare/expected/judgment.json` | `1c32a9c92311e43d19a01633a171240b3b4fa215adcafe7f4d470dc45b4f5c57` | `b4032a207bb7459e537cb2d77b975d535190719d7adcc3fe415cbfdce7f54865` | `schema_version`, `UNEXPLAINED` 문장, `todo` 키·문구 |
| `examples/form-fill/expected/judgment.json` | `08cb0cac5df480e2611d2d92dbfca31cc2ebd6f3fcb9430d98d35343b74a0186` | `06fbce369498d930718c692f388c542a5c6c6106bbc183237e5cf2711feff5d3` | `schema_version`, `UNEXPLAINED` 문장 |
| `examples/mixed-exceptions/expected/judgment.json` | `85e5da47de436644228ca4f1d75ac086891d1739a34c93f441bee71afdc9df11` | `12aeacf95061c20c952d62ab9f2e688e52e029bf734ce3bee962406dd752371f` | `schema_version`만 |
| `examples/signoff-final/expected/judgment.json` | `84da5aa2d9aaa6ead84c767235495d96189636987bb78ac24b62ddeda607871b` | `4fab1989397ff6b3e22e31501965dc93dbef0934083e7c67d7dc1994fed39252` | `schema_version`, `UNEXPLAINED`·`FOCUS` 문장, `todo` 키·문구 |

### 7.2 table-compare

`examples/table-compare/expected/judgment.json` (SHA-256 `b4032a207bb7459e537cb2d77b975d535190719d7adcc3fe415cbfdce7f54865`):

```json
{
  "schema_version": 2,
  "case": "table-compare",
  "verdict": "conditional",
  "rule": "FOCUS",
  "reason": "정확도·누락률은 문턱을 충족하나 불일치 3건이 모두 signal_type = irregular인 행이므로 조건부.",
  "thresholds": {
    "accuracy_min": 85,
    "missing_max": 5,
    "time_must_drop": false
  },
  "defaults": [],
  "checks": [
    {
      "rule": "ACCURACY",
      "result": "met",
      "reason": "정확도 90.0%(27/30)가 문턱 85% 이상이다."
    },
    {
      "rule": "MISSING",
      "result": "met",
      "reason": "누락률 3.3%(1/30)가 문턱 5% 이하다."
    },
    {
      "rule": "UNEXPLAINED",
      "result": "not_reviewed",
      "reason": "검토할 항목 4건(불일치 3건, 추가 행 1건)이 설명되는지 사람이 아직 보지 않았다."
    },
    {
      "rule": "TIME",
      "result": "off",
      "reason": "시간 조건을 쓰지 않는다(time_must_drop: false)."
    },
    {
      "rule": "FOCUS",
      "result": "found",
      "reason": "불일치 3건이 모두 signal_type = irregular인 행이다(irregular 12행 중 3행, 다른 값 18행 중 0행)."
    }
  ],
  "todo": [
    {
      "key": "review_items",
      "text": "검토할 항목 4건(불일치 3건, 추가 행 1건)을 보고 설명이 되는지 적는다."
    }
  ]
}
```

### 7.3 form-fill

`examples/form-fill/expected/judgment.json` (SHA-256 `06fbce369498d930718c692f388c542a5c6c6106bbc183237e5cf2711feff5d3`):

```json
{
  "schema_version": 2,
  "case": "form-fill",
  "verdict": "pass",
  "rule": "PASS",
  "reason": "정확도·누락률이 문턱을 충족하고 조건부·보류 조건에 걸리지 않으므로 통과.",
  "thresholds": {
    "accuracy_min": 85,
    "missing_max": 5,
    "time_must_drop": false
  },
  "defaults": [],
  "checks": [
    {
      "rule": "ACCURACY",
      "result": "met",
      "reason": "정확도 100.0%(20/20)가 문턱 85% 이상이다."
    },
    {
      "rule": "MISSING",
      "result": "met",
      "reason": "누락률 0.0%(0/20)가 문턱 5% 이하다."
    },
    {
      "rule": "UNEXPLAINED",
      "result": "none",
      "reason": "검토할 항목(불일치·추가 행·중복 행)이 없다."
    },
    {
      "rule": "TIME",
      "result": "off",
      "reason": "시간 조건을 쓰지 않는다(time_must_drop: false)."
    },
    {
      "rule": "FOCUS",
      "result": "few",
      "reason": "불일치가 0건이라 몰림을 보지 않는다."
    }
  ],
  "todo": []
}
```

### 7.4 mixed-exceptions

`examples/mixed-exceptions/expected/judgment.json` (SHA-256 `12aeacf95061c20c952d62ab9f2e688e52e029bf734ce3bee962406dd752371f`):

```json
{
  "schema_version": 2,
  "case": "mixed-exceptions",
  "verdict": "not_verified",
  "rule": "NOT_VERIFIED",
  "reason": "검증 자료(case.yaml inputs)가 없어 미검증.",
  "thresholds": {
    "accuracy_min": 95,
    "missing_max": 2,
    "time_must_drop": true
  },
  "defaults": [
    "accuracy_min",
    "missing_max",
    "time_must_drop"
  ],
  "checks": [],
  "todo": [
    {
      "key": "add_data",
      "text": "기준 자료(사람이 만든 결과)를 reference/에, 결과 자료(AI 적용 후 결과)를 candidate/에 넣고 case.yaml inputs에 파일 패턴과 키 열을 적는다."
    },
    {
      "key": "add_compare",
      "text": "case.yaml compare에 대조할 열과 허용 오차를 적는다."
    },
    {
      "key": "measure_time",
      "text": "건당 처리 시간·검토 시간과 적용 전 시간(분)을 잰다."
    },
    {
      "key": "confirm_thresholds",
      "text": "문턱에 기본값을 썼다(accuracy_min 95, missing_max 2, time_must_drop true). 맞는지 확인하고 case.yaml thresholds에 적는다."
    }
  ]
}
```

### 7.5 signoff-final

`examples/signoff-final/expected/judgment.json` (SHA-256 `4fab1989397ff6b3e22e31501965dc93dbef0934083e7c67d7dc1994fed39252`):

```json
{
  "schema_version": 2,
  "case": "signoff-final",
  "verdict": "hold",
  "rule": "ACCURACY",
  "reason": "정확도 80.0%가 문턱 95%에 못 미치므로 보류.",
  "thresholds": {
    "accuracy_min": 95,
    "missing_max": 2,
    "time_must_drop": false
  },
  "defaults": [],
  "checks": [
    {
      "rule": "ACCURACY",
      "result": "not_met",
      "reason": "정확도 80.0%(16/20)가 문턱 95%에 못 미친다."
    },
    {
      "rule": "MISSING",
      "result": "not_met",
      "reason": "누락률 5.0%(1/20)가 문턱 2%를 넘는다."
    },
    {
      "rule": "UNEXPLAINED",
      "result": "not_reviewed",
      "reason": "검토할 항목 5건(불일치 4건, 추가 행 1건)이 설명되는지 사람이 아직 보지 않았다."
    },
    {
      "rule": "TIME",
      "result": "off",
      "reason": "시간 조건을 쓰지 않는다(time_must_drop: false)."
    },
    {
      "rule": "FOCUS",
      "result": "spread",
      "reason": "불일치 4건이 record_kind 열의 한 값에 몰리지 않았다."
    }
  ],
  "todo": [
    {
      "key": "review_items",
      "text": "검토할 항목 5건(불일치 4건, 추가 행 1건)을 보고 설명이 되는지 적는다."
    }
  ]
}
```
