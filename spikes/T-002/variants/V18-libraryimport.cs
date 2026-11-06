// V18: LibraryImport(소스 생성 P/Invoke). Core csproj에 AllowUnsafeBlocks가 없을 때 빌드되는지 본다.
using System.Runtime.InteropServices;

namespace Scorecard.Core.Spike;

public static partial class V18
{
    [LibraryImport("libc", EntryPoint = "getpid")]
    public static partial int GetPid();
}
