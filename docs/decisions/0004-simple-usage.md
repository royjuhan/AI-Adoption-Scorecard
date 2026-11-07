# 0004. 사용을 단순하게: 입구 둘, 엔진 하나, 스코어카드 HTML 한 장

- 날짜: 2026-11-07
- 상태: 승인(2026-11-07, juhan "제안대로 승인"). 결정 1~6과 사람 승인 필요 1~17 모두 제안대로. "사람 결정" 절은 juhan이 같은 날 정했다.
- 작성: prototyper
- 확인 대상 커밋: `82f3652`
- 관련: 지시문 1·2·3·4·5·6·7·9·10·13절, `CLAUDE.md`, `.claude/agents/cli-dev.md`, `docs/spec/case-schema.md`(T-003), `docs/spec/classification.md`(T-004), `docs/spec/guard-tests.md`(T-002), `docs/spec/report.md`, `docs/spec/build-request.md`, ADR 0003(결정: 후보 1)

표기: `[읽음]` 커밋된 파일을 읽어 확인. **미확인**: 실험·조회하지 않은 것. **해석**: 지시 원문에 없어 prototyper가 채운 것(사람 확인 필요). 담당 ①~④와 행 모델 A는 `classification.md`의 이름이다. G1·G2·G3와 R1~R4는 `guard-tests.md`의 이름이다.

이 ADR은 파일을 바꾸지 않는다. "사람 결정" 절 밖의 문구는 모두 제안이다. 사람이 승인한 뒤에 반영한다(질문 1).

## 지시 원문 (juhan, 2026-11-07)

같은 날 갱신판이다. 앞 지시를 대체한다. 1~5와 "바뀌지 않는 것"·"순서"는 앞 지시와 같고 6이 더해졌다. 추정 답 표시 모양(1의 `source: inferred`)과 순서는 뒤의 "사람 결정"이 고쳤다.

```text
방향 변경: 사용을 단순하게 한다. ADR 0004로 기록하고 스펙·카드에 반영해.
사용자가 하는 일은 셋뿐이다: 업무를 말하고, 스코어카드를 확인하고, 만들 단계를 고른다.
1. 입구 두 개, 엔진 하나
   - Claude Code 스킬 /scorecard: 사용자가 자유롭게 말하면 AI가 질문으로 좁혀 answers.yaml을 만든다. 답을 사용자에게 보여 확인받은 뒤 엔진을 돌린다. AI가 추정한 답은 source: inferred로 표시한다.
   - scorecard.exe: 오프라인. 정해진 질문에 번호로 답하는 마법사. AI 없음.
   - 엔진(Scorecard.Core)은 지금처럼 AI 없음. 분류·지표·판정은 엔진만 한다. 지시문 2절 원칙 1·6은 "엔진과 exe에 한해"로 고친다.
2. 결과는 HTML 한 장이 기본
   - 맨 위 요약(몇 단계 중 몇 단계가 ②, 절약 추정), 단계 표(①~④ 배지, 근거 한 줄, 기본값 표시), 제안 카드("이 단계 만들까요?").
   - 외부 파일 없는 단일 HTML, 오프라인에서 열림. Excel은 내보내기, PDF는 브라우저 인쇄. QuestPDF는 뺀다.
3. 명령은 넷: start(문답→스코어카드 열기), open, check(자료 넣고 검증), build(의뢰서). 기존 세부 명령은 내부·테스트용으로 남긴다.
4. 확인은 한 화면: 단계마다 "맞음/바꾸기"만 고른다. 사유는 바꿀 때만 한 줄.
5. 제작 게이트 3곳은 쉬운 말로 묻는다: "이렇게 만들까요?", "결과를 넣을까요?", "2주 써 보고 괜찮나요?"
6. 엔진은 사람용 입구(exe, HTML)와 AI용 입구(CLI·JSON)를 같은 수준으로 지원한다.
   - 모든 명령은 대화 없이 파일 입력(--answers 등)만으로 돌 수 있어야 한다. --json을 주면 결과를 JSON으로 낸다. JSON 스키마는 docs/spec에 고정하고 버전 필드를 넣는다.
   - 오류도 JSON으로 낸다(코드·경로·메시지). 종료 코드를 정해 둔다: 성공 0, 입력 오류 2, 내부 오류 1.
   - Claude 스킬은 이 AI용 입구만 쓴다. 스킬 안에 판정 로직을 두지 않는다.
   - HTML과 JSON은 같은 결과 객체에서 만든다. 같은 입력이면 JSON이 바이트 단위로 같아야 한다(결정론 테스트에 추가).
   - MCP 서버는 M4 이후 선택 과제로 카드만 만들어 둔다.
바뀌지 않는 것: T-001~T-005 결과물, examples/·expected/, 사람 게이트 3곳.
순서: ADR 0004 초안 → 내 승인 → T-006(대조 엔진)은 그대로 진행 → M2 카드를 이 흐름으로 다시 쪼개.
```

## 사람 결정 (2026-11-07, juhan)

리드가 전달한 원문을 그대로 인용한다. 결정 1(ADR 0003 → 후보 1)은 ADR 0003 "결정"에 적었다.

```text
[결정 2] ADR 0004 버전 필드
- classification.json과 metrics.json 모두 첫 키를 "schema_version": 1(정수)로 한다.
- metrics.json은 처음부터 버전을 넣는다. metrics.md 3.2·7절의 블록과 SHA-256을 다시 만들고, Python 독립 구현(xcheck)도 같은 바이트를 내는지 다시 확인한다.
- source: inferred는 답 값의 형식을 바꾸지 않는다. 단계마다 선택 항목 inferred: [키 목록]을 두고, case-schema 4.4에 같은 PR에서 추가한다. 예제에는 넣지 않는다.

[순서] 카드끼리 동시에 열지 않는다. 1) ADR 0003·0004 문서만 커밋 → juhan 승인 2) T-007 expected 마이그레이션(classification.json에 schema_version 추가) 3) T-006 스펙은 버전 반영해 계속, 구현과 expected/metrics.json 커밋은 T-007 머지 뒤 4) T-008 human_signoff 질문 추가 5) M2 카드를 ADR 0004 흐름으로 다시 쪼갬.

[expected 변경 규칙] T-007부터: 커밋 메시지 "expected migration:"으로 시작, 그 커밋엔 expected 파일과 스펙 JSON 블록·SHA-256 줄만. expected·스펙 블록 diff는 파일마다 `"schema_version": 1,` 한 줄 추가뿐. expected를 엔진 출력에서 복사하지 않고 그 한 줄을 손(또는 그 한 줄만 넣는 스크립트)으로 넣고 엔진이 맞춘다. 시작 전 main의 통과 수를 적고, 끝난 뒤 그 이상·삭제/Skip 없음을 PR 본문에. reviewer는 git diff --stat과 expected diff 전문을 PR에 붙인다.

[상시 승인 예외] expected/ 변경, 테스트 삭제·완화, ADR 결정, 지시문 2절 원칙 변경은 매번 juhan에게 묻는다.
```

이 결정이 이 ADR에 주는 뜻:

- "바뀌지 않는 것: examples/·expected/"는 juhan이 정한 두 변경만 예외로 둔다. T-007의 `"schema_version": 1,` 한 줄, T-008의 `human_signoff: no`(ADR 0003). 그 밖에는 그대로다.
- 이 ADR의 제안 가운데 지시문 2절 원칙을 고치는 것(1·6, 그리고 이 ADR이 더 제안한 3·7)과 새 expected 파일(`report-outline.json`)은 매번 juhan에게 묻는다. 상시 승인("끝날때까지 추천값으로")으로 넘기지 않는다.
- T-005·T-006 카드의 "상시 승인(2026-11-07)으로 추천값을 확정값으로 쓴다" [읽음 `docs/tasks/T-005-…md`, `T-006-…md`]는 expected에 대해서는 더 쓰지 않는다. T-006의 `expected/metrics.json`도 juhan이 직접 확정한다. 리드가 T-006 카드 문구를 고친다.
- 결정 2의 metrics.json 버전·블록·SHA-256·xcheck는 T-006 prototyper의 일이다. 이 ADR은 `docs/spec/metrics.md`와 `spikes/T-006/`을 건드리지 않는다.
- "case-schema 4.4에 같은 PR에서 추가"의 "같은 PR"이 무엇을 가리키는지는 질문 16.

## 배경

지금 지시문이 정한 사용 흐름 [읽음 `docs/README-instructions.md`]:

| 항목 | 지금 |
| --- | --- |
| 3.1 사용자 흐름 | 9단계. 명령 9개: `new`, `interview [--answers]`, `classify`, `compare`, `measure`, `judge`, `review`, `report`, `propose`. 4단계(자료 투입)는 "폴더에 복사". `--answers`는 `interview`에만 있다 |
| 3.1 사람 검토 | `review`: "분류·판정을 한 줄씩 확인, 사유 입력. 사유 없으면 확정 안 됨" |
| 3.5 스코어카드 | "Excel 시트 = PDF 섹션". 목차 6개 |
| 4절 기술 스택 | Excel = ClosedXML, PDF = QuestPDF("커뮤니티 라이선스 조건 확인 후 사용, 안 되면 Excel만"), 저장 = SQLite. JSON 출력 줄은 없다 |
| 6절 cli-dev | "Excel·PDF 평가서", "report 명령: … Excel 시트를 만들고 같은 내용을 PDF로 낸다" |
| 9절 스킬 | `new-case`, `review-checklist`, `intake-request` 셋. 사용자 입구 스킬은 없다 |
| 10절 M2 | 만드는 것 "interview(--answers)·classify·compare·measure·judge·review·report·propose, Excel·PDF, 번들". G2 "`interview --answers`→`report`→`propose` end-to-end", "샘플 평가서(Excel·PDF)를 README에 게시" |
| 2절 1 | "`src/`에 LLM API, 네트워크 호출, 외부 프로세스 호출을 넣지 않는다" |
| 2절 6 | "사용자의 서술을 프로그램이 '이해'하려 하지 않는다. 정해진 질문으로 답을 받고…" |
| 2절 7 | "판정 로직은 `Core`에만. CLI와 UI는 표시만 한다" |
| 13절 첫 줄 | "사용자 서술을 LLM이나 자체 알고리즘으로 '이해'해서 단계를 스스로 나누지 않는다" |
| 종료 코드·JSON 출력 | 지시문에 없다 |

