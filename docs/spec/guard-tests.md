# guard-tests (T-002 스펙)

- 상태: 승인됨(2026-11-06, juhan "추천대로"). 지금 규칙만 남겼다. 텍스트 검사와의 비교표, 승인 질문, 스파이크 단계·PR 확인 절차는 저장소 이력에 있다.
- 작업 카드: `docs/tasks/T-002-guard-tests.md`
- ADR 0010: 도구는 온라인 AI 도구다. G1(네트워크 금지)은 지우지 않고 납품물 템플릿 검사로 옮긴다. 옮기는 카드(M2-2 또는 M2-4) 전까지 `src/`에 그대로 걸린다.

## 0. 표기

- `[읽음]`: 커밋된 파일을 읽어서 확인한 사실.
- `[S0]`~`[S10]`, `[V01]`~`[V18]`: T-002 스파이크 단계와 변형. 스파이크는 지웠다(이력).
- **미확인**: 실험하지 않았거나 이 환경에서 확인할 수 없는 것.
- 검사 이름: **G1** 네트워크 금지, **G2** 결정론, **G3** 원본 불변. G1의 규칙 ID는 N1~N6, D1, R1(Core 전용 리플렉션 호출).

## 1. 범위

- G1: `src/`의 컴파일 결과에 네트워크·외부 프로세스·LLM 참조가 없다.
- G2: 같은 입력을 여러 번 처리하면 같은 출력이 나온다.
- G3: 처리 전후로 입력이 그대로다.

범위 밖: 제3자 패키지 내부 코드, publish 산출물 검사(release 에이전트), pre-commit 검사(지시문 2절 10).

## 2. 입력

| 검사 | 입력 |
| --- | --- |
| G1 | 테스트 출력 폴더(`AppContext.BaseDirectory`)의 `Scorecard.Core.dll`, `scorecard.dll` |
| G1 자기 검사 | 같은 폴더의 `Scorecard.Tests.dll` (카나리아 포함, 4.3) |
| G2·G3 | 처리 대상(`GuardTarget`)과 합성 샘플 `tests/Scorecard.Tests/TestData/guard/<샘플>/` |

- 테스트 프로젝트는 Core와 Cli를 참조한다. 그래서 테스트 출력 폴더에 두 dll이 있다.

## 3. 출력

xUnit 테스트 결과. G1 실패 메시지는 규칙 ID와 찾은 참조를 한 줄에 하나씩 적는다(예: Core에 `HttpClient`를 넣으면 `N1 asmref System.Net.Http`, `N3 namespace System.Net.Http.HttpClient`, `N3 type System.Net.Http.HttpClient` [V01]). G2·G3는 조건 이름과 다른 항목을 적는다:

```
G2 R3 다른 경로: out/summary.txt SHA-256 다름
G3 추가: input/extra.tmp
G3 변경(수정 시각): input/case.yaml
G3 삭제: input/notes.md
```

## 4. 규칙

### 4.1 G1 방법: 메타데이터 검사

텍스트 검사(정규식)는 별칭(`Directory.Build.props`), 문자열 리플렉션, `Process`, `TcpClient`, P/Invoke, 제3자 LLM SDK를 놓치고 주석·문자열에서 오탐했다. 메타데이터 검사는 모두 잡고 오탐이 없었다 [S2].

결정: G1은 메타데이터 검사만 한다. 텍스트 검사는 테스트로 두지 않는다. reviewer의 눈 검사는 그대로다.

메타데이터 검사의 한계:

- 테스트가 도는 구성의 빌드만 본다. `#if DEBUG` 안 코드는 Release 검사에서 보이지 않는다 [V12]. CI는 Release, 로컬 `dotnet test` 기본값은 Debug다 [읽음 ci.yml]. 배포도 Release다(지시문 4절). 다른 `DefineConstants`로 publish하면 그 빌드는 검사 밖이다(**미확인**, 실험 안 함).
- 제3자 어셈블리 내부는 보지 않는다. N6 허용 목록에 사람이 확인한 것만 넣는다.
- 리플렉션 우회는 N5·R1 목록의 멤버만 잡는다. `DynamicInvoke`, `MethodInvoker`, `dynamic`, 식 트리 `Compile`, 허용된 제3자 라이브러리를 거치는 우회 등 목록 밖 경로는 못 잡는다. `XmlReader.Create(url)`처럼 프레임워크 내부에서 네트워크를 쓰는 API와 Windows UNC 경로도 못 잡는다. reviewer 검사는 그대로 둔다.
- `string.GetHashCode()`는 메타데이터에 `System.Object::GetHashCode`로 남는다 [S5]. 문자열 해시만 골라 막을 수 없다.

