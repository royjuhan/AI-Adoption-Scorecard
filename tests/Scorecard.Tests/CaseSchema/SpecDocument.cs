using Scorecard.Tests.Structure;

namespace Scorecard.Tests.CaseSchema;

// 승인된 스펙 문서(docs/spec/case-schema.md)를 읽는다. 표와 코드 블록만 본다.
// Core를 쓰지 않는다. 문서 줄바꿈이 CRLF여도 같은 결과가 나온다(ReadAllLines).
internal static class SpecDocument
{
    public const string CaseSchemaPath = "docs/spec/case-schema.md";
    public const string InstructionsPath = "docs/README-instructions.md";

    // 4.4 기본값 표의 머리 행. 스펙이 "표 모양을 바꾸지 않는다"고 정했다(승인 결정 5).
    public const string DefaultsTableHeader = "| 키 | 질문 | 형식 | 선택지·범위 | 기본값 |";

    // 4.9 오류 코드 표의 머리 행.
    public const string ErrorCodeTableHeader = "| 코드 | 언제 | 위치 | 메시지(스파이크 출력) |";

    // 지시문 3.2 질문 표의 머리 행.
    public const string InstructionsQuestionTableHeader = "| 키 | 질문 | 선택지 | 기본값 |";

    // 머리 행이 header와 정확히 같은 표 하나. 구분 행(| --- |)은 뺀다. 칸은 앞뒤 공백을 지운다.
    // 같은 머리 행이 둘 이상이면 실패한다. 어느 표인지 모호해지기 때문이다.
    public static IReadOnlyList<string[]> Table(string relativePath, string header)
    {
        var lines = File.ReadAllLines(RepoRoot.Combine(relativePath));
        var starts = Enumerable.Range(0, lines.Length).Where(i => lines[i].Trim() == header).ToArray();
        if (starts.Length != 1)
        {
            Assert.Fail($"{relativePath}에서 머리 행 '{header}'이 {starts.Length}번 나온다.");
        }

        var columns = Cells(header).Length;
        var rows = new List<string[]>();
        for (var i = starts[0] + 1; i < lines.Length && lines[i].TrimStart().StartsWith('|'); i++)
        {
            var cells = Cells(lines[i]);
            if (cells.All(c => c.Length > 0 && c.All(ch => ch is '-' or ':')))
            {
                continue;
            }

            if (cells.Length != columns)
            {
                Assert.Fail($"{relativePath} {i + 1}줄의 칸 수가 {cells.Length}다. {columns}이어야 한다.");
            }

            rows.Add(cells);
        }

        return rows;
    }

    // case-schema.md의 heading 행(예: "### 6.1 정상 (전체)")부터 다음 제목 행 전까지의 코드 블록 내용.
    // 블록마다 줄을 LF로 잇고 끝에 LF 하나를 붙인다(샘플 파일과 같은 모양).
    public static IReadOnlyList<string> CodeBlocks(string heading) => CodeBlocks(CaseSchemaPath, heading);

    // 다른 문서의 코드 블록. 규칙은 위와 같다.
    public static IReadOnlyList<string> CodeBlocks(string relativePath, string heading)
    {
        var lines = File.ReadAllLines(RepoRoot.Combine(relativePath));
        var start = Array.FindIndex(lines, l => l.TrimEnd() == heading);
        Assert.True(start >= 0, $"{relativePath}에 제목 '{heading}'이 없다.");

        var blocks = new List<string>();
        List<string>? current = null;
        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (current is null && line.StartsWith('#'))
            {
                break;
            }

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (current is null)
                {
                    current = [];
                }
                else
                {
                    blocks.Add(string.Concat(current.Select(l => l + "\n")));
                    current = null;
                }

                continue;
            }

            current?.Add(line);
        }

        return blocks;
    }

    // 코드 블록 하나를 줄 목록으로. 끝의 빈 줄은 뺀다.
    public static string[] BlockLines(string block) =>
        block.Split('\n').Where(l => l.Length > 0).ToArray();

    // 마크다운 칸의 `코드` 표시를 뗀다.
    public static string Unquote(string cell) => cell.Trim().Trim('`');

    // 표 행의 칸. 앞뒤 '|'를 떼고 '|'로 나눈다. "\|"는 칸 구분이 아니다.
    private static string[] Cells(string row)
    {
        var text = row.Trim();
        text = text.Substring(1, text.Length - (text.EndsWith('|') ? 2 : 1));
        var cells = new List<string>();
        var cell = new System.Text.StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '|')
            {
                cell.Append('|');
                i++;
            }
            else if (text[i] == '|')
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else
            {
                cell.Append(text[i]);
            }
        }

        cells.Add(cell.ToString().Trim());
        return cells.ToArray();
    }
}

// 4.4 기본값 표의 한 행. 칸을 그대로 담는다(Key·Default는 `표시를 뗀 값).
internal sealed record DefaultRow(string Key, string Question, string Kind, string Choices, string Default)
{
    // 4.4 표를 순서대로 읽는다.
    public static IReadOnlyList<DefaultRow> ReadAll() =>
        SpecDocument.Table(SpecDocument.CaseSchemaPath, SpecDocument.DefaultsTableHeader)
            .Select(c => new DefaultRow(SpecDocument.Unquote(c[0]), c[1], c[2], c[3], SpecDocument.Unquote(c[4])))
            .ToArray();

    public bool IsEnum => Kind == "열거";

    // 열거 선택지. "table / fixed_form" → ["table", "fixed_form"]. 열거가 아니면 빈 배열.
    public string[] ChoiceList => IsEnum ? Choices.Split(" / ").Select(s => s.Trim()).ToArray() : [];
}
