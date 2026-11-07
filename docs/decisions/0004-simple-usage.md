# 0004. 사용을 단순하게: 입구 둘, 엔진 하나, 스코어카드 HTML 한 장

- 날짜: 2026-11-07
- 상태: 제안 (사람 승인 대기)
- 작성: prototyper
- 확인 대상 커밋: `82f3652`
- 관련: 지시문 1·2·3·4·5·6·7·9·10·13절, `CLAUDE.md`, `.claude/agents/cli-dev.md`, `docs/spec/case-schema.md`(T-003), `docs/spec/classification.md`(T-004), `docs/spec/report.md`, `docs/spec/build-request.md`, ADR 0003

표기: `[읽음]` 커밋된 파일을 읽어 확인. **미확인**: 실험·조회하지 않은 것. **해석**: 지시 원문에 없어 prototyper가 채운 것(사람 확인 필요). 담당 ①~④와 행 모델 A는 `classification.md`의 이름이다.

이 ADR은 파일을 바꾸지 않는다. 아래 문구는 모두 제안이다. 사람이 승인한 뒤에 반영한다(질문 1).

## 지시 원문 (juhan, 2026-11-07)

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
바뀌지 않는 것: T-001~T-005 결과물, examples/·expected/, 사람 게이트 3곳.
순서: ADR 0004 초안 → 내 승인 → T-006(대조 엔진)은 그대로 진행 → M2 카드를 이 흐름으로 다시 쪼개.
```

## 배경

지금 지시문이 정한 사용 흐름 [읽음 `docs/README-instructions.md`]:

| 항목 | 지금 |
| --- | --- |
| 3.1 사용자 흐름 | 9단계. 명령 9개: `new`, `interview [--answers]`, `classify`, `compare`, `measure`, `judge`, `review`, `report`, `propose`. 4단계(자료 투입)는 "폴더에 복사" |
| 3.1 사람 검토 | `review`: "분류·판정을 한 줄씩 확인, 사유 입력. 사유 없으면 확정 안 됨" |
| 3.5 스코어카드 | "Excel 시트 = PDF 섹션". 목차 6개 |
| 4절 기술 스택 | Excel = ClosedXML, PDF = QuestPDF("커뮤니티 라이선스 조건 확인 후 사용, 안 되면 Excel만"), 저장 = SQLite |
| 6절 cli-dev | "Excel·PDF 평가서", "report 명령: … Excel 시트를 만들고 같은 내용을 PDF로 낸다" |
| 9절 스킬 | `new-case`, `review-checklist`, `intake-request` 셋. 사용자 입구 스킬은 없다 |
| 10절 M2 | 만드는 것 "interview(--answers)·classify·compare·measure·judge·review·report·propose, Excel·PDF, 번들". G2 "`interview --answers`→`report`→`propose` end-to-end", "샘플 평가서(Excel·PDF)를 README에 게시" |
| 2절 1 | "`src/`에 LLM API, 네트워크 호출, 외부 프로세스 호출을 넣지 않는다" |
| 2절 6 | "사용자의 서술을 프로그램이 '이해'하려 하지 않는다. 정해진 질문으로 답을 받고…" |
| 13절 첫 줄 | "사용자 서술을 LLM이나 자체 알고리즘으로 '이해'해서 단계를 스스로 나누지 않는다" |

지금 저장소 상태 [읽음]:

- Cli는 빈 진입점이다. `Program.cs`가 "아직 명령이 없습니다 (M0)"만 찍는다. M2 명령은 하나도 없다. 그래서 흐름을 지금 바꾸는 비용이 작다.
- Core에는 `Cases`(T-003), `Classification`(T-004), `Synthetic`(T-005)이 있다. T-006(대조)은 진행 중이다.
- G1은 `Scorecard.Core.dll`과 `scorecard.dll`을 검사한다. Cli에도 N1~N6이 걸린다. N2·N3는 `System.Diagnostics.Process`·`ProcessStartInfo`를 막는다 [읽음 `guard-tests.md` 4.2, `tests/Scorecard.Tests/Guards/NetworkGuardTests.cs`, `MetadataScanner.cs`].
- `answers.yaml`은 `case.yaml` 스키마의 부분이다(T-004 승인 결정 13). 표에 없는 키는 `unknown-field` 오류다. 단계 항목의 키는 `name`, `answers` 둘뿐이다 [읽음 `case-schema.md` 4.3·4.4].
- `report.md`, `build-request.md`는 미작성이다 [읽음].
- `guard-tests.md` 미확인 9: "Excel·PDF 바이트 결정론"(ClosedXML·QuestPDF 미실험) [읽음].
- `README.md`·`README.ko.md` "하는 일" 4: "스코어카드(Excel + PDF)" [읽음].

## 결정 (제안)

### 1. 입구 둘, 엔진 하나

| | 스킬 `/scorecard` | `scorecard.exe` | 엔진 `Scorecard.Core` |
| --- | --- | --- | --- |
| 위치 | `.claude/skills/scorecard/SKILL.md` (`src/` 밖) | `src/Scorecard.Cli` | `src/Scorecard.Core` |
| AI | 쓴다(Claude Code 대화) | 없음. 오프라인 | 없음 |
| 하는 일 | 자유 서술을 질문으로 좁혀 `answers.yaml` 초안을 만든다. 답을 보여 확인받고 `scorecard start --answers`를 부른다 | 정해진 질문에 번호로 답하는 마법사. Enter면 기본값 | 분류·지표·판정. 근거 문장 |
| 하지 않는 일 | ①~④를 스스로 정하거나 미리 말하지 않는다. 지표·판정을 계산하지 않는다 | 판정·분류 로직(Core만) | — |

- 두 입구는 같은 `answers.yaml`을 만든다. 엔진은 어느 입구에서 왔는지 모른다.
- AI가 추정한 답은 `source: inferred`로 표시한다(아래 "추정 답 표시"). exe는 추정 답을 만들지 않는다.
- 모르는 답은 두 입구 모두 키를 비운다. 그래서 기본값 표시("기본값 사용, 확인 필요")가 지금처럼 남는다. 스킬은 기본값을 값으로 써 넣지 않는다(**해석**).

### 2. 결과는 HTML 한 장

- 기본 결과는 외부 파일 없는 단일 HTML이다. 오프라인에서 연다.
- 맨 위 요약(n단계 중 m단계가 ②, 절약 추정) → 단계 표(①~④ 배지, 근거 한 줄, 기본값 표시) → 제안 카드("이 단계 만들까요?").
- Excel은 내보내기다. PDF는 브라우저 인쇄다. QuestPDF를 기술 스택에서 뺀다.
- 스크립트를 넣지 않는다(**해석**, 질문 9). 그림·글꼴·스타일은 모두 HTML 안에 둔다.
- 분류·수치·추천은 Core가 만든다. HTML은 Cli가 그 결과로 쓴다(표시만). Core·Cli 모두 패키지를 더하지 않는다. 이스케이프는 `System.Text.Encodings.Web`의 `HtmlEncoder`를 쓴다. 이 어셈블리는 이미 Core가 참조하는 프레임워크 어셈블리다 [읽음 `classification.md` 4.10].

### 3. 명령 넷

| 사용자가 하는 일 | 명령 | 안에서 하는 일 (지금 3.1의 명령) |
| --- | --- | --- |
| 업무를 말한다 | `start` | `new` → `interview`(`--answers`면 묻지 않음) → `classify` → 확인 한 화면(`review`) → `report` → 스코어카드 열기 |
| 스코어카드를 본다 | `open` | `report`. `--excel`이면 Excel 내보내기(**해석**) |
| 자료를 넣고 검증한다 | `check` | 자료 투입(읽기 전용 복사 + 해시) → `compare` → `measure` → `judge` → 확인 한 화면(`review`) → `report` |
| 만들 단계를 고른다 | `build` | `propose` → 예면 `build-request/` 번들, `export-check.md` 확인 |

- 지금 명령 9개는 내부·테스트용으로 남긴다. 도움말에는 넷만 보인다(**해석**, 질문 13).

### 4. 확인은 한 화면

- 단계마다 "맞음 / 바꾸기"만 고른다. 바꿀 때만 사유를 한 줄 쓴다.
- 지금 3.1의 "사유 없으면 확정 안 됨"은 "바꾸기에 사유가 없으면 확정 안 됨"으로 좁힌다. 맞음은 사유 없이 확정된다.
- 엔진 분류와 사람 선택을 둘 다 남긴다. 사람이 바꿔도 엔진 결과를 지우지 않는다(**해석**).
- 어디서 고르는지는 질문 8.

### 5. 제작 게이트 3곳은 쉬운 말로

지시문 2절 4의 사람 게이트 3곳은 그대로다. 제작물(사용자의 단계를 만드는 일)에서 의뢰한 사람에게 묻는 말만 바꾼다. 대응은 **해석**이다(질문 12).

| 2절 4 게이트 | 쉬운 말 | 묻는 곳(제안) |
| --- | --- | --- |
| 스펙 승인 | "이렇게 만들까요?" | 제작 환경(M3 `scorecard-factory-template`)에서 스펙을 보이고 묻는다. M2 `build`는 번들의 `acceptance.md`에 이 질문을 적는다 |
| PR 머지 | "결과를 넣을까요?" | 제작 환경. 만든 도구와 테스트 보고서를 보이고 묻는다 |
| 마일스톤 데모(제작물은 인수) | "2주 써 보고 괜찮나요?" | 받은 쪽. 2주 사용 뒤 `check`로 다시 재고 묻는다(M4 "사후 평가서 v2"와 같은 자리) |

- `export-check.md`의 사람 확인(2절 8, 3.6)은 그대로 둔다. 실제 자료가 없는지 보는 보안 확인이라 제작 게이트와 따로 있다.

## 바뀌는 조항과 고칠 문구 (초안)

지시문은 사람이 쓴 문서다. 지난번에는 사람 결정 뒤 리드가 고치고 조항 끝에 "(날짜 사람 결정: …)"을 붙였다 [읽음 커밋 `7735f8a`]. 같은 방식을 제안한다(질문 1).

### 지시문 2절 1

- 지금 [읽음]: "**실행 시 AI 호출 없음.** `src/`에 LLM API, 네트워크 호출(`HttpClient`, `Socket`, `WebClient`), 외부 프로세스 호출을 넣지 않는다. 테스트가 이를 검사한다."
- 제안:

> 1. **실행 시 AI 호출 없음 (엔진과 exe에 한해).** `src/`(엔진 `Scorecard.Core`와 `scorecard.exe`)에 LLM API, 네트워크 호출(`HttpClient`, `Socket`, `WebClient`), 외부 프로세스 호출을 넣지 않는다. 테스트가 이를 검사한다. Claude Code 스킬 `/scorecard`는 `src/` 밖의 입구라 AI를 쓴다. 스킬은 `answers.yaml`을 만들고 `scorecard` 명령을 부를 뿐, 분류·지표·판정을 하지 않는다(2026-11-07 사람 결정: ADR 0004).

### 지시문 2절 6

- 지금 [읽음]: "**자유 서술은 저장만, 구조화는 질문으로.** 사용자의 서술을 프로그램이 "이해"하려 하지 않는다. 정해진 질문으로 답을 받고, 모르면 기본값을 넣고 "기본값 사용, 확인 필요"로 표시한다."
- 제안:

> 6. **자유 서술은 저장만, 구조화는 질문으로 (엔진과 exe에 한해).** 엔진과 exe는 사용자의 서술을 "이해"하려 하지 않는다. 정해진 질문으로 답을 받고, 모르면 기본값을 넣고 "기본값 사용, 확인 필요"로 표시한다. 스킬 `/scorecard`는 서술을 질문으로 좁혀 `answers.yaml` 초안을 만든다. AI가 추정한 답은 `source: inferred`로 표시하고, 답을 사용자에게 보여 확인받은 뒤에만 엔진을 돌린다. 모르는 답은 비워 두어 기본값 표시가 남게 한다(2026-11-07 사람 결정: ADR 0004).

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
> | 업무를 말한다 | `scorecard start <case> [--answers answers.yaml]` | 자유 서술을 `intake.md`에 그대로 저장. "입력은 무엇, 거기서 무엇을, 결과는 어디로"를 묻고 단계를 만든 뒤, 단계마다 질문 9개를 번호로 고른다. 모르면 Enter → 기본값. 분류 → 확인 한 화면 → 스코어카드를 쓰고 연다. `--answers`로 답 파일을 주면 묻지 않는다(스킬·테스트·재현용) |
> | 스코어카드를 본다 | `scorecard open <case> [--excel]` | 저장된 답·확인·검증 결과로 스코어카드를 다시 쓰고 연다. `--excel`이면 같은 내용을 Excel로 내보낸다 |
> | 자료를 넣고 검증한다 | `scorecard check <case>` | `reference/`(사람이 만든 결과), `candidate/`(AI 적용 후 결과)를 읽기 전용으로 복사하고 해시를 남긴다. 대조, 건당 처리·검토 시간 입력(스톱워치 또는 수기), 판정 제안(통과/조건부/보류/미검증) → 확인 한 화면 → 스코어카드 갱신 |
> | 만들 단계를 고른다 | `scorecard build <case>` | ② 단계 중 먼저 만들 하나를 추천 → "이 단계 만들까요?" 예 / 아니오 / 나중에 → 예면 `build-request/` 번들 생성 |
>
> - 확인 한 화면: 단계마다 "맞음 / 바꾸기"만 고른다. 바꿀 때만 사유를 한 줄 쓴다. 사유 없는 바꾸기는 확정되지 않는다.
> - 내부 명령 `new`, `interview`, `classify`, `compare`, `measure`, `judge`, `review`, `report`, `propose`는 테스트·재현용으로 남긴다. 도움말에는 넷만 보인다.

### 지시문 3.5 (스코어카드)

- 지금 [읽음]: 제목 "스코어카드 목차 (Excel 시트 = PDF 섹션)", 목차 1 업무 개요, 2 단계 분해표와 담당 제안, 3 소프트웨어로 대체 가능한 범위·먼저 만들 단계 1개, 4 검증 결과, 5 사람 게이트와 남는 위험, 6 다음 할 일(제작 여부 포함).
- 제안(구역 순서는 질문 9):

> ### 3.5 스코어카드 (HTML 한 장)
>
> 외부 파일 없는 단일 HTML 하나. 오프라인에서 연다. 스크립트·외부 글꼴·외부 그림을 쓰지 않는다. Excel은 내보내기(`open --excel`, 시트 = 아래 구역), PDF는 브라우저 인쇄.
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
- "내보내기 명령은 `export-check.md`에 사람 확인이 없으면 실패한다" → "`build`는 …"로 명령 이름만 바꾼다.

### 지시문 4절 (기술 스택)

| 항목 | 지금 [읽음] | 제안 |
| --- | --- | --- |
| 평가서 | (없음) | 단일 HTML. 패키지 없음. Cli가 쓰고 `HtmlEncoder`로 이스케이프 |
| Excel | ClosedXML | ClosedXML (내보내기만) |
| PDF | QuestPDF (커뮤니티 라이선스 조건 확인 후 사용, 안 되면 Excel만) | 행 삭제. "PDF는 브라우저 인쇄(QuestPDF 쓰지 않음)"를 평가서 행에 적는다 |
| 입구 스킬 | (없음) | Claude Code 스킬 `.claude/skills/scorecard/`. 제품 코드가 아니다. `src/` 밖 |
| 저장 | SQLite | 이 ADR은 건드리지 않는다(미확인 8) |

### 지시문 5절 (저장소 구조)

- `skills/` 줄에 `scorecard/SKILL.md`를 더한다.
- `spec/` 줄에 M2 스펙 파일을 더한다(가칭 `commands.md`, `skill-scorecard.md`. 아래 "영향 받는 스펙").

### 지시문 6절과 `.claude/agents/cli-dev.md`

| 줄 | 지금 [읽음] | 제안 |
| --- | --- | --- |
| description | "질문 흐름(interview), 명령 구조, Excel·PDF 평가서, 제작 의뢰서 번들 생성" | "명령 넷(start·open·check·build)과 내부 명령, 번호 마법사, 확인 한 화면, 단일 HTML 스코어카드와 Excel 내보내기, 제작 의뢰서 번들" |
| 2 | "interview 명령: 질문은 docs/spec/classification.md의 9개 순서대로. Enter면 기본값. 답은 case.yaml에 쓴다." | "start 명령: 질문은 docs/spec/case-schema.md 4.4의 9개 순서대로 번호로 고른다. Enter면 기본값이고 키를 비워 둔다. 답은 answers.yaml에 쓴다." (질문 표는 case-schema 4.4에만 있다. T-003 승인 결정 5 [읽음]) |
| 3 | "report 명령: … Excel 시트를 만들고 같은 내용을 PDF로 낸다." | "open 명령: docs/spec/report.md의 구역 순서대로 단일 HTML을 쓴다. 외부 파일·스크립트를 넣지 않는다. --excel이면 같은 내용을 Excel 시트로." |
| 4 | "propose 명령: ② 단계 중 하나를 추천하고 "예/아니오/나중에"를 받는다. …" | "build 명령: ② 단계 중 하나를 추천하고 "이 단계 만들까요?"에 예/아니오/나중에를 받는다. …"(뒷부분 그대로) |
| 새 6 | — | "브라우저·다른 프로그램을 띄우지 않는다(G1 N2·N3). 스코어카드 파일 경로를 알린다." (질문 7) |

### 지시문 7절과 `CLAUDE.md`

- "src/에 네트워크·LLM·외부 프로세스 호출 금지. 테스트가 검사한다." → "src/(엔진·exe)에 네트워크·LLM·외부 프로세스 호출 금지. 테스트가 검사한다. AI는 src/ 밖의 스킬 /scorecard에서만."
- "자유 서술은 저장만, 구조화는 정해진 질문으로. 기본값은 "기본값 사용, 확인 필요"로 표시." → "엔진·exe는 자유 서술을 저장만, 구조화는 정해진 질문으로. 기본값은 "기본값 사용, 확인 필요"로 표시. 스킬이 추정한 답은 source: inferred로 표시하고, 사용자 확인 뒤에만 엔진에 넣는다."

### 지시문 9절 (스킬)

표에 한 행을 더한다:

> | `scorecard` | 사용자가 Claude Code에서 업무를 말할 때(`/scorecard`) | 서술을 `intake.md`에 그대로 저장 → 정해진 질문(단계, 질문 9개)으로 좁혀 `answers.yaml` 초안 → 추정한 답은 `source: inferred`, 모르면 키를 비움 → 답을 사용자에게 보여 확인 → `scorecard start <case> --answers` → 엔진 결과를 그대로 전하고 확인 한 화면 → `build` 안내. ①~④를 스스로 말하지 않는다. 분류·지표·판정은 엔진만 |

### 지시문 10절 M2 행

- 제안:

> | M2 CLI + 스코어카드 | 3주 | 명령 넷 `start`·`open`·`check`·`build`(내부 명령 new·interview(--answers)·classify·compare·measure·judge·review·report·propose), 번호 마법사, 확인 한 화면, 단일 HTML 스코어카드와 Excel 내보내기, 번들, 스킬 `/scorecard` | G2: 예제 3종에서 `start --answers`→`open`→`build`까지 end-to-end 테스트 통과. 샘플 스코어카드(HTML)를 README에 게시. 비공개 사례 1건을 같은 exe로 로컬 실행해 요약만 README "사례"에 추가 |

- 지금 G2 문구 `interview --answers`→`report`→`propose`를 새 명령으로 그대로 옮겼다. `check`는 넣지 않았다(지금 G2에도 `compare`가 없다 [읽음]). `check`는 자기 카드(M2-5)에서 테스트한다.

### 그 밖에 리드가 같이 고칠 곳

- `README.md`·`README.ko.md` "하는 일" 4: "스코어카드(Excel + PDF)" → "스코어카드(HTML 한 장)". "실행할 때는 AI를 쓰지 않는다"는 엔진·exe에 대해 그대로 맞다.
- `.claude/skills/review-checklist/SKILL.md`에 세 줄(제안):
  - [ ] 스킬 `/scorecard`가 분류·지표·판정을 하지 않고 엔진 결과만 전하는가
  - [ ] 스코어카드 HTML에 외부 참조(`http:`, `https:`, `//`, 외부 `src`·`href`)가 없는가
  - [ ] 추정 답에 `source: inferred` 표시가 남는가
