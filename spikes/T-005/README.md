# T-005 스파이크 (머지 대상 아님)

합성 예제 생성기를 구현하기 전에 불확실한 것을 확인한다. 결과는 `docs/spec/synthetic-examples.md`에 옮겼다.

## 실행

```
spikes/T-005/run-spike.sh <작업폴더> <로그폴더>
```

- 작업 폴더는 없거나 비어 있어야 한다. 저장소 밖 임시 폴더를 준다.
- 커밋된 HEAD를 작업 폴더에 clone하고 `spikes/T-005/`와 작업 트리의 `docs/spec/synthetic-examples.md`를 복사해 변형을 넣는다. 저장소의 `src/`, `tests/`, `examples/`는 건드리지 않는다. 빌드·커밋도 clone 안에서만 한다.
- 아무것도 지우지 않는다. 네트워크는 NuGet 패키지 받기에만 쓴다.
- 요약은 `<로그폴더>/summary.txt`. 절대 경로는 `<작업>`, `<로그>`, `<저장소>`, `<패키지>`, `<홈>`으로 바꿔 적는다. 마지막 실행 요약을 `summary.txt`에 복사해 두었다.
- 스펙 7절 블록을 다시 채울 때: `python3 -I spikes/T-005/fill-blocks.py docs/spec/synthetic-examples.md <작업>/ex`. 그다음 스파이크를 다시 돌려 S10을 확인한다.

## 파일

| 경로 | 내용 |
| --- | --- |
| `core/Synthetic/` | 후보 생성기(`SyntheticGenerator`), 수열(`SplitMix64`), CSV 바이트(`SyntheticCsv`), 모델. clone의 `src/Scorecard.Core/Synthetic/`에 복사된다 |
| `SynthSpike/` | 스파이크 도구. `gen`(스펙 표 + case.yaml → CSV), `oracle`(대조 셈 3종), `case-check`, `blocks`, `structure`, `scan`(패턴 검사), `probe`(메모리 안 실험), `refs`(dll 참조 목록) |
| `xcheck/generate.py` | Python 독립 구현. 스펙 4.2·4.5 순서로 같은 바이트가 나오는지 본다 |
| `drafts/` | 예제 3종 `case.yaml`·`intake.md` 초안. 모두 합성 |
| `tests-variant/GuardTargets.cs` | `GuardTargets`에 `synthetic-case-valid`를 더한 모양 |
| `tests-variant/ExamplePatternTests.cs` | 스펙 4.10 패턴 표로 `examples/`를 검사하는 테스트 모양 |
| `hooks/pre-commit` | 패턴 테스트만 부르는 git 훅(S9 비교용. 제안 아님) |
| `fill-blocks.py` | 생성 파일 바이트로 스펙 7.2~7.4 블록·SHA-256 줄을 채운다 |

## 단계

| 단계 | 한 일 |
| --- | --- |
| S0 | clone, 스파이크 파일·스펙 복사 |
| S1 | 기준선 Release `-warnaserror` 빌드, 전체 테스트 |
| S2 | Core에 후보 4파일 → 빌드 → G1 → `dotnet format --verify-no-changes` → 전체 테스트 |
| S3 | 도구 빌드, Core 참조 목록 비교, 예제 조립·생성·확인, 대조 셈, 바이트 확인, 결정론(다른 프로세스·경로·de-DE·Python) |
| S4 | `PROBE`: 수열, 문화권 7종, 반복, 대안 P1~P4, 시드 0~1999, 검사, 패턴, CSV |
| S5 | `GuardTargets` 변형 → G2·G3 → 전체 테스트 |
| S6 | 문화권 음성 대조 → G1 → G2 |
| S7 | 예제 파일을 clone에 넣고 전체 테스트, `core.autocrlf=true` clone에서 바이트 |
| S8 | 패턴 검사(도구, 테스트 변형, 값 심기·되돌리기) |
| S9 | git 훅 비교(심은 값, 정상, `--no-verify`, 훅 끔) |
| S10 | 스펙 7절 블록·SHA-256, 4.7 구조 표 대조 |
