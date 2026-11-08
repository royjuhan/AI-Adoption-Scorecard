#!/usr/bin/env python3
# oracle 판정(judgment.json schema_version 2). docs/spec/judgment.md 2절, 3.1~3.3, 4.1~4.10, 5절만 보고 짰다
# (src/는 읽지 않았다, ADR 0005 결정 9). T-013에서 T-011 판(judge_v1.py)과 판 고르기를 지웠다(ADR 0007 결정 4, ADR 0008).
# 대조는 oracle compare.py의 결과(metrics.md 3.1 메모리 결과: 항목과 기준 행 전부, metrics.md 4.3 duplicate 항목)를 쓴다.
# 규칙 값(4.2 기본값, 4.9 검토 항목, 4.3 판정 표, 4.4 확인 표, 4.5 focus_min, 4.6 읽은 문턱, 4.7 다음 할 일)은
# 스펙 표를 손으로 옮겨 이 파일에 적었다. 실행 중에 스펙 문서를 읽지 않는다. selftest.py가 상수와 표를 대조한다.
# T-014(docs/spec/text-check.md 5절): 점수표(scores)가 있으면 확인 SCORE를 더하고, 대조가 미검증이어도 점수표로 판정한다
#   (길 "점수표만"). 점수표가 없으면 judgment.md 4절 그대로다(바이트가 T-013과 같다). 체크리스트는 대조 셈으로 들어온다.
#   PR (a) 리뷰 뒤: 점수표만인데 inputs가 있고 대조할 열이 없으면(no_compare_fields) 통과가 아니라 조건부(5.3),
#   점수표만은 읽지 않는 문턱을 defaults에 적지 않는다(5.1), score_gap_max가 0이면 "넘게" 없는 문장(5.2 문턱 0 문장 표).
# T-015(docs/spec/confusion.md 5절): confusion이 있으면 확인 RECALL·PRECISION을 더하고(SCORE 뒤), 판정 표 순서 3 뒤에
#   보류 두 행, 순서 7 뒤에 조건부 한 행을 넣는다. 순서 4 이후 행의 읽은 문턱에 recall_min·precision_min이 든다.
#   confusion이 없으면 바이트가 T-014와 같다.
#
# 사용: python3 tools/oracle/judge.py <케이스 폴더>
#   표준 출력에 judgment.json 바이트(시간·사람 입력 없음. judgment.md 8절 골든과 같다).
#   범위 밖 입력이면 표준 오류에 이유, 종료 코드 2.
# 표준 라이브러리만 쓴다. 네트워크·외부 프로세스 없음.
from __future__ import annotations

import re
import sys
from dataclasses import dataclass
from decimal import Decimal, DecimalException, localcontext
from pathlib import Path

sys.dont_write_bytecode = True  # 저장소에 __pycache__를 쓰지 않는다.
sys.path.insert(0, str(Path(__file__).resolve().parent))

from compare import (DEC_MAX, ITEM_DUPLICATE, ITEM_EXTRA, ITEM_MISMATCH, ITEM_MISSING, REASON_NO_COMPARE_FIELDS,  # noqa: E402
                     REASON_NO_INPUTS, STATUS_MEASURED, STATUS_NOT_VERIFIED, CompareRun, _case_yaml, _fits, compare_run)
from oracle_case import Case, parse_case  # noqa: E402
from oracle_common import EXACT, OracleError, utf16_units, utf8_stdio  # noqa: E402
from oracle_json import JsonNumber, render  # noqa: E402

SCHEMA_VERSION = 2  # judgment.md 3.2: T-012가 속성 뜻을 바꿔 1 → 2(ADR 0004 결정 2)
CASE_FILE = 'case.yaml'

# 0절 판정 값.
VERDICT_PASS, VERDICT_CONDITIONAL, VERDICT_HOLD, VERDICT_NOT_VERIFIED = 'pass', 'conditional', 'hold', 'not_verified'

# 4.9: 검토 항목 = 불일치(값 불일치·누락) + 추가 행 + 결과 쪽 중복 행.
REVIEW_STATUSES = (ITEM_MISMATCH, ITEM_MISSING, ITEM_EXTRA, ITEM_DUPLICATE)

# 4.2 문턱 기본값 표(T-012 그대로). (키, 기본값 글). 표 순서가 defaults 순서다.
THRESHOLD_DEFAULTS = (
    ('accuracy_min', '95'),
    ('missing_max', '2'),
    ('time_must_drop', 'true'),
)

# 4.3 판정 표. (순서, 근거 키, 확인 결과, 판정, 판정 문장 틀). 위에서부터 처음 걸리는 행. PASS는 결과 없음(None).
VERDICT_TABLE = (
    (1, 'NOT_VERIFIED', 'no_inputs', 'not_verified', '검증 자료(case.yaml inputs)가 없어 미검증.'),
    (1, 'NOT_VERIFIED', 'no_compare_fields', 'not_verified', '대조할 열(case.yaml compare)이 없어 미검증.'),
    (2, 'ACCURACY', 'not_met', 'hold', '정확도 {accuracy_pct}%가 문턱 {accuracy_min}%에 못 미치므로 보류.'),
    (3, 'MISSING', 'not_met', 'hold', '누락률 {missing_rate_pct}%가 문턱 {missing_max}%를 넘으므로 보류.'),
    (4, 'UNEXPLAINED', 'found', 'hold', '사람이 설명할 수 없다고 적은 검토 항목 키가 {unexplained}개 있으므로 보류.'),
    (5, 'TIME', 'not_measured', 'conditional',
     '정확도·누락률은 문턱을 충족하나 시간을 재지 않아 줄었는지 알 수 없으므로 조건부.'),
    (5, 'TIME', 'not_met', 'conditional',
     '정확도·누락률은 문턱을 충족하나 처리·검토 시간 {total}분이 적용 전 {before}분보다 짧지 않으므로 조건부.'),
    (6, 'FOCUS', 'found', 'conditional',
     '정확도·누락률은 문턱을 충족하나 불일치 {bad}건이 모두 {group} = {value}인 행이므로 조건부.'),
    (7, 'UNEXPLAINED', 'not_reviewed', 'conditional',
     '정확도·누락률은 문턱을 충족하나 검토할 항목 {items}건({breakdown})을 사람이 아직 보지 않았으므로 조건부.'),
    (8, 'PASS', None, 'pass', '정확도·누락률이 문턱을 충족하고 조건부·보류 조건에 걸리지 않으므로 통과.'),
)

