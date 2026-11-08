#!/usr/bin/env python3
# oracle 분류기. docs/spec/classification.md 3.2·3.3·4.1~4.8·4.12·9절과 case-schema.md 4.4·4.13·4.14만 보고 짰다.
# src/는 읽지 않았다(ADR 0005 결정 9). 규칙 값은 스펙 표를 손으로 옮겨 이 파일에 적었다.
# 스펙 표가 바뀌면 prototyper가 이 파일을 같이 고친다. 실행 중에 스펙 문서를 읽지 않는다.
#
# 판이 둘이다(ADR 0013). schema 2(classification.md 9절, M2-2): 단계마다 source·rule_opinion·differs, AI 판단 행,
# 확인 항목에서 confirm_existing_feature를 뺀다. schema 1: M2-2 전 규칙 그대로(동결). ai가 있는 입력은 거절한다.
# check.py가 expected classification.json의 schema_version으로 판을 고른다. M2-2 PR (b) 뒤 첫 oracle 변경에서 schema 1 길을 지운다.
#
# 사용: python3 tools/oracle/classify.py [--schema 1|2] <answers.yaml 또는 case.yaml>
#   표준 출력에 classification.json 바이트를 쓴다(기본 schema 2). 범위 밖 입력이면 표준 오류에 이유, 종료 코드 2.
# 표준 라이브러리만 쓴다. 네트워크·외부 프로세스 없음.
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

sys.dont_write_bytecode = True  # 저장소에 __pycache__를 쓰지 않는다.
sys.path.insert(0, str(Path(__file__).resolve().parent))

from oracle_case import ANSWER_KEYS, Case, parse_case  # noqa: E402
from oracle_common import OracleError, utf8_stdio  # noqa: E402
from oracle_json import render  # noqa: E402

SCHEMA_VERSION = 2  # classification.md 9.2 첫 키(ADR 0013). 지금 판
SCHEMAS = (1, 2)  # 1은 M2-2 전 판(3.2). PR (b) 뒤 지운다(ADR 0013 결정 5)

# 4.2 조건 표. (근거 키, 키, 비교, 값). 같은 근거 키의 행은 모두 참이어야 걸린다.
CONDITIONS = (
    ('SIGNOFF', 'human_signoff', '=', ('yes',)),
    ('Q1', 'existing_feature', '=', ('yes',)),
    ('Q2', 'rule_expressible', '=', ('no',)),
    ('Q3', 'input_kind', 'in', ('free_doc', 'image', 'conversation')),
    ('Q4', 'frequency', '=', ('rare',)),
    ('Q4', 'batch_size', '<', (5,)),
    ('PARTIAL', 'rule_expressible', '=', ('partial',)),
    ('GATE', 'failure_impact', 'in', ('external', 'safety_legal')),
    ('OVERUSE', 'current_actor', '=', ('ai_each_time',)),
    ('CHECK_EXISTING', 'existing_feature', '=', ('unknown',)),
)

# 4.1 해석 표: step 행 잎의 순서. 처음 걸리는 곳에서 멈추고, 아무것도 안 걸리면 ELSE.
LEAF_ORDER = ('SIGNOFF', 'Q1', 'Q2', 'Q3', 'Q4')

# 4.4 행 종류 값.
KIND_STEP = 'step'
KIND_EXCEPTION_REVIEW = 'exception_review'
KIND_HUMAN_GATE = 'human_gate'

