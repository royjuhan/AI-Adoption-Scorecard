# AI-Adoption-Scorecard

AI Adoption Scorecard (AI 도입 평가표). 사용자의 업무를 질문으로 단계화하고, 단계별 담당(①기존기능/②규칙기반SW/③실행시AI/④사람)을 분류하고, AI 적용 사례의 정확도·시간·AI 필요성을 측정해 스코어카드와 제작 의뢰서를 낸다.
스펙: @docs/spec/classification.md @docs/spec/metrics.md @docs/spec/report.md

## 절대 원칙
- src/에 네트워크·LLM·외부 프로세스 호출 금지. 테스트가 검사한다.
- 입력 파일은 읽기만. 출력은 새 폴더에. 입력 해시를 기록.
- 같은 입력·같은 버전 → 같은 결과. 난수·현재시각을 판정에 쓰지 않음.
- 판정·분류 로직은 Scorecard.Core에만. Cli는 표시만.
- 테스트 없는 기능은 미완료. 기존 테스트 삭제·완화는 PR 본문에 이유 + reviewer 확인.
- main 직접 커밋 금지. 브랜치 이름 task/T-xxx-요약.
- 자유 서술은 저장만, 구조화는 정해진 질문으로. 기본값은 "기본값 사용, 확인 필요"로 표시.
- build-request/는 export-check.md에 사람 확인이 없으면 내보내지 않음.

## 작업 흐름
작업 카드(docs/tasks) → prototyper 스펙 → [사람 승인] → test-engineer 실패 테스트 → core-dev/cli-dev 구현 → reviewer → (지적 있으면 구현으로) → [사람 PR 머지] → 마일스톤 끝 [사람 데모 확인] → release
- 리뷰 루프는 3회까지. 넘으면 사람에게 올린다.
- 동시 진행 작업 카드는 2개까지. 카드에 폴더 범위를 적는다.

## 에이전트 폴더 제한
prototyper: spikes/, docs/ · test-engineer: tests/, cases/ · core-dev: src/Scorecard.Core/ · cli-dev: src/Scorecard.Cli/ · reviewer: 수정 없음 · sweeper: 테스트 제외 전부(동작 변경 없음) · release: 태그·CHANGELOG·publish

## 스타일
한글 문서·주석, 영문 식별자. 짧은 문장. .NET 10, xUnit, dotnet format.
