using Paddock.Domain.Random;

namespace Paddock.Tests.Random;

/// <summary>
/// Vectors from the C reference by Blackman and Vigna:
/// https://prng.di.unimi.it/xoshiro256starstar.c
/// and https://prng.di.unimi.it/splitmix64.c
/// Seed 0 is expanded with SplitMix64 into the four state words, which is the
/// seeding procedure the xoshiro file recommends.
/// </summary>
public class Xoshiro256StarStarTests
{
    // SplitMix64(0) state, then the first outputs of xoshiro256**.
    private static readonly ulong[] SeedZeroState =
    [
        0xe220a8397b1dcdafUL,
        0x6e789e6aa1b965f4UL,
        0x06c45d188009454fUL,
        0xf88bb8a8724c81ecUL,
    ];

    private static readonly ulong[] SeedZeroOutputs =
    [
        0xdec90d521e93e35dUL,
        0x98111e5867aa96c4UL,
        0x03bf383cda766ddfUL,
        0x6d63eb71794f8875UL,
        0xeea58f407147061cUL,
        0xfc42cb017ae11e87UL,
    ];

    private static readonly ulong[] ExplicitStateOutputs =
    [
        0x9999999999998192UL,
        0x8181818181816c7aUL,
        0xc37110472cd7c4e5UL,
        0x539ecceed444c8b5UL,
        0x8102a6dca48cd2c8UL,
        0x9fa9fa03f8006179UL,
    ];

    [Fact]
    public void FromSeedZeroMatchesSplitMixAndReferenceOutputs()
    {
        var rng = Xoshiro256StarStar.FromSeed(0);

        Assert.Equal(new RngState(SeedZeroState[0], SeedZeroState[1], SeedZeroState[2], SeedZeroState[3]), rng.State);
        Assert.Equal(SeedZeroOutputs, ReadULongs(rng, SeedZeroOutputs.Length));
    }

    [Fact]
    public void ExplicitStateMatchesReferenceOutputs()
    {
        var rng = new Xoshiro256StarStar(
            0x0123456789ABCDEFUL,
            0xFEDCBA9876543210UL,
            0x0F1E2D3C4B5A6978UL,
            0x8877665544332211UL);

        Assert.Equal(ExplicitStateOutputs, ReadULongs(rng, ExplicitStateOutputs.Length));
    }

    [Fact]
    public void SameSeedProducesTheSameSequence()
    {
        var left = Xoshiro256StarStar.FromSeed(42);
        var right = Xoshiro256StarStar.FromSeed(42);

        Assert.Equal(ReadULongs(left, 32), ReadULongs(right, 32));
    }

    [Fact]
    public void RestoringStateReproducesTheSequence()
    {
        var rng = Xoshiro256StarStar.FromSeed(99);
        _ = rng.NextULong();
        var saved = rng.State;
        var expected = ReadULongs(rng, 8);

        rng.State = saved;
        Assert.Equal(expected, ReadULongs(rng, expected.Length));

        var restored = new Xoshiro256StarStar(saved);
        Assert.Equal(expected, ReadULongs(restored, expected.Length));
    }

    [Fact]
    public void ReadingStateDoesNotAdvanceTheGenerator()
    {
        var rng = Xoshiro256StarStar.FromSeed(7);
        var before = rng.State;
        var again = rng.State;
        Assert.Equal(before, again);

        var first = rng.NextULong();
        rng.State = before;
        Assert.Equal(first, rng.NextULong());
    }

    [Fact]
    public void AllZeroStateIsRejectedAndAFailedWriteDoesNotChangeState()
    {
        Assert.Throws<ArgumentException>(() => new Xoshiro256StarStar(0, 0, 0, 0));

        var rng = Xoshiro256StarStar.FromSeed(1);
        var saved = rng.State;
        Assert.Throws<ArgumentException>(() => rng.State = new RngState(0, 0, 0, 0));
        Assert.Equal(saved, rng.State);
    }

