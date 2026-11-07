using System.Globalization;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Classification;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Synthetic;

// 합성 예제 테스트 도우미. 예제 파일 읽기, 생성, CSV 읽기, 단순 대조(스펙 4.7 용어).
// 대조는 T-006 대조 엔진이 아니다. 자료에 심은 구조를 세기 위한 테스트 안의 셈이다.
internal static class SyntheticInput
{
    // 자료(reference/·candidate/)가 있는 예제. 스펙 4.3 계획 표·7.1 파일 표와 같은지는 테스트가 본다.
    public static readonly string[] DataExamples = ["table-compare", "form-fill", "signoff-final"];

    // 자료가 없는 예제(지시문 12절 "자료 없음", 스펙 3.2).
    public static readonly string[] NoDataExamples = ["mixed-exceptions"];

    // T-004가 만든 예제 파일. T-005는 바꾸지 않는다(스펙 4.12).
    public static readonly string[] ClassificationFiles = ["answers.yaml", "expected/classification.json"];

    // 단계 하나짜리 case.yaml 앞부분. 아래에 inputs·compare를 붙인다.
    public const string OneStepCase = "name: p\ntask: t\nsteps:\n  - name: s\n";

    // 키 열 id, compare 없음. 열 종류 확인·수열 확인용.
    public const string NoCompareCase = OneStepCase + "inputs: {reference: reference/*.csv, candidate: candidate/*.csv, key: id}\n";

    // 세 종류의 compare 필드(정수 n tolerance 0, 소수 d tolerance_pct 1.0, 선택 c). 스펙 4.6 확인용.
    public const string ThreeKindCase = NoCompareCase +
        "compare:\n  - {field: n, tolerance: 0}\n  - {field: d, tolerance_pct: 1.0}\n  - {field: c}\n";

    public static TheoryData<string> DataExampleNames => new(DataExamples);

    public static TheoryData<string> AllExampleNames => new(ClassificationInput.Examples);

    public static string ExampleRelative(string example, string relative) =>
        $"{ClassificationInput.ExamplesRelativePath}/{example}/{relative}";

    public static string ExamplePath(string example, string relative) => RepoRoot.Combine(ExampleRelative(example, relative));

    public static string ExampleDirectory(string example) => RepoRoot.Combine($"{ClassificationInput.ExamplesRelativePath}/{example}");

    // 파일이 없으면 이름을 보이고 실패한다(빈 통과 금지).
    public static byte[] ExampleBytes(string example, string relative)
    {
        var path = ExamplePath(example, relative);
        Assert.True(File.Exists(path), $"{ExampleRelative(example, relative)}가 없다.");
        return File.ReadAllBytes(path);
    }

    // examples/<예제>/case.yaml → 모델. 파싱 오류면 실패한다.
    public static CaseDefinition CaseOf(string example)
    {
        Assert.True(File.Exists(ExamplePath(example, CaseParser.FileName)), $"{ExampleRelative(example, CaseParser.FileName)}가 없다.");
        return CaseInput.Valid(CaseParser.ParseDirectory(ExampleDirectory(example)));
    }

    public static CaseDefinition Case(string yaml) => CaseInput.Valid(yaml);

    // examples/<예제>/case.yaml + 4.3·4.4 표 → 생성.
    public static SyntheticTables Generate(string example) => SyntheticGenerator.Generate(CaseOf(example), SyntheticSpec.Plan(example));

    // 생성 결과 → (reference 바이트, candidate 바이트).
    public static (byte[] Reference, byte[] Candidate) Render(SyntheticTables tables) =>
        (SyntheticCsv.Render(tables.Header, tables.Reference), SyntheticCsv.Render(tables.Header, tables.Candidate));

    // 예제의 커밋된 CSV. folder는 SyntheticCsv.ReferenceFolder 또는 CandidateFolder.
    public static string CsvRelative(string example, string folder) => $"{folder}/{SyntheticSpec.Plan(example).FileName}";

    public static byte[] CommittedCsv(string example, string folder) => ExampleBytes(example, CsvRelative(example, folder));

    // SyntheticRow → [키, 값...].
    public static string[][] Cells(IEnumerable<SyntheticRow> rows) => rows.Select(r => (string[])[r.Key, .. r.Values]).ToArray();

