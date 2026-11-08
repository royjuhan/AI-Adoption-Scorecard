using System.Text.Json;
using System.Text.RegularExpressions;
using Scorecard.Core.Cases;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Classification;

// 승인된 분류 스펙(docs/spec/classification.md)의 표를 읽는다. Core를 쓰지 않는다.
// 스펙 4.9(승인 결정 9): Core는 값·문장 틀을 상수로 두고, 테스트가 이 표와 대조한다.
// 표 모양(머리 행)은 스펙이 "바꾸지 않는다"고 정했다. 머리 행이 문서에 정확히 한 번 나와야 한다.
internal static partial class ClassificationSpec
{
    public const string Document = SpecDocument.ClassificationPath;

    public const string ConditionHeader = "| 근거 키 | 키 | 비교 | 값 |";
    public const string TemplateHeader = "| 근거 키 | 담당 | 행 종류 | 문장 틀 |";
    public const string PhraseHeader = "| 키 | 값 | 문구 |";
    public const string UsedHeader = "| 근거 키 | 읽은 답 |";
    public const string CheckHeader = "| 키 | 조건 | 뜻 |";
    public const string ValueHeader = "| 값 | 뜻 |";
    public const string BranchHeader = "| 바꾼 답 | 행 |";
    public const string MarkHeader = "| 기본값인 답 | 문장 표시 |";
    public const string EscapeHeader = "| 단계 이름에 든 글자 | JSON 안 모양 |";
    public const string GuardHeader = "| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |";

    public const string ExampleSummaryHeader =
        "| 예제 | 사용자 단계 | 행(모델 A) | ② | ③ | ④ 예외 검토 | ④ 게이트 | 확인 항목 | AI 과잉 후보 | 기본값 표시 문장 |";

    // 지시문 12절 공개 예제 표.
    public const string InstructionsExampleHeader = "| 예제 | 업무 | 단계 수 | 기대 분류(요지) | 검증 자료 |";

    // 3.2 모양 블록의 제목. 속성 이름·순서·첫 키는 이 블록에서 읽는다.
    public const string ShapeHeading = "### 3.2 classification.json";

    // ADR 0004 결정 2(T-007): classification.json의 첫 키는 "schema_version". 값은 정수 2(ADR 0013, M2-2).
    // 스펙 3.2 블록이 같은 값을 적었는지는 테스트가 본다.
    public const string SchemaVersionKey = "schema_version";
    public const int SchemaVersion = 2;

    // 스펙 4.6. 공백 하나 + "(기본값 사용, 확인 필요)". 지시문 3.3·스펙 4.6 문구와 같은지는 테스트가 본다.
    public const string DefaultMark = " (기본값 사용, 확인 필요)";

    // 스펙 4.7 확인 항목 키. confirm_existing_feature는 schema 2에서 뺐다(ADR 0013 결정 3). 나오지 않는지 볼 때만 쓴다.
    public const string MeasureMinutesPerItem = "measure_minutes_per_item";
    public const string RemovedConfirmExistingFeature = "confirm_existing_feature";

    // 스펙 4.2의 행을 만들지 않는 조건.
    public const string OveruseRule = "OVERUSE";

    // 스펙 6.6 AI 판단 장면(M2-2). 비교 값 표와 장면 표.
    public const string AiCompareHeader = "| AI 담당 | 비교 값 | 뜻 |";
    public const string AiSceneHeader = "| 바꾼 답 | AI 담당 | 규칙 의견 | differs | AI 과잉 후보 | 행 |";

    // 스펙 6.7 AI 판단 표본. 블록 앞의 표시 줄 머리.
    public const string AiSampleYamlMarker = "AI 판단 표본 `tests/Scorecard.Tests/TestData/classification/ai-judgment/answers.yaml`";
    public const string AiSampleJsonMarker = "AI 판단 표본 classification.json (SHA-256 `";

    // 스펙 4.4 행 종류.
    public const string KindStep = "step";
    public const string KindExceptionReview = "exception_review";
    public const string KindHumanGate = "human_gate";

    public static IReadOnlyList<string[]> Table(string header) => SpecDocument.Table(Document, header);

    // 3.2 모양 블록(코드 블록 하나). 줄은 LF로 잇고 끝에 LF 하나(SpecDocument.CodeBlocks).
    public static string ShapeBlock() => Assert.Single(SpecDocument.CodeBlocks(Document, ShapeHeading));

    // 3.2 모양 블록의 최상위 속성 이름(블록 순서).
    public static string[] ShapeTopLevelNames()
    {
        using var shape = JsonDocument.Parse(ShapeBlock());
        return shape.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
    }

    // 4.2 조건 표.
    public static IReadOnlyList<Condition> Conditions() =>
        Table(ConditionHeader)
            .Select(c => new Condition(SpecDocument.Unquote(c[0]), SpecDocument.Unquote(c[1]), SpecDocument.Unquote(c[2]), SpecDocument.CodeSpans(c[3])))
            .ToArray();

