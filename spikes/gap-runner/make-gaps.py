#!/usr/bin/env python3
"""잠재 빈 곳 14~25 합성 예제를 만든다. 스파이크. 남긴다(docs/cases-public/README.md 4절 2).

실제 회사 자료·이름·수치 없음(지시문 2절 8·10). 값은 모두 이 스크립트가 지어낸 것이다.
같은 스크립트를 다시 돌리면 같은 바이트가 나온다(난수·시각 없음).
쓰기: python3 -I spikes/gap-runner/make-gaps.py
"""
import math
import pathlib
import shutil
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent  # spikes/

HEAD = "# 합성 예시. 실제 업무·값 없음. 잠재 빈 곳 {n} 확인용(docs/cases-public/README.md 3절).\n"

BASE = dict(
    input_kind="table",
    rule_expressible="yes",
    frequency="weekly",
    batch_size=20,
    exception_rate=5,
    current_actor="human",
    minutes_per_item=2,
    failure_impact="internal_rework",
    existing_feature="no",
    human_signoff="no",
)
ORDER = list(BASE)


def ans(**kw):
    a = dict(BASE)
    a.update(kw)
    keys = ORDER + [k for k in a if k not in ORDER]
    return "{" + ", ".join(f"{k}: {a[k]}" for k in keys) + "}"


def write(path, text):
    p = ROOT / path
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_bytes(text.encode("utf-8"))


def csv(path, header, rows):
    lines = [",".join(header)] + [",".join(str(c) for c in r) for r in rows]
    write(path, "\n".join(lines) + "\n")


def case_yaml(n, name, task, steps, inputs=None, compare=None, thresholds=None, extra_top=None, comment=None):
    out = [HEAD.format(n=n)]
    if comment:
        out += [f"# {c}\n" for c in comment]
    out.append(f"name: {name}\n")
    out.append(f"task: {task}\n")
    if extra_top:
        out += [f"{k}: {v}\n" for k, v in extra_top]
    out.append("steps:\n")
    for s in steps:
        out.append(f"  - name: {s[0]}\n")
        out.append(f"    answers: {s[1]}\n")
        for k, v in s[2:]:
            out.append(f"    {k}: {v}\n")
    if inputs:
        out.append("inputs:\n")
        out += [f"  {k}: {v}\n" for k, v in inputs]
    if compare:
        out.append("compare:\n")
        out += [f"  - {c}\n" for c in compare]
    if thresholds:
        out.append("thresholds:\n")
        out += [f"  {k}: {v}\n" for k, v in thresholds]
    return "".join(out)


def answers_yaml(n, name, task, steps, comment=None):
    out = [HEAD.format(n=n)]
    if comment:
        out += [f"# {c}\n" for c in comment]
    out.append(f"name: {name}\ntask: {task}\nsteps:\n")
    for s in steps:
        out.append(f"  - name: {s[0]}\n    answers: {s[1]}\n")
    return "".join(out)


IO = [("reference", "reference/*.csv"), ("candidate", "candidate/*.csv")]
TH = [("accuracy_min", 95), ("missing_max", 2), ("time_must_drop", "false")]


def runs(gap, lines):
    write(f"{gap}/runs.txt", "# <상대 경로> [gap-runner 옵션]\n" + "\n".join(lines) + "\n")


def clean(gap):
    d = ROOT / gap
    if d.exists():
        shutil.rmtree(d)