- `.claude/skills/new-case/SKILL.md`: `/scorecard`·`start`와 겹친다. 지우지 않고 "사용자 입구는 /scorecard" 한 줄을 더한다(제안).

## 추정 답 표시 (`source: inferred`)

### 지금 스키마에 들어갈 자리가 없다

- 답 9개 매핑에 다른 키를 쓰면 `unknown-field`, 답 값 자리에 매핑을 쓰면 `type` "스칼라 값이어야 한다"다 [읽음 `case-schema.md` 4.4·4.9]. 단계 항목의 키는 `name`, `answers`뿐이다.
- 그래서 어떤 모양이든 승인된 T-003 스펙(case-schema 4.3·4.4·4.12)을 바꾸는 별도 카드가 필요하다(M2-1).

### 모양 후보

안 1. 단계에 선택 키 `source` (추천):

```yaml
steps:
  - name: 주문서 양식 읽기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 20}
    source: {batch_size: inferred, frequency: inferred}
```

안 2. 답 값 자리에 매핑:

```yaml
    answers: {input_kind: fixed_form, batch_size: {value: 20, source: inferred}}
```

안 3. 따로 둔 파일(`answers.source.yaml` 같은 것). 엔진은 읽지 않고 스코어카드만 읽는다.

| 항목 | 안 1 단계 `source` | 안 2 답 값 매핑 | 안 3 따로 둔 파일 |
| --- | --- | --- | --- |
| 원문 `source: inferred`와 | 낱말은 같고 모양이 다르다 | 글자 그대로 같다 | 다르다 |
| case-schema 바뀌는 곳 | 단계 항목 표에 1행 | 답 9개 모두 "스칼라 또는 매핑". 4.6 형식·4.9 `type` 규칙이 키마다 두 갈래 | 없음 |
| 지금 `type` 오류 자리 | 그대로 | 답 값의 매핑이 오류가 아니게 된다. 그 자리를 검사하는 기존 테스트가 있는지는 다 보지 않았다(**미확인**) | 그대로 |
| 엔진이 근거 문장에 표시 | 할 수 있다 | 할 수 있다 | 못 한다 |
| 두 파일이 어긋날 위험 | 없음 | 없음 | 있다 |
| 기존 예제 3종 | 그대로 읽힌다 | 그대로 읽힌다 | 그대로 |

