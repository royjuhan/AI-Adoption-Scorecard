# guard-tests (T-002 스펙)

- 상태: 승인됨 (2026-11-06, juhan "추천대로"). 본문과 맨 끝 "사람 승인 결과"가 다르면 승인 결과가 우선한다.
- 작업 카드: `docs/tasks/T-002-guard-tests.md`
- 작성: prototyper, 2026-11-06
- 확인 대상 커밋: `6eaec0e`
- 확인 환경: Ubuntu 24.04.5 x64, ext4, root 계정(uid 0), .NET SDK 10.0.114, xUnit 2.9.3
- 스파이크: `spikes/T-002/` (머지 대상 아님, main에는 없다). 파일은 커밋 `8bc8885`에서 볼 수 있다. 실행은 `spikes/T-002/run-spike.sh <작업폴더> <로그폴더>`. 요약 원문은 `spikes/T-002/summary.txt`. 이 문서의 `spikes/T-002/…` 경로는 모두 그 커밋 기준이다.

## 0. 표기

- `[읽음]`: 커밋된 파일을 읽어서 확인한 사실.
- `[S0]`~`[S10]`: 스파이크 단계. `[V01]`~`[V18]`: S2·S3의 변형. 목록은 9절.
- **미확인**: 실험하지 않았거나 이 환경에서 확인할 수 없는 것.
- 검사 이름: **G1** 네트워크 금지, **G2** 결정론, **G3** 원본 불변. G1의 규칙 ID는 N1~N6, D1.

## 1. 범위

카드 수용 기준 1~3을 다룬다.

- G1: `src/`의 컴파일 결과에 네트워크·외부 프로세스·LLM 참조가 없다.
- G2: 같은 입력을 여러 번 처리하면 같은 출력이 나온다.
- G3: 처리 전후로 입력이 그대로다.

범위 밖:

- Core 처리 기능. T-002는 검사 도구와 그 자기 검사만 만든다(4.4).
- 골든 테스트(`examples/*/expected/`). T-004부터.
- 제3자 패키지 내부 코드, publish 산출물 검사(release 에이전트).
- pre-commit 검사(지시문 2절 10).
- `src/` csproj, CI 변경. 승인 질문 11.

## 2. 입력

| 검사 | 입력 |
| --- | --- |
| G1 | 테스트 출력 폴더(`AppContext.BaseDirectory`)의 `Scorecard.Core.dll`, `scorecard.dll` |
| G1 자기 검사 | 같은 폴더의 `Scorecard.Tests.dll` (카나리아 포함, 4.3) |
| G2·G3 | 처리 대상(`GuardTarget`)과 합성 샘플 `tests/Scorecard.Tests/TestData/guard/<샘플>/` |

- 테스트 프로젝트는 Core를 참조한다 [읽음]. 그래서 테스트 출력 폴더에 `Scorecard.Core.dll`이 있다 [S1]. 이 복사본은 Core 변경을 반영한다 [V01].
- 테스트 프로젝트는 지금 Cli를 참조하지 않는다 [읽음]. 테스트 출력 폴더에 `scorecard.dll`이 없다 [S1].
- 테스트 프로젝트에 Cli 참조를 더하면 `scorecard`, `scorecard.dll`, `scorecard.deps.json`, `scorecard.runtimeconfig.json`이 테스트 출력 폴더에 온다. `-warnaserror` 빌드와 기존 테스트 20개가 통과한다 [S3]. → 승인 질문 4.
- 지금 상태: Core는 `System.Runtime` 하나만 참조한다. Cli는 `System.Runtime`, `System.Console`. 둘 다 금지 참조 0건 [S1].

## 3. 출력

xUnit 테스트 결과. 실패 메시지는 규칙 ID와 찾은 참조를 한 줄에 하나씩 적는다(6.1). G2·G3는 조건 이름과 다른 항목을 적는다(6.4).

## 4. 규칙

### 4.1 G1 방법: 메타데이터 검사

두 방법을 같은 변형에 돌렸다 [S2]. 모두 Release 빌드, Core에 파일 하나를 더한 clone.

| 변형 | 내용 | 텍스트 (.cs) | 텍스트 (+.csproj/.props/.targets) | 메타데이터 |
| --- | --- | --- | --- | --- |
| V01 | `using` + `new HttpClient()` | 잡음 | 잡음 | 잡음 (N1, N3) |
| V02 | 전체 이름 `System.Net.Http.HttpClient` | 잡음 | 잡음 | 잡음 (N1, N3) |
| V03 | .cs 안 `using Web = ...HttpClient` | 잡음 (별칭 줄) | 잡음 | 잡음 (N1, N3) |
| V04 | `Directory.Build.props`의 `<Using Alias="Web">` | **놓침** | 잡음 (.props 줄) | 잡음 (N1, N3) |
| V05 | 주석에만 금지 이름 | **오탐** 4건 | 오탐 | 0건 (맞음) |
| V06 | 문자열에만 금지 이름 | **오탐** | 오탐 | 0건 (맞음) |
| V07 | `Type.GetType("...Http" + "Client, ...")` + `Activator.CreateInstance` | **놓침** | 놓침 | 잡음 (N5) |
| V08 | `new Process()` 후 `p.Start()` | 코드는 **놓침** (주석 줄만 걸림) | 같음 | 잡음 (N2, N3) |
| V09 | `TcpClient` (카드 목록에 없음) | **놓침** | 놓침 | 잡음 (N1, N3) |
| V10 | `DllImport("libc", EntryPoint = "socket")` | **놓침** | 놓침 | 잡음 (N4) |
| V11 | `#if NEVER_DEFINED` 안에만 | 걸림 | 걸림 | 0건 (컴파일되지 않음) |
| V12 | `#if DEBUG` 안에만 | 걸림 | 걸림 | Release 0건, Debug 잡음 |
| V13 | 가짜 LLM SDK(제3자 어셈블리) 호출 | **놓침** | 놓침 | 잡음 (N6). 허용 목록에 넣으면 0건 |
| V14 | 허용 API: `WebUtility`, `Uri`, `SHA256`, `new T()`, `File`, LINQ, `Regex`, `JsonSerializer` | 0건 | 0건 | 0건 (오탐 없음) |
| V16 | `DateTime.Now`, `Random`, `Guid.NewGuid` | 놓침 | 놓침 | Core 검사(D1)에서 잡음 |
| V17 | `new WebClient()` | 잡음 | 잡음 | 잡음 (N1, N3). 그리고 `-warnaserror` 빌드가 SYSLIB0014로 먼저 실패 |
| V18 | `LibraryImport` | 0건 | 0건 | 검사 전에 빌드 실패 (SYSLIB1062, CS0227) |

