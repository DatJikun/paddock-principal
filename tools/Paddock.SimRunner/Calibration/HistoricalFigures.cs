using Paddock.Data.Historical;
using Paddock.DataPipeline;

namespace Paddock.SimRunner.Calibration;

/// <summary>
/// The historical side of the comparison, from the owner's LOCAL Jolpica cache (PP-041: never committed). The same population
/// rules as <c>stats</c>: shared-drive rows and Indianapolis 500 rounds are left out. Pit stops, laps led and safety cars are not
/// in this cache, so those figures stay null here.
/// </summary>
public static class HistoricalFigures
{
    public static EraFigures For(NormalizedHistoricalData data, IReadOnlySet<int> seasons, Func<int, int, bool?> rainDuringRace)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(seasons);
        ArgumentNullException.ThrowIfNull(rainDuringRace);

        var indy = data.Races.Races.Where(r => r.IsIndianapolis500).Select(r => (r.Season, r.Round)).ToHashSet();
        var rows = data.Results.Results
            .Where(r => seasons.Contains(r.Season) && !r.IsSharedDrive && !indy.Contains((r.Season, r.Round)))
            .GroupBy(r => (r.Season, r.Round))
            .ToList();

        int starters = 0, finishers = 0, mech = 0, acc = 0, lapped = 0, poleWins = 0, races = 0, rainKnown = 0, rainYes = 0;
        var margins = new List<double>();
        foreach (var race in rows)
        {
            races++;
            foreach (var row in race)
            {
                var kind = Paddock.DataPipeline.FinishStatus.Classify(row.Status);
                if (kind == Paddock.DataPipeline.FinishStatus.Kind.NonStart)
                {
                    continue;
                }

                starters++;
                switch (kind)
                {
                    case Paddock.DataPipeline.FinishStatus.Kind.ClassifiedFinish:
                        finishers++;
                        if (row.Status.StartsWith('+') || row.Status == "Lapped")
                        {
                            lapped++;
                        }

                        break;
                    case Paddock.DataPipeline.FinishStatus.Kind.Mechanical:
                        mech++;
                        break;
                    case Paddock.DataPipeline.FinishStatus.Kind.Accident:
                        acc++;
                        break;
                }
            }

            var winner = race.FirstOrDefault(r => r.IsClassified && r.PositionText == "1");
            if (winner is not null && winner.Grid == 1)
            {
                poleWins++;
            }

            var second = race.FirstOrDefault(r => r.IsClassified && r.PositionText == "2");
            if (winner?.TimeMillis is long w && second?.TimeMillis is long s && s >= w)
            {
                margins.Add((s - w) / 1000d);
            }

            if (rainDuringRace(race.Key.Season, race.Key.Round) is { } rain)
            {
                rainKnown++;
                if (rain)
                {
                    rainYes++;
                }
            }
        }

        var retired = starters - finishers;
        return new EraFigures(
            races,
            starters,
            EraFigures.Ratio(finishers, starters) ?? 0,
            EraFigures.Ratio(mech, retired) ?? 0,
            EraFigures.Ratio(acc, retired) ?? 0,
            null,
            EraFigures.Median(margins),
            EraFigures.Ratio(lapped, finishers) ?? 0,
            EraFigures.Ratio(poleWins, races) ?? 0,
            null,
            null,
            null,
            EraFigures.Ratio(rainYes, rainKnown) ?? 0);
    }
}
