# case.yaml·answers.yaml 모델. case-schema 4.3~4.6, 4.13, T-012 개정(inputs.group), T-014(text-check.md 4.1: checklist·scores·
# thresholds.score_gap_max), M2-2(case-schema 4.14: 단계의 AI 판단 ai)를 보고 짰다(src/는 읽지 않았다).
# 오류가 있는 입력은 모델을 만들지 않고 OracleError로 멈춘다. 엔진의 오류 줄은 흉내 내지 않는다.
from __future__ import annotations

import re
from dataclasses import dataclass, field
from decimal import Decimal

from oracle_common import OracleError, net_trim
from oracle_yaml import Mapping, Scalar, Sequence, load

# case-schema 4.4 답 표. 순서가 곧 출력 순서다(classification 3.2 defaults, 4.12 Inferred).
# (키, 형식, 선택지 또는 범위, 기본값)
ANSWERS = (
    ('input_kind', 'enum', ('table', 'fixed_form', 'free_doc', 'image', 'conversation'), 'fixed_form'),
    ('rule_expressible', 'enum', ('yes', 'partial', 'no'), 'partial'),
    ('frequency', 'enum', ('daily', 'weekly', 'monthly', 'rare'), 'weekly'),
    ('batch_size', 'int', (1, 2147483647), 10),
    ('exception_rate', 'number', (Decimal(0), Decimal(100)), Decimal(20)),
    ('current_actor', 'enum', ('human', 'ai_each_time', 'program', 'mixed'), 'human'),
    ('minutes_per_item', 'number', (Decimal(0), None), None),
    ('failure_impact', 'enum', ('internal_rework', 'external', 'safety_legal'), 'internal_rework'),
    ('existing_feature', 'enum', ('yes', 'no', 'unknown'), 'unknown'),
    ('human_signoff', 'enum', ('yes', 'no'), 'no'),
)
ANSWER_KEYS = tuple(a[0] for a in ANSWERS)

# case-schema 4.3 최상위 키, 4.4 단계 항목 키, 4.5 inputs·compare·thresholds 키.
TOP_KEYS = ('name', 'task', 'before', 'after', 'ai_usage', 'steps', 'inputs', 'compare', 'checklist', 'scores', 'thresholds')
STEP_KEYS = ('name', 'answers', 'inferred', 'ai')  # ai: M2-2(case-schema 4.14), 선택
AI_KEYS = ('assignee', 'reason')
AI_ASSIGNEES = ('①', '②', '②M', '③', '④')  # case-schema 4.14. 서수 비교
INPUT_KEYS = ('reference', 'candidate', 'key', 'group')  # group: T-012 개정(case-schema 10.2), 선택
COMPARE_KEYS = ('field', 'tolerance', 'tolerance_pct')
THRESHOLD_KEYS = ('accuracy_min', 'missing_max', 'time_must_drop', 'score_gap_max')  # score_gap_max: T-014
CHECKLIST_KEYS = ('text', 'items')  # T-014 text-check.md 4.1
CHECKLIST_ITEM_KEYS = ('item', 'column', 'any')
SCORES_KEYS = ('sheet', 'sources', 'min', 'max')
AI_USAGE = ('build-time', 'runtime', 'hybrid')

# case-schema 4.6 값 형식.
NAME_RE = re.compile(r'[a-z0-9]+(?:-[a-z0-9]+)*', re.ASCII)
INT_RE = re.compile(r'-?(?:0|[1-9][0-9]*)', re.ASCII)
NUMBER_RE = re.compile(r'-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?', re.ASCII)


@dataclass(frozen=True)
class Answer:
    value: object  # str | int | Decimal | None
    is_default: bool
    is_inferred: bool


@dataclass(frozen=True)
class AiJudgment:
    """case-schema 4.14 단계의 ai. 담당(AI_ASSIGNEES 하나)과 근거(쓴 글 그대로)."""
    assignee: str
    reason: str


@dataclass(frozen=True)
class Step:
    name: str
    answers: dict  # 키 → Answer, ANSWER_KEYS 순서
    ai: AiJudgment | None = None  # M2-2. 키가 없거나 null이면 None


@dataclass(frozen=True)
class Inputs:
    reference: str
    candidate: str
    key: str
    group: str | None = None  # T-012: 몰림을 볼 기준 자료의 유형 열 이름. 적지 않았거나 null이면 None


