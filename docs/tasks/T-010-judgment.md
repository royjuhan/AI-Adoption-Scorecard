# T-010 판정 (통과·조건부·보류·미검증)

- 마일스톤: M1 (지시문 3.4, metrics.md 11절)
- 상태: 스펙 작성 중 (prototyper)

## 목표

대조 결과(`CompareMetrics`)와 케이스의 `thresholds`로 판정을 낸다. 판정은 통과·조건부·보류·미검증 넷이다(지시문 3.4). 판정마다 근거 문장을 Core가 만든다. 미검증이면 "무엇을 재면 되는지"를 적는다. 판정 로직은 Core에만 둔다.

## 수용 기준

1. 예제 4종의 판정이 지시문 12절과 맞는다: table-compare 조건부, form-fill 통과, mixed-exceptions 미검증, signoff-final(정확도 80.0, 문턱 미달)은 스펙이 정한 값.
2. 판정 규칙 표(문턱 경계, 시간 조건, "특정 유형에만 불일치가 몰림", "설명 안 되는 불일치")가 스펙과 테스트에서 맞는다. 경계는 셈(`counts`)으로 비교한다(metrics.md 4.8 권고).
3. 시간 자료가 없을 때의 판정과 표시가 정해져 있고 테스트가 있다.
4. 판정 결과를 담는 파일·속성(새 파일 또는 metrics.json 속성)과 expected가 스펙 블록에서 정해진다. expected 추가·변경은 juhan이 직접 승인하고 ADR 0004 expected 변경 규칙을 따른다.
5. 시작 전 main 통과 수는 1282다. 끝난 뒤 그 이상이고, 삭제·Skip된 테스트가 없다.
6. dotnet test가 ubuntu·windows CI에서 녹색이다.

## 폴더 범위

`docs/spec/`, `src/Scorecard.Core/`, `tests/`, `examples/*/expected/`(juhan 승인 뒤), `examples/*/case.yaml`(필요하면, juhan 승인 뒤)

## 메모

- 출발점: 지시문 3.4 판정 문장, metrics.md 11절(넘겨받는 것, 권고), case-schema의 `thresholds`(승인 질문 11), T-005 4.7(table-compare 불일치가 `irregular`에 몰림).
- "몰림"에 쓸 열 이름은 `case.yaml`에 없다(metrics.md 승인 질문 16). 스키마에 키를 더할지 스펙 질문으로 묻는다.
- 시간 지표(처리·검토 시간)는 내부 명령 `measure`(M2)의 몫이다. 이 카드에서 시간 입력을 어디서 받을지, 없으면 판정이 어떻게 되는지 정한다.
- AI 과잉 후보는 분류 카드(T-004)에 이미 있다.
