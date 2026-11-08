using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using static System.FormattableString;

namespace Scorecard.Core.Cases;

// 트리를 스키마 순서대로 걷는다(스펙 4.3~4.6). 필드마다 형식·필수 여부를 보고 오류를 모은다.
// 찾는 순서는 4.3 표 순서다. 같은 위치의 오류는 이 순서로 나온다(스펙 4.9).
// 숫자는 정규식으로 모양을 본 뒤 InvariantCulture로 읽는다. 이름 비교는 서수다.
internal sealed class CaseReader(ErrorSink errors)
{
    private const int MaxNameLength = 64;
    private const decimal PercentMin = 0m;
    private const decimal PercentMax = 100m;
    private const decimal ToleranceMin = 0m;

    private const string MissingField = "필수 필드가 없다";
    private const string EmptyValue = "값이 비어 있다";
    private const string MustBeMapping = "매핑이어야 한다";
    private const string MustBeList = "목록이어야 한다";
    private const string MustBeScalar = "스칼라 값이어야 한다";
    private const string GroupPath = "inputs.group";
    private const string RelativePathMessage = "케이스 폴더 기준 상대 경로를 '/'로 쓴다 ('..', ':', '\\' 없음)";

    // text-check.md 4.1 scores.min·max 범위.
    private const int ScoreBoundMin = 0;
    private const int ScoreBoundMax = 100;