- 추천 안 1. 바뀌는 곳이 가장 적고, 답 값의 모양(4.6)을 그대로 둔다. 안 2가 원문 글자에 더 가깝다는 점은 그대로 적는다. → 질문 2.

### 안 1의 규칙 (초안)

- `source`는 선택이다. 없으면 모든 답이 추정이 아니다.
- 키는 질문 9개 키 중 하나(아니면 `unknown-field`), 값은 `inferred` 하나(아니면 `enum`).
- `source`에 적은 키는 `answers`에 값이 있어야 한다. 값이 없거나 null이면 그 답은 기본값이지 추정이 아니다. 오류로 막는다(코드·문구는 M2-1 스펙).
- 그래서 한 답이 기본값이면서 추정일 수는 없다. 기본값 = 사용자도 AI도 값을 주지 않음. 추정 = AI가 값을 줬고 사용자가 그 값을 따로 말하지 않음.
- 파서 결과: `Answer<T>`에 `IsInferred`를 더한다. 공개 API 변경이다(T-003 승인 결정 14를 고친다). 기존 2인자 생성(`new Answer<string>("fixed_form", true)` [읽음 `CaseApiShapeTests.cs`])은 선택 인자로 두면 그대로 컴파일될 것으로 본다(**미확인**).
- exe 마법사와 지금 예제는 `source`를 쓰지 않는다.