    // 4.5 문장 틀 표. 근거 키 → 담당·행 종류·문장 틀.
    public static IReadOnlyDictionary<string, Template> Templates() =>
        Table(TemplateHeader).ToDictionary(
            c => SpecDocument.Unquote(c[0]),
            c => new Template(SpecDocument.Unquote(c[0]), c[1].Trim(), SpecDocument.Unquote(c[2]), SpecDocument.Unquote(c[3])),
            StringComparer.Ordinal);

    // 4.5 문구 표. (키, 값) → 문구.
    public static IReadOnlyDictionary<(string Key, string Value), string> Phrases() =>
        Table(PhraseHeader).ToDictionary(c => (SpecDocument.Unquote(c[0]), SpecDocument.Unquote(c[1])), c => c[2].Trim());

    // 4.6 읽은 답 표. 근거 키 칸은 "ELSE"나 "ELSE (frequency = rare)" 모양이다.
    public static IReadOnlyList<UsedEntry> UsedEntries() =>
        Table(UsedHeader).Select(c =>
        {
            var label = SpecDocument.Unquote(c[0]);
            var match = UsedLabel().Match(label);
            Assert.True(match.Success, $"4.6 읽은 답 표의 근거 키 칸 '{label}'을 읽을 수 없다.");
            var condition = match.Groups["key"].Success ? (match.Groups["key"].Value, match.Groups["value"].Value) : ((string, string)?)null;
            return new UsedEntry(match.Groups["rule"].Value, condition, SpecDocument.CodeSpans(c[1]));
        }).ToArray();

    // 4.7 확인 항목 키(표 순서).
    public static string[] CheckKeys() => Table(CheckHeader).Select(c => SpecDocument.Unquote(c[0])).ToArray();

    // 행 하나를 정할 때 트리가 읽은 답(4.6 표). 조건이 붙은 줄이 맞으면 그 줄, 아니면 조건 없는 줄.
    public static string[] UsedFor(IReadOnlyList<UsedEntry> entries, string rule, StepAnswers a)
    {
        var values = ClassificationInput.Values(a);
        var candidates = entries.Where(e => e.Rule == rule).ToArray();
        var conditioned = candidates.Where(e => e.Condition is { } c && values[c.Key] == c.Value).ToArray();
        var chosen = conditioned.Length > 0 ? conditioned : candidates.Where(e => e.Condition is null).ToArray();
        Assert.True(chosen.Length == 1, $"4.6 읽은 답 표에서 근거 키 {rule}의 줄을 하나로 정할 수 없다({chosen.Length}개).");
        return chosen[0].Keys;
    }

    // 4.5 문장 틀에 답을 넣는다. {키 문구}는 문구 표, {키}는 값(InvariantCulture). 문구 안의 {batch_size}도 바꾼다.
    // 남은 {…}가 있거나 문구가 없으면 실패한다.
    public static string Render(string template, StepAnswers a, IReadOnlyDictionary<(string Key, string Value), string> phrases)
    {
        var values = ClassificationInput.Values(a);
        var text = PhrasePlaceholder().Replace(template, m =>
        {
            var key = m.Groups["key"].Value;
            Assert.True(
                phrases.TryGetValue((key, values[key]), out var phrase),
                $"4.5 문구 표에 ({key}, {values[key]})이 없다. 문장 틀: {template}");
            return phrase;
        });
        text = ValuePlaceholder().Replace(text, m => values[m.Groups["key"].Value]);
        Assert.DoesNotContain('{', text);
        Assert.DoesNotContain('}', text);
        return text;
    }

    [GeneratedRegex(@"^(?<rule>[A-Z0-9_]+)( \((?<key>[a-z_]+) = (?<value>[a-z_0-9]+)\))?\z", RegexOptions.CultureInvariant)]
    private static partial Regex UsedLabel();

    [GeneratedRegex(@"\{(?<key>[a-z_]+) 문구\}", RegexOptions.CultureInvariant)]
    private static partial Regex PhrasePlaceholder();

    [GeneratedRegex(@"\{(?<key>[a-z_]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex ValuePlaceholder();
}

// 4.2 조건 표의 한 행. 같은 근거 키의 행은 모두 참이어야 걸린다(그리고).
internal sealed record Condition(string Rule, string Key, string Op, string[] Values);

// 4.5 문장 틀 표의 한 행.
internal sealed record Template(string Rule, string Assignee, string Kind, string Text);

// 4.6 읽은 답 표의 한 행. Condition은 "ELSE (frequency = rare)"의 (frequency, rare).
internal sealed record UsedEntry(string Rule, (string Key, string Value)? Condition, string[] Keys);
