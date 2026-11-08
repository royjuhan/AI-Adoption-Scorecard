using System.Globalization;
using Scorecard.Core.Cases;

namespace Scorecard.Core.Report;

// 확인 파일(review.yaml)을 읽는다. docs/spec/report.md 5절, 데이터 5절 표.
// 단계마다 맞음(keep) 또는 바꾸기(change). 바꾸기는 새 담당과 사유 한 줄이 있어야 확정된다. 없으면 오류(종료 2)다.
// 문법·인코딩 오류는 case.yaml 파서와 같다(case-schema.md 4.7·4.8). 오류는 모두 모은다.
public static class ReviewParser
{
    public const string Keep = "keep";
    public const string Change = "change";

    // 바꾸기에 쓸 수 있는 담당(classification.md 4.13). 비교 값은 Core 분류와 같다.
    public static readonly IReadOnlyList<string> Assignees = ["①", "②", "②M", "③", "④"];

    private static readonly string[] EntryKeys = ["number", "answer", "assignee", "reason"];

    // content를 읽는다. stepNumbers는 classification.json의 단계 번호다. 그 밖의 번호는 range 오류다.
    public static (IReadOnlyList<ReviewEntry>? Entries, IReadOnlyList<CaseError> Errors) Parse(byte[] content, string sourceName, IReadOnlyCollection<int> stepNumbers)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(stepNumbers);

        var errors = new ErrorSink(sourceName);
        var text = CaseParser.Decode(content, errors);
        var root = text is null ? null : YamlTree.Read(text, errors);
        var entries = new List<ReviewEntry>();
        if (root is not null)
        {
            ReadRoot(root, errors, stepNumbers, entries);
        }

