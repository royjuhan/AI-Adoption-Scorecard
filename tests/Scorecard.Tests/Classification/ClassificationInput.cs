using System.Globalization;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Classification;

// 분류 테스트 도우미. 답을 만들고 단계 하나를 분류한다.
// 기준 답은 스펙 6.1 문장에서 읽는다(모두 직접 쓴 값). 키·선택지는 case-schema 4.4 표에서 읽는다.
internal static class ClassificationInput
{
    // 스펙 6.1 "기준 답: `...`" 줄.
    public const string BaseLinePrefix = "기준 답: ";

    public const string ExamplesRelativePath = "examples";

    // 공개 예제 4종(지시문 12절, 스펙 7절). signoff-final은 T-009(ADR 0003 남은 질문 2, signoff-final.md V1).
    public static readonly string[] Examples = ["table-compare", "form-fill", "mixed-exceptions", "signoff-final"];

    // T-014 문장 결과 예제(text-check.md 7절). 위 4종의 스펙 표(classification·synthetic-examples·metrics·judgment)에는
    // 넣지 않는다(text-check.md 11절). 예제 폴더 전체를 보는 테스트는 AllExamples를 쓴다.
    public static readonly string[] TextCheckExamples = ["reply-drafts"];

    // examples/ 아래 예제 폴더 전부(5종).
    public static readonly string[] AllExamples = [.. Examples, .. TextCheckExamples];

    // 스펙 4.5 조합의 건수. Q4 경계(5) 앞뒤와 최솟값, 그보다 큰 값.
    public static readonly int[] BatchSizes = [1, 4, 5, 12];

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly Lazy<StepAnswers> BaseAnswers = new(ReadBase);

    // 6.1 기준 답. ELSE ②로 분류된다.
    public static StepAnswers Base => BaseAnswers.Value;

    public static TheoryData<string> ExampleNames => new(Examples);

    // (a, b) 목록 → TheoryData. xUnit 2.9의 TheoryData<T1, T2>에는 목록을 받는 생성자가 없다.
    public static TheoryData<string, string> TheoryPairs(IEnumerable<(string A, string B)> items)
    {
        var data = new TheoryData<string, string>();
        foreach (var (a, b) in items)
        {
            data.Add(a, b);
        }

        return data;
    }

    // 4.4 표 순서의 키 10개.
    public static string[] Keys => DefaultRow.ReadAll().Select(r => r.Key).ToArray();

    // 4.4 표의 열거 선택지.
    public static string[] Choices(string key) => DefaultRow.ReadAll().Single(r => r.Key == key).ChoiceList;

    // "k: v, k: v" → [(k, v), ...]. 빈 문자열이면 빈 목록.
    public static (string Key, string Value)[] Pairs(string text) =>
        text.Length == 0
            ? []
            : text.Split(", ").Select(p => p.Split(": ")).Select(p =>
            {
                Assert.True(p.Length == 2, $"'k: v' 모양이 아니다: {string.Join(": ", p)}");
                return (p[0].Trim(), p[1].Trim());
            }).ToArray();

