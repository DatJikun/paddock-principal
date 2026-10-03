using System.Collections.Immutable;
using Paddock.Simulation.Racing.Tyres;

namespace Paddock.Tests.Racing.Tyres;

/// <summary>The catalog under test is a synthetic ESTIMATE fixture, see TyreFuelConstants.</summary>
public class TyreCompoundCatalogTests
{
    private static readonly TyreCompoundCatalog Catalog = TyreCompoundCatalog.Default;

    [Fact]
    public void Era1955_HasOneCompound() => Assert.Single(Catalog.AvailableCompounds(1955));

    [Fact]
    public void Era1985_HasMoreCompounds_WithQualifyingSpecial()
    {
        var compounds = Catalog.AvailableCompounds(1985);
        Assert.True(compounds.Length > 1);
        Assert.Contains(compounds, c => c.Kind == TyreCompoundKind.QualifyingSpecial);
        Assert.Equal(3, Catalog.RaceCompounds(1985).Length);
    }

    [Fact]
    public void Era2020_HasFiveNumberedCompounds()
    {
        var compounds = Catalog.AvailableCompounds(2020);
        Assert.Equal(5, compounds.Length);
        Assert.Equal(["C5", "C4", "C3", "C2", "C1"], compounds.Select(c => c.Id));
    }

    [Fact]
    public void EverySeasonFrom1950To2026_HasCompounds_SoftestFirst()
    {
        for (var season = 1950; season <= 2026; season++)
        {
            var compounds = Catalog.AvailableCompounds(season);
            Assert.NotEmpty(compounds);
            Assert.Equal(compounds.OrderBy(c => c.Hardness), compounds);
        }
    }

    [Fact]
    public void SofterCompounds_GripMoreButWearFaster_AndCliffSooner()
    {
        for (var season = 1950; season <= 2026; season++)
        {
            var race = Catalog.RaceCompounds(season);
            for (var i = 1; i < race.Length; i++)
            {
                Assert.True(race[i].GripBase > race[i - 1].GripBase, $"{season} {race[i].Id} grip");
                Assert.True(race[i].WearRate < race[i - 1].WearRate, $"{season} {race[i].Id} wear");
                Assert.True(race[i].CliffLap > race[i - 1].CliffLap, $"{season} {race[i].Id} cliff");
            }
        }
    }

    [Fact]
    public void QualifyingSpecial_IsFasterThanAnyRaceCompound_AndDiesQuickly()
    {
        var special = Catalog.AvailableCompounds(1985).Single(c => c.Kind == TyreCompoundKind.QualifyingSpecial);
        Assert.All(Catalog.RaceCompounds(1985), c => Assert.True(special.GripBase < c.GripBase));
        Assert.True(special.CliffLap < Catalog.RaceCompounds(1985).Min(c => c.CliffLap));
    }

    [Fact]
    public void CompoundIds_AreUniqueAcrossEras()
    {
        var ids = Enumerable.Range(1950, 77)
            .Select(Catalog.EraOf)
            .Distinct()
            .SelectMany(era => era.Compounds)
            .Select(c => c.Id)
            .Append(Catalog.WetCompound(2000).Id)
            .ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void WetCompound_IsAHook_WithWetKind_AndSlowOnDryTrack()
    {
        var wet = Catalog.WetCompound(1985);
        Assert.Equal(TyreCompoundKind.Wet, wet.Kind);
        Assert.True(wet.GripBase > Catalog.RaceCompounds(1985).Max(c => c.GripBase));
        Assert.Equal(wet, Catalog.WetCompound(2020));
    }

    [Fact]
    public void SeasonBeforeTheFirstEra_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Catalog.AvailableCompounds(1949));

    [Fact]
    public void CustomEras_AreAccepted_AndValidated()
    {
        var one = new TyreCompound("a", TyreCompoundKind.Dry, 0, 0, 0.02, 40, 1);
        var two = new TyreCompound("b", TyreCompoundKind.Dry, 0, 0, 0.02, 40, 1);
        var wet = new TyreCompound("w", TyreCompoundKind.Wet, 9, 4, 0.2, 8, 1);
        TyreCompoundEra Era(string name, int from, int? to, TyreCompound c) => new(name, from, to, [c]);

        var catalog = new TyreCompoundCatalog([Era("x", 2000, null, one)], wet);
        Assert.Single(catalog.AvailableCompounds(2030));

        Assert.Throws<ArgumentException>(() => new TyreCompoundCatalog([Era("x", 2000, 2010, one), Era("y", 2010, null, two)], wet));
        Assert.Throws<ArgumentException>(() => new TyreCompoundCatalog([Era("x", 2000, 2005, one), Era("y", 2006, null, one)], wet));
        Assert.Throws<ArgumentException>(() => new TyreCompoundCatalog([new TyreCompoundEra("x", 2000, null, ImmutableArray<TyreCompound>.Empty)], wet));
        Assert.Throws<ArgumentException>(() => new TyreCompoundCatalog([Era("x", 2000, null, one)], one));
        Assert.Throws<ArgumentException>(() => new TyreCompoundCatalog([], wet));
    }
}
