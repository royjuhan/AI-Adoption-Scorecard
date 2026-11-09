using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Scorecard.Core.Comparison;
using Scorecard.Core.Report;

namespace Scorecard.Cli;

// 스코어카드 HTML 한 장. docs/spec/report.md 6절, 데이터 6절 구역 표.
// 결과 객체(ScorecardResult) 하나만 받아 표시한다. 숫자·판정·문장을 만들지 않는다(지시문 2절 7, ADR 0004 결정 6).
// 값 이름을 한국어 이름표로 바꾸는 것만 한다(표시). 막대 너비는 셈 그대로 flex 값으로 쓴다(나눗셈 없음).
// 외부 파일·스크립트·글꼴·그림이 없다. 시각·난수가 없다. 줄바꿈은 LF, UTF-8(BOM 없음).
// T-022(지시문 0016 3·4·5·6절): 기본 화면은 업무 말만 쓴다. 분류 코드·근거 키·파일·해시·schema는 <details> 펼쳐 보기 안에 둔다.
// 첫 화면은 상태 네 줄 → 결론 세 줄 → 네 칸 요약. 왼쪽 표와 오른쪽 카드에 같은 문장을 두지 않는다.
internal static class ScorecardHtml
{
    public const string FileName = "scorecard.html";

    // 한글·①~④는 그대로 두고 <>&"' 와 제어 문자만 바꾼다.
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    // 담당 이름표. 기본 화면은 업무 말(Name), 펼쳐 보기는 코드(Code)를 같이 쓴다.
    private static readonly Dictionary<string, (string Name, string Code, string Css)> Assignees = new(StringComparer.Ordinal)
    {
        ["①"] = ("기존 기능 유지", "① 기존 기능", "b1"),
        ["②"] = ("규칙 자동화 검토", "② 규칙 SW", "b2"),
        ["②M"] = ("고정 학습 모델 검토", "②M 학습 모델", "b2m"),
        ["③"] = ("AI 초안 + 담당자 확인", "③ AI 보조", "b3"),
        ["④"] = ("담당자 직접 처리", "④ 사람", "b4"),
    };

    private static readonly Dictionary<string, string> WhoNow = new(StringComparer.Ordinal)
    {
        ["human"] = "사람",
        ["ai_each_time"] = "매번 AI",
        ["program"] = "프로그램",
        ["mixed"] = "사람과 프로그램",
        ["unknown"] = "확인 안 됨",
    };

    private static readonly Dictionary<string, string> Sources = new(StringComparer.Ordinal)
    {
        ["stated"] = "사용자가 말함",
        ["inferred"] = "AI 추정",
        ["default"] = "기본값",
    };

    private static readonly Dictionary<string, string> GateKinds = new(StringComparer.Ordinal)
    {
        ["exception_review"] = "예외 건 담당자 검토",
        ["human_gate"] = "담당자 확인",
        ["signoff"] = "담당자 서명",
    };

    private static readonly Dictionary<string, string> Materials = new(StringComparer.Ordinal)
    {
        ["virtual"] = "가상 시나리오",
        ["real"] = "실제 업무 설명",
        ["unknown"] = "적지 않음",
    };

    private static readonly Dictionary<string, string> ClassificationKinds = new(StringComparer.Ordinal)
    {
        ["ai_draft"] = "설명 기반 초안",
        ["rule_tree"] = "규칙 트리 결과(AI 판단 없음)",
    };

    private static readonly Dictionary<string, (string Label, string Css)> Verdicts = new(StringComparer.Ordinal)
    {
        ["pass"] = ("통과", "v-good"),
        ["conditional"] = ("조건부", "v-warn"),
        ["hold"] = ("보류", "v-bad"),
        ["not_verified"] = ("미검증", "v-na"),
    };

    private static readonly Dictionary<string, (string Label, string Css)> ItemStatuses = new(StringComparer.Ordinal)
    {
        ["match"] = ("오차 안", "v-good"),
        ["mismatch"] = ("불일치", "v-bad"),
        ["missing"] = ("누락", "v-warn"),
        ["extra"] = ("추가", "v-na"),
        ["duplicate"] = ("중복", "v-na"),
    };

    private static readonly Dictionary<string, string> CheckResults = new(StringComparer.Ordinal)
    {
        ["met"] = "충족",
        ["not_met"] = "못 미침",
        ["none"] = "없음",
        ["not_reviewed"] = "사람 확인 전",
        ["explained"] = "설명됨",
        ["found"] = "몰림",
        ["off"] = "쓰지 않음",
        ["not_measured"] = "재지 않음",
        ["few"] = "적음",
        ["one_value"] = "값 하나",
        ["spread"] = "고름",
        ["revealed"] = "가림 깨짐",
    };

    private static readonly Dictionary<string, string> SelfCheckResults = new(StringComparer.Ordinal)
    {
        ["pass"] = "통과",
        ["fail"] = "실패",
    };

    private static readonly Dictionary<string, string> AnswersNotes = new(StringComparer.Ordinal)
    {
        ["missing"] = "answers.yaml이 케이스 폴더에 없어 추정 표시와 지금 하는 쪽을 보이지 않는다.",
        ["invalid"] = "answers.yaml을 읽을 수 없어 추정 표시와 지금 하는 쪽을 보이지 않는다.",
        ["mismatch"] = "answers.yaml의 단계가 classification.json과 달라 추정 표시와 지금 하는 쪽을 보이지 않는다.",
    };

    // 할 일 묶음 제목(지시문 5.2). implementer는 펼쳐 보기 안에 둔다.
    private static readonly (string Group, string Title)[] TodoGroups =
    [
        ("now", "지금 할 일"),
        ("improve", "개선을 진행하려면"),
        ("verify", "실제 효과까지 검증하려면(선택)"),
    ];

