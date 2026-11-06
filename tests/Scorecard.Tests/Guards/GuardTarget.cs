namespace Scorecard.Tests.Guards;

// G2·G3 처리 대상 하나. docs/spec/guard-tests.md 4.5.
// Run(inputDir, outputDir): inputDir을 읽고 outputDir에 쓴다.
// outputDir은 아직 없는 경로다. 필요하면 대상이 만든다.
// Sample은 TestData/guard/ 아래 샘플 폴더 이름이다.
// 실제 Core 대상 목록(GuardTargets)은 첫 Core 처리 기능 카드에서 만든다(승인 결정 7).
internal sealed record GuardTarget(string Name, string Sample, Action<string, string> Run);