    // 답 하나를 바꾼다. 값은 YAML에 쓰는 모양(숫자는 InvariantCulture, null은 "null").
    public static StepAnswers With(StepAnswers a, string key, string value, bool isDefault = false) => key switch
    {
        "input_kind" => a with { InputKind = new(value, isDefault) },
        "rule_expressible" => a with { RuleExpressible = new(value, isDefault) },
        "frequency" => a with { Frequency = new(value, isDefault) },
        "batch_size" => a with { BatchSize = new(int.Parse(value, CultureInfo.InvariantCulture), isDefault) },
        "exception_rate" => a with { ExceptionRate = new(decimal.Parse(value, CultureInfo.InvariantCulture), isDefault) },
        "current_actor" => a with { CurrentActor = new(value, isDefault) },
        "minutes_per_item" => a with
        {
            MinutesPerItem = new(value == "null" ? null : decimal.Parse(value, CultureInfo.InvariantCulture), isDefault),
        },
        "failure_impact" => a with { FailureImpact = new(value, isDefault) },
        "existing_feature" => a with { ExistingFeature = new(value, isDefault) },
        "human_signoff" => a with { HumanSignoff = new(value, isDefault) },
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "4.4 표에 없는 키"),
    };

    public static StepAnswers With(StepAnswers a, IEnumerable<(string Key, string Value)> changes) =>
        changes.Aggregate(a, (current, c) => With(current, c.Key, c.Value));

    // 기준 답에서 바꾼 답. "k: v, k: v".
    public static StepAnswers Answers(string changes) => With(Base, Pairs(changes));

    // 값은 그대로 두고 그 키만 IsDefault = true로 둔다(스펙 6.2).
    public static StepAnswers MarkDefault(StepAnswers a, string key) =>
        With(a, key, Value(a, key), isDefault: true);

    // 답 하나의 값(InvariantCulture, null은 "null").
    public static string Value(StepAnswers a, string key) =>
        CaseDefaultsTests.AllAnswers(a).Single(x => x.Key == key).Value;

    // 키 → 값.
    public static Dictionary<string, string> Values(StepAnswers a) =>
        CaseDefaultsTests.AllAnswers(a).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

    // IsDefault인 키(4.4 표 순서).
    public static string[] DefaultKeys(StepAnswers a) =>
        CaseDefaultsTests.AllAnswers(a).Where(x => x.IsDefault).Select(x => x.Key).ToArray();

    // 단계 여럿의 케이스. 단계 번호는 1부터, steps 순서.
    public static CaseDefinition Definition(string caseName, params (string Name, StepAnswers Answers)[] steps) =>
        new(
            caseName,
            "t",
            null,
            null,
            null,
            steps.Select((s, i) => new CaseStep(i + 1, s.Name, s.Answers)).ToArray(),
            null,
            [],
            new CaseThresholds(null, null, null));

    // 단계 하나를 분류한다.
    public static StepClassification One(StepAnswers a, string name = "s") =>
        Assert.Single(StepClassifier.Classify(Definition("p", (name, a))).Steps);

    // 행을 "② ELSE" 모양으로.
    public static string[] RowLabels(StepClassification s) => s.Rows.Select(r => $"{r.Assignee} {r.Rule}").ToArray();

    // 스펙 4.5 조합: 입력 5 × 기준 3 × 빈도 4 × 건수 4 × 영향 3 × 기존 기능 3 × 서명 2 = 4,320개. 나머지 답은 기준 답.
    public static IReadOnlyList<StepAnswers> AllCombinations()
    {
        var (inputs, rules, frequencies) = (Choices("input_kind"), Choices("rule_expressible"), Choices("frequency"));
        var (impacts, existing, signoffs) = (Choices("failure_impact"), Choices("existing_feature"), Choices("human_signoff"));
        var all =
            from ik in inputs
            from re in rules
            from fr in frequencies
            from bs in BatchSizes
            from fi in impacts
            from ef in existing
            from hs in signoffs
            select With(Base, [
                ("input_kind", ik),
                ("rule_expressible", re),
                ("frequency", fr),
                ("batch_size", bs.ToString(CultureInfo.InvariantCulture)),
                ("failure_impact", fi),
                ("existing_feature", ef),
                ("human_signoff", hs),
            ]);
        return all.ToArray();
    }

    public static byte[] Bytes(string yaml) => Utf8NoBom.GetBytes(yaml);

    public static string Text(byte[] bytes) => Utf8NoBom.GetString(bytes);

    public static string ExamplePath(string example, string relative) =>
        RepoRoot.Combine($"{ExamplesRelativePath}/{example}/{relative}");

    // 공개 예제 answers.yaml → 파서(이름 answers.yaml) → 분류 → classification.json 바이트. 파싱 오류면 실패한다.
    public static byte[] RenderAnswers(byte[] answersYaml) =>
        ClassificationJson.Render(StepClassifier.Classify(ParseAnswers(answersYaml)));

    public static CaseDefinition ParseAnswers(byte[] answersYaml) =>
        CaseInput.Valid(CaseParser.Parse(answersYaml, "answers.yaml"));

    private static StepAnswers ReadBase()
    {
        var line = SpecDocument.Line(SpecDocument.ClassificationPath, BaseLinePrefix);
        var pairs = Pairs(SpecDocument.CodeSpans(line)[0]);
        Assert.Contains("(모두 직접 쓴 값)", line, StringComparison.Ordinal);
        Assert.Equal(Keys, pairs.Select(p => p.Key));

        // 시작 값은 아무것이나 좋다. 10개를 모두 덮어쓴다.
        var start = new StepAnswers(
            new("?", true), new("?", true), new("?", true), new(0, true), new(0m, true),
            new("?", true), new(null, true), new("?", true), new("?", true), new("?", true));
        var answers = With(start, pairs);
        Assert.Empty(DefaultKeys(answers));
        return answers;
    }
}