- 텍스트 검사는 `\bHttpClient\b`, `\bSocket\b`, `\bWebClient\b`, `\bProcess\.Start\b`, LLM 이름(`OpenAI`, `Anthropic`, API 주소)을 정규식으로 찾았다.
- 텍스트 검사에서 `Socket을`은 `\bSocket\b`에 걸리지 않았다 [V06]. .NET 정규식의 `\b`가 한글을 단어 문자로 보기 때문이다. 주석의 오탐 여부가 조사 띄어쓰기에 따라 달라진다.
- 지금 테스트 20개는 Core에 `HttpClient`가 있어도 모두 통과한다 [V01]. 즉 지금은 이 위반을 잡는 테스트가 없다.

결정(제안): G1은 메타데이터 검사로 한다. 텍스트 검사는 테스트로 두지 않는다. → 승인 질문 1.

메타데이터 검사의 한계:

- 테스트가 도는 구성의 빌드만 본다. `#if DEBUG` 안 코드는 Release 검사에서 보이지 않는다 [V12]. CI는 Release, 로컬 `dotnet test` 기본값은 Debug다 [읽음 ci.yml]. 배포도 Release다(지시문 4절). 다른 `DefineConstants`로 publish하면 그 빌드는 검사 밖이다(**미확인**, 실험 안 함).
- 제3자 어셈블리 내부는 보지 않는다. N6 허용 목록에 사람이 확인한 것만 넣는다.
- 리플렉션 우회는 N5 목록의 멤버만 잡는다. 허용된 제3자 라이브러리를 거치는 우회 등 목록 밖 경로는 못 잡는다. reviewer 검사는 그대로 둔다.
- `string.GetHashCode()`는 메타데이터에 `System.Object::GetHashCode`로 남는다 [S5]. 문자열 해시만 골라 막을 수 없다.

### 4.2 G1 규칙

검사 대상과 적용 규칙:

| 어셈블리 | 위치 | 규칙 |
| --- | --- | --- |
| `Scorecard.Core.dll` | 테스트 출력 폴더 | N1~N6, D1 |
| `scorecard.dll` | 테스트 출력 폴더 (Cli 참조 필요, 승인 질문 4) | N1~N6 |

- 파일이 없으면 실패로 본다. 빈 통과를 막는다.
- 어셈블리를 로드하지 않는다. `PEReader`로 파일의 메타데이터 표만 읽는다. 스파이크 검사기가 이 방식으로 S1~S5의 모든 dll을 읽었다.
- `System.Reflection.Metadata`는 공유 프레임워크에 있다. 테스트 프로젝트에서 패키지 추가 없이 빌드된다 [S4b].
- 이름은 서수(대소문자 구분)로 비교한다.

| ID | 메타데이터 표 | 금지 조건 | 확인 |
| --- | --- | --- | --- |
| N1 | AssemblyRef | 이름이 `System.Net`이거나 `System.Net.`으로 시작 | [V01][V09][V17][S5]. `WebUtility`, `Uri`는 `System.Runtime`에 있어 걸리지 않는다 [V14][S5] |
| N2 | AssemblyRef | `System.Diagnostics.Process`, `System.Runtime.Loader` | [V08][S5] |
| N3 | TypeRef | 타입 `System.Net.Http.HttpClient`, `System.Net.Sockets.Socket`, `System.Net.WebClient`, `System.Diagnostics.Process`, `System.Diagnostics.ProcessStartInfo`, `System.Runtime.InteropServices.NativeLibrary`. 네임스페이스(같거나 하위) `System.Net.Http`, `System.Net.Sockets`, `System.Net.WebSockets`, `System.Net.Mail`, `System.Net.NetworkInformation`, `System.Net.Quic`, `System.Net.Security`, `System.Runtime.Loader` | 타입 6개와 `System.Net.Http`·`System.Net.Sockets`·`System.Runtime.Loader` 네임스페이스는 [S5]. 나머지 네임스페이스 5개는 **미확인**(카나리아에 없음) |
| N4 | MethodDef, ModuleRef | P/Invoke 메서드(`PinvokeImpl` 플래그)가 있거나 ModuleRef가 1개 이상 | `DllImport` [V10][S5], `LibraryImport` [S5] |
| N5 | MemberRef | `System.Type::GetType`(정적만, 인스턴스 `GetType()` 제외), `::GetTypeFromProgID`, `::GetTypeFromCLSID`, `System.Reflection.Assembly::Load`, `::LoadFrom`, `::LoadFile`, `::UnsafeLoadFrom`, `::GetType`, `System.Reflection.Module::GetType`, `System.AppDomain::Load`, `System.Activator::CreateInstance`(제네릭 제외), `System.Runtime.InteropServices.Marshal::GetDelegateForFunctionPointer` | 전부 [S5]. 제네릭 `CreateInstance<T>`(`new T()`)는 걸리지 않는다 [V14] |
| N6 | AssemblyRef | 런타임 폴더(`RuntimeEnvironment.GetRuntimeDirectory()`)에 같은 이름의 dll이 없으면 허용 목록에 있어야 한다. 허용 목록은 지금 비어 있다 | [V13] |
| D1 | TypeRef, MemberRef (Core만) | 타입 `System.Random`, `System.Security.Cryptography.RandomNumberGenerator`, `System.Diagnostics.Stopwatch`. 멤버 `System.DateTime::get_Now`, `::get_UtcNow`, `::get_Today`, `System.DateTimeOffset::get_Now`, `::get_UtcNow`, `System.Guid::NewGuid`, `::CreateVersion7`, `System.Environment::get_TickCount`, `::get_TickCount64`, `::get_MachineName`, `::get_UserName`, `::GetEnvironmentVariable`, `::GetEnvironmentVariables`, `System.TimeProvider::get_System` | 전부 [S5], Core 빌드에서 [V16] |

