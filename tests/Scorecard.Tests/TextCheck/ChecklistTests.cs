using System.Text.Json;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Comparison;

namespace Scorecard.Tests.TextCheck;

// 스펙 4.2 체크리스트 대조. 맞추기 표(메모리 바이트), 체크리스트 장면 표(규칙 10), 4.5 체크리스트 코드 표,
// 열 찾기(규칙 1), 결과 쪽 중복·누락·추가(규칙 7·8). 카드 수용 기준 2.
[Trait("Spec", "text-check")]
public class ChecklistTests
{
    private const string Item = "x";

    public static TheoryData<string, string, string, string> MatchRows
    {
        get
        {
            var data = new TheoryData<string, string, string, string>();
            foreach (var c in TextCheckSpec.Table(TextCheckSpec.MatchHeader))
            {
                data.Add(TextCheckSpec.Cell(c[0]), c[1].Trim(), c[2].Trim(), TextCheckSpec.Cell(c[3]));
            }

            return data;
        }
    }

    public static TheoryData<string, string, string, string> SceneRows
    {
        get
        {
            var data = new TheoryData<string, string, string, string>();
            foreach (var c in TextCheckSpec.Table(TextCheckSpec.ChecklistSceneHeader))
            {
                data.Add(c[0], TextCheckSpec.Cell(c[1]), TextCheckSpec.Cell(c[2]), TextCheckSpec.Cell(c[3]));
            }

            return data;
        }
    }

    // 맞추기 표. 항목 하나(x)와 compare 필드 n(같은 값)을 둔다. compare 필드가 있어 기준 칸이 비어도 no-check가 아니다(규칙 10).
    // found → 일치(items 없음), absent → 값 불일치(missing_items = [x]), skipped → 셈에 들지 않음(required 0).
    [Theory]
    [MemberData(nameof(MatchRows))]
    public void Match_table_row_holds(string item, string referenceCell, string text, string result)
    {
        var column = item == "column";
        var phrases = column ? [] : item["any: [".Length..^1].Split(", ");
        var yaml = CompareInput.InputsCase + "compare:\n  - {field: n, tolerance: 0}\n" +
            "checklist:\n  text: reply\n  items:\n" +
            (column ? $"    - {{item: {Item}, column: c}}\n" : $"    - {{item: {Item}, any: [{string.Join(", ", phrases.Select(Yaml))}]}}\n");
        var reference = column ? $"id,n,c\nk1,1,{CompareInput.Quote(Value(referenceCell))}\n" : "id,n\nk1,1\n";
        var candidate = $"id,n,reply\nk1,1,{CompareInput.Quote(Value(text))}\n";

        var metrics = Render(yaml, reference, candidate);
        var entry = Assert.Single(metrics.GetProperty("compare").GetProperty("checklist").GetProperty("entries").EnumerateArray());
        var items = metrics.GetProperty("compare").GetProperty("items");
        var (required, found, absent) = (entry.GetProperty("required").GetInt32(), entry.GetProperty("found").GetInt32(), entry.GetProperty("absent").GetInt32());

        Assert.Equal(Item, entry.GetProperty("item").GetString());
        Assert.Equal(required, found + absent);
        switch (result)
        {
            case "found":
                Assert.Equal((1, 1, 0), (required, found, absent));
                Assert.Equal(0, items.GetArrayLength());
                break;
            case "absent":
                Assert.Equal((1, 0, 1), (required, found, absent));
                var only = Assert.Single(items.EnumerateArray());
                Assert.Equal(CompareEngine.ItemMismatch, only.GetProperty("status").GetString());
                Assert.Equal([Item], TextCheckInput.Strings(only.GetProperty("missing_items")));
                Assert.Equal(0, only.GetProperty("differences").GetArrayLength());
                break;
            case "skipped":
                Assert.Equal((0, 0, 0), (required, found, absent));
                Assert.Equal(0, items.GetArrayLength());
                break;
            default:
                Assert.Fail($"알 수 없는 결과: {result}");
                break;
        }
    }

    // 맞추기 표는 결과 셋을 모두 쓴다(빈 통과 방지).
    [Fact]
    public void Match_table_uses_all_results()
    {
        var results = TextCheckSpec.Table(TextCheckSpec.MatchHeader).Select(c => TextCheckSpec.Cell(c[3])).Distinct().Order(StringComparer.Ordinal);
        Assert.Equal(["absent", "found", "skipped"], results);
    }

