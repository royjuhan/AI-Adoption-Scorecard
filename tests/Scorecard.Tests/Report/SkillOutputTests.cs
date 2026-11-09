using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scorecard.Core.Report;
using Scorecard.Tests.Cli;

namespace Scorecard.Tests.Report;

// T-022(ADR 0016): 스킬 선택 항목이 스코어카드에 나타나는 모양. 확인하면 바뀔 수 있음 셈, 상태·결론,
// 담당자 확인 문장, 할 일 묶음, 가상 시나리오 줄, 화면에 코드를 보이지 않음(데이터 report.md 3·4·6절).
[Trait("Spec", "report")]
public partial class SkillOutputTests
{
    public const string TentativeTableHeader = "| 표본 | ② 단계 | 말한 것만 | 확인하면 바뀔 수 있음 | 제안 단계 | 제안 추정 |";
    public const string GateTextTableHeader = "| 확인 종류 | 화면 문장 |";
    public const string TodoGroupTableHeader = "| 묶음 | 화면 제목 | 키 |";

    private static readonly string[] CountKeys = ["steps", "software", "ai_assist", "ai_overuse"];
    private static readonly string[] Codes = ["①", "②", "③", "④", "사람 게이트", "SIGNOFF", "ELSE", "sha256", "schema_version"];

    public static TheoryData<string> Golden => ReportInput.GoldenNames;

    private static JsonDocument GoldenJson(string name) => JsonDocument.Parse(File.ReadAllBytes(ReportInput.GoldenPath(name, ScorecardJson.FileName)));

    private static string GoldenHtml(string name) => File.ReadAllText(ReportInput.GoldenPath(name, "scorecard.html"), Encoding.UTF8);

    // 데이터 4.1 표: 표본마다 ② 단계 수 = 말한 것만 + 확인하면 바뀔 수 있음, 제안 단계와 제안 추정.
    [Fact]
    public void Tentative_table_matches_golden()
    {
        var rows = ReportInput.Table(TentativeTableHeader);
        Assert.Equal(Classification.ClassificationInput.AllExamples.Length + ReportInput.Samples.Length, rows.Count);
        foreach (var row in rows)
        {
            using var doc = GoldenJson(CliInput.Cell(row[0]));
            var summary = doc.RootElement.GetProperty("summary");
            var proposal = doc.RootElement.GetProperty("proposal");
            Assert.Equal(
                (CliInput.Cell(row[1]), CliInput.Cell(row[2]), CliInput.Cell(row[3]), CliInput.Cell(row[4]), CliInput.Cell(row[5]) == "예"),
                (summary.GetProperty("software").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
                 summary.GetProperty("stated").GetProperty("software").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
                 summary.GetProperty("tentative").GetProperty("software").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
                 proposal.GetProperty("step").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
                 proposal.GetProperty("tentative").GetBoolean()));
        }
    }

    // 말한 것만 + 확인하면 바뀔 수 있음 = 전체(네 셈 모두). 추정 단계 수 = tentative인 단계 수.
    [Theory]
    [MemberData(nameof(Golden))]
    public void Stated_and_tentative_counts_add_up(string name)
    {
        using var doc = GoldenJson(name);
        var summary = doc.RootElement.GetProperty("summary");
        var all = new Dictionary<string, int>
        {
            ["steps"] = summary.GetProperty("steps").GetInt32(),
            ["software"] = summary.GetProperty("software").GetInt32(),
            ["ai_assist"] = summary.GetProperty("ai_assist").GetInt32(),
            ["ai_overuse"] = summary.GetProperty("ai_overuse").GetInt32(),
        };
        foreach (var key in CountKeys)
        {
            Assert.Equal(all[key], summary.GetProperty("stated").GetProperty(key).GetInt32() + summary.GetProperty("tentative").GetProperty(key).GetInt32());
        }

        var tentativeSteps = doc.RootElement.GetProperty("steps").EnumerateArray().Count(s => s.GetProperty("tentative").GetBoolean());
        Assert.Equal(tentativeSteps, summary.GetProperty("tentative").GetProperty("steps").GetInt32());
    }