지금 저장소 상태 [읽음]:

- Cli는 빈 진입점이다. `Program.cs`가 "아직 명령이 없습니다 (M0)"만 찍는다. M2 명령은 하나도 없다. 그래서 흐름을 지금 바꾸는 비용이 작다.
- Core에는 `Cases`(T-003), `Classification`(T-004), `Synthetic`(T-005)이 있다. T-006(대조)은 진행 중이다.
- G1은 `Scorecard.Core.dll`과 `scorecard.dll`을 검사한다. Cli에도 N1~N6이 걸린다. N2·N3는 `System.Diagnostics.Process`·`ProcessStartInfo`를 막는다 [읽음 `guard-tests.md` 4.2, `tests/Scorecard.Tests/Guards/NetworkGuardTests.cs`, `MetadataScanner.cs`].
- G2 R3는 다른 작업 폴더에서 돌려 출력 바이트를 비교한다. 입력의 절대 경로를 출력하는 가짜 대상 `abs-path`는 R3에서만 다르다. T-002 승인 결정 9: "출력에는 입력 폴더 기준 상대 경로만 쓴다" [읽음 `guard-tests.md` 4.5·승인 결과].
- `classification.json`에는 지금 버전 필드가 없다. 최상위 속성은 `case`, `steps`뿐이다 [읽음 `ClassificationJson.cs`, `examples/*/expected/classification.json`]. 바이트 규칙은 `Utf8JsonWriter`, 들여쓰기 2칸, LF, 끝 `\n`, UTF-8(BOM 없음)이다 [읽음 `classification.md` 3.3]. 속성 순서 테스트는 3.2 모양 블록을 읽어 비교한다 [읽음 `ClassificationJsonTests.Property_order_matches_spec_shape_block`].
- 파서 오류는 `CaseError(Source, Line, Column, Code, Path, Message)`다. `Source`는 파일 이름이고 절대 경로를 쓰지 않는다. `Path`는 필드 경로(예: `steps[2].answers.batch_size`)다. 입출력 예외는 메시지 대신 예외 타입 이름만 쓴다 [읽음 `case-schema.md` 4.9·4.10].
- `answers.yaml`은 `case.yaml` 스키마의 부분이다(T-004 승인 결정 13). 표에 없는 키는 `unknown-field` 오류다. 단계 항목의 키는 `name`, `answers` 둘뿐이다 [읽음 `case-schema.md` 4.3·4.4].
- `report.md`, `build-request.md`는 미작성이다 [읽음]. `metrics.md`는 T-006에서 작성 중이다(이 ADR은 읽지 않았다).
- `guard-tests.md` 미확인 9: "Excel·PDF 바이트 결정론"(ClosedXML·QuestPDF 미실험) [읽음].
- `README.md`·`README.ko.md` "하는 일" 4: "스코어카드(Excel + PDF)" [읽음].

## 결정 (제안)

### 1. 입구 둘, 엔진 하나

| | 스킬 `/scorecard` | `scorecard.exe` | 엔진 `Scorecard.Core` |
| --- | --- | --- | --- |
| 위치 | `.claude/skills/scorecard/SKILL.md` (`src/` 밖) | `src/Scorecard.Cli` | `src/Scorecard.Core` |
| AI | 쓴다(Claude Code 대화) | 없음. 오프라인 | 없음 |
| 쓰는 입구(결정 6) | AI용 입구만(`--json` + 파일 입력) | 사람용 입구(번호 마법사, HTML)와 AI용 입구 둘 다 낸다 | — |
| 하는 일 | 자유 서술을 질문으로 좁혀 `answers.yaml` 초안을 만든다. 답을 보여 확인받고 `scorecard start --answers … --json`을 부른다 | 정해진 질문에 번호로 답하는 마법사. Enter면 기본값 | 분류·지표·판정. 근거 문장. 결과 객체 |
| 하지 않는 일 | ①~④를 스스로 정하거나 미리 말하지 않는다. 지표·판정을 계산하지 않는다 | 판정·분류 로직(Core만) | — |

- 두 입구는 같은 `answers.yaml`을 만든다. 엔진은 어느 입구에서 왔는지 모른다.
- AI가 추정한 답은 단계의 `inferred` 목록에 키를 적어 표시한다(결정 2, 아래 "추정 답 표시"). exe는 추정 답을 만들지 않는다.
- 모르는 답은 두 입구 모두 키를 비운다. 그래서 기본값 표시("기본값 사용, 확인 필요")가 지금처럼 남는다. 스킬은 기본값을 값으로 써 넣지 않는다(**해석**).
- 예외: `human_signoff`(ADR 0003)는 단계마다 묻지 않고 "사람이 최종 확인·서명하는 단계가 어디인가"를 한 번 묻는다. 고른 단계는 `yes`, 나머지는 `no`로 두 입구 모두 명시한다(결정 1).

### 2. 결과는 HTML 한 장

- 사람에게 주는 기본 결과는 외부 파일 없는 단일 HTML이다. 오프라인에서 연다.
- 맨 위 요약(n단계 중 m단계가 ②, 절약 추정) → 단계 표(①~④ 배지, 근거 한 줄, 기본값 표시) → 제안 카드("이 단계 만들까요?").
- Excel은 내보내기다. PDF는 브라우저 인쇄다. QuestPDF를 기술 스택에서 뺀다.
- 스크립트를 넣지 않는다(**해석**, 질문 8). 그림·글꼴·스타일은 모두 HTML 안에 둔다.
- HTML과 JSON은 같은 결과 객체에서 만든다(결정 6). 분류·수치·추천은 Core가 결과 객체에 넣는다. HTML은 Cli가 그 객체로만 쓴다(표시만). 패키지를 더하지 않는다. 이스케이프는 `System.Text.Encodings.Web`의 `HtmlEncoder`를 쓴다. 이 어셈블리는 이미 Core가 참조하는 프레임워크 어셈블리다 [읽음 `classification.md` 4.10].

### 3. 명령 넷

| 사용자가 하는 일 | 명령 | 안에서 하는 일 (지금 3.1의 명령) | 대화 없이 줄 것(결정 6, 제안) |
| --- | --- | --- | --- |
| 업무를 말한다 | `start` | `new` → `interview` → `classify` → 확인 한 화면(`review`) → `report` → 스코어카드 열기 | `--answers answers.yaml`, `--intake intake.md`(선택), `--review review.yaml`(선택) |
| 스코어카드를 본다 | `open` | `report`. `--excel`이면 Excel 내보내기(**해석**) | `--review review.yaml`(선택), `--excel` |
| 자료를 넣고 검증한다 | `check` | 자료 투입(읽기 전용 복사 + 해시) → `compare` → `measure` → `judge` → 확인 한 화면(`review`) → `report` | `--reference <폴더>`, `--candidate <폴더>`, `--times times.yaml`(시간), `--review review.yaml`(선택). 대조 설정은 `case.yaml`의 `inputs`·`compare`·`thresholds` |
| 만들 단계를 고른다 | `build` | `propose` → 예면 `build-request/` 번들, `export-check.md` 확인 | `--step <번호>`(= "예"). 사람 확인은 `export-check.md`에 사람이 적은 것만 본다(질문 15) |

- 지금 명령 9개는 내부·테스트용으로 남긴다. 도움말에는 넷만 보인다(**해석**, 질문 11).
- 파일 이름 `review.yaml`, `times.yaml`, 옵션 이름은 가칭이다. 명령 스펙(M2-1)에서 정한다.

### 4. 확인은 한 화면

- 단계마다 "맞음 / 바꾸기"만 고른다. 바꿀 때만 사유를 한 줄 쓴다.
- 지금 3.1의 "사유 없으면 확정 안 됨"은 "바꾸기에 사유가 없으면 확정 안 됨"으로 좁힌다. 맞음은 사유 없이 확정된다.
- 엔진 분류와 사람 선택을 둘 다 남긴다. 사람이 바꿔도 엔진 결과를 지우지 않는다(**해석**).
- 대화 없이 돌 때는 확인 파일(`--review`)로 받는다. 파일이 없으면 "확인 전"으로 남긴다(오류가 아니다). 어디서 고르는지는 질문 7.

### 5. 제작 게이트 3곳은 쉬운 말로

지시문 2절 4의 사람 게이트 3곳은 그대로다. 제작물(사용자의 단계를 만드는 일)에서 의뢰한 사람에게 묻는 말만 바꾼다. 대응은 **해석**이다(질문 17).

| 2절 4 게이트 | 쉬운 말 | 묻는 곳(제안) |
| --- | --- | --- |
| 스펙 승인 | "이렇게 만들까요?" | 제작 환경(M3 `scorecard-factory-template`)에서 스펙을 보이고 묻는다. M2 `build`는 번들의 `acceptance.md`에 이 질문을 적는다 |
| PR 머지 | "결과를 넣을까요?" | 제작 환경. 만든 도구와 테스트 보고서를 보이고 묻는다 |
| 마일스톤 데모(제작물은 인수) | "2주 써 보고 괜찮나요?" | 받은 쪽. 2주 사용 뒤 `check`로 다시 재고 묻는다(M4 "사후 평가서 v2"와 같은 자리) |

- `export-check.md`의 사람 확인(2절 8, 3.6)은 그대로 둔다. 실제 자료가 없는지 보는 보안 확인이라 제작 게이트와 따로 있다.

### 6. 사람용 입구와 AI용 입구를 같은 수준으로

