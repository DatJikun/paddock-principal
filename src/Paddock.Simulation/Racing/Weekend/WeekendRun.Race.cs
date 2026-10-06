using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Pace;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Reliability;
using Paddock.Simulation.Racing.Tyres;
using Paddock.Simulation.Racing.Weather;

namespace Paddock.Simulation.Racing.Weekend;

internal sealed partial class WeekendRun
{
    private readonly List<CarLapRecord> _lapRecords = [];
    private readonly List<PitStopRecord> _pitStops = [];
    private readonly List<NeutralisationRecord> _neutralisations = [];
    private readonly List<RawEvent> _raw = [];
    private readonly List<double> _leaderEnds = [];

    private NeutralisationKind _neutralKind = NeutralisationKind.None;
    private int _neutralLast;
    private int _neutralIncidentLap;

    /// <summary>An event the lap loop noticed, before it is placed on the tape (the tape needs the finish to know what stood).</summary>
    private sealed record RawEvent(double TimeSeconds, int Category, int CarIndex, int Sub, int Lap, Func<int, long, RaceEvent> Make)
    {
        /// <summary>When set, the event belongs to a retirement of this car and is dropped if the car turns out to have finished.</summary>
        public int? RetirementOfCar { get; init; }

        /// <summary>Dropped when it happens after the winner took the flag.</summary>
        public bool DropAfterFlag { get; init; }
    }

    // Order of events at the same instant.
    private const int CatWeather = 0;
    private const int CatIncident = 1;
    private const int CatRetire = 2;
    private const int CatSafety = 3;
    private const int CatPitIn = 4;
    private const int CatPitOut = 5;
    private const int CatLap = 6;
    private const int CatPosition = 7;
    private const int CatRaceStopped = 8;
    private const int CatFinish = 9;

    private sealed class LapWork(Car car, int index, double startCum)
    {
        public Car Car { get; } = car;

        public int Index { get; } = index;

        public double StartCum { get; } = startCum;

        public double Natural { get; set; }

        public double Clean { get; set; }

        public double Noise { get; set; }

        public double Extra { get; set; }

        public double End { get; set; }

        public double StopSeconds { get; set; }

        public bool Held { get; set; }

        public RetireInfo? Retire { get; set; }

        public string Compound { get; set; } = string.Empty;

        public int TyreAge { get; set; }

        public double FuelStart { get; set; }

        public int DriverIndex { get; set; }

        public double RetireTime => StartCum + (0.5d * Natural);
    }

    private void RunRace()
    {
        var order = new List<Car>(_cars);
        for (var i = 0; i < order.Count; i++)
        {
            order[i].Cum = i * WeekendConstants.GridSlotGapSeconds;
        }

        for (var lap = 1; lap <= _in.TotalLaps; lap++)
        {
            if (order.Count == 0)
            {
                break;
            }

            _endLap = lap;
            if (RunLap(lap, ref order))
            {
                break;
            }
        }

        AddWeatherEvents();
    }

