using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Classification;

// 스펙 4.12 추정 표시(T-008, ADR 0004 결정 2·질문 2)와 6.5 표본. 카드 수용 기준 3.
// 규칙: 행을 정할 때 읽은 답(4.6 표) 중 하나라도 IsInferred면 문장 끝에 " (AI 추정, 확인 필요)".
// 기본값 표시와 같이 붙으면 기본값 표시 뒤에 둔다. 분류(담당·근거 키·행)·defaults·확인 항목·AI 과잉 후보는 추정과 상관없다.
[Trait("Spec", "classification")]
public class InferredMarkTests
{
    private const string SignoffInferredHeading = "### 6.5 서명 단계와 추정 표시 (T-008)";
    private const string ExampleHeader = "| 추정인 답 | 문장 표시 |";
    private const string RuleBullet = "- 추정 표시 규칙:";
    private const string OrderBullet = "- 기본값 표시와 같이 붙으면 기본값 표시 뒤에 둔다:";

    // 4.12 예시 표를 옮긴 것. 표 칸 두 개(그대로)와, 그 행이 뜻하는 경우들.
    // 경우: 추정인 답, "키 없음"(기본값)인 답, 기준 답(6.1)에서 바꾼 답, 기대 행("근거 키:표시"). 표시는 "", "D", "I", "DI"(MarkCode).
    // 표가 바뀌면 Example_table_is_transcribed_exactly가 실패한다. 그때 이 목록을 표에 맞춘다.
    private static readonly ExampleRow[] Examples =
    [
        new("`frequency`", "`step` 행", [new(["frequency"], [], "", ["ELSE:I"])]),
        new("`failure_impact`, `failure_impact: external`", "`step`·게이트 두 행", [new(["failure_impact"], [], "failure_impact: external", ["ELSE:I", "GATE:I"])]),
        new("`rule_expressible`, `rule_expressible: partial`", "`step`·예외 검토 두 행", [new(["rule_expressible"], [], "rule_expressible: partial", ["ELSE:I", "PARTIAL:I"])]),
        new(
            "`exception_rate` / `current_actor` / `minutes_per_item`",
            "없음(트리가 읽지 않는다). `Inferred`에는 남는다",
            [new(["exception_rate"], [], "", ["ELSE:"]), new(["current_actor"], [], "", ["ELSE:"]), new(["minutes_per_item"], [], "", ["ELSE:"])]),
        new("`batch_size`, `frequency: weekly`", "없음", [new(["batch_size"], [], "frequency: weekly", ["ELSE:"])]),
        new(
            "`batch_size`, `frequency: rare, batch_size: 10`(ELSE) / `3`(Q4)",
            "`step` 행",
            [new(["batch_size"], [], "frequency: rare, batch_size: 10", ["ELSE:I"]), new(["batch_size"], [], "frequency: rare, batch_size: 3", ["Q4:I"])]),
        new("`input_kind`, `existing_feature: yes`(Q1)", "없음(Q1은 `input_kind`를 읽지 않는다)", [new(["input_kind"], [], "existing_feature: yes", ["Q1:"])]),
        new("`human_signoff`, `human_signoff: yes`", "`SIGNOFF` 행", [new(["human_signoff"], [], "human_signoff: yes", ["SIGNOFF:I"])]),
        new("`frequency`·`failure_impact`, `human_signoff: yes`", "없음(`SIGNOFF`는 `human_signoff`만 읽는다)", [new(["frequency", "failure_impact"], [], "human_signoff: yes", ["SIGNOFF:"])]),
        new("`frequency`, `existing_feature` 키 없음", "`step` 행에 두 표시(기본값 뒤 추정)", [new(["frequency"], ["existing_feature"], "", ["ELSE:DI"])]),
        new(
            "`failure_impact`, `human_signoff` 키 없음, `failure_impact: external`",
            "`step` 행에 두 표시, 게이트 행에 추정 표시만",
            [new(["failure_impact"], ["human_signoff"], "failure_impact: external", ["ELSE:DI", "GATE:I"])]),
    ];

    public static TheoryData<int> ExampleIndexes => new(Enumerable.Range(0, Examples.Length));

