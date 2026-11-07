# T-008 스파이크(머지 대상 아님). answers.yaml·case.yaml의 흐름 매핑 answers 줄 끝에 human_signoff를 더한다.
# 사용: python3 -I add-signoff.py <입력> <출력> [yes로 둘 단계 번호...]
# "    answers: {...}" 줄만 바꾼다. 그 밖의 줄(주석, inputs, compare, thresholds)은 바이트 그대로다.
# 이미 human_signoff가 있는 줄이 있으면 멈춘다.
import re
import sys

source, target, *yes = sys.argv[1:]
yes_steps = {int(n) for n in yes}
pattern = re.compile(rb"^(    answers: \{.*)\}$")
data = open(source, "rb").read()
assert data.endswith(b"\n") and b"\r" not in data, source
lines = data.split(b"\n")
step = 0
out = []
for line in lines:
    if line.startswith(b"  - name: "):
        step += 1
    match = pattern.match(line)
    if match:
        assert b"human_signoff" not in line, f"{source}: 이미 human_signoff가 있다"
        value = b"yes" if step in yes_steps else b"no"
        line = match.group(1) + b", human_signoff: " + value + b"}"
    out.append(line)
changed = sum(1 for a, b in zip(lines, out) if a != b)
open(target, "wb").write(b"\n".join(out))
print(f"RESULT add-signoff {source.split('/')[-2]}/{source.split('/')[-1]} steps={step} changed={changed} yes={sorted(yes_steps)}")
