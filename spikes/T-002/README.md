# T-002 스파이크 (머지 대상 아님)

네트워크 금지 검사를 소스 텍스트로 할지, 컴파일된 어셈블리의 메타데이터로 할지 비교한다.
결정론·원본 불변 도우미가 어떤 위반을 잡는지 가짜 대상으로 확인한다.
스펙: `docs/spec/guard-tests.md`.

## 실행

```
spikes/T-002/run-spike.sh <작업폴더> <로그폴더>
```

- 작업 폴더는 없거나 비어 있어야 한다. 스크립트는 아무것도 지우지 않는다.
- 저장소의 커밋된 HEAD를 작업 폴더에 clone해서 변형을 넣는다. 저장소의 `src/`, `tests/`는 건드리지 않는다.
- 도구 빌드 산출물(bin/obj)도 작업 폴더로 간다(`--artifacts-path`).
- 요약은 `<로그폴더>/summary.txt`. 절대 경로는 `<작업>`, `<저장소>`, `<홈>`으로 바꿔 적는다.
- 이 폴더의 `summary.txt`는 2026-11-06 실행(HEAD `6eaec0e`, SDK 10.0.114, Ubuntu 24.04, ext4, uid 0)의 요약이다. 약 1분 30초 걸렸다.

## 파일

| 파일 | 내용 |
| --- | --- |
| `GuardSpike/` | 콘솔 도구. `meta`(메타데이터 검사), `text`(텍스트 검사), `guard`(가짜 대상 실험), `hash`, `enumorder`, `readonly`, `sha` |
| `GuardSpike/Canary.cs` | 검사 목록의 항목마다 하나씩 금지 API를 참조하는 카나리아(N3 네임스페이스 중 WebSockets·Mail·NetworkInformation·Quic·Security는 없음). 호출하지 않는다 |
| `variants/V01~V18` | Core(또는 Cli)에 넣어 보는 변형 |
| `variants/X01` | 대상 0개 Theory |
| `variants/X02` | BannedApiAnalyzers 설정과 pragma 우회 |
| `variants/X03` | 테스트 프로젝트 안 카나리아, System.Reflection.Metadata 사용 |
| `run-spike.sh` | 단계 S0~S10 실행 |
| `summary.txt` | 위 실행의 요약 |

## 단계

| 단계 | 한 일 |
| --- | --- |
| S0 | 도구 빌드 |
| S1 | 변형 없는 clone. Release `-warnaserror` 빌드. Core·Cli 메타데이터 검사, 텍스트 검사, 테스트 출력 폴더 목록 |
| S2 | Core 변형 V01~V14, V16~V18. 변형마다 새 clone. `-warnaserror` 빌드 → 일반 빌드 → 메타데이터 검사 → 텍스트 검사(.cs만, 프로젝트 파일 포함). V12는 Debug도. V01은 테스트 프로젝트까지 빌드·실행 |
| S3 | 테스트 프로젝트에 Cli 참조 추가 → 빌드·테스트 → scorecard.dll 검사. V15(Cli에 HttpClient) 넣고 다시 검사 |
| S4 | 대상 0개 Theory(X01) 실행 |
| S4b | 테스트 프로젝트 안 카나리아(X03). `-warnaserror` 빌드, 기존 테스트, Scorecard.Tests.dll 검사 |
| S5 | 도구 자신(카나리아 포함) 검사 |
| S6 | 가짜 대상 15종 × 원본 불변 3방식 × 결정론 3조건 |
| S7 | `string.GetHashCode`를 두 프로세스에서 |
| S8 | 파일 열거 순서, 읽기 전용 속성 |
| S9 | `core.autocrlf=true` clone에서 TestData 바이트, 하위 `.gitattributes` 효과 |
| S10 | BannedApiAnalyzers 5.0.0으로 빌드 시 검사, pragma 우회 |

## 결과 요지

- 텍스트 검사는 V04(props 별칭)·V07(문자열 리플렉션)·V08(`p.Start()`)·V09(TcpClient)·V10(P/Invoke)·V13(제3자 SDK)·V16(시각·난수)을 놓쳤다. V05·V06(주석·문자열)은 오탐했다.
- 메타데이터 검사는 위 우회를 모두 잡았고(V16은 `--core`의 D1), 허용 변형 V14에서 오탐 0이었다. `#if` 안 코드는 그 구성으로 빌드될 때만 보인다(V11, V12).
- 원본 불변은 SHA-256만 보면 새 파일·같은 내용 다시 쓰기·임시 파일·시각 변경·옆 폴더 쓰기를 놓쳤다. 목록·크기·수정 시각까지 보면 모두 잡았다(S6).
- 결정론은 반복·다른 경로·de-DE 문화권 세 조건으로 시각·GUID·절대 경로·문화권 의존을 잡았다. 날짜 단위 시각(`DateTime.Today`), `string.GetHashCode`, 파일 열거 순서 의존은 못 잡았다(S6~S8).
- 자세한 표와 근거는 스펙 4절, 원문은 `summary.txt`.