    // 끝은 \z로 묶는다. $는 끝 줄바꿈 앞에서도 맞는다(따옴표·블록 값의 "a\n").
    private static readonly Regex CaseNamePattern = new(@"^[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);
    private static readonly Regex IntegerPattern = new(@"^-?(0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant);
    private static readonly Regex NumberPattern = new(@"^-?(0|[1-9][0-9]*)(\.[0-9]+)?\z", RegexOptions.CultureInvariant);

    // 각 매핑에 쓸 수 있는 키. 4.3·4.4·4.5 표 순서다.
    // checklist·scores는 문장 결과 검증의 두 길이다(text-check.md 4.1, T-014).
    private static readonly string[] RootKeys =
        ["name", "task", "before", "after", "ai_usage", "steps", "inputs", "compare", "checklist", "scores", "thresholds"];

    // inferred는 AI가 추정한 답의 키 목록이다(스펙 4.13, ADR 0004 결정 2).
    // ai는 AI가 적은 단계 담당과 근거다(스펙 4.14, ADR 0013).
    private static readonly string[] StepKeys = ["name", "answers", "inferred", "ai"];
    private static readonly string[] AiKeys = ["assignee", "reason"];
    // group은 선택 키다(스펙 10.1, T-012).
    private static readonly string[] InputKeys = ["reference", "candidate", "key", "group"];
    private static readonly string[] CompareKeys = ["field", "tolerance", "tolerance_pct"];
    private static readonly string[] ThresholdKeys = ["accuracy_min", "missing_max", "time_must_drop", "score_gap_max"];
    private static readonly string[] ChecklistKeys = ["text", "items"];
    private static readonly string[] ChecklistItemKeys = ["item", "column", "any"];
    private static readonly string[] ScoresKeys = ["sheet", "sources", "min", "max"];
    private static readonly string[] AiUsageChoices = ["build-time", "runtime", "hybrid"];

    // 오류가 있어도 끝까지 걷는다. 필수 값이 없으면 null이다. 모델은 오류가 없을 때만 쓴다.
    public CaseDefinition? Read(YamlNode root)
    {
        if (root is not YamlMapping map)
        {
            TypeError(root, string.Empty, MustBeMapping);
            return null;
        }

        var fields = Fields(map, RootKeys, string.Empty);
        var name = RequiredString(fields, map, "name", "name");
        if (name is not null && !IsCaseName(name))
        {
            Add(fields["name"], ErrorCode.Format, "name", "소문자·숫자·'-'만, 64자까지 쓴다");
        }

        var task = RequiredString(fields, map, "task", "task");
        var before = OptionalString(fields, "before", "before");
        var after = OptionalString(fields, "after", "after");
        var aiUsage = TryGetPresent(fields, "ai_usage", out var aiUsageNode) ? Choice(aiUsageNode, "ai_usage", AiUsageChoices) : null;
        var steps = ReadSteps(fields, map);
        var inputs = ReadInputs(fields);
        var compare = ReadCompare(fields);
        CheckGroupConflict(inputs, compare);
        var checklist = ReadChecklist(fields, inputs, compare);
        var scores = ReadScores(fields);
        var thresholds = ReadThresholds(fields);
        return name is null || task is null
            ? null
            : new CaseDefinition(name, task, before, after, aiUsage, steps, inputs.Inputs, compare, thresholds, checklist, scores);
    }

    private IReadOnlyList<CaseStep> ReadSteps(Dictionary<string, YamlNode> fields, YamlMapping parent)
    {
        if (!fields.TryGetValue("steps", out var node))
        {
            Add(parent, ErrorCode.Required, "steps", MissingField);
            return [];
        }

        if (node is YamlScalar { IsNull: true })
        {
            Add(node, ErrorCode.Required, "steps", EmptyValue);
            return [];
        }

        if (node is not YamlSequence sequence)
        {
            TypeError(node, "steps", MustBeList);
            return [];
        }

        if (sequence.Items.Count == 0)
        {
            Add(sequence, ErrorCode.Required, "steps", "단계가 1개 이상 있어야 한다");
        }

        var steps = new List<CaseStep>();
        var firstNumbers = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < sequence.Items.Count; i++)
        {
            var number = i + 1;
            var path = YamlTree.ItemPath("steps", number);
            if (sequence.Items[i] is not YamlMapping item)
            {
                TypeError(sequence.Items[i], path, MustBeMapping);
                continue;
            }

            var stepFields = Fields(item, StepKeys, path);
            var namePath = YamlTree.ChildPath(path, "name");
            var name = RequiredString(stepFields, item, "name", namePath);

            // 단계 이름은 서수 비교로 중복 금지. 유니코드 정규화는 하지 않는다(스펙 4.4).
            if (name is not null && !firstNumbers.TryAdd(name, number))
            {
                var first = YamlTree.ItemPath("steps", firstNumbers[name]);
                Add(stepFields["name"], ErrorCode.Duplicate, namePath, $"단계 이름이 중복된다 (처음: {first})");
            }

            var answersPath = YamlTree.ChildPath(path, "answers");
            var answerFields = AnswerFields(stepFields, answersPath);
            var answers = ReadAnswers(answerFields, answersPath);
            var inferred = ReadInferred(stepFields, path, answerFields, answersPath);
            var ai = ReadAi(stepFields, path);
            steps.Add(new CaseStep(number, name ?? string.Empty, MarkInferred(answers, inferred), ai));
        }

        return steps;
    }

    // answers 매핑의 키. 없거나 null이면 빈 매핑이다. 매핑이 아니면 type 오류를 적고 빈 매핑.
    private Dictionary<string, YamlNode> AnswerFields(Dictionary<string, YamlNode> stepFields, string path)
    {
        if (!TryGetPresent(stepFields, "answers", out var node))
        {
            return new Dictionary<string, YamlNode>(StringComparer.Ordinal);
        }

        if (node is YamlMapping map)
        {
            return Fields(map, StepQuestions.Keys, path);
        }

        TypeError(node, path, MustBeMapping);
        return new Dictionary<string, YamlNode>(StringComparer.Ordinal);
    }

    // answers가 없거나 null이면 10개 모두 기본값이다(스펙 4.4).
    private StepAnswers ReadAnswers(Dictionary<string, YamlNode> answers, string path)
    {
        return new StepAnswers(
            EnumAnswer(answers, StepQuestions.InputKind, path),
            EnumAnswer(answers, StepQuestions.RuleExpressible, path),
            EnumAnswer(answers, StepQuestions.Frequency, path),
            IntegerAnswer(answers, StepQuestions.BatchSize, path),
            NumberAnswer(answers, StepQuestions.ExceptionRate, path),
            EnumAnswer(answers, StepQuestions.CurrentActor, path),
            MeasureAnswer(answers, StepQuestions.MinutesPerItem, path),
            EnumAnswer(answers, StepQuestions.FailureImpact, path),
            EnumAnswer(answers, StepQuestions.ExistingFeature, path),
            EnumAnswer(answers, StepQuestions.HumanSignoff, path));
    }

    // 스펙 4.13 inferred: AI가 추정한 답의 키 목록. 없거나 null이면 추정 답이 없다. 목록이 아니면 type.
    // 원소는 answers 표의 키다(서수 비교, 따옴표 상관없음). null 원소는 required, 키가 아니면 enum.
    // 같은 키가 둘이면 둘째부터 duplicate.
    // 적은 키는 answers에 값이 있어야 한다(값이 없는 답은 기본값이지 추정이 아니다).
    // 없거나 null이면 required(그 원소 위치, 경로는 그 답). 값이 있지만 잘못된 답은 그 답의 오류만 낸다.
    // 돌려주는 집합은 찾기에만 쓴다. 출력 순서는 4.4 표 순서다(StepClassifier가 정한다).
    private HashSet<string> ReadInferred(
        Dictionary<string, YamlNode> stepFields, string stepPath, Dictionary<string, YamlNode> answerFields, string answersPath)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (!TryGetPresent(stepFields, "inferred", out var node))
        {
            return keys;
        }

        var path = YamlTree.ChildPath(stepPath, "inferred");
        if (node is not YamlSequence sequence)
        {
            TypeError(node, path, MustBeList);
            return keys;
        }

        var firstNumbers = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < sequence.Items.Count; i++)
        {
            var item = sequence.Items[i];
            var itemPath = YamlTree.ItemPath(path, i + 1);
            if (item is YamlScalar { IsNull: true })
            {
                Add(item, ErrorCode.Required, itemPath, EmptyValue);
                continue;
            }

            var key = Choice(item, itemPath, StepQuestions.Keys);
            if (key is null)
            {
                continue;
            }

            if (!firstNumbers.TryAdd(key, i + 1))
            {
                var first = YamlTree.ItemPath(path, firstNumbers[key]);
                Add(item, ErrorCode.Duplicate, itemPath, $"추정 답 키가 중복된다 (처음: {first})");
                continue;
            }

            if (!TryGetPresent(answerFields, key, out _))
            {
                Add(item, ErrorCode.Required, YamlTree.ChildPath(answersPath, key), "inferred에 적은 답은 answers에 값이 있어야 한다");
                continue;
            }

            keys.Add(key);
        }