# ---------------------------------------------------------------- 14 중첩 구조
def gap14():
    g = "gap-14"
    clean(g)
    heads = [("inv01", "v-a", 300), ("inv02", "v-b", 600), ("inv03", "v-a", 150)]
    lines = [
        ("inv01", 1, "p-01", 100), ("inv01", 2, "p-02", 200),
        ("inv02", 1, "p-01", 100), ("inv02", 2, "p-03", 200), ("inv02", 3, "p-04", 300),
        ("inv03", 1, "p-02", 50), ("inv03", 2, "p-05", 100),
    ]
    cand_lines = [r for r in lines if not (r[0] == "inv02" and r[1] == 3)]  # 품목 줄 하나 빠짐
    step = [("송장 읽기", ans(input_kind="image"))]
    task = "합성 송장의 머리(거래처·합계)와 품목 줄을 읽어 옮긴다"
    # (a) 머리 + 품목 두 파일을 그대로 한 케이스에
    write(f"{g}/nested/case.yaml", case_yaml(14, "gap-14-nested", task, step, IO + [("key", "invoice_id")],
          ["{field: total, tolerance: 0}", "{field: amount, tolerance: 0}"], TH,
          comment=["머리 CSV와 품목 CSV를 같은 reference/·candidate/ 폴더에 둔다. 키는 송장 번호."]))
    for side, ls in (("reference", lines), ("candidate", cand_lines)):
        csv(f"{g}/nested/{side}/headers.csv", ["invoice_id", "vendor_code", "total"], heads)
        csv(f"{g}/nested/{side}/lines.csv", ["invoice_id", "line_no", "item_code", "amount"], ls)
    # (b) 품목 줄만, 키 = 송장 번호(한 키 아래 여러 줄)
    write(f"{g}/lines-only/case.yaml", case_yaml(14, "gap-14-lines-only", task, step, IO + [("key", "invoice_id")],
          ["{field: item_code}", "{field: amount, tolerance: 0}"], TH,
          comment=["품목 줄만. 한 송장 번호 아래 줄이 여럿이다(키 하나에 여러 행)."]))
    csv(f"{g}/lines-only/reference/lines.csv", ["invoice_id", "line_no", "item_code", "amount"], lines)
    csv(f"{g}/lines-only/candidate/lines.csv", ["invoice_id", "line_no", "item_code", "amount"], cand_lines)
    # (c) 머리만 대조(지금 엔진이 받는 모양). 품목 줄 누락이 보이지 않는다.
    write(f"{g}/headers-only/case.yaml", case_yaml(14, "gap-14-headers-only", task, step, IO + [("key", "invoice_id")],
          ["{field: vendor_code}", "{field: total, tolerance: 0}"], TH,
          comment=["머리만 대조한다. 결과 자료의 품목 줄 하나가 빠졌지만 머리 합계는 송장에 인쇄된 값을 옮겨 같다."]))
    csv(f"{g}/headers-only/reference/headers.csv", ["invoice_id", "vendor_code", "total"], heads)
    csv(f"{g}/headers-only/candidate/headers.csv", ["invoice_id", "vendor_code", "total"], heads)
    # (d) 우회: 사용자가 줄 키(line_id)를 만들어 평평하게
    flat = [(f"{r[0]}-{r[1]}", r[0], r[2], r[3]) for r in lines]
    cflat = [(f"{r[0]}-{r[1]}", r[0], r[2], r[3]) for r in cand_lines]
    write(f"{g}/flat/case.yaml", case_yaml(14, "gap-14-flat", task, step,
          IO + [("key", "line_id"), ("group", "invoice_id")],
          ["{field: item_code}", "{field: amount, tolerance: 0}"], TH,
          comment=["우회: 사용자가 송장 번호-줄 번호로 줄 키를 만들었다. 송장 단위 셈은 없다."]))
    csv(f"{g}/flat/reference/lines.csv", ["line_id", "invoice_id", "item_code", "amount"], flat)
    csv(f"{g}/flat/candidate/lines.csv", ["line_id", "invoice_id", "item_code", "amount"], cflat)
    runs(g, ["nested", "lines-only", "headers-only --reviewed", "flat"])


