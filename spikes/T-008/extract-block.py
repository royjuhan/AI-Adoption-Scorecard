# T-008 스파이크(머지 대상 아님). 스펙 문서의 코드 블록 하나를 파일로 꺼낸다.
# 사용: python3 -I extract-block.py <문서> <제목 줄> <블록 번호(0부터)> <출력 파일>
# 규칙은 테스트의 SpecDocument.CodeBlocks와 같다: 제목 줄(오른쪽 공백 뗌)부터, 펜스 밖에서 '#'로 시작하는 줄 전까지.
# 블록 줄마다 LF를 붙인다(끝에 LF).
import sys

doc, heading, index, out = sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4]
lines = open(doc, encoding="utf-8").read().split("\n")
start = [i for i, l in enumerate(lines) if l.rstrip() == heading]
assert len(start) >= 1, f"제목이 없다: {heading}"
blocks, current = [], None
for line in lines[start[0] + 1:]:
    if current is None and line.startswith("#"):
        break
    if line.startswith("```"):
        if current is None:
            current = []
        else:
            blocks.append("".join(l + "\n" for l in current))
            current = None
        continue
    if current is not None:
        current.append(line)
open(out, "w", encoding="utf-8", newline="\n").write(blocks[index])
print(f"BLOCK extract {heading} [{index}] lines={blocks[index].count(chr(10))} blocks={len(blocks)}")