        return errors.Count == 0 ? (entries.OrderBy(e => e.Number).ToArray(), []) : (null, errors.Sorted());
    }

    private static void ReadRoot(YamlNode root, ErrorSink errors, IReadOnlyCollection<int> stepNumbers, List<ReviewEntry> entries)
    {
        if (root is not YamlMapping mapping)
        {
            errors.Add(root.Line, root.Column, ErrorCodes.Type, string.Empty, "매핑이어야 한다");
            return;
        }

        YamlNode? steps = null;
        foreach (var entry in mapping.Entries)
        {
            if (entry.Key.Value == "steps")
            {
                steps = entry.Value;
            }
            else
            {
                errors.Add(entry.Key.Line, entry.Key.Column, ErrorCodes.UnknownField, entry.Key.Value, "모르는 키다");
            }
        }

        if (steps is null)
        {
            errors.Add(mapping.Line, mapping.Column, ErrorCodes.Required, "steps", "필수 키가 없다");
            return;
        }

        if (steps is not YamlSequence sequence)
        {
            errors.Add(steps.Line, steps.Column, ErrorCodes.Type, "steps", "목록이어야 한다");
            return;
        }

        var seen = new Dictionary<int, string>();
        for (var i = 0; i < sequence.Items.Count; i++)
        {
            var path = YamlTree.ItemPath("steps", i + 1);
            if (ReadEntry(sequence.Items[i], path, errors, stepNumbers) is not { } item)
            {
                continue;
            }

            if (seen.TryGetValue(item.Entry.Number, out var first))
            {
                errors.Add(item.Line, item.Column, ErrorCodes.Duplicate, YamlTree.ChildPath(path, "number"),
                    FormattableString.Invariant($"같은 단계가 두 번 나온다: {item.Entry.Number} (처음: {first})"));
                continue;
            }

            seen[item.Entry.Number] = path;
            entries.Add(item.Entry);
        }
    }

    private static (ReviewEntry Entry, int Line, int Column)? ReadEntry(YamlNode node, string path, ErrorSink errors, IReadOnlyCollection<int> stepNumbers)
    {
        if (node is not YamlMapping mapping)
        {
            errors.Add(node.Line, node.Column, ErrorCodes.Type, path, "매핑이어야 한다");
            return null;
        }

        var values = new Dictionary<string, YamlScalar>(StringComparer.Ordinal);
        var ok = true;
        foreach (var entry in mapping.Entries)
        {
            var key = entry.Key.Value;
            var keyPath = YamlTree.ChildPath(path, key);
            if (!EntryKeys.Contains(key, StringComparer.Ordinal))
            {
                errors.Add(entry.Key.Line, entry.Key.Column, ErrorCodes.UnknownField, keyPath, "모르는 키다");
                ok = false;
            }
            else if (entry.Value is not YamlScalar scalar || scalar.IsNull)
            {
                errors.Add(entry.Value.Line, entry.Value.Column, ErrorCodes.Type, keyPath, "값이 하나여야 한다");
                ok = false;
            }
            else
            {
                values[key] = scalar;
            }
        }

        int? number = null;
        if (!values.TryGetValue("number", out var numberNode))
        {
            errors.Add(mapping.Line, mapping.Column, ErrorCodes.Required, YamlTree.ChildPath(path, "number"), "필수 키가 없다");
            ok = false;
        }
        else if (!numberNode.Plain || !int.TryParse(numberNode.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            errors.Add(numberNode.Line, numberNode.Column, ErrorCodes.Type, YamlTree.ChildPath(path, "number"), "정수여야 한다");
            ok = false;
        }
        else if (!stepNumbers.Contains(n))
        {
            errors.Add(numberNode.Line, numberNode.Column, ErrorCodes.Range, YamlTree.ChildPath(path, "number"),
                FormattableString.Invariant($"없는 단계다: {n}"));
            ok = false;
        }
        else
        {
            number = n;
        }

        string? answer = null;
        if (!values.TryGetValue("answer", out var answerNode))
        {
            errors.Add(mapping.Line, mapping.Column, ErrorCodes.Required, YamlTree.ChildPath(path, "answer"), "필수 키가 없다");
            ok = false;
        }
        else if (answerNode.Value is not (Keep or Change))
        {
            errors.Add(answerNode.Line, answerNode.Column, ErrorCodes.Enum, YamlTree.ChildPath(path, "answer"), "keep 또는 change여야 한다");
            ok = false;
        }
        else
        {
            answer = answerNode.Value;
        }

        values.TryGetValue("assignee", out var assigneeNode);
        values.TryGetValue("reason", out var reasonNode);
        var reason = reasonNode?.Value.Trim();
        if (reasonNode is not null && reasonNode.Value.Contains('\n', StringComparison.Ordinal))
        {
            errors.Add(reasonNode.Line, reasonNode.Column, ErrorCodes.Format, YamlTree.ChildPath(path, "reason"), "사유는 한 줄이다");
            ok = false;
        }

        if (assigneeNode is not null && !Assignees.Contains(assigneeNode.Value, StringComparer.Ordinal))
        {
            errors.Add(assigneeNode.Line, assigneeNode.Column, ErrorCodes.Enum, YamlTree.ChildPath(path, "assignee"), "①, ②, ②M, ③, ④ 가운데 하나여야 한다");
            ok = false;
        }

        if (answer == Keep && assigneeNode is not null)
        {
            errors.Add(assigneeNode.Line, assigneeNode.Column, ErrorCodes.Conflict, YamlTree.ChildPath(path, "assignee"), "맞음(keep)에는 담당을 적지 않는다");
            ok = false;
        }

        if (answer == Change)
        {
            if (assigneeNode is null)
            {
                errors.Add(mapping.Line, mapping.Column, ErrorCodes.Required, YamlTree.ChildPath(path, "assignee"), "바꾸기에는 새 담당이 필요하다");
                ok = false;
            }

            if (string.IsNullOrEmpty(reason))
            {
                var (line, column) = reasonNode is null ? (mapping.Line, mapping.Column) : (reasonNode.Line, reasonNode.Column);
                errors.Add(line, column, ErrorCodes.Required, YamlTree.ChildPath(path, "reason"), "바꾸기에는 사유 한 줄이 필요하다. 사유가 없으면 확정하지 않는다");
                ok = false;
            }
        }

        return ok && number is { } num && answer is not null
            ? (new ReviewEntry(num, answer, answer == Change ? assigneeNode!.Value : null, string.IsNullOrEmpty(reason) ? null : reason), mapping.Line, mapping.Column)
            : null;
    }

    // case-schema.md 4.9 오류 코드 가운데 이 파일이 쓰는 것.
    private static class ErrorCodes
    {
        public const string Type = "type";
        public const string UnknownField = "unknown-field";
        public const string Required = "required";
        public const string Enum = "enum";
        public const string Format = "format";
        public const string Range = "range";
        public const string Duplicate = "duplicate";
        public const string Conflict = "conflict";
    }
}