    // 표시 문자열은 스펙 4.12 규칙 글머리의 "공백 하나와 `(AI 추정, 확인 필요)`"다.
    // 순서 글머리의 예시는 "… (기본값 사용, 확인 필요) (AI 추정, 확인 필요)"로 끝난다(기본값 표시 뒤).
    [Fact]
    public void Inferred_mark_matches_spec_bullets()
    {
        var rule = SpecDocument.Line(ClassificationSpec.Document, RuleBullet);
        var order = SpecDocument.Line(ClassificationSpec.Document, OrderBullet);

        Assert.Contains("공백 하나와 `" + InferredInput.InferredMark.TrimStart() + "`", rule, StringComparison.Ordinal);
        Assert.Equal(" ", InferredInput.InferredMark[..1]);
        Assert.EndsWith(ClassificationSpec.DefaultMark + InferredInput.InferredMark, SpecDocument.CodeSpans(order)[0], StringComparison.Ordinal);
    }

    // 4.12 예시 표 = 이 파일의 목록(칸 글자 그대로, 행 순서까지).
    [Fact]
    public void Example_table_is_transcribed_exactly()
    {
        var table = SpecDocument.Table(ClassificationSpec.Document, ExampleHeader);

        Assert.Equal(table.Select(c => (c[0], c[1])), Examples.Select(e => (e.AnswerCell, e.MarkCell)));
    }

    // 4.12 예시 표 각 행. 경우마다 행(근거 키·표시)과 Inferred·Defaults·문장 전체를 본다.
    [Theory]
    [MemberData(nameof(ExampleIndexes))]
    public void Example_row(int index)
    {
        var entries = ClassificationSpec.UsedEntries();
        var templates = ClassificationSpec.Templates();
        var phrases = ClassificationSpec.Phrases();
        foreach (var c in Examples[index].Cases)
        {
            var a = ClassificationInput.Answers(c.Changes);
            a = c.MissingKeys.Aggregate(a, InferredInput.MissingKey);
            a = InferredInput.MarkInferred(a, c.InferredKeys);
            var step = ClassificationInput.One(a);

            Assert.Equal(c.ExpectedRows, step.Rows.Select(r => $"{r.Rule}:{InferredInput.MarkCode(r.Reason)}"));
            Assert.Equal(ClassificationInput.Keys.Where(c.InferredKeys.Contains), step.Inferred);
            Assert.Equal(ClassificationInput.Keys.Where(c.MissingKeys.Contains), step.Defaults);
            Assert.All(step.Rows, r => Assert.Equal(InferredInput.ExpectedReason(templates[r.Rule].Text, a, ClassificationSpec.UsedFor(entries, r.Rule, a), phrases), r.Reason));
        }
    }

    // 4.12 규칙 전체: 조합 4,320개 × 키 10개. 키 하나만 IsInferred(값·IsDefault는 그대로)일 때,
    // 읽은 답에 그 키가 있는 행만 추정 표시가 문장 끝에 한 번 붙는다. Inferred는 그 키 하나, defaults는 빈 목록.
    // 분류(행 종류·담당·근거 키·읽은 답)와 확인 항목·AI 과잉 후보는 추정이 없을 때와 같다.
    [Fact]
    public void Inferred_mark_appears_exactly_on_rows_that_read_the_inferred_answer()
    {
        var entries = ClassificationSpec.UsedEntries();
        var templates = ClassificationSpec.Templates();
        var phrases = ClassificationSpec.Phrases();
        var keys = ClassificationInput.Keys;
        var differences = new List<string>();
        var marked = 0;

        foreach (var explicitAnswers in ClassificationInput.AllCombinations())
        {
            var plain = ClassificationInput.One(explicitAnswers);
            foreach (var key in keys)
            {
                var a = InferredInput.MarkInferred(explicitAnswers, key);
                var step = ClassificationInput.One(a);
                if (!step.Inferred.SequenceEqual([key]) || step.Defaults.Count != 0)
                {
                    differences.Add($"{key}: inferred [{string.Join(",", step.Inferred)}] defaults [{string.Join(",", step.Defaults)}]");
                }

                if (!step.Checks.SequenceEqual(plain.Checks) || step.AiOveruseCandidate != plain.AiOveruseCandidate)
                {
                    differences.Add($"{key}: 확인 항목·AI 과잉 후보가 추정 때문에 바뀌었다");
                }

                if (!Shape(step).SequenceEqual(Shape(plain)))
                {
                    differences.Add($"{key}: 분류가 추정 때문에 바뀌었다 [{string.Join(" | ", Shape(plain))}] → [{string.Join(" | ", Shape(step))}]");
                    continue;
                }

                foreach (var row in step.Rows)
                {
                    var mark = ClassificationSpec.UsedFor(entries, row.Rule, a).Contains(key);
                    marked += mark ? 1 : 0;
                    var expected = ClassificationSpec.Render(templates[row.Rule].Text, a, phrases) + (mark ? InferredInput.InferredMark : string.Empty);
                    if (expected != row.Reason)
                    {
                        differences.Add($"{key} 추정, {row.Rule}\n  스펙: {expected}\n  Core: {row.Reason}");
                    }
                }
            }
        }

        Assert.True(marked > 0);
        Assert.True(differences.Count == 0, $"차이 {differences.Count}건(앞 20건):\n" + string.Join("\n", differences.Take(20)));
    }

