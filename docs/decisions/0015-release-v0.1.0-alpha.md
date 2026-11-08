# 0015. 릴리스 v0.1.0-alpha

- 날짜: 2026-11-08
- 상태: 기록(release). 새 결정은 없다. ADR 0010 B-12(M2-4 뒤 v0.1.0-alpha 태그)를 따른다. 태그는 머지 뒤 리드가 단다.
- 관련: ADR 0004(종료 코드, G2), ADR 0010, `CHANGELOG.md`, 지시문 10절 M2 행.

## 산출물

- 바탕 커밋: `188916de30dd5f4e14cd0932db0d7db54f1a4029`(PR #29 head, M2 전부). 이 PR은 문서만 더한다. `src/`는 같다.
- 명령: `dotnet publish src/Scorecard.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o <저장소 밖 폴더>`
- 환경: Linux 컨테이너, .NET SDK 10.0.114. win-x64 런타임 팩은 프록시로 받아졌다(성공).

| 산출물 | 크기(바이트) | SHA-256 |
| --- | --- | --- |
| `scorecard.exe` (win-x64) | 74346155 | `123340a414d92eb1583db67761186d00cff5902a4b00a66c3f2d3a08d13ce6ef` |
| `scorecard` (linux-x64, 확인용) | 74450353 | `2cf1d63d47fb371fbf285d954f29e0fe05595e7dba02ff025630ff25e3417b0c` |

- 같은 커밋·같은 폴더에서 win-x64를 한 번 더 publish하니 SHA-256이 같았다. 다른 기계·다른 경로에서 같은지는 보지 않았다.
- 출력 폴더에는 `scorecard.pdb`, `Scorecard.Core.pdb`도 생긴다. 배포에는 exe만 쓴다.
- 바이너리는 저장소에 넣지 않는다.

## 실행 확인

- `scorecard.exe`는 이 환경(Linux)에서 돌릴 수 없다. Windows 실기 실행은 아직 하지 않았다.
- 대신 같은 소스의 linux-x64 단일 파일로 `examples/table-compare` 사본에서 `start --answers` → `check` → `open`(모두 `--json`)을 돌렸다. 종료 코드 모두 0. `classification.json`·`metrics.json`·`judgment.json`은 `expected/`와, `scorecard.html`은 골든 `tests/Scorecard.Tests/TestData/report/table-compare/scorecard.html`과 바이트까지 같았다.

## 네트워크·프로세스 검사

- 대상: 우리 어셈블리 `Scorecard.Core.dll`과 `scorecard.dll`(publish가 묶기 전 IL, `bin/Release/net10.0/…`). 런타임 자체(System.Net.Http 등)는 self-contained exe에 같이 묶이지만 우리 코드가 부르지 않는다.
- 방법 1(G1 가드, `dotnet test`에 포함): `tests/Scorecard.Tests/Guards/MetadataScanner.cs`가 PEReader로 메타데이터 표(AssemblyRef, TypeRef, MemberRef, PinvokeImpl, ModuleRef)를 읽는다. `System.Net*` 참조, `System.Diagnostics.Process`, `HttpClient`, `Socket`, `WebClient` 등을 막는다. `NetworkGuardTests`의 Core·Cli 테스트가 통과했다.
- 방법 2(이번 손 검사): 두 dll의 메타데이터 문자열 힙을 `grep -a`로 찾았다. 패턴 `HttpClient|HttpMessageHandler|WebClient|WebRequest|Socket*|TcpClient|UdpClient|Dns|System.Net.*|Process*|Anthropic|OpenAI`. 걸린 것은 `ProcessMinutes`(판정 시간 입력의 속성 이름, `JudgmentTimes`) 하나뿐이다. `System.Diagnostics` 이름은 컴파일러가 넣는 `DebuggableAttribute`·`DebuggerHiddenAttribute`·`Conditional`이다.
- 방법 3: `src/`의 `.cs`·`.csproj`에서 같은 패턴을 찾았다. `obj/`의 SDK 생성 파일 `GlobalUsings.g.cs`의 `global using System.Net.Http;` 말고는 없다. 이 using은 타입 참조를 만들지 않는다(방법 1·2에서 참조 0).
- 결과: 우리 코드에 네트워크·외부 프로세스 호출 없음.

## G2 게이트(지시문 10절 M2 행)

- 예제 end-to-end(`start --answers` → `open` → `build`, 사람용·`--json`): T-021 테스트로 통과.
- 명령 넷 `--json` 결정론: G2 대상 테스트로 통과.
- 샘플 스코어카드(HTML) README 게시: `docs/samples/scorecard-table-compare.html`(골든 HTML 사본)을 README.md·README.ko.md에 한 줄로 걸었다.
- 비공개 사례 1건을 같은 exe로 로컬 실행해 요약을 README "사례"에 추가: 하지 않았다. juhan이 실제 PC에서 비공개 자료로 해야 한다. 그 전까지 이 항목은 열려 있다.

## 남은 위험

- Windows 실기에서 exe를 돌려 보지 않았다. `--out` 대소문자 경로 문제(`docs/LATER.md` M2-1)도 Windows에서 확인해야 한다.
- 샘플 HTML은 골든 사본이다. 골든이 바뀌면 손으로 다시 복사한다.