    private const string VirtualBanner = "가상 시나리오 기반. 실제 운영 수치·검증 결과 아님";
    private const string NoComparison = "이번 실행에는 비교 자료를 넣지 않았다.";
    private const string NoCompareFields = "비교할 열을 아직 정하지 않았다.";
    private const string NoSaving = "개선 전후 비교에 필요한 단계별 시간은 아직 없어 절감은 계산하지 않았다.";
    private const string DefineErrors = "실제 검증 때 무엇을 오류로 셀지 먼저 정한다.";

    public static byte[] Render(ScorecardResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var h = new StringBuilder();
        h.Append("<!doctype html>\n<html lang=\"ko\">\n<head>\n<meta charset=\"utf-8\">\n");
        h.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        h.Append("<title>AI 도입 스코어카드 · ").Append(E(r.Case)).Append("</title>\n");
        h.Append("<style>\n").Append(CssLf).Append("</style>\n</head>\n<body>\n<div class=\"wrap\">\n");

        Header(h, r);
        Summary(h, r);
        h.Append("<div class=\"grid\">\n");
        Steps(h, r);
        h.Append("<aside class=\"side\">\n");
        Proposal(h, r);
        Gates(h, r);
        Opinions(h, r);
        h.Append("</aside>\n</div>\n");
        Verification(h, r);
        Todo(h, r);
        Overview(h, r);
        h.Append("</div>\n</body>\n</html>\n");
        return CliJson.Utf8.GetBytes(h.ToString());
    }

    private static void Header(StringBuilder h, ScorecardResult r)
    {
        h.Append("<header>\n<h1>AI 도입 스코어카드 <span class=\"case\">").Append(E(r.Case)).Append("</span></h1>\n");
        if (r.Task is not null)
        {
            h.Append("<div class=\"task\">").Append(E(r.Task)).Append("</div>\n");
        }

        h.Append("</header>\n");
        if (r.Scenario == "virtual")
        {
            h.Append("<p class=\"banner\">").Append(VirtualBanner).Append("</p>\n");
        }

        if (AnswersNotes.TryGetValue(r.Answers, out var note))
        {
            h.Append("<p class=\"notice\">").Append(E(note)).Append("</p>\n");
        }
    }

    // 1구역: 상태 네 줄 → 결론 세 줄 → 네 칸.
    private static void Summary(StringBuilder h, ScorecardResult r)
    {
        var st = r.Status;
        var s = r.Summary;
        var v = r.Verification;
        h.Append("<section id=\"zone-1\">\n<ul class=\"status\">");
        Status(h, "자료 성격", Materials.GetValueOrDefault(st.Material, st.Material));
        Status(h, "업무 분류", ClassificationKinds.GetValueOrDefault(st.Classification, st.Classification));
        Status(h, "담당자 확인", I(st.Confirmed) + "/" + I(st.Steps) + " 확인됨" + (st.ConfirmedIn == "conversation" ? "(대화에서 확인됨 포함)" : string.Empty));
        Status(h, "실제 성능 시험", st.Test == "measured" ? "진행함" : "진행하지 않음");
        h.Append("</ul>\n");

        var c = r.Conclusion;
        h.Append("<div class=\"conclusion\">\n");
        Line(h, "유지할 것", c.Keep);
        Line(h, "먼저 확인할 것", c.FirstCheck);
        Line(h, "아직 판단하지 않은 것", c.Undecided);
        if (r.OpenQuestions.Count > 0)
        {
            h.Append("<div class=\"label\">확인하면 바뀔 수 있음</div>\n<ul class=\"questions\">");
            foreach (var q in r.OpenQuestions)
            {
                h.Append("<li>").Append(E(q)).Append("</li>");
            }

            h.Append("</ul>\n");
        }

        if (c.EndState is not null)
        {
            h.Append("<p class=\"end\">").Append(E(c.EndState)).Append("</p>\n");
        }

        h.Append("</div>\n<div class=\"tiles\">\n");

        h.Append("<div class=\"tile\">\n<div class=\"q\">규칙 자동화를 검토할 단계(단계 수 기준)</div>\n<div class=\"big\">")
            .Append(I(s.Stated.Software)).Append("<small>/ ").Append(I(s.Steps)).Append("단계</small></div>\n");
        if (s.Tentative.Software > 0)
        {
            h.Append("<div class=\"sub\">확인하면 바뀔 수 있음 ").Append(I(s.Tentative.Software)).Append("단계</div>\n");
        }

        h.Append("<div class=\"stack\" aria-label=\"담당 구성\">");
        foreach (var a in s.Assignees.Where(a => a.Count > 0))
        {
            h.Append("<span class=\"").Append(Assignees[a.Assignee].Css).Append("-fill\" style=\"flex:").Append(I(a.Count)).Append("\"></span>");
        }

        h.Append("</div>\n<div class=\"legend\">");
        foreach (var a in s.Assignees.Where(a => a.Count > 0))
        {
            h.Append("<span><i class=\"").Append(Assignees[a.Assignee].Css).Append("-fill\"></i>").Append(E(Assignees[a.Assignee].Name)).Append(' ').Append(I(a.Count)).Append("</span>");
        }

        h.Append("</div>\n</div>\n");

        h.Append("<div class=\"tile\">\n<div class=\"q\">실제 자료 시험</div>\n<div class=\"big\">");
        if (v.Status == "measured" && v.AccuracyPct is { } accuracy)
        {
            h.Append(D(accuracy)).Append('%');
            if (v.Verdict is not null)
            {
                h.Append(Verdict(v.Verdict));
            }
        }
        else
        {
            h.Append("<span class=\"na\">진행하지 않음</span>");
        }

        h.Append("</div>\n<div class=\"sub\">").Append(E(v.Status == "measured" ? v.VerdictReason ?? string.Empty : NotMeasured(v))).Append("</div>\n</div>\n");

        h.Append("<div class=\"tile\">\n<div class=\"q\">절감 효과</div>\n<div class=\"big na\">");
        h.Append(v.Time is { Result: "met" } ? "줄었다" : v.Time is { Result: "not_met" } ? "안 줄었다" : "아직 계산하지 않음");
        h.Append("</div>\n<div class=\"sub\">");
        if (s.Minutes.Count > 0)
        {
            h.Append("입력된 시간: ").Append(string.Join(" · ", s.Minutes.Select(m => I(m.Step) + "단계 건당 " + D(m.Minutes) + "분"))).Append(". ");
        }

        h.Append(E(v.Time is { Result: "met" or "not_met" } ? v.Time.Reason : NoSaving)).Append("</div>\n</div>\n");

        h.Append("<div class=\"tile\">\n<div class=\"q\">AI 활용 유지 제안</div>\n<div class=\"big\">").Append(I(s.Stated.AiAssist))
            .Append("<small>단계</small></div>\n<div class=\"sub\">실행 때 AI 초안을 쓰고 담당자가 확인하는 단계.");
        if (s.Tentative.AiAssist > 0)
        {
            h.Append(" 확인하면 바뀔 수 있음 ").Append(I(s.Tentative.AiAssist)).Append("단계.");
        }

        h.Append("</div>\n</div>\n</div>\n</section>\n");
    }