- N6의 프레임워크 판정: `System.Runtime`, `System.Console`, `System.Linq`, `System.Security.Cryptography`, `System.Text.Json`, `System.Text.RegularExpressions`, `System.Reflection.Metadata`는 프레임워크로 판정됐다 [S1][V14][S4b]. 가짜 SDK `FakeLlm`과 `xunit.core`는 아니었다 [V13][S4b]. Windows 런타임 폴더에서 같은 결과인지는 **미확인**.
- LLM API는 이름 목록을 두지 않는다. 직접 HTTP 호출은 N1·N3가, SDK 패키지는 N6가 잡는다 [V01][V13]. SDK가 허용 목록에 들어가면 못 잡는다. 그래서 허용 목록 추가는 사람이 확인한다. → 승인 질문 3.
- D1은 지시문 2절 3(난수·현재시각 금지)을 정적으로 막는다. G2가 날짜 단위 시각을 못 잡기 때문이다(4.5). 환경 변수 읽기도 넣었다. 환경 변수로 API 키를 읽는 경로를 막으려는 것이다(`.claude/rules/core-no-network.md` "외부 API 키" [읽음]). 출력이 실행 환경에 따라 달라지는 것도 막는다. → 승인 질문 5.
- 빌드가 먼저 막는 것: `WebClient`는 SYSLIB0014 경고 [V17], `Type.GetTypeFromProgID`·`GetTypeFromCLSID`는 CA1416 경고 [S0]. CI는 `-warnaserror`라 이 경고는 빌드 실패가 된다 [읽음 ci.yml]. `-warnaserror`가 이런 경고를 오류로 바꾸는 것은 SYSLIB0014 [V17]와 분석기 경고 RS0030 [S10]에서 확인했다. CA1416 자체를 `-warnaserror`로 빌드하지는 않았다. `LibraryImport`는 `AllowUnsafeBlocks` 없이는 빌드 오류다 [V18]. `DllImport`는 csproj 변경 없이 `-warnaserror`도 통과한다 [V10]. 그래서 N4가 필요하다.

### 4.3 G1 자기 검사 (카나리아)

- 테스트 어셈블리에 금지 API를 참조하는 카나리아 클래스를 둔다. 아무도 호출하지 않는다.
- 같은 검사기로 `Scorecard.Tests.dll`을 읽어, 카나리아가 기대 규칙마다 잡히는지 본다. 검사기가 망가져 늘 0건을 내는 경우를 막는다.
- 확인 [S4b]: 테스트 프로젝트에 카나리아를 넣어도 `-warnaserror` 빌드와 기존 테스트 20개가 통과한다. 검사기는 카나리아의 N1·N2·N3·N4·D1 항목을 잡는다. Core 복사본은 0건 그대로다.
- `Scorecard.Tests.dll`에는 `xunit.core` 등 비프레임워크 참조도 있어 N6가 걸린다 [S4b]. 자기 검사는 "기대 항목이 모두 들어 있는가"만 본다. 0건 여부는 보지 않는다.
- 카나리아의 `WebClient`는 `#pragma warning disable SYSLIB0014`가 있어야 빌드된다 [S4b][V17]. `LibraryImport`는 `AllowUnsafeBlocks`가 필요하므로 카나리아에는 `DllImport`만 쓴다 [V18].
- → 승인 질문 6.

### 4.4 G2·G3가 무엇을 실행하나

지금 Core에 처리 기능이 없다 [읽음: `src/Scorecard.Core`에 .cs 0개]. 선택지:

| 안 | 내용 | 확인된 장단점 |
| --- | --- | --- |
| A | 도우미(계약)와 가짜 대상 자기 검사만 만든다. 실제 Core 대상은 T-003부터 등록 | 가짜 대상으로 도우미의 탐지력을 보일 수 있다 [S6]. T-003 전까지 Core는 검사받지 않는다(검사할 기능이 없다) |
| B | Core에 최소 처리 기능(예: 입력 해시 계산)을 지금 넣는다 | 카드 폴더 범위(`tests/`) 밖이다 [읽음 카드]. 카드에 없는 기능이다(지시문 13절) |
| C | CLI 프로세스를 실행해 검사 | Cli에 명령이 없다 [읽음 Program.cs]. 기존 테스트는 외부 프로세스를 부르지 않는다 [읽음 RepoRoot.cs] |
| D | 빈 대상 목록으로 Theory를 만든다 | xUnit이 `No data found`로 실패한다 [S4] |

