# CHANGELOG

사람이 읽는 변경 요약이다. 자세한 결정은 `docs/decisions/`, 카드별 기록은 `docs/STATUS.md`에 있다.

## v0.2.0-alpha (2026-11-10)

처음으로 GitHub 릴리스에서 받는 판이다. v0.1.0-alpha 태그는 만들지 않았다(ADR 0017).

### 바뀐 것

- 스킬 `/scorecard`: 단계마다 사람 손이 가는 동작을 나열하고 동작마다 담당을 정한다. 말한 것·AI 추정·기본값을 나누고, 추정은 추정형 문장으로 쓴다. 질문은 추천이 바뀌는 것만 3개까지. 자기 검사 규칙 `docs/spec/skill-output-rules.md`(ADR 0016).
- 스코어카드: 상태 네 줄, 결론 세 줄(유지할 것·먼저 확인할 것·아직 판단하지 않은 것), 지금 방식·추천·바꿔야 할 것·확인할 것 네 칸, "담당자 확인", 셈은 "말한 것만"과 "확인하면 바뀔 수 있음"을 따로. 코드·해시는 펼쳐 보기 안. `scorecard.json` schema 2.
- 엔진(분류 트리·대조·판정)과 예제 정답(`expected/`)은 그대로다.
- 내려받기: 릴리스를 발행하면 win-x64 zip이 자동으로 붙는다(`.github/workflows/release.yml`).

## v0.1.0-alpha (2026-11-08)

첫 알파. M2(명령 넷, 스킬, HTML 스코어카드, 제작 의뢰서 번들)까지 들어 있다. 기록: ADR 0015.

### 쓸 수 있는 것

- 명령 넷. 묻지 않고 파일 입력과 옵션만 받는다. `--json`이면 JSON 문서 하나를 낸다. 종료 코드는 0 성공, 2 입력 오류, 1 내부 오류(ADR 0004).
  - `scorecard start <case> --answers <파일>`: 단계를 분류해 `classification.json`을 쓴다.
  - `scorecard check <case>`: 기준 자료와 결과 자료를 대조해 `metrics.json`, 판정해 `judgment.json`을 쓴다.
  - `scorecard open <case>`: 스코어카드 HTML 한 장(`scorecard.html`)과 결과 객체(`scorecard.json`)를 쓴다. `--review <파일>`로 단계 확인(맞음/바꾸기 + 사유)을 기록한다.
  - `scorecard build <case>`: 먼저 만들 ② 단계를 추천한다. `--step <번호>`면 제작 의뢰서 번들 `build-request/`를 쓴다.
- 분류: 단계마다 담당 ①기존 기능 ②규칙 기반 SW(②M 학습 모델 포함) ③실행 시 AI ④사람. AI 판단(`ai`)과 규칙 트리의 "규칙 의견"을 같이 적고, 다르면 표시한다. 기본값·AI 추정 답은 "확인 필요"로 표시한다. `classification.json` schema 2(ADR 0010·0013).
- 대조·지표: CSV 키 맞추기, 허용 오차(절대·백분율), 정확도·누락률, 오류 13종(`metrics.json`).
- 판정: 정확도·시간·AI 필요성 판정(`judgment.json`).
- 문장 결과 검증: 체크리스트와 점수표로 문장 결과를 확인한다(ADR 0009).
- HTML 스코어카드: 외부 로드 없는 한 파일. 390·900·1236px에서 가로 스크롤 없음. 입력 SHA-256을 적는다. 샘플: `docs/samples/scorecard-table-compare.html`.
- 제작 의뢰서 번들: `spec.md`, `io-schema.json`, `acceptance.md`, `context.json`, `samples/`, `export-check.md`. 자료 값은 넣지 않는다. `export-check.md`에 사람 확인이 없거나 검사에 걸리면 내보내지 않는다(종료 코드 2).
- 스킬 `/scorecard`: 단계마다 AI 판단과 추정 답을 보여 주고, 사용자가 확인한 뒤에만 `answers.yaml`을 쓰고 `start --answers`를 부른다. 숫자·판정은 엔진 JSON 값만 옮긴다.
- 공개 합성 예제 5종: table-compare, form-fill, mixed-exceptions, signoff-final, reply-drafts.
- 같은 입력·같은 버전이면 같은 바이트(경로·문화권과 상관없다). 입력 파일은 읽기만 한다.

### 알려진 한계

- 배포 파일은 win-x64 단일 exe다. Windows 실기에서 아직 돌려 보지 않았다(ADR 0015).
- 비공개 사례 1건 로컬 실행(G2)은 아직 하지 않았다.
- `--times`(시간 입력)와 절약 추정, Excel 내보내기(`open --excel`), 확인 뒤 다시 분류·판정, 실행마다 새 출력 폴더는 아직 없다.
- 그 밖의 미룬 일과 리뷰의 막지 않는 지적은 `docs/LATER.md`에 있다.
