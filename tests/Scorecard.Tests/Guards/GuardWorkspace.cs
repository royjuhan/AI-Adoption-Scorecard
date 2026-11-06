using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Guards;

// G2·G3 작업 폴더. docs/spec/guard-tests.md 4.5 "작업 폴더 준비".
// 1. 저장소 샘플을 임시 작업 폴더의 input/에 복사한다. 저장소 샘플은 대상에 직접 넘기지 않는다.
// 2. 작업 폴더 이름은 임시 폴더 아래 무작위 이름이다. 테스트 쪽 난수라 판정과 무관하다.
// 3. 복사 뒤 작업 폴더 아래 모든 수정 시각을 FixedTime으로 맞춘다.
// 4. Dispose에서 지운다. 읽기 전용 속성은 쓰지 않는다.
internal sealed class GuardWorkspace : IDisposable
{
    // 실행 전 수정 시각. 쓰기가 일어나면 다른 값으로 바뀌어 G3가 잡는다.
    public static readonly DateTime FixedTime = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // 합성 샘플 위치(ADR 0002).
    public const string SamplesRelativePath = "tests/Scorecard.Tests/TestData/guard";

    private GuardWorkspace(string root)
    {
        Root = root;
    }

    // 작업 폴더 전체 경로.
    public string Root { get; }

    // 대상에 넘기는 입력 폴더.
    public string Input => Path.Combine(Root, "input");

    // 작업 폴더 바로 아래 경로(출력 폴더 등).
    public string PathOf(string name) => Path.Combine(Root, name);

    // 샘플 폴더 전체 경로.
    public static string SamplePath(string sample) => RepoRoot.Combine($"{SamplesRelativePath}/{sample}");

    // 작업 폴더를 만든다. reverseCopy면 샘플 파일을 서수 역순으로 복사한다(G2 R3).
    public static GuardWorkspace Create(string sample, bool reverseCopy)
    {
        var source = SamplePath(sample);
        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(source, f).Replace(Path.DirectorySeparatorChar, '/'))
            .ToArray();
        if (files.Length == 0)
        {
            throw new InvalidOperationException($"샘플 폴더가 비어 있다: {SamplesRelativePath}/{sample}");
        }

        var ordered = reverseCopy
            ? files.OrderDescending(StringComparer.Ordinal)
            : files.Order(StringComparer.Ordinal);

        var workspace = new GuardWorkspace(Directory.CreateTempSubdirectory("scorecard-guard-").FullName);
        try
        {
            foreach (var relative in ordered)
            {
                var from = Path.Combine(source, relative.Replace('/', Path.DirectorySeparatorChar));
                var to = Path.Combine(workspace.Input, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);

                // File.Copy는 원본 속성을 옮길 수 있다. 바이트만 옮긴다.
                File.WriteAllBytes(to, File.ReadAllBytes(from));
            }

            FixTimes(workspace.Root);
            return workspace;
        }
        catch
        {
            workspace.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        // 정리 실패는 판정과 무관하다. 임시 폴더라 남아도 다른 실행에 영향이 없다.
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    // 파일 먼저, 그다음 폴더를 깊은 것부터, 마지막에 작업 폴더 자신.
    private static void FixTimes(string root)
    {
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetLastWriteTimeUtc(file, FixedTime);
        }

        var directories = Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
            .OrderByDescending(d => d.Count(c => c == Path.DirectorySeparatorChar))
            .ThenBy(d => d, StringComparer.Ordinal);
        foreach (var directory in directories)
        {
            Directory.SetLastWriteTimeUtc(directory, FixedTime);
        }

        Directory.SetLastWriteTimeUtc(root, FixedTime);
    }
}