    // 체크리스트 장면 표. 받음이면 accuracy_pct 글이 표와 같다. 오류면 그 코드의 오류만 있고 출력이 없다.
    [Theory]
    [MemberData(nameof(SceneRows))]
    public void Checklist_scene_matches_table(string change, string result, string code, string accuracy)
    {
        var run = TextCheckInput.Run(TextCheckSpec.Example, change);
        if (result == TextCheckSpec.Accepted)
        {
            TextCheckInput.Valid(run);
            Assert.Equal(accuracy, run.MetricsJson.GetProperty("compare").GetProperty("accuracy_pct").GetRawText());
        }
        else
        {
            Assert.Equal(TextCheckSpec.Rejected, result);
            Assert.False(run.IsValid);
            Assert.NotEmpty(run.Errors);
            Assert.All(run.Codes, c => Assert.Equal(code, c));
        }
    }

    // 4.5 체크리스트 코드 표와 4.2 장면 글머리: 셋째 행은 d12 하나(reference/replies.csv:13), 넷째 행은 모든 기준 행(첫 오류 :2).
    // 위치는 기준 파일:그 행 줄(열 0), 경로는 빈 문자열. 메시지는 표 틀 그대로.
    [Fact]
    public void No_check_errors_follow_code_table()
    {
        var row = Assert.Single(TextCheckSpec.Table(TextCheckSpec.ChecklistCodeHeader));
        Assert.Equal("no-check", TextCheckSpec.Cell(row[0]));
        var template = Assert.Single(SpecDocument.CodeSpans(row[3]));
        var scenes = TextCheckSpec.Table(TextCheckSpec.ChecklistSceneHeader).Where(c => TextCheckSpec.Cell(c[2]) == "no-check").Select(c => c[0]).ToArray();
        Assert.Equal(2, scenes.Length);

        var one = TextCheckInput.Run(TextCheckSpec.Example, scenes[0]).Errors;
        var all = TextCheckInput.Run(TextCheckSpec.Example, scenes[1]).Errors;
        Assert.All(one.Concat(all), e => Assert.Equal("no-check", e.Code));

        Assert.Equal([(13, "d12")], one.Select(e => (e.Line, Key(e))));
        Assert.Equal(Enumerable.Range(2, 12).Select(l => (l, $"d{l - 1:D2}")), all.Select(e => (e.Line, Key(e))));
        Assert.All(one.Concat(all), e =>
        {
            Assert.Equal(("reference/replies.csv", 0, string.Empty), (e.Source, e.Column, e.Path));
            Assert.Matches(TextCheckSpec.MessagePattern(template), e.Message);
            Assert.Equal(template.Replace("<키>", Key(e), StringComparison.Ordinal), e.Message);
        });
    }

    // 규칙 1: 기준 파일의 column 항목 열, 결과 파일의 checklist.text 열이 없으면 column(metrics.md 4.7 문구).
    // 찾는 순서는 키 → compare → group → 체크리스트 열이다(같은 줄·열이면 찾은 순서).
    [Fact]
    public void Missing_checklist_columns_are_column_errors_in_lookup_order()
    {
        var reference = TextCheckInput.Run(TextCheckSpec.Example, "`reference/replies.csv`: `reply_id,topic,ticket,action\\n` → `reply_id,topix,tickex,action\\n`");
        var candidate = TextCheckInput.Run(TextCheckSpec.Example, "`candidate/replies.csv`: `reply_id,reply\\n` → `reply_id,replies\\n`");
        var doubled = TextCheckInput.Run(TextCheckSpec.Example, "`candidate/replies.csv`: `reply_id,reply\\n` → `reply_id,reply,reply\\n`");

        Assert.Equal(
            ["reference/replies.csv:1: column topic: 머리 행에 열이 없다", "reference/replies.csv:1: column ticket: 머리 행에 열이 없다"],
            reference.Errors.Select(e => e.ToString()));
        Assert.Equal(["candidate/replies.csv:1: column reply: 머리 행에 열이 없다"], candidate.Errors.Select(e => e.ToString()));
        Assert.Equal(["candidate/replies.csv:1:16: duplicate-column reply: 머리 행에 같은 열이 둘 이상 있다 (처음: 2번째 칸)"], doubled.Errors.Select(e => e.ToString()));
    }

