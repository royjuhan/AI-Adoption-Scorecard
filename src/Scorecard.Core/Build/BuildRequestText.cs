using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Core.Comparison;
using Scorecard.Core.Report;

namespace Scorecard.Core.Build;

// 번들의 글 파일(spec.md, acceptance.md, samples/, export-check.md). docs/spec/build-request.md 4절.
// 줄은 "\n"으로만 잇는다(여러 줄 원시 문자열을 쓰지 않는다. 체크아웃 줄바꿈과 상관없이 같은 바이트). 끝에 "\n" 하나.
internal static class BuildRequestText
{
    // 출력 열(4.2): 키 → compare 필드 → 체크리스트 문장 열 → 유형 열. 같은 이름은 처음 것만. inputs가 없으면 빈 목록.
    public static IReadOnlyList<BundleColumn> Columns(CaseDefinition? definition)
    {
        if (definition?.Inputs is not { } inputs)
        {
            return [];
        }

        var columns = new List<BundleColumn> { new(inputs.Key, BuildRequestRules.RoleKey, BuildRequestRules.TypeText, null, null) };
        foreach (var f in definition.Compare)
        {
            var number = f.Tolerance is not null || f.TolerancePct is not null;
            Add(columns, new BundleColumn(f.Field, BuildRequestRules.RoleCompare, number ? BuildRequestRules.TypeNumber : BuildRequestRules.TypeText, f.Tolerance, f.TolerancePct));
        }

        if (definition.Checklist is { } checklist)
        {
            Add(columns, new BundleColumn(checklist.Text, BuildRequestRules.RoleChecklist, BuildRequestRules.TypeText, null, null));
        }

        if (inputs.Group is { } group)
        {
            Add(columns, new BundleColumn(group, BuildRequestRules.RoleGroup, BuildRequestRules.TypeText, null, null));
        }

        return columns;
    }

