namespace Scorecard.Core.Cases;

// 오류 코드. 스펙 4.9 표의 문자열이다.
internal static class ErrorCode
{
    public const string File = "file";
    public const string Encoding = "encoding";
    public const string Empty = "empty";
    public const string Syntax = "syntax";
    public const string Depth = "depth";
    public const string MultiDocument = "multi-document";
    public const string Alias = "alias";
    public const string Tag = "tag";
    public const string DuplicateKey = "duplicate-key";
    public const string Type = "type";
    public const string UnknownField = "unknown-field";
    public const string Required = "required";
    public const string Enum = "enum";
    public const string Format = "format";
    public const string Range = "range";
    public const string Duplicate = "duplicate";
    public const string Conflict = "conflict";
}

// 오류를 모은다. 첫 오류에서 멈추지 않는다(스펙 4.9).
internal sealed class ErrorSink(string source)
{
    private readonly List<CaseError> errors = [];

    public int Count => errors.Count;

    public void Add(int line, int column, string code, string path, string message) =>
        errors.Add(new CaseError(source, line, column, code, path, message));

    // file·encoding·empty·syntax·depth는 그 한 건만 낸다. 모은 오류를 버린다.
    public void Fatal(int line, int column, string code, string path, string message)
    {
        errors.Clear();
        Add(line, column, code, path, message);
    }

    // (줄, 열) 오름차순. 같은 위치는 찾은 순서다(OrderBy는 안정 정렬).
    public CaseError[] Sorted() => errors.OrderBy(e => e.Line).ThenBy(e => e.Column).ToArray();
}