### 엔진이 추정 답을 다루는 법 (초안)

- 분류(담당·근거 키·행)는 바뀌지 않는다. 엔진은 값만 본다. 추정 답도 값이 있으면 그 값으로 분류한다.
- 근거 문장: 그 행을 정할 때 트리가 읽은 답(`classification.md` 4.6 표) 중 하나라도 `IsInferred`면 문장 끝에 ` (AI 추정, 확인 필요)`를 붙인다. 기본값 표시와 같이 붙으면 기본값 표시 뒤에 둔다. 4.6 표와 규칙 모양은 그대로 쓴다.
- 단계마다 추정 답 키 목록(case-schema 4.4 순서)을 메모리 결과 `StepClassification`에 둔다. `classification.json`에는 쓰지 않는다(추천). `Used`도 메모리에만 있는 것과 같은 방식이다 [읽음 `classification.md` 3.2].
- 대안: 목록이 비어 있지 않을 때만 `"inferred"` 속성을 쓴다. 이 경우도 예제 바이트는 그대로다.
- 확인 항목(4.7)·AI 과잉 후보(4.8)는 그대로다.
- 스코어카드 단계 표는 답마다 기본값·추정 표시를 같이 보인다.
- → 질문 3.

### 예제·expected를 바꾸지 않는 조건과 맞추기

