#!/usr/bin/env python3
# T-011 스파이크(머지 대상 아님). 예제 폴더 파일 목록 테스트에 expected/judgment.json을 더한다(계획에 새 파일을 더하는 변경).
# 사용: tests-variant.py <clone 루트>
import sys
from pathlib import Path

p = Path(sys.argv[1]) / 'tests/Scorecard.Tests/Synthetic/ExampleFileTests.cs'
s = p.read_text(encoding='utf-8')
old = '.Append("expected/metrics.json").Order(StringComparer.Ordinal);'
if s.count(old) != 1:
    sys.exit('PATCH-FAIL ExampleFileTests.cs')
p.write_text(s.replace(old, '.Append("expected/metrics.json").Append("expected/judgment.json").Order(StringComparer.Ordinal);'), encoding='utf-8')
print('PATCH ExampleFileTests.cs expected/judgment.json')