    // Returns true when the race is over after this lap (a red flag that does not restart).
    private bool RunLap(int lap, ref List<Car> order)
    {
        var start = order.ToArray();
        var n = start.Length;
        var neutralLap = lap <= _neutralLast && _neutralKind is NeutralisationKind.SafetyCar or NeutralisationKind.VirtualSafetyCar;
        var kind = neutralLap ? _neutralKind : NeutralisationKind.None;

        var work = new LapWork[n];
        for (var i = 0; i < n; i++)
        {
            work[i] = new LapWork(start[i], i, start[i].Cum)
            {
                Compound = start[i].Tyre.Compound.Id,
                TyreAge = start[i].Tyre.AgeLaps,
                FuelStart = start[i].Fuel,
                DriverIndex = start[i].DriverIndex,
            };
        }

        // The pit wall: who is consulted this lap decides pace mode and a stop at the end of the lap.
        var consult = lap == 1 || ((lap - 1) % _in.StrategistCadenceLaps) == 0 || neutralLap;
        for (var i = 0; i < n; i++)
        {
            start[i].Pit = null;
            if (consult)
            {
                Consult(start[i], i, start, lap);
            }
        }

        // Failures arrive from the Failures stream (sampled once per car); fuel runs out.
        foreach (var w in work)
        {
            ApplyFailures(w, lap);
        }

        // Lap times.
        var referenceLap = kind switch
        {
            NeutralisationKind.SafetyCar => _baseLap * WeekendConstants.SafetyCarLapFactor,
            NeutralisationKind.VirtualSafetyCar => _baseLap * WeekendConstants.VirtualSafetyCarLapFactor,
            _ => 0d,
        };
        foreach (var w in work)
        {
            if (kind == NeutralisationKind.None)
            {
                ComputeLap(w, lap, w.Index > 0 ? w.StartCum - work[w.Index - 1].StartCum : double.PositiveInfinity);
            }
            else
            {
                w.Natural = referenceLap;
                w.Clean = referenceLap;
            }

            if (w.Retire is null && w.Car.Fuel + 1e-9 < BurnThisLap(w.Car, kind))
            {
                // Out of fuel: the plan did not cover the race.
                w.Retire = new RetireInfo(lap, w.RetireTime, RetirementReason.Other, null, InjuryGrade.None, false, w.Car.Driver.DriverId);
            }
        }

        foreach (var w in work)
        {
            if (w.Retire is { } r && Math.Abs(r.TimeSeconds - w.RetireTime) > 0d)
            {
                w.Retire = r with { TimeSeconds = w.RetireTime };
            }
        }

        // Incidents (only on racing laps) and what they ask for.
        Neutralisation? requested = null;
        if (kind == NeutralisationKind.None)
        {
            requested = SampleIncidents(work, lap);
        }

        // Pit stops and the order at the end of the lap.
        var survivors = work.Where(w => w.Retire is null).ToList();
        foreach (var w in survivors)
        {
            PlanStop(w, lap, kind);
        }

        switch (kind)
        {
            case NeutralisationKind.SafetyCar:
                ResolveSafetyCar(survivors);
                break;
            case NeutralisationKind.VirtualSafetyCar:
                foreach (var w in survivors)
                {
                    w.End = w.StartCum + w.Natural + w.StopSeconds;
                }

                break;
            default:
                ResolvePasses(survivors);
                break;
        }

        CommitLap(work, lap, kind, neutralLap);

        // New order: by time at the end of the lap, ties keep the order the lap started in.
        var newOrder = survivors.OrderBy(w => w.End).ThenBy(w => w.Index).Select(w => w.Car).ToList();
        order = newOrder;
        _leaderEnds.Add(newOrder.Count > 0 ? newOrder[0].Cum : double.NaN);
        for (var rank = 0; rank < newOrder.Count; rank++)
        {
            var car = newOrder[rank];
            var w = work[Array.IndexOf(start, car)];
            _lapRecords.Add(new CarLapRecord(
                car.Entry.CarId,
                lap,
                car.Entry.Drivers[w.DriverIndex].DriverId,
                w.End - w.StartCum,
                w.End,
                rank + 1,
                w.Noise,
                w.Compound,
                w.TyreAge,
                w.FuelStart,
                neutralLap,
                w.Held));
            car.Ranks.Add(rank + 1);
        }

        // The neutralisation ends, or begins, after this lap.
        if (neutralLap && lap == _neutralLast)
        {
            if (lap < _in.TotalLaps && newOrder.Count > 0)
            {
                var ended = _neutralKind;
                var endTime = newOrder[0].Cum;
                _raw.Add(new RawEvent(endTime, CatSafety, -1, 1, lap, (seq, ms) => new SafetyCar(seq, lap, ms, SafetyCarPhase.Ending, ended == NeutralisationKind.VirtualSafetyCar))
                {
                    DropAfterFlag = true,
                });
            }

            _neutralKind = NeutralisationKind.None;
        }

        if (requested is { } plan && lap < _in.TotalLaps && newOrder.Count > 0)
        {
            return BeginNeutralisation(plan, lap, newOrder);
        }

        return false;
    }

