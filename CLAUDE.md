# AI-Adoption-Scorecard

AI Adoption Scorecard (AI 도입 평가표). 사용자의 업무를 질문으로 단계화하고, 단계별 담당(①기존기능/②규칙기반SW/③실행시AI/④사람)을 분류하고, AI 적용 사례의 정확도·시간·AI 필요성을 측정해 스코어카드와 제작 의뢰서를 낸다.
스펙: @docs/spec/classification.md @docs/spec/metrics.md @docs/spec/report.md

## 절대 원칙
- src/(엔진·exe)에 네트워크·LLM·외부 프로세스 호출 금지. 테스트가 검사한다. AI는 src/ 밖의 스킬 /scorecard에서만. 스킬은 --json 입구만 쓴다(2026-11-07 사람 결정: ADR 0004).
- 입력 파일은 읽기만. 출력은 새 폴더에. 입력 해시를 기록.
- 같은 입력·같은 버전 → 같은 결과. 난수·현재시각을 판정에 쓰지 않음.
- 판정·분류 로직은 Scorecard.Core에만. Cli는 표시만.
- 테스트 없는 기능은 미완료. 기존 테스트 삭제·완화는 PR 본문에 이유 + reviewer 확인.
- main 직접 커밋 금지. 브랜치 이름 task/T-xxx-요약.
- 엔진·exe는 자유 서술을 저장만, 구조화는 정해진 질문으로. 기본값은 "기본값 사용, 확인 필요"로 표시. 스킬이 추정한 답은 단계의 inferred 목록에 적고, 사용자 확인 뒤에만 엔진에 넣는다(2026-11-07 사람 결정: ADR 0004). human_signoff는 서명 단계를 한 번 물어 모든 단계에 적는다(ADR 0003).
- build-request/는 export-check.md에 사람 확인이 없으면 내보내지 않음.
- 모든 명령은 대화 없이 파일 입력만으로 돈다. --json 출력은 같은 입력이면 바이트까지 같다. 종료 코드 0 성공, 2 입력 오류, 1 내부 오류(2026-11-07 사람 결정: ADR 0004).
- expected/ 변경, 테스트 삭제·완화, ADR 결정, 지시문 2절 원칙 변경은 T-010까지 사람에게 묻는다. T-010 뒤에는 추천안을 채택하고 카드와 docs/STATUS.md에 한 줄씩 적는다. 회귀는 CI 잡(build-test·regression-guard·mutation·oracle)이 막는다. 원칙 변경은 ADR로 남긴다. 지시문 2절 8·10(실제 자료·회사 자료 금지)은 바꾸지 않고 제안도 하지 않는다. expected 변경은 ADR 0004의 expected 변경 규칙을 따른다(2026-11-07 사람 결정: ADR 0004·0005).

## 작업 흐름
작업 카드(docs/tasks) → prototyper 스펙 → [사람 승인] → test-engineer 실패 테스트 → core-dev/cli-dev 구현 → reviewer → (지적 있으면 구현으로) → [사람 PR 머지] → 마일스톤 끝 [사람 데모 확인] → release
- T-010 뒤(ADR 0005): [사람 승인]은 추천안 채택 + STATUS.md 기록, [사람 PR 머지]는 CI 잡 모두 녹색 + reviewer 막는 지적 없음이면 리드가 머지, [사람 데모 확인]은 [재검토] 한 번 + STATUS.md 기록. 멈추지 않고 다음 카드로 간다.
- 리뷰 루프는 3회까지. 넘으면 사람에게 올린다(T-010 뒤에는 ADR 0005 [재검토]).
- 동시 진행 작업 카드는 2개까지. 카드에 폴더 범위를 적는다.

## 에이전트 폴더 제한
prototyper: spikes/, docs/, tools/oracle/(스펙만 보고, src/를 읽지 않는다) · test-engineer: tests/, cases/ · core-dev: src/Scorecard.Core/ · cli-dev: src/Scorecard.Cli/ · reviewer: 수정 없음 · ci-dev: .github/workflows/, tools/ci/, .config/dotnet-tools.json(바꾸면 ADR과 STATUS.md 원칙 변경 행을 같이 둔다) · sweeper: 테스트·tools/oracle/·tools/ci/·.github/workflows/ 제외 전부(동작 변경 없음) · release: 태그·CHANGELOG·publish

## 스타일
한글 문서·주석, 영문 식별자. 짧은 문장. .NET 10, xUnit, dotnet format.
