#!/usr/bin/env python3
# T-011 스파이크(머지 대상 아님). clone의 GuardTargets에 판정 대상 judge-case-valid를 더한다(judgment.md 4.12).
# 샘플은 기존 case-valid다. 새 샘플을 더하지 않으므로 샘플 목록(중복 뺀 것)은 그대로다.
# 사용: guard-variant.py <clone 루트>
import sys
from pathlib import Path

p = Path(sys.argv[1]) / 'tests/Scorecard.Tests/Guards/GuardTargets.cs'
s = p.read_text(encoding='utf-8')
pairs = [
    ('using Scorecard.Core.Comparison;\n', 'using Scorecard.Core.Comparison;\nusing Scorecard.Core.Judgment;\n'),
    ('        new("compare-invalid", "compare-invalid", CompareToFile),\n',
     '        new("compare-invalid", "compare-invalid", CompareToFile),\n        new("judge-case-valid", "case-valid", JudgeToFile),\n'),
    ('    // 오류 줄(CaseError.ToString) + LF. UTF-8(BOM 없음).\n',
     '    // T-011 판정(judgment.md 4.12): case-valid를 대조하고 판정한다. 시간·사람 입력은 없다(null).\n'
     '    private static void JudgeToFile(string input, string output)\n'
     '    {\n'
     '        var definition = CaseParser.ParseDirectory(input).Case ?? throw new InvalidOperationException("판정 대상 표본이 파싱되지 않는다.");\n'
     '        var metrics = CompareEngine.CompareDirectory(input, definition).Metrics ?? throw new InvalidOperationException("판정 대상 표본이 대조되지 않는다.");\n'
     '        Directory.CreateDirectory(output);\n'
     '        File.WriteAllBytes(Path.Combine(output, JudgmentJson.FileName), JudgmentJson.Render(JudgmentEngine.Judge(definition, metrics)));\n'
     '    }\n\n'
     '    // 오류 줄(CaseError.ToString) + LF. UTF-8(BOM 없음).\n'),
]
for old, new in pairs:
    if s.count(old) != 1:
        sys.exit(f'PATCH-FAIL GuardTargets.cs: {old[:50]!r}')
    s = s.replace(old, new)
p.write_text(s, encoding='utf-8')
print('PATCH GuardTargets.cs judge-case-valid')