결정(제안): A. → 승인 질문 7.

- 실제 대상 목록(`GuardTargets`)과 그 Theory는 첫 Core 처리 기능 카드에서 만든다. 대상이 0개인 Theory는 실패하기 때문이다 [S4].
- 메모리 결과만 내는 API(예: 파서)는 테스트 쪽 어댑터가 결과를 출력 폴더에 파일로 쓴다. 직렬화 방법과 그 결정론은 해당 카드에서 정한다(**미확인**).

### 4.5 G2·G3 계약

처리 대상:

```csharp
// 처리 대상 하나. inputDir을 읽고 outputDir에 쓴다.
// outputDir은 아직 없는 경로다. 필요하면 대상이 만든다.
internal sealed record GuardTarget(string Name, string Sample, Action<string, string> Run);
```

작업 폴더 준비 (G2·G3 공통):

1. `RepoRoot.Combine("tests/Scorecard.Tests/TestData/guard/<Sample>")`의 파일을 임시 작업 폴더 `<work>/input/`에 복사한다. 저장소의 샘플은 직접 넘기지 않는다.
2. 작업 폴더 이름은 `Path.GetTempPath()` 아래 무작위 이름이다. 테스트 쪽 난수라 판정과 무관하다.
3. 복사 뒤 `<work>` 아래 모든 파일·폴더의 수정 시각을 `2000-01-01T00:00:00Z`로 맞춘다. 깊은 폴더부터.
4. 출력 폴더는 `<work>/out`(G3) 또는 `<work>/out1`, `out2`…(G2).
5. 끝나면 작업 폴더를 지운다. 읽기 전용 속성은 쓰지 않는다.

스냅숏: 폴더 아래 항목 목록. 항목은 상대 경로('/' 구분, 서수 정렬), 종류(파일·폴더), 크기, SHA-256(소문자 16진수), 수정 시각(UTC).

#### G3 원본 불변

- 실행 전후 `<work>`의 스냅숏을 비교한다. 출력 폴더는 뺀다. 다섯 항목 모두 같아야 한다.
- 카드 문구("SHA-256이 같다")보다 엄격하다. 이유 [S6]:

| 가짜 대상 | SHA-256만 | +목록·크기 | +수정 시각 |
| --- | --- | --- | --- |
| good (정상) | 통과 | 통과 | 통과 |
| input-append (입력에 덧붙임) | 잡음 | 잡음 | 잡음 |
| input-delete (입력 삭제) | 잡음 | 잡음 | 잡음 |
| input-new-file (입력 폴더에 새 파일) | **놓침** | 잡음 | 잡음 |
| write-sibling (출력 폴더 밖 작업 폴더에 쓰기) | **놓침** | 잡음 | 잡음 |
| input-rewrite-same (같은 내용으로 다시 쓰기) | **놓침** | **놓침** | 잡음 |
| input-temp-file (입력 폴더에 임시 파일 만들고 지움) | **놓침** | **놓침** | 잡음 (폴더 수정 시각) |
| input-touch-mtime (수정 시각만 바꿈) | **놓침** | **놓침** | 잡음 |

- 읽기 전용 속성으로 막는 방법은 쓰지 않는다. root 계정에서는 읽기 전용(모드 0444) 파일에 덧붙이기가 성공했다 [S8]. 일반 계정·Windows에서 막히는지는 **미확인**.
- → 승인 질문 8.

#### G2 결정론

같은 대상을 네 번 돌리고 R1과 비교한다. 비교는 경로·종류·크기·SHA-256. 수정 시각은 보지 않는다.

| 실행 | 작업 폴더 | 입력 복사 순서 | 문화권(`CurrentCulture`, `CurrentUICulture`) |
| --- | --- | --- | --- |
| R1 (기준) | A | 서수 순 | InvariantCulture |
| R2 반복 | A (출력 폴더만 새로) | — | InvariantCulture |
| R3 다른 경로 | B (다른 경로) | 역순 | InvariantCulture |
| R4 문화권 | A (출력 폴더만 새로) | — | de-DE |

결과 [S6] (각 열은 R1과 같은가). 스파이크의 D-same·D-move·D-culture가 R2·R3·R4에 해당한다. 스파이크의 D-move는 기준도 새 작업 폴더에서 돌렸다.

| 가짜 대상 | R2 | R3 | R4 |
| --- | --- | --- | --- |
| good | 같음 | 같음 | 같음 |
| clock-utcnow (`DateTime.UtcNow` 출력) | **다름** | **다름** | **다름** |
| guid (`Guid.NewGuid` 출력) | **다름** | **다름** | **다름** |
| abs-path (입력의 절대 경로 출력) | 같음 | **다름** | 같음 |
| culture-format (현재 문화권으로 숫자 읽기·쓰기) | 같음 | 같음 | **다름** |
| clock-today (`DateTime.Today` 날짜 출력) | 같음 | 같음 | 같음 → G2로 못 잡음 |
| string-hashcode (`"abc".GetHashCode()` 출력) | 같음 | 같음 | 같음 → G2로 못 잡음 |
| enum-order (`EnumerateFiles` 순서 그대로 출력) | 같음 | 같음 | 같음 → G2로 못 잡음 |