    private bool BeginNeutralisation(Neutralisation plan, int lap, List<Car> newOrder)
    {
        var instigatorTime = _lastIncidentTime;
        switch (plan.Kind)
        {
            case NeutralisationKind.SafetyCar or NeutralisationKind.VirtualSafetyCar:
            {
                var virtualCar = plan.Kind == NeutralisationKind.VirtualSafetyCar;
                var last = Math.Min(_in.TotalLaps, lap + plan.DurationLaps);
                _raw.Add(new RawEvent(instigatorTime, CatSafety, -1, 0, lap, (seq, ms) => new SafetyCar(seq, lap, ms, SafetyCarPhase.Deployed, virtualCar))
                {
                    DropAfterFlag = true,
                });
                _neutralKind = plan.Kind;
                _neutralLast = last;
                _neutralIncidentLap = lap;
                _neutralisations.Add(new NeutralisationRecord(plan.Kind, lap, last, false));
                return false;
            }

            case NeutralisationKind.RedFlag:
            {
                // A red flag that ends the race is shown when the leader completes the lap the race stands at.
                var flagTime = plan.ResumesRace ? instigatorTime : newOrder[0].Cum;
                _raw.Add(new RawEvent(flagTime, plan.ResumesRace ? CatSafety : CatRaceStopped, -1, 2, lap, (seq, ms) => new RedFlag(seq, lap, ms)));
                if (!plan.ResumesRace)
                {
                    _neutralisations.Add(new NeutralisationRecord(NeutralisationKind.RedFlag, lap, lap, false));
                    return true;
                }

                // The race is stopped and restarted: the same order, a standing-start gap, the clock of the race moves on.
                var stoppage = plan.DurationLaps * _baseLap * WeekendConstants.RedFlagStoppageLapFactor;
                var leader = newOrder[0].Cum + stoppage;
                for (var i = 0; i < newOrder.Count; i++)
                {
                    newOrder[i].Cum = leader + (i * WeekendConstants.RestartGapSeconds);
                }

                _neutralisations.Add(new NeutralisationRecord(NeutralisationKind.RedFlag, lap, lap, true));
                return false;
            }

            default:
                return false;
        }
    }

    // ---- The pit wall -------------------------------------------------------------------------------------

    private void Consult(Car car, int index, Car[] start, int lap)
    {
        var minute = car.Cum / 60d;
        var from = Math.Clamp((int)Math.Floor(minute), 0, _weather.DurationMinutes);
        var key = (from, car.Entry.ForecastQuality);
        if (!_forecasts.TryGetValue(key, out var forecast))
        {
            forecast = WeatherForecaster.ForecastFor(_weather, from, WeekendConstants.ForecastHorizonMinutes, car.Entry.ForecastQuality);
            _forecasts[key] = forecast;
        }

        var own = new OwnCarKnowledge(
            car.Entry.CarId,
            car.Tyre.Compound.Id,
            car.Tyre.AgeLaps,
            car.Fuel,
            car.Burn,
            car.Mode,
            [.. car.Used],
            car.Stops,
            car.Entry.Drivers.Length > 1 && !car.Swapped,
            car.StintLaps,
            car.Entry.Crew);
        var gaps = new VisibleGaps(
            index + 1,
            index > 0 ? car.Cum - start[index - 1].Cum : null,
            index < start.Length - 1 ? start[index + 1].Cum - car.Cum : null);
        var snapshot = new KnowledgeSnapshot(
            _in.Season,
            lap,
            _in.TotalLaps,
            car.LastGreenLap ?? _baseLap,
            minute,
            _pitRules,
            _laneLoss,
            car.Tank,
            _compoundInfos,
            own,
            gaps,
            forecast);

        var decision = car.Strategist.Decide(snapshot);
        car.Mode = decision.Pace;
        if (decision.Action != StrategyAction.PitNow || lap >= _in.TotalLaps)
        {
            return;
        }

        string? compound = null;
        if (decision.CompoundId is { } id && _pitRules.TyreChangeAllowed)
        {
            if (!car.Compounds.ContainsKey(id))
            {
                throw new InvalidOperationException($"The strategist of '{car.Entry.CarId}' chose the unknown compound '{id}'.");
            }

            compound = id;
        }

        var refuel = _pitRules.RefuellingAllowed ? Math.Max(0d, decision.RefuelKg) : 0d;
        var swap = decision.SwapDriver && own.CanSwapDriver && _pitRules.DriverChangeAllowed;
        if (compound is null && refuel <= 0d && !swap)
        {
            return;
        }

        car.Pit = new PendingPit(compound, refuel, swap);
    }

