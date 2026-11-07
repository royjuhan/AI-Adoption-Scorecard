using System.Text;

namespace Scorecard.Tests.CaseSchema;

// 스펙 4.8 인코딩·줄바꿈. 표 각 행. UTF-8만 받는다.
// 변형 바이트는 커밋하지 않고 정상 샘플 바이트에서 만든다(스펙 7.1). TestData에 \r을 두지 않는다.
// 바이트 N은 파일 처음부터 0으로 센 위치(BOM 포함). encoding 오류의 줄은 앞선 LF 수 + 1, 열은 없다.
[Trait("Spec", "case-schema")]
public class CaseEncodingTests
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    private const string Utf16Message = "case.yaml:1: encoding: UTF-16이다. UTF-8로 저장한다";

    // 같은 모델이 나와야 하는 변형. 기준은 BOM 없는 LF.
    [Theory]
    [InlineData("bom")]
    [InlineData("crlf")]
    [InlineData("cr")]
    [InlineData("bom-crlf")]
    public void Bom_and_line_break_variants_match_lf_byte_for_byte(string variant)
    {
        var lf = CaseInput.Sample(SpecSamples.ValidFull);
        var text = Encoding.UTF8.GetString(lf);
        byte[] bytes = variant switch
        {
            "bom" => [.. Bom, .. lf],
            "crlf" => CaseInput.Bytes(text.Replace("\n", "\r\n")),
            "cr" => CaseInput.Bytes(text.Replace("\n", "\r")),
            "bom-crlf" => [.. Bom, .. CaseInput.Bytes(text.Replace("\n", "\r\n"))],
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };

        CaseInput.Valid(CaseInput.Parse(lf));
        Assert.Equal(ParseResultJson.Render(CaseInput.Parse(lf)), ParseResultJson.Render(CaseInput.Parse(bytes)));
    }

    // 오류 표본도 BOM·CRLF에서 같은 오류 줄(줄 번호 포함)이 나온다.
    [Fact]
    public void Bom_crlf_error_sample_gives_same_error_lines()
    {
        var text = CaseInput.SampleText(SpecSamples.MissingRequired);
        byte[] bytes = [.. Bom, .. CaseInput.Bytes(text.Replace("\n", "\r\n"))];

        CaseInput.AssertErrors(CaseInput.Parse(bytes), SpecSamples.MissingRequiredErrors);
    }

    // UTF-16 BOM(LE FF FE, BE FE FF)은 encoding 한 건. StreamReader처럼 UTF-16으로 읽어 주지 않는다.
    [Fact]
    public void Utf16_little_endian_bom_is_encoding_error()
    {
        var text = CaseInput.SampleText(SpecSamples.ValidFull);
        byte[] bytes = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)];

        CaseInput.AssertErrors(CaseInput.Parse(bytes), Utf16Message);
    }

    [Fact]
    public void Utf16_big_endian_bom_is_encoding_error()
    {
        var text = CaseInput.SampleText(SpecSamples.ValidFull);
        byte[] bytes = [.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(text)];

        CaseInput.AssertErrors(CaseInput.Parse(bytes), Utf16Message);
    }

    // UTF-32 BE BOM(00 00 FE FF): 앞 두 바이트(NUL)는 UTF-8로 맞고, 바이트 2(FE)에서 틀린다.
    [Fact]
    public void Utf32_big_endian_bom_is_invalid_utf8_at_byte_two()
    {
        var utf32 = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
        byte[] bytes = [.. utf32.GetPreamble(), .. utf32.GetBytes("name: a\ntask: t\nsteps:\n  - name: s\n")];

        CaseInput.AssertErrors(CaseInput.Parse(bytes), "case.yaml:1: encoding: UTF-8이 아니다 (바이트 2)");
    }

    // 잘못된 UTF-8(CP949 한글 "값" = B0 AA)을 3줄 값 자리에 넣는다. 자동 변환하지 않는다(승인 결정 8).
    // "name: sample-cp949\n"(19) + "task: t\n"(8) + "before: "(8) = 바이트 35.
    [Fact]
    public void Cp949_bytes_are_invalid_utf8_with_line_and_byte()
    {
        byte[] bytes = [.. CaseInput.Bytes("name: sample-cp949\ntask: t\nbefore: "), 0xB0, 0xAA, .. CaseInput.Bytes("\nsteps:\n  - name: s\n")];

        CaseInput.AssertErrors(CaseInput.Parse(bytes), "case.yaml:3: encoding: UTF-8이 아니다 (바이트 35)");
    }

    // BOM 다음 잘못된 바이트. 바이트 수는 BOM 3바이트를 포함한다: 3 + "name: a\n"(8) + "task: "(6) = 17.
    [Fact]
    public void Byte_offset_includes_bom()
    {
        byte[] bytes = [.. Bom, .. CaseInput.Bytes("name: a\ntask: "), 0xC3, 0x28, (byte)'\n'];

        CaseInput.AssertErrors(CaseInput.Parse(bytes), "case.yaml:2: encoding: UTF-8이 아니다 (바이트 17)");
    }

    // 첫 바이트부터 틀리면 1줄, 바이트 0.
    [Fact]
    public void Invalid_first_byte_is_line_one_byte_zero()
    {
        CaseInput.AssertErrors(CaseInput.Parse([0xFF, (byte)'\n']), "case.yaml:1: encoding: UTF-8이 아니다 (바이트 0)");
    }

    // 한글이 있는 UTF-16(BOM 없음): "# 합" → 23 00 20 00 69 D5 … 바이트 5(D5 뒤가 이어짐 바이트가 아님)에서 틀린다.
    [Fact]
    public void Utf16_without_bom_with_korean_is_invalid_utf8()
    {
        var bytes = Encoding.Unicode.GetBytes(CaseInput.SampleText(SpecSamples.ValidFull));

        CaseInput.AssertErrors(CaseInput.Parse(bytes), "case.yaml:1: encoding: UTF-8이 아니다 (바이트 5)");
    }

    // ASCII만 있는 UTF-16(BOM 없음)은 UTF-8로 맞는 바이트(NUL 섞임)라 encoding이 아니라 syntax다.
    [Fact]
    public void Utf16_without_bom_ascii_only_is_syntax_error()
    {
        var bytes = Encoding.Unicode.GetBytes("name: a\ntask: t\nsteps:\n  - name: s\n");

        AssertSyntaxAt(bytes, "case.yaml:1:3: ");
    }

    // BOM 둘: 둘째 BOM이 글자(U+FEFF)로 남아 첫 줄 주석이 깨진다. syntax (2:5).
    [Fact]
    public void Double_bom_leaves_second_bom_as_character()
    {
        byte[] bytes = [.. Bom, .. Bom, .. CaseInput.Sample(SpecSamples.ValidFull)];

        AssertSyntaxAt(bytes, "case.yaml:2:5: ");
    }

    // 값 안의 NUL 바이트는 syntax (2:9).
    [Fact]
    public void Nul_byte_in_value_is_syntax_error()
    {
        AssertSyntaxAt(CaseInput.Bytes("name: a\ntask: t\0x\nsteps:\n  - name: s\n"), "case.yaml:2:9: ");
    }

    // BOM 하나는 첫 키 이름에 섞이지 않는다(Encoding.UTF8.GetString 함정).
    [Fact]
    public void Single_bom_is_not_part_of_first_key()
    {
        byte[] bytes = [.. Bom, .. CaseInput.Bytes(SpecSamples.Minimal)];

        Assert.Equal("sample-minimal", CaseInput.Valid(CaseInput.Parse(bytes)).Name);
    }

    // syntax 한 건. 위치와 앞부분만 본다. 영어 메시지 본문은 YamlDotNet 버전에 따른다(스펙 8절 6).
    private static void AssertSyntaxAt(byte[] bytes, string position)
    {
        var result = CaseInput.Parse(bytes);
        var line = Assert.Single(CaseInput.Lines(result));

        Assert.StartsWith(position + "syntax: YAML 문법 오류: ", line, StringComparison.Ordinal);
        Assert.Null(result.Case);
    }
}