- de-DE를 고른 이유: `1.5.ToString()`이 `1,5`, `double.Parse("1.5")`가 `15`다. ko-KR은 둘 다 invariant와 같아 숫자 검사에 쓸모가 없다 [S6].
- 전제 확인: R4 전에 de-DE의 소수점이 `,`인지 확인한다. 아니면 실패로 본다. ICU가 없는 환경에서 R4가 조용히 약해지는 것을 막는다. 이 환경에는 ICU가 있었다 [S6]. ubuntu·windows 러너는 **미확인**.
- 문화권은 `try/finally`로 되돌린다.
- R3 때문에 "출력에 입력의 절대 경로를 쓰지 않는다"가 규칙이 된다. → 승인 질문 9.

G2가 못 잡는 것과 대신 막는 곳:

| 의존 | 사실 | 대신 막는 곳 |
| --- | --- | --- |
| 날짜 단위 시각 | 같은 날 두 번 돌리면 같다 [S6] | D1 (4.2) |
| `string.GetHashCode` | 프로세스마다 값이 다르다 [S7]. 한 프로세스 안에서는 같다 [S6][S7] | 골든 테스트(다른 프로세스가 만든 `expected/`). 효과는 **미확인**(T-004~) |
| 파일 열거 순서 | ext4에서 파일 3개를 c,a,b 순으로 만들어도, a,b,c 순으로 만들어도 열거 순서는 b,c,a였다. 200개도 두 생성 순서에서 열거 순서가 같았다. 이름 순도 아니었다 [S8]. 그래서 역순 복사로도 못 잡았다 [S6] | Core 규칙 "파일 목록은 서수 정렬해 쓴다"를 리뷰 항목으로. Windows 열거 순서는 **미확인** |
| 시간대 | 실험 안 함 | **미확인** |

예외:

- 평가서의 생성 일시(지시문 2절 3). 도우미는 출력을 정규화하지 않는다. 대상 어댑터가 고정 시각을 Core에 넘긴다. Core API가 생성 일시를 인자로 받는 방식은 해당 카드에서 정한다(**미확인**).
- Excel·PDF 파일이 같은 입력에서 바이트까지 같은지: **미확인**(ClosedXML·QuestPDF 미실험).

### 4.6 가짜 대상 (T-002 자기 검사)

T-002의 G2·G3 테스트는 가짜 대상으로 도우미를 검사한다. 정상 대상은 통과하고, 위반 대상은 해당 검사에서 실패해야 한다.

| 가짜 대상 | 기대 |
| --- | --- |
| good | G2·G3 모두 차이 0건 |
| clock-utcnow, guid | G2 R2·R3·R4 모두 차이 |
| abs-path | G2 R3 차이 (R2·R4는 같음) |
| culture-format | G2 R4 차이 (R2·R3는 같음) |
| input-append, input-delete, input-new-file, write-sibling, input-rewrite-same, input-temp-file, input-touch-mtime | G3 차이 |

- clock-today, string-hashcode, enum-order는 테스트로 넣지 않는다. 도우미가 못 잡는 것을 "통과해야 한다"로 굳히지 않기 위해서다. 한계는 이 스펙에 남긴다.

### 4.7 샘플 자료 (TestData)

```
tests/Scorecard.Tests/TestData/
├─ .gitattributes          * -text
└─ guard/
   └─ basic/
      ├─ case.yaml         name: guard-sample\nsteps: 2\n
      ├─ notes.md          # 합성 메모\n실제 값 없음.\n
      ├─ blob.bin          0x00~0xFF 256바이트
      └─ data/
         └─ sample.csv     id,value\n1,1.5\n2,2.25\n
```

- 모두 합성이다. 실제 값 없음. 텍스트는 UTF-8(BOM 없음), 줄바꿈 LF.
- `case.yaml`은 바이트 샘플이다. T-003 스키마와 무관하다. 파서 대상이 되면 T-003에서 맞춘다.
- `.gitattributes` 이유 [S9]: `core.autocrlf=true`로 clone하면 텍스트 파일이 CRLF로 바뀐다. 실험한 2줄짜리 `sample.csv`는 15바이트가 17바이트가 됐고 SHA-256이 바뀌었다. 바이너리(NUL 포함 5바이트)는 그대로다. `TestData/.gitattributes`에 `* -text`를 두면 같은 clone에서도 바이트가 그대로다.
- G2·G3는 같은 실행 안에서 비교하므로 CRLF 영향이 없다. 그래도 샘플의 해시를 테스트에 적거나, T-004 이후 골든 테스트가 입력 해시를 비교하면 영향을 받는다. 그래서 지금 막는다.
- GitHub windows 러너의 `core.autocrlf` 기본값: **미확인**. 그래서 텍스트 샘플에 `\r`이 없는지 보는 테스트를 하나 둔다(7절). 이 테스트가 windows 러너에서 실제 상태를 알려 준다.
- → 승인 질문 10.

## 5. 예외

