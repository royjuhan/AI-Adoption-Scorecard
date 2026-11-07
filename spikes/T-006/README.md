# T-006 스파이크 (머지 대상 아님)

대조 엔진(`docs/spec/metrics.md`)의 후보 코드와 실험. 머지 전에 이 폴더를 뺀다.

- `core/Comparison/`: Core에 넣을 후보 6파일. 스파이크는 clone의 `src/Scorecard.Core/Comparison/`에 복사해 빌드한다.
- `CompareSpike/`: 실험 도구. `run`(예제 → metrics.json), `probe`(메모리 안 실험), `spec`(스펙 표·블록 대조), `guard-samples`, `refs`.
- `xcheck/compare.py`: 스펙 글만 보고 따로 짠 Python 구현. 예제 3종 바이트가 같은지 본다.
- `samples/guard/compare-invalid/`: G2·G3 오류 표본 후보.
- `tests-variant/`: clone에서만 바꿔 보는 테스트 파일(GuardTargets, CoreGuardTargetTests).
- `fill-blocks.py`: 스파이크 출력으로 스펙 7.2~7.4 블록·SHA-256 줄과 6.4 오류 블록을 채운다.
- `run-spike.sh <작업폴더> <로그폴더>`: 전체 단계(S0~S8). 저장소의 `src/`, `tests/`, `examples/`는 건드리지 않는다.
- `summary.txt`: 마지막 실행 요약(절대 경로 없음).
