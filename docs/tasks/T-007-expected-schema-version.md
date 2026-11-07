# T-007 expected 마이그레이션: classification.json에 schema_version

- 마일스톤: M1 (ADR 0004 결정 2, 순서 2)
- 상태: 진행 중

## 목표

`classification.json`의 첫 키를 `"schema_version": 1`(정수)로 한다. 예제 3종 `expected/classification.json`과 `docs/spec/classification.md`의 JSON 블록에 그 한 줄을 넣고, Core가 거기에 맞춘다.

## 수용 기준

1. expected 파일 3개와 스펙 JSON 블록(3.2 모양, 7.2~7.4)의 diff는 파일·블록마다 `  "schema_version": 1,` 한 줄 추가뿐이다. 스펙의 SHA-256 줄 3개는 새 값으로 바뀐다.
2. expected는 엔진 출력에서 복사하지 않는다. 그 한 줄만 넣는 스크립트로 넣고, `ClassificationJson.Render`가 그 바이트를 낸다.
3. 시작 전 main 통과 수는 830이다. 끝난 뒤 830 이상이고, 삭제·Skip된 테스트가 없다.
4. dotnet test가 ubuntu·windows CI에서 녹색이다.

## 폴더 범위

`examples/*/expected/classification.json`, `docs/spec/classification.md`, `src/Scorecard.Core/Classification/`, `tests/`

## 규칙 (ADR 0004 "사람 결정")

- expected 커밋 메시지는 "expected migration:"으로 시작한다. 그 커밋에는 expected 파일과 스펙의 해당 JSON 블록·SHA-256 줄만 넣는다. 코드는 다른 커밋에 넣는다.
- reviewer는 `git diff --stat`과 expected diff 전문을 PR에 붙인다.
- expected 변경이라 머지 전에 juhan이 직접 확인한다.
