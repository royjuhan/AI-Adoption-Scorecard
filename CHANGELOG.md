# CHANGELOG

사람이 읽는 변경 요약이다. 자세한 결정은 `docs/decisions/`, 카드별 기록은 `docs/STATUS.md`에 있다.

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