# ---------------------------------------------------------------- 15 집합·순위
def gap15():
    g = "gap-15"
    clean(g)
    ref = ["d01", "d02", "d03", "d04", "d05"]
    cand = ["d01", "d03", "d02", "d06", "d07"]  # 3개 겹침, 순서 일부 바뀜
    step = [("관련 문서 다섯 개 고르기", ans(input_kind="free_doc", rule_expressible="partial"))]
    task = "합성 질의 하나에 관련 문서 다섯 개를 순위대로 고른다"
    # (a) 집합만: 키 열 하나, 대조할 열 없음
    write(f"{g}/set-only/case.yaml", case_yaml(15, "gap-15-set-only", task, step, IO + [("key", "doc_id")], None, TH,
          comment=["집합만 비교한다. 대조할 값 열이 없다(키가 곧 결과다)."]))
    csv(f"{g}/set-only/reference/top5.csv", ["doc_id"], [[d] for d in ref])
    csv(f"{g}/set-only/candidate/top5.csv", ["doc_id"], [[d] for d in cand])
    # (b) 집합 + 더미 열
    write(f"{g}/set-hit/case.yaml", case_yaml(15, "gap-15-set-hit", task, step, IO + [("key", "doc_id")],
          ["{field: listed}"], TH, comment=["우회: 모든 행에 같은 값(listed = y)을 넣어 대조 열을 만든다."]))
    csv(f"{g}/set-hit/reference/top5.csv", ["doc_id", "listed"], [[d, "y"] for d in ref])
    csv(f"{g}/set-hit/candidate/top5.csv", ["doc_id", "listed"], [[d, "y"] for d in cand])
    # (c) 순위 열 대조
    write(f"{g}/ranked/case.yaml", case_yaml(15, "gap-15-ranked", task, step, IO + [("key", "doc_id")],
          ["{field: rank, tolerance: 0}"], TH, comment=["순위 열을 정확 일치로 대조한다."]))
    csv(f"{g}/ranked/reference/top5.csv", ["doc_id", "rank"], [[d, i + 1] for i, d in enumerate(ref)])
    csv(f"{g}/ranked/candidate/top5.csv", ["doc_id", "rank"], [[d, i + 1] for i, d in enumerate(cand)])
    # (d) 질의 둘(같은 문서가 두 질의에 나온다)
    q_ref = [("q1", d, i + 1) for i, d in enumerate(ref)] + [("q2", d, i + 1) for i, d in enumerate(["d01", "d08", "d09"])]
    q_cand = [("q1", d, i + 1) for i, d in enumerate(cand)] + [("q2", d, i + 1) for i, d in enumerate(["d08", "d01", "d10"])]
    write(f"{g}/multi-query/case.yaml", case_yaml(15, "gap-15-multi-query", task, step, IO + [("key", "doc_id")],
          ["{field: rank, tolerance: 0}"], TH, comment=["질의 둘. 키는 문서 번호 하나뿐이라 (질의, 문서) 짝을 키로 쓸 수 없다."]))
    csv(f"{g}/multi-query/reference/topk.csv", ["query_id", "doc_id", "rank"], q_ref)
    csv(f"{g}/multi-query/candidate/topk.csv", ["query_id", "doc_id", "rank"], q_cand)
    runs(g, ["set-only", "set-hit", "ranked", "multi-query"])


# ---------------------------------------------------------------- 16 표본 크기
def wilson(k, n, z=1.96):
    p = k / n
    d = 1 + z * z / n
    c = (p + z * z / (2 * n)) / d
    h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / d
    return 100 * (c - h), 100 * (c + h)


def gap16():
    g = "gap-16"
    clean(g)
    step = [("합성 금액 옮기기", ans())]
    task = "합성 전표의 금액을 결과표에 옮긴다"
    calc = ["# 엔진 밖 계산(Wilson 95% 구간, z = 1.96). 엔진은 이 값을 내지 않는다."]
    for tag, n, bad_every in (("n20", 20, 20), ("n200", 200, 20), ("n20-80", 20, 5)):
        rows = [(f"r{i:03d}", 100 + i) for i in range(1, n + 1)]
        cand = [(k, v + 7 if i % bad_every == 0 else v) for i, (k, v) in enumerate(rows, start=1)]
        d = f"{g}/{tag}"
        k = n - n // bad_every
        write(f"{d}/case.yaml", case_yaml(16, f"gap-16-{tag}", task, step, IO + [("key", "row_id")],
              ["{field: amount, tolerance: 0}"], TH,
              comment=[f"{n}건 중 {n // bad_every}건 값 불일치(일치 {100 * k // n}%). 시간 자료 없음."]))
        csv(f"{d}/reference/rows.csv", ["row_id", "amount"], rows)
        csv(f"{d}/candidate/rows.csv", ["row_id", "amount"], cand)
        lo, hi = wilson(k, n)
        where = "구간 안" if lo < 95 <= hi else ("구간 위" if hi < 95 else "구간 아래")
        calc.append(f"{tag}: {k}/{n} = {100 * k / n:.1f}%  Wilson95 [{lo:.1f}, {hi:.1f}] 폭 {hi - lo:.1f}  문턱 95는 {where}")
    write(f"{g}/wilson.txt", "\n".join(calc) + "\n")
    runs(g, ["n20 --reviewed", "n200 --reviewed", "n20-80 --reviewed"])