    // 추정 답(inferred)이 있는 단계는 확인하면 바뀔 수 있다. 셈은 말한 것에서 빠진다.
    [Fact]
    public void Inferred_answer_moves_a_step_to_tentative()
    {
        using var work = CliWorkspace.Create("table-compare");
        var answers = Path.Combine(work.Case, "answers.yaml");
        var text = File.ReadAllText(answers).Replace(
            "existing_feature: no, human_signoff: no}\n  - name: 불규칙",
            "existing_feature: no, human_signoff: no}\n    inferred: [frequency]\n  - name: 불규칙",
            StringComparison.Ordinal);
        File.WriteAllText(answers, text);

        var (open, _) = ReportInput.Produce(work, "table-compare");

        using var doc = JsonDocument.Parse(open.Stdout);
        var summary = doc.RootElement.GetProperty("summary");
        Assert.True(doc.RootElement.GetProperty("steps")[0].GetProperty("tentative").GetBoolean());
        Assert.Equal(1, summary.GetProperty("tentative").GetProperty("steps").GetInt32());
        Assert.Equal(summary.GetProperty("software").GetInt32() - 1, summary.GetProperty("stated").GetProperty("software").GetInt32());
        Assert.Contains("가정: 처리 주기", doc.RootElement.GetProperty("steps")[0].GetProperty("assumptions")[0].GetString(), StringComparison.Ordinal);
    }

    // 채점 1회 뒤(T-022): 추정 답에 기댄 담당자 확인은 유지할 것에 세지 않고 아직 판단하지 않은 것에 센다(report.md 12.2).
    [Fact]
    public void Inferred_gate_is_undecided_not_kept()
    {
        using var work = ReportInput.Workspace(ReportInput.ActionsSample);
        var answers = Path.Combine(work.Case, "answers.yaml");
        File.WriteAllText(answers, File.ReadAllText(answers).Replace(
            "minutes_per_item: 15, failure_impact: external, existing_feature: no, human_signoff: no}\n",
            "minutes_per_item: 15, failure_impact: external, existing_feature: no, human_signoff: no}\n    inferred: [failure_impact]\n",
            StringComparison.Ordinal));

        var (open, _) = ReportInput.Produce(work, ReportInput.ActionsSample);

        Assert.Equal(0, open.Exit);
        using var doc = JsonDocument.Parse(open.Stdout);
        var gate = doc.RootElement.GetProperty("gates").EnumerateArray().Single(g => g.GetProperty("kind").GetString() == "human_gate");
        Assert.EndsWith("(AI 추정, 확인 필요)", gate.GetProperty("reason").GetString(), StringComparison.Ordinal);
        var conclusion = doc.RootElement.GetProperty("conclusion");
        Assert.Equal("4단계는 사람이 판단하는 일로 지금처럼 둔다. 담당자 확인 1곳은 그대로 둔다.", conclusion.GetProperty("keep").GetString());
        Assert.Contains("추정·기본값에 기댄 담당자 확인 1곳(확인하면 바뀔 수 있음).", conclusion.GetProperty("undecided").GetString(), StringComparison.Ordinal);
    }

    // 기본값에 기댄 담당자 확인도 같다. 둘 다 추정·기본값이면 유지할 것에 담당자 확인이 없다.
    [Fact]
    public void Defaulted_and_inferred_gates_are_not_kept()
    {
        using var work = ReportInput.Workspace(ReportInput.ActionsSample);
        var answers = Path.Combine(work.Case, "answers.yaml");
        File.WriteAllText(answers, File.ReadAllText(answers)
            .Replace("{input_kind: fixed_form, rule_expressible: partial, frequency: weekly, batch_size: 10, exception_rate: 25,", "{input_kind: fixed_form, frequency: weekly, batch_size: 10, exception_rate: 25,", StringComparison.Ordinal)
            .Replace(
                "minutes_per_item: 15, failure_impact: external, existing_feature: no, human_signoff: no}\n",
                "minutes_per_item: 15, failure_impact: external, existing_feature: no, human_signoff: no}\n    inferred: [failure_impact]\n",
                StringComparison.Ordinal));

        var (open, _) = ReportInput.Produce(work, ReportInput.ActionsSample);

        Assert.Equal(0, open.Exit);
        using var doc = JsonDocument.Parse(open.Stdout);
        var review = doc.RootElement.GetProperty("gates").EnumerateArray().Single(g => g.GetProperty("kind").GetString() == "exception_review");
        Assert.EndsWith("(기본값 사용, 확인 필요)", review.GetProperty("reason").GetString(), StringComparison.Ordinal);
        var conclusion = doc.RootElement.GetProperty("conclusion");
        Assert.Equal("4단계는 사람이 판단하는 일로 지금처럼 둔다.", conclusion.GetProperty("keep").GetString());
        Assert.Contains("추정·기본값에 기댄 담당자 확인 2곳(확인하면 바뀔 수 있음).", conclusion.GetProperty("undecided").GetString(), StringComparison.Ordinal);
    }