    private static string NotMeasured(ScorecardVerification v) => v.Reason == "no_compare_fields" ? NoCompareFields : NoComparison;

    private static void Status(StringBuilder h, string label, string value) =>
        h.Append("<li><span class=\"k\">").Append(label).Append("</span>").Append(E(value)).Append("</li>");

    private static void Line(StringBuilder h, string label, string text) =>
        h.Append("<div class=\"line\"><span class=\"k\">").Append(label).Append("</span><span>").Append(E(text)).Append("</span></div>\n");

    // 2구역 단계 표: 지금 하는 방식 | 추천 | 바꿔야 할 것 | 확인할 것. 코드·근거 키·동작 목록은 펼쳐 보기.
    private static void Steps(StringBuilder h, ScorecardResult r)
    {
        h.Append("<section class=\"panel\" id=\"zone-2\">\n<h2>단계별 판단</h2>\n<div class=\"steps\">\n");
        h.Append("<div class=\"cols head\"><div>지금 하는 방식</div><div>추천</div><div>바꿔야 할 것</div><div>확인할 것</div></div>\n");
        foreach (var s in r.Steps)
        {
            h.Append("<div class=\"row").Append(s.Differs ? " differs" : string.Empty).Append("\">\n");
            h.Append("<div class=\"title\"><span class=\"num\">").Append(I(s.Number)).Append("</span><span class=\"name\">").Append(E(s.Name))
                .Append("</span><span class=\"state\">").Append(Review(s.Review, r.Status.ConfirmedIn)).Append("</span></div>\n");
            h.Append("<div class=\"cols\">\n");
            Cell(h, "지금 하는 방식", E(s.Now));
            var rec = Badge(s.Assignee) + "<div class=\"why\">" + E(s.Reason) + "</div>";
            if (s.Tentative)
            {
                rec += "<div class=\"tent\">확인하면 바뀔 수 있음</div>";
            }

            Cell(h, "추천", rec);
            Cell(h, "바꿔야 할 것", E(s.Change));
            var check = E(s.Check);
            if (s.Assumptions.Count > 0)
            {
                check += "<ul class=\"assume\">" + string.Concat(s.Assumptions.Select(a => "<li>" + E(a) + "</li>")) + "</ul>";
            }

            Cell(h, "확인할 것", check);
            h.Append("</div>\n");
            StepDetails(h, s);
            h.Append("</div>\n");
        }

        h.Append("</div>\n</section>\n");
    }

    private static void Cell(StringBuilder h, string label, string html) =>
        h.Append("<div class=\"cell\"><div class=\"lbl\">").Append(label).Append("</div>").Append(html).Append("</div>\n");

    // 단계 펼쳐 보기: 동작 목록, 분류 코드·근거 키·규칙 의견, 기본값·추정 키, 엔진 부가 행, 확인 전후 담당.
    private static void StepDetails(StringBuilder h, ScorecardStep s)
    {
        h.Append("<details><summary>펼쳐 보기</summary>\n<div class=\"more\">\n");
        if (s.Actions.Count > 0)
        {
            h.Append("<div class=\"acts\"><div class=\"act head\"><div>동작</div><div>지금</div><div>추천</div><div>출처</div></div>\n");
            foreach (var a in s.Actions)
            {
                h.Append("<div class=\"act\"><div>").Append(E(a.Action)).Append("</div><div>").Append(E(WhoNow.GetValueOrDefault(a.WhoNow, a.WhoNow)))
                    .Append("</div><div>").Append(Code(a.Proposed)).Append("</div><div>").Append(E(Sources.GetValueOrDefault(a.Source, a.Source))).Append("</div></div>\n");
            }

            h.Append("</div>\n");
        }

        h.Append("<div class=\"codes\">");
        h.Append(s.Source == "ai" ? "<span class=\"chip ai\">AI 판단</span>" : "<span class=\"chip\">규칙 트리</span>");
        h.Append(" <span class=\"chip\">").Append(Code(s.Assignee)).Append(' ').Append(E(s.Rule)).Append("</span>");
        h.Append(" <span class=\"chip\">규칙 의견 ").Append(E(s.RuleOpinion.Assignee)).Append(' ').Append(E(s.RuleOpinion.Rule)).Append("</span>");
        if (s.Differs)
        {
            h.Append(" <span class=\"chip diff\">규칙 의견과 다름</span>");
        }

        if (s.Defaults.Count > 0)
        {
            h.Append(" <span class=\"chip mark\">기본값 ").Append(E(string.Join(", ", s.Defaults))).Append("</span>");
        }

        if (s.Inferred.Count > 0)
        {
            h.Append(" <span class=\"chip mark ai\">AI 추정 ").Append(E(string.Join(", ", s.Inferred))).Append("</span>");
        }

        h.Append("</div>\n");
        foreach (var x in s.Extra)
        {
            h.Append("<div class=\"extra\"><span class=\"chip\">").Append(E(x.Assignee)).Append(' ').Append(E(x.Rule)).Append("</span> ")
                .Append(E(GateKinds.GetValueOrDefault(x.Kind, x.Kind))).Append(": ").Append(E(x.Reason)).Append("</div>\n");
        }

        if (s.Review.Status == "changed")
        {
            h.Append("<div class=\"extra\">확인: 바뀜 → ").Append(E(s.Review.Assignee ?? string.Empty)).Append(". 사유: ").Append(E(s.Review.Reason ?? string.Empty)).Append("</div>\n");
        }

        h.Append("</div>\n</details>\n");
    }

