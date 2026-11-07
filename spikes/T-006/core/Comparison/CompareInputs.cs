using Scorecard.Core.Cases;

namespace Scorecard.Core.Comparison;

// case.yaml inputs의 패턴으로 케이스 폴더의 파일을 찾아 바이트를 읽는다. docs/spec/metrics.md 4.1.
// 입력은 읽기만 한다. 파일을 쓰지 않는다. 결과·오류에 절대 경로를 넣지 않는다(케이스 폴더 기준 상대 경로만).
// 패턴: '/'로 나눈 조각. 폴더 조각은 글자 그대로(서수 비교). 마지막 조각에만 '*'(0자 이상)·'?'(1자)를 쓴다.
// 이름 비교는 서수(대소문자 구분)다. OS의 검색 패턴(Directory.EnumerateFiles(dir, "*.csv"))을 쓰지 않고 이름 목록을 직접 맞춘다.
// 찾은 파일은 이름 서수 순이다. 하위 폴더는 보지 않는다.
public static class CompareInputs
{
    private static readonly char[] FolderWildcards = ['*', '?', '[', ']', '{', '}'];
    private static readonly char[] NameForbidden = ['[', ']', '{', '}'];

    public static CompareSourcesResult Read(string caseDirectory, CaseInputs inputs)
    {
        ArgumentException.ThrowIfNullOrEmpty(caseDirectory);
        ArgumentNullException.ThrowIfNull(inputs);

        var errors = new List<CaseError>();
        var reference = ReadSide(caseDirectory, inputs.Reference, errors);
        var candidate = ReadSide(caseDirectory, inputs.Candidate, errors);
        return errors.Count == 0
            ? new CompareSourcesResult(new CompareSources(reference, candidate), [])
            : new CompareSourcesResult(null, errors);
    }

    // 4.1 패턴 모양. 빈 조각·'.' 조각, 폴더 조각의 와일드카드, '**', '[' ']' '{' '}'는 받지 않는다.
    public static bool IsSupportedPattern(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var segments = pattern.Split('/');
        if (segments.Any(s => s.Length == 0 || s == "."))
        {
            return false;
        }

        var name = segments[^1];
        return segments[..^1].All(s => s.IndexOfAny(FolderWildcards) < 0)
            && name.IndexOfAny(NameForbidden) < 0
            && !name.Contains("**", StringComparison.Ordinal);
    }

    // 이름 하나가 마지막 조각과 맞는가. '*'는 0자 이상, '?'는 UTF-16 한 단위. 나머지는 서수 비교.
    public static bool Matches(string namePattern, string name)
    {
        ArgumentNullException.ThrowIfNull(namePattern);
        ArgumentNullException.ThrowIfNull(name);
        int p = 0, n = 0, star = -1, mark = 0;
        while (n < name.Length)
        {
            if (p < namePattern.Length && (namePattern[p] == '?' || namePattern[p] == name[n]) && namePattern[p] != '*')
            {
                p++;
                n++;
            }
            else if (p < namePattern.Length && namePattern[p] == '*')
            {
                star = p++;
                mark = n;
            }
            else if (star >= 0)
            {
                p = star + 1;
                n = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (p < namePattern.Length && namePattern[p] == '*')
        {
            p++;
        }

        return p == namePattern.Length;
    }

    private static List<SourceFile> ReadSide(string caseDirectory, string pattern, List<CaseError> errors)
    {
        var files = new List<SourceFile>();
        if (!IsSupportedPattern(pattern))
        {
            errors.Add(new CaseError(pattern, 0, 0, CompareRules.CodePattern, string.Empty, "지원하지 않는 패턴이다. 폴더는 글자 그대로, 마지막 조각에만 '*'·'?'를 쓴다"));
            return files;
        }

        var segments = pattern.Split('/');
        var folder = caseDirectory;
        var relative = string.Empty;
        try
        {
            foreach (var segment in segments[..^1])
            {
                // 대소문자를 무시하는 파일 시스템에서도 같은 결과가 나오게 이름 목록에서 서수로 찾는다.
                if (!Directory.EnumerateDirectories(folder).Any(d => string.Equals(Path.GetFileName(d), segment, StringComparison.Ordinal)))
                {
                    errors.Add(new CaseError(pattern, 0, 0, CompareRules.CodeFile, string.Empty, "폴더가 없다: " + relative + segment));
                    return files;
                }

                folder = Path.Combine(folder, segment);
                relative += segment + "/";
            }

            var names = Directory.EnumerateFiles(folder)
                .Select(f => Path.GetFileName(f))
                .Where(n => Matches(segments[^1], n))
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (names.Length == 0)
            {
                errors.Add(new CaseError(pattern, 0, 0, CompareRules.CodeFile, string.Empty, "일치하는 파일이 없다"));
                return files;
            }

            foreach (var name in names)
            {
                var path = relative + name;
                try
                {
                    files.Add(new SourceFile(path, File.ReadAllBytes(Path.Combine(folder, name))));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 예외 Message에는 절대 경로가 있다. 타입 이름만 쓴다.
                    errors.Add(new CaseError(path, 0, 0, CompareRules.CodeFile, string.Empty, "파일을 읽을 수 없다: " + ex.GetType().Name));
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            errors.Add(new CaseError(pattern, 0, 0, CompareRules.CodeFile, string.Empty, "폴더가 없다: " + (relative.Length == 0 ? "." : relative.TrimEnd('/'))));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add(new CaseError(pattern, 0, 0, CompareRules.CodeFile, string.Empty, "폴더를 읽을 수 없다: " + ex.GetType().Name));
        }

        return files;
    }
}
