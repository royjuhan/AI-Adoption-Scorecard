using Scorecard.Core.Cases;

namespace Scorecard.Tests.TextCheck;

// 스펙 4.1 파서 장면 표. 바탕은 7.3 case.yaml 블록이다. "바꾼 곳"의 옛 글은 바탕에 꼭 한 번 있다.
// 받음이면 오류가 없고, 오류면 그 코드·메시지의 오류가 있다. 카드 수용 기준 1.
[Trait("Spec", "text-check")]
public class ParserSceneTests
{
    public static TheoryData<string, string, string, string> Rows
    {
        get
        {
            var data = new TheoryData<string, string, string, string>();
            foreach (var c in TextCheckSpec.Table(TextCheckSpec.ParserSceneHeader))
            {
                data.Add(c[0], TextCheckSpec.Cell(c[1]), TextCheckSpec.Cell(c[2]), TextCheckSpec.Cell(c[3]));
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void Parser_scene_matches_table(string change, string result, string code, string message)
    {
        var text = TextCheckInput.Text(TextCheckSpec.FileBlock(CaseParser.FileName));
        if (change.Trim() != TextCheckSpec.NoChange)
        {
            var spans = Scorecard.Tests.CaseSchema.SpecDocument.CodeSpans(change);
            Assert.Equal(2, spans.Length);
            var (old, @new) = (TextCheckSpec.Unescape(spans[0]), TextCheckSpec.Unescape(spans[1]));
            Assert.Equal(1, Count(text, old));
            text = text.Replace(old, @new, StringComparison.Ordinal);
        }

        var parsed = CaseParser.Parse(TextCheckInput.Bytes(text), CaseParser.FileName);
        if (result == TextCheckSpec.Accepted)
        {
            Assert.True(parsed.IsValid, "받아야 한다:\n" + string.Join("\n", parsed.Errors.Select(e => e.ToString())));
            Assert.NotNull(parsed.Case);
        }
        else
        {
            Assert.Equal(TextCheckSpec.Rejected, result);
            Assert.Null(parsed.Case);
            Assert.Contains(parsed.Errors, e => e.Code == code && e.Message == message);
            Assert.All(parsed.Errors, e => Assert.Equal(CaseParser.FileName, e.Source));
        }
    }

    // 표의 오류 코드는 case-schema.md 4.9의 17개 안에 있다(4.1 "새 코드가 없다").
    [Fact]
    public void Parser_scene_codes_are_existing_case_codes()
    {
        var codes = Scorecard.Tests.CaseSchema.SpecDocument.Table(Scorecard.Tests.CaseSchema.SpecDocument.CaseSchemaPath, Scorecard.Tests.CaseSchema.SpecDocument.ErrorCodeTableHeader)
            .Select(c => TextCheckSpec.Cell(c[0]))
            .ToHashSet(StringComparer.Ordinal);
        var rows = TextCheckSpec.Table(TextCheckSpec.ParserSceneHeader);

        Assert.Equal(17, codes.Count);
        Assert.Contains(rows, r => TextCheckSpec.Cell(r[1]) == TextCheckSpec.Accepted);
        Assert.All(rows.Where(r => TextCheckSpec.Cell(r[1]) == TextCheckSpec.Rejected), r => Assert.Contains(TextCheckSpec.Cell(r[2]), codes));
    }

    // 7.3 case.yaml은 받는다. 두 길의 자리(checklist·scores)와 thresholds.score_gap_max를 읽고, 단계 질문은 그대로 10개다(4.1 끝).
    [Fact]
    public void Example_case_yaml_is_accepted_with_both_paths()
    {
        var parsed = CaseParser.Parse(TextCheckSpec.FileBlock(CaseParser.FileName), CaseParser.FileName);

        Assert.True(parsed.IsValid, string.Join("\n", parsed.Errors.Select(e => e.ToString())));
        Assert.Equal(TextCheckSpec.Example, parsed.Case!.Name);
        Assert.Equal("topic", parsed.Case.Inputs?.Group);
        Assert.Empty(parsed.Case.Compare);
        Assert.Equal(3, parsed.Case.Steps.Count);
        Assert.Equal(10, Scorecard.Tests.Classification.ClassificationInput.Keys.Length);
    }

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