    // ---- Failures and lap times ---------------------------------------------------------------------------

    private void ApplyFailures(LapWork w, int lap)
    {
        var car = w.Car;
        var failure = car.Failure;
        if (failure.First is { } first && first.Lap == lap)
        {
            switch (first.Effect)
            {
                case FailureEffect.LosePower power:
                    car.PowerFactor *= 1d - (power.Percent / 100d);
                    break;
                case FailureEffect.PitForRepair repair:
                    car.RepairSeconds += repair.TimeCostSeconds;
                    break;
            }
        }

        if (failure.Retirement is { } retirement && retirement.Lap == lap)
        {
            w.Retire = new RetireInfo(lap, 0d, RetirementReason.Mechanical, retirement.Component, InjuryGrade.None, false, car.Driver.DriverId, retirement.Sudden);
        }
    }

    private double BurnThisLap(Car car, NeutralisationKind kind)
    {
        var mode = car.Mode switch
        {
            PaceMode.Push => PitConstants.PushBurnFactor,
            PaceMode.Save => 1d - TyreFuelConstants.FuelSavingBurnReduction,
            _ => 1d,
        };
        return car.Burn * mode * NeutralWearBurnFactor(kind);
    }

    private static double NeutralWearBurnFactor(NeutralisationKind kind) => kind switch
    {
        NeutralisationKind.SafetyCar => WeekendConstants.SafetyCarWearBurnFactor,
        NeutralisationKind.VirtualSafetyCar => WeekendConstants.VirtualSafetyCarWearBurnFactor,
        _ => 1d,
    };

    private void ComputeLap(LapWork w, int lap, double gapAhead)
    {
        var car = w.Car;
        var wetness = _weather.At(w.StartCum / 60d).TrackWetness;
        var compound = car.Tyre.Compound;
        var tyreLoss = car.Tyre.CurrentLapLossSeconds;
        double mismatch;
        if (compound.Kind == TyreCompoundKind.Wet)
        {
            var fit = Math.Min(1d, wetness / WeekendConstants.WetTyreFullWetness);
            tyreLoss *= 1d - fit;
            mismatch = 1d - fit;
        }
        else
        {
            mismatch = Math.Min(1d, wetness / WeekendConstants.DryTyreWetnessLimit);
        }

        // The noise draw is a child of the LapNoise stream by car and lap: nothing else moves it (INV-004).
        var rng = _lapNoiseStream.DeriveChild(string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"race:{car.Entry.CarId}:lap:{lap}"));
        var draw = Gaussian.FromUniforms(rng.NextDouble(), rng.NextDouble());

        var performance = car.Entry.Car with { Power = Math.Clamp(car.Entry.Car.Power * car.PowerFactor, 0d, 100d) };
        var inputs = new LapInputs
        {
            Track = _in.Track,
            Season = _in.Season,
            Limits = _limits,
            Car = performance,
            Driver = car.Driver.Pace,
            FuelMassKg = w.FuelStart,
            TyreWearFactor = Math.Max(0d, tyreLoss),
            WetnessLevel = wetness,
            TyreFitMismatch = mismatch,
            NoiseDraw = draw,
        };

        var margin = _pass.OvertakeMarginSeconds;
        var clean = LapTimeModel.Compute(inputs);
        var actual = clean;
        if (gapAhead < Math.Max(margin, PaceConstants.TrafficGapThresholdSeconds))
        {
            actual = LapTimeModel.Compute(inputs with
            {
                TrafficGapAheadSeconds = gapAhead,
                DirtyAirLevel = gapAhead < margin ? 1d - (gapAhead / margin) : 0d,
            });
        }

