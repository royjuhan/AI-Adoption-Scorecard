using Scorecard.Core.Cases;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Classification;

// T-008 추정 표시 테스트 도우미(스펙 4.12, 6.5). 답에 IsInferred를 켜고, 추정 키를 4.4 표 순서로 편다.
// ClassificationInput.With는 Answer를 새로 만들어 IsInferred를 false로 되돌린다. 그래서 기본값·값을 먼저 정하고 추정은 마지막에 켠다.
internal static class InferredInput
{
    // 스펙 4.12 "공백 하나와 `(AI 추정, 확인 필요)`". 스펙 글머리와 같은지는 InferredMarkTests가 본다.
    public const string InferredMark = " (AI 추정, 확인 필요)";

    // 스펙 6.5 TestData 표본(블록 = 파일).
    public const string SignoffInferredSample = "tests/Scorecard.Tests/TestData/classification/signoff-inferred/answers.yaml";

    public static byte[] SignoffInferredBytes() => File.ReadAllBytes(RepoRoot.Combine(SignoffInferredSample));

    // 값·IsDefault는 그대로 두고 그 키만 IsInferred = true.
    public static StepAnswers MarkInferred(StepAnswers a, string key) => key switch
    {
        "input_kind" => a with { InputKind = a.InputKind with { IsInferred = true } },
        "rule_expressible" => a with { RuleExpressible = a.RuleExpressible with { IsInferred = true } },
        "frequency" => a with { Frequency = a.Frequency with { IsInferred = true } },
        "batch_size" => a with { BatchSize = a.BatchSize with { IsInferred = true } },
        "exception_rate" => a with { ExceptionRate = a.ExceptionRate with { IsInferred = true } },
        "current_actor" => a with { CurrentActor = a.CurrentActor with { IsInferred = true } },
        "minutes_per_item" => a with { MinutesPerItem = a.MinutesPerItem with { IsInferred = true } },
        "failure_impact" => a with { FailureImpact = a.FailureImpact with { IsInferred = true } },
        "existing_feature" => a with { ExistingFeature = a.ExistingFeature with { IsInferred = true } },
        "human_signoff" => a with { HumanSignoff = a.HumanSignoff with { IsInferred = true } },
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "4.4 표에 없는 키"),
    };

    public static StepAnswers MarkInferred(StepAnswers a, IEnumerable<string> keys) =>
        keys.Aggregate(a, MarkInferred);

    // 그 키를 "키 없음"으로 둔다: 4.4 표의 기본값, IsDefault = true(파서가 키 없는 답에 하는 것과 같다).
    public static StepAnswers MissingKey(StepAnswers a, string key) =>
        ClassificationInput.With(a, key, DefaultRow.ReadAll().Single(r => r.Key == key).Default, isDefault: true);

    // 답 10개 중 IsInferred인 키(4.4 표 순서). 키 목록이 4.4 표와 같은지는 Flag_keys_follow_spec_table_order가 본다.
    // 반복문에서 부르므로 스펙 파일을 읽지 않는다.
    public static string[] InferredKeys(StepAnswers a) => Flags(a).Where(f => f.IsInferred).Select(f => f.Key).ToArray();

    public static (string Key, bool IsInferred)[] Flags(StepAnswers a)
    {
        (string Key, bool IsInferred)[] flags =
        [
            ("input_kind", a.InputKind.IsInferred),
            ("rule_expressible", a.RuleExpressible.IsInferred),
            ("frequency", a.Frequency.IsInferred),
            ("batch_size", a.BatchSize.IsInferred),
            ("exception_rate", a.ExceptionRate.IsInferred),
            ("current_actor", a.CurrentActor.IsInferred),
            ("minutes_per_item", a.MinutesPerItem.IsInferred),
            ("failure_impact", a.FailureImpact.IsInferred),
            ("existing_feature", a.ExistingFeature.IsInferred),
            ("human_signoff", a.HumanSignoff.IsInferred),
        ];
        return flags;
    }

    // 행 하나의 기대 문장: 문장 틀 + 기본값 표시(읽은 답에 기본값이 있으면) + 추정 표시(읽은 답에 추정이 있으면). 이 순서다.
    public static string ExpectedReason(string template, StepAnswers a, IReadOnlyList<string> used, IReadOnlyDictionary<(string Key, string Value), string> phrases)
    {
        var defaults = ClassificationInput.DefaultKeys(a);
        var inferred = InferredKeys(a);
        var text = ClassificationSpec.Render(template, a, phrases);
        if (used.Any(defaults.Contains))
        {
            text += ClassificationSpec.DefaultMark;
        }

        if (used.Any(inferred.Contains))
        {
            text += InferredMark;
        }

        return text;
    }

    // 문장 끝 표시를 짧은 글자로: "" / "D" / "I" / "DI". 표시 순서가 다르면 "ID", 같은 표시가 두 번이면 "DD" 같은 모양이 된다.
    public static string MarkCode(string reason)
    {
        var code = string.Empty;
        var text = reason;
        while (true)
        {
            if (text.EndsWith(InferredMark, StringComparison.Ordinal))
            {
                code = "I" + code;
                text = text[..^InferredMark.Length];
            }
            else if (text.EndsWith(ClassificationSpec.DefaultMark, StringComparison.Ordinal))
            {
                code = "D" + code;
                text = text[..^ClassificationSpec.DefaultMark.Length];
            }
            else
            {
                return code;
            }
        }
    }
}
