using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Scorecard.Core.Cases;

// 스파이크 후보 C: YamlDotNet 저수준 Parser 이벤트로 작은 트리를 만든다.
// 객체 역직렬화(YamlDotNet.Serialization)를 쓰지 않는다. 리플렉션으로 타입을 만들지 않는다.
// 앵커·별칭·태그·중복 키·깊이 초과는 여기서 오류로 적는다.
internal abstract record YNode(int Line, int Column);

// Plain은 따옴표·블록 없이 쓴 값인가. null 판정과 숫자·불리언 판정에 쓴다.
internal sealed record YScalar(int Line, int Column, string Value, bool Plain) : YNode(Line, Column)
{
    // YAML 1.2 core 스키마의 null: 따옴표 없는 빈 값, ~, null, Null, NULL.
    public bool IsNull => Plain && Value is "" or "~" or "null" or "Null" or "NULL";
}

internal sealed record YMapping(int Line, int Column, IReadOnlyList<KeyValuePair<YScalar, YNode>> Entries) : YNode(Line, Column);

internal sealed record YSequence(int Line, int Column, IReadOnlyList<YNode> Items) : YNode(Line, Column);

// 오류로 적고 건너뛴 자리. 스키마 검사는 이 노드에 오류를 더 내지 않는다.
internal sealed record YInvalid(int Line, int Column) : YNode(Line, Column);

internal static class YamlTree
{
    // YamlDotNet 18의 Mark.Line·Column은 long이다. case.yaml 크기에서는 int로 충분하다.
    // 이보다 깊으면 오류. case.yaml의 실제 깊이는 4(루트 → steps → 항목 → answers)다.
    public const int MaxDepth = 16;

    // 문서 하나를 읽는다. 문법 오류·깊이 초과면 그 한 건만 남기고 null.
    public static YNode? Read(string text, ErrorSink sink)
    {
        var parser = new Parser(new StringReader(text));
        var collected = new ErrorSink(sink.Source);
        try
        {
            parser.Consume<StreamStart>();
            if (parser.Accept<StreamEnd>(out _))
            {
                sink.Add(0, 0, "empty", "", "내용이 없다");
                return null;
            }

            parser.Consume<DocumentStart>();
            var root = ReadNode(parser, collected, 1, "");
            parser.Consume<DocumentEnd>();
            if (parser.Accept<DocumentStart>(out var second))
            {
                collected.Add((int)second.Start.Line, (int)second.Start.Column, "multi-document", "", "문서가 둘 이상이다. 하나만 쓴다");
            }
            else
            {
                parser.Consume<StreamEnd>();
            }

            sink.AddRange(collected);
            if (root is YScalar { IsNull: true })
            {
                sink.Add(0, 0, "empty", "", "내용이 없다");
                return null;
            }

            return root;
        }
        catch (YamlException ex)
        {
            sink.Add((int)ex.Start.Line, (int)ex.Start.Column, "syntax", "", "YAML 문법 오류: " + ex.Message);
            return null;
        }
        catch (DepthExceededException ex)
        {
            sink.Add(ex.Line, ex.Column, "depth", ex.NodePath, $"너무 깊다. {MaxDepth}단계까지 쓴다");
            return null;
        }
    }

    private sealed class DepthExceededException(int line, int column, string path) : Exception
    {
        public int Line { get; } = line;

        public int Column { get; } = column;

        public string NodePath { get; } = path;
    }

    // path는 오류에 쓰는 일반 경로. 매핑은 "a.b", 목록 항목은 "a[1]"(1부터).
    private static YNode ReadNode(IParser parser, ErrorSink sink, int depth, string path)
    {
        if (parser.TryConsume<AnchorAlias>(out var alias))
        {
            sink.Add((int)alias.Start.Line, (int)alias.Start.Column, "alias", path, "별칭(*)은 쓰지 않는다");
            return new YInvalid((int)alias.Start.Line, (int)alias.Start.Column);
        }

        var start = parser.Current!.Start;
        if (parser.Current is NodeEvent node)
        {
            if (!node.Anchor.IsEmpty)
            {
                sink.Add((int)start.Line, (int)start.Column, "alias", path, "앵커(&)는 쓰지 않는다");
            }

            if (!node.Tag.IsEmpty)
            {
                sink.Add((int)start.Line, (int)start.Column, "tag", path, "태그(!)는 쓰지 않는다");
            }
        }

        // 넘으면 바로 멈춘다. 건너뛰기(SkipThisAndNestedEvents)는 깊은 흐름 중첩에서 아주 느리다(스파이크 S3).
        if (depth > MaxDepth && parser.Current is not Scalar)
        {
            throw new DepthExceededException((int)start.Line, (int)start.Column, path);
        }

        if (parser.TryConsume<Scalar>(out var scalar))
        {
            return new YScalar((int)start.Line, (int)start.Column, scalar.Value, scalar.Style == ScalarStyle.Plain);
        }

        if (parser.TryConsume<SequenceStart>(out _))
        {
            var items = new List<YNode>();
            while (!parser.TryConsume<SequenceEnd>(out _))
            {
                items.Add(ReadNode(parser, sink, depth + 1, $"{path}[{items.Count + 1}]"));
            }

            return new YSequence((int)start.Line, (int)start.Column, items);
        }

        parser.Consume<MappingStart>();
        var entries = new List<KeyValuePair<YScalar, YNode>>();
        var firstLine = new Dictionary<string, int>(StringComparer.Ordinal);
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var keyNode = ReadNode(parser, sink, depth + 1, path);
            var childPath = keyNode is YScalar k ? Join(path, k.Value) : path;
            var value = ReadNode(parser, sink, depth + 1, childPath);
            if (keyNode is not YScalar key)
            {
                if (keyNode is not YInvalid)
                {
                    sink.Add(keyNode.Line, keyNode.Column, "type", path, "키는 문자열이어야 한다");
                }

                continue;
            }

            if (firstLine.TryGetValue(key.Value, out var first))
            {
                sink.Add(key.Line, key.Column, "duplicate-key", childPath, $"중복된 키다 (처음: {first}줄)");
                continue;
            }

            firstLine.Add(key.Value, key.Line);
            entries.Add(new KeyValuePair<YScalar, YNode>(key, value));
        }

        return new YMapping((int)start.Line, (int)start.Column, entries);
    }

    private static string Join(string path, string key) => path.Length == 0 ? key : path + "." + key;
}