@dataclass(frozen=True)
class CompareField:
    field: str
    tolerance_text: str | None  # case.yaml에 쓴 숫자 글 그대로(metrics 3.2 "값 그대로(자릿수 유지)")
    tolerance_pct_text: str | None

    @property
    def is_number(self) -> bool:
        return self.tolerance_text is not None or self.tolerance_pct_text is not None


@dataclass(frozen=True)
class Thresholds:
    """case-schema 4.5 thresholds. 적지 않은 키(또는 null)는 None. 판정(judgment.md 4.2)이 기본값을 정한다."""
    accuracy_min_text: str | None  # case.yaml에 쓴 숫자 글 그대로(judgment.md 3.2 "값 그대로(자릿수 유지)")
    missing_max_text: str | None
    time_must_drop: bool | None
    score_gap_max_text: str | None = None  # T-014. scores가 있을 때만 쓴다(text-check.md 4.1)


@dataclass(frozen=True)
class ChecklistItem:
    """text-check.md 4.1 checklist.items 하나. column(기준 자료 그 행의 칸) 또는 any(문구 목록) 하나만."""
    item: str
    column: str | None
    phrases: tuple  # any의 문구(쓴 글 그대로). column이면 ()


@dataclass(frozen=True)
class Checklist:
    text: str  # 결과 자료의 문장 열 이름
    items: tuple  # ChecklistItem, 쓴 순서


@dataclass(frozen=True)
class Scores:
    sheet: str  # 점수표 파일 패턴
    sources: str  # 출처 표 파일 패턴
    min: int
    max: int


@dataclass(frozen=True)
class Case:
    name: str
    task: str
    steps: list
    inputs: Inputs | None
    compare: list = field(default_factory=list)
    thresholds: Thresholds | None = None  # thresholds 키가 없거나 null이면 None
    checklist: Checklist | None = None  # T-014. 키가 없거나 null이면 None
    scores: Scores | None = None  # T-014. 키가 없거나 null이면 None


class _Reader:
    def __init__(self, source: str):
        self.source = source

    def fail(self, node, path: str, message: str):
        line = getattr(node, 'line', 0)
        where = f'{self.source}:{line}' if line else self.source
        raise OracleError(f'{where}: {path}: {message} (오류 입력, oracle 범위 밖)')

    def mapping(self, node, path: str, keys) -> Mapping:
        if not isinstance(node, Mapping):
            self.fail(node, path, '매핑이어야 한다')
        for k in node:
            if k not in keys:
                self.fail(node[k], f'{path}.{k}' if path else k, '알 수 없는 필드다')
        return node

    def string(self, node, path: str, required: bool):
        if node is None or (isinstance(node, Scalar) and node.is_null):
            if required:
                self.fail(node, path, '필수 필드가 없다 또는 값이 비어 있다')
            return None
        if not isinstance(node, Scalar):
            self.fail(node, path, '스칼라 값이어야 한다')
        if required and net_trim(node.text) == '':
            self.fail(node, path, '값이 비어 있다')
        return node.text

    def enum(self, node, path: str, choices):
        text = self.string(node, path, required=False)
        if text is None:
            return None
        if text not in choices:
            self.fail(node, path, f"선택지 밖의 값 '{text}'")
        return text

    def integer(self, node, path: str, low: int, high: int):
        if node is None or (isinstance(node, Scalar) and node.is_null):
            return None
        if not isinstance(node, Scalar):
            self.fail(node, path, '스칼라 값이어야 한다')
        if node.style != 'plain' or not INT_RE.fullmatch(node.text):
            self.fail(node, path, f'정수가 아니다: {node.text}')
        value = int(node.text)
        if node.text == '-0' or value < low or value > high:
            self.fail(node, path, f'범위 밖이다: {node.text}')
        return value

    def number(self, node, path: str, low, high):
        """숫자 글과 값. 글은 출력에 그대로 쓴다. '-0' 꼴은 스펙이 출력 모양을 정하지 않아 받지 않는다."""
        if node is None or (isinstance(node, Scalar) and node.is_null):
            return None, None
        if not isinstance(node, Scalar):
            self.fail(node, path, '스칼라 값이어야 한다')
        if node.style != 'plain' or not NUMBER_RE.fullmatch(node.text):
            self.fail(node, path, f'숫자가 아니다: {node.text}')
        value = Decimal(node.text)
        if node.text.startswith('-') and value == 0:
            raise OracleError(f'{self.source}:{node.line}: {path}: 음수 0({node.text})은 oracle 범위 밖')
        frac = node.text.split('.', 1)[1] if '.' in node.text else ''
        if len(frac) > 28 or value.copy_abs() > Decimal('79228162514264337593543950335'):
            raise OracleError(f'{self.source}:{node.line}: {path}: decimal로 그대로 담기지 않는 숫자는 oracle 범위 밖')
        if (low is not None and value < low) or (high is not None and value > high):
            self.fail(node, path, f'범위 밖이다: {node.text}')
        return node.text, value

    def boolean(self, node, path: str):
        if node is None or (isinstance(node, Scalar) and node.is_null):
            return None
        if not isinstance(node, Scalar) or node.style != 'plain' or node.text not in ('true', 'false'):
            self.fail(node, path, 'true 또는 false로 쓴다')
        return node.text == 'true'

    def path_text(self, node, path: str) -> str:
        text = self.string(node, path, required=True)
        if text.startswith('/') or '\\' in text or ':' in text or '..' in text.split('/'):
            self.fail(node, path, "케이스 폴더 기준 상대 경로를 '/'로 쓴다 ('..', ':', '\\' 없음)")
        return text


