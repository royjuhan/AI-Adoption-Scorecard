using System.Globalization;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Classification;

namespace Scorecard.Tests.Synthetic;

// 스펙 4.8 case.yaml(승인 결정 8)과 자료 없는 예제(3.2, 4.12, 승인 결정 12). 카드 수용 기준 3·4.
// case.yaml의 name·task·steps = T-004 answers.yaml(classification 스펙 7.1의 5). case.yaml 분류 = expected/classification.json.
[Trait("Spec", "synthetic-examples")]
public class CaseYamlTests
{
    private const string AnswersFile = "answers.yaml";
    private const string ExpectedFile = "expected/" + ClassificationJson.FileName;

    // 파싱 결과의 name·task·steps가 answers.yaml과 같다. 단계는 답 9개와 IsDefault까지 같다.
    [Theory]
    [MemberData(nameof(SyntheticInput.AllExampleNames), MemberType = typeof(SyntheticInput))]
    public void Case_steps_equal_answers(string example)
    {
        var definition = SyntheticInput.CaseOf(example);
        var answers = ClassificationInput.ParseAnswers(SyntheticInput.ExampleBytes(example, AnswersFile));

        Assert.Equal(example, definition.Name);
        Assert.Equal(answers.Name, definition.Name);
        Assert.Equal(answers.Task, definition.Task);
        Assert.Equal(answers.Steps, definition.Steps);
        Assert.NotEmpty(definition.Steps);
    }

    // 4.8: answers.yaml의 name: 줄부터 끝까지가 case.yaml 안에 바이트 그대로 있다. 앞 주석 줄만 다르다.
    // 자료 키(inputs·compare·thresholds)는 그 뒤에 붙는다.
    [Theory]
    [MemberData(nameof(SyntheticInput.AllExampleNames), MemberType = typeof(SyntheticInput))]
    public void Case_bytes_contain_answers_from_name_line(string example)
    {
        var (caseHead, caseTail) = SplitAtName(SyntheticInput.ExampleBytes(example, CaseParser.FileName), "case.yaml");
        var (answersHead, answersTail) = SplitAtName(SyntheticInput.ExampleBytes(example, AnswersFile), AnswersFile);

        Assert.All(caseHead.Concat(answersHead), l => Assert.StartsWith("#", l, StringComparison.Ordinal));
        Assert.True(
            caseTail.AsSpan().StartsWith(answersTail),
            $"examples/{example}/case.yaml의 name: 줄부터가 answers.yaml의 name: 줄부터 끝까지로 시작하지 않는다.");
        if (SyntheticInput.NoDataExamples.Contains(example))
        {
            Assert.Equal(answersTail, caseTail);
        }
    }

    // 카드 수용 기준 4: case.yaml을 분류한 결과 = expected/classification.json 바이트.
    [Theory]
    [MemberData(nameof(SyntheticInput.AllExampleNames), MemberType = typeof(SyntheticInput))]
    public void Case_classification_equals_expected_bytes(string example)
    {
        var expected = SyntheticInput.ExampleBytes(example, ExpectedFile);

        Assert.Equal(expected, ClassificationJson.Render(StepClassifier.Classify(SyntheticInput.CaseOf(example))));
    }

    // 4.8 표: inputs·compare·thresholds. before·after·ai_usage는 넣지 않는다. T-012: inputs 칸의 유형 열 = inputs.group.
    [Theory]
    [MemberData(nameof(SyntheticInput.AllExampleNames), MemberType = typeof(SyntheticInput))]
    public void Case_data_keys_match_spec_table(string example)
    {
        var definition = SyntheticInput.CaseOf(example);
        var (inputs, compare, thresholds) = CaseKeys(example);

        Assert.Equal(inputs, definition.Inputs);
        Assert.Equal(compare, definition.Compare);
        Assert.Equal(thresholds, definition.Thresholds);
        Assert.Null(definition.Before);
        Assert.Null(definition.After);
        Assert.Null(definition.AiUsage);
    }

    // 4.7·4.8: 구조 표의 정확도·누락률이 thresholds를 충족한다(table-compare 90.0 ≥ 85, 3.3 ≤ 5. form-fill 100.0, 0.0).
    // 시간 자료가 없어 time_must_drop은 false다.
    // T-009(signoff-final.md V6, juhan 승인 J2·J3 안 A): 문턱을 충족하는 자료 예제만 본다.
    // signoff-final(정확도 80.0 < 95, 누락률 5.0 > 2)의 문턱 미달은 SignoffFinalExampleTests가 본다.
    [Theory]
    [MemberData(nameof(ThresholdExampleNames))]
    public void Structure_meets_thresholds(string example)
    {
        var thresholds = SyntheticInput.CaseOf(example).Thresholds;
        var (_, accuracy, missingRate) = SyntheticSpec.Structure(example);

        Assert.NotNull(thresholds.AccuracyMin);
        Assert.NotNull(thresholds.MissingMax);
        Assert.False(thresholds.TimeMustDrop);
        Assert.True(SyntheticInput.Number(accuracy) >= thresholds.AccuracyMin, $"{example}: 정확도 {accuracy} < accuracy_min {thresholds.AccuracyMin}");
        Assert.True(SyntheticInput.Number(missingRate) <= thresholds.MissingMax, $"{example}: 누락률 {missingRate} > missing_max {thresholds.MissingMax}");
    }