- 예제 3종의 `answers.yaml`·`case.yaml`에는 `source`가 없다 [읽음 `examples/*/answers.yaml`, `case.yaml`]. 안 1·2·3 모두 없는 파일을 지금과 같게 읽는다. 모든 답의 `IsInferred`가 false다.
- 그래서 문장 표시가 붙지 않는다. `classification.json` 형식을 그대로 두면(추천) 바이트가 같다. 기존 골든 테스트(`tests/Scorecard.Tests/Classification/ExampleGoldenTests.cs` [읽음 파일 목록])가 그대로 지킨다.
- 추정 답 테스트는 `tests/Scorecard.Tests/TestData/`의 새 합성 표본으로 한다(ADR 0002). `examples/`에 파일을 더하지 않는다.
- 어긋남(그대로 적음): 지시문 2절 11은 "새 규칙·지표를 넣으려면 `examples/*/expected/`에 기대값을 먼저 추가한다"이다 [읽음]. 추정 표시는 분류 규칙·지표가 아니라 표시라고 보고 TestData로만 테스트하는 안을 제안한다. → 질문 3.

### 추정 표시는 언제 지우나

- 제안: 사용자가 그 답을 직접 말하거나, 그 답 하나를 보고 "맞다"고 하면 스킬이 `source`에서 지운다. 한꺼번에 넘긴 답("다 맞아")은 남긴다. → 질문 4.
- 스킬은 모르는 답에 기본값을 값으로 써 넣지 않는다. 키를 비운다. 그래야 기본값 표시가 사라지지 않는다(2절 6).
- 단계 나누기 자체(단계 이름)에는 추정 표시를 두지 않는다. 단계 목록은 사용자가 통째로 확인한다(**해석**).

## 스킬 `/scorecard`와 원칙 1의 경계

- 스킬은 `.claude/skills/scorecard/SKILL.md`다. 기존 스킬 셋과 같은 자리·모양(frontmatter `name`, `description`)이다 [읽음 `.claude/skills/*/SKILL.md`]. 컴파일되지 않고 exe에 들어가지 않는다.
- G1은 `Scorecard.Core.dll`, `scorecard.dll`만 본다 [읽음 `guard-tests.md` 2절·4.2]. 스킬은 G1 대상이 아니고, 대상일 필요도 없다. G1·G2·G3는 그대로다. `.claude/rules/core-no-network.md`(paths `src/Scorecard.Core/**`)도 그대로다 [읽음].
- 스킬은 엔진을 `scorecard` 명령으로만 부른다. 프로세스를 띄우는 것은 Claude Code이고 `src/`가 아니다.
- 스킬이 지킬 것(SKILL.md에 적는다, 제안):
  1. 서술은 `intake.md`에 그대로 저장한다. 고쳐 쓰지 않는다.
  2. 질문은 case-schema 4.4의 9개와 3.1의 단계 질문만 쓴다. 선택지 밖의 값을 만들지 않는다.
  3. 추정한 답은 `source: inferred`. 모르면 키를 비운다.
  4. `answers.yaml` 전체를 사용자에게 보이고 확인받은 뒤에만 `scorecard start --answers`를 부른다.
  5. ①~④, 정확도, 판정을 스스로 말하지 않는다. 엔진 결과(`classification.json`, 스코어카드)를 그대로 전한다.
  6. `reference/`·`candidate/`·`cases/`의 자료 파일 내용을 읽지 않는다(질문 5).