        return keys;
    }

    // 스펙 4.14 ai: AI가 적은 단계 담당과 근거. 없거나 null이면 AI 판단이 없다. 매핑이 아니면 type.
    // assignee·reason 둘 다 필수다. 다른 키는 unknown-field. assignee는 StepQuestions.AiAssignees 중 하나(서수), 아니면 enum.
    // reason은 저장만 한다. 앞뒤 공백을 지우지 않는다. 비면(null, "", 공백뿐) required.
    private StepAi? ReadAi(Dictionary<string, YamlNode> stepFields, string stepPath)
    {
        if (!TryGetPresent(stepFields, "ai", out var node))
        {
            return null;
        }

        var path = YamlTree.ChildPath(stepPath, "ai");
        if (node is not YamlMapping map)
        {
            TypeError(node, path, MustBeMapping);
            return null;
        }

        var fields = Fields(map, AiKeys, path);
        var assigneePath = YamlTree.ChildPath(path, "assignee");
        var assignee = RequiredString(fields, map, "assignee", assigneePath);
        if (assignee is not null && !StepQuestions.AiAssignees.Contains(assignee, StringComparer.Ordinal))
        {
            Add(fields["assignee"], ErrorCode.Enum, assigneePath, $"선택지 밖의 값 '{assignee}'. 가능한 값: {string.Join(", ", StepQuestions.AiAssignees)}");
            assignee = null;
        }

        var reason = RequiredString(fields, map, "reason", YamlTree.ChildPath(path, "reason"));
        return assignee is null || reason is null ? null : new StepAi(assignee, reason);
    }

    // inferred에 적은 답에 IsInferred를 단다. 값과 IsDefault는 그대로다.
    private static StepAnswers MarkInferred(StepAnswers a, HashSet<string> keys) =>
        keys.Count == 0
            ? a
            : a with
            {
                InputKind = Mark(a.InputKind, StepQuestions.InputKind.Key, keys),
                RuleExpressible = Mark(a.RuleExpressible, StepQuestions.RuleExpressible.Key, keys),
                Frequency = Mark(a.Frequency, StepQuestions.Frequency.Key, keys),
                BatchSize = Mark(a.BatchSize, StepQuestions.BatchSize.Key, keys),
                ExceptionRate = Mark(a.ExceptionRate, StepQuestions.ExceptionRate.Key, keys),
                CurrentActor = Mark(a.CurrentActor, StepQuestions.CurrentActor.Key, keys),
                MinutesPerItem = Mark(a.MinutesPerItem, StepQuestions.MinutesPerItem.Key, keys),
                FailureImpact = Mark(a.FailureImpact, StepQuestions.FailureImpact.Key, keys),
                ExistingFeature = Mark(a.ExistingFeature, StepQuestions.ExistingFeature.Key, keys),
                HumanSignoff = Mark(a.HumanSignoff, StepQuestions.HumanSignoff.Key, keys),
            };

    private static Answer<T> Mark<T>(Answer<T> answer, string key, HashSet<string> keys) =>
        keys.Contains(key) ? answer with { IsInferred = true } : answer;

    // 답이 없거나 null이면 기본값, IsDefault true. 값을 쓰면 IsDefault false.
    // 값이 잘못되면 오류를 적는다. 그때 모델은 버려지므로 Value는 쓰이지 않는다.
    private Answer<string> EnumAnswer(Dictionary<string, YamlNode> answers, EnumQuestion question, string parent)
    {
        if (!TryGetPresent(answers, question.Key, out var node))
        {
            return new Answer<string>(question.Default, IsDefault: true);
        }

        var value = Choice(node, YamlTree.ChildPath(parent, question.Key), question.Choices);
        return new Answer<string>(value ?? question.Default, IsDefault: false);
    }

    private Answer<int> IntegerAnswer(Dictionary<string, YamlNode> answers, IntegerQuestion question, string parent)
    {
        if (!TryGetPresent(answers, question.Key, out var node))
        {
            return new Answer<int>(question.Default, IsDefault: true);
        }

        var path = YamlTree.ChildPath(parent, question.Key);
        if (!PlainText(node, path, IntegerPattern, "정수가 아니다", out var text))
        {
            return new Answer<int>(question.Default, IsDefault: false);
        }

        // 모양은 맞지만 long에 안 들어가는 값도 범위 밖으로 본다.
        if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || value < question.Min
            || value > int.MaxValue)
        {
            Add(node, ErrorCode.Range, path, Invariant($"범위 밖이다 ({question.Min} 이상 {int.MaxValue} 이하): {text}"));
            return new Answer<int>(question.Default, IsDefault: false);
        }

        return new Answer<int>((int)value, IsDefault: false);
    }

    private Answer<decimal> NumberAnswer(Dictionary<string, YamlNode> answers, NumberQuestion question, string parent)
    {
        if (!TryGetPresent(answers, question.Key, out var node))
        {
            return new Answer<decimal>(question.Default, IsDefault: true);
        }

        var value = Number(node, YamlTree.ChildPath(parent, question.Key), question.Min, question.Max);
        return new Answer<decimal>(value ?? question.Default, IsDefault: false);
    }

    private Answer<decimal?> MeasureAnswer(Dictionary<string, YamlNode> answers, MeasureQuestion question, string parent)
    {
        if (!TryGetPresent(answers, question.Key, out var node))
        {
            return new Answer<decimal?>(null, IsDefault: true);
        }

        return new Answer<decimal?>(Number(node, YamlTree.ChildPath(parent, question.Key), question.Min, null), IsDefault: false);
    }

    private InputsRead ReadInputs(Dictionary<string, YamlNode> fields)
    {
        var none = new InputsRead(null, null, null, null);
        if (!TryGetPresent(fields, "inputs", out var node))
        {
            return none;
        }

        if (node is not YamlMapping map)
        {
            TypeError(node, "inputs", MustBeMapping);
            return none;
        }

        // 있으면 reference·candidate·key는 필수다. 없으면 표 순서로 inputs 매핑 위치에 적는다. group은 선택이다(스펙 10.1).
        var inputFields = Fields(map, InputKeys, "inputs");
        var reference = RequiredRelativePath(inputFields, map, "inputs", "reference");
        var candidate = RequiredRelativePath(inputFields, map, "inputs", "candidate");
        var key = RequiredString(inputFields, map, "key", "inputs.key");
        var (group, groupNode) = OptionalGroup(inputFields);
        var inputs = reference is null || candidate is null || key is null ? null : new CaseInputs(reference, candidate, key, group);
        return new InputsRead(inputs, key, group, groupNode);
    }

    // 스펙 10.2 inputs.group. 키 없음·null이면 적지 않은 것이다. 형식 규칙은 key와 같다:
    // 스칼라가 아니면 type, 비거나 공백뿐이면 required(위치는 그 값). 앞뒤 공백을 지우지 않는다.
    private (string? Group, YamlNode? Node) OptionalGroup(Dictionary<string, YamlNode> inputFields)
    {
        if (!TryGetPresent(inputFields, "group", out var node))
        {
            return (null, null);
        }

        var value = ScalarText(node, GroupPath);
        if (value is null)
        {
            return (null, null);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            Add(node, ErrorCode.Required, GroupPath, EmptyValue);
            return (null, null);
        }

        return (value, node);
    }

    // 스펙 10.2 conflict: group이 key 또는 compare 필드와 같으면(서수) 오류다. compare를 다 읽은 뒤 찾는다. 위치는 group 값.
    private void CheckGroupConflict(InputsRead inputs, IReadOnlyList<CompareField> compare)
    {
        if (inputs.Group is not { } group || inputs.GroupNode is not { } node)
        {
            return;
        }

        if (string.Equals(group, inputs.Key, StringComparison.Ordinal)
            || compare.Any(c => string.Equals(c.Field, group, StringComparison.Ordinal)))
        {
            Add(node, ErrorCode.Conflict, GroupPath, "group은 key·compare 필드와 다른 열을 쓴다");
        }
    }

    // 필수 경로 패턴(inputs.reference·candidate, scores.sheet·sources). 형식 규칙은 스펙 4.5, text-check.md 4.1.
    private string? RequiredRelativePath(Dictionary<string, YamlNode> fields, YamlMapping parent, string parentPath, string key)
    {
        var path = YamlTree.ChildPath(parentPath, key);
        var value = RequiredString(fields, parent, key, path);
        if (value is not null && !IsRelativePath(value))
        {
            Add(fields[key], ErrorCode.Format, path, RelativePathMessage);
        }

        return value;
    }

    // text-check.md 4.1 checklist. 매핑이 아니면 type. inputs가 없으면 required(위치는 checklist 값, 경로 inputs).
    // text는 필수이고 key·group·compare 필드와 같으면 conflict(위치는 text 값). items는 1개 이상.
    private CaseChecklist? ReadChecklist(Dictionary<string, YamlNode> fields, InputsRead inputs, IReadOnlyList<CompareField> compare)
    {
        if (!TryGetPresent(fields, "checklist", out var node))
        {
            return null;
        }

        if (node is not YamlMapping map)
        {
            TypeError(node, "checklist", MustBeMapping);
            return null;
        }

        if (!TryGetPresent(fields, "inputs", out _))
        {
            Add(node, ErrorCode.Required, "inputs", "checklist가 있으면 inputs가 있어야 한다");
        }

        var checklistFields = Fields(map, ChecklistKeys, "checklist");
        var text = RequiredString(checklistFields, map, "text", "checklist.text");
        if (text is not null
            && (string.Equals(text, inputs.Key, StringComparison.Ordinal)
                || string.Equals(text, inputs.Group, StringComparison.Ordinal)
                || compare.Any(c => string.Equals(c.Field, text, StringComparison.Ordinal))))
        {
            Add(checklistFields["text"], ErrorCode.Conflict, "checklist.text", "checklist.text는 key·compare 필드·group과 다른 열을 쓴다");
        }

        var items = ReadChecklistItems(checklistFields, map);
        return text is null || items is null ? null : new CaseChecklist(text, items);
    }

    // checklist.items. 항목마다 item(필수, 서수 중복 금지)과 column·any 가운데 꼭 하나.
    private List<ChecklistItem>? ReadChecklistItems(Dictionary<string, YamlNode> fields, YamlMapping parent)
    {
        const string path = "checklist.items";
        if (!fields.TryGetValue("items", out var node))
        {
            Add(parent, ErrorCode.Required, path, MissingField);
            return null;
        }

        if (node is YamlScalar { IsNull: true })
        {
            Add(node, ErrorCode.Required, path, EmptyValue);
            return null;
        }

        if (node is not YamlSequence sequence)
        {
            TypeError(node, path, MustBeList);
            return null;
        }

        if (sequence.Items.Count == 0)
        {
            Add(sequence, ErrorCode.Required, path, "항목이 1개 이상 있어야 한다");
        }

        var items = new List<ChecklistItem>();
        var firstNumbers = new Dictionary<string, int>(StringComparer.Ordinal);
        var valid = sequence.Items.Count > 0;
        for (var i = 0; i < sequence.Items.Count; i++)
        {
            var number = i + 1;
            var itemPath = YamlTree.ItemPath(path, number);
            if (sequence.Items[i] is not YamlMapping item)
            {
                TypeError(sequence.Items[i], itemPath, MustBeMapping);
                valid = false;
                continue;
            }

            var itemFields = Fields(item, ChecklistItemKeys, itemPath);
            var namePath = YamlTree.ChildPath(itemPath, "item");
            var name = RequiredString(itemFields, item, "item", namePath);
            if (name is not null && !firstNumbers.TryAdd(name, number))
            {
                var first = YamlTree.ItemPath(path, firstNumbers[name]);
                Add(itemFields["item"], ErrorCode.Duplicate, namePath, $"체크리스트 항목이 중복된다 (처음: {first})");
            }

            var hasColumn = TryGetPresent(itemFields, "column", out _);
            var hasAny = TryGetPresent(itemFields, "any", out var anyNode);
            if (hasColumn && hasAny)
            {
                Add(item, ErrorCode.Conflict, itemPath, "column과 any 중 하나만 쓴다");
            }
            else if (!hasColumn && !hasAny)
            {
                Add(item, ErrorCode.Required, itemPath, "column 또는 any가 있어야 한다");
            }

            var column = hasColumn ? RequiredString(itemFields, item, "column", YamlTree.ChildPath(itemPath, "column")) : null;
            var phrases = hasAny ? ReadPhrases(anyNode!, YamlTree.ChildPath(itemPath, "any")) : null;
            if (name is null || hasColumn == hasAny || (hasColumn && column is null) || (hasAny && phrases is null))
            {
                valid = false;
                continue;
            }

            items.Add(new ChecklistItem(name, column, phrases ?? []));
        }

        return valid ? items : null;
    }

    // any 문구 목록. 목록이 아니면 type, 비면 required. 문구는 비지 않은 문자열이다(required). 쓴 글 그대로 둔다.
    private List<string>? ReadPhrases(YamlNode node, string path)
    {
        if (node is not YamlSequence sequence)
        {
            TypeError(node, path, MustBeList);
            return null;
        }

        if (sequence.Items.Count == 0)
        {
            Add(sequence, ErrorCode.Required, path, "문구가 1개 이상 있어야 한다");
            return null;
        }

        var phrases = new List<string>();
        var valid = true;
        for (var i = 0; i < sequence.Items.Count; i++)
        {
            var phrase = sequence.Items[i];
            var phrasePath = YamlTree.ItemPath(path, i + 1);
            var value = ScalarText(phrase, phrasePath);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (phrase is YamlScalar)
                {
                    Add(phrase, ErrorCode.Required, phrasePath, EmptyValue);
                }

                valid = false;
                continue;
            }

            phrases.Add(value);
        }

        return valid ? phrases : null;
    }

    // text-check.md 4.1 scores. sheet·sources는 inputs 경로와 같은 규칙. min·max는 0 이상 100 이하의 정수, min < max(아니면 max 값에 range).
    private CaseScores? ReadScores(Dictionary<string, YamlNode> fields)
    {
        if (!TryGetPresent(fields, "scores", out var node))
        {
            return null;
        }

        if (node is not YamlMapping map)
        {
            TypeError(node, "scores", MustBeMapping);
            return null;
        }

        var scoreFields = Fields(map, ScoresKeys, "scores");
        var sheet = RequiredRelativePath(scoreFields, map, "scores", "sheet");
        var sources = RequiredRelativePath(scoreFields, map, "scores", "sources");
        var min = ScoreBound(scoreFields, map, "min");
        var max = ScoreBound(scoreFields, map, "max");
        if (min is { } low && max is { } high && low >= high)
        {
            Add(scoreFields["max"], ErrorCode.Range, "scores.max", Invariant($"범위 밖이다 (min보다 커야 한다): {high}"));
            return null;
        }

        return sheet is null || sources is null || min is null || max is null ? null : new CaseScores(sheet, sources, min.Value, max.Value);
    }

    // scores.min·max 하나. 키가 없으면 부모 매핑 위치에, null이면 그 값 위치에 required.
    private int? ScoreBound(Dictionary<string, YamlNode> fields, YamlMapping parent, string key)
    {
        var path = YamlTree.ChildPath("scores", key);
        if (!fields.TryGetValue(key, out var node))
        {
            Add(parent, ErrorCode.Required, path, MissingField);
            return null;
        }

        if (node is YamlScalar { IsNull: true })
        {
            Add(node, ErrorCode.Required, path, EmptyValue);
            return null;
        }

        if (!PlainText(node, path, IntegerPattern, "정수가 아니다", out var text))
        {
            return null;
        }

        // 음의 0(-0)은 정수 패턴에 맞지만 long으로 읽으면 0이 된다. 스펙 4.6 정수 행대로 범위 밖으로 본다.
        if (text == "-0"
            || !long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || value < ScoreBoundMin
            || value > ScoreBoundMax)
        {
            Add(node, ErrorCode.Range, path, Invariant($"범위 밖이다 ({ScoreBoundMin} 이상 {ScoreBoundMax} 이하): {text}"));
            return null;
        }

        return (int)value;
    }

    private IReadOnlyList<CompareField> ReadCompare(Dictionary<string, YamlNode> fields)
    {
        if (!TryGetPresent(fields, "compare", out var node))
        {
            return [];
        }

        if (node is not YamlSequence sequence)
        {
            TypeError(node, "compare", MustBeList);
            return [];
        }

        // 항목이 있는데 inputs가 없으면(키 없음·null) required. 위치는 compare 값(스펙 4.5).
        if (sequence.Items.Count > 0 && !TryGetPresent(fields, "inputs", out _))
        {
            Add(sequence, ErrorCode.Required, "inputs", "compare가 있으면 inputs가 있어야 한다");
        }

        var result = new List<CompareField>();
        var firstNumbers = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < sequence.Items.Count; i++)
        {
            var number = i + 1;
            var path = YamlTree.ItemPath("compare", number);
            if (sequence.Items[i] is not YamlMapping item)
            {
                TypeError(sequence.Items[i], path, MustBeMapping);
                continue;
            }

            var itemFields = Fields(item, CompareKeys, path);
            var fieldPath = YamlTree.ChildPath(path, "field");
            var field = RequiredString(itemFields, item, "field", fieldPath);
            if (field is not null && !firstNumbers.TryAdd(field, number))
            {
                var first = YamlTree.ItemPath("compare", firstNumbers[field]);
                Add(itemFields["field"], ErrorCode.Duplicate, fieldPath, $"비교 필드가 중복된다 (처음: {first})");
            }

            var tolerance = OptionalNumber(itemFields, "tolerance", YamlTree.ChildPath(path, "tolerance"), ToleranceMin, null);
            var tolerancePct = OptionalNumber(itemFields, "tolerance_pct", YamlTree.ChildPath(path, "tolerance_pct"), ToleranceMin, null);

            // 둘 다 쓰면 conflict. 위치는 그 항목 시작.
            if (TryGetPresent(itemFields, "tolerance", out _) && TryGetPresent(itemFields, "tolerance_pct", out _))
            {
                Add(item, ErrorCode.Conflict, path, "tolerance와 tolerance_pct 중 하나만 쓴다");
            }

            result.Add(new CompareField(field ?? string.Empty, tolerance, tolerancePct));
        }

        return result;
    }

    // 읽기만 한다. 없으면 세 값 모두 null이다(스펙 4.5, 승인 결정 11).
    private CaseThresholds ReadThresholds(Dictionary<string, YamlNode> fields)
    {
        if (!TryGetPresent(fields, "thresholds", out var node))
        {
            return new CaseThresholds(null, null, null);
        }

        if (node is not YamlMapping map)
        {
            TypeError(node, "thresholds", MustBeMapping);
            return new CaseThresholds(null, null, null);
        }

        // score_gap_max는 scores가 있을 때만 쓴다. 없으면 required(위치는 그 값, 경로 scores. text-check.md 4.1).
        var thresholdFields = Fields(map, ThresholdKeys, "thresholds");
        if (TryGetPresent(thresholdFields, "score_gap_max", out var gapNode) && !TryGetPresent(fields, "scores", out _))
        {
            Add(gapNode, ErrorCode.Required, "scores", "score_gap_max가 있으면 scores가 있어야 한다");
        }

        return new CaseThresholds(
            OptionalNumber(thresholdFields, "accuracy_min", "thresholds.accuracy_min", PercentMin, PercentMax),
            OptionalNumber(thresholdFields, "missing_max", "thresholds.missing_max", PercentMin, PercentMax),
            OptionalBoolean(thresholdFields, "time_must_drop", "thresholds.time_must_drop"),
            OptionalNumber(thresholdFields, "score_gap_max", "thresholds.score_gap_max", PercentMin, PercentMax));
    }

    // 매핑의 키를 이름으로 모은다. 목록에 없는 키는 unknown-field(그 키 위치). 키 비교는 서수다.
    private Dictionary<string, YamlNode> Fields(YamlMapping map, string[] allowed, string path)
    {
        var fields = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
        foreach (var entry in map.Entries)
        {
            if (allowed.Contains(entry.Key.Value, StringComparer.Ordinal))
            {
                fields.Add(entry.Key.Value, entry.Value);
            }
            else
            {
                Add(entry.Key, ErrorCode.UnknownField, YamlTree.ChildPath(path, entry.Key.Value), "알 수 없는 필드다");
            }
        }

        return fields;
    }

    // 필수 문자열. 키가 없으면 부모 매핑 위치에, 값이 비면(null·""·공백뿐) 그 값 위치에 required.
    // 자유 서술은 저장만 한다. 앞뒤 공백을 지우지 않는다(스펙 4.3).
    private string? RequiredString(Dictionary<string, YamlNode> fields, YamlMapping parent, string key, string path)
    {
        if (!fields.TryGetValue(key, out var node))
        {
            Add(parent, ErrorCode.Required, path, MissingField);
            return null;
        }

        var value = ScalarText(node, path);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (node is YamlScalar)
            {
                Add(node, ErrorCode.Required, path, EmptyValue);
            }

            return null;
        }

        return value;
    }

    private string? OptionalString(Dictionary<string, YamlNode> fields, string key, string path) =>
        TryGetPresent(fields, key, out var node) ? ScalarText(node, path) : null;

    // 스칼라의 문자열. null 스칼라면 null. 모음이면 type 오류를 적고 null.
    private string? ScalarText(YamlNode node, string path)
    {
        if (node is YamlScalar scalar)
        {
            return scalar.IsNull ? null : scalar.Value;
        }

        TypeError(node, path, MustBeScalar);
        return null;
    }

    // 문자열이 선택지와 서수로 같은가(대소문자 구분). 따옴표는 상관없다. 아니면 enum 오류.
    private string? Choice(YamlNode node, string path, string[] choices)
    {
        var value = ScalarText(node, path);
        if (value is null || choices.Contains(value, StringComparer.Ordinal))
        {
            return value;
        }

        Add(node, ErrorCode.Enum, path, $"선택지 밖의 값 '{value}'. 가능한 값: {string.Join(", ", choices)}");
        return null;
    }

    private decimal? OptionalNumber(Dictionary<string, YamlNode> fields, string key, string path, decimal min, decimal? max) =>
        TryGetPresent(fields, key, out var node) ? Number(node, path, min, max) : null;

    // decimal로 읽는다. 소수 자릿수를 보존한다(1.50은 1.50). 범위는 min 이상, max가 있으면 max 이하.
    private decimal? Number(YamlNode node, string path, decimal min, decimal? max)
    {
        if (!PlainText(node, path, NumberPattern, "숫자가 아니다", out var text))
        {
            return null;
        }

        if (decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            && value >= min
            && (max is null || value <= max))
        {
            return value;
        }

        var range = max is null ? Invariant($"{min} 이상") : Invariant($"{min} 이상 {max} 이하");
        Add(node, ErrorCode.Range, path, $"범위 밖이다 ({range}): {text}");
        return null;
    }

    // 불리언은 따옴표 없는 true·false만 받는다(스펙 4.6).
    private bool? OptionalBoolean(Dictionary<string, YamlNode> fields, string key, string path)
    {
        if (!TryGetPresent(fields, key, out var node))
        {
            return null;
        }

        if (node is not YamlScalar scalar)
        {
            TypeError(node, path, MustBeScalar);
            return null;
        }

        if (scalar is { Plain: true, Value: "true" })
        {
            return true;
        }

        if (scalar is { Plain: true, Value: "false" })
        {
            return false;
        }

        Add(node, ErrorCode.Format, path, $"true 또는 false로 쓴다: {scalar.Value}");
        return null;
    }

    // 따옴표 없는 스칼라이고 모양이 맞는가. 아니면 type 또는 format 오류. 따옴표가 있으면 고칠 방법을 덧붙인다.
    private bool PlainText(YamlNode node, string path, Regex pattern, string notMessage, out string text)
    {
        text = string.Empty;
        if (node is not YamlScalar scalar)
        {
            TypeError(node, path, MustBeScalar);
            return false;
        }

        if (scalar.Plain && pattern.IsMatch(scalar.Value))
        {
            text = scalar.Value;
            return true;
        }

        var hint = scalar.Plain ? string.Empty : " (따옴표 없이 쓴다)";
        Add(node, ErrorCode.Format, path, $"{notMessage}: {scalar.Value}{hint}");
        return false;
    }

    // 별칭 자리는 이미 오류로 적었으므로 type 오류를 더하지 않는다.
    private void TypeError(YamlNode node, string path, string message)
    {
        if (node is not YamlAlias)
        {
            Add(node, ErrorCode.Type, path, message);
        }
    }

    private void Add(YamlNode node, string code, string path, string message) =>
        errors.Add(node.Line, node.Column, code, path, message);

    // 키가 있고 값이 null이 아닌가. 키 없음과 null은 같은 뜻이다(스펙 4.4).
    private static bool TryGetPresent(Dictionary<string, YamlNode> fields, string key, [NotNullWhen(true)] out YamlNode? node) =>
        fields.TryGetValue(key, out node) && node is not YamlScalar { IsNull: true };

    private static bool IsCaseName(string name) => name.Length <= MaxNameLength && CaseNamePattern.IsMatch(name);

    // 케이스 폴더 기준 상대 경로: '/'로 시작하지 않고 '\'·':'·'..' 조각이 없다(스펙 4.5).
    private static bool IsRelativePath(string value) =>
        !value.StartsWith('/')
        && !value.Contains('\\')
        && !value.Contains(':')
        && !value.Split('/').Contains("..", StringComparer.Ordinal);

    // inputs를 읽은 결과. Inputs는 필수 키가 모두 있을 때만 있다.
    // Key·Group·GroupNode는 conflict 검사(스펙 10.2)에 쓴다. 다른 키에 오류가 있어도 검사한다.
    private sealed record InputsRead(CaseInputs? Inputs, string? Key, string? Group, YamlNode? GroupNode);
}
