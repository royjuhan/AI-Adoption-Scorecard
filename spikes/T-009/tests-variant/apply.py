#!/usr/bin/env python3
# T-009 스파이크(머지 대상 아님). 예제 목록을 고정한 기존 테스트를 clone에서만 고친다(signoff-final.md 5절 표의 V1~V6).
# 실제 변경은 juhan 승인 뒤 test-engineer가 한다. 저장소의 tests/는 바꾸지 않는다.
# 사용: apply.py <저장소> [--no-thresholds | --only-thresholds]
#   --no-thresholds: V6을 빼고 넣는다(V6 없이 무엇이 깨지는지 보려고). --only-thresholds: 그 뒤에 V6만 넣는다.
import sys
from pathlib import Path

T = 'tests/Scorecard.Tests/'
H = '### 7.5 signoff-final'
CHANGES = [
    ('V1', T + 'Classification/ClassificationInput.cs',
     '    // 공개 예제 3종(지시문 12절, 스펙 7절).\n    public static readonly string[] Examples = ["table-compare", "form-fill", "mixed-exceptions"];\n',
     '    // 공개 예제 4종(지시문 12절, 스펙 7절). signoff-final은 T-009(ADR 0003 남은 질문 2).\n    public static readonly string[] Examples = ["table-compare", "form-fill", "mixed-exceptions", "signoff-final"];\n'),
    ('V2', T + 'Classification/ExampleGoldenTests.cs',
     '        ["mixed-exceptions"] = "### 7.4 mixed-exceptions",\n    };\n',
     '        ["mixed-exceptions"] = "### 7.4 mixed-exceptions",\n        ["signoff-final"] = "' + H + '",\n    };\n'),
    ('V3', T + 'Synthetic/SyntheticInput.cs',
     '    public static readonly string[] DataExamples = ["table-compare", "form-fill"];\n',
     '    public static readonly string[] DataExamples = ["table-compare", "form-fill", "signoff-final"];\n'),
    ('V4', T + 'Synthetic/SyntheticSpec.cs',
     '        ["mixed-exceptions"] = "### 7.4 mixed-exceptions",\n    };\n',
     '        ["mixed-exceptions"] = "### 7.4 mixed-exceptions",\n        ["signoff-final"] = "' + H + '",\n    };\n'),
    ('V5', T + 'Comparison/MetricsSpec.cs',
     '        ["mixed-exceptions"] = "### 7.4 mixed-exceptions",\n    };\n',
     '        ["mixed-exceptions"] = "### 7.4 mixed-exceptions",\n        ["signoff-final"] = "' + H + '",\n    };\n'),
    # V6(안 A만): 문턱 충족 테스트를 문턱을 충족하는 예제로 좁힌다. signoff-final이 문턱 미달인 것은 새 테스트가 본다.
    ('V6', T + 'Synthetic/CaseYamlTests.cs',
     '    [Theory]\n    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]\n    public void Structure_meets_thresholds(string example)\n',
     '    [Theory]\n    [MemberData(nameof(ThresholdExampleNames))]\n    public void Structure_meets_thresholds(string example)\n'),
    ('V6', T + 'Synthetic/CaseYamlTests.cs',
     '    public static TheoryData<string> NoDataExampleNames => new(SyntheticInput.NoDataExamples);\n',
     '    public static TheoryData<string> NoDataExampleNames => new(SyntheticInput.NoDataExamples);\n\n'
     '    // T-009: 문턱을 충족하는 자료 예제. signoff-final(정확도 80.0 < 95)은 SignoffFinalExampleTests가 미달을 본다.\n'
     '    public static TheoryData<string> ThresholdExampleNames => new(SyntheticInput.DataExamples.Where(e => e != "signoff-final"));\n'),
]


def main(repo, mode):
    for name, rel, old, new in CHANGES:
        if (mode == 'no-thresholds' and name == 'V6') or (mode == 'only-thresholds' and name != 'V6'):
            continue
        path = Path(repo) / rel
        text = path.read_text(encoding='utf-8')
        assert text.count(old) == 1, (name, rel)
        path.write_text(text.replace(old, new), encoding='utf-8')
        print(f'RESULT variant {name} {rel}')
    if mode == 'only-thresholds':
        return
    new_test = Path(__file__).with_name('SignoffFinalExampleTests.cs')
    (Path(repo) / T / 'Synthetic' / new_test.name).write_bytes(new_test.read_bytes())
    print(f'RESULT variant N1 {T}Synthetic/{new_test.name} (새 테스트 제안)')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2][2:] if len(sys.argv) > 2 else 'all')
