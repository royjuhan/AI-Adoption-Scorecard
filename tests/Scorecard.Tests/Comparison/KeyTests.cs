using Scorecard.Core.Comparison;

namespace Scorecard.Tests.Comparison;

// 스펙 4.3 키(승인 질문 5). 카드 수용 기준 2(키 중복, 빈 키).
// 키는 정규화(앞뒤 공백) 뒤 서수 비교. 대소문자·NFC/NFD·숫자 모양은 다른 키다.
// 빈 키는 empty-key(기준·결과 모두). 기준 쪽 중복은 둘째부터 duplicate-key(처음 위치를 메시지에).
// T-012(12.2): 결과 쪽 중복은 오류가 아니라 항목 duplicate다(DuplicateTests).
[Trait("Spec", "metrics")]
public class KeyTests
{
    private const string MissingAndExtra = "ref=1 cand=1 match=0 mismatch=0 missing=1 extra=1 within=0 acc=0.0 miss=100.0";

    private static readonly Scorecard.Core.Cases.CaseDefinition Definition = CompareInput.Case(CompareInput.TextCase);

    // 중복 4종: 같은 파일, 결과 쪽, 공백을 떼고 같아진 키, 셋째 중복도 처음 위치를 가리킨다. 파일 사이는 MultiFileTests.
    // T-012(metrics.md 12.2): 결과 쪽 둘째 행은 오류가 아니라 항목 duplicate다.
    // T-013(4.3·13.2): 결과 쪽에 두 번 나온 k1은 두 행이 모두 기준과 같아도 값 불일치다.
    [Fact]
    public void Duplicate_keys_are_reported_from_the_second()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,v\nk1,a\nk2,b\nk1,c\n", "id,v\nk1,a\n"),
            "reference/a.csv:4:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)");
        var candidateSide = CompareInput.Measured(CompareInput.Run(Definition, "id,v\nk1,a\n", "id,v\nk1,a\nk1,a\n"));
        Assert.Equal(
            ["k1 mismatch reference/a.csv:2 candidate/a.csv:2", "k1 duplicate reference/a.csv:2 candidate/a.csv:3"],
            candidateSide.Items.Select(i => $"{i.Key} {i.Status} {i.Reference?.File}:{i.Reference?.Line} {i.Candidate?.File}:{i.Candidate?.Line}"));
        Assert.Equal("ref=1 cand=2 match=0 mismatch=1 missing=0 extra=0 within=0 acc=0.0 miss=0.0", CompareInput.Counts(candidateSide));
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,v\nk1,a\n k1 ,b\n", "id,v\nk1,a\n"),
            "reference/a.csv:3:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)");
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,v\nk1,a\nk1,b\nk1,c\n", "id,v\nk1,a\n"),
            "reference/a.csv:3:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)",
            "reference/a.csv:4:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)");
    }

    // 키 칸 위치는 키 열의 칸이다(첫 열이 아니어도).
    [Fact]
    public void Duplicate_key_position_is_the_key_cell()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "v,id\na,k1\nb,k1\n", "id,v\nk1,a\n"),
            "reference/a.csv:3:3: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)");
    }

    // 빈 키 3종: 빈 칸, 공백만, 결과 쪽.
    [Fact]
    public void Empty_keys_are_reported()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,v\nk1,a\n,b\n", "id,v\nk1,a\n"),
            "reference/a.csv:3:1: empty-key id: 키가 비어 있다");
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,v\nk1,a\n  ,b\n", "id,v\nk1,a\n"),
            "reference/a.csv:3:1: empty-key id: 키가 비어 있다");
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,v\nk1,a\n", "id,v\nk1,a\n,b\n"),
            "candidate/a.csv:3:1: empty-key id: 키가 비어 있다");
    }

    // 앞뒤 공백을 떼고 맞춘다. metrics.json의 키도 정규화 뒤 값이다.
    [Fact]
    public void Key_spaces_are_trimmed_before_matching()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(Definition, "id,v\nk1,a\n", "id,v\n\" k1\t\",b\n"));

        Assert.Equal("ref=1 cand=1 match=0 mismatch=1 missing=0 extra=0 within=0 acc=0.0 miss=0.0", CompareInput.Counts(metrics));
        Assert.Equal("k1", Assert.Single(metrics.Items).Key);
    }

    // 대소문자, NFC·NFD, 숫자 모양(1과 1.0)이 다르면 다른 키다. 누락 1 + 추가 1.
    [Theory]
    [InlineData("log01", "LOG01")]
    [InlineData("é", "é")]
    [InlineData("가", "가")]
    [InlineData("1", "1.0")]
    [InlineData("I", "ı")]
    public void Different_key_text_is_a_different_key(string reference, string candidate)
    {
        var metrics = CompareInput.Measured(CompareInput.Run(Definition, $"id,v\n{reference},a\n", $"id,v\n{candidate},a\n"));

        Assert.Equal(MissingAndExtra, CompareInput.Counts(metrics));
        Assert.Equal(
            [(reference, CompareEngine.ItemMissing), (candidate, CompareEngine.ItemExtra)],
            metrics.Items.Select(i => (i.Key, i.Status)));
    }

    // 오류 행도 키는 등록한다(6.4). 숫자 오류가 있는 행의 키가 뒤에 다시 나오면 duplicate-key도 난다.
    [Fact]
    public void Key_of_row_with_number_error_is_still_registered()
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);

        CompareInput.AssertErrors(
            CompareInput.Run(definition, "id,n,t\nk1,x,a\nk1,1,a\n", CompareInput.GoodCsv),
            "reference/a.csv:2:4: number n: 숫자가 아니다: x",
            "reference/a.csv:3:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)");
    }

    // 메시지 안의 키 값: 제어 문자는 \uXXXX, 64자를 넘으면 자르고 "…"(4.7).
    [Fact]
    public void Key_in_message_is_escaped_and_truncated()
    {
        var longKey = new string('k', 70);

        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,v\n\"a\u0001b\",x\n\"a\u0001b\",y\n", "id,v\nk1,a\n"),
            "reference/a.csv:3:1: duplicate-key id: 키가 중복된다: a\\u0001b (처음: reference/a.csv:2)");
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, $"id,v\n{longKey},x\n{longKey},y\n", "id,v\nk1,a\n"),
            $"reference/a.csv:3:1: duplicate-key id: 키가 중복된다: {new string('k', 64)}… (처음: reference/a.csv:2)");
    }
}