    // 4.12 순서: 읽은 답 중 하나는 기본값, 다른 하나는 추정이면 두 표시가 "기본값 뒤 추정" 순서로 붙는다.
    // 조합 4,320개 × (기본값 키, 추정 키) 순서쌍. 키는 4.6 읽은 답 표에 나오는 것만 쓴다(7개, 쌍 42개).
    // 트리가 읽지 않는 키는 어느 행에도 표시를 만들지 않는다(위 테스트와 DefaultMarkTests가 본다).
    // 문장 = 틀 + 기본값 표시(읽었으면) + 추정 표시(읽었으면).
    [Fact]
    public void Default_mark_comes_before_inferred_mark_for_all_pairs()
    {
        var entries = ClassificationSpec.UsedEntries();
        var templates = ClassificationSpec.Templates();
        var phrases = ClassificationSpec.Phrases();
        var keys = ClassificationInput.Keys.Where(k => entries.Any(e => e.Keys.Contains(k))).ToArray();
        Assert.Equal(7, keys.Length);
        var differences = new List<string>();
        var both = 0;

        foreach (var explicitAnswers in ClassificationInput.AllCombinations())
        {
            foreach (var defaultKey in keys)
            {
                var withDefault = ClassificationInput.MarkDefault(explicitAnswers, defaultKey);
                foreach (var inferredKey in keys.Where(k => k != defaultKey))
                {
                    var a = InferredInput.MarkInferred(withDefault, inferredKey);
                    foreach (var row in ClassificationInput.One(a).Rows)
                    {
                        var used = ClassificationSpec.UsedFor(entries, row.Rule, a);
                        var expected = InferredInput.ExpectedReason(templates[row.Rule].Text, a, used, phrases);
                        both += used.Contains(defaultKey) && used.Contains(inferredKey) ? 1 : 0;
                        if (expected != row.Reason)
                        {
                            differences.Add($"기본값 {defaultKey}, 추정 {inferredKey}, {row.Rule}\n  스펙: {expected}\n  Core: {row.Reason}");
                        }
                    }
                }
            }
        }

        Assert.True(both > 0, "두 표시가 같이 붙는 행이 하나도 없다.");
        Assert.True(differences.Count == 0, $"차이 {differences.Count}건(앞 20건):\n" + string.Join("\n", differences.Take(20)));
    }