    public static string Spec(BundleView v)
    {
        var s = v.Step;
        var a = v.Answers;
        var lines = new List<string>
        {
            $"# 제작 의뢰서: {v.Scorecard.Case} {N(s.Number)}단계",
            string.Empty,
            "- 업무: " + (v.Scorecard.Task ?? "(적지 않음)"),
            $"- 만들 단계: {N(s.Number)}단계 {s.Name}",
            "- 담당: " + s.Assignee + (s.Assignee == "②M"
                ? ". 미리 학습해 고정한 모델을 규칙 SW처럼 돌린다. 실행 시 AI를 부르지 않는다."
                : ". 규칙 기반 소프트웨어로 만든다. 실행 시 AI를 부르지 않는다."),
            $"- 판단: {(s.Source == StepClassifier.SourceAi ? "AI 판단" : "규칙 트리")} {s.Assignee} `{s.Rule}`. 규칙 의견 {s.RuleOpinion.Assignee} `{s.RuleOpinion.Rule}`" + (s.Differs ? "(AI 판단과 다르다)." : "."),
            "- 근거: " + s.Reason,
            string.Empty,
            "## 입력 → 처리 → 출력",
            string.Empty,
            a is null
                ? "- 입력: 답이 없다(answers.yaml·case.yaml에 같은 단계가 없다). 제작 전에 입력 형태를 정한다."
                : $"- 입력: 형태 `{a.InputKind.Value}`, 한 번에 {N(a.BatchSize.Value)}건, 빈도 `{a.Frequency.Value}`.",
            "- 처리: 근거 문장의 판단을 코드 규칙으로 옮긴다(아래 규칙 초안).",
            v.Columns.Count == 0
                ? "- 출력: 열이 정해지지 않았다. case.yaml에 inputs를 적고 다시 만든다."
                : $"- 출력: `io-schema.json`의 열 {N(v.Columns.Count)}개. 키 `{v.Columns[0].Name}`.",
            string.Empty,
            "## 규칙 초안",
            string.Empty,
        };

        var rules = v.Columns.Where(c => c.Role == BuildRequestRules.RoleCompare).Select(c => $"- `{c.Name}`: {Tolerance(c)}").ToList();
        if (v.Definition?.Checklist is { } checklist)
        {
            rules.Add($"- `{checklist.Text}`: 문장. 항목 {string.Join(", ", checklist.Items.Select(i => "`" + i.Item + "`"))}이 모두 들어가야 한다.");
        }

        lines.AddRange(rules.Count > 0 ? rules : ["- 비교 열이 없다. 규칙은 제작 환경의 스펙 승인 때 정한다."]);
        lines.Add("- 기본값을 쓴 답: " + KeyList(s.Defaults) + ". 제작 전에 확인한다.");
        lines.Add("- AI가 추정한 답: " + KeyList(s.Inferred) + ".");
        lines.Add(string.Empty);
        lines.Add("## 예외 처리");
        lines.Add(string.Empty);
        if (a is not null)
        {
            lines.Add($"- 예외·이상 건 비율: {N(a.ExceptionRate.Value)}%.");
        }

        var review = s.Extra.FirstOrDefault(r => r.Kind == StepClassifier.KindExceptionReview);
        lines.Add(review is null
            ? "- 예외 검토 행이 없다. 규칙에 맞지 않는 건은 결과에 넣지 않고 멈춘 뒤 사람에게 넘긴다."
            : $"- 예외 검토 {review.Assignee}: {review.Reason}");
        lines.Add(string.Empty);
        lines.Add("## 사람 게이트");
        lines.Add(string.Empty);
        var gate = s.Extra.FirstOrDefault(r => r.Kind == StepClassifier.KindHumanGate);
        lines.Add(gate is null ? "- 이 단계 뒤 사람 게이트가 없다." : $"- 이 단계 뒤 사람 게이트 {gate.Assignee}: {gate.Reason}");
        var others = v.Scorecard.Gates.Where(g => g.Step != s.Number).Select(g => $"{N(g.Step)}단계 {g.Name}(`{g.Kind}`)").ToArray();
        if (others.Length > 0)
        {
            lines.Add("- 업무의 다른 사람 확인: " + string.Join(", ", others) + ".");
        }

        lines.Add(string.Empty);
        lines.Add("## 실행 조건");
        lines.Add(string.Empty);
        lines.Add("- 실행 시 AI·네트워크·외부 프로세스를 쓰지 않는다.");
        lines.Add("- 같은 입력이면 같은 결과다. 난수·현재 시각을 결과에 쓰지 않는다.");
        lines.Add("- 입력 파일은 읽기만 한다. 출력은 새 폴더에 쓴다.");
        return Join(lines);
    }