def parse_case(data: bytes, source: str) -> Case:
    """case.yaml 또는 answers.yaml 바이트 → Case. answers.yaml은 같은 스키마의 부분이다(classification 2절)."""
    r = _Reader(source)
    root = r.mapping(load(data, source), '', TOP_KEYS)

    name = r.string(root.get('name'), 'name', required=True)
    if len(name) > 64 or not NAME_RE.fullmatch(name):
        r.fail(root.get('name'), 'name', "소문자·숫자·'-'만, 64자까지 쓴다")
    task = r.string(root.get('task'), 'task', required=True)
    r.string(root.get('before'), 'before', required=False)
    r.string(root.get('after'), 'after', required=False)
    r.enum(root.get('ai_usage'), 'ai_usage', AI_USAGE)

    steps_node = root.get('steps')
    if steps_node is None or (isinstance(steps_node, Scalar) and steps_node.is_null):
        r.fail(steps_node, 'steps', '필수 필드가 없다')
    if not isinstance(steps_node, Sequence):
        r.fail(steps_node, 'steps', '목록이어야 한다')
    if len(steps_node) == 0:
        r.fail(steps_node, 'steps', '단계가 1개 이상 있어야 한다')
    steps = []
    seen_names = set()
    for n, step_node in enumerate(steps_node, start=1):
        p = f'steps[{n}]'
        step_map = r.mapping(step_node, p, STEP_KEYS)
        step_name = r.string(step_map.get('name'), f'{p}.name', required=True)
        if step_name in seen_names:
            r.fail(step_map.get('name'), f'{p}.name', '단계 이름이 중복된다')
        seen_names.add(step_name)
        steps.append(Step(step_name, _answers(r, step_map, p), _ai(r, step_map, p)))

    inputs = None
    inputs_node = root.get('inputs')
    if inputs_node is not None and not (isinstance(inputs_node, Scalar) and inputs_node.is_null):
        m = r.mapping(inputs_node, 'inputs', INPUT_KEYS)
        group_node = m.get('group')
        group = r.string(group_node, 'inputs.group', required=False)
        if group is not None and net_trim(group) == '':
            r.fail(group_node, 'inputs.group', '값이 비어 있다')  # required (case-schema T-012 개정)
        inputs = Inputs(
            r.path_text(m.get('reference'), 'inputs.reference'),
            r.path_text(m.get('candidate'), 'inputs.candidate'),
            r.string(m.get('key'), 'inputs.key', required=True),
            group,
        )

    compare = []
    compare_node = root.get('compare')
    if compare_node is not None and not (isinstance(compare_node, Scalar) and compare_node.is_null):
        if not isinstance(compare_node, Sequence):
            r.fail(compare_node, 'compare', '목록이어야 한다')
        seen_fields = set()
        for n, item in enumerate(compare_node, start=1):
            p = f'compare[{n}]'
            m = r.mapping(item, p, COMPARE_KEYS)
            fname = r.string(m.get('field'), f'{p}.field', required=True)
            if fname in seen_fields:
                r.fail(m.get('field'), f'{p}.field', '비교 필드가 중복된다')
            seen_fields.add(fname)
            tol, _ = r.number(m.get('tolerance'), f'{p}.tolerance', Decimal(0), None)
            pct, _ = r.number(m.get('tolerance_pct'), f'{p}.tolerance_pct', Decimal(0), None)
            if tol is not None and pct is not None:
                r.fail(m, p, 'tolerance와 tolerance_pct 중 하나만 쓴다')
            compare.append(CompareField(fname, tol, pct))
        if compare and inputs is None:
            r.fail(compare_node, 'inputs', 'compare가 있으면 inputs가 있어야 한다')

    # T-012: group은 key·compare 필드와 다른 열이어야 한다(conflict, 위치는 group 값).
    if inputs is not None and inputs.group is not None:
        if inputs.group == inputs.key or any(f.field == inputs.group for f in compare):
            r.fail(root.get('inputs').get('group'), 'inputs.group', 'group은 key·compare 필드와 다른 열을 쓴다')

    checklist = _checklist(r, root, inputs, compare)
    scores = _scores(r, root)

    thresholds = None
    thresholds_node = root.get('thresholds')
    if thresholds_node is not None and not (isinstance(thresholds_node, Scalar) and thresholds_node.is_null):
        m = r.mapping(thresholds_node, 'thresholds', THRESHOLD_KEYS)
        acc, _ = r.number(m.get('accuracy_min'), 'thresholds.accuracy_min', Decimal(0), Decimal(100))
        miss, _ = r.number(m.get('missing_max'), 'thresholds.missing_max', Decimal(0), Decimal(100))
        drop = r.boolean(m.get('time_must_drop'), 'thresholds.time_must_drop')
        gap, _ = r.number(m.get('score_gap_max'), 'thresholds.score_gap_max', Decimal(0), Decimal(100))
        if gap is not None and scores is None:
            r.fail(m.get('score_gap_max'), 'scores', 'score_gap_max가 있으면 scores가 있어야 한다')  # required
        thresholds = Thresholds(acc, miss, drop, gap)

    return Case(name, task, steps, inputs, compare, thresholds, checklist, scores)


