using System.Globalization;

namespace Scorecard.Tests.Guards;

// G2·G3 도우미. docs/spec/guard-tests.md 4.5.
// 차이 목록을 돌려준다. 빈 목록이 통과다. 줄 형식은 스펙 6.4.
// 대상이 예외를 던지면 그 실행의 차이로 적는다(스펙 5절).
internal static class GuardChecks
{
    // G2 R4 문화권. 1.5.ToString()이 "1,5", double.Parse("1.5")가 15다(스펙 4.5).
    public const string GermanCultureName = "de-DE";

    // G2 빈 출력 규칙의 차이 줄(T-003 스펙 4.11).
    public const string NoOutput = "G2 R1 기준: 출력 파일 없음";

    // G3 출력 폴더 이름. 스냅숏에서 뺀다.
    private const string ImmutabilityOutput = "out";

    // G3 원본 불변. 실행 전후 작업 폴더(출력 폴더 제외)를 비교한다.
    // 경로·종류·크기·SHA-256·수정 시각 다섯 항목이 모두 같아야 한다(승인 결정 8).
    public static IReadOnlyList<string> Immutability(GuardTarget target)
    {
        var differences = new List<string>();
        using var workspace = GuardWorkspace.Create(target.Sample, reverseCopy: false);

        var before = FolderSnapshot.Take(workspace.Root, exclude: ImmutabilityOutput);
        var error = TryRun(target, workspace.Input, workspace.PathOf(ImmutabilityOutput));
        var after = FolderSnapshot.Take(workspace.Root, exclude: ImmutabilityOutput);

        if (error is not null)
        {
            differences.Add($"G3 예외: {error}");
        }

        foreach (var change in FolderSnapshot.Compare(before, after, compareTime: true))
        {
            differences.Add(change.Kind switch
            {
                ChangeKind.Added => $"G3 추가: {change.Path}",
                ChangeKind.Removed => $"G3 삭제: {change.Path}",
                _ => $"G3 변경({change.Fields}): {change.Path}",
            });
        }

        return differences;
    }

    // G2 결정론. 같은 대상을 네 번 돌리고 R1과 비교한다(승인 결정 9).
    // 비교는 출력 폴더의 경로·종류·크기·SHA-256. 수정 시각은 보지 않는다.
    // R1 기준: 작업 폴더 A, 서수 순 복사, InvariantCulture.
    // R2 반복: A(출력 폴더만 새로), InvariantCulture.
    // R3 다른 경로: 작업 폴더 B, 역순 복사, InvariantCulture.
    // R4 문화권: A(출력 폴더만 새로), de-DE.
    // 빈 출력 규칙(T-003 스펙 4.11): R1 출력 폴더에 1바이트 이상인 파일이 없으면 "G2 R1 기준: 출력 파일 없음".
    public static IReadOnlyList<string> Determinism(GuardTarget target)
    {
        var differences = new List<string>();
        using var a = GuardWorkspace.Create(target.Sample, reverseCopy: false);
        using var b = GuardWorkspace.Create(target.Sample, reverseCopy: true);
        var invariant = CultureInfo.InvariantCulture;

        var baseline = RunForOutput(target, a, "out1", invariant, "G2 R1 기준", differences);

        // 출력이 없거나 0바이트 파일뿐이면 R2~R4가 빈 출력끼리 비교해 그냥 통과한다. 차이로 적는다.
        // R2~R4 비교는 그대로 한다. G3에는 넣지 않는다(G3는 입력 보존만 본다).
        if (!baseline.Any(e => !e.IsDirectory && e.Size > 0))
        {
            differences.Add(NoOutput);
        }

        var repeat = RunForOutput(target, a, "out2", invariant, "G2 R2 반복", differences);
        AddOutputDifferences(differences, "G2 R2 반복", baseline, repeat);

        var moved = RunForOutput(target, b, "out3", invariant, "G2 R3 다른 경로", differences);
        AddOutputDifferences(differences, "G2 R3 다른 경로", baseline, moved);

        // 전제가 깨지면 R4가 조용히 약해진다. 실패로 적고 R4는 건너뛴다.
        var problem = GermanCulturePrecondition();
        if (problem is not null)
        {
            differences.Add($"G2 전제: {problem}");
        }
        else
        {
            var german = CultureInfo.GetCultureInfo(GermanCultureName);
            var cultured = RunForOutput(target, a, "out4", german, "G2 R4 문화권", differences);
            AddOutputDifferences(differences, "G2 R4 문화권", baseline, cultured);
        }

        return differences;
    }

    // R4 전제. de-DE 소수점이 ','이면 null, 아니면 문제 설명.
    // ICU가 없는 환경(InvariantGlobalization)에서 R4가 약해지는 것을 막는다.
    public static string? GermanCulturePrecondition()
    {
        try
        {
            var separator = CultureInfo.GetCultureInfo(GermanCultureName).NumberFormat.NumberDecimalSeparator;
            return separator == ","
                ? null
                : $"{GermanCultureName} 소수점이 ','가 아니다: '{separator}'";
        }
        catch (CultureNotFoundException ex)
        {
            return $"{GermanCultureName} 문화권이 없다: {ex.Message}";
        }
    }

    // 문화권을 바꿔 대상을 한 번 돌리고 출력 폴더 스냅숏을 돌려준다. 문화권은 finally로 되돌린다.
    private static IReadOnlyList<SnapshotEntry> RunForOutput(
        GuardTarget target,
        GuardWorkspace workspace,
        string outputName,
        CultureInfo culture,
        string prefix,
        List<string> differences)
    {
        var output = workspace.PathOf(outputName);
        var (savedCulture, savedUiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        string? error;
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            error = TryRun(target, workspace.Input, output);
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
            CultureInfo.CurrentUICulture = savedUiCulture;
        }

        if (error is not null)
        {
            differences.Add($"{prefix}: 예외 {error}");
        }

        return FolderSnapshot.Take(output);
    }

    private static void AddOutputDifferences(
        List<string> differences,
        string prefix,
        IReadOnlyList<SnapshotEntry> baseline,
        IReadOnlyList<SnapshotEntry> current)
    {
        foreach (var change in FolderSnapshot.Compare(baseline, current, compareTime: false))
        {
            differences.Add(change.Kind switch
            {
                ChangeKind.Added => $"{prefix}: out/{change.Path} 추가",
                ChangeKind.Removed => $"{prefix}: out/{change.Path} 삭제",
                _ => $"{prefix}: out/{change.Path} {change.Fields} 다름",
            });
        }
    }

    // 대상을 한 번 돌린다. 예외가 나면 "타입: 메시지", 아니면 null.
    private static string? TryRun(GuardTarget target, string input, string output)
    {
        try
        {
            target.Run(input, output);
            return null;
        }
        catch (Exception ex)
        {
            return $"{ex.GetType().FullName}: {ex.Message}";
        }
    }
}
