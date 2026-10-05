using Paddock.Application.Career;
using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.SimRunner.Calibration;
using Paddock.Tests.Career;

namespace Paddock.Tests.Racing;

/// <summary>
/// Issue #227: career races must produce finish rates within the race engine calibration band (TECH §8, PP-062).
/// </summary>
public class CareerRaceFinishRateTests
{
    // The 1950s calibration band from calibrate-race (#122): simulated rate is ~46.7%, tolerance +-0.08 abs.
    private const double EraCalibrationFinishRate = 0.467;

    [Theory]
    [InlineData(CareerPreset.Balanced, 1UL)]
    [InlineData(CareerPreset.Balanced, 2UL)]
    [InlineData(CareerPreset.Chaos, 1UL)]
    [InlineData(CareerPreset.Chaos, 2UL)]
    public void FirstRoundsOf1955Career_FinishRateIsWithinCalibrationBand(CareerPreset preset, ulong seed)
    {
        if (preset == CareerPreset.Balanced && !CareerKit.HasRealPeopleCache)
        {
            return;
        }

        var opened = CareerKit.Opened(preset, 1955, seed, "ferrari");
        _ = CareerHost.RunUntil(opened.Session, new GameDate(1955, 8, 1), null, CareerKit.OptionsFor(opened));

        var archive = opened.Session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName);
        Assert.NotNull(archive);
        Assert.True(archive.Races.Count >= 4, $"Expected at least 4 races, got {archive.Races.Count}");

        var starters = archive.Races.Sum(r => r.Rows.Count);
        var finishers = archive.Races.Sum(r => r.Rows.Count(row => row.Classified));
        var finishRate = (double)finishers / starters;

        var details = string.Join("\n", archive.Races.SelectMany((race, i) =>
            race.Rows.Select(r => $"R{i + 1} P{r.Position}. {r.TeamId} {r.DriverId} - {(r.Classified ? "OK" : r.RetirementKey)}")));

        Assert.True(
            Math.Abs(finishRate - EraCalibrationFinishRate) <= CalibrationTargets.FinishRateTolerance,
            $"Expected finish rate within {CalibrationTargets.FinishRateTolerance:P0} of {EraCalibrationFinishRate:P1}, but got {finishers}/{starters} ({finishRate:P1}). Details:\n{details}");
    }
}
