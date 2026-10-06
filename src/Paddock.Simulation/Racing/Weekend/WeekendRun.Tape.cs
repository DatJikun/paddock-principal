using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Weather;

namespace Paddock.Simulation.Racing.Weekend;

internal sealed partial class WeekendRun
{
    /// <summary>How a car's race stood once the winner had taken the flag.</summary>
    private sealed record Outcome(Car Car, FinishStatus Status, int Laps, double TotalSeconds, bool Finished);

    private RaceWeekendResult Finish(Qualifying.QualifyingResult qualifying)
    {
        var endLap = _endLap;
        var winnerTime = _cars.Where(c => c.Retired is null).Select(c => c.EndTimes[endLap - 1]).DefaultIfEmpty(double.PositiveInfinity).Min();

        var outcomes = new List<Outcome>(_cars.Count);
        foreach (var car in _cars)
        {
            // Laps counted at the flag: what the car had completed when the winner finished, plus the lap it was on.
            var completed = 0;
            while (completed < car.EndTimes.Count && car.EndTimes[completed] <= winnerTime)
            {
                completed++;
            }

            var stands = false;
            if (car.Retired is { } retired)
            {
                // A retirement after the flag does not stand when the car had crossed the line behind the winner meanwhile.
                stands = retired.TimeSeconds <= winnerTime || completed == car.EndTimes.Count;
                car.RetiredAfterFlagStands = stands;
            }

            if (stands)
            {
                var retired2 = car.Retired!;
                var laps = car.EndTimes.Count;
                var status = retired2.Reason switch
                {
                    RetirementReason.Mechanical => FinishStatus.Mechanical,
                    RetirementReason.Accident => FinishStatus.Accident,
                    _ => FinishStatus.Other,
                };
                outcomes.Add(new Outcome(car, status, laps, laps > 0 ? car.EndTimes[laps - 1] : 0d, false));
            }
            else
            {
                var laps = Math.Min(endLap, completed + 1);
                outcomes.Add(new Outcome(car, FinishStatus.Classified, laps, car.EndTimes[laps - 1], true));
            }
        }

        var ordered = outcomes
            .OrderByDescending(o => o.Laps)
            .ThenBy(o => o.TotalSeconds)
            .ThenBy(o => o.Car.Grid)
            .ToList();

        // The fastest lap of the race: the best lap among the laps that count (completed before the flag took the car).
        var bestMs = long.MaxValue;
        foreach (var o in ordered)
        {
            for (var j = 0; j < o.Laps; j++)
            {
                bestMs = Math.Min(bestMs, Ms(o.Car.LapSeconds[j]));
            }
        }

        // A stop made after the car had been flagged (a lapped car) never happened for the result.
        var lapsByGrid = ordered.ToDictionary(o => o.Car.Grid, o => o.Laps);
        var carByGrid = ordered.ToDictionary(o => o.Car.Entry.CarId, o => o.Car, StringComparer.Ordinal);
        var counted = _pitStops.Where(s => s.Lap <= lapsByGrid[carByGrid[s.CarId].Grid]).ToList();

        var results = ImmutableArray.CreateBuilder<CarRaceResult>(ordered.Count);
        foreach (var o in ordered)
        {
            var car = o.Car;
            var carStops = counted.Where(s => s.CarId == car.Entry.CarId).ToList();
            var swapped = carStops.Any(s => s.DriverSwap);
            var fastest = false;
            for (var j = 0; j < o.Laps && !fastest; j++)
            {
                fastest = Ms(car.LapSeconds[j]) == bestMs;
            }

            results.Add(new CarRaceResult(
                car.Entry.CarId,
                car.TapeId,
                car.Entry.ConstructorId,
                car.Grid,
                o.Status,
                o.Laps,
                o.TotalSeconds,
                o.Finished ? null : car.Retired!.Lap,
                o.Finished ? null : car.Retired!.Component,
                swapped ? [car.Entry.Drivers[0].DriverId, car.Entry.Drivers[1].DriverId] : [car.Entry.Drivers[0].DriverId],
                carStops.Count,
                [car.Used[0], .. carStops.Where(s => s.ChangedTyres).Select(s => s.CompoundAfter)],
                fastest,
                !o.Finished && car.Retired?.Sudden == true));
        }

        var classification = RaceClassifier.Classify(
            _pointsRules,
            results.Select(r => r.ToInput()).ToList(),
            new RaceContext(_in.TotalLaps, _in.IsFinalRound));

        var tape = BuildTape(ordered, classification, winnerTime, lapsByGrid);
        var persons = BuildPersonOutcomes(ordered, results.ToImmutable());

        return new RaceWeekendResult(
            qualifying,
            tape,
            results.ToImmutable(),
            classification,
            persons,
            _weather,
            [.. _lapRecords],
            [.. counted],
            [.. _neutralisations],
            _in.TotalLaps,
            endLap);
    }

