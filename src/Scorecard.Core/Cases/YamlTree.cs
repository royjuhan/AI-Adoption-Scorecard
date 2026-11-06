using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Scorecard.Core.Cases;

// YAML 노드. 위치는 1부터다. 열은 UTF-16 코드 단위다.
internal abstract record YamlNode(int Line, int Column);

// Plain은 따옴표·블록 없이 쓴 값인가. null 판정과 정수·숫자·불리언 판정에 쓴다.
internal sealed record YamlScalar(int Line, int Column, string Value, bool Plain) : YamlNode(Line, Column)
{
    // YAML 1.2 core 스키마의 null: 따옴표 없는 빈 값, ~, null, Null, NULL(스펙 4.6).
    public bool IsNull => Plain && Value is "" or "~" or "null" or "Null" or "NULL";
}

internal sealed record YamlMapping(int Line, int Column, IReadOnlyList<YamlEntry> Entries) : YamlNode(Line, Column);

internal sealed record YamlEntry(YamlScalar Key, YamlNode Value);

internal sealed record YamlSequence(int Line, int Column, IReadOnlyList<YamlNode> Items) : YamlNode(Line, Column);

// 별칭 자리. 이미 오류로 적었다. 스키마 검사는 이 자리를 더 보지 않는다(스펙 4.7).
internal sealed record YamlAlias(int Line, int Column) : YamlNode(Line, Column);

// YamlDotNet 저수준 Parser 이벤트로 작은 트리를 만든다(스펙 4.1 방식 C).
// 객체 역직렬화(YamlDotNet.Serialization)를 쓰지 않는다. YamlDotNet.Core 네임스페이스만 쓴다(G1 Y1).
// 앵커·별칭·태그·중복 키·키 형식·깊이·문서 수를 여기서 본다(스펙 4.7).
internal static class YamlTree
{
    // 루트 매핑이 1단계다. 스칼라는 세지 않는다. case.yaml의 실제 깊이는 4다(루트 → steps → 항목 → answers).
    public const int MaxDepth = 16;

    // 문서 하나를 읽는다. 문서가 없거나 문법 오류·깊이 초과면 그 한 건만 남기고 null을 돌려준다.
    public static YamlNode? Read(string text, ErrorSink errors)
    {
        var parser = new Parser(new StringReader(text));
        try
        {
            parser.Consume<StreamStart>();
            if (parser.Accept<StreamEnd>(out _))
            {
                errors.Fatal(0, 0, ErrorCode.Empty, string.Empty, "내용이 없다");
                return null;
            }

            parser.Consume<DocumentStart>();
            var root = ReadNode(parser, errors, depth: 1, path: string.Empty);
            parser.Consume<DocumentEnd>();
            if (root is YamlScalar { IsNull: true })
            {
                errors.Fatal(0, 0, ErrorCode.Empty, string.Empty, "내용이 없다");
                return null;
            }

            // 둘째 문서는 읽지 않는다. 첫 문서는 계속 검사한다.
            if (parser.Accept<DocumentStart>(out var second))
            {
                var (line, column) = Position(second.Start);
                errors.Add(line, column, ErrorCode.MultiDocument, string.Empty, "문서가 둘 이상이다. 하나만 쓴다");
            }
            else
            {
                parser.Consume<StreamEnd>();
            }

            return root;
        }
        catch (YamlException ex)
        {
            // 메시지는 YamlDotNet의 영어 문장 그대로다(스펙 4.9).
            var (line, column) = Position(ex.Start);
            errors.Fatal(line, column, ErrorCode.Syntax, string.Empty, "YAML 문법 오류: " + ex.Message);
            return null;
        }
        catch (DepthExceededException ex)
        {
            errors.Fatal(ex.Line, ex.Column, ErrorCode.Depth, ex.NodePath, FormattableString.Invariant($"너무 깊다. {MaxDepth}단계까지 쓴다"));
            return null;
        }
    }