# 4.4 확인 표. (확인, 결과, 확인 문장 틀). 확인 순서는 표에서 처음 나오는 순서다.
CHECK_TABLE = (
    ('ACCURACY', 'met', '정확도 {accuracy_pct}%({match}/{reference})가 문턱 {accuracy_min}% 이상이다.'),
    ('ACCURACY', 'not_met', '정확도 {accuracy_pct}%({match}/{reference})가 문턱 {accuracy_min}%에 못 미친다.'),
    ('MISSING', 'met', '누락률 {missing_rate_pct}%({missing}/{reference})가 문턱 {missing_max}% 이하다.'),
    ('MISSING', 'not_met', '누락률 {missing_rate_pct}%({missing}/{reference})가 문턱 {missing_max}%를 넘는다.'),
    ('UNEXPLAINED', 'none', '검토할 항목(불일치·추가 행·중복 행)이 없다.'),
    ('UNEXPLAINED', 'not_reviewed', '검토할 항목 {items}건({breakdown})이 설명되는지 사람이 아직 보지 않았다.'),
    ('UNEXPLAINED', 'explained', '검토할 항목 {items}건({breakdown})을 사람이 보고 모두 설명했다.'),
    ('UNEXPLAINED', 'found', '검토할 항목 {items}건({breakdown}) 중 키 {unexplained}개를 사람이 설명할 수 없다고 적었다.'),
    ('TIME', 'off', '시간 조건을 쓰지 않는다(time_must_drop: false).'),
    ('TIME', 'not_measured', '시간을 재지 않았다.'),
    ('TIME', 'met', '처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧다.'),
    ('TIME', 'not_met', '처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧지 않다.'),
    ('FOCUS', 'few', '불일치가 {bad}건이라 몰림을 보지 않는다.'),
    ('FOCUS', 'off', 'case.yaml inputs.group에 유형 열을 적지 않아 불일치 {bad}건이 한 유형에 몰렸는지 보지 않는다.'),
    ('FOCUS', 'one_value', '{group} 열이 기준 자료에서 값이 하나뿐이라 몰림을 볼 수 없다.'),
    ('FOCUS', 'found',
     '불일치 {bad}건이 모두 {group} = {value}인 행이다({value} {rows_in}행 중 {bad}행, 다른 값 {rows_out}행 중 0행).'),
    ('FOCUS', 'spread', '불일치 {bad}건이 {group} 열의 한 값에 몰리지 않았다.'),
)
CHECK_ORDER = tuple(dict.fromkeys(c for c, _r, _t in CHECK_TABLE))

# 4.5 숫자 표(T-012 그대로).
FOCUS_MIN = 2

# 4.6: 확인 문장이 쓰는 문턱(T-012 그대로). UNEXPLAINED·FOCUS는 문턱을 쓰지 않는다.
CHECK_THRESHOLD = {'ACCURACY': 'accuracy_min', 'MISSING': 'missing_max', 'TIME': 'time_must_drop'}

# 4.6 읽은 문턱 표. 판정 표의 순서마다. 판정 문장은 이 문턱 가운데 하나라도 기본값이면 표시를 붙인다.
READ_THRESHOLDS = {
    1: (),
    2: ('accuracy_min',),
    3: ('accuracy_min', 'missing_max'),
    4: ('accuracy_min', 'missing_max'),
    5: ('accuracy_min', 'missing_max', 'time_must_drop'),
    6: ('accuracy_min', 'missing_max', 'time_must_drop'),
    7: ('accuracy_min', 'missing_max', 'time_must_drop'),
    8: ('accuracy_min', 'missing_max', 'time_must_drop'),
}

# 4.6 표시 글자(classification.md 4.6과 같다): 공백 하나 + 괄호 글.
DEFAULT_MARK = ' (기본값 사용, 확인 필요)'

# 4.7 다음 할 일 표. (키, 문구). 표 순서가 todo 순서다. 언제 나오는지는 judge()가 표의 "나오는 때" 칸대로 정한다.
TODO_TABLE = (
    ('add_data', '기준 자료(사람이 만든 결과)를 reference/에, 결과 자료(AI 적용 후 결과)를 candidate/에 넣고 '
                 'case.yaml inputs에 파일 패턴과 키 열을 적는다.'),
    ('add_compare', 'case.yaml compare에 대조할 열과 허용 오차를 적는다.'),
    ('measure_time', '건당 처리 시간·검토 시간과 적용 전 시간(분)을 잰다.'),
    ('review_items', '검토할 항목 {items}건({breakdown})을 보고 설명이 되는지 적는다.'),
    ('add_group', '불일치가 한 유형에 몰렸는지 보려면 case.yaml inputs.group에 기준 자료의 유형 열 이름을 적는다.'),
    ('confirm_thresholds', '문턱에 기본값을 썼다({defaults}). 맞는지 확인하고 case.yaml thresholds에 적는다.'),
)

# 4.9 {breakdown}: 0이 아닌 것만 이 순서로 ', '로 잇는다. (항목 상태들, 이름).
BREAKDOWN_PARTS = (
    ((ITEM_MISMATCH, ITEM_MISSING), '불일치'),
    ((ITEM_EXTRA,), '추가 행'),
    ((ITEM_DUPLICATE,), '중복 행'),
)

# ---- T-014 점수표 (text-check.md 5절). judgment.md 표에 더하는 행이다. 기존 상수는 그대로다. ----

# 5.1 점수 문턱 기본값. scores가 있을 때만 thresholds·defaults에 든다(4.2 표 뒤).
SCORE_THRESHOLD_DEFAULTS = (('score_gap_max', '0'),)

