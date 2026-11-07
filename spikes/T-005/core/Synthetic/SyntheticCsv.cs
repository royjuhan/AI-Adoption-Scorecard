using System.Text;

namespace Scorecard.Core.Synthetic;

// 합성 CSV 바이트(T-005 스파이크 후보).
// 머리 행 하나 + 행. 구분자 ','. 따옴표를 쓰지 않는다. 줄바꿈 "\n", 마지막 줄 뒤에도 "\n".
// 값은 ASCII 인쇄 문자(0x20~0x7E)만, ','·'"' 없음. 그래서 UTF-8 바이트 = ASCII 바이트이고 BOM이 없다.
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
                throw new ArgumentException($"{row.Key}: 칸 수가 머리 행과 다르다.", nameof(rows));
            }

            AppendLine(text, [row.Key, .. row.Values]);
        }

        return Encoding.ASCII.GetBytes(text.ToString());
    }

    // 셀 하나가 규칙에 맞는지. 빈 값도 받지 않는다.
    public static void CheckCell(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0 || value.Any(ch => ch < 0x20 || ch > 0x7E || ch is ',' or '"'))
        {
            throw new ArgumentException($"CSV 값은 ASCII 인쇄 문자만, ','·'\"' 없이 쓴다: '{value}'", nameof(value));
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
}
