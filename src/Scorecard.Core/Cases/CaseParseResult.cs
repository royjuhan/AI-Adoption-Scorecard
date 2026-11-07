namespace Scorecard.Core.Cases;

// 파서 결과. 오류가 하나라도 있으면 Case는 null이다(스펙 4.12).
public sealed record CaseParseResult(CaseDefinition? Case, IReadOnlyList<CaseError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

// 오류 하나(스펙 4.9).
// Source는 파일 이름(상대 이름)이다. 절대 경로를 쓰지 않는다.
// Line·Column은 1부터다. 0이면 없음. 열은 UTF-16 코드 단위다.
// Path는 일반 경로다(예: steps[2].answers.batch_size). 문서 전체 오류는 빈 문자열이다.
public sealed record CaseError(string Source, int Line, int Column, string Code, string Path, string Message)
{
    // <Source>:<Line>:<Column>: <Code> <Path>: <Message>
    // 열이 0이면 <Source>:<Line>:, 줄도 0이면 <Source>:. 경로가 비면 <Code>: <Message>.
    public override string ToString()
    {
        var position = Line == 0
            ? Source
            : Column == 0
                ? FormattableString.Invariant($"{Source}:{Line}")
                : FormattableString.Invariant($"{Source}:{Line}:{Column}");
        var path = Path.Length == 0 ? string.Empty : " " + Path;
        return $"{position}: {Code}{path}: {Message}";
    }
}
