# AI-Adoption-Scorecard

[한국어](README.ko.md)

A tool that checks whether a company's AI adoption actually worked, and finds the steps of a job that plain software can finish. Built by an agent team, runs with no AI at run time.

> Status: M0 (repository setup). Nothing usable yet.

## What it does

1. You describe a job. The tool asks fixed questions and splits it into steps.
2. Each step gets an owner: ① existing feature, ② rule-based software, ③ AI at run time, ④ human. Each choice comes with a reason.
3. If AI is already in use, it measures three things: is it accurate, did time drop, was AI needed.
4. It writes a scorecard (one HTML page) and, if you say yes, a build-request bundle.

AI에 보내면 안 되는 자료는 넣지 마세요. 구조만 설명해도 분류는 됩니다.

## Install

Download the zip from [Releases](https://github.com/royjuhan/AI-Adoption-Scorecard/releases) (win-x64, `scorecard.exe` + examples). Unzip and double-click `1-예제-돌려보기.bat`. See `사용법.txt` inside.

## 5-minute example

`dotnet run --project src/Scorecard.Cli -- check examples/table-compare --json` compares and judges the example (results in `examples/table-compare/out/`).
Commands: `start`, `open`, `check`, `build`. No prompts; `--json` prints one JSON document. See `docs/spec/json-output.md`.
Sample scorecard: [docs/samples/scorecard-table-compare.html](docs/samples/scorecard-table-compare.html).

## Cases

TBD (summaries only, no private data).

## License

MIT. See [LICENSE](LICENSE).