# 5.2 점수 확인 표. (확인, 결과, 확인 문장 틀). 확인 순서는 judgment.md 4.4 다섯 뒤.
SCORE_CHECK_TABLE = (
    ('SCORE', 'revealed', '점수표에서 출처가 드러난 곳이 {findings}군데 있어 점수를 비교하지 않는다.'),
    ('SCORE', 'met', 'AI 결과 평균 {ai_mean}점({ai_ratings}개)이 사람 결과 평균 {human_mean}점({human_ratings}개)보다 '
                     '{score_gap_max}점 넘게 낮지 않다.'),
    ('SCORE', 'not_met', 'AI 결과 평균 {ai_mean}점({ai_ratings}개)이 사람 결과 평균 {human_mean}점({human_ratings}개)보다 '
                         '{score_gap_max}점 넘게 낮다.'),
)
# 5.2: 확인 문장이 문턱을 읽는 결과. revealed는 점수를 비교하지 않아 문턱을 읽지 않는다.
SCORE_CHECK_READS = {'revealed': (), 'met': ('score_gap_max',), 'not_met': ('score_gap_max',)}

# 5.2 문턱 0 문장 표. (문장, 근거 키, 결과, 틀). score_gap_max 값이 0이면(0, 0.0 모두) 5.2 확인 문장·5.3 판정 문장 대신 쓴다.
SCORE_ZERO_TABLE = (
    ('확인', 'SCORE', 'met', 'AI 결과 평균 {ai_mean}점({ai_ratings}개)이 사람 결과 평균 {human_mean}점({human_ratings}개)보다 낮지 않다.'),
    ('확인', 'SCORE', 'not_met', 'AI 결과 평균 {ai_mean}점({ai_ratings}개)이 사람 결과 평균 {human_mean}점({human_ratings}개)보다 낮다.'),
    ('판정', 'SCORE', 'not_met', 'AI 결과 평균 {ai_mean}점이 사람 결과 평균 {human_mean}점보다 낮으므로 보류.'),
)

# 5.3 판정 행. (자리, 근거 키, 확인 결과, 길, 판정, 판정 문장 틀). 자리 '4 뒤'는 judgment.md 4.3 순서 4 뒤·5 앞.
# 길: '모두' = 대조가 대조함이든 미검증이든, '점수표만' = 대조가 미검증이고 점수표가 있을 때(그 순서의 행 대신).
PATH_ALL, PATH_SCORES_ONLY, PATH_TABLE = '모두', '점수표만', '표 대조'
SCORE_VERDICT_ROWS = (
    ('4 뒤', 'SCORE', 'revealed', PATH_ALL, 'hold', '점수표에서 출처가 드러난 곳이 {findings}군데 있어 점수를 쓸 수 없으므로 보류.'),
    ('4 뒤', 'SCORE', 'not_met', PATH_ALL, 'hold',
     'AI 결과 평균 {ai_mean}점이 사람 결과 평균 {human_mean}점보다 {score_gap_max}점 넘게 낮으므로 보류.'),
    ('4 뒤', 'NOT_VERIFIED', REASON_NO_COMPARE_FIELDS, PATH_SCORES_ONLY, 'conditional',
     '점수는 문턱을 충족하나 대조할 열(case.yaml compare·checklist)이 없어 기준·결과 자료(case.yaml inputs)를 쓰지 않았으므로 조건부.'),
    ('5', 'TIME', 'not_measured', PATH_SCORES_ONLY, 'conditional', '점수는 문턱을 충족하나 시간을 재지 않아 줄었는지 알 수 없으므로 조건부.'),
    ('5', 'TIME', 'not_met', PATH_SCORES_ONLY, 'conditional',
     '점수는 문턱을 충족하나 처리·검토 시간 {total}분이 적용 전 {before}분보다 짧지 않으므로 조건부.'),
    ('8', 'PASS', None, PATH_SCORES_ONLY, 'pass', '점수가 문턱을 충족하고 조건부·보류 조건에 걸리지 않으므로 통과.'),
)

# 5.4 읽은 문턱. (자리, 근거 키, 확인 결과(None = 결과와 상관없음), 길, 문턱). 길 '표 대조' = 대조함 + 점수표.
SCORE_READ_THRESHOLDS = (
    ('4 뒤', 'SCORE', 'revealed', PATH_TABLE, ('accuracy_min', 'missing_max')),
    ('4 뒤', 'SCORE', 'not_met', PATH_TABLE, ('accuracy_min', 'missing_max', 'score_gap_max')),
    ('5~8', None, None, PATH_TABLE, ('accuracy_min', 'missing_max', 'time_must_drop', 'score_gap_max')),
    ('4 뒤', 'SCORE', 'revealed', PATH_SCORES_ONLY, ()),
    ('4 뒤', 'SCORE', 'not_met', PATH_SCORES_ONLY, ('score_gap_max',)),
    ('4 뒤', 'NOT_VERIFIED', REASON_NO_COMPARE_FIELDS, PATH_SCORES_ONLY, ('score_gap_max',)),
    ('5', 'TIME', None, PATH_SCORES_ONLY, ('time_must_drop', 'score_gap_max')),
    ('8', 'PASS', None, PATH_SCORES_ONLY, ('time_must_drop', 'score_gap_max')),
)

# 5.1: 점수표만이면 defaults에는 이 길이 읽는 문턱(5.4 점수표만 행의 문턱)만 적는다. accuracy_min·missing_max는 읽지 않는다.
SCORES_ONLY_READ = tuple(dict.fromkeys(k for _z, _k, _r, path, th in SCORE_READ_THRESHOLDS if path == PATH_SCORES_ONLY for k in th))

# 5.5 다음 할 일(더하는 것). judgment.md 4.7 표의 add_group 뒤, confirm_thresholds 앞.
SCORE_TODO_TABLE = (
    ('rescore_blind', '출처가 드러나지 않게 가림 키를 새로 붙이고 점수를 다시 매긴다.'),
)

# ---- T-015 혼동행렬 (confusion.md 5절). judgment.md·text-check.md 표에 더하는 행이다. 기존 상수는 그대로다. ----

# 5.1 혼동 문턱 기본값. confusion이 있을 때만 thresholds·defaults에 든다(점수 문턱 뒤).
CONFUSION_THRESHOLD_DEFAULTS = (('recall_min', '95'), ('precision_min', '95'))