        var paceDelta = car.Mode switch
        {
            PaceMode.Push => -PitConstants.PushPaceGainSeconds,
            PaceMode.Save => FuelModel.FuelSavingPaceLossSeconds,
            _ => 0d,
        };
        var degrade = 0d;
        foreach (var failure in new[] { car.Failure.First, car.Failure.Retirement })
        {
            if (failure is not null)
            {
                degrade = Math.Max(degrade, failure.DegradedPaceLossOnLap(lap));
            }
        }

        w.Natural = (actual.TotalSeconds + paceDelta) * (1d + degrade);
        w.Clean = (clean.TotalSeconds + paceDelta) * (1d + degrade);
        w.Noise = clean.Seconds(LapLayer.Noise);
    }

    // ---- Incidents ----------------------------------------------------------------------------------------

    private double _lastIncidentTime;

    private Neutralisation? SampleIncidents(LapWork[] work, int lap)
    {
        Neutralisation? best = null;
        var byCar = work.ToDictionary(w => w.Car.Entry.CarId, StringComparer.Ordinal);
        for (var i = 0; i < work.Length; i++)
        {
            var w = work[i];
            if (w.Retire is not null)
            {
                continue;
            }

            var nearby = new List<string>();
            for (var j = i - 1; j >= 0 && w.StartCum - work[j].StartCum <= WeekendConstants.NearbySeconds; j--)
            {
                nearby.Add(work[j].Car.Entry.CarId);
            }

            for (var j = i + 1; j < work.Length && work[j].StartCum - w.StartCum <= WeekendConstants.NearbySeconds; j++)
            {
                nearby.Add(work[j].Car.Entry.CarId);
            }

            var wetness = _weather.At(w.StartCum / 60d).TrackWetness;
            var driver = w.Car.Driver;
            var incident = IncidentSampler.SampleLap(
                _incidentStream,
                new IncidentCar(w.Car.Entry.CarId, driver.Aggression, driver.Pace.Composure),
                new LapContext(lap, wetness, nearby),
                _incidentContext);
            if (incident is null)
            {
                continue;
            }

            var time = w.RetireTime;
            var involved = ImmutableArray.CreateBuilder<string>();
            foreach (var participant in incident.Participants)
            {
                var target = byCar[participant.CarId];
                involved.Add(target.Car.TapeId);
                ApplyOutcome(target, participant.Outcome, lap, time);
            }

            if (incident.WorstSeverity != OutcomeSeverity.None)
            {
                var severity = incident.WorstSeverity switch
                {
                    OutcomeSeverity.Minor => IncidentSeverity.Minor,
                    OutcomeSeverity.Retire => IncidentSeverity.Major,
                    _ => IncidentSeverity.Severe,
                };
                var ids = involved.ToImmutable();
                _raw.Add(new RawEvent(time, CatIncident, w.Car.Grid, 0, lap, (seq, ms) => new Incident(seq, lap, ms, ids, severity))
                {
                    DropAfterFlag = true,
                });
            }

            var plan = NeutralisationPlanner.Plan(incident, _in.Safety, _in.TotalLaps);
            if (plan.Kind is NeutralisationKind.SafetyCar or NeutralisationKind.VirtualSafetyCar or NeutralisationKind.RedFlag
                && (best is null || plan.Kind > best.Kind || (plan.Kind == best.Kind && plan.DurationLaps > best.DurationLaps)))
            {
                best = plan;
                _lastIncidentTime = time;
            }
        }

        return best;
    }

    // The incident happens at one instant (the instigator's mid-lap): every car it stops stops then, inside its own lap.
    private static void ApplyOutcome(LapWork w, IncidentOutcome outcome, int lap, double incidentTime)
    {
        if (w.Retire is not null)
        {
            return;
        }

        switch (outcome.Severity)
        {
            case OutcomeSeverity.Minor:
                w.Extra += WeekendConstants.MinorIncidentLossSeconds;
                break;
            case OutcomeSeverity.Retire or OutcomeSeverity.Injury or OutcomeSeverity.Fatal:
                w.Retire = new RetireInfo(
                    lap,
                    Math.Clamp(incidentTime, w.StartCum, w.StartCum + w.Natural),
                    RetirementReason.Accident,
                    null,
                    outcome.Injury,
                    outcome.IsFatal,
                    w.Car.Driver.DriverId);
                break;
        }
    }

    // ---- Pit stops and the order --------------------------------------------------------------------------

    private void PlanStop(LapWork w, int lap, NeutralisationKind kind)
    {
        var car = w.Car;
        var planned = car.Pit;
        var repair = car.RepairSeconds;
        if (planned is null && repair <= 0d)
        {
            return;
        }

        var factor = kind switch
        {
            NeutralisationKind.SafetyCar => WeekendConstants.SafetyCarPitLossFactor,
            NeutralisationKind.VirtualSafetyCar => WeekendConstants.VirtualSafetyCarPitLossFactor,
            _ => 1d,
        };
        w.StopSeconds = 0d;
        if (planned is not null)
        {
            // The stop is made at the end of the lap, after the lap's fuel is burnt: the refuel is what fits in the tank then.
            var fuelAfter = Math.Max(0d, car.Fuel - BurnThisLap(car, kind));
            var refuel = Math.Min(planned.RefuelKg, Math.Max(0d, car.Tank - fuelAfter));
            var request = new PitStopRequest(planned.CompoundId is not null, refuel, planned.Swap);
            var time = PitStopModel.Sample(_pitStream, car.Entry.CarId, car.Stops + 1, _pitRules, _laneLoss, request, car.Entry.Crew);
            w.StopSeconds = (time.TotalSeconds * factor) + repair;
            _pendingStops[w.Index] = new StopFacts(planned.CompoundId, refuel, planned.Swap, repair > 0d, time.Fault, lap);
        }
        else
        {
            w.StopSeconds = repair;
            _pendingStops[w.Index] = new StopFacts(null, 0d, false, true, PitStopFault.None, lap);
        }

        car.RepairSeconds = 0d;
    }

    private readonly Dictionary<int, StopFacts> _pendingStops = [];

    private sealed record StopFacts(string? CompoundId, double RefuelKg, bool Swap, bool IncludesRepair, PitStopFault Fault, int Lap);

    private void ResolvePasses(List<LapWork> survivors)
    {
        var follow = WeekendConstants.FollowGapSeconds;
        var placed = new List<LapWork>(survivors.Count);
        foreach (var w in survivors)
        {
            var end = w.StartCum + w.Natural + w.Extra;
            if (w.StopSeconds > 0d)
            {
                // A car in the pit lane is not held up, and does not hold anyone up.
                w.End = end + w.StopSeconds;
                continue;
            }

            var held = false;
            for (var pass = 0; pass <= placed.Count; pass++)
            {
                var again = false;
                for (var p = placed.Count - 1; p >= 0; p--)
                {
                    var other = placed[p];
                    if (end >= other.End + follow)
                    {
                        break;
                    }

                    var situation = new PassSituation(
                        w.StartCum - other.StartCum,
                        w.Clean,
                        other.End - other.StartCum,
                        w.Car.Driver.Overtaking,
                        other.Car.Driver.Defending);
                    if (_pass.Passes(situation))
                    {
                        continue;
                    }

                    end = other.End + follow;
                    held = true;
                    again = true;
                    break;
                }

                if (!again)
                {
                    break;
                }
            }

            w.End = end;
            w.Held = held;
            var at = placed.Count;
            while (at > 0 && placed[at - 1].End > end)
            {
                at--;
            }

            placed.Insert(at, w);
        }
    }

    private void ResolveSafetyCar(List<LapWork> survivors)
    {
        // The field bunches up behind the safety car: gaps close to a few tenths; the stops come on top.
        // A car that is far behind still has to complete a positive lap — bunching must not rewind the clock (#261).
        var scLap = _baseLap * WeekendConstants.SafetyCarLapFactor;
        var minLap = scLap * WeekendConstants.SafetyCarMinLapFactor;
        double previousOld = 0d;
        double previousNew = 0d;
        for (var i = 0; i < survivors.Count; i++)
        {
            var w = survivors[i];
            var bunched = i == 0
                ? w.StartCum + scLap
                : previousNew + Math.Min(Math.Max(0d, w.StartCum - previousOld), WeekendConstants.BunchedGapSeconds);
            previousOld = w.StartCum;
            var racingEnd = Math.Max(bunched, w.StartCum + minLap);
            previousNew = racingEnd;
            w.End = racingEnd + w.StopSeconds;
        }
    }

    // ---- State at the end of the lap ----------------------------------------------------------------------

    private void CommitLap(LapWork[] work, int lap, NeutralisationKind kind, bool neutralLap)
    {
        var wearBurn = NeutralWearBurnFactor(kind);
        foreach (var w in work)
        {
            var car = w.Car;
            if (w.Retire is { } retire)
            {
                car.Retired = retire;
                _raw.Add(new RawEvent(retire.TimeSeconds, CatRetire, car.Grid, 0, lap, (seq, ms) => new Retirement(seq, lap, ms, car.TapeId, retire.Reason))
                {
                    RetirementOfCar = car.Grid,
                });
                continue;
            }

            // Tyres and fuel of the lap just run.
            var weight = FuelModel.CarWeightFactor(w.FuelStart);
            var wearMode = car.Mode switch
            {
                PaceMode.Push => PitConstants.PushWearFactor,
                PaceMode.Save => PitConstants.SaveWearFactor,
                _ => 1d,
            };
            var multiplier = TyreWear.WearMultiplier(new TyreConditions(_abrasiveness, car.Driver.Smoothness / 100d, weight));
            car.Tyre = new TyreSet(car.Tyre.Compound, car.Tyre.AgeLaps + 1, car.Tyre.Wear + (multiplier * wearMode * wearBurn));
            car.Fuel = Math.Max(0d, car.Fuel - BurnThisLap(car, kind));
            car.StintLaps++;
            car.LapsDriven[w.DriverIndex]++;
            if (w.StopSeconds <= 0d && !neutralLap)
            {
                car.LastGreenLap = w.End - w.StartCum;
            }

            var lapSeconds = w.End - w.StartCum;
            car.LapSeconds.Add(lapSeconds);
            car.EndTimes.Add(w.End);
            car.Cum = w.End;

            if (_pendingStops.Remove(w.Index, out var stop))
            {
                ExecuteStop(w, stop);
            }
        }

        _pendingStops.Clear();
    }

    private void ExecuteStop(LapWork w, StopFacts stop)
    {
        var car = w.Car;
        var before = car.Tyre.Compound.Id;
        if (stop.CompoundId is { } id)
        {
            car.Tyre = TyreSet.New(car.Compounds[id]);
            car.Used.Add(id);
        }

        car.Fuel += stop.RefuelKg;
        if (stop.Swap)
        {
            car.DriverIndex = 1;
            car.StintLaps = 0;
            car.Swapped = true;
        }

        car.Stops++;
        var after = car.Tyre.Compound.Id;
        _pitStops.Add(new PitStopRecord(
            car.Entry.CarId,
            stop.Lap,
            w.StopSeconds,
            stop.CompoundId is not null,
            after,
            stop.RefuelKg,
            stop.Swap,
            stop.IncludesRepair && stop.CompoundId is null && !stop.Swap && stop.RefuelKg <= 0d,
            stop.Fault));

        var lap = stop.Lap;
        var endTime = w.End;
        var seconds = w.StopSeconds;
        var index = car.Grid;
        var tapeId = car.TapeId;
        _raw.Add(new RawEvent(endTime - seconds, CatPitIn, index, 0, lap, (seq, ms) => new PitStop(seq, lap, ms, tapeId, PitLanePhase.In, 0, before)));
        _raw.Add(new RawEvent(endTime, CatPitOut, index, 0, lap, (seq, ms) => new PitStop(seq, lap, ms, tapeId, PitLanePhase.Out, Ms(seconds), after)));
    }
}