# ---------------------------------------------------------------- 17 기준 자료 신뢰도
def gap17():
    g = "gap-17"
    clean(g)
    items = [f"i{i:02d}" for i in range(1, 21)]
    a = {k: ("defect" if i % 4 == 0 else "ok") for i, k in enumerate(items, start=1)}
    flip = {"ok": "defect", "defect": "ok"}
    b = {k: (flip[v] if k in ("i03", "i08", "i15") else v) for k, v in a.items()}   # 평가자 B: A와 3건 다름
    ai = {k: (flip[v] if k in ("i03", "i11") else v) for k, v in a.items()}         # AI: A와 2건 다름
    step = [("불량 여부 판정", ans(input_kind="image", rule_expressible="partial"))]
    task = "합성 부품 사진을 보고 불량 여부를 적는다"
    hdr = ["item_id", "label"]
    write(f"{g}/two-refs/case.yaml", case_yaml(17, "gap-17-two-refs", task, step, IO + [("key", "item_id")],
          ["{field: label}"], TH, comment=["기준 자료 두 벌(평가자 A·B)을 reference/에 같이 둔다."]))
    csv(f"{g}/two-refs/reference/rater_a.csv", hdr, a.items())
    csv(f"{g}/two-refs/reference/rater_b.csv", hdr, b.items())
    csv(f"{g}/two-refs/candidate/ai.csv", hdr, ai.items())
    write(f"{g}/a-vs-b/case.yaml", case_yaml(17, "gap-17-a-vs-b", task, step, IO + [("key", "item_id")],
          ["{field: label}"], TH, comment=["평가자 A를 기준, 평가자 B를 결과로 넣어 사람 간 일치율을 잰다."]))
    csv(f"{g}/a-vs-b/reference/rater_a.csv", hdr, a.items())
    csv(f"{g}/a-vs-b/candidate/rater_b.csv", hdr, b.items())
    write(f"{g}/a-vs-ai/case.yaml", case_yaml(17, "gap-17-a-vs-ai", task, step, IO + [("key", "item_id")],
          ["{field: label}"], TH, comment=["평가자 A를 기준, AI를 결과로."]))
    csv(f"{g}/a-vs-ai/reference/rater_a.csv", hdr, a.items())
    csv(f"{g}/a-vs-ai/candidate/ai.csv", hdr, ai.items())
    runs(g, ["two-refs", "a-vs-b --reviewed", "a-vs-ai --reviewed"])


# ---------------------------------------------------------------- 18 시간에 따른 저하
def gap18():
    g = "gap-18"
    clean(g)
    rows = [(f"r{i:02d}", 50 + i) for i in range(1, 21)]
    bad = {1: {"r05"}, 2: {"r05", "r12"}, 3: {"r05", "r12", "r18"}}  # 95% → 90% → 85%
    step = [("합성 측정값 옮기기", ans())]
    task = "합성 측정값을 결과표에 옮긴다. 같은 케이스를 세 번 check한다"
    th = [("accuracy_min", 90), ("missing_max", 2), ("time_must_drop", "false")]

    def cand(r):
        return [(k, v + 3 if k in bad[r] else v) for k, v in rows]

    for r in (1, 2, 3):
        d = f"{g}/run{r}"
        write(f"{d}/case.yaml", case_yaml(18, "gap-18-drift", task, step, IO + [("key", "row_id")],
              ["{field: value, tolerance: 0}"], th, comment=[f"{r}번째 check. 기준 자료는 같고 결과 자료만 새로 받았다."]))
        csv(f"{d}/reference/rows.csv", ["row_id", "value"], rows)
        csv(f"{d}/candidate/rows.csv", ["row_id", "value"], cand(r))
    d = f"{g}/merged"
    write(f"{d}/case.yaml", case_yaml(18, "gap-18-drift", task, step, IO + [("key", "row_id")],
          ["{field: value, tolerance: 0}"], th, comment=["세 번의 결과 자료를 candidate/에 같이 둔다(이력을 한 케이스에 두려는 시도)."]))
    csv(f"{d}/reference/rows.csv", ["row_id", "value"], rows)
    for r in (1, 2, 3):
        csv(f"{d}/candidate/run{r}.csv", ["row_id", "value"], cand(r))
    runs(g, ["run1 --reviewed", "run2 --reviewed", "run3 --reviewed", "merged"])