    // 채점 1회 뒤(T-022): AI 판단 단계는 말한 동작이 단계 담당을 받치면 추정 동작이 섞여도 말한 것만에 든다.
    // 받치는 말한 동작이 없으면 확인하면 바뀔 수 있음이다(report.md 12.3).
    [Fact]
    public void Ai_step_is_tentative_only_without_a_stated_action_for_its_assignee()
    {
        using var golden = GoldenJson(ReportInput.ActionsSample);
        var steps = golden.RootElement.GetProperty("steps");
        Assert.Contains(steps[1].GetProperty("actions").EnumerateArray(), a => a.GetProperty("source").GetString() == "inferred");
        Assert.False(steps[1].GetProperty("tentative").GetBoolean());

        using var work = ReportInput.Workspace(ReportInput.ActionsSample);
        var answers = Path.Combine(work.Case, "answers.yaml");
        File.WriteAllText(answers, File.ReadAllText(answers)
            .Replace("{action: 빈칸 찾기, who_now: human, proposed: ②, source: stated}", "{action: 빈칸 찾기, who_now: human, proposed: ②, source: inferred}", StringComparison.Ordinal)
            .Replace("{action: 단위가 허용 목록에 있는지 보기, who_now: human, proposed: ②, source: stated}", "{action: 단위가 허용 목록에 있는지 보기, who_now: human, proposed: ④, source: stated}", StringComparison.Ordinal));

        var (open, _) = ReportInput.Produce(work, ReportInput.ActionsSample);

        Assert.Equal(0, open.Exit);
        using var doc = JsonDocument.Parse(open.Stdout);
        Assert.True(doc.RootElement.GetProperty("steps")[1].GetProperty("tentative").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("steps")[2].GetProperty("tentative").GetBoolean());
    }

    // 답 파일을 못 쓰면(answers ≠ used) 추정 답을 모르니 stated가 아닌 동작이 있는 AI 단계는 확인하면 바뀔 수 있음이다.
    [Fact]
    public void Ai_step_with_unstated_action_is_tentative_when_answers_are_not_used()
    {
        using var work = ReportInput.Workspace(ReportInput.ActionsSample);
        var output = work.PathOf("o");
        var answers = Path.Combine(work.Case, "answers.yaml");
        Assert.Equal(0, CliInput.Run("start", work.Case, "--answers", answers, "--out", output, "--json").Exit);
        File.Delete(answers);

        var open = CliInput.Run("open", work.Case, "--out", output, "--json");

        Assert.Equal(0, open.Exit);
        using var doc = JsonDocument.Parse(open.Stdout);
        Assert.Equal("missing", doc.RootElement.GetProperty("answers").GetString());
        var steps = doc.RootElement.GetProperty("steps");
        Assert.Equal(
            [true, true, false, false],
            steps.EnumerateArray().Select(s => s.GetProperty("tentative").GetBoolean()));
    }

    // 상태 네 줄과 결론: 답 파일 메모가 있으면 AI 초안·가상 자료·대화 확인, 없으면 규칙 트리·모름.
    [Fact]
    public void Status_and_conclusion_follow_case_notes()
    {
        using var actions = GoldenJson(ReportInput.ActionsSample);
        var status = actions.RootElement.GetProperty("status");
        Assert.Equal(("virtual", "ai_draft", "conversation", 3, 4), (status.GetProperty("material").GetString(), status.GetProperty("classification").GetString(), status.GetProperty("confirmed_in").GetString(), status.GetProperty("confirmed").GetInt32(), status.GetProperty("steps").GetInt32()));
        Assert.Equal("업무 구조 검토 완료. 지금 제출할 것 없음.", actions.RootElement.GetProperty("conclusion").GetProperty("end_state").GetString());
        Assert.Equal("주 10건", actions.RootElement.GetProperty("stated_numbers")[1].GetString());
        Assert.Equal(5, actions.RootElement.GetProperty("self_check").GetArrayLength());
        Assert.Equal(
            [(1, 2), (3, 15)],
            actions.RootElement.GetProperty("summary").GetProperty("minutes").EnumerateArray().Select(m => (m.GetProperty("step").GetInt32(), m.GetProperty("minutes").GetInt32())));

        using var example = GoldenJson("table-compare");
        var plain = example.RootElement.GetProperty("status");
        Assert.Equal(("unknown", "rule_tree", JsonValueKind.Null), (plain.GetProperty("material").GetString(), plain.GetProperty("classification").GetString(), plain.GetProperty("confirmed_in").ValueKind));
        Assert.Equal(JsonValueKind.Null, example.RootElement.GetProperty("scenario").ValueKind);
        Assert.Equal(JsonValueKind.Null, example.RootElement.GetProperty("task_summary").ValueKind);
        Assert.Equal(0, example.RootElement.GetProperty("self_check").GetArrayLength());
    }

