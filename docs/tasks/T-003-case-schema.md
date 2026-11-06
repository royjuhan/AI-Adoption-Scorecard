# T-003 case.yaml 스키마와 파서

- 마일스톤: M0 (지시문 11절)
- 상태: 리뷰 중 (스펙 승인 2026-11-06)

## 목표

`docs/spec/case-schema.md`를 확정하고, YamlDotNet으로 `case.yaml`을 읽고 검증하는 파서를 Core에 만든다.

## 수용 기준

1. `docs/spec/case-schema.md`에 필드, 형식, 필수 여부, 기본값이 있다. 지시문 3.2(질문 9개)와 12.1(case.yaml 예시)에 맞는다.
2. 필수 필드가 빠지면 어떤 필드인지 알려 주는 오류를 낸다.
3. 파서는 결정론·원본 불변 검사(T-002 G2·G3)의 첫 실제 대상으로 `GuardTargets`에 등록된다.
4. dotnet test가 ubuntu·windows CI에서 녹색이다.

## 폴더 범위

`docs/spec/`, `src/Scorecard.Core/`, `tests/`

## 샘플 자료

`tests/Scorecard.Tests/TestData/`에 합성 case.yaml(정상, 필드 누락, 형식 오류). 실제 업무 이름·값 없음.

## 메모 (T-002에서 넘어온 것)

- 파서를 T-002의 `GuardTargets`에 등록한다(T-002 승인 결정 7).
- 대상이 출력을 하나도 쓰지 않으면 G2·G3가 빈 출력끼리 비교해 그냥 통과한다. "R1 출력이 비어 있으면 차이로 적는다"를 스펙에서 정한다(T-002 reviewer 지적 7).
- YamlDotNet은 Core의 첫 제3자 패키지다. G1 N6 허용 목록에 더하고 PR 본문에 적는다(T-002 승인 결정 3).
- 출력에는 입력 폴더 기준 상대 경로만 쓴다(T-002 승인 결정 9).