    // 스펙 3.3 모양의 CSV를 읽는다. ASCII만, LF, 끝 LF, 빈 줄 없음, 따옴표 없음. 아니면 실패한다.
    public static CsvTable ParseCsv(byte[] bytes, string label)
    {
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], $"{label}: BOM이 없어야 한다.");
        var bad = bytes.Select((b, i) => (b, i)).Where(x => x.b != (byte)'\n' && (x.b < 0x20 || x.b > 0x7E)).Take(5).ToArray();
        Assert.True(bad.Length == 0, $"{label}: ASCII 인쇄 문자와 LF만 써야 한다. 처음 위치: {string.Join(", ", bad.Select(x => $"{x.i}=0x{x.b:x2}"))}");
        Assert.DoesNotContain((byte)'"', bytes);
        Assert.True(bytes.Length > 0 && bytes[^1] == (byte)'\n', $"{label}: 마지막 줄 뒤에 LF가 있어야 한다.");

        var lines = Encoding.ASCII.GetString(bytes, 0, bytes.Length - 1).Split('\n');
        Assert.True(lines.All(l => l.Length > 0), $"{label}: 빈 줄이 없어야 한다.");
        var rows = lines.Select(l => l.Split(',')).ToArray();
        Assert.All(rows, r => Assert.Equal(rows[0].Length, r.Length));
        Assert.All(rows.SelectMany(r => r), cell => Assert.True(cell.Length > 0 && cell.Trim() == cell, $"{label}: 빈 칸이나 앞뒤 공백이 있는 칸 '{cell}'"));
        return new CsvTable(rows[0], rows.Skip(1).ToArray());
    }

    // 스펙 4.7 용어로 센다. 항목 = 기준 행. variant: base(≤, 기준값 대비 %), strict(<, 허용 오차 0은 같음), candbase(결과값 대비 %).
    // 스펙 4.5: 경계값을 심지 않았으므로 세 변형의 셈이 같아야 한다.
    public static Tally Count(CaseDefinition definition, IReadOnlyList<string> header, string[][] reference, string[][] candidate, string variant = Variants.Base)
    {
        var inputs = definition.Inputs ?? throw new InvalidOperationException("inputs가 없는 케이스는 셀 수 없다.");
        var key = IndexOf(header, inputs.Key);
        var referenceKeys = reference.Select(r => r[key]).ToHashSet(StringComparer.Ordinal);
        var byKey = candidate.ToDictionary(r => r[key], StringComparer.Ordinal);
        int match = 0, mismatch = 0, missing = 0, within = 0;
        foreach (var r in reference)
        {
            if (!byKey.TryGetValue(r[key], out var c))
            {
                missing++;
                continue;
            }

            var ok = true;
            var differs = false;
            foreach (var field in definition.Compare)
            {
                var i = IndexOf(header, field.Field);
                differs |= !string.Equals(r[i], c[i], StringComparison.Ordinal);
                ok &= Within(field, r[i], c[i], variant);
            }

            if (ok)
            {
                match++;
                within += differs ? 1 : 0;
            }
            else
            {
                mismatch++;
            }
        }

        var extra = candidate.Count(c => !referenceKeys.Contains(c[key]));
        return new Tally(reference.Length, candidate.Length, match, mismatch, missing, extra, within);
    }

    public static Tally Count(CaseDefinition definition, SyntheticTables tables, string variant = Variants.Base) =>
        Count(definition, tables.Header, Cells(tables.Reference), Cells(tables.Candidate), variant);

    public static int IndexOf(IReadOnlyList<string> header, string name)
    {
        for (var i = 0; i < header.Count; i++)
        {
            if (string.Equals(header[i], name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        Assert.Fail($"머리 행 [{string.Join(",", header)}]에 열 '{name}'이 없다.");
        return -1;
    }

    // 정확도·누락률 칸 모양(4.7 구조 표): 소수 한 자리, 0에서 먼 쪽 반올림.
    public static string Percent(int part, int whole) =>
        Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero).ToString("F1", CultureInfo.InvariantCulture);

    public static decimal Number(string value) => decimal.Parse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    private static bool Within(CompareField field, string reference, string candidate, string variant)
    {
        if (field.Tolerance is null && field.TolerancePct is null)
        {
            return string.Equals(reference, candidate, StringComparison.Ordinal);
        }

        var r = Number(reference);
        var c = Number(candidate);
        var diff = Math.Abs(c - r);
        var limit = field.Tolerance ?? (field.TolerancePct!.Value * Math.Abs(variant == Variants.CandidateBase ? c : r) / 100m);
        return variant == Variants.Strict ? diff < limit || (diff == 0 && limit == 0) : diff <= limit;
    }
}

// 대조 변형 이름(스펙 4.5 경계값 bullet, 4.7 "대조 변형 3종").
internal static class Variants
{
    public const string Base = "base";
    public const string Strict = "strict";
    public const string CandidateBase = "candbase";

    public static readonly string[] All = [Base, Strict, CandidateBase];
}

// 스펙 4.7 구조 표의 셈 일곱 개.
internal sealed record Tally(int Reference, int Candidate, int Match, int Mismatch, int Missing, int Extra, int Within)
{
    public string Accuracy => SyntheticInput.Percent(Match, Reference);

    public string MissingRate => SyntheticInput.Percent(Missing, Reference);
}

// 읽은 CSV. Rows는 데이터 행(머리 행 제외), 칸은 [키, 값...].
internal sealed record CsvTable(string[] Header, string[][] Rows);