    // 확인 파일 confirmed_in: conversation은 대화에서 확인한 기록이다. HTML 단계 표가 "대화에서 확인됨"을 보인다.
    [Fact]
    public void Confirmed_in_conversation_is_recorded_and_shown()
    {
        var (file, errors) = ReviewParser.ParseFile(Encoding.UTF8.GetBytes("confirmed_in: conversation\nsteps:\n  - {number: 1, answer: keep}\n"), "review.yaml", [1, 2]);

        Assert.Empty(errors);
        Assert.Equal(ReviewParser.ConfirmedInConversation, file!.ConfirmedIn);
        Assert.Single(file.Entries);
        Assert.Contains("대화에서 확인됨", GoldenHtml(ReportInput.ActionsSample), StringComparison.Ordinal);
        Assert.DoesNotContain("대화에서 확인됨", GoldenHtml(ReportInput.AiSample), StringComparison.Ordinal);
    }

    // 데이터 4.6 표: 담당자 확인 화면 문장. human_gate는 gate_note가 있으면 그 글이다.
    [Theory]
    [MemberData(nameof(Golden))]
    public void Gate_text_is_table_text_or_gate_note(string name)
    {
        var texts = ReportInput.Table(GateTextTableHeader).ToDictionary(r => CliInput.Cell(r[0]), r => CliInput.Cell(r[1]), StringComparer.Ordinal);
        using var doc = GoldenJson(name);
        var steps = doc.RootElement.GetProperty("steps").EnumerateArray().ToArray();
        foreach (var gate in doc.RootElement.GetProperty("gates").EnumerateArray())
        {
            var kind = gate.GetProperty("kind").GetString()!;
            var step = steps.Single(s => s.GetProperty("number").GetInt32() == gate.GetProperty("step").GetInt32());
            var note = step.GetProperty("gate_note");
            var expected = kind == "human_gate" && note.ValueKind == JsonValueKind.String ? note.GetString() : texts[kind];
            Assert.Equal(expected, gate.GetProperty("text").GetString());
        }
    }

    [Fact]
    public void Gate_note_replaces_the_human_gate_text()
    {
        using var doc = GoldenJson(ReportInput.ActionsSample);
        var gate = doc.RootElement.GetProperty("gates").EnumerateArray().Single(g => g.GetProperty("kind").GetString() == "human_gate");
        Assert.Equal("보내기 전에 담당자가 초안을 읽고 고친다. 4단계 원본 대조 때 함께 본다.", gate.GetProperty("text").GetString());
    }

    // 데이터 6절 할 일 묶음 표: 묶음 순서 now → improve → verify → implementer, 표에 적은 키는 그 묶음이다.
    [Theory]
    [MemberData(nameof(Golden))]
    public void Todo_groups_follow_the_table(string name)
    {
        var rows = ReportInput.Table(TodoGroupTableHeader);
        var order = rows.Select(r => CliInput.Cell(r[0])).ToList();
        var keyGroup = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            foreach (Match m in CodeSpan().Matches(row[2]))
            {
                keyGroup[m.Groups[1].Value] = CliInput.Cell(row[0]);
            }
        }