- 데이터 경계: 스킬을 쓰면 사용자의 업무 서술과 답이 AI 서비스로 간다. exe는 그렇지 않다. 지시문 1절은 번들이 "보안 경계 밖"으로 간다고 하고, 2절 8·10은 실제 자료를 경계 밖·저장소에 두지 않는다 [읽음]. 스킬이 경계 밖으로 보내는 것을 어디까지로 둘지 사람이 정해야 한다. → 질문 5.
- 스킬 파일을 쓸 에이전트가 없다. CLAUDE.md 폴더 제한에 `.claude/skills/`가 없다 [읽음]. → 질문 6.

## 영향 받는 스펙

| 스펙 | 영향 |
| --- | --- |
| `case-schema.md` (T-003, 승인됨) | 단계 항목 `source`(안 1), `Answer<T>.IsInferred`, 오류 한 가지. M2-1 카드에서 고치고 사람 승인 |
| `classification.md` (T-004, 승인됨) | 4.6에 추정 표시 규칙 한 단락과 표 하나. 3.2 `classification.json` 형식은 그대로(추천). 7절 예제 블록 그대로 |
| `report.md` (미작성) | HTML 한 장으로 쓴다. 구역 순서(3.5 제안), 요약 숫자 세는 법(질문 10), 절약 추정 식(질문 11), 먼저 만들 단계 추천 규칙, 생성 일시(질문 9), `expected/report-outline.json` 모양. Excel 시트 = HTML 구역 |
| `build-request.md` (미작성) | `build` 명령. 번들 파일은 3.6 그대로. `acceptance.md`에 제작 게이트 세 질문. `export-check.md` 사람 확인 그대로 |
| `metrics.md` (T-006 작성 중) | 바뀌지 않는다. `check`가 대조 결과를 쓴다 |
| `guard-tests.md` (T-002, 승인됨) | G1 그대로. 미확인 9의 "PDF 바이트 결정론"은 QuestPDF를 빼면 없어진다. HTML 렌더러는 G2·G3 대상에 더한다(M2-3) |
| `synthetic-examples.md` (T-005, 승인됨) | 바뀌지 않는다 |
| 새 스펙 (가칭) | `commands.md`: 명령 넷, 번호 마법사 화면, 확인 한 화면, 답·확인 파일을 어디에 쓰는지(2절 2: 입력을 덮어쓰지 않는다). `skill-scorecard.md`: 스킬 본문 블록(질문 6) |
| ADR 0003 (제안) | 충돌 없음. 후보 1(질문 10개)로 정해지면 마법사·스킬의 질문 수만 바뀐다 |

## M2 카드 재분할 (초안)

- 번호는 리드가 카드를 열 때 정한다. 아래는 가칭 M2-1~M2-8이다.
- 앞에 남은 일: T-006(대조, 진행 중, 그대로)과 M1 판정 카드(아직 없다 [읽음 `docs/tasks/`]). 지시문 10절은 "게이트를 못 넘으면 다음으로 가지 않는다"다 [읽음]. 그래서 M2 카드는 지금 써 두고, M1 게이트 G1 뒤에 연다(질문 14).
- 동시 진행 2개(CLAUDE.md)를 지키는 순서(제안): [T-006 + 판정 카드] → G1 → [M2-1 + M2-2] → [M2-3] → [M2-4 + M2-6] → [M2-5 + M2-7] → [M2-8].

| 가칭 | 이름 | 목표 | 수용 기준(요지) | 폴더 범위 | 앞 카드 |
| --- | --- | --- | --- | --- | --- |
| M2-1 | 추정 답 표시 | case-schema·classification에 `source: inferred`(질문 2~4 답대로) | 예제 3종 `classification.json` 바이트 그대로. TestData 추정 표본에서 `IsInferred`·문장 표시가 스펙 표와 같다 | `docs/spec/`, `src/Scorecard.Core/`, `tests/` | 없음 |
| M2-2 | `start` 번호 마법사 | exe 입구. 서술 저장, 단계 이름, 질문 9개를 번호로, Enter = 기본값(키 비움), `answers.yaml` 쓰기, 분류 | 예제 3종을 번호 입력(표준 입력)으로 답하면 `classification.json`이 expected와 바이트까지 같다. `--answers`로 주면 묻지 않고 같은 결과 | `docs/spec/`, `src/Scorecard.Core/`, `src/Scorecard.Cli/`, `tests/` | 없음 |
| M2-3 | 스코어카드 HTML과 `open` | 요약·단계 표·제안 카드를 단일 HTML로. `start` 끝에 쓰고 `open`이 다시 쓴다 | 예제 3종이 `expected/report-outline.json`(새 파일, 사람 확정)과 일치. HTML에 외부 참조가 없고 G2·G3 통과 | `docs/spec/`, `src/Scorecard.Core/`, `src/Scorecard.Cli/`, `tests/`, `examples/`(report-outline.json 추가만) | M2-2 |
| M2-4 | 확인 한 화면 | 단계마다 맞음/바꾸기, 바꿀 때만 사유 한 줄. 결과를 스코어카드에 표시 | 맞음은 사유 없이 확정, 사유 없는 바꾸기는 확정 안 됨. 엔진 분류와 사람 선택이 둘 다 남는다 | `docs/spec/`, `src/Scorecard.Core/`, `src/Scorecard.Cli/`, `tests/` | M2-3 |
| M2-5 | `check` 자료 넣고 검증 | 읽기 전용 복사·해시, 대조 설정 묻기, 대조(T-006), 시간 입력, 판정(판정 카드), 스코어카드 갱신 | 예제 3종 `metrics.json`과 같고 검증 구역이 report-outline과 일치. 입력 SHA-256이 스코어카드에 남고 G3 통과 | `docs/spec/`, `src/Scorecard.Cli/`, `tests/` | M2-3, T-006, 판정 카드 |
| M2-6 | `build` 의뢰서 | 제안 카드 단계로 `build-request/` 번들. `acceptance.md`에 제작 게이트 세 질문 | form-fill에서 번들 6종이 나오고 실제 값 패턴이 없다. `export-check.md` 사람 확인이 없으면 내보내기가 실패한다 | `docs/spec/`, `src/Scorecard.Core/`, `src/Scorecard.Cli/`, `tests/` | M2-3 |
| M2-7 | 스킬 `/scorecard` | SKILL.md(질문 5·6 답대로). 스킬이 만드는 `answers.yaml` 모양을 엔진이 받는다 | SKILL.md가 스펙 블록과 바이트까지 같다. 스킬 모양의 TestData `answers.yaml`(추정 답 포함)이 오류 없이 분류된다. 대화는 G2 데모에서 사람이 본다 | `docs/spec/`, `.claude/skills/scorecard/`(질문 6), `tests/` | M2-1, M2-2 |
| M2-8 | Excel 내보내기와 end-to-end | `open --excel`, 예제 3종 `start --answers`→`open`→`build` 테스트, README 샘플 스코어카드 | G2 문구의 end-to-end 테스트 통과. ClosedXML을 N6 허용 목록에 더하고 PR 본문에 적는다 | `src/Scorecard.Cli/`, `tests/`, README(리드) | M2-3~M2-7 |

