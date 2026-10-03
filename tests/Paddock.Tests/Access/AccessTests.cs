using Paddock.Application.Access;

namespace Paddock.Tests.Access;

public class AccessTests
{
    private static readonly ManagerId Alice = new("alice");
    private static readonly ManagerId Bob = new("bob");
    private static readonly FactKey Power = new("car.1.power");

    private sealed class FixedProvider(ManagerId manager, FactKey fact) : IKnowledgeProvider
    {
        public bool Knows(ManagerId m, FactKey f) => m == manager && f == fact;
    }

    [Fact]
    public void ContextsCarryKindAndManager()
    {
        Assert.Equal(AccessKind.Developer, AccessContext.Developer.Kind);
        Assert.Null(AccessContext.Developer.Manager);
        Assert.Equal(Alice, AccessContext.ForManager(Alice).Manager);
        Assert.Equal(AccessKind.Ai, AccessContext.ForAi(Bob).Kind);
    }

    [Fact]
    public void DeveloperSeesEverythingEvenWithoutProviders()
    {
        var policy = new DefaultVisibilityPolicy([]);
        Assert.True(policy.CanSee(AccessContext.Developer, Power));
    }

    [Fact]
    public void ManagersSeeOnlyWhatAProviderReveals()
    {
        var policy = new DefaultVisibilityPolicy([new FixedProvider(Alice, Power)]);

        Assert.True(policy.CanSee(AccessContext.ForManager(Alice), Power));
        Assert.True(policy.CanSee(AccessContext.ForAi(Alice), Power));
        Assert.False(policy.CanSee(AccessContext.ForManager(Bob), Power));
        Assert.False(policy.CanSee(AccessContext.ForManager(Alice), new FactKey("car.1.reliability")));
    }

    [Fact]
    public void ManagersSeeNothingWithoutProviders()
    {
        var policy = new DefaultVisibilityPolicy([]);
        Assert.False(policy.CanSee(AccessContext.ForManager(Alice), Power));
    }

    [Fact]
    public void DefaultKnownIsUnknown()
    {
        Known<double> value = default;
        Assert.Equal(KnownKind.Unknown, value.Kind);
        Assert.False(value.TryGetExact(out _));
        Assert.False(value.TryGetBand(out _));
        Assert.Equal(Known<double>.Unknown, value);
    }

    [Fact]
    public void ExactAndBandedExposeTheirValues()
    {
        var exact = Known<int>.Exact(7);
        Assert.True(exact.TryGetExact(out var seven));
        Assert.Equal(7, seven);
        Assert.False(exact.TryGetBand(out _));

        var band = Known<int>.Banded(3, 9);
        Assert.False(band.TryGetExact(out _));
        Assert.True(band.TryGetBand(out var range));
        Assert.Equal(new Band<int>(3, 9), range);
    }

    [Fact]
    public void BandedRejectsInvertedRange()
    {
        Assert.Throws<ArgumentException>(() => Known<int>.Banded(9, 3));
    }

    [Fact]
    public void NarrowingStaysInsideTheBand()
    {
        var narrowed = Known<int>.Banded(0, 10).Narrow(4, 6);
        Assert.True(narrowed.TryGetBand(out var band));
        Assert.Equal(new Band<int>(4, 6), band);

        Assert.Throws<ArgumentException>(() => Known<int>.Banded(4, 6).Narrow(3, 6));
        Assert.Throws<ArgumentException>(() => Known<int>.Banded(4, 6).Narrow(7, 8));
        Assert.Throws<InvalidOperationException>(() => Known<int>.Exact(5).Narrow(5, 5));
    }

    [Fact]
    public void UnknownCanBeNarrowedToABand()
    {
        Assert.Equal(Known<int>.Banded(1, 2), Known<int>.Unknown.Narrow(1, 2));
    }

    [Fact]
    public void RevealingNeedsTheValueInsideTheBand()
    {
        Assert.Equal(Known<int>.Exact(5), Known<int>.Banded(4, 6).Reveal(5));
        Assert.Equal(Known<int>.Exact(99), Known<int>.Unknown.Reveal(99));
        Assert.Throws<ArgumentException>(() => Known<int>.Banded(4, 6).Reveal(7));
        Assert.Throws<InvalidOperationException>(() => Known<int>.Exact(5).Reveal(5));
    }

    [Fact]
    public void KnownHasNoConversionToTheUnderlyingValue()
    {
        var operators = typeof(Known<double>)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name is "op_Implicit" or "op_Explicit");
        Assert.Empty(operators);
    }
}