def _is_null(node) -> bool:
    return node is None or (isinstance(node, Scalar) and node.is_null)


def _checklist(r: _Reader, root: Mapping, inputs, compare: list):
    """text-check.md 4.1 checklist. 오류 입력은 OracleError로 멈춘다(코드·메시지는 흉내 내지 않는다)."""
    node = root.get('checklist')
    if _is_null(node):
        return None
    m = r.mapping(node, 'checklist', CHECKLIST_KEYS)
    text = r.string(m.get('text'), 'checklist.text', required=True)
    items_node = m.get('items')
    if _is_null(items_node):
        r.fail(items_node if items_node is not None else m, 'checklist.items', '필수 필드가 없다')
    if not isinstance(items_node, Sequence):
        r.fail(items_node, 'checklist.items', '목록이어야 한다')
    if len(items_node) == 0:
        r.fail(items_node, 'checklist.items', '항목이 1개 이상 있어야 한다')
    items, seen = [], {}
    for n, item_node in enumerate(items_node, start=1):
        p = f'checklist.items[{n}]'
        im = r.mapping(item_node, p, CHECKLIST_ITEM_KEYS)
        name = r.string(im.get('item'), f'{p}.item', required=True)
        if name in seen:
            r.fail(im.get('item'), f'{p}.item', f'체크리스트 항목이 중복된다 (처음: checklist.items[{seen[name]}])')  # duplicate
        seen[name] = n
        has_column, has_any = not _is_null(im.get('column')), not _is_null(im.get('any'))
        if has_column and has_any:
            r.fail(im, p, 'column과 any 중 하나만 쓴다')  # conflict
        if not has_column and not has_any:
            r.fail(im, p, 'column 또는 any가 있어야 한다')  # required
        if has_column:
            items.append(ChecklistItem(name, r.string(im.get('column'), f'{p}.column', required=True), ()))
            continue
        any_node = im.get('any')
        if not isinstance(any_node, Sequence):
            r.fail(any_node, f'{p}.any', '목록이어야 한다')
        if len(any_node) == 0:
            r.fail(any_node, f'{p}.any', '문구가 1개 이상 있어야 한다')
        phrases = tuple(r.string(x, f'{p}.any[{k}]', required=True) for k, x in enumerate(any_node, start=1))
        items.append(ChecklistItem(name, None, phrases))
    if inputs is None:
        r.fail(node, 'inputs', 'checklist가 있으면 inputs가 있어야 한다')  # required
    if text == inputs.key or text == inputs.group or any(f.field == text for f in compare):
        r.fail(m.get('text'), 'checklist.text', 'checklist.text는 key·compare 필드·group과 다른 열을 쓴다')  # conflict
    return Checklist(text, tuple(items))