| | 사람용 입구 | AI용 입구 |
| --- | --- | --- |
| 실행 | `scorecard.exe` 번호 마법사, 확인 한 화면 | 같은 exe에 `--json` + 파일 입력 |
| 결과 | HTML 한 장(+ Excel 내보내기), 터미널 메시지 | 표준 출력에 JSON 문서 하나 |
| 쓰는 쪽 | 사람 | 스킬 `/scorecard`, 테스트, (M4 이후 선택) MCP 서버 |

규칙(제안):

1. 모든 명령은 대화 없이 돈다. `--json`이면 묻지 않는다. 필요한 파일이 없으면 기다리지 않고 입력 오류(2)로 끝낸다.
2. `--json`이 없으면 사람용이다. 번호 마법사는 표준 입력을 읽는다(테스트는 표준 입력으로 번호를 넣는다).
3. `--json`이면 표준 출력에 JSON 문서 하나만 쓴다. 성공·오류 모두 같다. 바이트 규칙은 `classification.md` 3.3과 같다(UTF-8 BOM 없음, LF, 들여쓰기 2칸, 끝 `\n`). 콘솔 인코딩에 기대지 않고 표준 출력 스트림에 UTF-8 바이트를 쓴다. 표준 오류에는 쓰지 않는다(**해석**, 질문 13).
4. 모든 JSON(결과 파일, `--json` 출력, 오류)은 첫 키가 `"schema_version": 1`(정수)이다. 결정 2를 `--json` 출력까지 넓힌 것이다(**해석**, 질문 14).
5. 결과 JSON(추천, 질문 14): 봉투를 두지 않는다. `--json` 출력은 그 명령의 결과 객체 그대로다. 결과가 파일 형식과 같으면 바이트도 같다. 예: `classify --json`의 표준 출력 = 그 케이스의 `classification.json`. 그러면 예제 `expected/`로 AI용 입구도 바로 검사된다. `start`·`open`·`check`의 결과는 스코어카드 결과 객체(새 스키마, `report.md`)다.
6. 오류 JSON(제안). 오류 객체는 `CaseError`의 여섯 값을 그대로 쓴다. `path`는 필드 경로다. 파일 경로가 아니다. 예는 `case-schema.md` 6.4의 오류 한 줄이다 [읽음]:

   ```json
   {
     "schema_version": 1,
     "exit_code": 2,
     "errors": [
       {
         "code": "range",
         "source": "case.yaml",
         "line": 15,
         "column": 19,
         "path": "steps[2].answers.batch_size",
         "message": "범위 밖이다 (1 이상 2147483647 이하): 0"
       }
     ]
   }
   ```

   - 대조·명령 인자·사람 확인 없음 같은 다른 오류도 같은 모양이다. 위치가 없으면 `line`·`column`은 0, 필드가 없으면 `path`는 빈 문자열(4.9와 같음).
   - 내부 오류의 `message`는 예외 타입 이름만 쓴다. 예외 메시지는 절대 경로를 담을 수 있다(4.10과 같음).
7. 종료 코드(제안, 질문 13): 0 성공. 판정이 보류·미검증이어도, `build`에서 아니오·나중에여도 0이다. 2 입력 오류. 인자 오류, 파일 없음, 파싱·검증 오류, 사유 없는 바꾸기, 사람 확인 없는 내보내기. 1 내부 오류. 예상하지 못한 예외.
8. 같은 결과 객체: 결과 객체와 JSON 쓰기는 Core에 둔다(`ClassificationJson`과 같은 방식). HTML 쓰기는 Cli에 두고 결과 객체 하나만 받는다. 그래서 HTML에만 있는 판정·수치가 생기지 않는다.
9. 결정론: 명령마다 `--json` 출력을 G2·G3 대상에 더한다. 테스트 어댑터가 표준 출력을 출력 폴더의 파일로 쓴다. R1~R4(다른 경로, de-DE 포함)에서 바이트가 같아야 한다. 테스트가 Cli를 프로세스로 띄우지 않도록 Cli에 프로세스 안 진입점(인자·표준 입출력을 받는 함수)을 둔다(**해석**).
10. 스킬은 AI용 입구만 쓴다. 스킬 안에 판정 로직을 두지 않는다. 스킬은 표준 출력 JSON과 종료 코드만 읽는다.
11. MCP 서버는 M4 이후 선택 과제다. 카드만 만든다(카드 표 "M4+").

### 경로와 G2 R3

G2 R3는 다른 작업 폴더에서 돌린다. 출력에 절대 경로가 들어가면 R3에서 바이트가 다르다 [읽음 `guard-tests.md` 4.5 `abs-path`]. 그래서 JSON에 이렇게 쓴다(제안):

- 파일 경로는 케이스 폴더 또는 출력 폴더 기준 상대 경로, `/` 구분. 절대 경로·`~`·드라이브 문자를 쓰지 않는다(T-002 승인 결정 9).
- 오류 `source`는 파일 이름이다(`case-schema.md` 4.9). 오류 `path`는 필드 경로라 이 규칙과 상관없다.
- 스코어카드 위치는 상대 경로(예: `scorecard.html`)로만 알린다. 부른 쪽(스킬)은 자기가 준 출력 폴더로 절대 경로를 만든다.
- 입력 SHA-256 목록의 키도 상대 경로다.
- 사람용 터미널 메시지(`--json` 없음)에는 열 파일의 절대 경로를 찍어도 된다(질문 6). 결과 파일이 아니고 G2 대상도 아니다. G2 대상은 `--json` 출력과 결과 파일(HTML 포함)이다.
- 음성 대조: 어댑터가 JSON에 절대 경로를 넣게 바꾸면 G2 R3가 잡아야 한다. T-004의 S6과 같은 방식이다 [읽음 `classification.md` 4.10].

## 추정 답 표시 (`inferred` 목록, 결정 2)

### 모양 (결정)

- 답 값의 형식은 바꾸지 않는다. 단계마다 선택 항목 `inferred: [키 목록]`을 둔다. case-schema 4.4(단계 항목 표)에 더한다. 예제에는 넣지 않는다.

```yaml
steps:
  - name: 주문서 양식 읽기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 20, human_signoff: no}
    inferred: [frequency, batch_size]
```

- 지시 원문의 `source: inferred`는 이 모양으로 바뀌었다. 지시문·스킬 문구도 "단계의 `inferred` 목록"으로 쓴다.

### 규칙 (초안, 스펙 카드에서 확정)

- `inferred`는 선택이다. 없으면 모든 답이 추정이 아니다.
- 원소는 답 키(case-schema 4.4 표의 키, T-008 뒤에는 `human_signoff` 포함)다. 아니면 `enum` 오류. 같은 키가 둘이면 `duplicate` 오류.
- 목록에 적은 키는 `answers`에 값이 있어야 한다. 값이 없거나 null이면 그 답은 기본값이지 추정이 아니다. 오류로 막는다(코드·문구는 스펙 카드).
- 그래서 한 답이 기본값이면서 추정일 수는 없다. 기본값 = 사용자도 AI도 값을 주지 않음. 추정 = AI가 값을 줬고 사용자가 그 값을 따로 말하지 않음.
- 파서 결과: `Answer<T>`에 `IsInferred`를 더한다. 공개 API 변경이다(T-003 승인 결정 14를 고친다). 기존 2인자 생성(`new Answer<string>("fixed_form", true)` [읽음 `CaseApiShapeTests.cs`])은 선택 인자로 두면 그대로 컴파일될 것으로 본다(**미확인**).
- 목록 순서는 쓴 순서와 상관없이 case-schema 4.4 표 순서로 읽는다(**해석**). 출력 순서가 입력 순서에 기대지 않게 한다.

### 엔진이 추정 답을 다루는 법 (초안)

- 분류(담당·근거 키·행)는 바뀌지 않는다. 엔진은 값만 본다. 추정 답도 값이 있으면 그 값으로 분류한다.
- 근거 문장: 그 행을 정할 때 트리가 읽은 답(`classification.md` 4.6 표) 중 하나라도 `IsInferred`면 문장 끝에 ` (AI 추정, 확인 필요)`를 붙인다. 기본값 표시와 같이 붙으면 기본값 표시 뒤에 둔다. 4.6 표와 규칙 모양은 그대로 쓴다.
- 단계마다 추정 답 키 목록(case-schema 4.4 순서)을 메모리 결과 `StepClassification`에 둔다. `classification.json` 파일에는 쓰지 않는다(추천). `Used`도 메모리에만 있는 것과 같은 방식이다 [읽음 `classification.md` 3.2]. 스코어카드 결과 객체에는 `inferred` 목록을 둔다(새 스키마라 지금 파일과 상관없다).
- 대안: 파일에도 목록이 비어 있지 않을 때만 `"inferred"` 속성을 쓴다. 이 경우도 예제 바이트는 그대로다(예제에 `inferred`가 없다).
- 확인 항목(4.7)·AI 과잉 후보(4.8)는 그대로다.
- 스코어카드 단계 표는 답마다 기본값·추정 표시를 같이 보인다.
- → 질문 2.

### 예제·expected와 맞추기

- 예제 3종의 `answers.yaml`·`case.yaml`에는 `inferred`가 없고, 결정 2가 "예제에는 넣지 않는다"다. 그래서 모든 답의 `IsInferred`가 false이고 문장 표시가 붙지 않는다. `classification.json` 파일에 목록을 쓰지 않으면(추천) 바이트가 그대로다. 기존 골든 테스트(`tests/Scorecard.Tests/Classification/ExampleGoldenTests.cs` [읽음 파일 목록])가 지킨다.
- 추정 답 테스트는 `tests/Scorecard.Tests/TestData/`의 새 합성 표본으로 한다(ADR 0002).
- 어긋남(그대로 적음): 지시문 2절 11은 "새 규칙·지표를 넣으려면 `examples/*/expected/`에 기대값을 먼저 추가한다"이다 [읽음]. 추정 표시는 분류 규칙·지표가 아니라 표시라고 보고 TestData로만 테스트하는 안을 제안한다. 2절 원칙에 걸리므로 juhan이 답한다. → 질문 2.

