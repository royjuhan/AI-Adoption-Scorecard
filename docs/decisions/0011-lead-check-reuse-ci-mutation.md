# 0011. lead_check가 CI mutation 결과를 다시 쓴다

- 날짜: 2026-11-08
- 상태: 채택(juhan 선택 "재사용하게 바꾸기", 2026-11-08). ADR 0010 B(기능 우선)의 연장이다.
- 작성: ci-dev, 리드 기록.
- 관련: ADR 0005 결정 5·남은 위험, ADR 0010 B. `tools/ci/lead_check.py`, `tools/ci/README.md` "lead_check".

## 배경

머지 전 `lead_check.py`는 Stryker를 로컬에서 다시 돈다(약 40분). CI `mutation` 잡이 같은 base·head로 이미 돌렸다. 같은 일을 두 번 한다.

## 결정

1. `lead_check.py`에 `--mutation-from <mutation.json>`을 더한다. CI `mutation` 잡이 올린 아티팩트 `mutation-result`의 파일이다. ci.yml은 바꾸지 않는다(이미 올린다).
2. 아래 조건이 모두 맞을 때만 그 파일을 쓴다(`reuse_mutation`). 하나라도 아니면 WARN 한 줄에 이유를 적고 지금처럼 로컬에서 돈다.
   - base 도구로 돈다(부트스트랩이 아니다).
   - base..head에 `.github/`·`tools/ci/` 아래 변경이 없다. 바뀌었으면 CI 결과를 바뀐 워크플로·도구가 만들었을 수 있다.
   - 파일의 `schema_version`이 1이고 `base`가 이 검사의 base와 같다.
   - 파일의 `head` 커밋이 리드 저장소에 있고, 그 트리가 이 검사의 head 트리와 같다. CI head는 GitHub 병합 커밋(`refs/pull/N/merge`)이라 SHA가 lead_check의 병합 커밋과 다르다. base와 트리가 같으면 mutation.py의 입력(바뀐 파일, 양쪽 트리)이 같다.
   - `status`가 `measured` 또는 `not_applicable`이다(mutation.py 종료 0).
3. 쓸 때는 `head`를 이 검사의 head로 바꾸고 원래 값을 `ci_head`에 남겨 base `regression_guard.py`에 넘긴다. guard의 판단(점수 비교 등)은 그대로다.
4. 파일은 head 코드가 돌기 전에 메모리에 읽는다. `lead-check.json`에 `mutation_reuse`(`from`, `used`, `reason`)를 적는다. 옵션이 없으면 동작은 지금과 같다.

## 리드가 쓰는 명령

```bash
git fetch origin
BR=<PR 브랜치>
RUN=$(gh run list --workflow ci.yml --branch "$BR" --event pull_request --json databaseId,headSha \
      -q ".[] | select(.headSha==\"$(git rev-parse origin/$BR)\") | .databaseId" | head -1)
M=$(mktemp -d) && gh run download "$RUN" -n mutation-result -D "$M"
CI_HEAD=$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["head"])' "$M/mutation.json")
git fetch origin "$CI_HEAD" || git fetch origin "pull/<N>/merge"
T=$(mktemp -d) && git archive origin/main tools/ci | tar -x -C "$T"
python3 -I "$T/tools/ci/lead_check.py" --repo . --base origin/main --head "origin/$BR" --mutation-from "$M/mutation.json"
```

- `mutation` 잡이 끝나기 전이면 아티팩트가 없다. 기다리거나 옵션 없이 돈다.
- main이 그 뒤 움직였으면 base가 달라 다시 쓰지 않는다(로컬에서 돈다).

## 남은 위험

- CI `mutation` 잡도 head 테스트를 돈다. 테스트가 남긴 프로세스가 업로드 전에 `mutation.json`을 고칠 수 있다. lead_check는 단계가 끝나면 프로세스 그룹을 죽이지만 CI 잡은 그러지 않는다. 다시 쓰면 이 차이만큼 독립성이 줄어든다(가드를 약하게 하는 변경). 덫 `ci-tamper`와 reviewer의 diff 검토가 그대로 남는다.
- 워크플로가 바뀌지 않았어도 GitHub 쪽(러너, 액션 버전)은 리드가 확인하지 못한다.
- 의심스러우면 옵션 없이 돈다. 결과가 다르면 로컬 결과를 따른다.
