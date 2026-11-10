using System.Globalization;

namespace Scorecard.Core.Cases;

// 4.16 역할 배분 선택 항목(T-024, ADR 0018). 스킬이 쓰고 Core는 형식만 보고 옮긴다. 분류·대조·판정에 쓰지 않는다.
// 단계 ai: roles(인간·AI·소프트웨어), judgment, execution, accountability, basis, varies.
// 최상위: purpose, done_when, facts, assumptions, tools, transitions.
internal sealed partial class CaseReader
{
    private static readonly string[] RoleKeys = ["human", "ai", "software"];
    private static readonly string[] AllocationTextKeys = ["judgment", "execution", "accountability", "basis", "varies"];
    private static readonly string[] CaseAllocationKeys = ["purpose", "done_when", "facts", "assumptions", "tools", "transitions"];

    private static readonly string[] ToolKeys =
        ["name", "steps", "function", "input", "process", "output", "reduces", "exceptions", "existing", "new", "burden"];

    private static readonly string[] TransitionKeys = ["step", "from", "to", "condition", "evidence"];

    // 단계 ai의 역할 배분. 여섯 키 중 하나도 없으면 null. 문장은 저장만 한다. 비면 required(roles 안은 예외).
    private StepAllocation? ReadStepAllocation(Dictionary<string, YamlNode> fields, string aiPath)
    {
        var roles = ReadRoles(fields, aiPath);
        var texts = AllocationTextKeys.Select(k => OptionalText(fields, k, YamlTree.ChildPath(aiPath, k))).ToArray();
        var present = TryGetPresent(fields, "roles", out _) || AllocationTextKeys.Any(k => TryGetPresent(fields, k, out _));
        return present ? new StepAllocation(roles, texts[0], texts[1], texts[2], texts[3], texts[4]) : null;
    }

    // roles: 매핑 {human, ai, software}. 값이 없거나 null이거나 빈 글이면 그 역할은 없다(세 역할을 억지로 채우지 않는다).
    // 세 역할이 모두 없으면 required.
    private StepRoles? ReadRoles(Dictionary<string, YamlNode> fields, string aiPath)
    {
        if (!TryGetPresent(fields, "roles", out var node))
        {
            return null;
        }

        var path = YamlTree.ChildPath(aiPath, "roles");
        if (node is not YamlMapping map)
        {
            TypeError(node, path, MustBeMapping);
            return null;
        }

        var f = Fields(map, RoleKeys, path);
        var values = RoleKeys.Select(k => TryGetPresent(f, k, out var v) ? ScalarText(v, YamlTree.ChildPath(path, k)) : null)
            .Select(v => string.IsNullOrWhiteSpace(v) ? null : v)
            .ToArray();
        if (values.All(v => v is null))
        {
            Add(map, ErrorCode.Required, path, "역할이 1개 이상 있어야 한다");
            return null;
        }

        return new StepRoles(values[0], values[1], values[2]);
    }

    // 최상위 역할 배분 메모. 여섯 키 중 하나도 없으면 null.
    private CaseAllocation? ReadCaseAllocation(Dictionary<string, YamlNode> fields, int stepCount)
    {
        var purpose = OptionalText(fields, "purpose", "purpose");
        var doneWhen = OptionalText(fields, "done_when", "done_when");
        var facts = TextList(fields, "facts");
        var assumptions = TextList(fields, "assumptions");
        var tools = ReadTools(fields, stepCount);
        var transitions = ReadTransitions(fields, stepCount);
        var present = CaseAllocationKeys.Any(k => TryGetPresent(fields, k, out _));
        return present ? new CaseAllocation(purpose, doneWhen, facts, assumptions, tools, transitions) : null;
    }

    // tools: 목록. 원소는 매핑이고 열한 키가 모두 필수다. steps는 있는 단계 번호 목록(1개 이상, 같은 번호 두 번 없음).
    private List<RoleTool> ReadTools(Dictionary<string, YamlNode> fields, int stepCount)
    {
        var tools = new List<RoleTool>();
        foreach (var (item, itemPath) in MappingItems(fields, "tools"))
        {
            var f = Fields(item, ToolKeys, itemPath);
            var steps = ReadStepNumbers(f, item, itemPath, stepCount);
            var texts = ToolKeys.Where(k => k != "steps").Select(k => RequiredString(f, item, k, YamlTree.ChildPath(itemPath, k))).ToArray();
            if (steps is not null && texts.All(t => t is not null))
            {
                tools.Add(new RoleTool(texts[0]!, steps, texts[1]!, texts[2]!, texts[3]!, texts[4]!, texts[5]!, texts[6]!, texts[7]!, texts[8]!, texts[9]!));
            }
        }

        return tools;
    }