# 5.2 혼동 확인 표. (확인, 결과, 확인 문장 틀). 확인 순서는 표에서 처음 나오는 순서이고 SCORE 뒤다.
CONFUSION_CHECK_TABLE = (
    ('RECALL', 'met', '관심 범주의 재현율이 모두 문턱 {recall_min}% 이상이다: {recall_all}.'),
    ('RECALL', 'not_met', '재현율이 문턱 {recall_min}%에 못 미치는 관심 범주가 있다: {recall_low}.'),
    ('RECALL', 'no_rows', '기준 자료에 행이 없는 관심 범주가 있어 재현율을 잴 수 없다: {no_rows}.'),
    ('PRECISION', 'met', '관심 범주의 정밀도가 모두 문턱 {precision_min}% 이상이다: {precision_all}.'),
    ('PRECISION', 'not_met', '정밀도가 문턱 {precision_min}%에 못 미치는 관심 범주가 있다: {precision_low}.'),
)
CONFUSION_CHECK_ORDER = tuple(dict.fromkeys(c for c, _r, _t in CONFUSION_CHECK_TABLE))
# 5.2: 확인 문장이 읽는 문턱. 결과와 상관없다.
CONFUSION_CHECK_READS = {'RECALL': ('recall_min',), 'PRECISION': ('precision_min',)}

# 5.3 판정 행. (자리, 근거 키, 확인 결과, 판정, 판정 문장 틀). '3 뒤' = judgment.md 4.3 순서 3 뒤·4 앞(RECALL 먼저),
# '7 뒤' = 순서 7 뒤·8(PASS) 앞.
CONFUSION_VERDICT_ROWS = (
    ('3 뒤', 'RECALL', 'not_met', 'hold', '관심 범주 {recall_low_classes}의 재현율이 문턱 {recall_min}%에 못 미치므로 보류.'),
    ('3 뒤', 'PRECISION', 'not_met', 'hold', '관심 범주 {precision_low_classes}의 정밀도가 문턱 {precision_min}%에 못 미치므로 보류.'),
    ('7 뒤', 'RECALL', 'no_rows', 'conditional',
     '정확도·누락률은 문턱을 충족하나 기준 자료에 관심 범주 {no_rows}의 행이 없어 재현율을 잴 수 없으므로 조건부.'),
)

# 5.4 읽은 문턱. (자리, 근거 키, 확인 결과, 바탕, 더하는 문턱). 바탕 '순서 3'·'순서 8' = 그 순서 행이 이 길에서 읽는 문턱,
# '그 행' = 걸린 행이 judgment.md 4.6·text-check.md 5.4대로 읽는 문턱. 자리 '4~8'은 순서 4, '4 뒤', 5~8의 모든 행이다.
BASE_ORDER_3, BASE_ORDER_8, BASE_ROW = '순서 3', '순서 8', '그 행'
CONFUSION_READ_THRESHOLDS = (
    ('3 뒤', 'RECALL', 'not_met', BASE_ORDER_3, ('recall_min',)),
    ('3 뒤', 'PRECISION', 'not_met', BASE_ORDER_3, ('recall_min', 'precision_min')),
    ('4~8', None, None, BASE_ROW, ('recall_min', 'precision_min')),
    ('7 뒤', 'RECALL', 'no_rows', BASE_ORDER_8, ('recall_min', 'precision_min')),
)

# 5.5 다음 할 일(더하는 것). judgment.md 4.7 표의 add_group 뒤(점수표가 있으면 rescore_blind 뒤), confirm_thresholds 앞.
CONFUSION_TODO_TABLE = (
    ('add_class_rows', '기준 자료에 관심 범주 {no_rows}의 행이 없다. case.yaml confusion.positive의 이름을 확인하고, '
     '맞으면 그 범주의 행을 기준 자료와 결과 자료에 넣어 재현율을 잰다.'),
)

EMPTY_VALUE = '(빈 칸)'  # 4.5: 빈 값은 (빈 칸)
SHOW_MAX = 64  # 4.5·metrics 4.7: 64자를 넘으면 자른다(자르는 모양은 범위 밖, show() 참고)


@dataclass(frozen=True)
class Times:
    """4.10 JudgmentTimes. 건당 분, decimal, 0 이상."""
    before: Decimal
    process: Decimal
    review: Decimal


# ---- 글 만들기 ----

_SLOT = re.compile(r'\{([a-z_]+)\}')


def fill(template: str, values: dict) -> str:
    """문장 틀의 {이름}을 값으로 바꾼다. 틀에 있는데 값이 없으면 oracle 내부 오류."""
    def one(m):
        name = m.group(1)
        if name not in values:
            raise OracleError(f'문장 틀의 {{{name}}} 값이 없다(oracle 내부 오류)')
        return values[name]
    return _SLOT.sub(one, template)


def show(text: str) -> str:
    """4.5: 이름·값 표시. 빈 값은 (빈 칸).

    metrics 4.7의 "제어 문자는 \\uXXXX, 64자를 넘으면 자르고 …"는 16진 대소문자, 어떤 글자가 제어 문자인지,
    "자"가 UTF-16 단위인지, 자른 뒤 길이가 64인지 65인지를 정하지 않았다. 그래서 그런 값은 범위 밖으로 멈춘다.
    """
    if text == '':
        return EMPTY_VALUE
    for ch in text:
        o = ord(ch)
        if o < 0x20 or 0x7F <= o <= 0x9F:
            raise OracleError(f'제어 문자 U+{o:04X}가 있는 이름·값의 표시 모양은 oracle 범위 밖')
    if len(utf16_units(text)) > SHOW_MAX:
        raise OracleError(f'{SHOW_MAX}자를 넘는 이름·값을 자르는 모양은 oracle 범위 밖')
    return text


def net_decimal_text(x: Decimal, what: str) -> str:
    """decimal을 InvariantCulture 글로(3.3·4.5). 지수 꼴 없이 자릿수를 그대로 쓴다."""
    if not _fits(x):
        raise OracleError(f'{what}를 decimal로 그대로 담을 수 없다 (oracle 범위 밖)')
    if x.is_signed() and x == 0:
        raise OracleError(f'{what}가 음수 0이다. 글 모양을 스펙이 정하지 않았다 (oracle 범위 밖)')
    return format(x, 'f')


# ---- 문턱 (4.2) ----