def _scores(r: _Reader, root: Mapping):
    """text-check.md 4.1 scores."""
    node = root.get('scores')
    if _is_null(node):
        return None
    m = r.mapping(node, 'scores', SCORES_KEYS)
    sheet = r.path_text(m.get('sheet'), 'scores.sheet')
    sources = r.path_text(m.get('sources'), 'scores.sources')
    bounds = {}
    for key in ('min', 'max'):
        node_k = m.get(key)
        value = r.integer(node_k, f'scores.{key}', -(10 ** 18), 10 ** 18)
        if value is None:
            r.fail(node_k if node_k is not None else m, f'scores.{key}', '필수 필드가 없다')  # required
        if not 0 <= value <= 100:
            r.fail(node_k, f'scores.{key}', f'범위 밖이다 (0 이상 100 이하): {node_k.text}')  # range
        bounds[key] = value
    low, high = bounds['min'], bounds['max']
    if low >= high:
        r.fail(m.get('max'), 'scores.max', f'범위 밖이다 (min보다 커야 한다): {high}')  # range, 위치는 max 값
    return Scores(sheet, sources, low, high)


def _ai(r: _Reader, step_map: Mapping, p: str):
    """case-schema 4.14 ai. 없거나 null이면 None. 오류 입력은 OracleError로 멈춘다."""
    node = step_map.get('ai')
    if _is_null(node):
        return None
    m = r.mapping(node, f'{p}.ai', AI_KEYS)
    assignee_node = m.get('assignee')
    assignee = r.string(assignee_node, f'{p}.ai.assignee', required=True)
    if assignee not in AI_ASSIGNEES:
        r.fail(assignee_node, f'{p}.ai.assignee', f"선택지 밖의 값 '{assignee}'")  # enum
    reason = r.string(m.get('reason'), f'{p}.ai.reason', required=True)
    return AiJudgment(assignee, reason)


def _answers(r: _Reader, step_map: Mapping, p: str) -> dict:
    node = step_map.get('answers')
    if node is None or (isinstance(node, Scalar) and node.is_null):
        given = Mapping(0)
    else:
        given = r.mapping(node, f'{p}.answers', ANSWER_KEYS)

    values = {}
    for key, kind, spec, default in ANSWERS:
        path = f'{p}.answers.{key}'
        child = given.get(key)
        if kind == 'enum':
            value = r.enum(child, path, spec)
        elif kind == 'int':
            value = r.integer(child, path, spec[0], spec[1])
        else:
            _, value = r.number(child, path, spec[0], spec[1])
        values[key] = value  # None이면 기본값(키 없음 또는 null)

    inferred = set()
    inf_node = step_map.get('inferred')
    if inf_node is not None and not (isinstance(inf_node, Scalar) and inf_node.is_null):
        if not isinstance(inf_node, Sequence):
            r.fail(inf_node, f'{p}.inferred', '목록이어야 한다')
        for m, item in enumerate(inf_node, start=1):
            ip = f'{p}.inferred[{m}]'
            if not isinstance(item, Scalar):
                r.fail(item, ip, '스칼라 값이어야 한다')
            if item.is_null:
                r.fail(item, ip, '값이 비어 있다')
            if item.text not in ANSWER_KEYS:
                r.fail(item, ip, f"선택지 밖의 값 '{item.text}'")
            if item.text in inferred:
                r.fail(item, ip, '추정 답 키가 중복된다')
            if values[item.text] is None:
                r.fail(item, f'{p}.answers.{item.text}', 'inferred에 적은 답은 answers에 값이 있어야 한다')
            inferred.add(item.text)

    answers = {}
    for key, _kind, _spec, default in ANSWERS:
        value = values[key]
        if value is None:
            answers[key] = Answer(default, True, False)
        else:
            answers[key] = Answer(value, False, key in inferred)
    return answers