# 4.5 문장 틀 표. 근거 키 → (담당, 행 종류, 문장 틀).
TEMPLATES = {
    'SIGNOFF': ('④', KIND_STEP, '사람이 최종 확인·서명하는 단계이므로(yes) 사람이 하는 것으로 분류.'),
    'Q1': ('①', KIND_STEP, '기존 프로그램에 같은 기능이 있으므로(yes) 기존 기능·설정으로 분류.'),
    'Q2': ('③', KIND_STEP, '판단 기준을 글로 쓸 수 없으므로(no) 실행 시 AI 보조 + 사람 확인으로 분류. 자동 승인은 하지 않는다.'),
    'Q3': ('③', KIND_STEP, '입력이 {input_kind 문구}({input_kind}) 입력 양식 표준화를 먼저 검토하고, 안 되면 실행 시 AI 보조로 분류. 해석만 AI, 계산·저장은 코드로 한다.'),
    'Q4': ('④', KIND_STEP, '드물게 반복되고 한 번에 {batch_size}건이므로 그대로 두거나 일회성 AI 보조로 분류. 개발비가 더 크다.'),
    'ELSE': ('②', KIND_STEP, '{rule_expressible 문구}({rule_expressible}), 입력이 {input_kind 문구}({input_kind}), {frequency 문구} 규칙 기반 소프트웨어로 분류.'),
    'PARTIAL': ('④', KIND_EXCEPTION_REVIEW, '판단 기준을 일부만 글로 쓸 수 있으므로(partial) 예외 건은 사람이 검토한다.'),
    'GATE': ('④', KIND_HUMAN_GATE, '틀리면 {failure_impact 문구}({failure_impact}) 이 단계 뒤에 사람 게이트를 둔다.'),
}

# 4.5 문구 표. (키, 값) → 문구. 문구 안의 {batch_size}도 바꾼다.
PHRASES = {
    ('input_kind', 'table'): '표 형식이며',
    ('input_kind', 'fixed_form'): '고정 양식이며',
    ('input_kind', 'free_doc'): '자유 형식 문서이므로',
    ('input_kind', 'image'): '이미지이므로',
    ('input_kind', 'conversation'): '대화이므로',
    ('rule_expressible', 'yes'): '판단 기준을 글로 쓸 수 있고',
    ('rule_expressible', 'partial'): '판단 기준을 일부만 글로 쓸 수 있고',
    ('frequency', 'daily'): '매일 반복되므로',
    ('frequency', 'weekly'): '매주 반복되므로',
    ('frequency', 'monthly'): '매월 반복되므로',
    ('frequency', 'rare'): '드물게 반복되지만 한 번에 {batch_size}건이므로',
    ('failure_impact', 'external'): '외부에 영향이 있으므로',
    ('failure_impact', 'safety_legal'): '안전·법규에 영향이 있으므로',
}

# 4.6 읽은 답 표. 키 순서는 case-schema 4.4 순서.
USED = {
    'SIGNOFF': ('human_signoff',),
    'Q1': ('failure_impact', 'existing_feature', 'human_signoff'),
    'Q2': ('rule_expressible', 'failure_impact', 'existing_feature', 'human_signoff'),
    'Q3': ('input_kind', 'rule_expressible', 'failure_impact', 'existing_feature', 'human_signoff'),
    'Q4': ('input_kind', 'rule_expressible', 'frequency', 'batch_size', 'failure_impact', 'existing_feature', 'human_signoff'),
    'ELSE': ('input_kind', 'rule_expressible', 'frequency', 'failure_impact', 'existing_feature', 'human_signoff'),
    'ELSE (frequency = rare)': ('input_kind', 'rule_expressible', 'frequency', 'batch_size', 'failure_impact', 'existing_feature', 'human_signoff'),
    'PARTIAL': ('rule_expressible',),
    'GATE': ('failure_impact',),
}

# 4.6 기본값 표시, 4.12 추정 표시. 문장 끝에 공백 하나와 함께. 둘 다면 기본값 뒤에 추정.
DEFAULT_MARK = ' (기본값 사용, 확인 필요)'
INFERRED_MARK = ' (AI 추정, 확인 필요)'

# 4.7 확인 항목 표. (키, 답 키, 값). 값 None은 "null이면". schema 1.
CHECKS = (
    ('measure_minutes_per_item', 'minutes_per_item', None),
    ('confirm_existing_feature', 'existing_feature', 'unknown'),
)
# 9.4 확인 항목 표(schema 2). confirm_existing_feature를 뺐다(AI가 판단한다).
CHECKS_V2 = (
    ('measure_minutes_per_item', 'minutes_per_item', None),
)

# 9.1·9.3(schema 2). AI 판단 행의 근거 키, source 값, 담당 비교 값 표(AI 담당 → 규칙 담당과 비교할 값).
RULE_AI = 'AI'
SOURCE_AI = 'ai'
SOURCE_RULE = 'rule'
COMPARE_AS = {'①': '①', '②': '②', '②M': '②', '③': '③', '④': '④'}