    public static string Acceptance(BundleView v)
    {
        var s = v.Step;
        var ver = v.Scorecard.Verification;
        var lines = new List<string>
        {
            $"# 수용 기준: {v.Scorecard.Case} {N(s.Number)}단계 {s.Name}",
            string.Empty,
            "## 기준 숫자",
            string.Empty,
        };

        if (ver.Thresholds is { } t)
        {
            lines.Add($"- 정확도 {N(t.AccuracyMin)}% 이상(기준 자료와 허용 오차 안에서 일치한 항목 / 기준 항목).");
            lines.Add($"- 누락률 {N(t.MissingMax)}% 이하.");
            lines.Add(t.TimeMustDrop
                ? "- 처리+검토 시간이 적용 전보다 줄어야 한다."
                : "- 시간 조건은 보지 않는다(`time_must_drop: false`). 건당 시간은 잰다.");
        }
        else
        {
            lines.Add("- 기준 숫자가 아직 없다. `scorecard check`를 돌린 뒤 다시 만든다.");
        }

        lines.Add(ver.Status switch
        {
            "measured" => $"- 지금 검증: 정확도 {Pct(ver.AccuracyPct)}%, 누락률 {Pct(ver.MissingRatePct)}%, 판정 `{ver.Verdict ?? "없음"}`.",
            "not_verified" => "- 지금 검증: 미검증. " + (ver.VerdictReason ?? ver.Reason ?? string.Empty),
            _ => "- 지금 검증: `scorecard check`를 돌리지 않았다.",
        });
        lines.Add(string.Empty);
        lines.Add("## 테스트 시나리오");
        lines.Add(string.Empty);
        if (v.Columns.Count > 0)
        {
            lines.Add("- `samples/output.csv`를 기준 자료로 두고, 같은 열을 내는 결과를 `scorecard check`로 대조한다. 정확도 100.0%, 누락률 0.0%여야 한다.");
            lines.Add($"- 키 `{v.Columns[0].Name}` 행 하나를 빼면 누락 1건으로 센다.");
            lines.AddRange(v.Columns.Where(c => c.Role == BuildRequestRules.RoleCompare).Select(c => c.Type == BuildRequestRules.TypeNumber
                ? $"- `{c.Name}`: 허용 오차 경계의 값은 일치, 넘으면 불일치로 센다."
                : $"- `{c.Name}`: 글자 하나가 다르면 불일치로 센다."));
        }
        else
        {
            lines.Add("- 출력 열이 정해지면 그 열로 기준 자료를 만들고 `scorecard check`로 대조한다.");
        }

        if (s.Extra.Any(r => r.Kind == StepClassifier.KindExceptionReview))
        {
            lines.Add("- 규칙에 맞지 않는 건은 결과에 넣지 않고 사람 검토 목록으로 보낸다.");
        }

        lines.Add("- 같은 입력을 두 번 돌리면 같은 바이트가 나온다.");
        lines.Add("- 네트워크를 끊고 돌려도 같은 결과가 나온다.");
        lines.Add(string.Empty);
        lines.Add("## 제작 게이트 세 질문");
        lines.Add(string.Empty);
        lines.Add("| 질문 | 언제 | 답할 때 볼 것 |");
        lines.Add("| --- | --- | --- |");
        lines.AddRange(BuildRequestRules.Gates.Select(g => $"| \"{g.Question}\" | {g.When} | {g.Look} |"));
        return Join(lines);
    }

    public static string SamplesReadme(bool hasCsv) => Join(hasCsv
        ?
        [
            "# 합성 샘플",
            string.Empty,
            "- 도구가 `io-schema.json`의 열로 만든 합성 값이다. 실제 값이 없다.",
            $"- `output.csv`: 출력 {N(BuildRequestRules.SampleRows)}행. 키 `{BuildRequestRules.SampleKeyPrefix}01`부터. 숫자 열은 정해진 식으로, 글자 열은 정해진 낱말을 돌려 쓴다.",
            "- 같은 케이스·같은 판이면 같은 바이트다.",
        ]
        :
        [
            "# 합성 샘플",
            string.Empty,
            "- case.yaml에 inputs가 없어 출력 열을 모른다. 샘플 CSV를 만들지 않았다.",
            "- 출력 열을 정하면 다시 만든다.",
        ]);

    // 합성 CSV(4.3). UTF-8(BOM 없음), 구분자 ',', LF. 머리 행 이름은 RFC 4180으로 감싼다(필요할 때만). 값은 ASCII 합성 값.
    public static byte[] SampleCsv(IReadOnlyList<BundleColumn> columns)
    {
        var text = new StringBuilder();
        text.Append(string.Join(',', columns.Select(c => Quote(c.Name)))).Append('\n');
        for (var i = 1; i <= BuildRequestRules.SampleRows; i++)
        {
            text.Append(string.Join(',', columns.Select(c => SampleValue(c, i)))).Append('\n');
        }

        return Utf8.GetBytes(text.ToString());
    }