| 상황 | 결과 | 근거 |
| --- | --- | --- |
| 검사할 dll이 없음 | 실패 | 빈 통과 방지(설계) |
| 대상 0개 Theory | xUnit 실패 `No data found` | [S4] |
| `#if DEBUG` 안 금지 코드 | 로컬 Debug 테스트는 잡음, CI Release는 못 봄 | [V12] |
| 주석·문자열의 금지 이름 | G1 통과 | [V05][V06] |
| 제3자 어셈블리 참조 | 허용 목록에 없으면 N6 실패 | [V13] |
| `WebClient` | 테스트 전에 CI 빌드 실패(SYSLIB0014) | [V17] |
| `GetTypeFromProgID`·`GetTypeFromCLSID` | CA1416 경고. CI(`-warnaserror`)에서는 빌드 실패가 될 것. `-warnaserror` 빌드는 **미확인** | [S0] |
| `LibraryImport` | `AllowUnsafeBlocks` 없이 빌드 실패 | [V18] |
| 대상이 예외를 던짐 | 그 검사 실패. 메시지에 예외 타입 | 설계 |
| de-DE 소수점이 `,`가 아님 | G2 전제 확인 실패 | 설계. 발생 환경 **미확인** |
| root 계정 | 읽기 전용 속성이 쓰기를 막지 못함. G3는 속성에 기대지 않음 | [S8] |
| 평가서 생성 일시 | 어댑터가 고정 시각을 넘김 | 지시문 2절 3 |

## 6. 예시

### 6.1 G1 실패 메시지 (V01을 넣었을 때)

검사기가 낸 항목 [V01]:

```
N1 asmref System.Net.Http
N3 namespace System.Net.Http.HttpClient
N3 type System.Net.Http.HttpClient
```

테스트 메시지 형식(제안):

```
Scorecard.Core.dll에 금지 참조 3건:
  N1 asmref System.Net.Http
  N3 namespace System.Net.Http.HttpClient
  N3 type System.Net.Http.HttpClient
```

### 6.2 수용 기준 2 확인 절차 (PR 본문용)

1. 저장소를 저장소 밖 임시 폴더에 clone한다.
2. `src/Scorecard.Core/NetworkCanary.cs`를 만든다. 내용은 `spikes/T-002/variants/V01-using-new.cs`와 같다.
3. `dotnet build AIAdoptionScorecard.sln -c Release -warnaserror`. 빌드는 성공해야 한다 [V01].
4. `dotnet test AIAdoptionScorecard.sln --no-build -c Release -- RunConfiguration.TreatNoTestsAsError=true`.
5. 기대: G1 Core 테스트 1건 실패. 메시지에 `N1 asmref System.Net.Http`. 다른 테스트는 통과. 근거: 같은 절차에서 스파이크 검사기가 테스트 출력 폴더의 Core 복사본에서 6.1의 3건을 냈다 [V01]. 실제 테스트로는 아직 돌려 보지 않았다.
6. 실패 줄을 PR 본문에 붙인다. clone은 버린다.

### 6.3 Cli 위반 (V15)

테스트 프로젝트가 Cli를 참조한 상태에서 Cli에 `HttpClient`를 넣으면, 테스트 출력 폴더의 `scorecard.dll`에서 N1·N3 3건이 나온다 [S3].

### 6.4 G2·G3 차이 메시지 형식 (제안)

```
G2 R3 다른 경로: out/summary.txt SHA-256 다름
G3 추가: input/extra.tmp
G3 변경(수정 시각): input/case.yaml
G3 삭제: input/notes.md
```

### 6.5 case.yaml 예시

해당 없음. 이 카드는 케이스를 다루지 않는다.

## 7. 테스트로 옮기는 방법 (test-engineer 참고)

- 폴더 `tests/Scorecard.Tests/Guards/`, 네임스페이스 `Scorecard.Tests.Guards`. Trait `Rule`은 `G1`·`G2`·`G3`.
- 기존 `Structure/RepoRoot.cs`로 샘플 경로를 찾는다.
- 검사기: 스파이크 `spikes/T-002/GuardSpike/MetaScan.cs`와 같은 방식. AssemblyRef, TypeRef(중첩 타입은 바깥 타입을 따라감), MemberRef(부모가 TypeSpec이면 제네릭 정의 이름), MethodDef의 `PinvokeImpl`, ModuleRef를 읽는다. 결과는 `"N1 asmref ..."` 같은 문자열 목록으로 돌려준다. 순서는 서수 정렬.
- 도우미: G2·G3는 차이 목록(`IReadOnlyList<string>`)을 돌려준다. 빈 목록이 통과다. 실제 대상 테스트는 `Assert.Empty`, 가짜 대상 테스트는 기대 조건 접두어가 있는지 본다.
- 가짜 대상과 카나리아는 테스트 프로젝트 안에 둔다. 스파이크 `GuardLab.cs`, `Canary.cs`, `variants/X03-test-canary.cs` 참고.
- `tests/Scorecard.Tests/Scorecard.Tests.csproj`에 Cli `ProjectReference`를 더한다(승인 질문 4). 기존 R6 구조 테스트는 그대로 통과한다 [S3].

테스트 목록(제안):

| Trait | 테스트 | 확인 내용 |
| --- | --- | --- |
| G1 | Core에 N1~N6 금지 참조가 없다 | `Scorecard.Core.dll` 0건 |
| G1 | Cli에 N1~N6 금지 참조가 없다 | `scorecard.dll` 0건 |
| G1 | Core에 D1 참조가 없다 | `Scorecard.Core.dll` 0건 |
| G1 | 검사기가 카나리아를 규칙마다 잡는다 | `Scorecard.Tests.dll` 결과에 기대 항목이 모두 있다 |
| G1 | 검사할 dll이 있다 | 두 파일 존재 |
| G2 | good: 차이 0건 | |
| G2 | clock-utcnow, guid, abs-path, culture-format: 기대 조건에 차이 | 4.6 |
| G2 | de-DE 전제 | 소수점 `,` |
| G3 | good: 차이 0건 | |
| G3 | 위반 가짜 대상 7종: 차이 있음 | 4.6 |
| G3 | 샘플 텍스트 파일에 `\r`이 없다 | 4.7 |