        using var doc = GoldenJson(name);
        var todo = doc.RootElement.GetProperty("todo").EnumerateArray().ToArray();
        Assert.NotEmpty(todo);
        var ranks = todo.Select(t => order.IndexOf(t.GetProperty("group").GetString()!)).ToArray();
        Assert.All(ranks, r => Assert.True(r >= 0));
        Assert.Equal(ranks.Order(), ranks);
        foreach (var item in todo)
        {
            var key = item.GetProperty("key").GetString()!;
            var group = item.GetProperty("group").GetString()!;
            Assert.Equal(keyGroup.TryGetValue(key, out var g) ? g : "verify", group);
        }
    }

    // 모든 단계를 확인하면 지금 할 일이 없다: 묶음 now가 비고 HTML은 "없음."을 보인다(지시문 5.2).
    [Fact]
    public void All_confirmed_leaves_nothing_to_do_now()
    {
        using var work = ReportInput.Workspace(ReportInput.ActionsSample);
        var review = work.PathOf("all.yaml");
        File.WriteAllText(review, "confirmed_in: conversation\nsteps:\n  - {number: 1, answer: keep}\n  - {number: 2, answer: keep}\n  - {number: 3, answer: keep}\n  - {number: 4, answer: keep}\n");

        var (open, output) = ReportInput.Produce(work, ReportInput.ActionsSample, "--review", review);

        Assert.Equal(0, open.Exit);
        using var doc = JsonDocument.Parse(open.Stdout);
        Assert.DoesNotContain(doc.RootElement.GetProperty("todo").EnumerateArray(), t => t.GetProperty("group").GetString() == "now");
        Assert.Equal(4, doc.RootElement.GetProperty("status").GetProperty("confirmed").GetInt32());
        var html = File.ReadAllText(Path.Combine(output, "scorecard.html"));
        Assert.Contains("지금 할 일</div>\n<p class=\"note\">없음.</p>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">확인 전<", html, StringComparison.Ordinal);
    }

    // 가상 시나리오 줄은 scenario: virtual일 때만. real로 바꾸면 줄이 없다.
    [Theory]
    [MemberData(nameof(Golden))]
    public void Virtual_banner_only_for_virtual_scenario(string name)
    {
        using var doc = GoldenJson(name);
        var virtualCase = doc.RootElement.GetProperty("scenario").ValueKind == JsonValueKind.String && doc.RootElement.GetProperty("scenario").GetString() == "virtual";
        Assert.Equal(virtualCase, GoldenHtml(name).Contains("가상 시나리오 기반", StringComparison.Ordinal));
    }

    [Fact]
    public void Real_scenario_has_no_virtual_banner()
    {
        using var work = ReportInput.Workspace(ReportInput.ActionsSample);
        var answers = Path.Combine(work.Case, "answers.yaml");
        File.WriteAllText(answers, File.ReadAllText(answers).Replace("scenario: virtual", "scenario: real", StringComparison.Ordinal));

        var (open, output) = ReportInput.Produce(work, ReportInput.ActionsSample);

        Assert.Equal(0, open.Exit);
        using var doc = JsonDocument.Parse(open.Stdout);
        Assert.Equal("real", doc.RootElement.GetProperty("status").GetProperty("material").GetString());
        Assert.DoesNotContain("가상 시나리오 기반", File.ReadAllText(Path.Combine(output, "scorecard.html")), StringComparison.Ordinal);
    }

    // 화면에 보이는 글(<style>·<details> 밖)에 담당 코드·근거 키·"사람 게이트"·해시·schema가 없다.
    [Theory]
    [MemberData(nameof(Golden))]
    public void Visible_html_has_no_codes(string name)
    {
        var visible = Visible(GoldenHtml(name));
        foreach (var code in Codes)
        {
            Assert.DoesNotContain(code, visible, StringComparison.Ordinal);
        }
    }

    // 입력된 시간은 절감 칸에 그대로 보인다(사용자 숫자 그대로).
    [Fact]
    public void Entered_minutes_are_shown()
    {
        Assert.Contains("입력된 시간: 1단계 건당 2분 · 3단계 건당 15분.", GoldenHtml(ReportInput.ActionsSample), StringComparison.Ordinal);
        Assert.DoesNotContain("입력된 시간:", GoldenHtml("table-compare"), StringComparison.Ordinal);
    }

    // 비교 자료가 없으면 문턱을 보이지 않고 "이번 실행에는 비교 자료를 넣지 않았다."를 보인다.
    [Fact]
    public void Not_checked_hides_thresholds()
    {
        var html = Visible(GoldenHtml(ReportInput.ActionsSample));
        Assert.Contains("이번 실행에는 비교 자료를 넣지 않았다.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("문턱", html, StringComparison.Ordinal);
    }

    private static string Visible(string html)
    {
        var text = StyleBlock().Replace(html, string.Empty);
        while (DetailsBlock().IsMatch(text))
        {
            text = DetailsBlock().Replace(text, string.Empty);
        }

        return Tag().Replace(text, " ");
    }

    [GeneratedRegex("<style>.*?</style>", RegexOptions.Singleline)]
    private static partial Regex StyleBlock();

    // 안쪽 details부터 지운다(중첩 대비).
    [GeneratedRegex("<details[^>]*>(?:(?!<details).)*?</details>", RegexOptions.Singleline)]
    private static partial Regex DetailsBlock();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex("`([a-z_]+)`")]
    private static partial Regex CodeSpan();
}