    // 규칙 7·8과 3.1: 결과 쪽 중복(d01 둘째 행), 추가(d99), 누락(d05).
    // 기준 항목 d01은 값 불일치이고 대표 행(완전한 답변)의 빠진 항목 []을, 중복 항목은 둘째 행의 빠진 항목 셋을 적는다.
    // 누락·추가는 []. 항목 셈은 기준에 키가 있는 결과 행마다(11 + 중복 1 = 12)다.
    [Fact]
    public void Duplicate_missing_and_extra_rows_follow_rules_7_and_8()
    {
        using var folder = TextCheckInput.Folder(TextCheckSpec.Example);
        var candidate = TextCheckInput.Read(folder.Root, "candidate/replies.csv");
        var lines = candidate.Split('\n').Where(l => l.Length > 0 && !l.StartsWith("d05,", StringComparison.Ordinal));
        TextCheckInput.Write(folder.Root, "candidate/replies.csv", string.Concat(lines.Select(l => l + "\n")) + "d01,안녕하세요.\nd99,안녕하세요.\n");

        var compare = TextCheckInput.Valid(TextCheckInput.Run(folder.Root)).MetricsJson.GetProperty("compare");
        var items = compare.GetProperty("items").EnumerateArray()
            .Select(i => (i.GetProperty("key").GetString(), i.GetProperty("status").GetString(), string.Join(",", TextCheckInput.Strings(i.GetProperty("missing_items")))))
            .ToArray();
        var entries = compare.GetProperty("checklist").GetProperty("entries").EnumerateArray()
            .Select(e => (e.GetProperty("item").GetString(), e.GetProperty("required").GetInt32(), e.GetProperty("found").GetInt32(), e.GetProperty("absent").GetInt32()))
            .ToArray();

        Assert.Equal(
            [
                ("d01", "mismatch", string.Empty),
                ("d03", "mismatch", "action"),
                ("d05", "missing", string.Empty),
                ("d12", "mismatch", "ticket"),
                ("d01", "duplicate", "ticket,action,closing"),
                ("d99", "extra", string.Empty),
            ],
            items);
        Assert.Equal([("ticket", 12, 10, 2), ("action", 12, 10, 2), ("closing", 12, 11, 1)], entries);
        Assert.Equal("66.7", compare.GetProperty("accuracy_pct").GetRawText());
    }

    // 3.1 entries 모양: column 항목은 phrases [], any 항목은 column null과 쓴 문구 그대로. checklist.text는 문장 열 이름.
    [Fact]
    public void Checklist_entries_echo_the_definition()
    {
        var checklist = TextCheckInput.Valid(TextCheckInput.Run(TextCheckSpec.Example, TextCheckSpec.NoChange)).MetricsJson
            .GetProperty("compare").GetProperty("checklist");
        var entries = checklist.GetProperty("entries").EnumerateArray().ToArray();

        Assert.Equal("reply", checklist.GetProperty("text").GetString());
        Assert.Equal(["ticket", "action", "closing"], entries.Select(e => e.GetProperty("item").GetString()));
        Assert.Equal(["ticket", "action", null], entries.Select(e => e.GetProperty("column").ValueKind == JsonValueKind.Null ? null : e.GetProperty("column").GetString()));
        Assert.Equal([0, 0, 2], entries.Select(e => e.GetProperty("phrases").GetArrayLength()));
        Assert.Equal(["감사합니다", "고맙습니다"], TextCheckInput.Strings(entries[2].GetProperty("phrases")));
    }

    private static string Key(CaseError error) => error.Message.Split(": ")[1].Split(' ')[0];

    // 표 칸 → 글. `코드`를 떼고 \n·\t를 푼다. "(빈 칸)"은 빈 글.
    private static string Value(string cell) => TextCheckSpec.Unescape(TextCheckSpec.Cell(cell));

    private static string Yaml(string text) => "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static JsonElement Render(string yaml, string reference, string candidate)
    {
        var definition = CompareInput.Case(yaml);
        var result = CompareInput.Run(definition, reference, candidate);
        return TextCheckInput.Json(MetricsJson.Render(CompareInput.Measured(result)));
    }
}
