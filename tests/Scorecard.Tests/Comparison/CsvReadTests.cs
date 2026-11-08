using System.Text;
using Scorecard.Core.Comparison;

namespace Scorecard.Tests.Comparison;

// 스펙 4.2 CSV 읽기(승인 질문 4). 카드 수용 기준 2(형식 오류: 인코딩·줄바꿈 변형).
// UTF-8만(BOM 하나는 뗀다), LF·CRLF·CR, 구분자 ',', RFC 4180 따옴표, 빈 행 건너뜀, 칸 수 검사.
// 줄은 행이 시작한 물리 줄(1부터), 열은 칸이 시작한 UTF-16 단위(1부터).
[Trait("Spec", "metrics")]
public class CsvReadTests
{
    private const string AllMatch = "ref=2 cand=2 match=2 mismatch=0 missing=0 extra=0 within=0 acc=100.0 miss=0.0";

    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    // 정상 자료와 같은 셈이 나오는 기준 자료 변형. 이름 → 바이트.
    public static TheoryData<string> SameCountVariants => new(Variants.Keys);

    private static readonly Dictionary<string, byte[]> Variants = new(StringComparer.Ordinal)
    {
        ["lf"] = CompareInput.Bytes(CompareInput.GoodCsv),
        ["crlf"] = CompareInput.Bytes(CompareInput.GoodCsv.Replace("\n", "\r\n", StringComparison.Ordinal)),
        ["cr"] = CompareInput.Bytes(CompareInput.GoodCsv.Replace('\n', '\r')),
        ["bom"] = [.. Bom, .. CompareInput.Bytes(CompareInput.GoodCsv)],
        ["bom-crlf"] = [.. Bom, .. CompareInput.Bytes(CompareInput.GoodCsv.Replace("\n", "\r\n", StringComparison.Ordinal))],
        ["no-final-newline"] = CompareInput.Bytes(CompareInput.GoodCsv.TrimEnd('\n')),
        ["mixed-newlines"] = CompareInput.Bytes("id,n,t\r\nk1,1,a\rk2,2,b\n"),
        ["blank-lines"] = CompareInput.Bytes("\n\nid,n,t\n\nk1,1,a\n\n\nk2,2,b\n\n"),
        ["all-empty-rows"] = CompareInput.Bytes("id,n,t\n,,\nk1,1,a\n , ,\t\nk2,2,b\n,,\n"),
        ["quoted-all"] = CompareInput.Bytes("\"id\",\"n\",\"t\"\n\"k1\",\"1\",\"a\"\n\"k2\",\"2\",\"b\"\n"),
        ["trailing-comma-header-and-rows"] = CompareInput.Bytes("id,n,t,\nk1,1,a,\nk2,2,b,\n"),
        ["nul-in-unused-column"] = CompareInput.Bytes("id,n,t,x\nk1,1,a,p\0q\nk2,2,b,\0\n"),
    };

