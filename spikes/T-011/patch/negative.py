#!/usr/bin/env python3
# T-011 스파이크 음성 대조(머지 대상 아님). clone의 후보 Core를 일부러 틀리게 바꾼다.
#   culture: 문장 숫자를 CurrentCulture로 쓴다 → G1은 통과, G2 R4(문화권)가 잡아야 한다.
#   clock: 판정 안에서 DateTime.Now를 읽는다 → G1(D1)이 잡아야 한다.
# 사용: negative.py <clone 루트> culture|clock
import sys
from pathlib import Path

root, kind = Path(sys.argv[1]), sys.argv[2]
if kind == 'culture':
    p = root / 'src/Scorecard.Core/Judgment/JudgmentRules.cs'
    old = 'private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);'
    new = 'private static string Inv(FormattableString text) => text.ToString(CultureInfo.CurrentCulture);'
else:
    p = root / 'src/Scorecard.Core/Judgment/JudgmentEngine.cs'
    old = '        ArgumentNullException.ThrowIfNull(definition);\n        ArgumentNullException.ThrowIfNull(metrics);\n'
    new = old + '        _ = DateTime.Now.Ticks;\n'
s = p.read_text(encoding='utf-8')
if s.count(old) != 1:
    sys.exit(f'PATCH-FAIL {p.name}')
p.write_text(s.replace(old, new), encoding='utf-8')
print(f'NEGATIVE {kind} {p.name}')