### 4.2 G1 규칙

| 어셈블리 | 위치 | 규칙 |
| --- | --- | --- |
| `Scorecard.Core.dll` | 테스트 출력 폴더 | N1~N6, D1, R1 |
| `scorecard.dll` | 테스트 출력 폴더 | N1~N6 |

- 파일이 없으면 실패로 본다. 빈 통과를 막는다.
- 어셈블리를 로드하지 않는다. `PEReader`로 파일의 메타데이터 표만 읽는다.
- `System.Reflection.Metadata`는 공유 프레임워크에 있다. 테스트 프로젝트에서 패키지 추가 없이 빌드된다 [S4b].
- 이름은 서수(대소문자 구분)로 비교한다.

| ID | 메타데이터 표 | 금지 조건 | 확인 |
| --- | --- | --- | --- |
| N1 | AssemblyRef | 이름이 `System.Net`이거나 `System.Net.`으로 시작 | [V01][V09][V17][S5]. `WebUtility`, `Uri`는 `System.Runtime`에 있어 걸리지 않는다 [V14][S5] |
| N2 | AssemblyRef | `System.Diagnostics.Process`, `System.Runtime.Loader` | [V08][S5] |
| N3 | TypeRef | 타입 `System.Net.Http.HttpClient`, `System.Net.Sockets.Socket`, `System.Net.WebClient`, `System.Diagnostics.Process`, `System.Diagnostics.ProcessStartInfo`, `System.Runtime.InteropServices.NativeLibrary`. 네임스페이스(같거나 하위) `System.Net.Http`, `System.Net.Sockets`, `System.Net.WebSockets`, `System.Net.Mail`, `System.Net.NetworkInformation`, `System.Net.Quic`, `System.Net.Security`, `System.Runtime.Loader` | 타입 6개와 `System.Net.Http`·`System.Net.Sockets`·`System.Runtime.Loader` 네임스페이스는 [S5]. 나머지 네임스페이스 5개는 **미확인**(카나리아에 없음) |
| N4 | MethodDef, ModuleRef | P/Invoke 메서드(`PinvokeImpl` 플래그)가 있거나 ModuleRef가 1개 이상 | `DllImport` [V10][S5], `LibraryImport` [S5] |
| N5 | MemberRef | `System.Type::GetType`(정적만, 인스턴스 `GetType()` 제외), `::GetTypeFromProgID`, `::GetTypeFromCLSID`, `System.Reflection.Assembly::Load`, `::LoadFrom`, `::LoadFile`, `::UnsafeLoadFrom`, `::LoadWithPartialName`, `::GetType`, `System.Reflection.Module::GetType`, `System.AppDomain::Load`, `::CreateInstance`, `::CreateInstanceAndUnwrap`, `::CreateInstanceFrom`, `::CreateInstanceFromAndUnwrap`, `::ExecuteAssembly`, `::ExecuteAssemblyByName`, `System.Activator::CreateInstance`(제네릭 제외), `::CreateInstanceFrom`, `System.Runtime.InteropServices.Marshal::GetDelegateForFunctionPointer` | 기존 항목은 [S5]. 추가 8개는 카나리아 자기 검사로 확인(T-002 리뷰 보강). 제네릭 `CreateInstance<T>`(`new T()`)는 걸리지 않는다 [V14]. 정적 멤버는 하위 타입 이름으로 불러도 선언 타입 키가 남는다 |
| N6 | AssemblyRef | 런타임 폴더(`RuntimeEnvironment.GetRuntimeDirectory()`)에 같은 이름의 dll이 없으면 허용 목록에 있어야 한다. 허용 목록은 지금 비어 있다 | [V13] |
| D1 | TypeRef, MemberRef (Core만) | 타입 `System.Random`, `System.Security.Cryptography.RandomNumberGenerator`, `System.Diagnostics.Stopwatch`. 멤버 `System.DateTime::get_Now`, `::get_UtcNow`, `::get_Today`, `System.DateTimeOffset::get_Now`, `::get_UtcNow`, `System.Guid::NewGuid`, `::CreateVersion7`, `System.Environment::get_TickCount`, `::get_TickCount64`, `::get_MachineName`, `::get_UserName`, `::GetEnvironmentVariable`, `::GetEnvironmentVariables`, `System.TimeProvider::get_System`, `System.Environment::ExpandEnvironmentVariables`, `::get_CurrentDirectory`, `::get_ProcessId`, `System.IO.Path::GetRandomFileName`, `::GetTempFileName`, `System.TimeZoneInfo::get_Local` | 전부 [S5], Core 빌드에서 [V16]. 추가 6개는 카나리아 자기 검사로 확인(T-002 리뷰 보강) |
| R1 | MemberRef (Core만) | `System.Reflection.MethodBase::Invoke`, `System.Reflection.ConstructorInfo::Invoke`, `System.Type::InvokeMember`, `System.Reflection.IReflect::InvokeMember` | 카나리아 자기 검사(T-002 리뷰 보강). `MethodInfo`·`DynamicMethod` 등의 `Invoke`는 `MethodBase::Invoke`로 남는다. 대리자 호출(`Func<T>::Invoke`)은 걸리지 않는다 |

