using Paddock.Application.Career;
using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.SimRunner.Calibration;
using Paddock.Tests.Career;
using Xunit.Abstractions;

namespace Paddock.Tests.Racing;

/// <summary>
/// Issue #227: the races of a career must finish at the rate the race engine was calibrated to (TECH §8, PP-062), not at a fifth of it.
/// The band is <see cref="CalibrationTargets"/>, the same one <c>calibrate-race</c> judges a season against: the whole 1955 season,
/// several seeds summed. It uses the Chaos preset (generated people), so it needs no Jolpica cache and gives the same world in CI.
/// </summary>
public class CareerRaceFinishRateTests(ITestOutputHelper output)
{
    private static readonly ulong[] Seeds = [1, 2, 3];

    [Fact]
    public void WholeSeasonOf1955Career_FinishRateIsWithinCalibrationBand()
    {
        var starters = 0;
        var finishers = 0;
        var races = 0;
        foreach (var seed in Seeds)
        {
            var opened = CareerKit.Opened(CareerPreset.Chaos, 1955, seed);
            _ = CareerHost.RunUntil(opened.Session, new GameDate(1955, 12, 1), null, CareerKit.OptionsFor(opened));

            var archive = opened.Session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName);
            Assert.NotNull(archive);
            races += archive.Races.Count;
            starters += archive.Races.Sum(race => race.Rows.Count);
            finishers += archive.Races.Sum(race => race.Rows.Count(row => row.Classified));
        }

        output.WriteLine($"Finish rate {(double)finishers / starters:P1}: {finishers} of {starters} starters over {races} races, seeds {string.Join(",", Seeds)}.");
        Assert.True(races >= 6 * Seeds.Length, $"Expected a whole season of races per seed, got {races}.");
        var rate = (double)finishers / starters;
        Assert.True(
            Math.Abs(rate - CalibrationTargets.FinishRate1950s) <= CalibrationTargets.FinishRateTolerance,
            $"Finish rate {rate:P1} ({finishers}/{starters} over {races} races, seeds {string.Join(",", Seeds)}) is outside {CalibrationTargets.FinishRate1950s:P1} +- {CalibrationTargets.FinishRateTolerance:P0}.");
    }
}