# ---------------------------------------------------------------- 19 처리량
def gap19():
    g = "gap-19"
    clean(g)
    task = "합성 거래 기록을 규칙으로 점검한다"
    write(f"{g}/volume/case.yaml", case_yaml(19, "gap-19-volume", task, [
        ("실시간 거래 점검(분당 1만 건)", ans(frequency="daily", batch_size=14400000, failure_impact="external")),
        ("소량 거래 점검(하루 20건)", ans(frequency="daily", batch_size=20, failure_impact="external")),
    ], comment=["두 단계는 처리량만 다르다(하루 1,440만 건 vs 하루 20건). 검증 자료 없음."]))
    write(f"{g}/answers-latency.yaml", answers_yaml(19, "gap-19-latency", task, [
        ("실시간 거래 점검(분당 1만 건)", ans(frequency="daily", batch_size=14400000, failure_impact="external",
                                   max_latency_ms=50)),
    ], comment=["처리량·응답 시간 질문을 답에 더해 본다(지금 질문 표에 없는 키)."]))
    runs(g, ["volume", "answers-latency.yaml"])


# ---------------------------------------------------------------- 20 되돌림
def gap20():
    g = "gap-20"
    clean(g)
    task = "합성 고객 건을 처리하고 결과를 내보낸다"
    write(f"{g}/reversible/case.yaml", case_yaml(20, "gap-20-reversible", task, [
        ("안내 메시지 보내기", ans(failure_impact="external")),
        ("환급 송금 실행", ans(failure_impact="external")),
        ("계정 차단 실행", ans(failure_impact="safety_legal")),
    ], comment=["1단계는 틀려도 정정 안내로 되돌릴 수 있다. 2·3단계는 되돌릴 수 없다. 답은 영향 말고 같다."]))
    write(f"{g}/answers-reversible.yaml", answers_yaml(20, "gap-20-answers", task, [
        ("환급 송금 실행", ans(failure_impact="external", reversible="no")),
    ], comment=["되돌림 질문을 답에 더해 본다(지금 질문 표에 없는 키)."]))
    runs(g, ["reversible", "answers-reversible.yaml"])


# ---------------------------------------------------------------- 21 개인정보·규제 플래그
def gap21():
    g = "gap-21"
    clean(g)
    task = "합성 회원 방문 기록의 방문 코드를 옮긴다"
    step = [("방문 코드 옮기기", ans(failure_impact="safety_legal"))]
    rows = [(f"m{i:02d}", f"합성회원{i:02d}", f"vc-{i % 3}") for i in range(1, 11)]
    hdr = ["member_id", "member_name", "visit_code"]
    write(f"{g}/flag-key/case.yaml", case_yaml(21, "gap-21-flag-key", task, step, IO + [("key", "member_id")],
          ["{field: visit_code}"], TH, extra_top=[("data_class", "personal")],
          comment=["자료 등급 키(data_class)를 최상위에 더해 본다."]))
    csv(f"{g}/flag-key/reference/visits.csv", hdr, rows)
    csv(f"{g}/flag-key/candidate/visits.csv", hdr, rows)
    write(f"{g}/personal/case.yaml", case_yaml(21, "gap-21-personal", task, step, IO + [("key", "member_id")],
          ["{field: visit_code}"], TH, comment=["개인 이름 열(합성)이 있는 자료. 자료 등급을 적을 곳이 없다."]))
    csv(f"{g}/personal/reference/visits.csv", hdr, rows)
    csv(f"{g}/personal/candidate/visits.csv", hdr, rows)
    runs(g, ["flag-key", "personal"])