    [Fact]
    public void NextDoubleMatchesTheReferenceBitPatternAndStaysInsideTheUnitInterval()
    {
        var rng = Xoshiro256StarStar.FromSeed(0);
        var value = rng.NextDouble();

        // First output 0xdec90d521e93e35d mapped with (x >> 11) * 2^-53.
        Assert.Equal(0x3febd921aa43d27cUL, (ulong)BitConverter.DoubleToInt64Bits(value));
        Assert.True(value >= 0.0);
        Assert.True(value < 1.0);

        for (var i = 0; i < 256; i++)
        {
            var sample = Xoshiro256StarStar.FromSeed((ulong)i).NextDouble();
            Assert.True(sample >= 0.0);
            Assert.True(sample < 1.0);
        }
    }

    [Fact]
    public void NextIntUsesHighBitsInsteadOfALowBitModulo()
    {
        // First raw output of seed 0 is 0xdec90d521e93e35d.
        // Low 8 bits are 0x5d (93). Lemire's method takes the high bits of the low 32: 0x1e (30).
        var rng = Xoshiro256StarStar.FromSeed(0);
        Assert.Equal(30, rng.NextInt(0, 256));
    }

    [Fact]
    public void NextIntMatchesAnIndependentBoundedSequence()
    {
        Assert.Equal([0, 1, 2, 1, 1, 1, 0, 0], ReadInts(Xoshiro256StarStar.FromSeed(0), 0, 3, 8));
        Assert.Equal([-2, 0, 2, 0, 0, 0, -1, -2], ReadInts(Xoshiro256StarStar.FromSeed(0), -2, 3, 8));
    }

    [Fact]
    public void NextIntRejectsTheBiasedResidueInsteadOfUsingIt()
    {
        const int bound = (1 << 30) + 1;
        uint range = bound;
        uint threshold = unchecked((uint)(0u - range)) % range;
        Assert.True(threshold > 1);

        var rng = Xoshiro256StarStar.FromSeed(12345);
        RngState? biased = null;
        for (var i = 0; i < 10_000 && biased is null; i++)
        {
            var saved = rng.State;
            var draw = rng.NextULong();
            // Lemire rejects when the low half of (low32 * range) falls under the threshold,
            // not when the raw draw itself is small.
            ulong product = (uint)draw * (ulong)range;
            if ((uint)product < threshold)
            {
                biased = saved;
            }
        }

        Assert.NotNull(biased);

        var singleStep = new Xoshiro256StarStar(biased.Value);
        _ = singleStep.NextULong();

        var sampled = new Xoshiro256StarStar(biased.Value);
        var value = sampled.NextInt(0, bound);

        Assert.InRange(value, 0, bound - 1);
        Assert.NotEqual(singleStep.State, sampled.State);
    }

    [Fact]
    public void NextIntRejectsAnEmptyOrReversedInterval()
    {
        var rng = Xoshiro256StarStar.FromSeed(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(4, 2));
    }

    [Theory]
    [InlineData(int.MinValue, int.MinValue + 1)]
    [InlineData(int.MaxValue - 1, int.MaxValue)]
    [InlineData(-10, -5)]
    public void NextIntCoversTheSingleOrNegativeEdge(int minInclusive, int maxExclusive)
    {
        var rng = Xoshiro256StarStar.FromSeed(3);
        for (var i = 0; i < 16; i++)
        {
            var value = rng.NextInt(minInclusive, maxExclusive);
            Assert.True(value >= minInclusive);
            Assert.True(value < maxExclusive);
        }
    }

    private static ulong[] ReadULongs(Xoshiro256StarStar rng, int count)
    {
        var values = new ulong[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = rng.NextULong();
        }

        return values;
    }

    private static int[] ReadInts(Xoshiro256StarStar rng, int minInclusive, int maxExclusive, int count)
    {
        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = rng.NextInt(minInclusive, maxExclusive);
        }

        return values;
    }
}