def effective_thresholds(case: Case):
    """(값 글 dict, 기본값을 쓴 키 목록). 값 글: accuracy_min·missing_max는 case.yaml 글 그대로, time_must_drop은 'true'·'false'."""
    t = case.thresholds
    given = {
        'accuracy_min': t.accuracy_min_text if t else None,
        'missing_max': t.missing_max_text if t else None,
        'time_must_drop': (None if t is None or t.time_must_drop is None else ('true' if t.time_must_drop else 'false')),
    }
    table = THRESHOLD_DEFAULTS
    if case.scores is not None:  # T-014 text-check.md 5.1: scores가 있을 때만.
        given['score_gap_max'] = t.score_gap_max_text if t else None
        table = THRESHOLD_DEFAULTS + SCORE_THRESHOLD_DEFAULTS
    if case.confusion is not None:  # T-015 confusion.md 5.1: confusion이 있을 때만, 점수 문턱 뒤.
        given['recall_min'] = t.recall_min_text if t else None
        given['precision_min'] = t.precision_min_text if t else None
        table = table + CONFUSION_THRESHOLD_DEFAULTS
    values, defaults = {}, []
    for key, default in table:
        if given[key] is None:
            values[key] = default
            defaults.append(key)
        else:
            values[key] = given[key]
    return values, defaults


# ---- 확인 다섯 (4.4) ----

def _cell(row, column: str) -> str:
    return row.values[row.header.index(column)]


def _focus(case: Case, run: CompareRun, bad_rows: list):
    """4.8 규칙 1~5(M-A). (결과, 문장 값 dict). 값은 기준 행의 칸만 본다. 결과 자료·추가 행·중복 행은 보지 않는다."""
    bad = len(bad_rows)
    if bad < FOCUS_MIN:
        return 'few', {}
    group = case.inputs.group
    if group is None:
        return 'off', {}
    # 대조가 기준 파일마다 이 열이 머리 행에 꼭 한 번 있는지 이미 봤다(metrics.md 12.3). 그래서 위치가 하나다.
    ref_values = [_cell(r, group) for r in run.reference_rows]
    if len(set(ref_values)) == 1:
        return 'one_value', {'group': show(group)}
    bad_values = {_cell(r, group) for r in bad_rows}
    if len(bad_values) == 1:
        value = next(iter(bad_values))
        rows_in = sum(1 for v in ref_values if v == value)
        return 'found', {'group': show(group), 'value': show(value), 'rows_in': str(rows_in),
                         'rows_out': str(len(ref_values) - rows_in)}
    return 'spread', {'group': show(group)}


def _time(values: dict, times: Times | None):
    if values['time_must_drop'] == 'false':
        return 'off', {}  # 받아도 보지 않는다.
    if times is None:
        return 'not_measured', {}
    texts = {}
    for name, slot_name in (('before', 'before'), ('process', 'process'), ('review', 'review')):
        x = getattr(times, name)
        if not isinstance(x, Decimal) or not x.is_finite():
            raise OracleError(f'시간 {name}이 decimal이 아니다 (oracle 범위 밖)')
        if x < 0:
            raise OracleError(f'시간 {name}이 음수다 (ArgumentOutOfRangeException 입력, 범위 밖)')
        texts[slot_name] = net_decimal_text(x, f'시간 {name}')
    try:
        with localcontext(EXACT):
            total = times.process + times.review
    except DecimalException:
        raise OracleError('시간 합 셈에서 반올림이 생겼다 (oracle 범위 밖)') from None
    if total > DEC_MAX:
        # 4.4: 결과는 not_met이지만 문장의 {total}을 스펙이 정하지 않았다.
        raise OracleError('시간 합이 decimal 범위를 넘는다. 문장의 {total} 모양을 스펙이 정하지 않았다 (oracle 범위 밖)')
    texts['total'] = net_decimal_text(total, '시간 합')
    return ('met' if total < times.before else 'not_met'), texts  # 같으면 줄지 않았다.


def breakdown(items: list) -> str:
    """4.9 {breakdown}: 불일치·추가 행·중복 행 가운데 0이 아닌 것을 이 순서로 '이름 N건'을 ', '로 잇는다."""
    parts = []
    for statuses, name in BREAKDOWN_PARTS:
        n = sum(1 for it in items if it['status'] in statuses)
        if n:
            parts.append(f'{name} {n}건')
    return ', '.join(parts)


def check_consistent(case: Case, run: CompareRun) -> None:
    """5절(T-013): 대조 결과가 이 정의에서 나온 것인지 본다. 아니면 ArgumentException(부르는 쪽 잘못)이다.

    케이스 이름은 oracle의 대조 결과에 없다(compare 부분만 있다). 그래서 이름 비교는 C# 쪽만 한다.
    """
    body = run.body
    if case.inputs is None:
        ok = body['status'] == STATUS_NOT_VERIFIED and body['reason'] == REASON_NO_INPUTS
    elif not case.compare and case.checklist is None:  # T-014: 체크리스트만 있어도 대조함이다.
        ok = body['status'] == STATUS_NOT_VERIFIED and body['reason'] == REASON_NO_COMPARE_FIELDS
    else:
        ok = (body['status'] == STATUS_MEASURED and body['key'] == case.inputs.key
              and [f['field'] for f in body['fields']] == [f.field for f in case.compare])
        group = case.inputs.group
        if ok and group is not None:
            ok = all(r.header.count(group) == 1 for r in run.reference_rows)
        # T-014 text-check.md 5.6: 체크리스트 문장 열·항목 이름(순서)이 정의와 같다.
        cl = case.checklist
        got = body.get('checklist')
        if cl is None:
            ok = ok and got is None
        else:
            ok = ok and got is not None and got['text'] == cl.text \
                and [e['item'] for e in got['entries']] == [i.item for i in cl.items]
    # T-014 5.6: 점수표 집계는 정의에 scores가 있을 때만 있다.
    ok = ok and ((run.scores is None) == (case.scores is None))
    # T-015 confusion.md 5.6: 혼동행렬은 정의에 confusion이 있을 때만 있고, 범주 필드·관심 범주(순서)가 정의와 같다.
    got = body.get('confusion')
    if case.confusion is None:
        ok = ok and got is None
    else:
        ok = ok and body['status'] == STATUS_MEASURED and got is not None and got['field'] == case.confusion.field \
            and [p['class'] for p in got['positive']] == list(case.confusion.positive)
    if not ok:
        raise OracleError('대조 결과가 정의와 맞지 않는다 (ArgumentException 입력, 범위 밖)')