    // path는 오류에 쓰는 일반 경로다. 매핑은 "a.b", 목록 항목은 "a[1]"(1부터).
    private static YamlNode ReadNode(IParser parser, ErrorSink errors, int depth, string path)
    {
        if (parser.TryConsume<AnchorAlias>(out var alias))
        {
            var (aliasLine, aliasColumn) = Position(alias.Start);
            errors.Add(aliasLine, aliasColumn, ErrorCode.Alias, path, "별칭(*)은 쓰지 않는다");
            return new YamlAlias(aliasLine, aliasColumn);
        }

        // TryConsume이 다음 이벤트를 읽어 두었으므로 Current는 null이 아니다.
        var current = parser.Current!;
        var (line, column) = Position(current.Start);
        if (current is NodeEvent node)
        {
            // 앵커가 있는 노드도 계속 검사한다. 별칭만 더 보지 않는다.
            if (!node.Anchor.IsEmpty)
            {
                errors.Add(line, column, ErrorCode.Alias, path, "앵커(&)는 쓰지 않는다");
            }

            if (!node.Tag.IsEmpty)
            {
                errors.Add(line, column, ErrorCode.Tag, path, "태그(!)는 쓰지 않는다");
            }
        }

        // 넘으면 바로 멈춘다. 건너뛰기(SkipThisAndNestedEvents)는 깊은 흐름 중첩에서 아주 느리다(스펙 4.7).
        if (depth > MaxDepth && current is not Scalar)
        {
            throw new DepthExceededException(line, column, path);
        }

        if (parser.TryConsume<Scalar>(out var scalar))
        {
            return new YamlScalar(line, column, scalar.Value, scalar.Style == ScalarStyle.Plain);
        }

        if (parser.TryConsume<SequenceStart>(out _))
        {
            var items = new List<YamlNode>();
            while (!parser.TryConsume<SequenceEnd>(out _))
            {
                items.Add(ReadNode(parser, errors, depth + 1, ItemPath(path, items.Count + 1)));
            }

            return new YamlSequence(line, column, items);
        }

        parser.Consume<MappingStart>();
        var entries = new List<YamlEntry>();
        var firstLines = new Dictionary<string, int>(StringComparer.Ordinal);
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = ReadNode(parser, errors, depth + 1, path);
            var childPath = key is YamlScalar named ? ChildPath(path, named.Value) : path;
            var value = ReadNode(parser, errors, depth + 1, childPath);
            if (key is not YamlScalar scalarKey)
            {
                if (key is not YamlAlias)
                {
                    errors.Add(key.Line, key.Column, ErrorCode.Type, path, "키는 문자열이어야 한다");
                }

                continue;
            }

            // 같은 키는 둘째 키 위치에 적는다. 첫 값을 쓴다.
            if (firstLines.TryGetValue(scalarKey.Value, out var firstLine))
            {
                errors.Add(
                    scalarKey.Line,
                    scalarKey.Column,
                    ErrorCode.DuplicateKey,
                    childPath,
                    FormattableString.Invariant($"중복된 키다 (처음: {firstLine}줄)"));
                continue;
            }

            firstLines.Add(scalarKey.Value, scalarKey.Line);
            entries.Add(new YamlEntry(scalarKey, value));
        }

        return new YamlMapping(line, column, entries);
    }

    public static string ChildPath(string path, string key) => path.Length == 0 ? key : path + "." + key;

    public static string ItemPath(string path, int number) => FormattableString.Invariant($"{path}[{number}]");

    // YamlDotNet의 Mark.Line·Column은 long이다. case.yaml 크기에서는 int로 충분하다.
    private static (int Line, int Column) Position(Mark mark) => ((int)mark.Line, (int)mark.Column);

    // 깊이 초과. 읽기를 바로 멈추려고 던진다. 이 파일 밖으로 나가지 않는다.
    private sealed class DepthExceededException(int line, int column, string nodePath) : Exception
    {
        public int Line { get; } = line;

        public int Column { get; } = column;

        public string NodePath { get; } = nodePath;
    }
}