### 추정 표시는 언제 지우나

- 제안: 사용자가 그 답을 직접 말하거나, 그 답 하나를 보고 "맞다"고 하면 스킬이 `inferred`에서 지운다. 한꺼번에 넘긴 답("다 맞아")은 남긴다. → 질문 3.
- 스킬은 모르는 답에 기본값을 값으로 써 넣지 않는다. 키를 비운다. 그래야 기본값 표시가 사라지지 않는다(2절 6). `human_signoff`만 예외로 늘 명시한다(ADR 0003 결정).
- 단계 나누기 자체(단계 이름)에는 추정 표시를 두지 않는다. 단계 목록은 사용자가 통째로 확인한다(**해석**).

## 스킬 `/scorecard`와 원칙 1의 경계

- 스킬은 `.claude/skills/scorecard/SKILL.md`다. 기존 스킬 셋과 같은 자리·모양(frontmatter `name`, `description`)이다 [읽음 `.claude/skills/*/SKILL.md`]. 컴파일되지 않고 exe에 들어가지 않는다.
- G1은 `Scorecard.Core.dll`, `scorecard.dll`만 본다 [읽음 `guard-tests.md` 2절·4.2]. 스킬은 G1 대상이 아니고, 대상일 필요도 없다. G1·G2·G3는 그대로다. `.claude/rules/core-no-network.md`(paths `src/Scorecard.Core/**`)도 그대로다 [읽음].
- 스킬은 엔진을 AI용 입구(`scorecard <명령> --json` + 파일 입력)로만 부른다. 표준 출력 JSON과 종료 코드만 읽는다. 프로세스를 띄우는 것은 Claude Code이고 `src/`가 아니다.
- 스킬이 지킬 것(SKILL.md에 적는다, 제안):
  1. 서술은 `intake.md`에 그대로 저장한다. 고쳐 쓰지 않는다.
  2. 질문은 case-schema 4.4의 키와 3.1의 단계 질문만 쓴다. 선택지 밖의 값을 만들지 않는다.
  3. 추정한 답은 그 단계의 `inferred` 목록에 적는다. 모르면 키를 비운다.
  4. 서명 단계는 한 번만 묻고 모든 단계에 `human_signoff`를 명시한다(ADR 0003).
  5. `answers.yaml` 전체를 사용자에게 보이고 확인받은 뒤에만 `scorecard start --answers … --json`을 부른다.
  6. ①~④, 정확도, 판정을 스스로 말하지 않는다. 엔진 JSON의 값을 그대로 전한다. 판정 로직을 두지 않는다.
  7. 종료 코드 2면 오류 JSON의 `path`·`message`를 사용자에게 보이고 그 답만 다시 묻는다. 1이면 멈추고 알린다.
  8. 사람 확인을 대신 적지 않는다. `review.yaml`에는 사용자가 대화에서 고른 것만 적는다. `export-check.md`의 사람 확인 줄은 쓰지 않는다(질문 15).
  9. `reference/`·`candidate/`·`cases/`의 자료 파일 내용을 읽지 않는다(질문 4).
- 데이터 경계: 스킬을 쓰면 사용자의 업무 서술과 답이 AI 서비스로 간다. exe는 그렇지 않다. 지시문 1절은 번들이 "보안 경계 밖"으로 간다고 하고, 2절 8·10은 실제 자료를 경계 밖·저장소에 두지 않는다 [읽음]. 스킬이 경계 밖으로 보내는 것을 어디까지로 둘지 사람이 정해야 한다. → 질문 4.
- 스킬 파일을 쓸 에이전트가 없다. CLAUDE.md 폴더 제한에 `.claude/skills/`가 없다 [읽음]. → 질문 5.

## 바뀌는 조항과 고칠 문구 (초안)

지시문은 사람이 쓴 문서다. 지난번에는 사람 결정 뒤 리드가 고치고 조항 끝에 "(날짜 사람 결정: …)"을 붙였다 [읽음 커밋 `7735f8a`]. 같은 방식을 제안한다(질문 1). 2절 원칙 변경은 상시 승인 예외라 조항마다 juhan이 답한다.

### 지시문 2절 1 (juhan 지시)

- 지금 [읽음]: "**실행 시 AI 호출 없음.** `src/`에 LLM API, 네트워크 호출(`HttpClient`, `Socket`, `WebClient`), 외부 프로세스 호출을 넣지 않는다. 테스트가 이를 검사한다."
- 제안:

> 1. **실행 시 AI 호출 없음 (엔진과 exe에 한해).** `src/`(엔진 `Scorecard.Core`와 `scorecard.exe`)에 LLM API, 네트워크 호출(`HttpClient`, `Socket`, `WebClient`), 외부 프로세스 호출을 넣지 않는다. 테스트가 이를 검사한다. Claude Code 스킬 `/scorecard`는 `src/` 밖의 입구라 AI를 쓴다. 스킬은 `answers.yaml`을 만들고 AI용 입구(`--json`)로 `scorecard` 명령을 부를 뿐, 분류·지표·판정을 하지 않는다(2026-11-07 사람 결정: ADR 0004).

### 지시문 2절 6 (juhan 지시)

- 지금 [읽음]: "**자유 서술은 저장만, 구조화는 질문으로.** 사용자의 서술을 프로그램이 "이해"하려 하지 않는다. 정해진 질문으로 답을 받고, 모르면 기본값을 넣고 "기본값 사용, 확인 필요"로 표시한다."
- 제안:

> 6. **자유 서술은 저장만, 구조화는 질문으로 (엔진과 exe에 한해).** 엔진과 exe는 사용자의 서술을 "이해"하려 하지 않는다. 정해진 질문으로 답을 받고, 모르면 기본값을 넣고 "기본값 사용, 확인 필요"로 표시한다. 스킬 `/scorecard`는 서술을 질문으로 좁혀 `answers.yaml` 초안을 만든다. AI가 추정한 답은 단계의 `inferred` 목록에 적고(답 값의 형식은 그대로), 답을 사용자에게 보여 확인받은 뒤에만 엔진을 돌린다. 모르는 답은 비워 두어 기본값 표시가 남게 한다(2026-11-07 사람 결정: ADR 0004).

### 지시문 2절 3 (이 ADR의 추가 제안, juhan 답 필요)

- 끝에 더한다: "`--json` 출력과 결과 파일은 같은 입력·같은 버전이면 바이트까지 같다. 경로는 상대 경로만 쓴다."

### 지시문 2절 7 (이 ADR의 추가 제안, juhan 답 필요)

- 지금 [읽음]: "**판정 로직은 `Core`에만.** CLI와 UI는 표시만 한다."
- 제안: "**판정 로직은 `Core`에만.** CLI·HTML·스킬은 표시만 한다. HTML과 `--json` 출력은 Core의 같은 결과 객체에서 만든다."

### 지시문 13절 첫 줄

- 지금 [읽음]: "사용자 서술을 LLM이나 자체 알고리즘으로 "이해"해서 단계를 스스로 나누지 않는다."
- 2절 6을 고치면 이 줄과 어긋난다. 스킬이 질문으로 좁혀 단계를 만든다.
- 제안: "엔진과 exe는 사용자 서술을 LLM이나 자체 알고리즘으로 "이해"해서 단계를 스스로 나누지 않는다. 스킬 `/scorecard`가 만든 단계와 답은 사용자가 확인한 뒤에만 엔진에 들어간다."

### 지시문 1절 (작은 수정)

- 지금 [읽음]: "평가서는 "이 단계, 제작할까요?"로 끝나고, 예라고 하면 **제작 의뢰서 번들**을 만든다."
- 제안: "스코어카드(HTML 한 장)는 "이 단계 만들까요?" 제안 카드로 끝나고, 예라고 하면 **제작 의뢰서 번들**을 만든다."

### 지시문 3.1 (표 전체 교체)

제안:

> ### 3.1 사용자 흐름 (명령 넷)
>
> 사용자가 하는 일은 셋이다: 업무를 말하고, 스코어카드를 확인하고, 만들 단계를 고른다. 입구는 둘(스킬 `/scorecard`, `scorecard.exe`), 엔진은 하나(`Scorecard.Core`)다.
>
> | 사용자가 하는 일 | 명령 | 하는 일 |
> | --- | --- | --- |
> | 업무를 말한다 | `scorecard start <case>` | 자유 서술을 `intake.md`에 그대로 저장. "입력은 무엇, 거기서 무엇을, 결과는 어디로"를 묻고 단계를 만든 뒤, 단계마다 질문 9개를 번호로 고른다. 모르면 Enter → 기본값. 끝에 "사람이 최종 확인·서명하는 단계가 어디인가"를 한 번 묻는다. 분류 → 확인 한 화면 → 스코어카드를 쓰고 연다 |
> | 스코어카드를 본다 | `scorecard open <case>` | 저장된 답·확인·검증 결과로 스코어카드를 다시 쓰고 연다. `--excel`이면 같은 내용을 Excel로 내보낸다 |
> | 자료를 넣고 검증한다 | `scorecard check <case>` | `reference/`(사람이 만든 결과), `candidate/`(AI 적용 후 결과)를 읽기 전용으로 복사하고 해시를 남긴다. 대조, 건당 처리·검토 시간 입력(스톱워치 또는 수기), 판정 제안(통과/조건부/보류/미검증) → 확인 한 화면 → 스코어카드 갱신 |
> | 만들 단계를 고른다 | `scorecard build <case>` | ② 단계 중 먼저 만들 하나를 추천 → "이 단계 만들까요?" 예 / 아니오 / 나중에 → 예면 `build-request/` 번들 생성 |
>
> - 확인 한 화면: 단계마다 "맞음 / 바꾸기"만 고른다. 바꿀 때만 사유를 한 줄 쓴다. 사유 없는 바꾸기는 확정되지 않는다.
> - 사람용 입구와 AI용 입구를 같은 수준으로 지원한다. 모든 명령은 대화 없이 파일 입력(`--answers`, `--review` 등)만으로 돈다. `--json`이면 결과와 오류를 JSON 문서 하나로 낸다. 모든 JSON은 첫 키가 `"schema_version": 1`이다. 스키마는 `docs/spec/json-output.md`에 고정한다. 종료 코드: 성공 0, 입력 오류 2, 내부 오류 1.
> - 내부 명령 `new`, `interview`, `classify`, `compare`, `measure`, `judge`, `review`, `report`, `propose`는 테스트·재현용으로 남긴다. 도움말에는 넷만 보인다.