- N6의 프레임워크 판정: `System.Runtime`, `System.Console`, `System.Linq`, `System.Security.Cryptography`, `System.Text.Json`, `System.Text.RegularExpressions`, `System.Reflection.Metadata`는 프레임워크로 판정됐다 [S1][V14][S4b]. 가짜 SDK `FakeLlm`과 `xunit.core`는 아니었다 [V13][S4b]. Windows 런타임 폴더에서 같은 결과인지는 **미확인**.
- LLM API는 이름 목록을 두지 않는다. 직접 HTTP 호출은 N1·N3가, SDK 패키지는 N6가 잡는다 [V01][V13]. SDK가 허용 목록에 들어가면 못 잡는다. 그래서 허용 목록 추가는 사람이 확인한다.
- D1은 지시문 2절 3(난수·현재시각 금지)을 정적으로 막는다. G2가 날짜 단위 시각을 못 잡기 때문이다(4.5). 환경 변수 읽기도 넣었다. 환경 변수로 API 키를 읽는 경로를 막으려는 것이다(`.claude/rules/core-no-network.md` "외부 API 키" [읽음]). 출력이 실행 환경에 따라 달라지는 것도 막는다.
- 빌드가 먼저 막는 것: `WebClient`는 SYSLIB0014 경고 [V17], `Type.GetTypeFromProgID`·`GetTypeFromCLSID`는 CA1416 경고 [S0]. CI는 `-warnaserror`라 이 경고는 빌드 실패가 된다 [읽음 ci.yml]. `-warnaserror`가 이런 경고를 오류로 바꾸는 것은 SYSLIB0014 [V17]와 분석기 경고 RS0030 [S10]에서 확인했다. CA1416 자체를 `-warnaserror`로 빌드하지는 않았다. `LibraryImport`는 `AllowUnsafeBlocks` 없이는 빌드 오류다 [V18]. `DllImport`는 csproj 변경 없이 `-warnaserror`도 통과한다 [V10]. 그래서 N4가 필요하다.

### 4.3 G1 자기 검사 (카나리아)

- 테스트 어셈블리에 금지 API를 참조하는 카나리아 클래스를 둔다. 아무도 호출하지 않는다.
- 같은 검사기로 `Scorecard.Tests.dll`을 읽어, 카나리아가 기대 규칙마다 잡히는지 본다. 검사기가 망가져 늘 0건을 내는 경우를 막는다.
- `Scorecard.Tests.dll`에는 `xunit.core` 등 비프레임워크 참조도 있어 N6가 걸린다 [S4b]. 자기 검사는 "기대 항목이 모두 들어 있는가"만 본다. 0건 여부는 보지 않는다.
- 카나리아의 `WebClient`는 `#pragma warning disable SYSLIB0014`가 있어야 빌드된다 [S4b][V17]. `LibraryImport`는 `AllowUnsafeBlocks`가 필요하므로 카나리아에는 `DllImport`만 쓴다 [V18].

### 4.4 G2·G3가 무엇을 실행하나

- 실제 대상(`GuardTargets`)은 Core 처리 기능 카드마다 등록한다(T-003 파서부터. 0개 Theory는 `No data found`로 실패 [S4]). 메모리 결과만 내는 API는 테스트 쪽 어댑터가 파일로 쓴다. 가짜 대상(4.6)은 도우미 자기 검사다.

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
- 카드 문구("SHA-256이 같다")보다 엄격하다. SHA-256만 보면 새 파일, 출력 밖 쓰기, 같은 내용 다시 쓰기, 임시 파일, 수정 시각 변경을 놓친다 [S6].
- 읽기 전용 속성으로 막는 방법은 쓰지 않는다. root 계정에서는 읽기 전용(모드 0444) 파일에 덧붙이기가 성공했다 [S8]. 일반 계정·Windows에서 막히는지는 **미확인**.

#### G2 결정론

