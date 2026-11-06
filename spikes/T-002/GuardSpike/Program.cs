// T-002 스파이크 도구. 머지 대상 아님. 테스트 코드가 아니다.
// 결과는 표준 출력에 줄 단위로 쓴다. 줄 머리: FIND, RESULT, GUARD, CULTURE, HASH, ENUM, READONLY, SHA, REF.
namespace GuardSpike;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            return Usage();
        }

        var rest = args[1..];
        return args[0] switch
        {
            "meta" => MetaScan.Run(rest),
            "text" => TextScan.Run(rest),
            "guard" => GuardLab.Run(rest),
            "hash" => Probes.Hash(rest),
            "enumorder" => Probes.EnumOrder(rest),
            "readonly" => Probes.ReadOnly(rest),
            "sha" => Probes.Sha(rest),
            _ => Usage(),
        };
    }

    private static int Usage()
    {
        Console.Error.WriteLine("""
            사용:
              meta <dll> [--core] [--allow <어셈블리>]...  컴파일된 어셈블리의 참조 검사
              text <폴더> [--project-files]               소스 텍스트 검색 (.cs, 선택: .csproj/.props/.targets)
              guard <빈 작업폴더>                         결정론·원본 불변 도우미 실험 (가짜 대상)
              hash <문자열>                               string.GetHashCode 값
              enumorder <빈 작업폴더>                     Directory.EnumerateFiles 순서
              readonly <빈 작업폴더>                      읽기 전용 파일에 쓰기가 막히는지
              sha <파일>...                               SHA-256
            """);
        return 2;
    }
}
