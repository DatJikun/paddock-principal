using Paddock.SimRunner.Calibration;
using Paddock.Tests.Racing.Weekend;

namespace Paddock.Tests.SimRunner;

/// <summary><c>calibrate-race</c> (#122): the pooled figures, the dry-day wet tyre rate, and the argument handling. The historical side needs the local Jolpica cache, which is never in the repo, so it is not run here.</summary>
public class CalibrateRaceTests
{
    [Fact]
    public void Figures_PoolTheSamplesByCounts()
    {
        var races = WeekendTestKit.Races(1988, 3);
        var samples = races.Select(r => RaceSample.From(r, 1988)).ToList();

        var figures = EraFigures.FromSamples(samples);

        Assert.Equal(3, figures.Races);
        Assert.Equal(races.Sum(r => r.CarResults.Length), figures.Starters);
        Assert.Equal(samples.Sum(s => s.Finishers) / (double)figures.Starters, figures.FinishRate, 12);
        Assert.InRange(figures.MechanicalShareOfRetirements + figures.AccidentShareOfRetirements, 0d, 1d);
        Assert.Equal(races.Sum(r => r.CarResults.Sum(c => c.Stops)) / (double)figures.Starters, figures.StopsPerCar!.Value, 12);
        Assert.All(samples, s => Assert.True(s.LeadChanges >= 0));
    }

    [Fact]
    public void Median_AndRatio_HandleEmptyAndEvenInput()
    {
        Assert.Null(EraFigures.Median([]));
        Assert.Equal(2.5, EraFigures.Median([4d, 1d, 2d, 3d]));
        Assert.Equal(3d, EraFigures.Median([5d, 1d, 3d]));
        Assert.Null(EraFigures.Ratio(1, 0));
        Assert.Equal(0.25, EraFigures.Ratio(1, 4));
    }

    // Issue #122: on dry race days ~6.5 percent of 1988 cars and ~15 percent of 2012 cars fitted wet tyres, because the forecast
    // probability ignored climatology. The pit wall fitting wet tyres on a dry day is a bug, so the whole-race rate is pinned here.
    [Theory]
    [InlineData(1988, 12)]
    [InlineData(2012, 5)]
    public void OnDryDays_AlmostNoCarFitsWetTyres(int season, int races)
    {
        var samples = WeekendTestKit.Races(season, races).Select(r => RaceSample.From(r, season)).Where(s => s.DryDay).ToList();

        Assert.NotEmpty(samples);
        var share = samples.Sum(s => s.CarsOnWetTyres) / (double)samples.Sum(s => s.Starters);
        Assert.True(share <= CalibrationTargets.WetTyresOnDryDayMax, $"{share:P1} of the cars fitted wet tyres on dry days in {season}.");
    }

    [Fact]
    public void TheBands_CoverEverySeasonOnceInOrder()
    {
        var bands = CalibrationTargets.Bands;
        Assert.Equal(1950, bands[0].From);
        Assert.Equal(2025, bands[^1].To);
        for (var i = 1; i < bands.Count; i++)
        {
            Assert.Equal(bands[i - 1].To + 1, bands[i].From);
        }

        Assert.All(bands, b => Assert.True(b.StopsLow <= b.StopsHigh));
    }

    [Theory]
    [InlineData("calibrate-race")]
    [InlineData("calibrate-race", "--from", "2000")]
    [InlineData("calibrate-race", "--from", "2010", "--to", "2000")]
    [InlineData("calibrate-race", "--from", "2000", "--to", "2001", "--stride", "0")]
    [InlineData("calibrate-race", "--from", "2000", "--to", "2001", "--bogus", "1")]
    public void BadArguments_PrintTheUsage_AndFail(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = CalibrateRaceCommand.Execute(args, stdout, stderr);

        Assert.Equal(1, code);
        Assert.Contains("Usage: calibrate-race", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingCache_IsReportedNotInvented()
    {
        var missing = Path.Combine(Path.GetTempPath(), "paddock-no-such-cache-" + Guid.NewGuid().ToString("N"));
        var stderr = new StringWriter();

        var code = CalibrateRaceCommand.Execute(["calibrate-race", "--from", "2000", "--to", "2000", "--cache", missing], new StringWriter(), stderr);

        Assert.Equal(1, code);
        Assert.Contains("Normalized Jolpica cache not found", stderr.ToString(), StringComparison.Ordinal);
    }
}