def judge(case: Case, run: CompareRun, times: Times | None = None, unexplained_keys=None) -> dict:
    """판정(4.1~4.10). 순수 함수. 반환: judgment.json의 schema_version·case 뒤 속성들(dict, 출력 순서)."""
    check_consistent(case, run)
    values, defaults = effective_thresholds(case)
    body = run.body
    scores_only = body['status'] == STATUS_NOT_VERIFIED and run.scores is not None  # T-014 5.3 길 "점수표만"
    if scores_only:  # T-014 5.1: 이 길이 읽지 않는 문턱은 기본값 표시·confirm_thresholds에 넣지 않는다.
        defaults = [k for k in defaults if k in SCORES_ONLY_READ]
    thresholds_json = {
        'accuracy_min': JsonNumber(values['accuracy_min']),
        'missing_max': JsonNumber(values['missing_max']),
        'time_must_drop': values['time_must_drop'] == 'true',
    }
    scores = run.scores
    if scores is not None:  # T-014 text-check.md 5.1·3.3: thresholds 끝에.
        thresholds_json['score_gap_max'] = JsonNumber(values['score_gap_max'])
    conf = body.get('confusion')
    if conf is not None:  # T-015 confusion.md 3.2: 점수 문턱 뒤.
        thresholds_json['recall_min'] = JsonNumber(values['recall_min'])
        thresholds_json['precision_min'] = JsonNumber(values['precision_min'])

    def mark(text: str, keys) -> str:
        return text + DEFAULT_MARK if any(k in defaults for k in keys) else text

    todo_table = _todo_table(scores is not None, conf is not None)
    todo_when = {k: False for k, _ in todo_table}
    slot = {'defaults': ', '.join(f'{k} {values[k]}' for k in defaults)}
    score_result = None
    if scores is not None:
        score_result, score_slot = _score(values, scores)
        slot.update(score_slot)
        todo_when['rescore_blind'] = score_result == 'revealed'
    score_sentences = {(c, r): t for c, r, t in SCORE_CHECK_TABLE}
    # T-014 5.2 문턱 0 문장: score_gap_max 값이 0이면 "{score_gap_max}점 넘게"가 없는 틀.
    zero = {(w, k, r): t for w, k, r, t in SCORE_ZERO_TABLE} \
        if scores is not None and Decimal(values['score_gap_max']) == 0 else {}

    def score_check() -> dict:
        template = zero.get(('확인', 'SCORE', score_result), score_sentences[('SCORE', score_result)])
        return {'rule': 'SCORE', 'result': score_result,
                'reason': mark(fill(template, slot), SCORE_CHECK_READS[score_result])}

    def score_row(rule: str, result: str) -> tuple:
        """5.3 판정 행. SCORE 행은 문턱이 0이면 문턱 0 문장 틀(판정 문장 틀만 바꾼다)."""
        row = next(r for r in SCORE_VERDICT_ROWS if r[1] == rule and r[2] == result)
        return row[:5] + (zero.get(('판정', rule, result), row[5]),)

    if scores_only:
        # T-014 5.3 길 "점수표만": 대조 확인 넷(ACCURACY·MISSING·UNEXPLAINED·FOCUS)이 없다. TIME과 SCORE만.
        if unexplained_keys is not None:
            raise OracleError('점수표만인데 사람 입력(검토 항목 키)을 받았다. 검토 항목이 없다 (oracle 범위 밖)')
        time_result, time_slot = _time(values, times)
        slot.update(time_slot)
        sentences = {(c, r): t for c, r, t in CHECK_TABLE}
        time_text = fill(sentences[('TIME', time_result)], slot)
        checks = [{'rule': 'TIME', 'result': time_result, 'reason': mark(time_text, (CHECK_THRESHOLD['TIME'],))},
                  score_check()]
        reads = {(z, k, res): th for z, k, res, path, th in SCORE_READ_THRESHOLDS if path == PATH_SCORES_ONLY}
        # 5.3 점수표만 순서: SCORE 보류 둘 → 대조할 열 없음(no_compare_fields) 조건부 → TIME 조건부 → PASS.
        unused_inputs = body['reason'] == REASON_NO_COMPARE_FIELDS
        if score_result in ('revealed', 'not_met'):
            row = score_row('SCORE', score_result)
            read = reads[('4 뒤', 'SCORE', score_result)]
        elif unused_inputs:
            row = score_row('NOT_VERIFIED', REASON_NO_COMPARE_FIELDS)
            read = reads[('4 뒤', 'NOT_VERIFIED', REASON_NO_COMPARE_FIELDS)]
        elif time_result in ('not_measured', 'not_met'):
            row = score_row('TIME', time_result)
            read = reads[('5', 'TIME', None)]
        else:
            row = score_row('PASS', None)
            read = reads[('8', 'PASS', None)]
        _zone, rule, _result, _path, verdict, template = row
        reason = mark(fill(template, slot), read)
        todo_when['measure_time'] = time_result == 'not_measured'
        todo_when['add_compare'] = unused_inputs  # 5.5: inputs 자료를 쓰려면 대조할 열을 적는다.
    elif body['status'] == STATUS_NOT_VERIFIED:
        if times is not None or unexplained_keys is not None:
            # 4.7 "미검증이고 time_must_drop이 true"가 시간을 받았을 때도 measure_time인지 스펙이 정하지 않았다.
            raise OracleError('미검증인데 시간·사람 입력을 받았다. 결과를 스펙이 정하지 않았다 (oracle 범위 밖)')
        row = next((r for r in VERDICT_TABLE if r[1] == 'NOT_VERIFIED' and r[2] == body['reason']), None)
        if row is None:
            raise OracleError(f"미검증 이유 {body['reason']!r}가 판정 표에 없다")
        order, rule, _result, verdict, template = row
        reason = mark(template, READ_THRESHOLDS[order])
        checks = []
        todo_when['add_data'] = body['reason'] == 'no_inputs'
        todo_when['add_compare'] = True
        todo_when['measure_time'] = values['time_must_drop'] == 'true'
    else:
        counts = body['counts']
        bad_items = [it for it in body['items'] if it['status'] in (ITEM_MISMATCH, ITEM_MISSING)]
        review_items = [it for it in body['items'] if it['status'] in REVIEW_STATUSES]
        bad = len(bad_items)
        if bad != counts['mismatch'] + counts['missing']:
            raise OracleError('불일치 수가 셈과 다르다(oracle 내부 오류)')
        if sum(1 for it in review_items if it['status'] == ITEM_EXTRA) != counts['extra']:
            raise OracleError('추가 행 수가 셈과 다르다(oracle 내부 오류)')
        by_key = {r.key: r for r in run.reference_rows}
        bad_rows = [by_key[it['key']] for it in bad_items]
        slot.update({
            'accuracy_pct': body['accuracy_pct'].text, 'missing_rate_pct': body['missing_rate_pct'].text,
            'match': str(counts['match']), 'missing': str(counts['missing']), 'reference': str(counts['reference']),
            'accuracy_min': values['accuracy_min'], 'missing_max': values['missing_max'], 'bad': str(bad),
            'items': str(len(review_items)), 'breakdown': breakdown(review_items),
        })
        results = {}
        try:
            with localcontext(EXACT):
                # 셈으로 비교한다(4.1 4, 4.4). 나눗셈을 쓰지 않는다.
                results['ACCURACY'] = 'met' if Decimal(counts['match'] * 100) >= \
                    Decimal(values['accuracy_min']) * counts['reference'] else 'not_met'
                results['MISSING'] = 'met' if Decimal(counts['missing'] * 100) <= \
                    Decimal(values['missing_max']) * counts['reference'] else 'not_met'
        except DecimalException:
            raise OracleError('문턱 셈에서 반올림이 생겼다 (oracle 범위 밖)') from None

        # 4.9 설명 안 되는 검토 항목. 사람 입력: None(아직 안 봄) 또는 검토 항목 키 목록.
        if unexplained_keys is not None:
            keys = list(unexplained_keys)
            review_keys = {it['key'] for it in review_items}
            if len(set(keys)) != len(keys) or not set(keys) <= review_keys:
                raise OracleError('사람 입력 키가 검토 항목이 아니거나 중복이다 (ArgumentException 입력, 범위 밖)')
            slot['unexplained'] = str(len(keys))
        if not review_items:
            results['UNEXPLAINED'] = 'none'
        elif unexplained_keys is None:
            results['UNEXPLAINED'] = 'not_reviewed'
        elif not unexplained_keys:
            results['UNEXPLAINED'] = 'explained'
        else:
            results['UNEXPLAINED'] = 'found'

        results['TIME'], time_slot = _time(values, times)
        slot.update(time_slot)
        results['FOCUS'], focus_slot = _focus(case, run, bad_rows)
        slot.update(focus_slot)

        conf_results, no_rows = {}, []
        if conf is not None:  # T-015 confusion.md 5.2.
            conf_results, conf_slot, no_rows = _confusion(values, conf)
            slot.update(conf_slot)
            todo_when['add_class_rows'] = bool(no_rows)

        sentences = {(c, r): t for c, r, t in CHECK_TABLE}
        checks = []
        for name in CHECK_ORDER:
            text = fill(sentences[(name, results[name])], slot)
            keys = (CHECK_THRESHOLD[name],) if name in CHECK_THRESHOLD else ()
            checks.append({'rule': name, 'result': results[name], 'reason': mark(text, keys)})
        if scores is not None:
            checks.append(score_check())  # T-014 5.2: 다섯 뒤.
        conf_sentences = {(c, r): t for c, r, t in CONFUSION_CHECK_TABLE}
        for name in (CONFUSION_CHECK_ORDER if conf is not None else ()):  # T-015 5.2: SCORE 뒤.
            text = fill(conf_sentences[(name, conf_results[name])], slot)
            checks.append({'rule': name, 'result': conf_results[name], 'reason': mark(text, CONFUSION_CHECK_READS[name])})

        # 4.3: 순서대로 보고 처음 걸리는 행. PASS는 어느 행에도 걸리지 않을 때다.
        # T-014 5.3: 점수표가 있으면 순서 4 뒤에 SCORE 두 행. 5~8행의 읽은 문턱에 score_gap_max(5.4).
        # T-015 confusion.md 5.3: 혼동행렬이 있으면 순서 3 뒤에 RECALL·PRECISION 보류, 순서 7 뒤에 RECALL no_rows 조건부.
        def row_read(order: int) -> tuple:
            """그 순서 행이 이 길(표 대조)에서 읽는 문턱(judgment.md 4.6, text-check.md 5.4)."""
            if scores is not None and order >= 5:
                return next(th for z, k, res, path, th in SCORE_READ_THRESHOLDS if path == PATH_TABLE and z == '5~8')
            return READ_THRESHOLDS[order]

        def plus(read: tuple, zone: str, rule=None, result=None) -> tuple:
            """T-015 5.4: 혼동행렬이 있으면 더하는 문턱."""
            if conf is None:
                return read
            add = next(th for z, k, res, _base, th in CONFUSION_READ_THRESHOLDS if z == zone and k == rule and res == result)
            return read + add

        def conf_hit(zone: str, rule: str, result: str, read: tuple) -> tuple:
            row = next(x for x in CONFUSION_VERDICT_ROWS if x[0] == zone and x[1] == rule and x[2] == result)
            return (rule, row[3], row[4], plus(read, zone, rule, result))

        hit = None
        for r in VERDICT_TABLE:
            if r[1] == 'NOT_VERIFIED':
                continue
            if r[0] == 4 and conf is not None:
                failed = next((n for n in CONFUSION_CHECK_ORDER if conf_results[n] == 'not_met'), None)
                if failed is not None:
                    hit = conf_hit('3 뒤', failed, 'not_met', READ_THRESHOLDS[3])
                    break
            if r[0] == 5 and scores is not None and score_result in ('revealed', 'not_met'):
                row = score_row('SCORE', score_result)
                read = next(th for z, k, res, path, th in SCORE_READ_THRESHOLDS
                            if path == PATH_TABLE and k == 'SCORE' and res == score_result)
                hit = (row[1], row[4], row[5], plus(read, '4~8'))
                break
            if r[1] == 'PASS' and conf is not None and conf_results['RECALL'] == 'no_rows':
                hit = conf_hit('7 뒤', 'RECALL', 'no_rows', row_read(8))
                break
            if r[1] == 'PASS' or results[r[1]] == r[2]:
                hit = (r[1], r[3], r[4], plus(row_read(r[0]), '4~8') if r[0] >= 4 else row_read(r[0]))
                break
        rule, verdict, template, read = hit
        reason = mark(fill(template, slot), read)
        todo_when['measure_time'] = results['TIME'] == 'not_measured'
        todo_when['review_items'] = results['UNEXPLAINED'] == 'not_reviewed'
        todo_when['add_group'] = results['FOCUS'] == 'off'

    todo_when['confirm_thresholds'] = bool(defaults)
    todo = [{'key': k, 'text': fill(t, slot)} for k, t in todo_table if todo_when[k]]
    return {
        'verdict': verdict, 'rule': rule, 'reason': reason, 'thresholds': thresholds_json,
        'defaults': defaults, 'checks': checks, 'todo': todo,
    }