같은 대상을 네 번 돌리고 R1과 비교한다. 비교는 경로·종류·크기·SHA-256. 수정 시각은 보지 않는다.

| 실행 | 작업 폴더 | 입력 복사 순서 | 문화권(`CurrentCulture`, `CurrentUICulture`) |
| --- | --- | --- | --- |
| R1 (기준) | A | 서수 순 | InvariantCulture |
| R2 반복 | A (출력 폴더만 새로) | — | InvariantCulture |
| R3 다른 경로 | B (다른 경로) | 역순 | InvariantCulture |
| R4 문화권 | A (출력 폴더만 새로) | — | de-DE |

- 가짜 대상이 어느 실행에서 잡히는지는 4.6 표다. 날짜 단위 시각, `string.GetHashCode`, 파일 열거 순서는 G2로 못 잡는다(아래 표) [S6].
- de-DE를 고른 이유: `1.5.ToString()`이 `1,5`, `double.Parse("1.5")`가 `15`다. ko-KR은 둘 다 invariant와 같아 숫자 검사에 쓸모가 없다 [S6].
- 전제 확인: R4 전에 de-DE의 소수점이 `,`인지 확인한다. 아니면 실패로 본다. ICU가 없는 환경에서 R4가 조용히 약해지는 것을 막는다. 이 환경에는 ICU가 있었다 [S6]. ubuntu·windows 러너는 **미확인**.
- 문화권은 `try/finally`로 되돌린다.
- R3 때문에 "출력에 입력의 절대 경로를 쓰지 않는다"가 규칙이 된다.

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

- `tests/Scorecard.Tests/TestData/.gitattributes`(`* -text`). `guard/basic/`에 `case.yaml`(바이트 샘플), `notes.md`, `blob.bin`(0x00~0xFF 256바이트), `data/sample.csv`. Core 대상은 `guard/case-valid` 등 각 카드의 표본을 쓴다.
- 모두 합성이다. 실제 값 없음. 텍스트는 UTF-8(BOM 없음), 줄바꿈 LF.
- `.gitattributes` 이유 [S9]: `core.autocrlf=true` clone에서 텍스트 파일이 CRLF로 바뀌어 SHA-256이 달라진다. `* -text`면 그대로다. 텍스트 샘플에 `\r`이 없는지 보는 테스트가 있다.

## 5. 예외

| 상황 | 결과 | 근거 |
| --- | --- | --- |
| 검사할 dll이 없음 | 실패 | 빈 통과 방지(설계) |
| `#if DEBUG` 안 금지 코드 | 로컬 Debug 테스트는 잡음, CI Release는 못 봄 | [V12] |
| 주석·문자열의 금지 이름 | G1 통과 | [V05][V06] |
| 제3자 어셈블리 참조 | 허용 목록에 없으면 N6 실패 | [V13] |
| 대상이 예외를 던짐 | 그 검사 실패. 메시지에 예외 타입 | 설계 |
| de-DE 소수점이 `,`가 아님 | G2 전제 확인 실패 | 설계. 발생 환경 **미확인** |
| root 계정 | 읽기 전용 속성이 쓰기를 막지 못함. G3는 속성에 기대지 않음 | [S8] |

## 7. 테스트

- 폴더 `tests/Scorecard.Tests/Guards/`, 네임스페이스 `Scorecard.Tests.Guards`. Trait `Rule`은 `G1`·`G2`·`G3`. 샘플 경로는 `Structure/RepoRoot.cs`로 찾는다.
- 검사기는 AssemblyRef, TypeRef(중첩 타입은 바깥 타입을 따라감), MemberRef(부모가 TypeSpec이면 제네릭 정의 이름), MethodDef의 `PinvokeImpl`, ModuleRef를 읽는다. 결과는 `"N1 asmref ..."` 같은 문자열 목록, 서수 정렬.
- G2·G3 도우미는 차이 목록을 돌려준다. 빈 목록이 통과다. 실제 대상은 `Assert.Empty`, 가짜 대상은 기대 조건 접두어를 본다.

## 8. 미확인 목록

1. Windows: N6 프레임워크 판정, 폴더 수정 시각(input-temp-file), 파일 열거 순서, 읽기 전용 속성.
2. N3 네임스페이스 중 카나리아에 없는 5개(`WebSockets`, `Mail`, `NetworkInformation`, `Quic`, `Security`)를 실제로 잡는지.
3. 다른 `DefineConstants`로 publish한 빌드.
4. 평가서 생성 일시 주입 방식, Excel·PDF 바이트 결정론.
5. 골든 테스트가 `string.GetHashCode`·열거 순서 의존을 실제로 잡는지. 시간대 의존.