### 지시문 3.5 (스코어카드)

- 지금 [읽음]: 제목 "스코어카드 목차 (Excel 시트 = PDF 섹션)", 목차 1 업무 개요, 2 단계 분해표와 담당 제안, 3 소프트웨어로 대체 가능한 범위·먼저 만들 단계 1개, 4 검증 결과, 5 사람 게이트와 남는 위험, 6 다음 할 일(제작 여부 포함).
- 제안(구역 순서는 질문 8):

> ### 3.5 스코어카드 (HTML 한 장)
>
> 외부 파일 없는 단일 HTML 하나. 오프라인에서 연다. 스크립트·외부 글꼴·외부 그림을 쓰지 않는다. Excel은 내보내기(`open --excel`, 시트 = 아래 구역), PDF는 브라우저 인쇄. HTML과 `--json` 출력은 같은 결과 객체에서 만든다.
>
> 1. 요약 — n단계 중 m단계가 ②, 절약 추정(시간을 안 쟀으면 "시간 측정 필요")
> 2. 단계 표 — ①~④ 배지, 근거 한 줄, 기본값·AI 추정 표시, 확인 결과(맞음 / 바뀜과 사유)
> 3. 제안 카드 — "이 단계 만들까요?" 먼저 만들 ② 단계 1개와 `build` 명령
> 4. 검증 결과 — 정확한가 · 줄었나 · AI가 필요했나 (측정값 또는 "미검증"과 측정 방법)
> 5. 사람 게이트와 남는 위험
> 6. 다음 할 일 — 확보할 기준 자료, 재야 할 시간, 확인할 질문
> 7. 업무 개요와 입력 기록 — 사용자가 말한 그대로 + 구조화된 입력→단계→출력, 입력 SHA-256

### 지시문 3.6 (번들)

- 표는 그대로. `acceptance.md` 행에 한 줄 더한다: "제작 게이트 세 질문("이렇게 만들까요?", "결과를 넣을까요?", "2주 써 보고 괜찮나요?")과 각 질문에 답할 때 볼 것".
- "내보내기 명령은 `export-check.md`에 사람 확인이 없으면 실패한다" → "`build`는 `export-check.md`에 사람 확인이 없으면 내보내지 않고 입력 오류(2)로 끝난다."

### 지시문 4절 (기술 스택)

| 항목 | 지금 [읽음] | 제안 |
| --- | --- | --- |
| 평가서 | (없음) | 단일 HTML. 패키지 없음. Cli가 결과 객체로 쓰고 `HtmlEncoder`로 이스케이프 |
| AI용 출력 | (없음) | JSON. `System.Text.Json`의 `Utf8JsonWriter`(T-004와 같은 바이트 규칙). 첫 키 `schema_version`. 패키지 없음 |
| Excel | ClosedXML | ClosedXML (내보내기만) |
| PDF | QuestPDF (커뮤니티 라이선스 조건 확인 후 사용, 안 되면 Excel만) | 행 삭제. "PDF는 브라우저 인쇄(QuestPDF 쓰지 않음)"를 평가서 행에 적는다 |
| 입구 스킬 | (없음) | Claude Code 스킬 `.claude/skills/scorecard/`. 제품 코드가 아니다. `src/` 밖 |
| 저장 | SQLite | 이 ADR은 건드리지 않는다(미확인 8) |

### 지시문 5절 (저장소 구조)

- `skills/` 줄에 `scorecard/SKILL.md`를 더한다.
- `spec/` 줄에 M2 스펙 파일을 더한다(가칭 `commands.md`, `json-output.md`, `skill-scorecard.md`. 아래 "영향 받는 스펙").
- `expected/` 설명(5절 끝)에 "JSON 파일은 첫 키가 `schema_version`"을 더한다.

### 지시문 6절과 `.claude/agents/cli-dev.md`

| 줄 | 지금 [읽음] | 제안 |
| --- | --- | --- |
| description | "질문 흐름(interview), 명령 구조, Excel·PDF 평가서, 제작 의뢰서 번들 생성" | "명령 넷(start·open·check·build)과 내부 명령, 번호 마법사, 확인 한 화면, AI용 입구(--json·오류 JSON·종료 코드), 단일 HTML 스코어카드와 Excel 내보내기, 제작 의뢰서 번들" |
| 2 | "interview 명령: 질문은 docs/spec/classification.md의 9개 순서대로. Enter면 기본값. 답은 case.yaml에 쓴다." | "start 명령: 질문은 docs/spec/case-schema.md 4.4 순서대로 번호로 고른다. Enter면 기본값이고 키를 비워 둔다. human_signoff만 단계마다 묻지 않고 한 번 묻고 모든 단계에 명시한다. 답은 answers.yaml에 쓴다." (질문 표는 case-schema 4.4에만 있다. T-003 승인 결정 5 [읽음]) |
| 3 | "report 명령: … Excel 시트를 만들고 같은 내용을 PDF로 낸다." | "open 명령: docs/spec/report.md의 구역 순서대로 단일 HTML을 쓴다. Core의 결과 객체만 쓴다. 외부 파일·스크립트를 넣지 않는다. --excel이면 같은 내용을 Excel 시트로." |
| 4 | "propose 명령: ② 단계 중 하나를 추천하고 "예/아니오/나중에"를 받는다. …" | "build 명령: ② 단계 중 하나를 추천하고 "이 단계 만들까요?"에 예/아니오/나중에를 받는다. …"(뒷부분 그대로) |
| 새 6 | — | "모든 명령은 --json과 파일 입력만으로 돈다. --json이면 묻지 않고 표준 출력에 JSON 하나만 쓴다. 오류도 JSON. 종료 코드 0/2/1. 스키마는 docs/spec/json-output.md." |
| 새 7 | — | "브라우저·다른 프로그램을 띄우지 않는다(G1 N2·N3). 스코어카드 파일 경로를 알린다." (질문 6) |

### 지시문 7절과 `CLAUDE.md`

- "src/에 네트워크·LLM·외부 프로세스 호출 금지. 테스트가 검사한다." → "src/(엔진·exe)에 네트워크·LLM·외부 프로세스 호출 금지. 테스트가 검사한다. AI는 src/ 밖의 스킬 /scorecard에서만. 스킬은 --json 입구만 쓴다."
- "자유 서술은 저장만, 구조화는 정해진 질문으로. 기본값은 "기본값 사용, 확인 필요"로 표시." → "엔진·exe는 자유 서술을 저장만, 구조화는 정해진 질문으로. 기본값은 "기본값 사용, 확인 필요"로 표시. 스킬이 추정한 답은 단계의 inferred 목록에 적고, 사용자 확인 뒤에만 엔진에 넣는다."
- 한 줄 더함: "모든 명령은 대화 없이 파일 입력만으로 돈다. --json 출력은 같은 입력이면 바이트까지 같다. 종료 코드 0 성공, 2 입력 오류, 1 내부 오류."
- 한 줄 더함(결정의 기록): "expected/ 변경, 테스트 삭제·완화, ADR 결정, 지시문 2절 원칙 변경은 매번 사람에게 묻는다. expected 변경은 ADR 0004의 expected 변경 규칙을 따른다."
- "동시 진행 작업 카드는 2개까지"는 이번 순서(카드끼리 동시에 열지 않음) 동안 1개로 운영한다. 문구를 바꿀지는 질문 12.

### 지시문 9절 (스킬)

표에 한 행을 더한다:

> | `scorecard` | 사용자가 Claude Code에서 업무를 말할 때(`/scorecard`) | 서술을 `intake.md`에 그대로 저장 → 정해진 질문(단계, case-schema 4.4의 키)으로 좁혀 `answers.yaml` 초안 → 추정한 답은 단계의 `inferred` 목록, 모르면 키를 비움, 서명 단계는 한 번 묻고 모든 단계에 명시 → 답을 사용자에게 보여 확인 → `scorecard start <case> --answers … --json` → 엔진 JSON을 그대로 전하고 확인 한 화면(`--review`) → `build` 안내. AI용 입구(`--json` + 파일)만 쓴다. ①~④를 스스로 말하지 않는다. 판정 로직을 두지 않는다 |

### 지시문 10절

- M2 행 제안:

> | M2 CLI + 스코어카드 | 3주 | 명령 넷 `start`·`open`·`check`·`build`(내부 명령 new·interview(--answers)·classify·compare·measure·judge·review·report·propose), 번호 마법사, 확인 한 화면, AI용 입구(`--json`, 오류 JSON, 종료 코드), 단일 HTML 스코어카드와 Excel 내보내기, 번들, 스킬 `/scorecard` | G2: 예제 3종에서 `start --answers`→`open`→`build`까지 end-to-end 테스트 통과(사람용과 `--json` 둘 다). 명령 넷의 `--json` 출력이 결정론(G2) 테스트를 통과. 샘플 스코어카드(HTML)를 README에 게시. 비공개 사례 1건을 같은 exe로 로컬 실행해 요약만 README "사례"에 추가 |

