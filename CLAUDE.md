# AI-Adoption-Scorecard

AI Adoption Scorecard (AI 도입 평가표). 사용자의 업무를 질문으로 단계화하고, 단계별 담당(①기존기능/②규칙기반SW/③실행시AI/④사람)을 분류하고, AI 적용 사례의 정확도·시간·AI 필요성을 측정해 스코어카드와 제작 의뢰서를 낸다.
스펙: @docs/spec/classification.md @docs/spec/metrics.md @docs/spec/report.md

## 절대 원칙
- 도구는 온라인 AI 도구다. 분류·제안·스코어카드 작성은 AI(스킬 /scorecard, 이후 독립 앱)가 한다. 검증(대조·지표·판정)은 Scorecard.Core 규칙 엔진이 하고, AI는 --json 입구로 부른다. 숫자는 늘 코드가 낸다. "오프라인·실행 시 AI 없음"은 납품물(의뢰서로 만드는 소프트웨어)에만. 네트워크·LLM·외부 프로세스 금지 가드는 지우지 않고 납품물 템플릿 검사로 옮긴다. 옮기는 카드(M2-2 또는 M2-4) 전까지 src/에 그대로 걸린다(2026-11-08 사람 결정: ADR 0010).
- 입력 파일은 읽기만. 출력은 새 폴더에. 입력 해시를 기록.
- 같은 입력·같은 버전 → 같은 결과. 난수·현재시각을 판정에 쓰지 않음.
- 판정 로직과 규칙 의견(분류 트리)은 Scorecard.Core에만. Cli·HTML·스킬은 Core의 숫자·판정을 바꾸지 않는다.
- 테스트 없는 기능은 미완료. 기존 테스트 삭제·완화는 PR 본문에 이유 + reviewer 확인.
- main 직접 커밋 금지. 브랜치 이름 task/T-xxx-요약.
- 분류는 AI가 단계마다 ①~④(②M 포함) 판단과 근거를 쓴다. 질문 10개와 규칙 트리는 AI의 체크리스트이고, 엔진은 트리 결과를 "규칙 의견"으로 같이 적는다. 다르면 스코어카드에 표시한다. exe는 묻지 않는다(문답 없음). 모르는 답은 기본값과 "기본값 사용, 확인 필요"로 표시. AI가 추정한 답은 단계의 inferred 목록에 적는다. human_signoff는 서명 단계를 한 번 물어 모든 단계에 적는다(ADR 0003). AI에 보낼 자료는 사용자가 정하고 도구는 넣은 자료를 그대로 쓴다(2026-11-08 사람 결정: ADR 0010).
- build-request/는 export-check.md에 사람 확인이 없으면 내보내지 않음.
- 모든 명령은 대화 없이 파일 입력만으로 돈다. --json 출력은 같은 입력이면 바이트까지 같다. 종료 코드 0 성공, 2 입력 오류, 1 내부 오류(2026-11-07 사람 결정: ADR 0004).
- expected/ 변경, 테스트 삭제·완화, ADR 결정, 지시문 2절 원칙 변경은 T-010까지 사람에게 묻는다. T-010 뒤에는 추천안을 채택하고 카드와 docs/STATUS.md에 한 줄씩 적는다. 회귀는 CI 잡(build-test·test-run·regression-guard·mutation·oracle·oracle-head)과 머지 전 `tools/ci/lead_check.py`가 막는다. 원칙 변경은 ADR로 남긴다. 지시문 2절 8·10(실제 자료·회사 자료 금지)은 바꾸지 않고 제안도 하지 않는다. expected 변경은 ADR 0004의 expected 변경 규칙을 따른다(2026-11-07 사람 결정: ADR 0004·0005).

## 작업 흐름
작업 카드(docs/tasks) → prototyper 스펙 → [사람 승인] → test-engineer 실패 테스트 → core-dev/cli-dev 구현 → reviewer → (막는 지적만 구현으로) → [사람 PR 머지] → 마일스톤 끝 [사람 데모 확인] → release
- T-010 뒤(ADR 0005·0010): [사람 승인]은 추천안 채택 + STATUS.md 기록, [사람 PR 머지]는 CI 잡 모두 녹색 + main 쪽 도구로 돌린 `tools/ci/lead_check.py`가 종료 0 + 리뷰 1회에 막는 지적 없음이면 리드가 머지(lead_check가 민감한 경로를 적으면 그 한 번의 리뷰를 카드를 본 적 없는 reviewer가 diff 전문으로 한다), [사람 데모 확인]은 두지 않는다(M2-4 뒤 v0.1.0-alpha 태그). 멈추지 않고 다음 카드로 간다.
- 리뷰는 PR마다 1회. 막는 지적만 고치고 나머지는 docs/LATER.md에 적고 머지. 2회차 리뷰 없음(ADR 0010).
- [재검토]는 CI 빨간불일 때만. 마일스톤 끝에도 돌지 않는다. 보강·정리 카드는 열지 않고 발견한 문제는 docs/LATER.md에 적는다(ADR 0010).
- 스펙은 200줄 이내. M2 카드는 스펙+구현을 PR 하나로. oracle은 Core 대조·판정에만, 스킬·HTML·명령은 골든 테스트만(ADR 0010).
- effort: prototyper·reviewer high, 나머지 medium, [재검토] reviewer만 xhigh(ADR 0010).
- 동시 진행 작업 카드는 2개까지. 카드에 폴더 범위를 적는다.

## 에이전트 폴더 제한
prototyper: spikes/, docs/, tools/oracle/(스펙만 보고, src/를 읽지 않는다) · test-engineer: tests/, cases/ · core-dev: src/Scorecard.Core/ · cli-dev: src/Scorecard.Cli/ · reviewer: 수정 없음 · ci-dev: .github/workflows/, tools/ci/, .config/dotnet-tools.json(바꾸면 ADR과 STATUS.md 원칙 변경 행을 같이 둔다) · sweeper: 테스트·tools/oracle/·tools/ci/·.github/workflows/ 제외 전부(동작 변경 없음) · release: 태그·CHANGELOG·publish

## 스타일
한글 문서·주석, 영문 식별자. 짧은 문장. .NET 10, xUnit, dotnet format.
