# 0010. AI 분류 + 규칙 검증(B안), 기능 우선

- 날짜: 2026-11-08
- 상태: 결정(juhan, 2026-11-08 08:26. 5번은 같은 날 juhan이 고쳤다). 적용: T-014 뒤쪽 PR(#18, 머지됨) 다음부터. 곧 이 ADR의 PR부터다.
- 작성: prototyper 초안, 리드 기록.
- 관련: ADR 0003, 0004, 0005, 0009. 지시문 1절·2절 1·6·7·3.1·3.3·3.5·5절·9절·10절·13절. `CLAUDE.md`, `.claude/agents/`, `README.md`, `docs/cases-public/README.md` 4절 3항.
- 번호: 닫은 PR #19(T-015a, 커밋 `5b21280`)가 쓰던 0010을 이 ADR이 쓴다. 그 PR의 ADR은 main에 없다.

## 지시 원문 (juhan, 2026-11-08 08:26. 5번은 고친 원문)

```text
방향 수정 두 가지. ADR 0010으로 기록하고 T-014 뒤쪽 PR부터 적용해.

[A. B안: AI 분류 + 규칙 검증]
1. 스코어카드 도구는 온라인 AI 도구다. 분류·제안·스코어카드 작성은 AI(/scorecard 스킬, 이후 독립 앱)가 한다.
2. 분류: AI가 업무 설명을 읽고 단계마다 ①~④(②M 포함) 판단과 근거를 쓴다. 질문 10개와 규칙 트리는
   AI가 참고하는 체크리스트로 쓰고, 트리 결과는 "규칙 의견"으로 같이 적는다. AI 판단과 다르면 스코어카드에 표시한다.
   classification.json 단계마다 source: ai | rule, rule_opinion 을 추가한다(schema_version 올림, expected migration 규칙).
3. 검증(대조·지표·판정)은 Scorecard.Core 규칙 엔진 그대로. AI가 호출하는 계산 도구다. 숫자는 항상 코드가 낸다.
4. "오프라인·실행 시 AI 없음"은 도구가 아니라 납품물(의뢰서로 만드는 소프트웨어)에만 적용한다.
   네트워크 금지 가드는 Core에서 제거하지 말고 납품물 템플릿(scorecard-factory-template) 검사로 옮긴다. 도구 CI에서는 끈다.
5. exe 문답(M2-4)은 없앤다. data_policy 설정은 두지 않는다. 어떤 자료를 AI에 보낼지는 사용자가 정한다.
   도구는 넣은 자료를 그대로 쓴다. README에 한 줄만 적는다: "AI에 보내면 안 되는 자료는 넣지 마세요. 구조만 설명해도 분류는 됩니다."
6. 지시문 2절 원칙 1·6과 CLAUDE.md를 이 기준으로 고친다. 회사 자료 금지(공개 저장소)는 그대로.

[B. 기능 우선, 보강은 나중]
7. T-014 뒤쪽 PR만 끝내고 M2로 간다. 빈 곳 카드는 M2 뒤.
8. 스펙 200줄 이내. 리뷰 1회, 막는 지적만 고치고 나머지는 docs/LATER.md에 적고 머지. 2회차 리뷰 없음.
9. [재검토]는 CI 빨간불일 때만. 보강·정리 카드는 열지 않는다. 발견한 문제는 LATER.md.
10. oracle은 Core 대조·판정에만. 스킬·HTML·명령에는 골든 테스트만.
11. effort: prototyper·reviewer high, 나머지 medium, 재검토 reviewer만 xhigh.
12. M2 순서: M2-1 명령 틀(start·open·check·build, --json) → M2-2 /scorecard 스킬(AI 분류·제안, 규칙 의견 병기)
    → M2-3 HTML 스코어카드(시안 참고, 390·900·1236px 가로 스크롤 없음) → M2-4 build 의뢰서 번들.
    카드마다 스펙+구현을 PR 하나로. M2-4 끝나면 v0.1.0-alpha 태그.

[바꾸지 않는 것] 공개 저장소에 회사 자료 금지, regression-guard·mutation·Core oracle 잡, expected migration 규칙, 테스트 먼저.
```

- 5번의 첫 원문은 `data_policy: send_structure_only | send_all`(기본값 `send_structure_only`) 설정이었다. juhan이 같은 날 위 문장으로 바꿨다. `data_policy`는 두지 않는다.

## 해석 (리드가 확인한다)

1. **"exe 문답(M2-4)".** ADR 0004 M2 표의 대화형 화면이다: 옛 M2-2 번호 마법사, 옛 M2-4 터미널 확인 한 화면. exe는 묻지 않는다. 파일 입력(`--answers`, `--review`, `--times`)과 `--json`만 받는다. 사람의 확인("맞음 / 바꾸기", 사유 한 줄)은 스킬 대화나 스코어카드에서 받아 `--review` 파일로 넣는다.
2. **`source`.** `ai`는 그 단계에 AI 판단이 있다는 뜻이다. `rule`은 AI 판단이 없어 규칙 트리 결과를 그대로 쓴 단계다(예: `answers.yaml`만 넣은 실행). `rule_opinion`의 모양(담당·근거 키·행)은 M2-2 스펙이 정한다.
3. **결정론(2절 3).** Core와 명령 출력에 적용한다. AI 판단은 파일에 저장된 뒤 입력으로 다룬다. 같은 파일이면 같은 바이트다.
4. **스킬과 자료 파일.** ADR 0004 "스킬이 지킬 것" 9(자료 파일 내용을 읽지 않는다)는 5번으로 바뀐다. 무엇을 AI에 넣을지는 사용자가 정한다. 대조는 그래도 Core가 한다.
5. **Excel 내보내기(옛 M2-8).** 12번의 M2 넷에 없다. M2 뒤로 미룬다(`docs/LATER.md`). 지시문 3.1·3.5의 `--excel` 글은 그대로 둔다.
6. **민감 경로 리뷰.** lead_check가 민감 경로를 적으면, 8번의 한 번뿐인 리뷰를 카드를 본 적 없는 reviewer가 diff 전문으로 한다. 따로 한 번 더 하지 않는다.

## M2 카드 번호 (옛 ADR 0004 표 → 새 번호)

| 옛 | 새 |
| --- | --- |
| M2-1 명령 틀과 AI용 입구 | M2-1 명령 틀(start·open·check·build, `--json`). 옛 M2-5 `check`의 명령 자리도 여기 |
| M2-2 `start` 번호 마법사 | 없앤다(5번) |
| M2-3 결과 객체·HTML·`open` | M2-3 HTML 스코어카드 |
| M2-4 확인 한 화면 | 없앤다(5번). `--review` 파일 입력은 M2-1 |
| M2-6 `build` 의뢰서 | M2-4 build 의뢰서 번들. 끝나면 v0.1.0-alpha 태그 |
| M2-7 스킬 `/scorecard` | M2-2 스킬(AI 분류·제안, 규칙 의견 병기) |
| M2-8 Excel·end-to-end | Excel은 M2 뒤. end-to-end는 M2-4에서 |

- 스펙에 남은 옛 번호(`judgment.md`의 "M2-4 `review`" 등)는 고치지 않는다. 옛 번호는 이 표로 읽는다.

## 기존 ADR과 바뀌는 곳

- **0003.** 질문 `human_signoff`와 트리의 `SIGNOFF`는 그대로다. 서명 단계를 한 번 묻는 쪽이 exe 문답에서 스킬(AI)로 바뀐다. 트리 결과는 이제 규칙 의견이다.
- **0004.** 결정 6(`--json`, 오류 JSON, 종료 코드 0/2/1, 바이트 결정론)과 expected 변경 규칙은 그대로다. 바뀌는 곳: 결정 1의 번호 마법사(없앰), 결정 4의 터미널 확인 화면(없앰), "스킬은 분류·지표·판정을 하지 않는다"와 "①~④를 스스로 말하지 않는다"(스킬이 분류한다. 지표·판정은 그대로 Core), 질문 4 데이터 경계(5번), M2 카드 표(위 표).
- **0005.** 결정 3(추천안 채택, STATUS 기록), 결정 4(2절 8·10), 결정 5(CI 잡), 결정 9(oracle은 prototyper만)는 그대로다. 바뀌는 곳: 결정 6 머지 조건(리뷰 1회, 막는 지적만 고치고 머지, 나머지 `docs/LATER.md`), 결정 7 [재검토](CI 빨간불일 때만. 리뷰 루프 3회 초과·마일스톤 끝 조건은 빠진다), CLAUDE.md "리뷰 루프 3회".
- **0009.** 체크리스트·점수표 엔진과 "엔진은 문자열 규칙과 셈만"은 그대로다(3번과 같은 뜻). text-check.md 9절의 "다음 카드" 항목은 `docs/LATER.md`로 간다.
- **`docs/cases-public/README.md` 4절 3항.** 빈 곳 카드 순서와 "M2는 6 혼동행렬까지 끝낸 뒤 시작"보다 7번이 앞선다. README는 juhan 원문이라 고치지 않는다. 4절 4·5항(질문 12개 상한, 스키마 버전)은 그대로다.

## 영향 받는 곳과 바꾸는 때

| 무엇 | 언제 |
| --- | --- |
| 지시문 2절 1·6·7과 2절 머리말, 1절 둘째 문단, 3.1 표·글머리, 3.3 끝 문단, 3.5 2구역, 5절 CI 줄, 9절 `scorecard` 행, 10절 M2 행, 13절 첫 줄 | 이 PR |
| `CLAUDE.md` 절대 원칙·작업 흐름, `.claude/agents/*.md` effort·cli-dev 2항·reviewer·prototyper 한 줄 | 이 PR |
| `README.md` 안내 한 줄(5번), `docs/LATER.md`(새), `docs/STATUS.md` | 이 PR |
| classification.json `source`·`rule_opinion`, `schema_version` 1 → 2, `expected migration:` 커밋 | M2-2 |
| 네트워크 금지 가드(G1)를 납품물 템플릿 검사로 옮기고 도구 CI에서 끄기 | 처음 필요한 카드(M2-2 또는 M2-4). 그때까지 G1·`.claude/rules/core-no-network.md`·reviewer 검사는 그대로. 가드 코드는 지우지 않는다 |
| exe 문답 없앰 | M2-1(명령 틀이 묻지 않는다) |
| HTML 스코어카드(시안, 390·900·1236px 가로 스크롤 없음) | M2-3 |

- 지금 Core는 AI가 필요 없다. 그래서 가드를 지금 옮기지 않는다.
- 분류 oracle(`tools/oracle/classify.py`)은 Core 분류 규칙이다. M2-2가 classification.json을 바꾸면 oracle 변경과 expected 변경을 같은 PR에 둘 수 없다(`oracle-and-expected`). 12번(PR 하나)과 어긋나면 M2-2에서 정하고 STATUS에 적는다.

## 바꾸지 않는 것

- 지시문 2절 8·10(번들에 실제 자료 없음, 공개 저장소에 회사 자료 없음). 글자 하나도 바꾸지 않았다.
- regression-guard·mutation·Core oracle 잡, lead_check, expected migration 규칙(ADR 0004), 테스트 먼저(2절 5), 결정론(2절 3), 원본 보존(2절 2).

## 남은 위험

- 사용자가 회사 자료를 AI에 넣을 수 있다. 도구는 거르지 않는다(5번). 저장소 금지(2절 10)와 번들 검사(2절 8, `export-check.md`)는 그대로다.
- AI 판단은 같은 서술에서 다를 수 있다. 규칙 의견과 다르면 표시되지만, 둘이 같은 쪽으로 틀리면 표시가 없다.
- 리뷰가 한 번이라 막지 않는 지적이 쌓인다. `docs/LATER.md`가 길어지면 M2 뒤 카드로 묶는다.
- `.claude/agents/`의 `effort` frontmatter를 Claude Code가 읽는지는 **미확인**이다. 리드가 띄울 때 effort를 직접 준다.