# ---------------------------------------------------------------- 22 단계 간 오류 전파
def gap22():
    g = "gap-22"
    clean(g)
    task = "합성 영수증 금액을 읽고(1단계) 거래처별 합계를 낸다(2단계)"
    steps = [
        ("영수증 금액 읽기", ans(input_kind="image", current_actor="ai_each_time")),
        ("거래처별 합계 내기", ans(input_kind="table")),
    ]
    # 1단계 정답: 영수증 10장 금액. AI 추출은 1장(rc07)이 틀렸다.
    receipts = [(f"rc{i:02d}", f"v-{'ab'[i % 2]}", 10 * i) for i in range(1, 11)]
    ai_receipts = [(k, v, a + 9 if k == "rc07" else a) for k, v, a in receipts]
    # 2단계: 거래처별 합계. 사람 정답 = 정답 금액 합. ② 프로그램에 정답 금액을 넣으면 같은 합.
    def totals(rs):
        t = {}
        for _, v, a in rs:
            t[v] = t.get(v, 0) + a
        return sorted(t.items())
    hdr = ["vendor_code", "total"]
    th = TH
    # (a) 단계 연결 키
    write(f"{g}/chain-key/case.yaml", case_yaml(22, "gap-22-chain-key", task, [
        steps[0], (steps[1][0], steps[1][1], ("input_from", "영수증 금액 읽기")),
    ], comment=["2단계에 입력 단계 키(input_from)를 더해 본다."]))
    # (b) 2단계만 잰다: 입력은 사람이 확인한 금액
    write(f"{g}/step2-only/case.yaml", case_yaml(22, "gap-22-step2-only", task, steps, IO + [("key", "vendor_code")],
          ["{field: total, tolerance: 0}"], th,
          comment=["2단계(규칙 SW)만 잰다. 입력에 사람이 확인한 금액을 넣었다."]))
    csv(f"{g}/step2-only/reference/totals.csv", hdr, totals(receipts))
    csv(f"{g}/step2-only/candidate/totals.csv", hdr, totals(receipts))
    # (c) 1단계만 잰다: 영수증 금액
    write(f"{g}/step1-only/case.yaml", case_yaml(22, "gap-22-step1-only", task, steps, IO + [("key", "receipt_id")],
          ["{field: amount, tolerance: 0}"], th, comment=["1단계(AI 읽기)만 잰다."]))
    csv(f"{g}/step1-only/reference/receipts.csv", ["receipt_id", "vendor_code", "amount"], receipts)
    csv(f"{g}/step1-only/candidate/receipts.csv", ["receipt_id", "vendor_code", "amount"], ai_receipts)
    # (d) 끝에서 끝: AI 읽기 → 규칙 합계
    write(f"{g}/end-to-end/case.yaml", case_yaml(22, "gap-22-end-to-end", task, steps, IO + [("key", "vendor_code")],
          ["{field: total, tolerance: 0}"], th,
          comment=["AI가 읽은 금액을 그대로 2단계에 넣은 최종 합계를 잰다."]))
    csv(f"{g}/end-to-end/reference/totals.csv", hdr, totals(receipts))
    csv(f"{g}/end-to-end/candidate/totals.csv", hdr, totals(ai_receipts))
    runs(g, ["chain-key", "step1-only --reviewed", "step2-only", "end-to-end"])


# ---------------------------------------------------------------- 23 호출 비용
def gap23():
    g = "gap-23"
    clean(g)
    task = "합성 문의를 분류하고 주문을 확인한다"
    write(f"{g}/volume/case.yaml", case_yaml(23, "gap-23-volume", task, [
        ("문의 유형 분류", ans(input_kind="free_doc", rule_expressible="partial", frequency="daily", batch_size=50000)),
        ("주문 번호 확인", ans(frequency="daily", batch_size=50000, current_actor="ai_each_time")),
    ], comment=["하루 5만 건. 건당 AI 요금을 적을 곳이 없다. 검증 자료 없음."]))
    write(f"{g}/answers-cost.yaml", answers_yaml(23, "gap-23-cost", task, [
        ("주문 번호 확인", ans(frequency="daily", batch_size=50000, current_actor="ai_each_time", ai_cost_per_item=0.02)),
    ], comment=["건당 AI 요금 질문을 답에 더해 본다(지금 질문 표에 없는 키). 요금 값은 지어낸 것."]))
    runs(g, ["volume", "answers-cost.yaml"])


