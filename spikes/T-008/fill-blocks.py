# T-008 스파이크(머지 대상 아님). 스펙 블록에 human_signoff: no를 넣고 case.yaml SHA-256 줄을 맞춘다.
# 사용: python3 -I fill-blocks.py <저장소 루트> <바꾼 예제 폴더(ex/<예제>/answers.yaml·case.yaml)>
# - classification.md 7.2~7.4: answers.yaml 블록 = 바꾼 answers.yaml (첫 블록만)
# - synthetic-examples.md 7.2~7.4: case.yaml 블록 = 바꾼 case.yaml, SHA-256 줄 = 그 파일의 SHA-256
# 다른 블록·줄(expected/classification.json 블록·SHA-256, CSV, intake.md)은 건드리지 않는다. 바뀐 줄 수를 찍는다.
import hashlib
import sys

root, ex = sys.argv[1:]
examples = {"table-compare": "### 7.2 table-compare", "form-fill": "### 7.3 form-fill", "mixed-exceptions": "### 7.4 mixed-exceptions"}


def replace_block(lines, heading, index, content):
    start = lines.index(heading)
    seen = -1
    i = start + 1
    while i < len(lines):
        if lines[i].startswith("#"):
            raise SystemExit(f"{heading}: 블록 {index}가 없다")
        if lines[i].startswith("```"):
            seen += 1
            end = i + 1
            while not lines[end].startswith("```"):
                end += 1
            if seen == index:
                new = content.split("\n")
                assert new[-1] == ""
                return lines[: i + 1] + new[:-1] + lines[end:]
            i = end
        i += 1
    raise SystemExit(f"{heading}: 블록 {index}가 없다")


def fill(doc, file_name, block_index, sha_prefix):
    path = f"{root}/docs/spec/{doc}"
    text = open(path, encoding="utf-8").read()
    assert "\r" not in text
    lines = text.split("\n")
    before = list(lines)
    for example, heading in examples.items():
        content = open(f"{ex}/{example}/{file_name}", encoding="utf-8").read()
        lines = replace_block(lines, heading, block_index, content)
        if sha_prefix:
            digest = hashlib.sha256(open(f"{ex}/{example}/{file_name}", "rb").read()).hexdigest()
            prefix = f"`examples/{example}/{file_name}` (SHA-256 `"
            hits = [i for i, l in enumerate(lines) if l.startswith(prefix)]
            assert len(hits) == 1, prefix
            lines[hits[0]] = f"{prefix}{digest}`):"
    changed = sum(1 for a, b in zip(before, lines) if a != b) + abs(len(before) - len(lines))
    open(path, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
    print(f"BLOCK fill {doc} {file_name} changed-lines={changed}")


fill("classification.md", "answers.yaml", 0, False)
fill("synthetic-examples.md", "case.yaml", 0, True)