    // 자료 없는 예제(mixed-exceptions): inputs null, compare 빈 목록, thresholds 모두 null, reference/·candidate/ 폴더 없음.
    // 생성기는 inputs가 없으면 ArgumentException이다(5절).
    [Theory]
    [MemberData(nameof(NoDataExampleNames))]
    public void No_data_example_has_no_inputs_and_no_data_folders(string example)
    {
        var definition = SyntheticInput.CaseOf(example);

        Assert.Null(definition.Inputs);
        Assert.Empty(definition.Compare);
        Assert.Equal(new CaseThresholds(null, null, null), definition.Thresholds);
        Assert.False(Directory.Exists(SyntheticInput.ExamplePath(example, SyntheticCsv.ReferenceFolder)), $"examples/{example}/reference/가 없어야 한다.");
        Assert.False(Directory.Exists(SyntheticInput.ExamplePath(example, SyntheticCsv.CandidateFolder)), $"examples/{example}/candidate/가 없어야 한다.");
        Assert.DoesNotContain(example, SyntheticSpec.PlanExamples());
        Assert.Throws<ArgumentException>(() => SyntheticGenerator.Generate(definition, SyntheticSpec.Plan("table-compare")));
    }

    public static TheoryData<string> NoDataExampleNames => new(SyntheticInput.NoDataExamples);

    // T-009(V6): 문턱을 충족하는 자료 예제 = 자료 예제에서 signoff-final을 뺀 것.
    public static TheoryData<string> ThresholdExampleNames =>
        new(SyntheticInput.DataExamples.Where(e => e != SignoffFinalExampleTests.Example));

    // 4.8 표의 그 예제 행 → (inputs, compare, thresholds). "없음"은 없음, "같음"은 바로 위 행과 같음.
    private static (CaseInputs? Inputs, CompareField[] Compare, CaseThresholds Thresholds) CaseKeys(string example)
    {
        var rows = SyntheticSpec.Table(SyntheticSpec.CaseKeysHeader);
        var index = rows.Select((c, i) => (c, i)).Single(x => SpecDocument.Unquote(x.c[0]) == example).i;
        var cells = rows[index];

        CaseInputs? inputs = null;
        if (cells[1] != SyntheticSpec.None)
        {
            // T-012(case-schema.md 10.5 5): 넷째 값 "유형 열 `x`"가 있으면 inputs.group이다. 없으면 Group = null.
            var spans = SpecDocument.CodeSpans(cells[1]);
            Assert.True(
                spans.Length is 3 or 4 && cells[1].Contains($"키 `{spans[2]}`", StringComparison.Ordinal) &&
                (spans.Length == 3 || cells[1].EndsWith($", 유형 열 `{spans[3]}`", StringComparison.Ordinal)),
                $"4.8 '{example}' inputs 칸 '{cells[1]}'");
            inputs = new CaseInputs(spans[0], spans[1], spans[2], spans.Length == 4 ? spans[3] : null);
        }

        var compare = new List<CompareField>();
        if (cells[2] != SyntheticSpec.None)
        {
            foreach (var span in SpecDocument.CodeSpans(cells[2]))
            {
                var pair = span.Split(": ");
                if (pair.Length == 1)
                {
                    compare.Add(new CompareField(span, null, null));
                    continue;
                }

                Assert.NotEmpty(compare);
                var value = decimal.Parse(pair[1], CultureInfo.InvariantCulture);
                compare[^1] = pair[0] switch
                {
                    "tolerance" => compare[^1] with { Tolerance = value },
                    "tolerance_pct" => compare[^1] with { TolerancePct = value },
                    _ => throw new InvalidOperationException($"4.8 '{example}' compare 칸의 '{span}'을 모른다."),
                };
            }
        }

        return (inputs, compare.ToArray(), Thresholds(rows, index, example));
    }

    private static CaseThresholds Thresholds(IReadOnlyList<string[]> rows, int index, string example)
    {
        var cell = rows[index][3];
        if (cell == "같음")
        {
            Assert.True(index > 0, $"4.8 '{example}' thresholds 칸 '같음' 위에 행이 없다.");
            return Thresholds(rows, index - 1, example);
        }

        if (cell == SyntheticSpec.None)
        {
            return new CaseThresholds(null, null, null);
        }

        var pairs = SpecDocument.CodeSpans(cell).Select(s => s.Split(": ")).ToArray();
        Assert.Equal(["accuracy_min", "missing_max", "time_must_drop"], pairs.Select(p => p[0]));
        return new CaseThresholds(
            decimal.Parse(pairs[0][1], CultureInfo.InvariantCulture),
            decimal.Parse(pairs[1][1], CultureInfo.InvariantCulture),
            bool.Parse(pairs[2][1]));
    }

    // 파일을 "name:" 줄 앞(줄 목록)과 그 줄부터 끝(바이트)으로 나눈다. "name:" 줄이 없으면 실패한다.
    private static (string[] Head, byte[] Tail) SplitAtName(byte[] bytes, string label)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var at = text.StartsWith("name:", StringComparison.Ordinal) ? 0 : text.IndexOf("\nname:", StringComparison.Ordinal) + 1;
        Assert.True(at >= 0 && text[at..].StartsWith("name:", StringComparison.Ordinal), $"{label}에 'name:' 줄이 없다.");
        var head = text[..at].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return (head, Encoding.UTF8.GetBytes(text[at..]));
    }
}
