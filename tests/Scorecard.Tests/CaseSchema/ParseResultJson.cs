using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Scorecard.Core.Cases;

namespace Scorecard.Tests.CaseSchema;

// 파서 결과를 바이트로 바꾼다. 스펙 4.11의 G2·G3 어댑터 형식이다.
// { "case": <모델 또는 null>, "errors": [<ToString() 문자열>…] }
// System.Text.Json, 들여쓰기, 줄바꿈 "\n", UnsafeRelaxedJsonEscaping, UTF-8(BOM 없음), 끝에 "\n".
// 테스트 쪽 코드라 G1 대상이 아니다. 모델 비교(BOM·CRLF가 LF와 같은가 등)에도 쓴다.
internal static class ParseResultJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static byte[] Render(CaseParseResult result)
    {
        var payload = new { @case = result.Case, errors = result.Errors.Select(e => e.ToString()).ToArray() };
        return Utf8NoBom.GetBytes(JsonSerializer.Serialize(payload, Options) + "\n");
    }

    public static string RenderText(CaseParseResult result) => Utf8NoBom.GetString(Render(result));
}
