using Paddock.Data.Authored;
using Paddock.Domain.Cars;

namespace Paddock.Tests.Authored;

/// <summary>
/// #227: the authored ESTIMATE of constructor car strength for 1954-1955, the stand-in for the ratings car effect (PP-041).
/// </summary>
public sealed class CarStrengthTests
{
    private static readonly string DataRoot = Path.Combine(RepoPaths.Root(), "data");

    [Fact]
    public void TheFileIsAnEstimateAndEveryConstructorIsOneTheTeamsDataKnows()
    {
        var file = CarStrengthLoader.Load(DataRoot);
        var data = AuthoredDataLoader.Load(DataRoot);

        Assert.Empty(CarStrengthValidator.Validate(file, data.ConstructorIds));
        Assert.Contains("ESTIMATE", file.Notes);
        Assert.NotEmpty(file.Strengths);
    }

    [Fact]
    public void TheAuthoredDataCarriesTheStrengthsAndAnUnlistedConstructorHasNone()
    {
        var source = AuthoredDataLoader.Load(DataRoot).CarStrength;

        Assert.NotNull(source);
        Assert.True(source.TryGet("mercedes", 1955, out var mercedes));
        Assert.True(source.TryGet("ferrari", 1955, out var ferrari));
        Assert.True(mercedes > ferrari);
        Assert.False(source.TryGet("pawl", 1955, out _));
        Assert.False(source.TryGet("mercedes", 1950, out _));
    }

    [Fact]
    public void AFragileCarStartsWithLowerReliabilityThanItsStrengthAndOthersHaveNoSuchRow()
    {
        var source = AuthoredDataLoader.Load(DataRoot).CarStrength!;

        foreach (var constructor in new[] { "lancia", "vanwall", "gordini" })
        {
            Assert.True(source.TryGet(constructor, 1955, out var strength));
            Assert.True(source.TryGetReliability(constructor, 1955, out var reliability));
            Assert.True(reliability < strength, constructor);
        }

        Assert.False(source.TryGetReliability("mercedes", 1955, out _));
    }

    [Fact]
    public void TheValidatorRefusesAnUnknownConstructorARepeatedRowAnOutOfRangeStrengthAndAFileThatDoesNotSayEstimate()
    {
        var known = new HashSet<string>(["a"], StringComparer.Ordinal);
        var bad = new CarStrengthFile(
            "no hedge",
            [
                new CarStrengthEntry(1954, "a", 50, "estimate", "x"),
                new CarStrengthEntry(1954, "a", 51, "high", "x"),
                new CarStrengthEntry(1954, "ghost", 50, "estimate", "x"),
                new CarStrengthEntry(1949, "a", 120, "estimate", "x"),
            ]);

        var codes = CarStrengthValidator.Validate(bad, known).Select(error => error.Code).ToHashSet();

        Assert.Contains(CarStrengthValidator.NotEstimate, codes);
        Assert.Contains(CarStrengthValidator.UnknownConstructor, codes);
        Assert.Contains(CarStrengthValidator.Duplicate, codes);
        Assert.Contains(CarStrengthValidator.BadNumber, codes);
    }
}