## 바뀌지 않는 것

- T-001~T-005 결과물. 엔진(`Scorecard.Core`)에 AI 없음. 분류 트리, 질문 9개, 근거 문장 틀.
- `examples/`와 `expected/`의 지금 파일 바이트. 새 파일(`metrics.json`은 T-006, `report-outline.json`은 M2-3)은 지시문 5절대로 더한다(질문 3).
- 사람 게이트 3곳(2절 4). G1·G2·G3. `export-check.md` 사람 확인.
- T-006 카드와 진행.

## 미확인

1. 단일 HTML을 Windows 기본 브라우저에서 파일로 열 때와 브라우저 인쇄로 PDF를 만들 때의 모양. 실험 안 함.
2. ClosedXML 버전·의존 패키지·라이선스, G1 N6 허용 목록에 무엇을 더해야 하는지, Excel 바이트 결정론. 조회·실험 안 함.
3. 스킬이 Claude Code에서 `scorecard`를 부를 때 대화형 입력(번호 마법사·확인 화면)을 쓸 수 없을 것으로 본다. 그래서 스킬은 `--answers`와 확인 파일을 쓴다(질문 8). 실험 안 함.
4. 스킬 사용자의 실행 환경. 배포(M4) 전에는 exe가 없어 `dotnet run --project src/Scorecard.Cli`와 .NET SDK가 필요하다.
5. 스킬 동작은 AI라 결정론이 아니다. 테스트는 스킬이 만드는 파일 모양만 본다. 대화 품질은 G2 데모에서 사람이 본다.
6. README에서 HTML 샘플을 어떻게 보일지. GitHub이 저장소의 `.html`을 렌더하지 않고 소스로 보일 것으로 본다(**미확인**). 스크린샷 그림 + 파일 링크를 제안한다.
7. `scorecard.exe`를 Windows에서 두 번 클릭해 열면 마법사가 끝난 뒤 콘솔 창이 바로 닫히는지. 실험 안 함.
8. 4절 SQLite가 아직 필요한지. 답·확인을 파일로 두면 필요 없을 수 있다. 이 ADR은 건드리지 않는다. M2-2 스펙에서 본다.
9. 확인 한 화면에서 바꿀 수 있는 것(단계 행 담당만인지, 예외 검토·게이트 행도인지). M2-4 스펙에서 정한다.

---

## 사람 승인 필요

질문마다 prototyper의 제안을 적었다. "제안대로"로 답할 수 있다.

1. **반영 방법.** 지시문은 사람이 쓴 문서다.
   - 제안: 이 ADR이 승인되면 리드가 "바뀌는 조항" 문구로 지시문(1·2·3·4·5·6·7·9·10·13절), `CLAUDE.md`, `.claude/agents/cli-dev.md`, README 두 벌, `review-checklist`를 한 커밋으로 고친다. 조항 끝에 "(2026-11-07 사람 결정: ADR 0004)"를 붙인다(커밋 `7735f8a`와 같은 방식). 테스트는 바뀌지 않는다.
2. **`source: inferred` 모양.** 안 1 단계 `source` 매핑 / 안 2 답 값 `{value, source}` / 안 3 따로 둔 파일.
   - 제안: 안 1. `source: {batch_size: inferred}`. 값은 `inferred` 하나. 답이 없는 키는 적을 수 없다(오류). 답 값의 모양(case-schema 4.6)을 그대로 둔다.
3. **엔진의 추정 표시와 예제.**
   - 제안: 분류는 값으로만 한다. 트리가 읽은 답(classification 4.6) 중 추정이 있으면 문장 끝에 ` (AI 추정, 확인 필요)`, 기본값 표시와 같이면 그 뒤. `classification.json` 형식은 그대로 두고 추정 키 목록은 메모리 결과에만. 추정 표시는 규칙·지표가 아니라 표시로 보고 2절 11의 "examples/ 먼저"를 적용하지 않는다. TestData로 테스트한다. "examples/·expected/는 바뀌지 않는다"는 "지금 파일의 바이트"로 읽고, 새 expected 파일(`metrics.json`, `report-outline.json`)은 지시문 5절대로 더한다.
4. **추정 표시를 지우는 때.**
   - 제안: 사용자가 그 답을 직접 말하거나 그 답 하나를 보고 맞다고 하면 지운다. 한꺼번에 넘긴 답은 남긴다. 스킬은 모르는 답에 기본값을 써 넣지 않고 키를 비운다. 단계 목록에는 추정 표시를 두지 않는다.