    // 확인 상태는 버튼이 아니라 글자다(지시문 6.5). 대화에서 확인받은 단계는 "대화에서 확인됨".
    private static string Review(StepReview review, string? confirmedIn)
    {
        var where = confirmedIn == "conversation" ? "대화에서 확인됨" : "확인됨";
        return review.Status switch
        {
            "kept" => "<span class=\"ok\">" + where + "</span>",
            "changed" => "<span class=\"chg\">" + where + " · 바뀜: " + E(Assignees.TryGetValue(review.Assignee ?? string.Empty, out var a) ? a.Name : review.Assignee ?? string.Empty) + "</span>",
            _ => "<span class=\"pending\">확인 전</span>",
        };
    }

    // 3구역: 먼저 확인할 것. 확인할 내용은 결론·단계 표에 있으므로 여기서는 순서와 고른 이유만 쓴다.
    private static void Proposal(StringBuilder h, ScorecardResult r)
    {
        h.Append("<div class=\"card prop\" id=\"zone-3\">\n<div class=\"k\">먼저 확인할 것</div>\n");
        if (r.Proposal is { } p)
        {
            h.Append("<h3>").Append(I(p.Step)).Append("단계 ").Append(E(p.Name)).Append("</h3>\n");
            h.Append("<ol class=\"flow3\"><li>기존 기능으로 되는지 확인</li><li>안 되면 새로 만들지 판단</li><li>만들기로 하면 의뢰서</li></ol>\n");
            h.Append("<p>").Append(E(p.Effect)).Append("</p>\n");
            h.Append("<details><summary>고른 규칙과 만들기로 정한 뒤(구현 담당자 안내)</summary><p>").Append(E(p.Reason)).Append("</p><p class=\"cmd\"><code>scorecard build ").Append(E(r.Case))
                .Append(" --step ").Append(I(p.Step)).Append("</code></p></details>\n");
        }
        else
        {
            h.Append("<h3>규칙 자동화를 검토할 단계가 없다</h3>\n");
        }

        h.Append("</div>\n");
    }

    // 5구역: 담당자 확인과 남는 위험(무엇이 틀릴 수 있고 어떻게 잡는가).
    private static void Gates(StringBuilder h, ScorecardResult r)
    {
        h.Append("<div class=\"card info\" id=\"zone-5\">\n<div class=\"k\">담당자 확인</div>\n");
        if (r.Gates.Count == 0)
        {
            h.Append("<p>담당자 확인 단계가 없다.</p>\n");
        }
        else
        {
            h.Append("<ul>");
            foreach (var g in r.Gates)
            {
                h.Append("<li><b>").Append(I(g.Step)).Append("단계</b> ").Append(E(GateKinds.GetValueOrDefault(g.Kind, g.Kind))).Append(": ");
                h.Append(E(g.Text)).Append("<details><summary>엔진 문장</summary>").Append(E(g.Reason)).Append("</details>");

                h.Append("</li>");
            }

            h.Append("</ul>\n");
        }

        h.Append("<div class=\"k sep\">남는 위험</div>\n");
        if (r.Risks.Count == 0)
        {
            h.Append("<p>따로 적을 위험이 없다.</p>\n");
        }
        else
        {
            h.Append("<ul class=\"risks\">");
            foreach (var n in r.Risks)
            {
                h.Append("<li>").Append(E(n.Text)).Append("</li>");
            }

            h.Append("</ul>\n");
        }

        h.Append("</div>\n");
    }

    // 판단이 갈린 단계·개선 후보(위험과 다른 카드, 지시문 6.6).
    private static void Opinions(StringBuilder h, ScorecardResult r)
    {
        if (r.Opinions.Count > 0)
        {
            h.Append("<div class=\"card\">\n<div class=\"k\">판단이 갈린 단계</div>\n<ul>");
            foreach (var n in r.Opinions)
            {
                // 이유는 단계 표 추천 칸에 있으므로 되풀이하지 않는다(지시문 6.7). 문장 전체(n.Text)는 scorecard.json에 있다.
                var step = r.Steps.First(s => s.Number == n.Step);
                h.Append("<li><b>").Append(I(step.Number)).Append("단계 ").Append(E(step.Name)).Append("</b>: ").Append(Badge(step.Assignee))
                    .Append(" 권장. 이유는 단계 표 추천 칸.<details><summary>의견 차이</summary>AI 판단 ").Append(E(step.Assignee))
                    .Append(", 규칙 트리 의견 ").Append(E(step.RuleOpinion.Assignee)).Append(' ').Append(E(step.RuleOpinion.Rule)).Append("</details></li>");
            }

            h.Append("</ul>\n</div>\n");
        }

        if (r.Candidates.Count > 0)
        {
            h.Append("<div class=\"card\">\n<div class=\"k\">개선 후보</div>\n<ul>");
            foreach (var n in r.Candidates)
            {
                h.Append("<li>").Append(E(n.Text)).Append("</li>");
            }

            h.Append("</ul>\n</div>\n");
        }
    }