    // 줄바꿈 3종·BOM·마지막 줄바꿈 없음·빈 행·따옴표 칸이 같은 셈. 기준 쪽만 바꿔도, 양쪽을 바꿔도 같다.
    [Theory]
    [MemberData(nameof(SameCountVariants))]
    public void Variants_give_same_counts(string variant)
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);
        var bytes = Variants[variant];

        Assert.Equal(AllMatch, CompareInput.Counts(CompareInput.Run(definition, bytes, CompareInput.Bytes(CompareInput.GoodCsv))));
        Assert.Equal(AllMatch, CompareInput.Counts(CompareInput.Run(definition, bytes, bytes)));
    }

    // 줄 번호가 같은 변형(줄바꿈·BOM·마지막 줄바꿈)은 metrics.json 바이트도 같다.
    [Theory]
    [InlineData("crlf")]
    [InlineData("cr")]
    [InlineData("bom")]
    [InlineData("bom-crlf")]
    [InlineData("no-final-newline")]
    [InlineData("mixed-newlines")]
    public void Newline_and_bom_variants_give_same_bytes(string variant)
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);
        const string candidate = "id,n,t\nk1,1,x\nk3,3,c\n";
        var baseline = MetricsJson.Render(CompareInput.Measured(CompareInput.Run(definition, CompareInput.Bytes(CompareInput.GoodCsv), CompareInput.Bytes(candidate))));

        var actual = MetricsJson.Render(CompareInput.Measured(CompareInput.Run(definition, Variants[variant], CompareInput.Bytes(candidate))));

        Assert.Equal(CompareInput.Text(baseline), CompareInput.Text(actual));
    }

    // 따옴표 칸: 안의 ','·'""'·줄바꿈. 칸 값은 따옴표를 푼 글자다(차이 칸으로 확인한다).
    [Fact]
    public void Quoted_cells_hold_commas_quotes_and_newlines()
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);

        var comma = CompareInput.Measured(CompareInput.Run(definition, "id,n,t\nk1,1,\"b,c\"\n", "id,n,t\nk1,1,\"b,d\"\n"));
        Assert.Equal(("b,c", "b,d"), Difference(comma));

        // 따옴표 칸 안의 "" = 따옴표로 시작하지 않는 칸 안의 " 글자 그대로.
        Assert.Equal(
            "ref=1 cand=1 match=1 mismatch=0 missing=0 extra=0 within=0 acc=100.0 miss=0.0",
            CompareInput.Counts(CompareInput.Run(definition, "id,n,t\nk1,1,\"say \"\"hi\"\"\"\n", "id,n,t\nk1,1,say \"hi\"\n")));

        // 따옴표 안 CRLF는 정규화에서 LF가 된다(4.6).
        Assert.Equal(
            "ref=2 cand=2 match=2 mismatch=0 missing=0 extra=0 within=0 acc=100.0 miss=0.0",
            CompareInput.Counts(CompareInput.Run(definition, "id,n,t\r\nk1,1,\"x\r\ny\"\r\nk2,2,b\r\n", "id,n,t\nk1,1,\"x\ny\"\nk2,2,b\n")));

        var newline = CompareInput.Measured(CompareInput.Run(definition, "id,n,t\nk1,1,\"x\ny\"\n", "id,n,t\nk1,1,\"x\nz\"\n"));
        Assert.Equal(("x\ny", "x\nz"), Difference(newline));
    }

    // 따옴표로 시작하지 않는 칸 안의 '"'는 글자 그대로다. 칸 앞 공백 뒤의 '"'도 글자 그대로다(따옴표 칸이 아니다).
    [Fact]
    public void Quote_not_at_cell_start_is_literal()
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);

        Assert.Equal(
            "ref=1 cand=1 match=1 mismatch=0 missing=0 extra=0 within=0 acc=100.0 miss=0.0",
            CompareInput.Counts(CompareInput.Run(definition, "id,n,t\nk1,1,a\"b\n", "id,n,t\nk1,1,\"a\"\"b\"\n")));

        var spaced = CompareInput.Measured(CompareInput.Run(definition, "id,n,t\nk1,1, \"a\"\n", "id,n,t\nk1,1,a\n"));
        Assert.Equal(("\"a\"", "a"), Difference(spaced));
    }

    // csv-syntax 2종: 닫히지 않은 따옴표(여는 따옴표 위치), 닫는 따옴표 뒤의 다른 글자(그 글자 위치).
    [Fact]
    public void Quote_errors_give_csv_syntax_at_position()
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);

        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\nk1,1,a\nk2,2,\"b\nk3,3,c\n", CompareInput.GoodCsv),
            "reference/a.csv:3:6: csv-syntax: 따옴표가 닫히지 않았다");
        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\nk1,1,\"a\"x\nk2,2,b\n", CompareInput.GoodCsv),
            "reference/a.csv:2:9: csv-syntax: 닫는 따옴표 뒤에 다른 글자가 있다");
        CompareInput.AssertErrors(
            CompareInput.Run(definition, CompareInput.GoodCsv, "id,n,t\nk1,1,a\n\"k2"),
            "candidate/a.csv:3:1: csv-syntax: 따옴표가 닫히지 않았다");
    }

    // UTF-16 BOM은 1줄 encoding 오류. UTF-8이 아닌 바이트는 그 바이트가 있는 줄, 메시지에 바이트 위치
    // (파일 처음부터 0으로 센 위치, BOM 포함. case-schema 4.8과 같다).
    [Fact]
    public void Non_utf8_bytes_give_encoding_errors()
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);
        var good = CompareInput.Bytes(CompareInput.GoodCsv);

        CompareInput.AssertErrors(
            CompareInput.Run(definition, [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(CompareInput.GoodCsv)], good),
            "reference/a.csv:1: encoding: UTF-16이다. UTF-8로 저장한다");
        CompareInput.AssertErrors(
            CompareInput.Run(definition, [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(CompareInput.GoodCsv)], good),
            "reference/a.csv:1: encoding: UTF-16이다. UTF-8로 저장한다");

        // CP949 "가"(B0 A1). "id,n,t\n"(7) + "k1,1,a\n"(7) + "k2,2,"(5) = 바이트 19, 3줄.
        CompareInput.AssertErrors(
            CompareInput.Run(definition, [.. CompareInput.Bytes("id,n,t\nk1,1,a\nk2,2,"), 0xB0, 0xA1, (byte)'\n'], good),
            "reference/a.csv:3: encoding: UTF-8이 아니다 (바이트 19)");

        // BOM 3 + "id,n,t\n"(7) = 바이트 10, 2줄.
        CompareInput.AssertErrors(
            CompareInput.Run(definition, [.. Bom, .. CompareInput.Bytes("id,n,t\n"), 0xFF, (byte)'\n'], good),
            "reference/a.csv:2: encoding: UTF-8이 아니다 (바이트 10)");

        CompareInput.AssertErrors(
            CompareInput.Run(definition, good, [0xFF, (byte)'\n']),
            "candidate/a.csv:1: encoding: UTF-8이 아니다 (바이트 0)");
    }

    // BOM 없는 UTF-16은 UTF-8로 읽혀 column 오류로 드러난다. BOM이 두 번이면 둘째 BOM(U+FEFF)이 첫 열 이름에 붙는다.
    // ';' 구분 CSV는 한 칸이 되어 column 오류다.
    [Fact]
    public void Misread_files_surface_as_column_errors()
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);
        var good = CompareInput.Bytes(CompareInput.GoodCsv);

        CompareInput.AssertErrors(
            CompareInput.Run(definition, Encoding.Unicode.GetBytes(CompareInput.GoodCsv), good),
            "reference/a.csv:1: column id: 머리 행에 열이 없다",
            "reference/a.csv:1: column n: 머리 행에 열이 없다",
            "reference/a.csv:1: column t: 머리 행에 열이 없다");
        CompareInput.AssertErrors(
            CompareInput.Run(definition, [.. Bom, .. Bom, .. good], good),
            "reference/a.csv:1: column id: 머리 행에 열이 없다");
        CompareInput.AssertErrors(
            CompareInput.Run(definition, CompareInput.Bytes("id;n;t\nk1;1;a\nk2;2;b\n"), good),
            "reference/a.csv:1: column id: 머리 행에 열이 없다",
            "reference/a.csv:1: column n: 머리 행에 열이 없다",
            "reference/a.csv:1: column t: 머리 행에 열이 없다");
    }

    // empty 3종: 빈 파일, BOM만, 빈 행만. 위치는 파일(줄 없음).
    [Fact]
    public void Files_without_header_give_empty_error()
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);
        var good = CompareInput.Bytes(CompareInput.GoodCsv);

        foreach (var bytes in new[] { Array.Empty<byte>(), Bom, CompareInput.Bytes("\n\n , \n\r\n") })
        {
            CompareInput.AssertErrors(CompareInput.Run(definition, bytes, good), "reference/a.csv: empty: 머리 행이 없다");
            CompareInput.AssertErrors(CompareInput.Run(definition, good, bytes), "candidate/a.csv: empty: 머리 행이 없다");
        }
    }

    // field-count 2종: 칸이 모자람·남음. 위치는 파일:줄(열 없음). 메시지에 두 칸 수.
    [Fact]
    public void Wrong_cell_count_gives_field_count_error()
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);

        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\nk1,1\nk2,2,b\n", CompareInput.GoodCsv),
            "reference/a.csv:2: field-count: 칸 수가 머리 행과 다르다 (머리 행 3칸, 이 행 2칸)");
        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\nk1,1,a,extra\nk2,2,b\n", CompareInput.GoodCsv),
            "reference/a.csv:2: field-count: 칸 수가 머리 행과 다르다 (머리 행 3칸, 이 행 4칸)");
    }

    // 줄은 행이 시작한 물리 줄이다. 따옴표 안 줄바꿈도 센다. 빈 행도 센다.
    // 열은 UTF-16 단위다: 한글 한 글자 1, 이모지 2.
    [Fact]
    public void Line_and_column_numbers_follow_physical_lines_and_utf16_units()
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);

        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\nk1,1,\"x\ny\"\nk1,2,b\n", CompareInput.GoodCsv),
            "reference/a.csv:4:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)");
        CompareInput.AssertErrors(
            CompareInput.Run(definition, "\nid,n,t\n\nk1,x,a\n", CompareInput.GoodCsv),
            "reference/a.csv:4:4: number n: 숫자가 아니다: x");
        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\n가나다,x,a\n", CompareInput.GoodCsv),
            "reference/a.csv:2:5: number n: 숫자가 아니다: x");
        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\n\U0001F600,x,a\n", CompareInput.GoodCsv),
            "reference/a.csv:2:4: number n: 숫자가 아니다: x");
        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\r\nk1,1,a\r\nk2,x,b\r\n", CompareInput.GoodCsv),
            "reference/a.csv:3:4: number n: 숫자가 아니다: x");
    }

    // 4.2 줄·열 번호(T-013 카드 수용 기준 6): 따옴표 칸 안의 CRLF는 줄바꿈 하나다(CR 하나, LF 하나로 두 번 세지 않는다).
    // 칸 안 줄바꿈 뒤의 칸, 다음 행이 시작한 줄이 그 수만큼만 늘어난다. CR만·LF만·CRLF 여러 개·섞인 것 모두.
    [Fact]
    public void Crlf_inside_quoted_cell_counts_as_one_line()
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);

        // 2줄에서 시작한 k1 행의 t 칸이 3줄(CRLF 하나)·4줄(CRLF 둘)·5줄(CRLF 셋)까지 간다. 다음 행 k1이 그 다음 줄이다.
        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\r\nk1,1,\"x\r\ny\"\r\nk1,2,b\r\n", CompareInput.GoodCsv),
            "reference/a.csv:4:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)");
        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\r\nk1,1,\"x\r\ny\r\nz\"\r\nk1,2,b\r\n", CompareInput.GoodCsv),
            "reference/a.csv:5:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)");
        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\nk1,1,\"\r\n\r\n\r\n\"\nk1,2,b\n", CompareInput.GoodCsv),
            "reference/a.csv:6:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)");

        // 섞인 줄바꿈: CRLF(1) + CR(1) + LF(1) = 세 줄. 칸 끝 CR 뒤 LF도 CRLF 하나다(칸 마지막 글자).
        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\nk1,1,\"a\r\nb\rc\nd\"\nk1,2,b\n", CompareInput.GoodCsv),
            "reference/a.csv:6:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)");
        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\nk1,1,\"a\r\n\"\nk1,2,b\n", CompareInput.GoodCsv),
            "reference/a.csv:4:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)");

        // 닫히지 않은 따옴표 칸이 CR로 끝나도(파일 마지막 글자) 뒤 글자를 읽지 않는다. 오류는 여는 따옴표 위치다.
        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\nk1,1,\"a\r", CompareInput.GoodCsv),
            "reference/a.csv:2:6: csv-syntax: 따옴표가 닫히지 않았다");

        // 칸 안 CRLF 뒤 같은 행의 다음 칸: 3줄이고, 열은 그 줄 처음부터 센다(b, 닫는 따옴표, 쉼표 뒤 → 4열).
        var textFirst = CompareInput.Case(CompareInput.InputsCase + "compare:\n  - {field: t}\n  - {field: n, tolerance: 0}\n");
        CompareInput.AssertErrors(
            CompareInput.Run(textFirst, "id,t,n\r\nk1,\"a\r\nb\",x\r\n", "id,t,n\nk1,a,1\n"),
            "reference/a.csv:3:4: number n: 숫자가 아니다: x");

        // 대조 결과의 행 위치(CsvRow.Line)도 같다. 값은 정규화 뒤 LF다(4.6).
        var metrics = CompareInput.Measured(CompareInput.Run(definition, "id,n,t\r\nk1,1,\"x\r\ny\r\nz\"\r\nk2,2,b\r\n", "id,n,t\nk1,1,\"x\ny\nz\"\nk2,2,b\n"));
        Assert.Equal(["k1 2 2", "k2 5 5"], metrics.Items.Select(i => $"{i.Key} {i.Reference!.Line} {i.Candidate!.Line}"));
        Assert.Equal("x\ny\nz", metrics.Items[0].Reference!.Cells[2]);
    }

    // 값 안의 NUL 등 제어 문자는 그대로 값이다(오류가 아니다).
    [Fact]
    public void Control_characters_in_values_are_kept()
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);

        var metrics = CompareInput.Measured(CompareInput.Run(definition, "id,n,t\nk1,1,a\0b\n", "id,n,t\nk1,1,a\0c\n"));

        Assert.Equal(("a\0b", "a\0c"), Difference(metrics));
    }

    // 결과 자료에 머리 행만 있으면 오류가 아니다. 모든 항목이 누락이다(0.0·100.0).
    [Fact]
    public void Candidate_with_header_only_makes_all_items_missing()
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);

        Assert.Equal(
            "ref=2 cand=0 match=0 mismatch=0 missing=2 extra=0 within=0 acc=0.0 miss=100.0",
            CompareInput.Counts(CompareInput.Run(definition, CompareInput.GoodCsv, "id,n,t\n")));
    }

    // 차이가 있는 항목 하나의 차이 하나(기준 칸, 결과 칸).
    private static (string Reference, string Candidate) Difference(CompareMetrics metrics)
    {
        var difference = Assert.Single(Assert.Single(metrics.Items, i => i.Differences.Count > 0).Differences);
        return (difference.Reference, difference.Candidate);
    }
}
