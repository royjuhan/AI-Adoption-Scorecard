#!/usr/bin/env python3
# T-010 스파이크(머지 대상 아님). clone의 Core에 안 M-A(case.yaml inputs.group)를 넣는다. judgment.md 4.8.
# 1) CaseModel.cs: CaseInputs에 선택 위치 매개변수 Group(기본 null). 기존 3인자 생성은 그대로 컴파일된다.
# 2) CaseReader.cs: inputs 키 목록에 group. 있으면 비어 있지 않은 문자열(빈 값은 required). 없거나 null이면 null.
# 3) CompareEngine.cs: group이 있으면 기준 파일 머리 행에 그 열이 있어야 한다(column·duplicate-column, metrics.md 4.7과 같은 규칙).
# 사용: apply-group.py <clone 루트>
import sys
from pathlib import Path

root = Path(sys.argv[1])


def patch(rel, pairs):
    p = root / rel
    s = p.read_text(encoding='utf-8')
    for old, new in pairs:
        if s.count(old) != 1:
            sys.exit(f'PATCH-FAIL {rel}: {old[:60]!r} {s.count(old)}번')
        s = s.replace(old, new)
    p.write_text(s, encoding='utf-8')
    print(f'PATCH {rel} {len(pairs)}')


patch('src/Scorecard.Core/Cases/CaseModel.cs', [(
    'public sealed record CaseInputs(string Reference, string Candidate, string Key);',
    '// Group(T-010 후보): 불일치 몰림을 볼 유형 열 이름(선택). 없으면 null.\n'
    'public sealed record CaseInputs(string Reference, string Candidate, string Key, string? Group = null);',
)])

patch('src/Scorecard.Core/Cases/CaseReader.cs', [
    ('private static readonly string[] InputKeys = ["reference", "candidate", "key"];',
     'private static readonly string[] InputKeys = ["reference", "candidate", "key", "group"];'),
    ('        var key = RequiredString(inputFields, map, "key", "inputs.key");\n'
     '        return reference is null || candidate is null || key is null ? null : new CaseInputs(reference, candidate, key);',
     '        var key = RequiredString(inputFields, map, "key", "inputs.key");\n'
     '        var group = OptionalName(inputFields, "group", "inputs.group");\n'
     '        return reference is null || candidate is null || key is null ? null : new CaseInputs(reference, candidate, key, group);'),
    ('    private string? OptionalString(Dictionary<string, YamlNode> fields, string key, string path) =>',
     '    // 선택 이름(T-010 후보). 없거나 null이면 null. 있으면 비어 있지 않아야 한다(공백뿐도 required).\n'
     '    private string? OptionalName(Dictionary<string, YamlNode> fields, string key, string path)\n'
     '    {\n'
     '        if (!TryGetPresent(fields, key, out var node))\n'
     '        {\n'
     '            return null;\n'
     '        }\n'
     '\n'
     '        var value = ScalarText(node, path);\n'
     '        if (value is not null && string.IsNullOrWhiteSpace(value))\n'
     '        {\n'
     '            Add(node, ErrorCode.Required, path, EmptyValue);\n'
     '            return null;\n'
     '        }\n'
     '\n'
     '        return value;\n'
     '    }\n'
     '\n'
     '    private string? OptionalString(Dictionary<string, YamlNode> fields, string key, string path) =>'),
])

patch('src/Scorecard.Core/Comparison/CompareEngine.cs', [
    ('var reference = ReadSide(0, referenceFiles, inputs.Key, definition.Compare, checkNumbers: true, errors);',
     'var reference = ReadSide(0, referenceFiles, inputs.Key, definition.Compare, checkNumbers: true, errors, inputs.Group);'),
    ('private static List<Row> ReadSide(int side, SourceFile[] files, string key, IReadOnlyList<CompareField> compare, bool checkNumbers, ErrorList errors)',
     'private static List<Row> ReadSide(int side, SourceFile[] files, string key, IReadOnlyList<CompareField> compare, bool checkNumbers, ErrorList errors, string? group = null)'),
    ('            var fieldIndex = compare.Select(c => ColumnIndex(file, csv.Header, header, c.Field, side, f, errors)).ToArray();\n'
     '            if (keyIndex < 0 || fieldIndex.Any(i => i < 0))',
     '            var fieldIndex = compare.Select(c => ColumnIndex(file, csv.Header, header, c.Field, side, f, errors)).ToArray();\n'
     '            var groupIndex = group is null ? 0 : ColumnIndex(file, csv.Header, header, group, side, f, errors);\n'
     '            if (keyIndex < 0 || fieldIndex.Any(i => i < 0) || groupIndex < 0)'),
])
