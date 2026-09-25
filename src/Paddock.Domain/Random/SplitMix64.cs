namespace Paddock.Domain.Random;

/// <summary>
/// SplitMix64, used only to expand one seed into the four-word xoshiro state.
/// Source: https://prng.di.unimi.it/splitmix64.c (Blackman and Vigna, public domain).
/// </summary>
internal static class SplitMix64
{
    public static void Fill(ulong seed, out ulong s0, out ulong s1, out ulong s2, out ulong s3)
    {
        s0 = Next(ref seed);
        s1 = Next(ref seed);
        s2 = Next(ref seed);
        s3 = Next(ref seed);
    }

    private static ulong Next(ref ulong state)
    {
        unchecked
        {
            ulong z = state += 0x9e3779b97f4a7c15UL;
            z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
            z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
            return z ^ (z >> 31);
        }
    }
}