    public static string ExportCheck(BundleView v, IReadOnlyList<SourceFile> files, IReadOnlyList<ScanHit> hits)
    {
        var lines = new List<string>
        {
            $"# 내보내기 확인: {v.Scorecard.Case} {N(v.Step.Number)}단계",
            string.Empty,
            "- 이 번들은 보안 경계 밖의 제작 환경으로 간다. 실제 자료(품번·이메일·경로·실제 값)가 없어야 한다(지시문 2절 8).",
            "- 사람이 직접 번들 파일을 읽고 맨 아래 확인 줄 끝에 이름이나 역할을 적는다. 스킬·AI는 확인 줄을 적지 않는다.",
            "- 이 파일의 다른 줄을 바꾸거나 번들이 바뀌면(답·단계·결과가 바뀌면) 확인이 무효다. build가 새 초안을 쓴다.",
            string.Empty,
            "## 번들 파일",
            string.Empty,
            "| 파일 | SHA-256 |",
            "| --- | --- |",
        };
        lines.AddRange(files.Select(f => $"| `{f.Path}` | `{Convert.ToHexStringLower(SHA256.HashData(f.Content))}` |"));
        lines.Add(string.Empty);
        lines.Add("## 문자열 검사");
        lines.Add(string.Empty);
        lines.Add("- 검사한 패턴: " + string.Join(", ", BuildRequestRules.Patterns.Select(p => "`" + p.Name + "`")) + ".");
        lines.Add($"- 걸린 것: {N(hits.Count)}건.");
        lines.AddRange(hits.Select(h => $"- `{h.Pattern}` `{h.Path}`:{N(h.Line)}: `{h.Value}`"));
        if (hits.Count > 0)
        {
            lines.Add("- 걸린 것이 있으면 확인 줄을 적어도 내보내지 않는다. 답 파일·case.yaml에서 값을 빼고 다시 돌린다.");
        }

        lines.Add(string.Empty);
        lines.Add("## 사람 확인");
        lines.Add(string.Empty);
        lines.Add("위 파일을 모두 읽었고 실제 자료가 없음을 확인했다.");
        lines.Add(string.Empty);
        lines.Add(BuildRequestRules.SignaturePrefix);
        return Join(lines);
    }

    private static string SampleValue(BundleColumn c, int i)
    {
        if (c.Role == BuildRequestRules.RoleKey)
        {
            return BuildRequestRules.SampleKeyPrefix + i.ToString("D2", CultureInfo.InvariantCulture);
        }

        if (c.Type == BuildRequestRules.TypeNumber)
        {
            // 절대 허용 오차: 정수. 백분율: 소수 두 자리.
            return c.TolerancePct is not null
                ? (10m + (i * 12.25m)).ToString("0.00", CultureInfo.InvariantCulture)
                : (i * 10).ToString(CultureInfo.InvariantCulture);
        }

        return c.Role switch
        {
            BuildRequestRules.RoleGroup => BuildRequestRules.SampleGroups[(i - 1) % BuildRequestRules.SampleGroups.Length],
            BuildRequestRules.RoleChecklist => BuildRequestRules.SampleChecklistPrefix + i.ToString("D2", CultureInfo.InvariantCulture),
            _ => BuildRequestRules.SampleTexts[(i - 1) % BuildRequestRules.SampleTexts.Length],
        };
    }

    private static string Tolerance(BundleColumn c) =>
        c.Tolerance is { } abs ? $"숫자. 허용 오차 {N(abs)}(절대)."
        : c.TolerancePct is { } pct ? $"숫자. 허용 오차 {N(pct)}%(기준 값 기준)."
        : "글자. 앞뒤 공백을 뗀 뒤 정확히 같아야 한다.";

    private static string Quote(string name) =>
        name.Length > 0 && !name.Any(ch => ch is ',' or '"' or '\n' or '\r') && name.Trim() == name
            ? name
            : "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static string KeyList(IReadOnlyList<string> keys) => keys.Count == 0 ? "없다" : string.Join(", ", keys.Select(k => "`" + k + "`"));

    private static void Add(List<BundleColumn> columns, BundleColumn column)
    {
        if (!columns.Any(c => c.Name == column.Name))
        {
            columns.Add(column);
        }
    }

    private static string Pct(decimal? value) => value is { } d ? N(d) : "-";

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string N(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Join(IEnumerable<string> lines) => string.Join('\n', lines) + "\n";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
}
