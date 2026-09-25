namespace Paddock.Domain.Random;

/// <summary>
/// xoshiro256** 1.0. Reference implementation by David Blackman and Sebastiano Vigna:
/// https://prng.di.unimi.it/xoshiro256starstar.c (public domain).
/// A single ulong seed is expanded with SplitMix64, as that file suggests.
/// This is a reference type so two variables share one stream instead of copying it.
/// The state must not be all zeroes.
/// </summary>
public sealed class Xoshiro256StarStar
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public Xoshiro256StarStar(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        SetState(s0, s1, s2, s3);
    }

    public Xoshiro256StarStar(RngState state)
        : this(state.S0, state.S1, state.S2, state.S3)
    {
    }

    public static Xoshiro256StarStar FromSeed(ulong seed)
    {
        SplitMix64.Fill(seed, out var s0, out var s1, out var s2, out var s3);
        if ((s0 | s1 | s2 | s3) == 0)
        {
            throw new InvalidOperationException("SplitMix64 expanded a seed to the all-zero xoshiro state.");
        }

        return new Xoshiro256StarStar(s0, s1, s2, s3);
    }

    public RngState State
    {
        get => new(_s0, _s1, _s2, _s3);
        set => SetState(value.S0, value.S1, value.S2, value.S3);
    }

    public ulong NextULong()
    {
        unchecked
        {
            ulong result = Rotl(_s0 * 5, 7) * 9;

            ulong t = _s1 << 17;

            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;

            _s2 ^= t;

            _s3 = Rotl(_s3, 45);

            return result;
        }
    }

    /// <summary>
    /// Value in [0, 1). Uses the top 53 bits, the conversion from Vigna's reference.
    /// </summary>
    public double NextDouble()
    {
        // 2^53. Every integer in [0, 2^53) is exactly representable, so this is exact.
        const double inverseTwo53 = 1.0 / 9007199254740992.0;
        return (NextULong() >> 11) * inverseTwo53;
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                "maxExclusive must be greater than minInclusive.");
        }

        uint range = (uint)((long)maxExclusive - minInclusive);
        ulong random = NextULong();
        ulong product = (uint)random * (ulong)range;
        uint low = (uint)product;
        if (low < range)
        {
            uint threshold = unchecked((uint)(0u - range)) % range;
            while (low < threshold)
            {
                random = NextULong();
                product = (uint)random * (ulong)range;
                low = (uint)product;
            }
        }

        return (int)(minInclusive + (long)(product >> 32));
    }

    private void SetState(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        if ((s0 | s1 | s2 | s3) == 0)
        {
            throw new ArgumentException("xoshiro256** state must not be all zeroes.");
        }

        _s0 = s0;
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;
    }

    private static ulong Rotl(ulong x, int k)
    {
        return (x << k) | (x >> (64 - k));
    }
}
