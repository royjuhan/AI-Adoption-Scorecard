# T-008 스파이크 (머지 대상 아님)

`human_signoff` 질문, 트리 맨 앞 `SIGNOFF`, 추정 답 목록 `inferred`(`docs/spec/human-signoff.md`)의 후보 코드와 실험. 머지 전에 이 폴더를 뺀다.

- `core/Cases/`, `core/Classification/`: Core에 넣을 후보 6파일(기존 파일을 바꾼 것). 스파이크는 clone의 `src/Scorecard.Core/`에 덮어써 빌드한다.
- `tests-variant/`: clone에서만 바꿔 보는 기존 테스트 10파일. 바꾼 까닭은 `human-signoff.md` 4절.
- `SignoffSpike/`: 실험 도구. `classify`, `metrics`, `rows`, `spec-rows`, `errors`, `probe`.
- `add-signoff.py`: `answers.yaml`·`case.yaml`의 `    answers: {…}` 줄 끝에 `, human_signoff: no`(또는 고른 단계는 `yes`)를 더한다.
- `fill-blocks.py`: 바꾼 예제 파일로 classification.md 7.2~7.4 `answers.yaml` 블록, synthetic-examples.md 7.2~7.4 `case.yaml` 블록·SHA-256 줄을 채운다.
- `instructions-variant.py`: clone의 지시문에 `human-signoff.md` 6절 제안 문구를 넣는다(기존 테스트와 맞는지 보려고).
- `extract-block.py`: 스펙 코드 블록을 파일로 꺼낸다(테스트의 `SpecDocument.CodeBlocks`와 같은 규칙).
- `mutate.py`: 음성 대조 변형 4종.
- `run-spike.sh <작업폴더> <로그폴더>`: 전체 단계. 저장소의 `src/`, `tests/`, `examples/`, 지시문은 건드리지 않는다.
- `summary.txt`: 마지막 실행 요약(절대 경로 없음).