    // 4.12: 두 표시가 같이 붙으면 문장은 "… (기본값 사용, 확인 필요) (AI 추정, 확인 필요)"로 끝나고, 표시는 각각 한 번이다.
    [Fact]
    public void Both_marks_end_the_sentence_default_first()
    {
        var a = InferredInput.MarkInferred(InferredInput.MissingKey(ClassificationInput.Base, "existing_feature"), "frequency");
        var row = Assert.Single(ClassificationInput.One(a).Rows);

        Assert.EndsWith(ClassificationSpec.DefaultMark + InferredInput.InferredMark, row.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(InferredInput.InferredMark + ClassificationSpec.DefaultMark, row.Reason, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(row.Reason, ClassificationSpec.DefaultMark));
        Assert.Equal(1, Occurrences(row.Reason, InferredInput.InferredMark));
        Assert.True(
            row.Reason.IndexOf(ClassificationSpec.DefaultMark, StringComparison.Ordinal) < row.Reason.IndexOf(InferredInput.InferredMark, StringComparison.Ordinal),
            row.Reason);
    }

    // 4.12: 읽은 답 여럿이 추정이어도 표시는 한 번이다.
    [Fact]
    public void Several_inferred_answers_give_one_mark()
    {
        var a = InferredInput.MarkInferred(ClassificationInput.Base, ClassificationInput.Keys);
        var step = ClassificationInput.One(a);

        Assert.Equal(ClassificationInput.Keys, step.Inferred);
        Assert.All(step.Rows, r => Assert.Equal(1, Occurrences(r.Reason, InferredInput.InferredMark)));
        Assert.All(step.Rows, r => Assert.EndsWith(InferredInput.InferredMark, r.Reason, StringComparison.Ordinal));
    }

    // 도우미의 키 순서가 case-schema 4.4 표 순서와 같다(Inferred는 이 순서로 나온다).
    [Fact]
    public void Flag_keys_follow_spec_table_order()
    {
        Assert.Equal(ClassificationInput.Keys, InferredInput.Flags(ClassificationInput.Base).Select(f => f.Key));
    }

    // 6.5 표본 파일 = 스펙 블록(바이트).
    [Fact]
    public void Signoff_inferred_sample_matches_spec_block()
    {
        var blocks = SpecDocument.CodeBlocks(ClassificationSpec.Document, SignoffInferredHeading);

        Assert.Equal(2, blocks.Count);
        Assert.Equal(Encoding.UTF8.GetBytes(blocks[0]), InferredInput.SignoffInferredBytes());
    }

    // 6.5 분류 결과 블록: 행마다 "<단계 번호> <담당> <근거 키>: <근거 문장>", 끝에 단계마다 "inferred <번호>: <키, 4.4 순서>".
    [Fact]
    public void Signoff_inferred_sample_gives_spec_result_block()
    {
        var block = SpecDocument.BlockLines(SpecDocument.CodeBlocks(ClassificationSpec.Document, SignoffInferredHeading)[1]);
        var result = StepClassifier.Classify(ClassificationInput.ParseAnswers(InferredInput.SignoffInferredBytes()));
        var lines = result.Steps.SelectMany(s => s.Rows.Select(r => $"{s.Number} {r.Assignee} {r.Rule}: {r.Reason}"))
            .Concat(result.Steps.Select(s => $"inferred {s.Number}: {string.Join(", ", s.Inferred)}"));

        Assert.Equal(block, lines);
    }

    // 6.5 글머리: 1단계 Inferred는 쓴 순서가 아니라 4.4 순서. 2단계 existing_feature 기본값 → defaults(schema 2는 확인 항목 없음).
    // 3단계 SIGNOFF 한 행(게이트 없음, 표시 없음). 4단계 human_signoff 기본값 표시, AI 과잉 후보.
    [Fact]
    public void Signoff_inferred_sample_step_fields_follow_spec_bullets()
    {
        var steps = StepClassifier.Classify(ClassificationInput.ParseAnswers(InferredInput.SignoffInferredBytes())).Steps;

        Assert.Equal(["frequency", "batch_size"], steps[0].Inferred);
        Assert.Equal(["existing_feature"], steps[1].Defaults);
        Assert.Empty(steps[1].Checks);
        Assert.Equal(["SIGNOFF"], steps[2].Rows.Select(r => r.Rule));
        Assert.Equal(string.Empty, InferredInput.MarkCode(steps[2].Rows[0].Reason));
        Assert.Equal(["human_signoff"], steps[3].Defaults);
        Assert.Equal([false, false, false, true], steps.Select(s => s.AiOveruseCandidate));
        Assert.Equal([1, 3, 1, 1], steps.Select(s => s.Rows.Count));
    }

    // 3.2·4.12: classification.json에 inferred 속성이 없다. 같은 답에서 inferred 줄만 뺀 파일과 reason 4줄만 다르다.
    [Fact]
    public void Json_has_no_inferred_property_and_differs_only_in_reasons()
    {
        var withInferred = ClassificationInput.Text(InferredInput.SignoffInferredBytes());
        var withoutInferred = string.Join("\n", withInferred.Split('\n').Where(l => !l.StartsWith("    inferred:", StringComparison.Ordinal)));
        Assert.NotEqual(withInferred, withoutInferred);

        var json = ClassificationInput.Text(ClassificationInput.RenderAnswers(ClassificationInput.Bytes(withInferred))).Split('\n');
        var plain = ClassificationInput.Text(ClassificationInput.RenderAnswers(ClassificationInput.Bytes(withoutInferred))).Split('\n');
        var differing = Enumerable.Range(0, json.Length).Where(i => json[i] != plain[i]).ToArray();

        // 케이스 이름(signoff-inferred)에는 낱말이 있으므로 속성 이름만 본다. 두 JSON의 속성 이름 목록(문서 순서)이 같다.
        var names = PropertyNames(ClassificationInput.RenderAnswers(ClassificationInput.Bytes(withInferred)));
        Assert.DoesNotContain(names, n => n.Contains("inferred", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(PropertyNames(ClassificationInput.RenderAnswers(ClassificationInput.Bytes(withoutInferred))), names);
        Assert.Equal(plain.Length, json.Length);
        Assert.Equal(4, differing.Length);
        Assert.All(differing, i => Assert.StartsWith("\"reason\": ", json[i].TrimStart(), StringComparison.Ordinal));
        Assert.All(differing, i => Assert.EndsWith(InferredInput.InferredMark + "\"", json[i], StringComparison.Ordinal));
        Assert.All(differing, i => Assert.Equal(plain[i][..^1] + InferredInput.InferredMark + "\"", json[i]));
    }

    // 결정론(4.10): 추정·서명이 있어도 같은 입력이면 같은 바이트다. 두 번, 그리고 de-DE·tr-TR 문화권에서도 같다.
    // 원본 불변: 표본 파일 바이트가 읽기 전후 같다.
    [Fact]
    public void Sample_renders_same_bytes_across_runs_and_cultures_and_file_is_unchanged()
    {
        var before = SHA256.HashData(InferredInput.SignoffInferredBytes());
        var first = ClassificationInput.RenderAnswers(InferredInput.SignoffInferredBytes());

        Assert.Equal(first, ClassificationInput.RenderAnswers(InferredInput.SignoffInferredBytes()));
        foreach (var name in new[] { "de-DE", "tr-TR" })
        {
            var (saved, savedUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                Assert.Equal(first, ClassificationInput.RenderAnswers(InferredInput.SignoffInferredBytes()));
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
                CultureInfo.CurrentUICulture = savedUi;
            }
        }

        Assert.Equal(before, SHA256.HashData(InferredInput.SignoffInferredBytes()));
    }

    // JSON의 모든 속성 이름(문서 순서, 깊이 우선).
    private static List<string> PropertyNames(byte[] json)
    {
        var names = new List<string>();
        using var document = JsonDocument.Parse(json);
        Walk(document.RootElement);
        return names;

        void Walk(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    names.Add(property.Name);
                    Walk(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item);
                }
            }
        }
    }

    // 비교용 분류 모양(문장 제외).
    private static string[] Shape(StepClassification s) =>
        s.Rows.Select(r => $"{r.Kind} {r.Assignee} {r.Rule} [{string.Join(",", r.Used)}]").ToArray();

    private static int Occurrences(string text, string part)
    {
        var count = 0;
        for (var i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private sealed record ExampleRow(string AnswerCell, string MarkCell, ExampleCase[] Cases);

    private sealed record ExampleCase(string[] InferredKeys, string[] MissingKeys, string Changes, string[] ExpectedRows);
}