5. **스킬이 AI 서비스로 보내는 범위.** exe는 보내지 않는다. 스킬은 서술과 답을 보낸다.
   - 제안: 스킬에는 업무 서술과 답만 넣는다. 스킬은 `reference/`·`candidate/`·`cases/`의 자료 파일 내용을 읽지 않는다(SKILL.md에 적는다). `check`는 exe로 하거나, 스킬은 엔진이 낸 요약 숫자만 전한다. README에 "보안 경계 안 업무는 exe 입구를 쓴다"를 적는다.
6. **스킬 파일을 누가 쓰나.** 폴더 제한에 `.claude/skills/`를 쓸 에이전트가 없다.
   - 제안: prototyper가 `docs/spec/skill-scorecard.md`에 SKILL.md 본문 블록을 쓰고, 승인 뒤 리드가 `.claude/skills/scorecard/SKILL.md`로 바이트 그대로 옮긴다. 테스트가 블록과 파일이 같은지 본다(T-004 예제 옮기기와 같은 방식). CLAUDE.md 폴더 제한은 바꾸지 않는다.
7. **"스코어카드 열기"와 G1.** 브라우저를 띄우려면 `Process.Start`가 필요하다. G1 N2·N3가 Cli에서 이것을 막는다 [읽음].
   - 제안: G1을 그대로 둔다. exe는 브라우저를 띄우지 않고 스코어카드 파일 경로를 크게 알린다("이 파일을 두 번 클릭하세요"). 스킬은 Claude Code에서 파일을 열어 준다. "열기"는 이 뜻으로 읽는다. 대안(Cli에 한해 G1 예외를 두기)은 2절 1을 약하게 해서 추천하지 않는다.
8. **확인 한 화면은 어디서.**
   - 제안: exe는 터미널 한 화면(단계 표를 보이고 "바꿀 단계 번호, 모두 맞으면 Enter" → 바꿀 단계만 담당 번호와 사유 한 줄). 스킬은 대화에서 같은 표를 보이고 고른 것만 확인 파일(가칭 `review.yaml`)에 적어 엔진에 넘긴다. 고르지 않은 단계는 "확인 전"으로 남는다. HTML은 읽기 전용이고 확인 결과를 보인다. 대안(HTML 안에서 고르고 파일을 내려받아 넣기)은 한 단계가 더 생겨 추천하지 않는다.
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
9. **HTML의 모양.**
   - 제안: 구역 순서는 3.5 제안(요약 → 단계 표 → 제안 카드 → 검증 결과 → 사람 게이트와 위험 → 다음 할 일 → 업무 개요·입력 기록). 스크립트를 넣지 않는다. 생성 일시를 넣지 않고 도구 버전과 입력 SHA-256을 넣는다. 그러면 G2 바이트 비교를 그대로 쓸 수 있고, 2절 3의 생성 일시 예외는 쓰지 않아도 된다(문구는 그대로 둔다).
10. **요약의 "몇 단계 중 몇 단계가 ②".**
    - 제안: 행 모델 A의 행 수로 센다. 12절 "단계 수"와 같은 수다(T-004 승인 결정 1). 사람 확인 행 수를 같이 적는다. 예제 3종은 "6단계(사람 확인 2) 중 4단계가 ②", "5단계(사람 확인 1) 중 4단계가 ②", "5단계(사람 확인 1) 중 2단계가 ②"다 [읽음 `classification.md` 7.1 요약 표].
11. **절약 추정 식.** 지시문에 식이 없다.
    - 제안: 한 달 절약 추정(분) = ② 단계마다 `minutes_per_item` × `batch_size` × 월 횟수의 합. 월 횟수는 daily 20, weekly 4, monthly 1, rare 0. `minutes_per_item`이 없는 단계는 빼고 "시간 측정 필요 n단계"로 적는다. 하나도 없으면 숫자 대신 "시간 측정 필요". `check`로 잰 시간이 있으면 잰 값을 쓴다. 숫자(20·4·1·0)는 `report.md` 표에 두고 Core 상수와 표 대조 테스트로 지킨다(지시문 13절, T-004 승인 결정 9 방식). 예제 3종은 `minutes_per_item`이 없어 셋 다 "시간 측정 필요"다 [읽음 `classification.md` 7.1].
12. **제작 게이트 3곳의 대응.**
    - 제안: "결정 5" 표대로. 스펙 승인 = "이렇게 만들까요?", PR 머지 = "결과를 넣을까요?", 인수 = "2주 써 보고 괜찮나요?". 문구는 지시문 3.6 `acceptance.md` 행에 적고(2절 4는 그대로), M2 `build`가 번들에 넣는다. 제작 환경에서 묻는 일은 M3 템플릿, 2주 뒤 묻는 일은 `check`와 M4 사후 검증에서 한다. `export-check.md` 사람 확인은 따로 그대로 둔다.
13. **명령 넷과 내부 명령.**
    - 제안: 도움말에는 `start`, `open`, `check`, `build`만 보인다. 내부 명령 9개는 숨긴 채 남겨 테스트·스킬·재현에 쓴다. Excel 내보내기는 `open --excel`. 10절 G2 문구는 `start --answers`→`open`→`build`로 옮긴다.
14. **M2 카드 분할과 시작 시점.**
    - 제안: "M2 카드 재분할" 표의 8장(가칭 M2-1~M2-8). 리드가 카드 문서를 지금 쓰고, T-006과 M1 판정 카드가 끝나 G1을 넘은 뒤에 연다. 동시 2장 순서는 표 위의 제안대로. T-006은 그대로 진행한다.

## 결정

미정. 사람이 위 질문에 답한다.

## 영향

- 이 ADR만으로 바뀌는 파일은 없다. 지시문·CLAUDE.md·에이전트·스킬·스펙·카드는 승인 뒤 반영한다(질문 1).
- T-006 작업(`docs/spec/metrics.md`, `spikes/T-006/`)은 건드리지 않는다.
