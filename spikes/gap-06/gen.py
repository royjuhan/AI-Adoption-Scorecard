#!/usr/bin/env python3
"""빈 곳 6(혼동행렬) 합성 예제 생성기. 스파이크. 남긴다(docs/cases-public/README.md 4절 2).

docs/spec/confusion.md 7.2 계획을 그대로 옮겼다. synthetic-examples.md 4.2 SplitMix64·NextBelow.
쓰기:
  python3 -I spikes/gap-06/gen.py --find-seed        # 조건을 만족하는 가장 작은 시드
  python3 -I spikes/gap-06/gen.py <시드> <폴더>      # 폴더에 reference/parts.csv·candidate/parts.csv
값은 모두 지어낸 것이다(지시문 2절 8·10). 표준 라이브러리만 쓴다.
"""
import sys
from pathlib import Path

MASK = (1 << 64) - 1
ROWS = 40
KEY_COL, KEY_PREFIX, KEY_DIGITS = 'part_id', 'p', 2
GROUP_COL, GROUPS = 'line', ['A', 'B']
CLASS_COL = 'result'
CLASS_LIST = ['정상'] * 8 + ['긁힘', '찍힘']
MISS = ('찍힘', '정상')  # 놓칠 범주 → 결과
FALSE_ALARM = ('정상', '긁힘')  # 오판: 이 범주 행 하나 → 결과
OMIT = '정상'  # 결과에서 뺄 행의 범주(오판 행은 빼고 고른다)


class SplitMix64:
    def __init__(self, seed: int):
        self.s = seed & MASK

    def next(self) -> int:
        self.s = (self.s + 0x9E3779B97F4A7C15) & MASK
        z = self.s
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & MASK
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & MASK
        return z ^ (z >> 31)

    def below(self, n: int) -> int:
        return (self.next() * n) >> 64


def generate(seed: int):
    g = SplitMix64(seed)
    keys = [f'{KEY_PREFIX}{i + 1:0{KEY_DIGITS}d}' for i in range(ROWS)]
    group, cls = [], []
    for _ in range(ROWS):  # 1. 행마다 유형, 범주(이 순서)
        group.append(GROUPS[g.below(len(GROUPS))])
        cls.append(CLASS_LIST[g.below(len(CLASS_LIST))])
    pool = [i for i in range(ROWS) if cls[i] == MISS[0]]  # 2. 놓칠 행
    if not pool:
        return None
    miss_row = pool[g.below(len(pool))]
    pool = [i for i in range(ROWS) if cls[i] == FALSE_ALARM[0]]  # 3. 오판 행
    alarm_row = pool[g.below(len(pool))]
    pool = [i for i in range(ROWS) if cls[i] == OMIT and i != alarm_row]  # 4. 누락 행
    omit_row = pool[g.below(len(pool))]
    result = list(cls)
    result[miss_row] = MISS[1]
    result[alarm_row] = FALSE_ALARM[1]
    order = [i for i in range(ROWS) if i != omit_row]  # 5. 결과 행 순서: Fisher–Yates
    for k in range(len(order) - 1, 0, -1):
        j = g.below(k + 1)
        order[k], order[j] = order[j], order[k]
    ref = [[KEY_COL, GROUP_COL, CLASS_COL]] + [[keys[i], group[i], cls[i]] for i in range(ROWS)]
    cand = [[KEY_COL, CLASS_COL]] + [[keys[i], result[i]] for i in order]
    return cls, ref, cand


def ok(seed: int) -> bool:
    """시드 조건(confusion.md 7.2): 긁힘이 꼭 4행, 찍힘이 2행 이상."""
    out = generate(seed)
    if out is None:
        return False
    cls = out[0]
    return cls.count('긁힘') == 4 and cls.count('찍힘') >= 2


def csv_bytes(rows) -> bytes:
    for r in rows:
        for c in r:
            if not c or c != c.strip() or any(x in c for x in ',"\n\r\t'):
                raise SystemExit(f'칸 규칙 위반: {c!r}')
    return ''.join(','.join(r) + '\n' for r in rows).encode('utf-8')


def main(argv):
    if argv == ['--find-seed']:
        seed = next(s for s in range(100000) if ok(s))
        print(seed)
        return 0
    seed, folder = int(argv[0]), Path(argv[1])
    if not ok(seed):
        raise SystemExit(f'시드 {seed}는 조건을 만족하지 않는다')
    _cls, ref, cand = generate(seed)
    (folder / 'reference').mkdir(parents=True, exist_ok=True)
    (folder / 'candidate').mkdir(parents=True, exist_ok=True)
    (folder / 'reference/parts.csv').write_bytes(csv_bytes(ref))
    (folder / 'candidate/parts.csv').write_bytes(csv_bytes(cand))
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