_PLACEHOLDER = re.compile(r'\{([a-z_]+)( 문구)?\}')


def _holds(rule: str, values: dict) -> bool:
    rows = [c for c in CONDITIONS if c[0] == rule]
    if not rows:
        raise OracleError(f'조건 표에 없는 근거 키 {rule}')
    for _rule, key, op, operands in rows:
        v = values[key]
        if op == '=':
            ok = v == operands[0]
        elif op == 'in':
            ok = v in operands
        elif op == '<':
            ok = v < operands[0]
        else:
            raise OracleError(f'모르는 비교 {op}')
        if not ok:
            return False  # 그리고: 앞이 거짓이면 뒤를 보지 않는다(4.6)
    return True


def _value_text(value) -> str:
    # 숫자는 InvariantCulture(classification 3.3). 문장에 들어가는 숫자는 batch_size(정수)뿐이다.
    if isinstance(value, bool) or value is None:
        raise OracleError(f'문장에 넣을 수 없는 값 {value!r}')
    return str(value)


def _sentence(rule: str, values: dict) -> str:
    template = TEMPLATES[rule][2]

    def sub(m):
        key, phrase = m.group(1), m.group(2)
        if phrase:
            text = PHRASES.get((key, values[key]))
            if text is None:
                raise OracleError(f'문구 표에 없는 값 {key}={values[key]}')
            return text.replace('{batch_size}', _value_text(values['batch_size']))
        return _value_text(values[key])

    return _PLACEHOLDER.sub(sub, template)


def classify(case: Case, schema: int = SCHEMA_VERSION) -> list:
    """Case → 단계 결과 목록. 원소는 dict: JSON 속성(number~rows)과 메모리 전용 inferred·used."""
    if schema not in SCHEMAS:
        raise OracleError(f'모르는 schema_version {schema}')
    result = []
    for number, step in enumerate(case.steps, start=1):
        if schema == 1 and step.ai is not None:
            raise OracleError(f'steps[{number}].ai: schema 1 판 범위 밖(ADR 0013)')  # 옛 판 가드
        answers = step.answers
        values = {k: answers[k].value for k in ANSWER_KEYS}
        rows = []

        def add(rule: str, used_key: str):
            assignee, kind, _ = TEMPLATES[rule]
            used = USED[used_key]
            reason = _sentence(rule, values)
            if any(answers[k].is_default for k in used):
                reason += DEFAULT_MARK
            if any(answers[k].is_inferred for k in used):
                reason += INFERRED_MARK
            rows.append({'kind': kind, 'assignee': assignee, 'rule': rule, 'reason': reason, 'used': used})

        leaf = next((r for r in LEAF_ORDER if _holds(r, values)), 'ELSE')
        add(leaf, 'ELSE (frequency = rare)' if leaf == 'ELSE' and values['frequency'] == 'rare' else leaf)
        # 4.3: PARTIAL은 ELSE일 때만, GATE는 SIGNOFF가 아닐 때. 순서는 step → exception_review → human_gate.
        if leaf == 'ELSE' and _holds('PARTIAL', values):
            add('PARTIAL', 'PARTIAL')
        if leaf != 'SIGNOFF' and _holds('GATE', values):
            add('GATE', 'GATE')
        # 9.3 규칙 5(schema 2): SIGNOFF 단계라도 AI 담당의 비교 값이 ④가 아니면 GATE를 붙인다.
        # 게이트를 빼는 근거는 step 행이 ④라는 것이다. 마지막 step 행이 ④일 때만 뺀다.
        if (schema == 2 and leaf == 'SIGNOFF' and step.ai is not None
                and COMPARE_AS[step.ai.assignee] != '④' and _holds('GATE', values)):
            add('GATE', 'GATE')

        out = {'number': number, 'name': step.name}
        if schema == 1:
            checks = [name for name, key, want in CHECKS if values[key] == want]
            # 4.8: current_actor가 ai_each_time이고 step 행 담당이 ②.
            overuse = _holds('OVERUSE', values) and rows[0]['assignee'] == '②'
            if _holds('CHECK_EXISTING', values) != ('confirm_existing_feature' in checks):
                raise OracleError('확인 항목 표와 조건 표가 어긋난다')  # 4.2 CHECK_EXISTING 행과 4.7 표는 같은 조건이다.
        else:
            # 9.1: 규칙 의견 = 트리 step 행의 담당·근거 키. AI 판단이 있으면 step 행만 AI 행으로 바꾼다.
            # 부가 행(PARTIAL·GATE)은 트리 것을 그대로 둔다(9.3 규칙 4). SIGNOFF 뒤 GATE만 규칙 5.
            opinion = {'assignee': rows[0]['assignee'], 'rule': rows[0]['rule']}
            if step.ai is not None:
                rows[0] = {'kind': KIND_STEP, 'assignee': step.ai.assignee, 'rule': RULE_AI,
                           'reason': step.ai.reason, 'used': ()}
                out['source'] = SOURCE_AI
                differs = COMPARE_AS[step.ai.assignee] != opinion['assignee']  # 9.3 표
            else:
                out['source'] = SOURCE_RULE
                differs = False
            out['rule_opinion'] = opinion
            out['differs'] = differs
            checks = [name for name, key, want in CHECKS_V2 if values[key] == want]
            # 9.1: current_actor가 ai_each_time이고 step 행 담당의 비교 값이 ②(② 또는 ②M).
            overuse = _holds('OVERUSE', values) and COMPARE_AS[rows[0]['assignee']] == '②'

        out.update({
            'defaults': [k for k in ANSWER_KEYS if answers[k].is_default],
            'checks': checks,
            'ai_overuse_candidate': overuse,
            'rows': rows,
            'inferred': [k for k in ANSWER_KEYS if answers[k].is_inferred],
        })
        result.append(out)
    return result


