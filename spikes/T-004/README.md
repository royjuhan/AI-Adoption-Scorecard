# T-004 스파이크 (머지 대상 아님)

분류 트리를 구현하기 전에 불확실한 것을 확인한다. 결과는 `docs/spec/classification.md`에 옮겼다.

## 실행

```
spikes/T-004/run-spike.sh <작업폴더> <로그폴더>
```

- 작업 폴더는 없거나 비어 있어야 한다. 저장소 밖 임시 폴더를 준다.
- 커밋된 HEAD를 작업 폴더에 clone하고 `spikes/T-004/`를 복사해 변형을 넣는다. 저장소의 `src/`, `tests/`, `examples/`는 건드리지 않는다. 빌드도 clone 안에서만 한다.
- 아무것도 지우지 않는다. 네트워크는 NuGet 패키지 받기에만 쓴다.
- 요약은 `<로그폴더>/summary.txt`. 절대 경로는 `<작업>`, `<로그>`, `<저장소>`, `<패키지>`, `<홈>`으로 바꿔 적는다. 마지막 실행 요약을 `summary.txt`에 복사해 두었다.
- S8은 작업 트리의 `docs/spec/classification.md`를 읽기만 한다.

## 파일

| 경로 | 내용 |
| --- | --- |
| `core/Classification/` | 후보 분류기(`StepClassifier`), 결과 모델, `ClassificationJson`(Utf8JsonWriter). clone의 `src/Scorecard.Core/Classification/`에 복사된다 |
| `ClassifySpike/` | 스파이크 도구. `classify`(answers.yaml → classification.json + 요약), `probe`(메모리 안 실험), `refs`(dll 참조 목록), `spec`(스펙 표와 후보 대조) |
| `samples/` | 예제 초안 `answers.yaml` 3종과 table-compare 5단계 변형(행 모델 B 비교용). 모두 합성 |
| `tests-variant/GuardTargets.cs` | `GuardTargets`에 `classifier-case-valid`를 더한 모양 |
| `tests-variant/AbsPathGuardTargets.cs` | 음성 대조: 분류 출력에 절대 경로를 섞는다 |

## 단계

| 단계 | 한 일 |
| --- | --- |
| S0 | clone, 스파이크 파일 복사 |
| S1 | 기준선 Release `-warnaserror` 빌드, 전체 테스트 |
| S2 | Core에 후보 3파일 → 빌드 → G1 → `dotnet format --verify-no-changes` → 전체 테스트 |
| S3 | 도구 빌드, Core 참조 목록(기준선·후보), 예제 초안·변형·12.1 합성 사본·guard `case-valid` 분류, 결정론(다른 프로세스·경로·de-DE) |
| S4 | `PROBE`: 분기, 순서, 기본값 표시, 문장, AI 과잉 후보, 이스케이프, 문화권, 줄바꿈 |
| S5 | `GuardTargets` 변형 → G2·G3 → 전체 테스트 |
| S6 | 절대 경로 음성 대조 → G2 |
| S7 | `core.autocrlf=true` clone에서 예제 파일 바이트(`.gitattributes` 유무) |
| S8 | 스펙 표 4개(조건, 문장 틀, 문구, 읽은 답)와 후보 대조 |
