using System.Collections.Immutable;
using System.Globalization;
using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Domain.World;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Pace;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Qualifying;
using Paddock.Simulation.Racing.Reliability;
using Paddock.Simulation.Racing.Tyres;
using Paddock.Simulation.Racing.Weather;

namespace Paddock.Simulation.Racing.Weekend;

/// <summary>
/// The state and the setup of one weekend run. The lap loop is in <c>WeekendRun.Race.cs</c>, the tape in
/// <c>WeekendRun.Tape.cs</c>. Internal: the public face is <see cref="RaceWeekend"/>.
/// </summary>
internal sealed partial class WeekendRun
{
    private readonly RaceWeekendInput _in;
    private readonly ITraceSink _sink;
    private readonly StrategistFactory? _factory;

    private readonly PitRules _pitRules;
    private readonly PointsRules _pointsRules;
    private readonly FuelModel _fuel;
    private readonly TyreCompoundCatalog _catalog;
    private readonly EraPerformanceLimits _limits;
    private readonly IPassModel _pass;
    private readonly double _baseLap;
    private readonly double _laneLoss;
    private readonly double _abrasiveness;
    private readonly TruthWeather _weather;
    private readonly RngStream _lapNoiseStream;
    private readonly RngStream _incidentStream;
    private readonly RngStream _pitStream;
    private readonly ImmutableArray<CompoundInfo> _compoundInfos;
    private readonly Dictionary<(int Minute, double Quality), WeatherForecast> _forecasts = [];
    private readonly IncidentRaceContext _incidentContext;

    private List<Car> _cars = [];
    private RngStream? _failureStream;
    private int _endLap;

    public WeekendRun(RaceWeekendInput input, ITraceSink sink, StrategistFactory? factory)
    {
        _in = input;
        _sink = sink;
        _factory = factory;
        Validate(input);

        _pitRules = PitRules.For(input.Rules);
        _pointsRules = PointsRules.For(input.Rules);
        _fuel = new FuelModel(_ => FuelRegime.FromRules(input.Rules));
        _catalog = input.Compounds ?? TyreCompoundCatalog.Default;
        _limits = EraPerformanceLimits.EstimateFor(input.Season);
        _pass = input.PassModel ?? MarginPassModel.Default;
        _baseLap = LapTimeModel.TrackBaseSeconds(input.Track, input.Season);
        _laneLoss = _pitRules.PitLaneTimeLossSeconds();

        var climate = input.Climate.For(input.Track.CircuitId, input.Month);
        var minutes = (int)Math.Ceiling(input.TotalLaps * _baseLap * WeekendConstants.WeatherDurationFactor / 60d)
            + WeekendConstants.WeatherDurationExtraMinutes;
        _weather = RaceWeather.Generate(input.MasterSeed, input.Season, input.Round, climate, minutes);

        _lapNoiseStream = RngStream.Derive(input.MasterSeed, RngStreamName.LapNoise, input.Season, input.Round);
        _incidentStream = IncidentSampler.DeriveRaceStream(input.MasterSeed, input.Season, input.Round);
        _pitStream = PitStopModel.DeriveRaceStream(input.MasterSeed, input.Season, input.Round);
        _incidentContext = new IncidentRaceContext(input.Safety, input.TrackDanger, input.Fatality == FatalityLevel.On);

        // The hidden truth of the track: how hard it is on tyres. Hashed from the LapNoise stream; no entry changes it.
        var abrasiveDraw = _lapNoiseStream.DeriveChild("track-abrasiveness").NextDouble();
        _abrasiveness = WeekendConstants.AbrasivenessMin
            + ((WeekendConstants.AbrasivenessMax - WeekendConstants.AbrasivenessMin) * abrasiveDraw);

        _compoundInfos =
        [
            .. _catalog.RaceCompounds(input.Season).Select(c => new CompoundInfo(c.Id, false)),
            new CompoundInfo(_catalog.WetCompound(input.Season).Id, true),
        ];
    }

    public RaceWeekendResult Execute()
    {
        var qualifying = RunQualifying();
        BuildCars(qualifying);
        RunRace();
        return Finish(qualifying);
    }

    private static void Validate(RaceWeekendInput input)
    {
        ArgumentNullException.ThrowIfNull(input.Track);
        ArgumentNullException.ThrowIfNull(input.Rules);
        ArgumentNullException.ThrowIfNull(input.Safety);
        ArgumentNullException.ThrowIfNull(input.Climate);
        if (input.TotalLaps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "A race has at least one lap.");
        }

