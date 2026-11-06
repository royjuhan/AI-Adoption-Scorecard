# T-003 스파이크 (머지 대상 아님)

case.yaml 파서를 만들기 전에 불확실한 것을 확인한다. 결과는 `docs/spec/case-schema.md`에 옮겼다.

## 실행

```
spikes/T-003/run-spike.sh <작업폴더> <로그폴더>
```

- 작업 폴더는 없거나 비어 있어야 한다. 저장소 밖 임시 폴더를 준다.
- 커밋된 HEAD를 작업 폴더에 clone해서 변형을 넣는다. 저장소의 `src/`, `tests/`는 건드리지 않는다.
- 아무것도 지우지 않는다. 네트워크는 NuGet 패키지 받기에만 쓴다.
- 요약은 `<로그폴더>/summary.txt`. 절대 경로는 `<작업>`, `<저장소>`, `<패키지>`, `<홈>`으로 바꿔 적는다. 마지막 실행 요약을 `summary.txt`에 복사해 두었다.

## 파일

| 경로 | 내용 |
| --- | --- |
| `CaseSpike/` | 스파이크 도구. `meta`(G1 검사기), `refs`(참조 목록), `users`(IL에서 금지 멤버를 부르는 네임스페이스), `parse`(후보 파서 실행), `probe`(메모리 안 실험), `timing`(YamlDotNet `Parser` 시간) |
| `core/A-reflection/` | 후보 A: `DeserializerBuilder`(리플렉션 역직렬화) 최소 코드 |
| `core/B-static/` | 후보 B: 정적 생성기(`Vecc.YamlDotNet.Analyzers.StaticGenerator`) 최소 코드 |
| `core/C-events/` | 후보 C: 저수준 `Parser` 이벤트 + 손으로 쓴 스키마 검사. 스펙의 오류 줄은 이 코드의 출력이다 |
| `samples/` | 합성 case.yaml 13종. 실제 업무·값 없음 |
| `tests-variant/` | G2·G3 등록 모양(테스트 쪽 어댑터), guard 샘플 2종, Y1 카나리아 모양(`y1/`) |
| `patches/empty-output.patch` | G2 "출력 파일 없음" 규칙과 가짜 대상 2종, 테스트 1개 |

`CaseSpike`는 `tests/Scorecard.Tests/Guards/MetadataScanner.cs`를 링크로 컴파일한다(읽기만).

## 단계

| 단계 | 한 일 |
| --- | --- |
| S0 | 도구 빌드 (`-warnaserror`, 산출물은 작업 폴더) |
| S1 | YamlDotNet 18.1.0 nuspec(라이선스·의존성), `YamlDotNet.dll`의 참조·G1 항목·그 항목을 부르는 네임스페이스 |
| S2 | clone 4개: 기준선, A, B, C. Core에 패키지와 코드를 넣고 Release `-warnaserror` 빌드 → Core 메타데이터 → G1 테스트 → N6 허용 목록에 `YamlDotNet` → G1 테스트. C는 전체 테스트, Cli 검사, `dotnet format` |
| S3 | `samples/` 13종과 guard 샘플 2종을 후보 C로 파싱. 오류 줄과 결과 해시 |
| S4 | 결정론: 프로세스 두 번, de-DE, 다른 경로 사본. 메모리 안 실험(인코딩, 줄바꿈, 문화권 6종, 열 번호, 깊이, 별칭 폭탄, NFC/NFD, 파일 없음, yes/no, 값 형식, 구조 경계, A 비교). `Parser` 시간 |
| S5 | C clone에 G2·G3 어댑터·샘플·빈 출력 패치 → G2·G3 테스트, 전체 테스트 |
| S6 | 어댑터가 파일을 쓰지 않게 바꿔 새 규칙이 잡는지 |
| S7 | 파서가 오류 줄에 절대 경로를 쓰게 바꿔 G2가 잡는지 |
| S8 | 테스트 프로젝트가 YamlDotNet Serialization·RepresentationModel 타입을 참조할 수 있는지(Y1 카나리아) |