- 지금 G2 문구 `interview --answers`→`report`→`propose`를 새 명령으로 옮기고, 지시 6의 "결정론 테스트에 추가"를 더했다. `check`는 넣지 않았다(지금 G2에도 `compare`가 없다 [읽음]). `check`는 자기 카드(M2-5)에서 테스트한다.
- 4번째 예제(ADR 0003)가 들어오면 "예제 3종"은 "예제 4종"이 된다. 그 카드에서 고친다.
- 표 아래 한 줄 더함: "MCP 서버는 M4 이후 선택 과제다. 카드만 둔다(ADR 0004)."

### 그 밖에 리드가 같이 고칠 곳

- `README.md`·`README.ko.md` "하는 일" 4: "스코어카드(Excel + PDF)" → "스코어카드(HTML 한 장)". "실행할 때는 AI를 쓰지 않는다"는 엔진·exe에 대해 그대로 맞다.
- `.claude/skills/review-checklist/SKILL.md`에 일곱 줄(제안):
  - [ ] 스킬 `/scorecard`가 `--json` 입구만 쓰고, 분류·지표·판정을 하지 않는가
  - [ ] HTML이 결과 객체만 받아 쓰는가(HTML에만 있는 수치·판정이 없는가)
  - [ ] 스코어카드 HTML에 외부 참조(`http:`, `https:`, `//`, 외부 `src`·`href`)가 없는가
  - [ ] `--json` 출력·결과 파일에 절대 경로가 없고 첫 키가 `schema_version`인가
  - [ ] 추정 답이 단계의 `inferred` 목록에 남는가
  - [ ] expected 변경 커밋이 "expected migration:"으로 시작하고 expected 파일·스펙 블록·SHA-256 줄만 담는가
  - [ ] PR 본문에 `git diff --stat`과 expected diff 전문, 시작 전·후 통과 수가 있는가
- `.claude/skills/new-case/SKILL.md`: `/scorecard`·`start`와 겹친다. 지우지 않고 "사용자 입구는 /scorecard" 한 줄을 더한다(제안).
- `docs/tasks/T-006-compare-engine.md`: "샘플 자료"의 상시 승인 문구를 expected에는 쓰지 않는다고 고치고, 결정 2(metrics.json 첫 키 `schema_version`)와 "구현·expected 커밋은 T-007 머지 뒤"를 메모에 더한다.

## 영향 받는 스펙

| 스펙 | 영향 |
| --- | --- |
| `classification.md` (T-004, 승인됨) | T-007: 3.2 모양 블록·7.2~7.4 블록에 `"schema_version": 1,` 한 줄, SHA-256 줄 3개(expected migration 커밋). 3.3의 "숫자는 근거 문장 안의 `batch_size` 하나뿐이다" 같은 본문은 그 커밋 밖에서 고친다. T-008: `SIGNOFF`(ADR 0003)와 추정 표시 규칙 |
| `case-schema.md` (T-003, 승인됨) | T-008: 4.4 답 표에 `human_signoff` 1행(ADR 0003), 단계 항목 표에 `inferred`(결정 2, 질문 16), `Answer<T>.IsInferred`, `StepAnswers` 10번째 답. 오류 JSON은 4.9 `CaseError`를 그대로 쓴다 |
| `metrics.md` (T-006 작성 중) | 결정 2: 첫 키 `schema_version`, 3.2·7절 블록·SHA-256 다시 만들기, xcheck 다시 확인. T-006 prototyper의 일이다. `check`의 결과 객체 검증 부분은 그 모양을 쓴다 |
| `synthetic-examples.md` (T-005, 승인됨) | T-008: 7.2~7.4 `case.yaml` 블록·SHA-256 줄(ADR 0003 남은 질문 3) |
| `guard-tests.md` (T-002, 승인됨) | G1 그대로. G2·G3 대상에 명령별 `--json` 출력과 HTML을 더한다(M2-1·M2-3). 미확인 9의 "PDF 바이트 결정론"은 QuestPDF를 빼면 없어진다 |
| `report.md` (미작성) | HTML 한 장과 스코어카드 결과 객체. 구역 순서(3.5 제안), 요약 숫자 세는 법(질문 9), 절약 추정 식(질문 10), 먼저 만들 단계 추천 규칙, 생성 일시(질문 8), `expected/report-outline.json` 모양(첫 키 `schema_version`, 새 expected라 juhan 확정). Excel 시트 = HTML 구역 |
| `build-request.md` (미작성) | `build` 명령. 번들 파일은 3.6 그대로. `acceptance.md`에 제작 게이트 세 질문. `export-check.md` 사람 확인 그대로. 사람 확인이 없으면 종료 코드 2 |
| 새 스펙 (가칭) | `commands.md`: 명령 넷과 옵션, 번호 마법사 화면(서명 단계 한 번 포함), 확인 한 화면, 답·확인 파일을 어디에 쓰는지(2절 2: 입력을 덮어쓰지 않는다). `json-output.md`: 명령별 결과 객체, 오류 객체, 종료 코드, 버전·경로 규칙(질문 14). `skill-scorecard.md`: 스킬 본문 블록(질문 5) |

## 카드 순서와 재분할 (결정된 순서에 맞춘 초안)

결정된 순서(위 "사람 결정")를 따른다. 카드끼리 동시에 열지 않는다. 카드 번호 T-007·T-008은 결정에 있다. 그 뒤 번호는 리드가 연다.

| 순서 | 카드 | 목표(한두 줄) |
| --- | --- | --- |
| 1 | (카드 아님) ADR 0003·0004 문서 커밋 | 이 두 문서만 커밋하고 juhan 승인을 받는다 |
| 2 | T-007 expected 마이그레이션 | `classification.json` 첫 키에 `"schema_version": 1`을 넣는다. 예제 expected 3개와 classification.md 3.2 모양 블록·7.2~7.4 블록·SHA-256 줄을 expected 변경 규칙대로 손으로 한 줄씩 고치고(커밋 "expected migration: …"), `ClassificationJson`이 맞춘다 |
| 3 | T-006 (진행 중) | 스펙은 버전 반영해 계속. 구현과 `expected/metrics.json` 커밋은 T-007 머지 뒤 |
| 4 | T-008 `human_signoff` 질문 추가 | ADR 0003 결정. 트리 맨 앞 `SIGNOFF`, `yes` 단계 뒤 게이트 없음. 예제 `answers.yaml`(·`case.yaml`) 모든 단계 `human_signoff: no`, `expected/classification.json` 바이트 그대로(바뀌면 멈추고 보고). 질문 16 해석 A면 같은 PR에서 case-schema 4.4에 `inferred`와 추정 표시도 넣는다 |
| 5 | M2 카드 다시 쪼갬 | 아래 표를 카드 문서로 쓴다 |

T-007 수용 기준(초안):

1. expected 3개와 스펙 블록 4개(3.2 모양 블록, 7.2~7.4)의 diff가 파일마다 `"schema_version": 1,` 한 줄 추가뿐이다. SHA-256 줄 3개만 값이 바뀐다.
2. 그 한 줄은 엔진 출력에서 복사하지 않고 손(또는 그 한 줄만 넣는 스크립트)으로 넣는다. 그 뒤 `ClassificationJson`이 맞춘다. 골든·블록 대조·속성 순서 테스트가 통과한다. 속성 순서 테스트는 3.2 블록을 읽으므로 테스트 코드는 바뀌지 않을 것으로 본다 [읽음 `ClassificationJsonTests.cs`](**미확인**).
3. 시작 전 main의 통과 수를 PR 본문에 적고, 끝난 뒤 그 이상이며 삭제·Skip이 없다. reviewer가 `git diff --stat`과 expected diff 전문을 PR에 붙인다.
4. 폴더 범위: `docs/spec/classification.md`, `examples/*/expected/`, `src/Scorecard.Core/`. expected 변경이라 juhan에게 묻는다(상시 승인 예외).

T-008 수용 기준(초안)은 ADR 0003 "영향"과 남은 질문을 따른다. 요지: `SIGNOFF` 분기 단위 테스트, `yes` 단계에 게이트 행 없음, 예제 expected 3개 바이트 그대로, (해석 A면) `inferred` 오류·표시 테스트(TestData).

그다음(제안, 질문 12): 4번째 예제 카드(ADR 0003 남은 질문 2) → M1 판정 카드(아직 없다 [읽음 `docs/tasks/`]) → M1 게이트 G1 → M2 카드를 하나씩. 지시문 10절은 "게이트를 못 넘으면 다음으로 가지 않는다"다 [읽음].

M2 카드(가칭, 하나씩 연다):