    // 4구역: 실제 자료 시험. 적용되지 않은 문턱은 기본 화면에 두지 않는다(지시문 5.4).
    private static void Verification(StringBuilder h, ScorecardResult r)
    {
        var v = r.Verification;
        var measured = v.Status == "measured";
        h.Append("<section class=\"panel verify\" id=\"zone-4\">\n<h2>실제 자료 시험");
        if (v.Counts is { } head)
        {
            h.Append("<span>기준 ").Append(I(head.Reference)).Append("건 ↔ 결과 ").Append(I(head.Candidate)).Append("건</span>");
        }

        h.Append("</h2>\n<div class=\"vbody\">\n<div>\n");
        if (v.Counts is { } c)
        {
            h.Append("<div class=\"counts\">");
            Count(h, c.Match, "일치", string.Empty);
            Count(h, c.Mismatch, "불일치", " bad");
            Count(h, c.Missing, "누락", " warn");
            Count(h, c.Extra, "추가", " na");
            h.Append("</div>\n<div class=\"bar\" aria-label=\"대조 결과 비율\">");
            Bar(h, c.Match, "fill-good");
            Bar(h, c.Mismatch, "fill-bad");
            Bar(h, c.Missing, "fill-warn");
            Bar(h, c.Extra, "fill-na");
            h.Append("</div>\n<div class=\"note\">일치 ").Append(I(c.Match)).Append("건 중 ").Append(I(c.Within)).Append("건은 허용오차 안의 차이. 누락률 ")
                .Append(v.MissingRatePct is { } m ? D(m) + "%" : "—").Append(".</div>\n");
        }
        else
        {
            h.Append("<p class=\"note\">").Append(E(NotMeasured(v))).Append(' ').Append(DefineErrors).Append("</p>\n");
        }

        if (measured && v.Verdict is not null)
        {
            h.Append("<div class=\"pattern\"><b>판정").Append(Verdict(v.Verdict)).Append("</b> ").Append(E(v.VerdictReason ?? string.Empty));
            if (v.Thresholds is { } t)
            {
                h.Append("<br>문턱: 정확도 ").Append(D(t.AccuracyMin)).Append("% 이상, 누락률 ").Append(D(t.MissingMax)).Append("% 이하");
            }

            h.Append("</div>\n");
        }

        if (v.Checks.Count > 0)
        {
            h.Append(measured ? "<div>" : "<details><summary>판정 확인 항목 펼쳐 보기</summary>");
            h.Append("<ul class=\"checks\">");
            foreach (var check in v.Checks)
            {
                h.Append("<li><span class=\"chip\">").Append(E(check.Rule)).Append(' ').Append(E(CheckResults.GetValueOrDefault(check.Result, check.Result)))
                    .Append("</span> ").Append(E(check.Reason)).Append("</li>");
            }

            h.Append("</ul>").Append(measured ? "</div>" : "</details>").Append('\n');
        }

        h.Append("</div>\n<div class=\"diffs\">\n");
        if (v.Items.Count > 0)
        {
            h.Append("<div class=\"d head\"><div>키</div><div>항목</div><div>기준 <span class=\"arrow\">→</span> 결과</div><div class=\"res\">결과</div></div>\n");
            foreach (var item in v.Items)
            {
                var (label, css) = ItemStatuses.GetValueOrDefault(item.Status, (item.Status, "v-na"));
                var diffs = item.Differences.Count == 0 ? new FieldDifference?[] { null } : item.Differences.Select(d => (FieldDifference?)d).ToArray();
                foreach (var d in diffs)
                {
                    h.Append("<div class=\"d\"><div class=\"key mono\">").Append(E(item.Key)).Append("</div><div class=\"field mono\">")
                        .Append(E(d?.Field ?? "—")).Append("</div><div class=\"vals mono\">");
                    if (d is not null)
                    {
                        h.Append(E(d.Reference)).Append(" <span class=\"arrow\">→</span> ").Append(E(d.Candidate));
                    }
                    else
                    {
                        h.Append(item.Status == "missing" ? "있음 <span class=\"arrow\">→</span> 없음" : "없음 <span class=\"arrow\">→</span> 있음");
                    }

                    h.Append("</div><div class=\"res\"><span class=\"verdict ").Append(css).Append("\">").Append(E(label)).Append("</span></div></div>\n");
                }
            }

            if (v.ItemsHidden > 0)
            {
                h.Append("<div class=\"note\">나머지 ").Append(I(v.ItemsHidden)).Append("개 항목은 metrics.json에 있다.</div>\n");
            }
        }
        else if (v.Counts is not null)
        {
            h.Append("<p class=\"note\">차이가 있는 항목이 없다.</p>\n");
        }

        h.Append("</div>\n</div>\n</section>\n");
    }

    // 6구역: 할 일 세 묶음 + 구현·검증 담당자 안내(펼쳐 보기).
    private static void Todo(StringBuilder h, ScorecardResult r)
    {
        h.Append("<section class=\"panel todo\" id=\"zone-6\">\n<h2>다음 할 일</h2>\n<div class=\"pad\">\n");
        foreach (var (group, title) in TodoGroups)
        {
            h.Append("<div class=\"label\">").Append(title).Append("</div>\n");
            TodoList(h, r.Todo.Where(t => t.Group == group).ToArray(), "없음.");
        }

        var guide = r.Todo.Where(t => t.Group == "implementer").ToArray();
        if (guide.Length > 0)
        {
            h.Append("<details><summary>구현·검증 담당자 안내</summary>\n");
            TodoList(h, guide, string.Empty);
            h.Append("</details>\n");
        }

        h.Append("</div>\n</section>\n");
    }

    private static void TodoList(StringBuilder h, ScorecardTodo[] items, string empty)
    {
        if (items.Length == 0)
        {
            h.Append("<p class=\"note\">").Append(empty).Append("</p>\n");
            return;
        }

        h.Append("<ol>");
        foreach (var n in items)
        {
            h.Append("<li>").Append(E(n.Text)).Append("</li>");
        }

        h.Append("</ol>\n");
    }