    private static ImmutableArray<PersonRaceOutcome> BuildPersonOutcomes(List<Outcome> ordered, ImmutableArray<CarRaceResult> results)
    {
        var people = ImmutableArray.CreateBuilder<PersonRaceOutcome>();
        for (var index = 0; index < ordered.Count; index++)
        {
            var o = ordered[index];
            var car = o.Car;
            var count = results[index].DriversWhoDrove.Length;
            for (var d = 0; d < count; d++)
            {
                var driver = car.Entry.Drivers[d];
                var retire = !o.Finished && car.Retired is { } r && r.DriverId == driver.DriverId ? r : null;
                people.Add(new PersonRaceOutcome(
                    driver.DriverId,
                    car.Entry.CarId,
                    car.LapsDriven[d],
                    retire is not null,
                    retire?.Reason,
                    retire?.Component,
                    retire?.Lap,
                    retire?.Injury ?? Incidents.InjuryGrade.None,
                    retire?.Fatal ?? false));
            }
        }

        return people.ToImmutable();
    }

    // ---- The tape -----------------------------------------------------------------------------------------

    private void AddWeatherEvents()
    {
        var horizon = _leaderEnds.Where(t => !double.IsNaN(t)).DefaultIfEmpty(0d).Last();
        var previous = _weather.Samples[0].WetnessBand;
        for (var minute = 1; minute <= _weather.DurationMinutes; minute++)
        {
            var time = minute * 60d;
            if (time > horizon)
            {
                break;
            }

            var band = _weather.Samples[minute].WetnessBand;
            if (band == previous)
            {
                continue;
            }

            previous = band;
            var lap = 1;
            while (lap < _endLap && lap <= _leaderEnds.Count && _leaderEnds[lap - 1] <= time)
            {
                lap++;
            }

            var condition = band.ToString().ToLowerInvariant();
            var eventLap = lap;
            _raw.Add(new RawEvent(time, CatWeather, 0, minute, eventLap, (seq, ms) => new WeatherChange(seq, eventLap, ms, condition)));
        }
    }

    private RaceTape BuildTape(List<Outcome> ordered, RaceClassification classification, double winnerTime, Dictionary<int, int> lapsByGrid)
    {
        var position = classification.Cars.ToDictionary(c => c.DriverIds[0], c => c.Position, StringComparer.Ordinal);
        var items = new List<RawEvent>(_raw.Count + (_cars.Count * (_endLap + 2)));
        foreach (var e in _raw)
        {
            if (e.DropAfterFlag && e.TimeSeconds > winnerTime)
            {
                continue;
            }

            if (e.RetirementOfCar is { } grid && _cars[grid - 1].RetiredAfterFlagStands is false)
            {
                continue;
            }

            if (e.Category is CatPitIn or CatPitOut && e.Lap > lapsByGrid[e.CarIndex])
            {
                continue;
            }

            items.Add(e);
        }

        foreach (var o in ordered)
        {
            var car = o.Car;
            var id = car.TapeId;
            for (var j = 1; j <= o.Laps; j++)
            {
                var lap = j;
                var time = car.EndTimes[j - 1];
                var lapMs = Ms(car.LapSeconds[j - 1]);
                var rank = car.Ranks[j - 1];
                var gapMs = Ms(time) - Ms(_leaderEnds[j - 1]);
                items.Add(new RawEvent(time, CatLap, car.Grid, 0, lap, (seq, ms) => new LapCompleted(seq, lap, ms, id, lapMs, rank, gapMs)));
                var before = j == 1 ? car.Grid : car.Ranks[j - 2];
                if (before != rank)
                {
                    items.Add(new RawEvent(time, CatPosition, car.Grid, 0, lap, (seq, ms) => new PositionChange(seq, lap, ms, id, before, rank)));
                }
            }

            if (o.Finished)
            {
                var laps = o.Laps;
                var total = Ms(o.TotalSeconds);
                var place = position[id];
                items.Add(new RawEvent(o.TotalSeconds, CatFinish, car.Grid, 0, laps, (seq, ms) => new Finished(seq, laps, ms, id, place, laps, total)));
            }
        }

        items.Sort(static (a, b) =>
        {
            var c = a.TimeSeconds.CompareTo(b.TimeSeconds);
            if (c != 0)
            {
                return c;
            }

            c = a.Category.CompareTo(b.Category);
            if (c != 0)
            {
                return c;
            }

            c = a.CarIndex.CompareTo(b.CarIndex);
            return c != 0 ? c : a.Sub.CompareTo(b.Sub);
        });

        var builder = new RaceTape.Builder();
        var seq = 0;
        builder.Append(new RaceStarted(seq++, 0, 0, _in.TotalLaps, [.. _cars.Select(c => c.TapeId)]));
        var best = long.MaxValue;
        var last = 0L;
        foreach (var item in items)
        {
            var ms = Ms(item.TimeSeconds);
            var raceEvent = item.Make(seq++, ms);
            builder.Append(raceEvent);
            last = Math.Max(last, ms);
            if (raceEvent is LapCompleted lc && lc.LapTimeMs > 0 && lc.LapTimeMs < best)
            {
                best = lc.LapTimeMs;
                builder.Append(new FastestLap(seq++, lc.Lap, lc.RaceTime, lc.DriverId, lc.LapTimeMs));
            }
        }

        builder.Append(new RaceEnded(seq, _endLap, last));
        return builder.Build();
    }
}
