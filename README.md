# AI-Adoption-Scorecard

[한국어](README.ko.md)

A tool that checks whether a company's AI adoption actually worked, and finds the steps of a job that plain software can finish. Built by an agent team, runs with no AI at run time.

> Status: M0 (repository setup). Nothing usable yet.

## What it does

1. You describe a job. The tool asks fixed questions and splits it into steps.
2. Each step gets an owner: ① existing feature, ② rule-based software, ③ AI at run time, ④ human. Each choice comes with a reason.
3. If AI is already in use, it measures three things: is it accurate, did time drop, was AI needed.
4. It writes a scorecard (Excel + PDF) and, if you say yes, a build-request bundle.

## Install

TBD (M4: single `scorecard.exe`, win-x64).

## 5-minute example

TBD (M2: `examples/table-compare`).

## Cases

TBD (summaries only, no private data).

## License

MIT. See [LICENSE](LICENSE).