    // transitions: 목록. 원소는 매핑이고 다섯 키가 모두 필수다. from·to는 담당 다섯 가운데 하나이고 서로 다르다.
    private List<RoleTransition> ReadTransitions(Dictionary<string, YamlNode> fields, int stepCount)
    {
        var transitions = new List<RoleTransition>();
        foreach (var (item, itemPath) in MappingItems(fields, "transitions"))
        {
            var f = Fields(item, TransitionKeys, itemPath);
            var step = RequiredStepNumber(f, item, "step", YamlTree.ChildPath(itemPath, "step"), stepCount);
            var from = RequiredChoice(f, item, "from", itemPath, StepQuestions.AiAssignees);
            var to = RequiredChoice(f, item, "to", itemPath, StepQuestions.AiAssignees);
            var condition = RequiredString(f, item, "condition", YamlTree.ChildPath(itemPath, "condition"));
            var evidence = RequiredString(f, item, "evidence", YamlTree.ChildPath(itemPath, "evidence"));
            if (from is not null && to is not null && string.Equals(from, to, StringComparison.Ordinal))
            {
                Add(f["to"], ErrorCode.Conflict, YamlTree.ChildPath(itemPath, "to"), "from과 to가 같다");
                continue;
            }

            if (step is { } number && from is not null && to is not null && condition is not null && evidence is not null)
            {
                transitions.Add(new RoleTransition(number, from, to, condition, evidence));
            }
        }

        return transitions;
    }

    // 매핑 원소 목록. 목록이 아니면 type, 빈 목록이면 required, 원소가 매핑이 아니면 type(그 원소는 건너뛴다).
    private List<(YamlMapping Item, string Path)> MappingItems(Dictionary<string, YamlNode> fields, string key)
    {
        var items = new List<(YamlMapping, string)>();
        if (!TryGetPresent(fields, key, out var node))
        {
            return items;
        }

        if (node is not YamlSequence sequence)
        {
            TypeError(node, key, MustBeList);
            return items;
        }

        if (sequence.Items.Count == 0)
        {
            Add(sequence, ErrorCode.Required, key, "항목이 1개 이상 있어야 한다");
        }

        for (var i = 0; i < sequence.Items.Count; i++)
        {
            var itemPath = YamlTree.ItemPath(key, i + 1);
            if (sequence.Items[i] is YamlMapping item)
            {
                items.Add((item, itemPath));
            }
            else
            {
                TypeError(sequence.Items[i], itemPath, MustBeMapping);
            }
        }

        return items;
    }

    // 도구의 steps: 단계 번호 목록. 하나라도 틀리면 null.
    private List<int>? ReadStepNumbers(Dictionary<string, YamlNode> fields, YamlMapping parent, string parentPath, int stepCount)
    {
        var path = YamlTree.ChildPath(parentPath, "steps");
        if (!fields.TryGetValue("steps", out var node))
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
            Add(sequence, ErrorCode.Required, path, "단계가 1개 이상 있어야 한다");
            return null;
        }

        var numbers = new List<int>();
        var valid = true;
        for (var i = 0; i < sequence.Items.Count; i++)
        {
            var itemPath = YamlTree.ItemPath(path, i + 1);
            var number = StepNumber(sequence.Items[i], itemPath, stepCount);
            if (number is null)
            {
                valid = false;
                continue;
            }

            if (numbers.Contains(number.Value))
            {
                Add(sequence.Items[i], ErrorCode.Duplicate, itemPath, string.Create(CultureInfo.InvariantCulture, $"같은 단계가 두 번 나온다: {number.Value}"));
                valid = false;
                continue;
            }

            numbers.Add(number.Value);
        }

        return valid ? numbers : null;
    }

    private int? RequiredStepNumber(Dictionary<string, YamlNode> fields, YamlMapping parent, string key, string path, int stepCount)
    {
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

        return StepNumber(node, path, stepCount);
    }

    // 단계 번호: 따옴표 없는 정수, 1 이상 단계 수 이하. 아니면 format 또는 range.
    private int? StepNumber(YamlNode node, string path, int stepCount)
    {
        if (!PlainText(node, path, IntegerPattern, "정수가 아니다", out var text))
        {
            return null;
        }

        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) || value < 1 || value > stepCount)
        {
            Add(node, ErrorCode.Range, path, "없는 단계다: " + text);
            return null;
        }

        return value;
    }
}
