# T-008 스파이크(머지 대상 아님). clone의 후보 StepClassifier.cs에 음성 대조 변형 하나를 넣는다.
# 사용: python3 -I mutate.py <StepClassifier.cs> <변형 이름>
# 변형:
#   gate-after-signoff     SIGNOFF 단계 뒤에도 게이트 행을 붙인다.
#   signoff-after-q1       existing_feature: yes면 SIGNOFF보다 Q1이 이긴다.
#   inferred-before-default 추정 표시를 기본값 표시 앞에 둔다.
#   signoff-reads-fi       SIGNOFF의 읽은 답에 failure_impact를 더한다.
import sys

path, name = sys.argv[1], sys.argv[2]
text = open(path, encoding="utf-8").read()


def rep(old, new):
    global text
    assert text.count(old) == 1, f"{name}: {old[:60]}"
    text = text.replace(old, new)


if name == "gate-after-signoff":
    rep("if (leaf.Rule != RuleSignoff && GateFailureImpacts", "if (GateFailureImpacts")
elif name == "signoff-after-q1":
    rep("if (a.HumanSignoff.Value == SignoffHumanSignoff)", "if (a.HumanSignoff.Value == SignoffHumanSignoff && a.ExistingFeature.Value != Q1ExistingFeature)")
elif name == "inferred-before-default":
    rep("text += DefaultMark;", "text += \"\\u0000D\";")
    rep("text += InferredMark;", "text += \"\\u0000I\";")
    rep("return new ClassificationRow(kind, assignee, rule, ordered, text);",
        "text = text.Replace(\"\\u0000D\\u0000I\", InferredMark + DefaultMark).Replace(\"\\u0000D\", DefaultMark).Replace(\"\\u0000I\", InferredMark);\n"
        "        return new ClassificationRow(kind, assignee, rule, ordered, text);")
elif name == "signoff-reads-fi":
    rep("SignoffReason, [StepQuestions.HumanSignoff.Key]);", "SignoffReason, [StepQuestions.HumanSignoff.Key, StepQuestions.FailureImpact.Key]);")
else:
    raise SystemExit(f"모르는 변형: {name}")
open(path, "w", encoding="utf-8", newline="\n").write(text)
print(f"RESULT mutate {name}")