- 처음부터 통과하는 테스트다. T-002는 검사 도구를 만드는 카드이고, 검사할 위반이 지금 없다. T-001 승인 결정 1과 같은 예외로 PR 본문에 적는다. 대신 수용 기준 2(6.2)로 G1이 실패할 수 있음을 보인다. 가짜 대상 테스트가 G2·G3의 실패 경로를 보인다.
- 실행 시간: 스파이크 도구의 `guard` 명령(가짜 대상 15종 × 7회 실행, 프로세스 시작 포함)이 0.17~0.29초 걸렸다(S6과 같은 명령을 따로 3회 잼). Stop 훅 30초 기준(지시문 8절)과 비교하면 작다. xUnit 안에서의 실제 시간은 **미확인**.

## 8. 미확인 목록

1. GitHub ubuntu·windows 러너에서 G1~G3가 실제로 녹색인지.
2. Windows 런타임 폴더로 N6 프레임워크 판정이 같은 결과인지.
3. Windows에서 폴더 수정 시각이 안의 파일 생성·삭제 때 바뀌는지(input-temp-file 탐지).
4. Windows에서 파일 열거 순서. 일반 계정·Windows에서 읽기 전용 속성 동작.
5. GitHub windows 러너의 `core.autocrlf` 기본값.
6. ubuntu·windows 러너에 ICU(de-DE 문화권)가 있는지.
7. N3 네임스페이스 중 `System.Net.WebSockets`, `System.Net.Mail`, `System.Net.NetworkInformation`, `System.Net.Quic`, `System.Net.Security`를 실제로 잡는지(카나리아에 없음). 같은 문자열 비교라 동작은 같을 것으로 보지만 실험하지 않았다.
8. 다른 `DefineConstants`로 publish한 빌드.
9. 메모리 결과 API의 직렬화 결정론, 평가서 생성 일시 주입 방식, Excel·PDF 바이트 결정론.
10. 골든 테스트가 `string.GetHashCode`·열거 순서 의존을 실제로 잡는지.
11. 시간대 의존.
12. 실제 테스트 실행 시간.

## 9. 스파이크 단계 목록

`spikes/T-002/run-spike.sh <작업폴더> <로그폴더>`. 커밋된 HEAD를 작업 폴더에 clone해서 변형을 넣는다. 저장소의 `src/`, `tests/`는 건드리지 않는다. 스크립트를 세 번 돌렸다. 마지막 두 번의 요약은 걸린 시간, `GetHashCode` 값, X03에 더한 줄 외에는 같았다. `spikes/T-002/summary.txt`는 마지막 실행이다.

| 단계 | 한 일 |
| --- | --- |
| S0 | 스파이크 도구 빌드 (`--artifacts-path`로 산출물을 작업 폴더에) |
| S1 | 변형 없는 clone. Release `-warnaserror` 빌드. Core·Cli 메타데이터 검사, 텍스트 검사, 테스트 출력 폴더 목록 |
| S2 | Core 변형 V01~V14, V16~V18. 변형마다 새 clone. `-warnaserror` 빌드, 일반 빌드, 메타데이터 검사, 텍스트 검사 두 가지. V12는 Debug도. V01은 테스트 프로젝트 빌드·실행까지 |
| S3 | 테스트 프로젝트에 Cli 참조 추가 → 빌드·테스트 → `scorecard.dll` 검사. V15(Cli에 `HttpClient`) 추가 후 다시 |
| S4 | 대상 0개 Theory(X01) |
| S4b | 테스트 프로젝트 안 카나리아와 `System.Reflection.Metadata` 사용(X03) |
| S5 | 스파이크 도구 자신(카나리아 포함) 검사 |
| S6 | 가짜 대상 15종. 원본 불변 3방식, 결정론 3조건 |
| S7 | `string.GetHashCode`를 두 프로세스에서 |
| S8 | 파일 열거 순서(3개, 200개, 만든 순서 두 가지), 읽기 전용 속성 |
| S9 | `core.autocrlf=true` clone의 TestData 바이트, 하위 `.gitattributes` |
| S10 | BannedApiAnalyzers 5.0.0: `-warnaserror` 빌드에서 RS0030 오류. `#pragma warning disable RS0030`이면 빌드 통과, 메타데이터 검사는 여전히 잡음 |

---

## 사람 승인 필요

질문마다 prototyper의 제안을 적었다. "제안대로"로 답할 수 있다.

1. **G1 방법.** 텍스트 검사는 V04·V07·V08·V09·V10·V13을 놓치고 주석·문자열에서 오탐했다. 메타데이터 검사는 모두 잡고 오탐 0이었다(4.1).
   - 제안: G1은 메타데이터 검사만 한다. 텍스트 검사는 테스트로 두지 않는다. reviewer 체크리스트의 눈 검사는 그대로 둔다.
2. **금지 목록(N1~N5)이 카드의 네 이름보다 넓다.** `System.Net*` 어셈블리 전체, `Process` 어셈블리, P/Invoke 전체, 문자열 기반 리플렉션 로드를 막는다(4.2).
   - 제안: 4.2 표 그대로 승인. Core·Cli가 나중에 이 중 하나가 꼭 필요하면 그 카드에서 사람에게 묻는다.
3. **제3자 어셈블리 허용 목록(N6).** 런타임 폴더에 없는 어셈블리는 목록에 있어야 통과한다. LLM SDK를 막는 장치다.
   - 제안: 지금은 빈 목록. 패키지를 처음 쓰는 카드(예: T-003 YamlDotNet)에서 test-engineer가 테스트의 목록에 이름을 더하고 PR 본문에 적는다. 사람이 PR 머지로 확인한다.
