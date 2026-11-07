using System.Globalization;
using System.Text.RegularExpressions;
using Scorecard.Core.Synthetic;

namespace Scorecard.Tests.Synthetic;

// 골든(스펙 8절 "골든", 카드 수용 기준 1·3)과 CSV 바이트 규칙(스펙 3.3, 승인 결정 7).
// examples/<예제>/case.yaml + 4.3·4.4 표 → Generate → Render가 커밋된 CSV와 바이트까지 같다.
// 테스트는 파일을 쓰지 않는다(4.1). 파일이 없으면 실패한다(빈 통과 금지).
[Trait("Spec", "synthetic-examples")]
public partial class ExampleCsvTests
{
    public static TheoryData<string, string> ExampleFolders
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var example in SyntheticInput.DataExamples)
            {
                data.Add(example, SyntheticCsv.ReferenceFolder);
                data.Add(example, SyntheticCsv.CandidateFolder);
            }

            return data;
        }
    }

    // 골든: 생성 바이트 = 커밋된 CSV 바이트. 다르면 줄 단위로 먼저 보인다.
    [Theory]
    [MemberData(nameof(ExampleFolders))]
    public void Generated_csv_equals_committed_file(string example, string folder)
    {
        var committed = SyntheticInput.CommittedCsv(example, folder);
        var (reference, candidate) = SyntheticInput.Render(SyntheticInput.Generate(example));
        var generated = folder == SyntheticCsv.ReferenceFolder ? reference : candidate;

        Assert.Equal(Text(committed), Text(generated));
        Assert.Equal(committed, generated);
    }

    // 생성 결과의 파일 이름은 계획 표 "파일" 칸이다. 커밋된 파일은 reference/·candidate/에 같은 이름으로 하나씩 있다(3.3 "파일").
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Each_data_folder_has_one_csv_named_by_plan(string example)
    {
        var plan = SyntheticSpec.Plan(example);

        Assert.Equal(plan.FileName, SyntheticInput.Generate(example).FileName);
        foreach (var folder in new[] { SyntheticCsv.ReferenceFolder, SyntheticCsv.CandidateFolder })
        {
            var dir = SyntheticInput.ExamplePath(example, folder);
            Assert.True(Directory.Exists(dir), $"{SyntheticInput.ExampleRelative(example, folder)}/ 폴더가 없다.");
            Assert.Equal([plan.FileName], Directory.EnumerateFileSystemEntries(dir).Select(f => Path.GetFileName(f)));
        }
    }

    // 3.3: BOM·CR 없음, 끝 LF, ASCII 인쇄 문자만, 따옴표 없음, 빈 값 없음, 칸 앞뒤 공백 없음.
    // 머리 행 = 키 열(inputs.key) + 4.4 열 표 순서.
    [Theory]
    [MemberData(nameof(ExampleFolders))]
    public void Committed_csv_follows_byte_rules(string example, string folder)
    {
        var relative = SyntheticInput.CsvRelative(example, folder);
        var csv = SyntheticInput.ParseCsv(SyntheticInput.CommittedCsv(example, folder), relative);
        var definition = SyntheticInput.CaseOf(example);

        Assert.Equal([definition.Inputs!.Key, .. SyntheticSpec.Columns(example).Select(c => c.Name)], csv.Header);
        Assert.NotEmpty(csv.Rows);
    }

    // 3.3 값 모양: 정수는 천 단위 구분 없음, 소수는 소수점 '.'와 고정 자릿수, 선택은 선택지 중 하나.
    // 기준 행과 심지 않은 결과 행은 4.4 범위 안이다.
    [Theory]
    [MemberData(nameof(ExampleFolders))]
    public void Committed_csv_values_have_column_shapes(string example, string folder)
    {
        var columns = SyntheticSpec.Columns(example);
        var csv = SyntheticInput.ParseCsv(SyntheticInput.CommittedCsv(example, folder), SyntheticInput.CsvRelative(example, folder));
        var planted = SyntheticInput.Generate(example).Candidate.ToDictionary(r => r.Key, r => r.Planted, StringComparer.Ordinal);

        foreach (var row in csv.Rows)
        {
            var plain = folder == SyntheticCsv.ReferenceFolder
                || planted[row[0]] is SyntheticGenerator.PlantedNone or SyntheticGenerator.PlantedExtra;
            for (var i = 0; i < columns.Length; i++)
            {
                var (column, value) = (columns[i], row[i + 1]);
                switch (column.Kind)
                {
                    case SyntheticColumn.KindInteger:
                        Assert.Matches(IntegerValue(), value);
                        break;
                    case SyntheticColumn.KindDecimal:
                        Assert.Matches($@"^-?[0-9]+\.[0-9]{{{column.Scale}}}\z", value);
                        break;
                    default:
                        Assert.Contains(value, column.Choices);
                        break;
                }

                if (plain && column.Kind != SyntheticColumn.KindChoice)
                {
                    var number = SyntheticInput.Number(value);
                    Assert.InRange(number, column.Min, column.Max);
                }
            }
        }
    }

    // 3.3·4.5: 기준 행은 키 오름차순(만든 순서). 키 = 접두어 + 1..기준 건수(앞 0 채움).
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Reference_rows_are_in_key_order(string example)
    {
        var plan = SyntheticSpec.Plan(example);
        var csv = SyntheticInput.ParseCsv(SyntheticInput.CommittedCsv(example, SyntheticCsv.ReferenceFolder), example);

        Assert.Equal(Keys(plan, 1, plan.Rows), csv.Rows.Select(r => r[0]));
    }

    // 4.5의 6·7: 결과 키 = 기준 키 − 누락 + 추가(키는 기준 건수+1부터). 결과 섞기가 "예"면 순서가 기준과 다르다.
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Candidate_keys_are_reference_minus_missing_plus_extra(string example)
    {
        var plan = SyntheticSpec.Plan(example);
        var tables = SyntheticInput.Generate(example);
        var csv = SyntheticInput.ParseCsv(SyntheticInput.CommittedCsv(example, SyntheticCsv.CandidateFolder), example);
        var missing = tables.Reference.Where(r => r.Planted == SyntheticGenerator.PlantedMissing).Select(r => r.Key).ToHashSet(StringComparer.Ordinal);

        var expected = Keys(plan, 1, plan.Rows).Where(k => !missing.Contains(k)).Concat(Keys(plan, plan.Rows + 1, plan.Extra)).ToArray();
        var actual = csv.Rows.Select(r => r[0]).ToArray();

        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
        Assert.Equal(plan.Missing, missing.Count);
        if (plan.ShuffleCandidate)
        {
            Assert.NotEqual(expected, actual);
        }
        else
        {
            Assert.Equal(expected, actual);
        }
    }

    private static string[] Keys(SyntheticPlan plan, int first, int count) =>
        Enumerable.Range(first, count)
            .Select(i => plan.KeyPrefix + i.ToString("D" + plan.KeyDigits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture))
            .ToArray();

    private static string Text(byte[] bytes) => System.Text.Encoding.ASCII.GetString(bytes);

    [GeneratedRegex(@"^-?[0-9]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex IntegerValue();
}
