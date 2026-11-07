using System.Globalization;
using System.Text;

namespace Scorecard.Core.Synthetic;

// 합성 CSV 바이트. docs/spec/synthetic-examples.md 3.3.
// 머리 행 하나 + 행. 구분자 ','. 따옴표를 쓰지 않는다. 줄바꿈 "\n", 마지막 줄 뒤에도 "\n" 하나.
// 값은 ASCII 인쇄 문자(0x20~0x7E)만 쓰고 ','·'"'·빈 값·앞뒤 공백이 없다. 그래서 UTF-8 바이트 = ASCII 바이트이고 BOM이 없다.
// 규칙에 맞지 않는 값은 ArgumentException이다. 고쳐 쓰지 않는다.
public static class SyntheticCsv
{
    public const string ReferenceFolder = "reference";
    public const string CandidateFolder = "candidate";

    public static byte[] Render(IReadOnlyList<string> header, IReadOnlyList<SyntheticRow> rows)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(rows);

        var text = new StringBuilder();
        AppendLine(text, header);
        foreach (var row in rows)
        {
            if (row.Values.Count + 1 != header.Count)
            {
                var message = string.Create(
                    CultureInfo.InvariantCulture,
                    $"칸 수가 머리 행과 다르다: 행 {row.Key}는 키 포함 {row.Values.Count + 1}칸, 머리 행은 {header.Count}칸.");
                throw new ArgumentException(message, nameof(rows));
            }

            AppendLine(text, [row.Key, .. row.Values]);
        }

        return Encoding.ASCII.GetBytes(text.ToString());
    }

    // 칸 하나가 3.3 값 규칙에 맞는지 본다. 빈 값과 앞뒤 공백도 받지 않는다(가운데 공백은 된다).
    public static void CheckCell(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0 || value[0] == ' ' || value[^1] == ' ' || value.Any(ch => ch < 0x20 || ch > 0x7E || ch is ',' or '"'))
        {
            throw new ArgumentException($"CSV 값은 ASCII 인쇄 문자만 쓰고 빈 값·앞뒤 공백·','·'\"'가 없어야 한다: '{Show(value)}'", nameof(value));
        }
    }

    private static void AppendLine(StringBuilder text, IReadOnlyList<string> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            CheckCell(cells[i]);
            if (i > 0)
            {
                text.Append(',');
            }

            text.Append(cells[i]);
        }

        text.Append('\n');
    }

    // 메시지용. 제어 문자는 \uXXXX로 보인다. 나머지(한글 포함)는 그대로 둔다.
    private static string Show(string value)
    {
        var shown = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch < 0x20 || ch == 0x7F)
            {
                shown.Append(CultureInfo.InvariantCulture, $"\\u{(int)ch:X4}");
            }
            else
            {
                shown.Append(ch);
            }
        }

        return shown.ToString();
    }
}