| 가칭 | 이름 | 목표 | 수용 기준(요지) | 폴더 범위 | 앞 카드 |
| --- | --- | --- | --- | --- | --- |
| M2-1 | 명령 틀과 AI용 입구 | 명령 넷과 숨긴 내부 명령, `--json`, 오류 JSON, 종료 코드 0/2/1, `json-output.md`, 프로세스 안 진입점. 첫 결과는 `classify` | 예제 3종 `classify --answers … --json` 표준 출력이 `expected/classification.json`과 바이트까지 같다(질문 14 추천안). 잘못된 `answers.yaml`이면 종료 코드 2와 오류 JSON(값이 `CaseError`와 같다), 내부 예외면 1. G2(R1~R4)·G3 통과, 절대 경로를 넣은 음성 대조를 G2 R3가 잡는다 | `docs/spec/`, `src/Scorecard.Core/`, `src/Scorecard.Cli/`, `tests/` | T-008, G1 |
| M2-2 | `start` 번호 마법사 | 사람용 입구. 서술 저장, 단계 이름, 질문을 번호로, Enter = 기본값(키 비움), 서명 단계 한 번, `answers.yaml` 쓰기 | 예제 3종을 번호 입력(표준 입력)으로 답하면 분류 결과가 `--answers`로 준 것과 바이트까지 같다. 서명 질문에 Enter면 모든 단계 `no`. `--json`이면 묻지 않는다 | `docs/spec/`, `src/Scorecard.Core/`, `src/Scorecard.Cli/`, `tests/` | M2-1 |
| M2-3 | 스코어카드 결과 객체·HTML·`open` | 요약·단계 표·제안 카드를 담은 결과 객체(Core), 같은 객체로 JSON(Core)과 단일 HTML(Cli) | 예제 3종이 `expected/report-outline.json`(새 파일, 첫 키 `schema_version`, juhan 확정)과 일치. HTML 렌더러는 결과 객체 하나만 받는다. HTML에 외부 참조가 없다. JSON·HTML 모두 G2·G3 통과 | `docs/spec/`, `src/Scorecard.Core/`, `src/Scorecard.Cli/`, `tests/`, `examples/`(report-outline.json 추가만) | M2-2 |
| M2-4 | 확인 한 화면 | 단계마다 맞음/바꾸기, 바꿀 때만 사유 한 줄. 터미널 한 화면과 `--review` 파일 | 맞음은 사유 없이 확정, 사유 없는 바꾸기는 확정 안 되고 `--json`이면 종료 코드 2. 엔진 분류와 사람 선택이 결과 객체에 둘 다 남는다 | `docs/spec/`, `src/Scorecard.Core/`, `src/Scorecard.Cli/`, `tests/` | M2-3 |
| M2-5 | `check` 자료 넣고 검증 | 읽기 전용 복사·해시, 대조(T-006), 시간 입력(`--times`), 판정(판정 카드), 스코어카드 갱신 | 예제 3종 `metrics.json`과 같고 검증 구역이 report-outline과 일치. 입력 SHA-256이 상대 경로 키로 남고 G3 통과 | `docs/spec/`, `src/Scorecard.Cli/`, `tests/` | M2-4 |
| M2-6 | `build` 의뢰서 | 제안 카드 단계로 `build-request/` 번들. `acceptance.md`에 제작 게이트 세 질문 | form-fill에서 번들 6종이 나오고 실제 값 패턴이 없다. `export-check.md` 사람 확인이 없으면 내보내지 않고 종료 코드 2 | `docs/spec/`, `src/Scorecard.Core/`, `src/Scorecard.Cli/`, `tests/` | M2-5 |
| M2-7 | 스킬 `/scorecard` | SKILL.md(질문 4·5 답대로). AI용 입구만 쓴다 | SKILL.md가 스펙 블록과 바이트까지 같다. SKILL.md에 나오는 명령은 모두 `--json`을 쓴다. 스킬 모양의 TestData `answers.yaml`(`inferred`, `human_signoff` 포함)이 종료 코드 0으로 분류된다. 대화는 G2 데모에서 사람이 본다 | `docs/spec/`, `.claude/skills/scorecard/`(질문 5), `tests/` | M2-6 |
| M2-8 | Excel 내보내기와 end-to-end | `open --excel`, 예제 3종 `start --answers`→`open`→`build` 테스트(사람용·`--json`), README 샘플 스코어카드 | G2 문구의 end-to-end 테스트 통과. ClosedXML을 N6 허용 목록에 더하고 PR 본문에 적는다 | `src/Scorecard.Cli/`, `tests/`, README(리드) | M2-7 |
| M4+ | (선택) MCP 서버 | AI용 입구(명령 넷 `--json`)를 MCP 도구로 감싼다. 카드만 만든다 | (카드 메모만) 전송은 표준 입출력만, 네트워크 없음. 도구 입출력 스키마는 `json-output.md`. 둘 곳(`src/` 안이면 G1 대상 추가)과 SDK 패키지의 G1 영향은 그 카드에서 실험한다 | 미정 | M4 |

- 질문 16이 해석 B면 T-008 다음, M2-1 앞에 "추정 답 표시" 카드(case-schema `inferred`, 근거 문장 표시)를 하나 더 둔다.
- expected를 새로 더하거나 바꾸는 카드(M2-3의 `report-outline.json`, 4번째 예제)는 그때마다 juhan에게 묻는다(상시 승인 예외).

## 바뀌지 않는 것

- T-001~T-005 결과물. 엔진(`Scorecard.Core`)에 AI 없음. 분류 트리의 Q1~Q4·ELSE(앞에 `SIGNOFF`가 더해진다, ADR 0003), 근거 문장 틀.
- `examples/`와 `expected/`의 파일 바이트. 예외는 juhan이 정한 둘뿐이다: T-007의 `"schema_version": 1,` 한 줄, T-008의 `human_signoff: no`. 새 파일(`metrics.json`은 T-006, `report-outline.json`은 M2-3)은 지시문 5절대로 더하고 juhan이 확정한다.
- 사람 게이트 3곳(2절 4). G1·G2·G3. `export-check.md` 사람 확인.
- T-006 카드와 진행(결정 2의 버전 반영과 순서만 더해진다).

## 미확인

1. 단일 HTML을 Windows 기본 브라우저에서 파일로 열 때와 브라우저 인쇄로 PDF를 만들 때의 모양. 실험 안 함.
2. ClosedXML 버전·의존 패키지·라이선스, G1 N6 허용 목록에 무엇을 더해야 하는지, Excel 바이트 결정론. 조회·실험 안 함.
3. 스킬이 Claude Code에서 `scorecard`를 부를 때 대화형 입력은 쓸 수 없을 것으로 본다. 그래서 스킬은 `--json`과 파일 입력만 쓴다(결정 6). 실험 안 함.
4. 스킬 사용자의 실행 환경. 배포(M4) 전에는 exe가 없어 `dotnet run --project src/Scorecard.Cli`와 .NET SDK가 필요하다.
5. 스킬 동작은 AI라 결정론이 아니다. 테스트는 스킬이 만드는 파일 모양과 SKILL.md 본문만 본다. 대화 품질은 G2 데모에서 사람이 본다.
6. README에서 HTML 샘플을 어떻게 보일지. GitHub이 저장소의 `.html`을 렌더하지 않고 소스로 보일 것으로 본다(**미확인**). 스크린샷 그림 + 파일 링크를 제안한다.
7. `scorecard.exe`를 Windows에서 두 번 클릭해 열면 마법사가 끝난 뒤 콘솔 창이 바로 닫히는지. 실험 안 함.
8. 4절 SQLite가 아직 필요한지. 답·확인을 파일로 두면 필요 없을 수 있다. 이 ADR은 건드리지 않는다. M2-1 스펙에서 본다.
9. 확인 한 화면에서 바꿀 수 있는 것(단계 행 담당만인지, 예외 검토·게이트 행도인지). M2-4 스펙에서 정한다.
10. System.CommandLine이 인자 오류에 내는 기본 종료 코드. 2로 맞추려면 설정·처리가 필요한지. 조회 안 함. System.CommandLine 패키지의 N6 허용 목록 추가는 지금도 M2 과제다.
11. Windows 콘솔에서 표준 출력에 UTF-8 바이트를 직접 쓸 때(리디렉션·파이프 포함) 바이트가 그대로인지. 실험 안 함.
12. MCP C# SDK 패키지가 G1(N1·N6)에 주는 영향, 표준 입출력 전송만 쓸 수 있는지. 조회 안 함(M4+ 카드).
13. T-007 뒤 `classification.md` 3.3 본문("숫자는 … `batch_size` 하나뿐이다")처럼 `schema_version`과 어긋나는 문장이 더 있는지. 다 찾지 않았다. T-007 스펙 단계에서 찾는다.

---

## 사람 승인 필요

결정 2로 답이 난 질문(추정 답 모양, 버전 필드)은 뺐다. 남은 질문마다 prototyper의 제안을 적었다. "제안대로"로 답할 수 있다. 2절 원칙(질문 1의 2절 1·3·6·7)과 expected(질문 2·9의 일부)는 상시 승인 예외라 따로 답이 필요하다.

1. **반영 방법.**
   - 제안: 이 ADR이 승인되면 리드가 "바뀌는 조항" 문구로 지시문(1·2·3·4·5·6·7·9·10·13절), `CLAUDE.md`, `.claude/agents/cli-dev.md`, README 두 벌, `review-checklist`, T-006 카드를 한 커밋으로 고친다. 조항 끝에 "(2026-11-07 사람 결정: ADR 0004)"를 붙인다(커밋 `7735f8a`와 같은 방식). 2절 1·6은 juhan 지시 문구, 2절 3·7은 이 ADR이 더 제안한 것이라 따로 예/아니오를 받는다. 테스트는 바뀌지 않는다.
2. **엔진의 추정 표시와 2절 11.**
   - 제안: 분류는 값으로만 한다. 트리가 읽은 답(classification 4.6) 중 추정이 있으면 문장 끝에 ` (AI 추정, 확인 필요)`, 기본값 표시와 같이면 그 뒤. `classification.json` 파일에는 `inferred`를 쓰지 않고 메모리 결과와 스코어카드 결과 객체에만 둔다. 추정 표시는 규칙·지표가 아니라 표시로 보고 2절 11의 "examples/ 먼저"를 적용하지 않는다. TestData로 테스트한다.
3. **추정 표시를 지우는 때.**
   - 제안: 사용자가 그 답을 직접 말하거나 그 답 하나를 보고 맞다고 하면 지운다. 한꺼번에 넘긴 답은 남긴다. 스킬은 모르는 답에 기본값을 써 넣지 않고 키를 비운다(`human_signoff`만 늘 명시). 단계 목록에는 추정 표시를 두지 않는다.
4. **스킬이 AI 서비스로 보내는 범위.** exe는 보내지 않는다. 스킬은 서술과 답을 보낸다.
   - 제안: 스킬에는 업무 서술과 답만 넣는다. 스킬은 `reference/`·`candidate/`·`cases/`의 자료 파일 내용을 읽지 않는다(SKILL.md에 적는다). `check`는 스킬이 부르더라도 엔진 JSON의 집계 숫자만 전한다. README에 "보안 경계 안 업무는 exe 입구를 쓴다"를 적는다.