        if (input.Month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "The month must be 1..12.");
        }

        if (input.Entries.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A weekend needs at least one entry.", nameof(input));
        }

        if (input.StrategistCadenceLaps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "The strategist cadence must be at least 1 lap.");
        }

        var cars = new HashSet<string>(StringComparer.Ordinal);
        var people = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in input.Entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.CarId);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.ConstructorId);
            if (!cars.Add(entry.CarId))
            {
                throw new ArgumentException($"Car id '{entry.CarId}' is used twice.", nameof(input));
            }

            if (entry.Drivers.IsDefaultOrEmpty || entry.Drivers.Length > 2)
            {
                throw new ArgumentException($"Car '{entry.CarId}' needs one or two drivers.", nameof(input));
            }

            foreach (var driver in entry.Drivers)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(driver.DriverId);
                if (!people.Add(driver.DriverId))
                {
                    throw new ArgumentException($"Driver '{driver.DriverId}' is entered twice (a person drives one car).", nameof(input));
                }

                foreach (var (rating, name) in new[]
                {
                    (driver.Aggression, nameof(DriverEntry.Aggression)),
                    (driver.Overtaking, nameof(DriverEntry.Overtaking)),
                    (driver.Defending, nameof(DriverEntry.Defending)),
                    (driver.Smoothness, nameof(DriverEntry.Smoothness)),
                })
                {
                    if (!(rating >= 0d && rating <= 100d))
                    {
                        throw new ArgumentOutOfRangeException(nameof(input), $"{name} of '{driver.DriverId}' must be in 0..100.");
                    }
                }
            }

            if (entry.StrategistSkill is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(input), $"Strategist skill of '{entry.CarId}' must be in 0..100.");
            }

            if (!(entry.ForecastQuality >= 0d && entry.ForecastQuality <= 1d))
            {
                throw new ArgumentOutOfRangeException(nameof(input), $"Forecast quality of '{entry.CarId}' must be in 0..1.");
            }
        }
    }

    private QualifyingResult RunQualifying()
    {
        var rules = _in.Rules;
        var qualifyingRules = QualifyingRules.FromCatalog(
            rules.Value("qualifying_format"),
            rules.Value("qualifying_time_cutoff"),
            rules.Value("pre_qualifying_session"),
            rules.Value("maximum_grid"),
            _in.Entries.Length);

        // Pace relative to the track base: the lap model with no noise, low fuel and no tyre wear (the weather of a
        // qualifying session is not generated, so it is run dry).
        var entrants = new List<QualifyingEntrant>(_in.Entries.Length);
        foreach (var entry in _in.Entries)
        {
            var inputs = new LapInputs
            {
                Track = _in.Track,
                Season = _in.Season,
                Limits = _limits,
                Car = entry.Car,
                Driver = entry.Primary.Pace,
                FuelMassKg = WeekendConstants.QualifyingFuelKg,
            };
            var breakdown = LapTimeModel.Compute(inputs);
            entrants.Add(new QualifyingEntrant(
                entry.Primary.DriverId,
                entry.ConstructorId,
                breakdown.TotalSeconds - breakdown.Seconds(LapLayer.TrackBase),
                entry.Primary.Pace.Consistency / 100d,
                InPreQualifying: entry.InPreQualifying));
        }

        var context = new QualifyingContext(_in.MasterSeed, _in.Season, _in.Round, _baseLap);
        return QualifyingSimulator.Run(context, entrants, qualifyingRules);
    }

    private void BuildCars(QualifyingResult qualifying)
    {
        var byDriver = _in.Entries.ToDictionary(e => e.Primary.DriverId, StringComparer.Ordinal);
        var raceCompounds = _catalog.RaceCompounds(_in.Season);
        var wet = _catalog.WetCompound(_in.Season);
        var startCompoundIndex = raceCompounds.Length / 2;
        var plannedFuelStops = _fuel.RefuellingAllowed(_in.Season) && _in.Season >= TyreFuelConstants.FastRigFirstSeason
            ? WeekendConstants.PlannedFuelStopsWithFastRig
            : 0;
        var failureStream = FailureSampler.DeriveRaceStream(_in.MasterSeed, _in.Season, _in.Round);
        _failureStream = failureStream;
        var aiStream = RuleBasedStrategist.DeriveRaceStream(_in.MasterSeed, _in.Season, _in.Round);
        var options = _in.StrategistOptions
            ?? new StrategistOptions(WeekendConstants.DefaultStrategistMaxStops, WeekendConstants.DefaultStrategistStopShiftVariants);
        var heat = HeatStress();

        _cars = [];
        var grid = 0;
        foreach (var slot in qualifying.Grid)
        {
            grid++;
            var entry = byDriver[slot.DriverId];
            var compounds = raceCompounds.Append(wet).ToDictionary(
                c => c.Id,
                c => entry.TyreSupplier.Apply(c, entry.PartnerTuned),
                StringComparer.Ordinal);
            var startCompound = compounds[raceCompounds[startCompoundIndex].Id];
            var burn = _fuel.BurnPerLapKg(_in.Season, entry.Engine, _in.Track.LengthKm, _in.TotalLaps);
            var tank = _fuel.StartLoadKg(_in.Season, entry.Engine, _in.Track.LengthKm, _in.TotalLaps);
            var start = _fuel.StartLoadKg(_in.Season, entry.Engine, _in.Track.LengthKm, _in.TotalLaps, plannedFuelStops);
            var failureInputs = new FailureInputs(_in.Season, entry.Primary.Smoothness / 100d, WeekendConstants.FailurePaceStress, heat);
            var failure = FailureSampler.Sample(failureStream, entry.CarId, _in.TotalLaps, entry.Components, failureInputs);

            // The planner can hand over a fuel load a rounding error below zero when a plan burns exactly what is on board.
            var calculators = new StrategyCalculators(
                (id, age, fuel) => TyreWear.LapLoss(
                    compounds[id],
                    age,
                    new TyreConditions(0.5, entry.Primary.Smoothness / 100d, FuelModel.CarWeightFactor(Math.Max(0d, fuel)))),
                fuel => FuelModel.MassPenaltySeconds(Math.Max(0d, fuel)));
            var tracing = _sink.IsEnabled
                ? new StrategistTracing(_sink, new WeekendKey(_in.Season, _in.Round), "strategist:" + entry.CarId)
                : null;
            var request = new StrategistRequest(entry, calculators, aiStream, options, tracing);
            var strategist = _factory is null
                ? new RuleBasedStrategist(entry.StrategistSkill, calculators, aiStream, options, tracing)
                : _factory(request);

            _cars.Add(new Car(entry, grid, compounds, strategist)
            {
                Tyre = TyreSet.New(startCompound),
                Used = [startCompound.Id],
                Fuel = start,
                Burn = burn,
                Tank = tank,
                Failure = failure,
                FailureInputs = failureInputs,
            });
        }

        if (_cars.Count == 0)
        {
            throw new InvalidOperationException("No car qualified for the race.");
        }
    }

    // The failure model's heat stress (0..1) from the true mean air temperature of the race: truth used only to sample failures.
    private double HeatStress()
    {
        var mean = _weather.Samples.Average(s => s.AirTempC);
        return Math.Clamp(
            (mean - WeekendConstants.HeatStressMinAirC) / (WeekendConstants.HeatStressMaxAirC - WeekendConstants.HeatStressMinAirC),
            0d,
            1d);
    }

    private static long Ms(double seconds) => (long)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero);

    private sealed record PendingPit(string? CompoundId, double RefuelKg, bool Swap);

    private sealed record RetireInfo(
        int Lap,
        double TimeSeconds,
        RetirementReason Reason,
        MechanicalComponent? Component,
        InjuryGrade Injury,
        bool Fatal,
        string DriverId,
        bool Sudden = false);

    private sealed class Car(RaceEntry entry, int grid, Dictionary<string, TyreCompound> compounds, IRaceStrategist strategist)
    {
        public RaceEntry Entry { get; } = entry;

        public int Grid { get; } = grid;

        public Dictionary<string, TyreCompound> Compounds { get; } = compounds;

        public IRaceStrategist Strategist { get; } = strategist;

        public required TyreSet Tyre { get; set; }

        public required List<string> Used { get; init; }

        public required double Fuel { get; set; }

        public required double Burn { get; init; }

        public required double Tank { get; init; }

        public required FailureSample Failure { get; set; }

        /// <summary>What the failures were sampled with, so an engine mode can sample them again with the same draws (#286).</summary>
        public required FailureInputs FailureInputs { get; init; }

        /// <summary>The engine mode the pit wall set (#286), and the lap each mode started on.</summary>
        public EngineMode Engine { get; set; } = EngineMode.Standard;

        public List<(int FromLap, EngineMode Mode)> EngineLaps { get; } = [];

        /// <summary>The team order (#286): let the team-mate by when it is right behind.</summary>
        public bool LetBy { get; set; }

        public PaceMode Mode { get; set; } = PaceMode.Standard;

        /// <summary>The pace the pit wall ordered (#286); null while the strategist decides.</summary>
        public PaceMode? ManualPace { get; set; }

        public int DriverIndex { get; set; }

        public int StintLaps { get; set; }

        public bool Swapped { get; set; }

        public int Stops { get; set; }

        public double Cum { get; set; }

        public double PowerFactor { get; set; } = 1d;

        public double? LastGreenLap { get; set; }

        public PendingPit? Pit { get; set; }

        public double RepairSeconds { get; set; }

        public RetireInfo? Retired { get; set; }

        public bool RetiredAfterFlagStands { get; set; } = true;

        public List<double> EndTimes { get; } = [];

        public List<double> LapSeconds { get; } = [];

        public List<int> Ranks { get; } = [];

        public int[] LapsDriven { get; } = new int[2];

        public DriverEntry Driver => Entry.Drivers[DriverIndex];

        public string TapeId => Entry.Drivers[0].DriverId;
    }
}