    // 7구역: 업무 요약 네 줄, 사용자가 준 숫자, 입력 원문(접음), 단계 흐름, 파일·해시·schema·자기 검사(펼쳐 보기).
    private static void Overview(StringBuilder h, ScorecardResult r)
    {
        h.Append("<section class=\"panel overview\" id=\"zone-7\">\n<h2>업무 요약과 입력 기록</h2>\n<div class=\"pad\">\n");
        if (r.TaskSummary is { } ts)
        {
            h.Append("<ul class=\"brief\">");
            Brief(h, "규모", ts.Scale);
            Brief(h, "현재 처리", ts.Current);
            Brief(h, "예외율", ts.ExceptionRate);
            Brief(h, "결과 전달", ts.Delivery);
            h.Append("</ul>\n");
        }

        if (r.StatedNumbers.Count > 0)
        {
            h.Append("<div class=\"label\">사용자가 준 숫자(그대로)</div>\n<ul class=\"brief nums\">");
            foreach (var n in r.StatedNumbers)
            {
                h.Append("<li>").Append(E(n)).Append("</li>");
            }

            h.Append("</ul>\n");
        }

        if (r.Intake is not null)
        {
            h.Append("<details><summary>입력 원문 보기</summary>\n<div class=\"intake\">").Append(E(r.Intake)).Append("</div>\n</details>\n");
        }

        h.Append("<div class=\"label\">단계 흐름</div>\n<ol class=\"flow\">");
        foreach (var s in r.Steps)
        {
            h.Append("<li>").Append(E(s.Name)).Append(' ').Append(Badge(s.Assignee)).Append("</li>");
        }

        h.Append("</ol>\n<details><summary>파일·해시·schema 펼쳐 보기</summary>\n");
        if (r.SelfCheck.Count > 0)
        {
            h.Append("<div class=\"label\">스킬 자기 검사</div>\n<ul class=\"hashes\">");
            foreach (var c in r.SelfCheck)
            {
                h.Append("<li><code>").Append(E(c.Rule)).Append("</code> ").Append(E(SelfCheckResults.GetValueOrDefault(c.Result, c.Result)));
                if (c.Note is not null)
                {
                    h.Append(" · ").Append(E(c.Note));
                }

                h.Append("</li>");
            }

            h.Append("</ul>\n");
        }

        h.Append("<div class=\"label\">입력 SHA-256</div>\n");
        Hashes(h, r.Inputs, "입력 파일이 없다.");
        h.Append("<div class=\"label\">결과 파일 SHA-256</div>\n");
        Hashes(h, r.Results, "결과 파일이 없다.");
        if (r.Review is { } review)
        {
            h.Append("<div class=\"label\">확인 파일 SHA-256</div>\n");
            Hashes(h, [review], string.Empty);
        }

        h.Append("<p class=\"foot\">scorecard.json schema ").Append(I(ScorecardJson.SchemaVersion))
            .Append(". 같은 입력이면 같은 결과. 숫자는 Scorecard.Core가 냈다. 이 파일은 scorecard.json과 같은 결과 객체에서 만들었다. 인쇄하면 PDF가 된다.</p>\n</details>\n</div>\n</section>\n");
    }

    private static void Brief(StringBuilder h, string label, string? value)
    {
        if (value is not null)
        {
            h.Append("<li><span class=\"k\">").Append(label).Append("</span>").Append(E(value)).Append("</li>");
        }
    }

    private static void Hashes(StringBuilder h, IReadOnlyList<FileHash> hashes, string empty)
    {
        if (hashes.Count == 0)
        {
            h.Append("<p class=\"note\">").Append(E(empty)).Append("</p>\n");
            return;
        }

        h.Append("<ul class=\"hashes\">");
        foreach (var x in hashes)
        {
            h.Append("<li><code>").Append(E(x.Path)).Append("</code> <span class=\"mono hash\">").Append(E(x.Sha256)).Append("</span></li>");
        }

        h.Append("</ul>\n");
    }

    private static void Count(StringBuilder h, int value, string label, string css) =>
        h.Append("<div class=\"cnt").Append(css).Append("\"><b>").Append(I(value)).Append("</b><span>").Append(label).Append("</span></div>");

    private static void Bar(StringBuilder h, int value, string css)
    {
        if (value > 0)
        {
            h.Append("<span class=\"").Append(css).Append("\" style=\"flex:").Append(I(value)).Append("\"></span>");
        }
    }

    // 기본 화면 배지: 업무 말 이름(코드 없음).
    private static string Badge(string assignee) =>
        Assignees.TryGetValue(assignee, out var a)
            ? "<span class=\"badge " + a.Css + "\">" + E(a.Name) + "</span>"
            : "<span class=\"badge\">" + E(assignee) + "</span>";

    // 펼쳐 보기 배지: 코드 이름.
    private static string Code(string assignee) =>
        Assignees.TryGetValue(assignee, out var a) ? E(a.Code) : E(assignee);

    private static string Verdict(string verdict)
    {
        var (label, css) = Verdicts.GetValueOrDefault(verdict, (verdict, "v-na"));
        return "<span class=\"verdict " + css + "\">" + E(label) + "</span>";
    }

    private static string E(string text) => Encoder.Encode(text);

    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    // 시안(M2-스코어카드-시안.html)의 스타일. 390·900·1236px에서 가로 스크롤이 없게 한다.
    // 원본 줄바꿈(Windows 체크아웃의 CRLF)과 상관없이 LF로 쓴다(결정론).
    private static readonly string CssLf = Css.ReplaceLineEndings("\n");