def to_json_value(case: Case, steps: list, schema: int = SCHEMA_VERSION) -> dict:
    """3.2 모양(schema 1) 또는 9.2 모양(schema 2). Used·Inferred는 쓰지 않는다."""
    if schema not in SCHEMAS:
        raise OracleError(f'모르는 schema_version {schema}')
    out_steps = []
    for s in steps:
        step = {'number': s['number'], 'name': s['name']}
        if schema == 2:
            step['source'] = s['source']
            step['rule_opinion'] = {'assignee': s['rule_opinion']['assignee'], 'rule': s['rule_opinion']['rule']}
            step['differs'] = s['differs']
        step['defaults'] = s['defaults']
        step['checks'] = s['checks']
        step['ai_overuse_candidate'] = s['ai_overuse_candidate']
        step['rows'] = [{'kind': r['kind'], 'assignee': r['assignee'], 'rule': r['rule'], 'reason': r['reason']}
                        for r in s['rows']]
        out_steps.append(step)
    return {'schema_version': schema, 'case': case.name, 'steps': out_steps}


def classification_bytes(data: bytes, source: str, schema: int = SCHEMA_VERSION) -> bytes:
    case = parse_case(data, source)
    return render(to_json_value(case, classify(case, schema), schema))


def schema_of(expected: bytes):
    """expected classification.json의 schema_version(1 또는 2). 읽을 수 없거나 모르는 값이면 None."""
    try:
        value = json.loads(expected.decode('utf-8')).get('schema_version')
    except (ValueError, AttributeError):
        return None
    return value if type(value) is int and value in SCHEMAS else None


def main(argv: list) -> int:
    utf8_stdio()
    schema = SCHEMA_VERSION
    if len(argv) == 3 and argv[0] == '--schema' and argv[1] in ('1', '2'):
        schema, argv = int(argv[1]), argv[2:]
    if len(argv) != 1:
        print('사용: classify.py [--schema 1|2] <answers.yaml 또는 case.yaml>', file=sys.stderr)
        return 2
    path = Path(argv[0])
    try:
        out = classification_bytes(path.read_bytes(), path.name, schema)
    except OracleError as e:
        print(f'oracle: {e}', file=sys.stderr)
        return 2
    sys.stdout.buffer.write(out)
    sys.stdout.buffer.flush()
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
