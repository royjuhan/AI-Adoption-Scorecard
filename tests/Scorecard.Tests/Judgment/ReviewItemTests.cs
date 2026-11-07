using System.Security.Cryptography;
using System.Text;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Comparison;
using Scorecard.Tests.Structure;
using Scorecard.Tests.Synthetic;

namespace Scorecard.Tests.Judgment;

// 스펙 11.3 검토 항목(T-012, 질문 1). 사람이 보고 설명해야 하는 대조 항목은 불일치·추가 행·중복 행이다.
// 11.9 끝 compare-duplicate 판정 블록(metrics.md 12.5 표본)도 여기서 본다.
[Trait("Spec", "judgment")]
public class ReviewItemTests
{
    // 11.3 표의 항목 상태는 metrics.md 4.4·12.2의 items[].status다. 표가 덮는 상태 = match를 뺀 대조 항목 상태 전부, 한 번씩.
    [Fact]
    public void Review_item_statuses_are_all_non_match_item_statuses()
    {
        var statuses = JudgmentSpec.ReviewItems().SelectMany(r => r.Statuses).ToArray();

        Assert.Equal(statuses.Distinct().Count(), statuses.Length);
        Assert.Equal(
            new[] { CompareEngine.ItemMismatch, CompareEngine.ItemMissing, CompareEngine.ItemExtra, CompareEngine.ItemDuplicate }.Order(StringComparer.Ordinal),
            statuses.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(CompareEngine.ItemMatch, statuses);
        Assert.Equal(["불일치", "추가 행", "중복 행"], JudgmentSpec.ReviewItems().Select(r => r.Item));
    }

    // 11.3 둘째 글머리의 예 "불일치 3건, 추가 행 1건"은 표 이름으로 센 {breakdown}이다. 0인 이름은 쓰지 않는다.
    [Fact]
    public void Breakdown_example_matches_rule()
    {
        Assert.Equal("불일치 3건, 추가 행 1건", JudgmentSpec.Breakdown(3, 1, 0));
        Assert.Equal(string.Empty, JudgmentSpec.Breakdown(0, 0, 0));
        Assert.Contains("`불일치 3건, 추가 행 1건`", File.ReadAllText(RepoRoot.Combine(JudgmentSpec.Document)), StringComparison.Ordinal);
    }

    // 허용오차 안 차이만 있는 일치는 검토 항목이 아니다. 검토 항목이 없으면 UNEXPLAINED none, 사람 입력 없이 통과다.
    [Fact]
    public void Match_with_differences_within_tolerance_is_not_a_review_item()
    {
        var definition = CompareInput.Case(
            SyntheticInput.OneStepCase +
            "inputs: {reference: reference/*.csv, candidate: candidate/*.csv, key: id}\ncompare:\n  - {field: v, tolerance: 1}\nthresholds:\n  time_must_drop: false\n");
        var metrics = CompareInput.Measured(CompareInput.Run(definition, "id,v\nk1,10\n", "id,v\nk1,11\n"));

        var judged = JudgmentEngine.Judge(definition, metrics);

        Assert.Equal(1, metrics.Counts!.Within);
        Assert.Equal(JudgmentInput.NoneResult, JudgmentInput.CheckOf(judged, JudgmentSpec.Unexplained).Result);
        Assert.Equal((JudgmentSpec.Pass, JudgmentSpec.PassRule), (judged.Verdict, judged.Rule));
        Assert.DoesNotContain("review_items", JudgmentInput.TodoKeys(judged));
    }

    // 11.9 끝: compare-duplicate(metrics.md 12.5 블록)를 대조·판정하면 judgment.json이 11.9 블록·SHA-256과 바이트까지 같다.
    // 검토 항목 5건 = 불일치 2(값 불일치 r2, 누락 r4) + 추가 행 1(r5) + 중복 행 2(r2, r5). 사람 입력 없음 → 순서 7 조건부.
    [Fact]
    public void Compare_duplicate_judgment_matches_spec_block()
    {
        var definition = CompareDuplicate.Definition();
        var judged = JudgmentEngine.Judge(definition, CompareInput.Measured(CompareDuplicate.Run()));

        var bytes = JudgmentJson.Render(judged);

        var block = SpecDocument.BlockAfter(JudgmentSpec.Document, JudgmentSpec.CompareDuplicateLinePrefix);
        var hash = SpecDocument.CodeSpans(SpecDocument.Line(JudgmentSpec.Document, JudgmentSpec.CompareDuplicateLinePrefix))[^1];
        Assert.Equal(block, CompareInput.Text(bytes));
        Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(block))));
        Assert.Equal((JudgmentSpec.Conditional, JudgmentSpec.Unexplained), (judged.Verdict, judged.Rule));
        Assert.Equal(["review_items"], JudgmentInput.TodoKeys(judged));
    }

    // compare-duplicate의 검토 항목 키(r2, r4, r5)는 사람 입력으로 받는다. r2 하나가 불일치·중복 두 항목을 덮는다. 일치 키 r1은 거절이다.
    [Fact]
    public void Compare_duplicate_review_keys_are_accepted_as_input()
    {
        var definition = CompareDuplicate.Definition();
        var metrics = CompareInput.Measured(CompareDuplicate.Run());

        var found = JudgmentEngine.Judge(definition, metrics, null, ["r2", "r5"]);
        var explained = JudgmentEngine.Judge(definition, metrics, null, []);

        Assert.Equal((JudgmentSpec.Hold, JudgmentSpec.Unexplained), (found.Verdict, found.Rule));
        Assert.Equal(
            JudgmentSpec.Fill(JudgmentSpec.FindVerdict(JudgmentSpec.Unexplained, JudgmentInput.Found).Template, new Dictionary<string, string> { ["unexplained"] = "2" }),
            found.Reason);
        Assert.Equal((JudgmentSpec.Pass, JudgmentSpec.PassRule), (explained.Verdict, explained.Rule));
        Assert.ThrowsAny<ArgumentException>(() => JudgmentEngine.Judge(definition, metrics, null, ["r1"]));
    }
}
