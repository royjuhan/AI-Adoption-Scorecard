# T-008 스파이크(머지 대상 아님). clone의 docs/README-instructions.md에 human-signoff.md 6절의 제안 문구를 넣는다.
# 리드가 사람 결정 뒤 고칠 문구가 기존 테스트(지시문 3.2 표 대조, 3.3 근거 문장 줄)와 맞는지 보려는 것이다.
# 저장소의 지시문은 바꾸지 않는다. 사용: python3 -I instructions-variant.py <clone의 README-instructions.md>
import sys

path = sys.argv[1]
text = open(path, encoding="utf-8").read()
assert "\r" not in text


def rep(old, new):
    global text
    assert text.count(old) == 1, old[:60]
    text = text.replace(old, new)


# 2절 6 (안 A)
rep(
    "모르는 답은 비워 두어 기본값 표시가 남게 한다(2026-11-07 사람 결정: ADR 0004).",
    "모르는 답은 비워 두어 기본값 표시가 남게 한다(2026-11-07 사람 결정: ADR 0004). "
    "단, `human_signoff`는 비워 두지 않는다. 서명 단계를 한 번 묻고 모든 단계에 `yes`/`no`를 적는다. "
    "아무 단계도 고르지 않으면 모두 `no`다(2026-11-07 사람 결정: ADR 0003).",
)
# 3.1
rep(
    '끝에 "사람이 최종 확인·서명하는 단계가 어디인가"를 한 번 묻는다.',
    '끝에 "사람이 최종 확인·서명하는 단계가 어디인가"를 한 번 묻는다. '
    "고른 단계는 `human_signoff: yes`, 나머지는 `no`로 적는다. 아무 단계도 고르지 않으면 모두 `no`다.",
)
# 3.2 제목, 표 끝 행, 표 아래 문장
rep("### 3.2 단계마다 묻는 질문 9개와 기본값", "### 3.2 단계마다 적는 답 10개와 기본값")
rep(
    "| `existing_feature` | 기존 프로그램에 같은 기능이 있나 | yes / no / unknown | unknown (확인 항목으로 남김) |\n",
    "| `existing_feature` | 기존 프로그램에 같은 기능이 있나 | yes / no / unknown | unknown (확인 항목으로 남김) |\n"
    "| `human_signoff` | 사람이 최종 확인·서명하는 단계인가 | yes / no | no (단계마다 묻지 않는다. 3.1의 서명 단계 질문으로 채운다) |\n"
    "\n"
    "앞의 9개는 단계마다 묻는다. `human_signoff`는 3.1의 서명 단계 질문으로 한 번 묻고 모든 단계에 값을 적는다(2026-11-07 사람 결정: ADR 0003).\n",
)
# 3.3 트리 첫 줄, 항상 줄, 추정 표시 줄
rep(
    "```\nQ1 existing_feature == yes",
    "```\nSIGNOFF human_signoff == yes         → ④ 사람이 최종 확인·서명 (뒤에 게이트를 붙이지 않는다)\nQ1 existing_feature == yes",
)
rep(
    "항상: failure_impact in {external, safety_legal} 이면 → 해당 단계 뒤에 ④ 사람 게이트 추가",
    "항상(SIGNOFF 제외): failure_impact in {external, safety_legal} 이면 → 해당 단계 뒤에 ④ 사람 게이트 추가",
)
rep(
    '기본값이 쓰인 답은 근거 문장 끝에 "(기본값 사용, 확인 필요)"를 붙인다.\n',
    '기본값이 쓰인 답은 근거 문장 끝에 "(기본값 사용, 확인 필요)"를 붙인다.\n'
    "\n"
    "AI가 추정한 답(단계의 `inferred`)이 쓰인 근거 문장 끝에는 `(AI 추정, 확인 필요)`를 붙인다. 기본값 표시가 같이 있으면 그 뒤에 둔다(2026-11-07 사람 결정: ADR 0004).\n",
)
open(path, "w", encoding="utf-8", newline="\n").write(text)
print("RESULT instructions-variant 2.6 3.1 3.2 3.3 applied")
