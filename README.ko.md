# AI-Adoption-Scorecard

[English](README.md)

업무의 어느 단계를 사람이, AI가, 일반 소프트웨어가 맡아야 하는지 찾고, 이미 쓰는 AI가 정말 효과가 있는지 확인하는 도구.

> 상태: 알파. 최신 Windows용 exe는 [Releases](https://github.com/royjuhan/AI-Adoption-Scorecard/releases)에 있다. 의견은 Issues에 남겨 주세요.

## 하는 일

1. 업무를 말로 설명하면 AI(Claude Code 스킬 `/scorecard`)가 단계로 나눈다.
2. 단계마다 담당을 근거와 함께 정한다: ① 기존 기능, ② 규칙 기반 소프트웨어(②M: 미리 학습해 고정한 모델), ③ 실행 시 AI, ④ 사람. 규칙 엔진이 "규칙 의견"을 옆에 같이 적고, 다르면 스코어카드에 표시한다.
3. 이미 AI를 쓰는 업무면 AI 결과를 사람이 만든 기준 자료와 대조해 정확도와 누락률을 잰다. AI가 필요 없어 보이는 단계도 표시한다. 시간이 줄었는지 재는 기능은 아직 없다. 숫자는 늘 엔진이 내고 AI가 만들지 않는다.
4. 스코어카드(HTML 한 장)를 내고, 사람이 확인하면 소프트웨어로 만들 단계의 제작 의뢰서 번들을 만든다.

AI에 보내면 안 되는 자료는 넣지 마세요. 구조만 설명해도 분류는 됩니다.

## 이런 분께

- AI를 도입했는데 효과가 있었는지 알고 싶은 팀.
- "AI로 하자"는 일이 사실은 작은 프로그램으로 돌릴 수 있는 정해진 규칙이 아닌지 의심되는 분.

## 설치

[Releases](https://github.com/royjuhan/AI-Adoption-Scorecard/releases)에서 zip을 받는다(Windows x64, `scorecard.exe`와 예제). 압축을 풀고 `1-예제-돌려보기.bat`을 더블클릭한다. 자세한 것은 안의 `사용법.txt`.

## 5분 예제

`dotnet run --project src/Scorecard.Cli -- check examples/table-compare --json`이 예제를 대조하고 판정한다(결과는 `examples/table-compare/out/`).
명령은 `start`, `open`, `check`, `build` 넷이다. 묻지 않는다. `--json`이면 JSON 문서 하나를 낸다. `docs/spec/json-output.md` 참고.
샘플 스코어카드: [docs/samples/scorecard-table-compare.html](docs/samples/scorecard-table-compare.html).

## 만든 방식

- .NET 10. 같은 입력·같은 판이면 엔진 결과가 바이트까지 같다.
- 입력 파일은 읽기만 한다. 스코어카드마다 읽은 파일의 SHA-256을 남긴다.
- 스펙은 `docs/spec/`, 결정 기록은 `docs/decisions/`.
- 예제는 모두 합성 자료다. 실제 회사 자료는 이 저장소에 넣지 않는다.

## 라이선스

MIT. [LICENSE](LICENSE) 참고.
