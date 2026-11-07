using System.Globalization;
using System.Text;
using Scorecard.Core.Cases;

namespace Scorecard.Core.Comparison;

// CSV 바이트 → 행. docs/spec/metrics.md 4.2.
// UTF-8만 받는다(BOM 하나는 뗀다). 줄바꿈은 LF·CRLF·CR 모두 행 구분이다. 구분자는 ','.
// 따옴표: 칸이 '"'로 시작하면 따옴표 칸이다. 안의 '""'는 '"' 하나. 닫는 따옴표 뒤에는 ','나 줄바꿈만 온다.
// 따옴표로 시작하지 않는 칸 안의 '"'는 글자 그대로다.
// 모든 칸이 빈(공백뿐 포함) 행은 건너뛴다. 첫 번째 남은 행이 머리 행이다.
internal static class CsvText
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    // 읽은 파일. Records에는 머리 행이 없다. 오류(치명)가 있으면 null을 돌려준다.
    public static CsvFile? Read(SourceFile file, Action<CaseError> fatal)
    {
        var text = Decode(file, fatal);
        if (text is null)
        {
            return null;
        }

        var records = new List<CsvRecord>();
        var i = 0;
        var line = 1;
        var col = 1;
        while (i < text.Length)
        {
            var startLine = line;
            var cells = new List<CsvCell>();
            while (true)
            {
                var cellLine = line;
                var cellCol = col;
                string value;
                if (i < text.Length && text[i] == '"')
                {
                    var openLine = line;
                    var openCol = col;
                    i++;
                    col++;
                    var sb = new StringBuilder();
                    while (true)
                    {
                        if (i >= text.Length)
                        {
                            fatal(Error(file, openLine, openCol, CompareRules.CodeCsvSyntax, string.Empty, "따옴표가 닫히지 않았다"));
                            return null;
                        }

                        var ch = text[i];
                        if (ch == '"')
                        {
                            if (i + 1 < text.Length && text[i + 1] == '"')
                            {
                                sb.Append('"');
                                i += 2;
                                col += 2;
                                continue;
                            }

                            i++;
                            col++;
                            break;
                        }

                        if (ch is '\r' or '\n')
                        {
                            var crlf = ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n';
                            sb.Append(crlf ? "\r\n" : ch.ToString());
                            i += crlf ? 2 : 1;
                            line++;
                            col = 1;
                            continue;
                        }

                        sb.Append(ch);
                        i++;
                        col++;
                    }

                    if (i < text.Length && text[i] is not (',' or '\r' or '\n'))
                    {
                        fatal(Error(file, line, col, CompareRules.CodeCsvSyntax, string.Empty, "닫는 따옴표 뒤에 다른 글자가 있다"));
                        return null;
                    }

                    value = sb.ToString();
                }
                else
                {
                    var start = i;
                    while (i < text.Length && text[i] is not (',' or '\r' or '\n'))
                    {
                        i++;
                        col++;
                    }

                    value = text[start..i];
                }

                cells.Add(new CsvCell(cellLine, cellCol, value));
                if (i < text.Length && text[i] == ',')
                {
                    i++;
                    col++;
                    continue;
                }

                break;
            }

            if (i < text.Length)
            {
                i += text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                line++;
                col = 1;
            }

            if (cells.Any(c => CompareRules.Normalize(c.Text).Length > 0))
            {
                records.Add(new CsvRecord(startLine, cells));
            }
        }

        if (records.Count == 0)
        {
            fatal(Error(file, 0, 0, CompareRules.CodeEmpty, string.Empty, "머리 행이 없다"));
            return null;
        }

        return new CsvFile(file.Path, records[0], records.Skip(1).ToArray());
    }

    public static CaseError Error(SourceFile file, int line, int column, string code, string path, string message) =>
        new(file.Path, line, column, code, path, message);

    // case-schema 4.8과 같은 규칙. UTF-16 BOM, 잘못된 바이트는 encoding 오류.
    private static string? Decode(SourceFile file, Action<CaseError> fatal)
    {
        var content = file.Content;
        if (content is [0xFF, 0xFE, ..] or [0xFE, 0xFF, ..])
        {
            fatal(Error(file, 1, 0, CompareRules.CodeEncoding, string.Empty, "UTF-16이다. UTF-8로 저장한다"));
            return null;
        }

        var start = content is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
        try
        {
            return StrictUtf8.GetString(content, start, content.Length - start);
        }
        catch (DecoderFallbackException ex)
        {
            var offset = Math.Clamp(start + ex.Index, 0, content.Length);
            var line = 1 + content.AsSpan(0, offset).Count((byte)'\n');
            fatal(Error(file, line, 0, CompareRules.CodeEncoding, string.Empty, string.Create(CultureInfo.InvariantCulture, $"UTF-8이 아니다 (바이트 {offset})")));
            return null;
        }
    }
}

// 칸 하나. Line·Column은 칸이 시작한 위치(1부터, 열은 UTF-16 단위). Text는 따옴표를 푼 원래 글자다.
internal sealed record CsvCell(int Line, int Column, string Text);

// 행 하나. Line은 행이 시작한 물리 줄이다.
internal sealed record CsvRecord(int Line, IReadOnlyList<CsvCell> Cells);

internal sealed record CsvFile(string Path, CsvRecord Header, IReadOnlyList<CsvRecord> Rows);