def _todo_table(with_scores: bool, with_confusion: bool = False) -> tuple:
    """4.7 표. 점수표가 있으면 add_group 뒤에 T-014 5.5 행을, 혼동행렬이 있으면 그 뒤에 T-015 5.5 행을 넣는다."""
    at = [k for k, _ in TODO_TABLE].index('add_group') + 1
    extra = (SCORE_TODO_TABLE if with_scores else ()) + (CONFUSION_TODO_TABLE if with_confusion else ())
    return TODO_TABLE[:at] + extra + TODO_TABLE[at:]


def _class_name(value: str) -> str:
    return show(value)


def recall_entry(p: dict) -> str:
    """T-015 5.2 {recall_all}·{recall_low}의 한 범주: `<범주> <재현율>%(<맞힘>/<기준 행>, 놓침 <N>건)`."""
    return f"{_class_name(p['class'])} {p['recall_pct'].text}%({p['hit']}/{p['reference']}, 놓침 {p['miss']}건)"


def precision_entry(p: dict) -> str:
    """T-015 5.2 {precision_all}·{precision_low}의 한 범주. 결과 쪽에서 그 범주로 적은 행이 없으면 `<범주> 결과 0건`."""
    if p['predicted'] == 0:
        return f"{_class_name(p['class'])} 결과 0건"
    return f"{_class_name(p['class'])} {p['precision_pct'].text}%({p['hit']}/{p['predicted']}, 오판 {p['false_alarm']}건)"