    private const string Css = """
:root{--bg:#f6f7f9;--card:#fff;--ink:#16181d;--ink2:#4a5060;--ink3:#7a8090;--line:#e3e6eb;--line2:#eef0f3;
--c1:#0f766e;--c1bg:#e6f6f4;--c2:#1d5bd8;--c2bg:#eaf1ff;--c2m:#0e7490;--c2mbg:#e4f5fa;--c3:#7c3aed;--c3bg:#f3edff;--c4:#b45309;--c4bg:#fff4e5;
--good:#15803d;--goodbg:#e8f7ee;--warn:#b45309;--warnbg:#fff4e5;--bad:#c2410c;--badbg:#fff0ea;--na:#6b7280;--nabg:#f1f2f4}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--ink);font-family:"Noto Sans CJK KR","Noto Sans KR","Malgun Gothic","Apple SD Gothic Neo",system-ui,sans-serif;font-size:14px;line-height:1.55;word-break:keep-all;overflow-wrap:anywhere}
.wrap{max-width:1180px;margin:0 auto;padding:24px 28px 32px}
header{border-bottom:1px solid var(--line);padding-bottom:12px;margin-bottom:14px}
h1{font-size:22px;margin:0 0 4px;letter-spacing:-.2px}
.case{display:inline-block;font-family:ui-monospace,Consolas,monospace;font-size:13px;background:#fff;border:1px solid var(--line);border-radius:6px;padding:1px 8px;margin-left:8px;vertical-align:3px;color:var(--ink2)}
.task{color:var(--ink2)}
.banner{background:#fff7d6;color:#6b4e00;border:1px solid #f0dc8a;border-radius:8px;padding:8px 12px;font-weight:700;font-size:13px;margin:0 0 12px}
.notice{background:var(--warnbg);color:#7a3e05;border-radius:8px;padding:8px 12px;font-size:12.5px;margin:0 0 12px}
.status{list-style:none;margin:0 0 12px;padding:0;display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:8px}
.status li{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:8px 12px;font-size:13px;min-width:0}
.status .k,.conclusion .k,.brief .k{display:block;font-size:11.5px;font-weight:700;color:var(--ink3)}
.conclusion{background:var(--card);border:2px solid var(--ink);border-radius:10px;padding:12px 16px;margin-bottom:12px}
.conclusion .line{display:grid;grid-template-columns:150px minmax(0,1fr);column-gap:12px;padding:5px 0;border-bottom:1px dashed var(--line2)}
.conclusion .line .k{font-size:13px;color:var(--ink);padding-top:1px}
.questions{margin:2px 0 0;padding-left:18px;color:var(--ink2);font-size:13px}
.end{margin:10px 0 0;font-weight:700}
.tiles{display:grid;grid-template-columns:1.25fr 1fr 1fr 1fr;gap:12px;margin-bottom:18px}
.tile{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:14px 16px;min-width:0}
.tile .q{font-size:12.5px;color:var(--ink2);font-weight:600;margin-bottom:6px}
.tile .big{font-size:28px;font-weight:700;letter-spacing:-.5px;line-height:1.1}
.tile .big small{font-size:14px;font-weight:600;color:var(--ink2);margin-left:4px}
.tile .sub{font-size:12.5px;color:var(--ink2);margin-top:8px}
.na{color:var(--na)}
.big.na{font-size:20px}
.verdict{display:inline-block;font-size:12px;font-weight:700;border-radius:999px;padding:2px 9px;margin-left:6px;vertical-align:5px;white-space:nowrap;letter-spacing:0}
.v-bad{color:var(--bad);background:var(--badbg)}.v-good{color:var(--good);background:var(--goodbg)}.v-warn{color:var(--warn);background:var(--warnbg)}.v-na{color:var(--na);background:var(--nabg)}
.stack{display:flex;height:10px;border-radius:5px;overflow:hidden;margin-top:10px;background:var(--line2)}
.stack span{display:block;height:100%}
.legend{display:flex;gap:4px 10px;flex-wrap:wrap;font-size:11.5px;color:var(--ink2);margin-top:6px}
.legend i{display:inline-block;width:9px;height:9px;border-radius:2px;margin-right:4px;vertical-align:-1px}
.b1-fill{background:var(--c1)}.b2-fill{background:var(--c2)}.b2m-fill{background:var(--c2m)}.b3-fill{background:var(--c3)}.b4-fill{background:var(--c4)}
.grid{display:grid;grid-template-columns:minmax(0,1fr) 320px;gap:16px;align-items:start}
.panel{background:var(--card);border:1px solid var(--line);border-radius:10px;min-width:0}
.panel h2{font-size:15px;margin:0;padding:14px 16px 10px}
.panel h2 span{font-weight:400;color:var(--ink3);font-size:12.5px;margin-left:6px}
.steps .cols{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));column-gap:12px}
.steps .cols.head{background:#fafbfc;border-top:1px solid var(--line);border-bottom:1px solid var(--line);padding:8px 14px;font-size:12px;color:var(--ink3);font-weight:600}
.steps .row{padding:11px 14px;border-bottom:1px solid var(--line2)}
.steps .row.differs{background:#fffaf5}
.steps .title{display:flex;flex-wrap:wrap;align-items:baseline;gap:4px 8px;margin-bottom:6px}
.steps .num{color:var(--ink3);font-variant-numeric:tabular-nums}
.steps .name{font-weight:700}
.steps .state{margin-left:auto;font-size:12px}
.steps .cell{font-size:13px;color:var(--ink2);min-width:0}
.steps .cell .lbl{display:none;font-size:11.5px;font-weight:700;color:var(--ink3)}
.steps .why{margin-top:4px}
.steps .tent{margin-top:4px;font-size:12px;color:var(--warn);font-weight:600}
.assume{margin:4px 0 0;padding-left:16px;font-size:12px}
.ok{color:var(--good)}.chg{color:var(--c4);font-weight:600}.pending{color:var(--ink3)}
details{margin-top:6px;font-size:12.5px;color:var(--ink2)}
summary{cursor:pointer;color:var(--ink3);font-size:12px}
.more{margin-top:6px;padding:8px 10px;background:#fafbfc;border-radius:8px}
.acts{margin-bottom:8px}
.act{display:grid;grid-template-columns:minmax(0,2fr) minmax(0,1fr) minmax(0,1fr) minmax(0,1fr);column-gap:8px;padding:4px 0;border-bottom:1px solid var(--line2)}
.act>div{min-width:0}
.act.head{font-weight:700;color:var(--ink3)}
.codes{display:flex;flex-wrap:wrap;gap:4px}
.extra{margin-top:6px}
.badge{display:inline-flex;align-items:center;font-size:12px;font-weight:700;border-radius:6px;padding:2px 7px}
.b1{color:var(--c1);background:var(--c1bg)}.b2{color:var(--c2);background:var(--c2bg)}.b2m{color:var(--c2m);background:var(--c2mbg)}.b3{color:var(--c3);background:var(--c3bg)}.b4{color:var(--c4);background:var(--c4bg)}
.chip{display:inline-block;font-size:11.5px;color:var(--ink2);background:var(--nabg);border-radius:5px;padding:1px 6px;font-weight:400}
.chip.ai{color:var(--c3);background:var(--c3bg)}
.chip.diff{color:var(--bad);background:var(--badbg);font-weight:700}
.side{display:flex;flex-direction:column;gap:12px;min-width:0}
.card{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:14px 16px;min-width:0}
.card.prop{border:2px solid var(--c2)}
.card .k{font-size:11.5px;font-weight:700;color:var(--c2);letter-spacing:.2px;margin-bottom:4px}
.card.info .k{color:var(--c4)}
.card .k.sep{margin-top:10px;padding-top:8px;border-top:1px dashed var(--line)}
.card h3{font-size:15px;margin:0 0 6px}
.card p{margin:0 0 8px;color:var(--ink2);font-size:13px}
.card .cmd{font-size:12px;margin:6px 0 0}
.card ul,.flow3{margin:4px 0 0;padding-left:16px;color:var(--ink2);font-size:12.5px}
.flow3{margin-bottom:8px}
.card li{margin-bottom:3px}
code{font-family:ui-monospace,Consolas,monospace;font-size:11.5px;background:#f3f4f6;border-radius:4px;padding:1px 5px;color:#333;overflow-wrap:anywhere}
.verify,.todo,.overview{margin-top:16px}
.vbody{display:grid;grid-template-columns:330px minmax(0,1fr);gap:18px;padding:0 16px 16px}
.vbody>div{min-width:0}
.counts{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:8px;margin-top:2px}
.cnt{border:1px solid var(--line);border-radius:8px;padding:8px 10px;min-width:0}
.cnt b{display:block;font-size:20px;line-height:1.2}
.cnt span{font-size:11.5px;color:var(--ink2)}
.cnt.bad b{color:var(--bad)}.cnt.warn b{color:var(--warn)}.cnt.na b{color:var(--na)}
.bar{display:flex;height:12px;border-radius:6px;overflow:hidden;margin-top:12px;background:var(--line2)}
.bar span{display:block;height:100%}
.fill-good{background:#7fb07f}.fill-bad{background:#e07a52}.fill-warn{background:#e0a93f}.fill-na{background:#b8bcc4}
.note{font-size:12px;color:var(--ink2);margin-top:6px}
.pattern{margin-top:12px;border-radius:8px;background:var(--warnbg);color:#7a3e05;font-size:12.5px;padding:9px 11px}
.pattern .verdict{vertical-align:0}
.checks{margin:10px 0 0;padding-left:0;list-style:none;font-size:12.5px;color:var(--ink2)}
.checks li{margin-bottom:5px}
.diffs .d{display:grid;grid-template-columns:64px 120px minmax(0,1fr) 84px;column-gap:10px;padding:8px 10px;border-bottom:1px solid var(--line2);font-size:12.5px;align-items:center}
.diffs .d>div{min-width:0}
.diffs .d.head{background:#fafbfc;border-top:1px solid var(--line);border-bottom:1px solid var(--line);font-size:12px;color:var(--ink3);font-weight:600}
.diffs .res{text-align:right}
.diffs .res .verdict{margin:0;vertical-align:0}
.mono{font-family:ui-monospace,Consolas,monospace}
.arrow{color:var(--ink3);margin:0 4px}
.todo ol{margin:0;padding-left:20px;color:var(--ink2);font-size:13px}
.todo li{margin-bottom:4px}
.pad{padding:0 16px 16px}
.label{font-size:12px;font-weight:700;color:var(--ink3);margin:10px 0 4px}
.brief{list-style:none;margin:0;padding:0;display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:6px 16px;font-size:13px;color:var(--ink2)}
.brief.nums{grid-template-columns:minmax(0,1fr)}
.intake{white-space:pre-wrap;color:var(--ink2);font-size:13px;background:#fafbfc;border:1px solid var(--line2);border-radius:8px;padding:10px 12px;margin-top:6px}
.flow{margin:0;padding-left:20px;color:var(--ink2);font-size:13px}
.flow li{margin-bottom:3px}
.hashes{margin:0;padding-left:0;list-style:none;font-size:12px;color:var(--ink2)}
.hashes li{margin-bottom:4px}
.hash{word-break:break-all;font-size:11.5px;color:var(--ink3)}
.foot{font-size:11.5px;color:var(--ink3);margin:12px 0 0}
@media (max-width:1040px){
.grid{grid-template-columns:minmax(0,1fr)}
.side{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));align-items:start}
.tiles,.status{grid-template-columns:repeat(2,minmax(0,1fr))}
.vbody{grid-template-columns:minmax(0,1fr)}
}
@media (max-width:760px){
.wrap{padding:16px 16px 24px}
h1{font-size:19px}
.case{margin-left:0}
.tile{padding:12px 14px}
.tile .big{font-size:24px}
.conclusion .line{grid-template-columns:minmax(0,1fr)}
.steps .cols.head{display:none}
.steps .cols{grid-template-columns:minmax(0,1fr);row-gap:8px}
.steps .cell .lbl{display:block}
.steps .state{margin-left:0;width:100%}
.act{grid-template-columns:minmax(0,1fr) minmax(0,1fr)}
.act.head{display:none}
.brief{grid-template-columns:minmax(0,1fr)}
.diffs .d.head{display:none}
.diffs .d{grid-template-columns:64px minmax(0,1fr) auto;grid-template-areas:"key field res" "key vals res";row-gap:2px}
.diffs .d .key{grid-area:key}
.diffs .d .field{grid-area:field}
.diffs .d .vals{grid-area:vals}
.diffs .d .res{grid-area:res}
}
@media (max-width:420px){
.tiles,.status{grid-template-columns:minmax(0,1fr);gap:8px}
.counts{grid-template-columns:repeat(2,minmax(0,1fr))}
}
@media print{body{background:#fff}.wrap{max-width:none}details{display:block}}

""";
}
