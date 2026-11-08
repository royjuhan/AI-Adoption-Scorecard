using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;

namespace Scorecard.Core.Build;

// 제작 의뢰서 번들 하나(docs/spec/build-request.md 3절). Files는 build-request/ 안 상대 경로('/')와 바이트, 지시문 3.6 표 순서다.
// export-check.md는 Files에 없다. ExportCheckDraft가 사람이 확인하기 전의 초안이다. 확인되면 사람이 적은 파일 그대로 번들에 넣는다.
public sealed record BuildRequestBundle(
    string Case,
    int Step,
    string Name,
    IReadOnlyList<SourceFile> Files,
    IReadOnlyList<ScanHit> Hits,
    byte[] ExportCheckDraft);

// 문자열 검사에 걸린 것 하나. Path는 build-request/ 안 상대 경로, Line은 1부터.
public sealed record ScanHit(string Pattern, string Path, int Line, string Value);

public sealed record BuildRequestResult(BuildRequestBundle? Bundle, IReadOnlyList<CaseError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

// 사람 확인 결과(4.5). Status: confirmed, missing, unsigned, changed, hits. confirmed면 Error가 null이고 Signer가 있다.
public sealed record ExportCheck(string Status, string? Signer, CaseError? Error)
{
    public bool IsConfirmed => Error is null;
}
