using System.Globalization;
using System.Text;

namespace Scorecard.Core.Comparison;

// 대조의 값·문구를 이 파일 한 곳에 둔다(docs/spec/metrics.md 4.9).
// 원본은 스펙 4.5 숫자 표, 4.6 텍스트 정규화, 4.7 오류 코드 표, 4.8 퍼센트 규칙이다.
// 실행 중에 스펙 문서를 읽지 않는다. 테스트가 스펙 표를 읽어 Core 동작과 대조한다.
internal static class CompareRules
{
    // 4.7 오류 코드.
    public const string CodeFile = "file";
    public const string CodePattern = "pattern";
    public const string CodeOverlap = "overlap";
    public const string CodeEncoding = "encoding";
    public const string CodeEmpty = "empty";
    public const string CodeCsvSyntax = "csv-syntax";
    public const string CodeFieldCount = "field-count";
    public const string CodeColumn = "column";
    public const string CodeDuplicateColumn = "duplicate-column";
    public const string CodeEmptyKey = "empty-key";
    public const string CodeDuplicateKey = "duplicate-key";
    public const string CodeNumber = "number";
    public const string CodeNoRows = "no-rows";

    // 4.8 퍼센트: 소수 첫째 자리. 0.05는 올린다(0에서 먼 쪽).
    public const int PercentDigits = 1;

    // 4.5 숫자 모양. 앞뒤 공백은 정규화(4.6)에서 이미 뗐다.
    public const NumberStyles NumberStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    // 4.6 텍스트 정규화: 줄바꿈(CRLF·CR)을 LF로, 앞뒤 공백(char.IsWhiteSpace)을 뗀다. 대소문자·유니코드 정규화는 하지 않는다.
    public static string Normalize(string cell) =>
        cell.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();

    // 4.5 숫자 읽기. InvariantCulture. 넘침·모양 밖은 false.
    public static bool TryNumber(string normalized, out decimal value) =>
        decimal.TryParse(normalized, NumberStyle, CultureInfo.InvariantCulture, out value);

    // 메시지에 넣는 값. 제어 문자는 \uXXXX로 보인다. 64자를 넘으면 자르고 "…"를 붙인다.
    public static string Show(string value)
    {
        var shown = new StringBuilder();
        foreach (var ch in value.Length > 64 ? value[..64] : value)
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

        if (value.Length > 64)
        {
            shown.Append('…');
        }

        return shown.ToString();
    }
}