def _confusion(values: dict, conf: dict):
    """T-015 confusion.md 5.2 RECALL·PRECISION. (결과 dict, 문장 값, 기준 행이 없는 관심 범주). 셈으로 비교한다."""
    recall_low, precision_low, no_rows = [], [], []
    try:
        with localcontext(EXACT):
            recall_min, precision_min = Decimal(values['recall_min']), Decimal(values['precision_min'])
            for p in conf['positive']:
                if p['reference'] == 0:
                    no_rows.append(p)
                elif Decimal(p['hit'] * 100) < recall_min * p['reference']:
                    recall_low.append(p)
                if p['predicted'] > 0 and Decimal(p['hit'] * 100) < precision_min * p['predicted']:
                    precision_low.append(p)
    except DecimalException:
        raise OracleError('혼동 문턱 셈에서 반올림이 생겼다 (oracle 범위 밖)') from None
    results = {
        'RECALL': 'not_met' if recall_low else ('no_rows' if no_rows else 'met'),
        'PRECISION': 'not_met' if precision_low else 'met',
    }
    slot = {
        'recall_min': values['recall_min'], 'precision_min': values['precision_min'],
        'recall_all': ', '.join(recall_entry(p) for p in conf['positive'] if p['reference'] > 0),
        'recall_low': ', '.join(recall_entry(p) for p in recall_low),
        'recall_low_classes': ', '.join(_class_name(p['class']) for p in recall_low),
        'precision_all': ', '.join(precision_entry(p) for p in conf['positive']),
        'precision_low': ', '.join(precision_entry(p) for p in precision_low),
        'precision_low_classes': ', '.join(_class_name(p['class']) for p in precision_low),
        'no_rows': ', '.join(_class_name(p['class']) for p in no_rows),
    }
    return results, slot, no_rows


def _score(values: dict, scores: dict):
    """T-014 text-check.md 5.2 SCORE. (결과, 문장 값). 셈으로 비교한다(나눗셈 없음)."""
    by = {s['source']: s for s in scores['sources']}
    ai, human = by['ai'], by['human']
    slot = {'findings': str(len(scores['blind']['findings'])),
            'ai_mean': ai['mean'].text, 'human_mean': human['mean'].text,
            'ai_ratings': str(ai['ratings']), 'human_ratings': str(human['ratings']),
            'score_gap_max': values['score_gap_max']}
    if scores['blind']['status'] == 'revealed':
        return 'revealed', slot
    try:
        with localcontext(EXACT):
            left = Decimal(ai['sum'] * human['ratings']) + Decimal(values['score_gap_max']) * ai['ratings'] * human['ratings']
            right = Decimal(human['sum'] * ai['ratings'])
    except DecimalException:
        raise OracleError('점수 셈에서 반올림이 생겼다 (oracle 범위 밖)') from None
    return ('met' if left >= right else 'not_met'), slot


def judgment_value(case: Case, run: CompareRun, times: Times | None = None, unexplained_keys=None) -> dict:
    return {'schema_version': SCHEMA_VERSION, 'case': case.name, **judge(case, run, times, unexplained_keys)}


def judgment_bytes(case_dir: Path, times: Times | None = None, unexplained_keys=None) -> bytes:
    """케이스 폴더 → judgment.json 바이트. 대조 오류 입력은 OracleError(판정하지 않는다, 5절)."""
    case = parse_case(_case_yaml(case_dir), CASE_FILE)
    run = compare_run(case_dir, case)
    return render(judgment_value(case, run, times, unexplained_keys))


def main(argv: list) -> int:
    utf8_stdio()
    if len(argv) != 1:
        print('사용: judge.py <케이스 폴더>', file=sys.stderr)
        return 2
    try:
        out = judgment_bytes(Path(argv[0]))
    except OracleError as e:
        print(f'oracle: {e}', file=sys.stderr)
        return 2
    sys.stdout.buffer.write(out)
    sys.stdout.buffer.flush()
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