4. **Cli도 검사하는가.** 카드는 "`src/`에"라고 한다. Cli를 검사하려면 테스트 프로젝트가 Cli를 참조해야 한다 [S3].
   - 제안: 검사한다(N1~N6). test-engineer가 `Scorecard.Tests.csproj`에 Cli `ProjectReference`를 더한다(tests/ 범위 안).
5. **D1(Core의 난수·현재시각·환경 정적 검사)을 T-002에 넣는가.** G2는 날짜 단위 시각을 못 잡는다 [S6]. 카드의 세 검사에는 없는 항목이다.
   - 제안: 넣는다. Core만. 목록은 4.2 D1(환경 변수 읽기 포함). Cli는 제외한다. 평가서 생성 일시와 스톱워치 측정은 Cli가 재서 값을 Core에 넘긴다.
6. **카나리아 자기 검사.** `Scorecard.Tests.dll`에 금지 API 참조(호출하지 않음)가 생긴다.
   - 제안: 둔다. reviewer의 "`src/`에 HttpClient 없음" 검사는 `src/`만 보므로 충돌하지 않는다.
7. **T-002의 G2·G3 대상(4.4).**
   - 제안: 안 A. 도우미와 가짜 대상 자기 검사만 만든다. 실제 대상 목록과 Theory는 첫 Core 처리 기능 카드(T-003 예정)에서 만든다. 리드 세션이 T-003 카드 메모에 "파서를 `GuardTargets`에 등록"을 적는다.
8. **G3 비교 범위.** 카드 문구는 "입력 파일의 SHA-256이 같다"이다. 스펙은 작업 폴더 전체(출력 제외)의 목록·크기·SHA-256·수정 시각을 본다(4.5).
   - 제안: 스펙대로 엄격하게 한다. 실행 전에 수정 시각을 2000-01-01Z로 고정한다. 읽기 전용 속성은 쓰지 않는다.
9. **G2 조건과 "출력에 절대 경로 금지".** R3(다른 경로) 때문에 출력에 입력의 절대 경로를 쓰면 실패한다.
   - 제안: R1~R4 네 조건 그대로. "출력에는 입력 폴더 기준 상대 경로만 쓴다"를 원칙으로 받아들인다. 평가서·번들의 경로 노출을 줄이는 효과도 있다(지시문 2절 8·10).
10. **TestData와 `.gitattributes`.** `core.autocrlf=true` clone에서 텍스트 샘플의 바이트가 바뀐다 [S9].
    - 제안: `tests/Scorecard.Tests/TestData/guard/basic/` 4파일과 `tests/Scorecard.Tests/TestData/.gitattributes`(`* -text`)를 둔다. `examples/`에도 같은 조치를 할지는 T-005에서 정한다.
11. **빌드 시 분석기(BannedApiAnalyzers).** RS0030으로 빌드를 막을 수 있지만 `#pragma`로 끌 수 있다 [S10]. `src/` csproj 변경이 필요하고, 수용 기준은 "테스트가 실패한다"이다.
    - 제안: T-002에는 넣지 않는다. 필요하면 별도 카드로 연다.
12. **G2가 못 잡는 의존(파일 열거 순서, `string.GetHashCode`).**
    - 제안: Core 규칙 "파일 목록은 서수 정렬해 쓴다, 해시 코드를 출력·순서에 쓰지 않는다"를 리드 세션이 `.claude/skills/review-checklist/SKILL.md`에 더한다. 골든 테스트(T-004~, ubuntu·windows)가 보강한다.
13. **"처음부터 통과하는 테스트" 예외.** G1~G3 테스트는 지금 위반이 없어 처음부터 통과한다.
    - 제안: T-001 승인 결정 1과 같이 예외로 인정한다. PR 본문에 적는다. 수용 기준 2 절차(6.2)의 실패 결과와 가짜 대상 테스트로 실패 경로를 보인다.

## 사람 승인 결과 (2026-11-06, juhan: "추천대로")

질문 1~13 모두 위에 적은 제안대로 승인됐다. 요약:

1. G1은 메타데이터 검사만. 텍스트 검사 테스트는 두지 않는다.
2. 4.2 금지 목록(N1~N5) 그대로.
3. N6 허용 목록은 지금 비어 있다. 패키지를 처음 쓰는 카드에서 test-engineer가 더하고 PR 본문에 적는다.
4. Cli도 검사한다. test-engineer가 테스트 프로젝트에 Cli 참조를 더한다.
5. D1(Core의 난수·현재시각·환경 정적 검사)을 T-002에 넣는다. Core만.
6. 카나리아 자기 검사를 둔다.
7. 안 A. 도우미와 가짜 대상 자기 검사만. 실제 대상은 T-003부터(리드가 T-003 카드 메모에 적는다).
8. G3는 작업 폴더 전체의 목록·크기·SHA-256·수정 시각을 비교한다. 수정 시각은 실행 전 2000-01-01Z로 고정한다.
9. G2 조건 R1~R4 그대로. 출력에는 입력 폴더 기준 상대 경로만 쓴다.
10. `TestData/guard/basic/`과 `TestData/.gitattributes`(`* -text`)를 둔다.
11. BannedApiAnalyzers는 넣지 않는다.
12. 리드가 review-checklist에 "파일 목록은 서수 정렬, 해시 코드를 출력·순서에 쓰지 않음"을 더한다.
13. 처음부터 통과하는 테스트는 예외로 인정하고 PR 본문에 적는다.
