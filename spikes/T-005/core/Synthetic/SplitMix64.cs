namespace Scorecard.Core.Synthetic;

// 결정론 수열 SplitMix64(Steele·Lea·Flood 2014, Vigna의 참조 구현과 같은 상수).
// 64비트 부호 없는 정수의 덧셈·곱셈·시프트만 쓴다. 플랫폼·문화권·프로세스와 상관없이 같은 수열이다.
// System.Random(G1 D1 금지)을 쓰지 않는다. 판정에는 쓰지 않는다. 합성 자료 생성 전용이다.
internal sealed class SplitMix64
{
    private ulong state;

    public SplitMix64(ulong seed) => state = seed;

    public ulong Next()
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            var z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    // 0 이상 bound 미만. Next() × bound의 상위 64비트(곱셈 방식). 한 번 부를 때 Next()를 정확히 한 번 쓴다.
    public int NextBelow(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bound, 1);
        return (int)Math.BigMul(Next(), (ulong)bound, out _);
    }
}
