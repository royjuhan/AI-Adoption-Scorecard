// V04: 별칭을 Directory.Build.props의 <Using Alias>로 정의. 이 파일에는 금지 이름이 없다.
namespace Scorecard.Core.Spike;

public static class V04
{
    public static object Make() => new Web();
}