5. **스킬 파일을 누가 쓰나.** 폴더 제한에 `.claude/skills/`를 쓸 에이전트가 없다.
   - 제안: prototyper가 `docs/spec/skill-scorecard.md`에 SKILL.md 본문 블록을 쓰고, 승인 뒤 리드가 `.claude/skills/scorecard/SKILL.md`로 바이트 그대로 옮긴다. 테스트가 블록과 파일이 같은지 본다(T-004 예제 옮기기와 같은 방식). CLAUDE.md 폴더 제한은 바꾸지 않는다.
6. **"스코어카드 열기"와 G1.** 브라우저를 띄우려면 `Process.Start`가 필요하다. G1 N2·N3가 Cli에서 이것을 막는다 [읽음].
   - 제안: G1을 그대로 둔다. exe는 브라우저를 띄우지 않는다. 사람용 터미널 메시지에 스코어카드 파일 경로를 크게 알린다("이 파일을 두 번 클릭하세요"). `--json`에는 상대 경로만 넣고, 스킬이 Claude Code에서 파일을 열어 준다. "열기"는 이 뜻으로 읽는다. 대안(Cli에 한해 G1 예외)은 2절 1을 약하게 해서 추천하지 않는다.
7. **확인 한 화면은 어디서.**
   - 제안: exe는 터미널 한 화면(단계 표를 보이고 "바꿀 단계 번호, 모두 맞으면 Enter" → 바꿀 단계만 담당 번호와 사유 한 줄). 스킬은 대화에서 같은 표를 보이고, 사용자가 고른 것만 확인 파일(가칭 `review.yaml`)에 적어 `--review`로 넘긴다. 파일이 없거나 고르지 않은 단계는 "확인 전"으로 남는다. HTML은 읽기 전용이고 확인 결과를 보인다. 대안(HTML 안에서 고르고 파일을 내려받아 넣기)은 한 단계가 더 생겨 추천하지 않는다.
   - 모양 예시(제안, table-compare expected 기준):

     ```text
     단계 확인 (모두 맞으면 Enter, 바꿀 단계는 번호를 쉼표로)
      1 정상 신호 구간 선택     ②
      2 불규칙 신호 구간 선택   ② + ④ 예외 검토
      3 구간 평균 계산          ②
      4 결과표 채우기           ② + ④ 사람 게이트
     바꿀 단계: 2
      2 불규칙 신호 구간 선택 → 담당 (1 ① 2 ② 3 ③ 4 ④): 4
        사유 한 줄: 예외가 많아 사람이 계속 한다
     ```
8. **HTML의 모양.**
   - 제안: 구역 순서는 3.5 제안(요약 → 단계 표 → 제안 카드 → 검증 결과 → 사람 게이트와 위험 → 다음 할 일 → 업무 개요·입력 기록). 스크립트를 넣지 않는다. 생성 일시를 넣지 않고 도구 버전과 입력 SHA-256을 넣는다. 그러면 HTML도 `--json`처럼 G2 바이트 비교를 그대로 쓸 수 있고, 2절 3의 생성 일시 예외는 쓰지 않아도 된다(문구는 그대로 둔다).
9. **요약의 "몇 단계 중 몇 단계가 ②".**
    - 제안: 행 모델 A의 행 수로 센다. 12절 "단계 수"와 같은 수다(T-004 승인 결정 1). ④ 행 수(예외 검토·게이트·`SIGNOFF`)를 "사람 확인"으로 같이 적는다. 예제 3종은 "6단계(사람 확인 2) 중 4단계가 ②", "5단계(사람 확인 1) 중 4단계가 ②", "5단계(사람 확인 1) 중 2단계가 ②"다 [읽음 `classification.md` 7.1 요약 표]. 이 값은 M2-3의 새 `report-outline.json`에 들어가므로 그 카드에서 juhan이 확정한다.
10. **절약 추정 식.** 지시문에 식이 없다.
    - 제안: 한 달 절약 추정(분) = ② 단계마다 `minutes_per_item` × `batch_size` × 월 횟수의 합. 월 횟수는 daily 20, weekly 4, monthly 1, rare 0. `minutes_per_item`이 없는 단계는 빼고 "시간 측정 필요 n단계"로 적는다. 하나도 없으면 숫자 대신 "시간 측정 필요". `check`로 잰 시간이 있으면 잰 값을 쓴다. 숫자(20·4·1·0)는 `report.md` 표에 두고 Core 상수와 표 대조 테스트로 지킨다(지시문 13절, T-004 승인 결정 9 방식). 예제 3종은 `minutes_per_item`이 없어 셋 다 "시간 측정 필요"다 [읽음 `classification.md` 7.1].
11. **명령 넷과 내부 명령.**
    - 제안: 도움말에는 `start`, `open`, `check`, `build`만 보인다. 내부 명령 9개는 숨긴 채 남겨 테스트·재현에 쓴다. 내부 명령도 `--json`·종료 코드 규칙을 똑같이 따른다. Excel 내보내기는 `open --excel`. 10절 G2 문구는 `start --answers`→`open`→`build`로 옮긴다.
12. **T-008 뒤의 순서와 동시 진행.**
    - 제안: T-008 → 4번째 예제 카드 → M1 판정 카드 → G1 → M2-1~M2-8을 하나씩. "카드끼리 동시에 열지 않는다"를 M2 끝까지 이어 가고, CLAUDE.md의 "동시 2개까지"는 그대로 두되 이 기간에는 1개로 운영한다고 카드에 적는다. M4+ MCP 서버 카드는 문서만 만든다.
13. **종료 코드와 오류 JSON 세부.**
    - 제안: 0 성공(보류·미검증 판정, `build`의 아니오·나중에, 확인 파일 없음 포함). 2 입력 오류(인자 오류, 파일 없음, 파싱·검증 오류, 사유 없는 바꾸기, 사람 확인 없는 내보내기). 1 내부 오류(예상하지 못한 예외, 메시지 대신 예외 타입 이름). `--json`이면 성공·오류 모두 표준 출력에 JSON 하나만 쓰고 표준 오류에는 쓰지 않는다. 오류 JSON은 `schema_version`, `exit_code`, `errors`(`CaseError` 여섯 값: code·source·line·column·path·message). `path`는 필드 경로, `source`는 파일 이름이다.
14. **`--json` 출력 모양과 스키마를 적는 법.**
    - 제안: 결정 2를 넓혀 모든 JSON의 첫 키를 `"schema_version": 1`로 한다. 봉투를 두지 않는다. `--json` 출력은 그 명령의 결과 객체 그대로이고, 파일과 같은 결과면 바이트도 같다(`classify --json` = `classification.json`). 그래서 예제 expected로 AI용 입구도 검사된다. 도구 버전은 `scorecard --version --json`으로 따로 낸다. 스키마는 지금 스펙들처럼 `docs/spec/json-output.md`에 키·형식·순서 표와 예시 블록으로 적고, 테스트가 표를 읽어 대조한다(`SpecDocument` 방식). JSON Schema 파일(`*.schema.json`)은 M4+ MCP 카드에서 같은 표로 만든다. 대안(봉투 `{schema_version, schema, tool_version, ok, result}`)은 파일과 바이트가 달라져 expected로 바로 검사할 수 없다.
15. **대화 없이 돌 때의 사람 확인.** 모든 명령이 파일만으로 돌면, 스킬(AI)이 사람 확인 파일을 쓸 수 있다.
    - 제안: 엔진은 파일만 본다. 그래서 규칙으로 막는다. `export-check.md`의 사람 확인 줄은 사람이 직접 적는다. 스킬은 그 줄을 쓰지 않는다(SKILL.md, review-checklist). `review.yaml`에는 스킬이 사용자가 대화에서 고른 것만 적는다. 이 둘을 지키는지는 사람 게이트(G2 데모)와 reviewer가 본다. 기술로 완전히 막을 수는 없다(**미확인**: 막는 방법을 실험하지 않았다).
16. **결정 2 "case-schema 4.4에 같은 PR에서 추가"의 "같은 PR".**
    - 해석 A(추천): T-008과 같은 PR. T-008도 case-schema 4.4·파서·`StepAnswers`를 고친다. 한 번에 고치면 공개 API 변경과 사람 승인이 한 번이다. T-008이 `inferred` 파싱과 근거 문장 추정 표시까지 맡는다.
    - 해석 B: `inferred`를 넣는 카드의 PR 안에서 스펙도 같이 고친다(스펙만 먼저 머지하지 않는다). 이때는 T-008 다음에 "추정 답 표시" 카드를 하나 더 둔다.
17. **제작 게이트 3곳의 대응.**
    - 제안: "결정 5" 표대로. 스펙 승인 = "이렇게 만들까요?", PR 머지 = "결과를 넣을까요?", 인수 = "2주 써 보고 괜찮나요?". 문구는 지시문 3.6 `acceptance.md` 행에 적고(2절 4는 그대로), M2 `build`가 번들에 넣는다. 제작 환경에서 묻는 일은 M3 템플릿, 2주 뒤 묻는 일은 `check`와 M4 사후 검증에서 한다. `export-check.md` 사람 확인은 따로 그대로 둔다.

## 결정

일부 결정(위 "사람 결정"). 나머지는 미정. 사람이 위 질문에 답한다.

## 영향

- 이 ADR만으로 바뀌는 파일은 없다. 지시문·CLAUDE.md·에이전트·스킬·스펙·카드는 승인 뒤 반영한다(질문 1).
- T-006 작업(`docs/spec/metrics.md`, `spikes/T-006/`)은 건드리지 않는다. 결정 2의 metrics.json 반영은 T-006 prototyper가 한다.
- 다음 할 일은 순서 1: ADR 0003·0004 두 문서만 커밋하고 juhan 승인을 받는다.

## 승인 결과

- 2026-11-07 06:16, juhan: "제안대로 승인". 사람 승인 필요 1~17을 모두 제안대로 정했다. 2절 원칙 1·3·6·7의 문구 변경도 포함한다(질문 1).
- 바뀌는 조항은 리드가 같은 PR의 한 커밋으로 반영한다.
