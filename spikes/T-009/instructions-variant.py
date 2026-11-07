#!/usr/bin/env python3
# T-009 스파이크(머지 대상 아님). 지시문(docs/README-instructions.md)의 제안 문구를 clone에만 넣는다.
# 지시문은 사람 결정 뒤 리드가 고친다(카드 폴더 범위). 저장소의 지시문은 바꾸지 않는다.
# 바꾸는 곳(제안, docs/spec/signoff-final.md 6절): 5절 폴더 트리 2곳, M1 G1, 12절 제목·첫 문단 2곳, 12절 표 1행, 표 아래 문장.
# 바꾸지 않는 곳: 12.1 블록, 11절 T-004~T-006 카드 문구, 14절 8, M2 G2(리드 질문), 3.1 표.
# 사용: instructions-variant.py <저장소>
import sys
from pathlib import Path

ROW = ('| `signoff-final` | 기록 파일에서 기준 구간을 골라 값을 내고, 보고서를 써서 사람이 서명해 제출한다 | 9 | '
       '파일 정리·안정 기록 구간·값 계산·보고서 채우기 = ②, 흔들린 기록 구간 = ②+④, 해석 문장 = ③+④, '
       '최종 확인·서명·제출 = ④(서명 단계, `human_signoff: yes`, 게이트 없음) | '
       'reference 20건 / candidate 20건, 불일치 4건·누락 1건 심어 둠 → 정확도 80%, 누락률 5% (문턱 95%·2% 미달. 판정은 판정 카드에서) |')

MIXED = ('| `mixed-exceptions` | 자유 형식 메일을 접수해 분류하고 답변 초안을 쓴다 | 5 | 접수 기록 = ②, 분류·답변 = ③(입력 양식 표준화 제안), 발송 = ④ | '
         '자료 없음 → 전 항목 "미검증", 평가서에 측정 방법만 |')

CHANGES = [
    ('├─ examples/         공개 합성 예제 3종 = 테스트의 정답 (12절)', '├─ examples/         공개 합성 예제 4종 = 테스트의 정답 (12절)'),
    ('│  ├─ form-fill/\n│  └─ mixed-exceptions/\n', '│  ├─ form-fill/\n│  ├─ mixed-exceptions/\n│  └─ signoff-final/\n'),
    ('G1: 공개 예제 3종의 출력이 `expected/`와 일치.', 'G1: 공개 예제 4종의 출력이 `expected/`와 일치.'),
    ('## 12. 공개 예제 3종 (`examples/`) — 테스트의 정답', '## 12. 공개 예제 4종 (`examples/`) — 테스트의 정답'),
    ('세 예제는 구조가 서로 다르게 고른다.', '네 예제는 구조가 서로 다르게 고른다.'),
    ('`answers.yaml`(질문 9개 답)', '`answers.yaml`(답 10개)'),
    (MIXED + '\n', MIXED + '\n' + ROW + '\n'),
    ('예제 3종의 `expected/`에는 위 표의 숫자와 분류가 그대로 들어간다.', '예제 4종의 `expected/`에는 위 표의 숫자와 분류가 그대로 들어간다.'),
]


def main(repo):
    path = Path(repo) / 'docs/README-instructions.md'
    text = path.read_text(encoding='utf-8')
    for old, new in CHANGES:
        assert text.count(old) == 1, old
        text = text.replace(old, new)
        print(f'RESULT instructions {old.splitlines()[0][:40]} -> changed')
    path.write_text(text, encoding='utf-8')


if __name__ == '__main__':
    main(sys.argv[1])