# ---------------------------------------------------------------- 24 입력 형식 변환
def xlsx(path, rows):
    """최소 xlsx(인라인 문자열). 고정 시각으로 써서 바이트가 같다."""
    def cell(ref, v):
        if isinstance(v, int):
            return f'<c r="{ref}"><v>{v}</v></c>'
        return f'<c r="{ref}" t="inlineStr"><is><t>{v}</t></is></c>'
    sheet_rows = []
    for ri, r in enumerate(rows, start=1):
        cells = "".join(cell(f"{chr(65 + ci)}{ri}", v) for ci, v in enumerate(r))
        sheet_rows.append(f'<row r="{ri}">{cells}</row>')
    files = {
        "[Content_Types].xml": '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>',
        "_rels/.rels": '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>',
        "xl/workbook.xml": '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets></workbook>',
        "xl/_rels/workbook.xml.rels": '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>',
        "xl/worksheets/sheet1.xml": '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>' + "".join(sheet_rows) + "</sheetData></worksheet>",
    }
    p = ROOT / path
    p.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(p, "w", zipfile.ZIP_DEFLATED) as z:
        for name, body in files.items():
            info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            z.writestr(info, body.encode("utf-8"))


def gap24():
    g = "gap-24"
    clean(g)
    task = "합성 손글씨 근무표를 읽어 시간을 집계한다"
    write(f"{g}/image-step/case.yaml", case_yaml(24, "gap-24-image-step", task, [
        ("근무표 시간 집계", ans(input_kind="image", frequency="weekly", batch_size=30)),
    ], comment=["사용자가 변환(읽기) 단계를 따로 적지 않고 한 단계로 적었다. 판단 기준은 글로 쓸 수 있다."]))
    rows = [("shift_id", "hours")] + [(f"s{i:02d}", 8) for i in range(1, 6)]
    write(f"{g}/xlsx/case.yaml", case_yaml(24, "gap-24-xlsx", task, [
        ("근무표 시간 집계", ans(input_kind="image")),
    ], [("reference", "reference/*.xlsx"), ("candidate", "candidate/*.xlsx"), ("key", "shift_id")],
        ["{field: hours, tolerance: 0}"], TH, comment=["기준·결과 자료가 xlsx다(사람이 엑셀로 정리)."]))
    xlsx(f"{g}/xlsx/reference/sheet.xlsx", rows)
    xlsx(f"{g}/xlsx/candidate/sheet.xlsx", rows)
    runs(g, ["image-step", "xlsx"])


# ---------------------------------------------------------------- 25 케이스 간 비교
def gap25():
    g = "gap-25"
    clean(g)
    hdr = ["row_id", "amount"]
    rows = [(f"r{i:02d}", 10 * i) for i in range(1, 11)]
    # a: ② 단계 둘, 대조 100% → 통과
    write(f"{g}/multi/case-a/case.yaml", case_yaml(25, "gap-25-a", "합성 전표 금액을 옮기고 합계를 낸다", [
        ("금액 옮기기", ans()), ("합계 내기", ans()),
    ], IO + [("key", "row_id")], ["{field: amount, tolerance: 0}"], TH))
    csv(f"{g}/multi/case-a/reference/rows.csv", hdr, rows)
    csv(f"{g}/multi/case-a/candidate/rows.csv", hdr, rows)
    # b: ③ 단계 하나 + ② 하나, 대조 80% → 보류
    write(f"{g}/multi/case-b/case.yaml", case_yaml(25, "gap-25-b", "합성 메모를 분류하고 건수를 센다", [
        ("메모 분류", ans(input_kind="free_doc", rule_expressible="no")), ("건수 세기", ans()),
    ], IO + [("key", "row_id")], ["{field: amount, tolerance: 0}"], TH))
    csv(f"{g}/multi/case-b/reference/rows.csv", hdr, rows)
    csv(f"{g}/multi/case-b/candidate/rows.csv", hdr, [(k, v + 1 if k in ("r03", "r08") else v) for k, v in rows])
    # c: 자료 없음 → 미검증
    write(f"{g}/multi/case-c/case.yaml", case_yaml(25, "gap-25-c", "합성 주문서를 읽어 수량을 옮긴다", [
        ("주문서 읽기", ans(input_kind="fixed_form")),
    ]))
    runs(g, ["multi", "multi/case-a", "multi/case-b --reviewed", "multi/case-c"])


for f in (gap14, gap15, gap16, gap17, gap18, gap19, gap20, gap21, gap22, gap23, gap24, gap25):
    f()
print("ok")
