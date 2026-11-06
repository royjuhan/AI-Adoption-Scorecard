// X01: 대상이 0개인 Theory가 xUnit에서 어떻게 되는지 본다. tests/Scorecard.Tests/에 넣는다(스크래치 clone만).
namespace Scorecard.Tests;

public class SpikeEmptyTheory
{
    public static TheoryData<string> NoTargets => [];

    [Theory]
    [MemberData(nameof(NoTargets))]
    public void Runs_for_each_target(string name) => Assert.NotNull(name);
}
