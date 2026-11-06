# T-002 네트워크 금지·결정론·원본 불변 테스트

- 마일스톤: M0
- 상태: 스펙 작성 대기 (prototyper)

## 목표

절대 원칙 1·2·3을 지키는지 검사하는 테스트 세 가지를 만든다.

1. 네트워크 금지: `src/`에 `HttpClient`, `Socket`, `WebClient`, `Process.Start`, LLM API 참조가 없다.
2. 결정론: 같은 입력을 두 번 처리하면 같은 출력이 나온다.
3. 원본 불변: 입력 파일의 SHA-256이 실행 전후로 같다.

## 수용 기준

1. 세 가지 검사 테스트가 `tests/Scorecard.Tests/`에 있다.
2. 일부러 `src/Scorecard.Core`에 `HttpClient`를 넣으면 네트워크 금지 테스트가 실패한다. 이 확인 결과를 PR 본문에 적는다.
3. `dotnet test`가 ubuntu·windows CI에서 녹색이다.

## 폴더 범위

`tests/`. 샘플은 `tests/Scorecard.Tests/TestData/`(ADR 0002).

## 샘플 자료

결정론·원본 불변 테스트용 합성 입력 파일. 실제 값 없음.

## 메모

아직 Core에 처리 기능이 없다. 결정론·원본 불변 테스트가 무엇을 실행할지는 prototyper가 스펙에서 정한다.
