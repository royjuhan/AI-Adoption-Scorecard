# AI-Adoption-Scorecard

[한국어](README.ko.md)

Find out which parts of a job should be done by people, by AI, or by plain software, and check whether the AI you already use actually works.

> Status: alpha. The latest Windows exe is on [Releases](https://github.com/royjuhan/AI-Adoption-Scorecard/releases). Feedback welcome in Issues.

## What it does

1. You describe a job in your own words. An AI assistant (the `/scorecard` skill for Claude Code) splits it into steps.
2. Each step gets an owner, with a reason: ① existing feature, ② rule-based software (②M: a fixed, pre-trained model), ③ AI at run time, ④ human. A rule engine writes its own "rule opinion" next to each AI call, and the scorecard flags where they differ.
3. If AI is already in use, the engine compares its results with a human-made reference: accuracy and missing rate. It also flags steps where AI may not be needed. Measuring time saved is not available yet. Every number comes from the engine, never from the AI.
4. You get a scorecard (one HTML page) and, after you confirm, a build-request bundle for the steps worth turning into software.

AI에 보내면 안 되는 자료는 넣지 마세요. 구조만 설명해도 분류는 됩니다.

## Who it is for

- Teams that adopted AI and want to know if it paid off.
- People who suspect a "use AI for it" task is really a fixed rule a small program could run.

## Install

Download the zip from [Releases](https://github.com/royjuhan/AI-Adoption-Scorecard/releases) (Windows x64: `scorecard.exe` + examples). Unzip and double-click `1-예제-돌려보기.bat`. See `사용법.txt` inside.

## 5-minute example

`dotnet run --project src/Scorecard.Cli -- check examples/table-compare --json` compares and judges the example (results in `examples/table-compare/out/`).
Commands: `start`, `open`, `check`, `build`. No prompts; `--json` prints one JSON document. See `docs/spec/json-output.md`.
Sample scorecard: [docs/samples/scorecard-table-compare.html](docs/samples/scorecard-table-compare.html).

## How it is built

- .NET 10. Same input, same version → byte-identical engine output.
- Input files are read only. Every scorecard records the SHA-256 of what it read.
- Specs live in `docs/spec/`, decisions in `docs/decisions/`.
- All examples are synthetic. No real company data goes into this repository.

## License

MIT. See [LICENSE](LICENSE).
