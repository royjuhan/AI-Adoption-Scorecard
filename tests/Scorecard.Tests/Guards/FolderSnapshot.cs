using System.Security.Cryptography;

namespace Scorecard.Tests.Guards;

// 폴더 아래 항목 하나. docs/spec/guard-tests.md 4.5 "스냅숏".
// Path는 폴더 기준 상대 경로('/' 구분). 폴더 항목은 Size 0, Sha256 빈 문자열.
internal sealed record SnapshotEntry(string Path, bool IsDirectory, long Size, string Sha256, DateTime LastWriteUtc);

internal enum ChangeKind
{
    Added,
    Removed,
    Changed,
}

// 두 스냅숏의 차이 하나. Fields는 Changed일 때 다른 항목 이름("크기, SHA-256" 등).
internal sealed record SnapshotChange(string Path, ChangeKind Kind, string Fields);

internal static class FolderSnapshot
{
    // 폴더 아래 모든 파일·폴더. 서수 정렬. 폴더가 없으면 빈 목록.
    // exclude는 바로 아래 항목 이름 하나(예: "out"). 그 항목과 그 아래를 뺀다.
    public static IReadOnlyList<SnapshotEntry> Take(string root, string? exclude = null)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        var entries = new List<SnapshotEntry>();
        foreach (var fullPath in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            var path = Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/');
            if (exclude is not null
                && (path == exclude || path.StartsWith(exclude + "/", StringComparison.Ordinal)))
            {
                continue;
            }

            if (Directory.Exists(fullPath))
            {
                entries.Add(new SnapshotEntry(path, true, 0, string.Empty, Directory.GetLastWriteTimeUtc(fullPath)));
            }
            else
            {
                var bytes = File.ReadAllBytes(fullPath);
                var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
                entries.Add(new SnapshotEntry(path, false, bytes.LongLength, sha, File.GetLastWriteTimeUtc(fullPath)));
            }
        }

        return entries.OrderBy(e => e.Path, StringComparer.Ordinal).ToArray();
    }

    // before와 after를 경로로 맞춰 비교한다. 결과는 경로 서수 순.
    // compareTime이 false면 수정 시각은 보지 않는다(G2).
    public static IReadOnlyList<SnapshotChange> Compare(
        IReadOnlyList<SnapshotEntry> before,
        IReadOnlyList<SnapshotEntry> after,
        bool compareTime)
    {
        var beforeByPath = before.ToDictionary(e => e.Path, StringComparer.Ordinal);
        var afterByPath = after.ToDictionary(e => e.Path, StringComparer.Ordinal);
        var changes = new List<SnapshotChange>();

        foreach (var path in beforeByPath.Keys.Union(afterByPath.Keys).Order(StringComparer.Ordinal))
        {
            if (!afterByPath.TryGetValue(path, out var a))
            {
                changes.Add(new SnapshotChange(path, ChangeKind.Removed, string.Empty));
            }
            else if (!beforeByPath.TryGetValue(path, out var b))
            {
                changes.Add(new SnapshotChange(path, ChangeKind.Added, string.Empty));
            }
            else
            {
                var fields = ChangedFields(b, a, compareTime);
                if (fields.Count > 0)
                {
                    changes.Add(new SnapshotChange(path, ChangeKind.Changed, string.Join(", ", fields)));
                }
            }
        }

        return changes;
    }

    private static List<string> ChangedFields(SnapshotEntry before, SnapshotEntry after, bool compareTime)
    {
        var fields = new List<string>();
        if (before.IsDirectory != after.IsDirectory)
        {
            fields.Add("종류");
        }

        if (before.Size != after.Size)
        {
            fields.Add("크기");
        }

        if (!string.Equals(before.Sha256, after.Sha256, StringComparison.Ordinal))
        {
            fields.Add("SHA-256");
        }

        if (compareTime && before.LastWriteUtc != after.LastWriteUtc)
        {
            fields.Add("수정 시각");
        }

        return fields;
    }
}
