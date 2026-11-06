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
        var parser = new GuardedParser(new Parser(new StringReader(text)));
        try
        {
            parser.Consume<StreamStart>();
            if (parser.Accept<StreamEnd>(out _))
            {
                errors.Fatal(0, 0, ErrorCode.Empty, string.Empty, "내용이 없다");
                return null;
            }

            parser.Consume<DocumentStart>();
            var root = ReadNode(parser, errors, depth: 1, ErrorPath.Root);
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
        catch (ParserFaultException ex)
        {
            // YamlException이 아닌 파서 예외. 위치가 없다. Message는 무엇이 담길지 몰라 쓰지 않고 타입 이름만 쓴다.
            errors.Fatal(0, 0, ErrorCode.Syntax, string.Empty, "YAML 문법 오류: 파서가 읽지 못했다: " + ex.FaultTypeName);
            return null;
        }
        catch (DepthExceededException ex)
        {
            errors.Fatal(ex.Line, ex.Column, ErrorCode.Depth, ex.NodePath, FormattableString.Invariant($"너무 깊다. {MaxDepth}단계까지 쓴다"));
            return null;
        }
    }

    // path는 오류에 쓰는 일반 경로다. 매핑은 "a.b", 목록 항목은 "a[1]"(1부터).
    // 문자열은 오류를 적을 때만 만든다(ErrorPath).
    private static YamlNode ReadNode(IParser parser, ErrorSink errors, int depth, ErrorPath path)
    {
        if (parser.TryConsume<AnchorAlias>(out var alias))
        {
            var (aliasLine, aliasColumn) = Position(alias.Start);
            errors.Add(aliasLine, aliasColumn, ErrorCode.Alias, path.ToString(), "별칭(*)은 쓰지 않는다");
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
                errors.Add(line, column, ErrorCode.Alias, path.ToString(), "앵커(&)는 쓰지 않는다");
            }

            if (!node.Tag.IsEmpty)
            {
                errors.Add(line, column, ErrorCode.Tag, path.ToString(), "태그(!)는 쓰지 않는다");
            }
        }

        // 넘으면 바로 멈춘다. 건너뛰기(SkipThisAndNestedEvents)는 깊은 흐름 중첩에서 아주 느리다(스펙 4.7).
        if (depth > MaxDepth && current is not Scalar)
        {
            throw new DepthExceededException(line, column, path.ToString());
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
                items.Add(ReadNode(parser, errors, depth + 1, path.Item(items.Count + 1)));
            }

            return new YamlSequence(line, column, items);
        }

        parser.Consume<MappingStart>();
        var entries = new List<YamlEntry>();
        var firstLines = new Dictionary<string, int>(StringComparer.Ordinal);
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = ReadNode(parser, errors, depth + 1, path);
            var childPath = key is YamlScalar named ? path.Child(named.Value) : path;
            var value = ReadNode(parser, errors, depth + 1, childPath);
            if (key is not YamlScalar scalarKey)
            {
                if (key is not YamlAlias)
                {
                    errors.Add(key.Line, key.Column, ErrorCode.Type, path.ToString(), "키는 문자열이어야 한다");
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
                    childPath.ToString(),
                    FormattableString.Invariant($"중복된 키다 (처음: {firstLine}줄)"));
                continue;
            }

            firstLines.Add(scalarKey.Value, scalarKey.Line);
            entries.Add(new YamlEntry(scalarKey, value));
        }

        return new YamlMapping(line, column, entries);
    }

    // 경로 문자열 규칙. ErrorPath.ToString()도 같은 규칙이다(앞이 비면 '.' 없이 키만).
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

    // 오류 경로. 부모를 가리키는 조각이다. 노드마다 문자열을 만들지 않는다.
    // 노드마다 만들면 경로 길이 × 노드 수만큼 든다(긴 키 아래의 넓은 목록).
    // 문자열은 처음 부를 때 한 번 만들어 저장하고 다시 쓴다(T-003 reviewer 2회차 N1).
    // 같은 경로의 오류가 많아도 문자열은 하나다(긴 키 아래 별칭 키 여러 개).
    // 사슬 길이는 깊이를 넘지 않는다(MaxDepth). ToString이 거슬러 올라가는 거리도 그만큼이다.
    // 스레드 안전은 따지지 않는다. Root 밖의 조각은 Read 한 번 안에서만 만들고 쓴다.
    // Read는 부를 때마다 새 Parser를 만들고 한 스레드에서 끝까지 읽는다. 조각은 Read 밖으로 나가지 않는다.
    // 여러 Read가 함께 쓰는 Root는 생성자에서 빈 문자열을 넣어 둔다. 그 뒤로 바뀌지 않는다.
    private sealed class ErrorPath
    {
        public static readonly ErrorPath Root = new(null, null, 0);

        private readonly ErrorPath? parent;

        // 매핑 키. null이면 목록 항목이고 number가 번호(1부터)다.
        private readonly string? key;
        private readonly int number;

        // 만든 경로 문자열. null이면 아직 만들지 않았다.
        private string? text;

        private ErrorPath(ErrorPath? parent, string? key, int number)
        {
            this.parent = parent;
            this.key = key;
            this.number = number;
            text = parent is null ? string.Empty : null;
        }

        public ErrorPath Child(string childKey) => new(this, childKey, 0);

        public ErrorPath Item(int itemNumber) => new(this, null, itemNumber);

        // 뿌리부터 ChildPath·ItemPath를 차례로 붙인 것과 같은 문자열이다.
        public override string ToString() => text ??= Build();

        // 문자열이 저장된 가장 가까운 조상부터 조각을 모아 한 번에 잇는다. Root는 늘 저장돼 있다.
        // 결과는 딱 맞는 크기로 한 번만 만든다. 거쳐 간 조상의 문자열은 만들지도 저장하지도 않는다.
        // 조상마다 저장하면 깊은 사슬 끝의 오류 하나가 조상 수만큼 긴 문자열을 더 만든다(긴 키 아래 서로 다른 깊은 사슬).
        private string Build()
        {
            var chain = new Stack<ErrorPath>();
            var known = this;
            while (known.text is null)
            {
                chain.Push(known);
                known = known.parent!;
            }

            var pieces = new List<string>((2 * chain.Count) + 1);
            if (known.text.Length > 0)
            {
                pieces.Add(known.text);
            }

            // Stack은 나중에 넣은 것(뿌리 쪽)부터 내준다.
            // ChildPath·ItemPath와 같은 규칙이다. 항목은 "[번호]", 키는 앞이 비면 '.' 없이 키만.
            var length = known.text.Length;
            foreach (var step in chain)
            {
                if (step.key is null)
                {
                    var item = ItemPath(string.Empty, step.number);
                    pieces.Add(item);
                    length += item.Length;
                }
                else
                {
                    if (length > 0)
                    {
                        pieces.Add(".");
                        length++;
                    }

                    pieces.Add(step.key);
                    length += step.key.Length;
                }
            }

            // 배열판 Concat은 길이를 먼저 더해 한 번만 할당한다. 조각이 하나면 그 문자열을 그대로 돌려준다.
            return string.Concat(pieces.ToArray());
        }
    }

    // YamlException이 아닌 파서 예외를 감싼다. Read가 syntax 한 건으로 바꾼다. 이 파일 밖으로 나가지 않는다.
    private sealed class ParserFaultException(Exception fault) : Exception(null, fault)
    {
        public string FaultTypeName { get; } = fault.GetType().Name;
    }

    // 이벤트 읽기(MoveNext·Current)만 감싼다. 거기서 난 YamlException 밖의 예외를 ParserFaultException으로 바꾼다.
    // 우리 코드에서 난 예외(버그)는 감싸지 않고 그대로 나간다.
    // 메모리 부족은 입력이 아니라 실행 환경 탓이라 바꾸지 않는다. 바꾸면 같은 입력이 컴퓨터마다 다른 결과가 된다.
    private sealed class GuardedParser(IParser inner) : IParser
    {
        public ParsingEvent? Current
        {
            get
            {
                try
                {
                    return inner.Current;
                }
                catch (Exception ex) when (IsFault(ex))
                {
                    throw new ParserFaultException(ex);
                }
            }
        }

        public bool MoveNext()
        {
            try
            {
                return inner.MoveNext();
            }
            catch (Exception ex) when (IsFault(ex))
            {
                throw new ParserFaultException(ex);
            }
        }

        private static bool IsFault(Exception ex) => ex is not (YamlException or OutOfMemoryException);
    }
}
