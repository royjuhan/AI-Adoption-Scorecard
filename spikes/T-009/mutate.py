#!/usr/bin/env python3
# T-009 스파이크(머지 대상 아님). 음성 대조: clone 하나를 바꿔 새 테스트·골든이 잡는지 본다. 저장소는 바꾸지 않는다.
# 사용: mutate.py <저장소> <변형>
#   gate-after-signoff : Core가 SIGNOFF 뒤에도 게이트를 붙인다(ADR 0003 규칙 위반).
#   step7-no           : 예제 answers.yaml·case.yaml 7단계를 human_signoff: no로 바꾼다(expected는 그대로).
#   seed-48            : synthetic-examples.md 4.3 signoff-final 시드를 48로 바꾼다(CSV는 그대로).
import sys
from pathlib import Path


def edit(path, old, new, count=1):
    text = path.read_text(encoding='utf-8')
    assert text.count(old) == count, (path, old)
    path.write_text(text.replace(old, new), encoding='utf-8')


def main(repo, which):
    r = Path(repo)
    ex = r / 'examples/signoff-final'
    if which == 'gate-after-signoff':
        edit(r / 'src/Scorecard.Core/Classification/StepClassifier.cs',
             'if (leaf.Rule != RuleSignoff && GateFailureImpacts', 'if (GateFailureImpacts')
    elif which == 'step7-no':
        line = 'current_actor: human, failure_impact: external, existing_feature: no, human_signoff: yes}'
        for name in ('answers.yaml', 'case.yaml'):
            edit(ex / name, line, line.replace('human_signoff: yes', 'human_signoff: no'))
    elif which == 'seed-48':
        edit(r / 'docs/spec/synthetic-examples.md', '| `signoff-final` | `47` |', '| `signoff-final` | `48` |')
    else:
        raise SystemExit(f'모르는 변형: {which}')
    print(f'RESULT mutate {which}')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
