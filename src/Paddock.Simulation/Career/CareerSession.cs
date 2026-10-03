using System.Globalization;
using Paddock.Domain.Career;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Simulation.Career;

/// <summary>Counts and the world-state hash on the morning after a season closes (1 January).</summary>
public sealed record CareerYearSummary(int Year, int Alive, int Retired, int Pool, int Contracts, string StateHash);

/// <summary>
/// Optional knobs for <see cref="CareerSession"/>. Null lists use <see cref="CareerDayEstimates"/>.
/// </summary>
public sealed class CareerSessionOptions
{
    public int GeneratedIntakePerSeason { get; init; } = CareerDayEstimates.GeneratedIntakePerSeason;

    public INameSource? Names { get; init; }

    public INameBlocklist? Blocklist { get; init; }

    public IReadOnlyList<QualityBandWeight>? PoolQuality { get; init; }

    public IReadOnlyList<NationalityWeight>? Nationalities { get; init; }
}

/// <summary>
/// One career's world, day clock, talent pool, and retirement ledger.
/// <see cref="LiveDay"/> is the only way the date moves: it runs <see cref="WorldClock.AdvanceDay"/>
/// with the placeholder handlers. The pool and the retirement ledger sit beside <see cref="WorldState"/>
/// (the world has no pool field). They are not part of <see cref="WorldState.StateHash"/>.
/// </summary>
public sealed class CareerSession
{
    private readonly DayHandlerRegistry _registry;
    private readonly int _openedYear;
    private readonly int _intakePerSeason;
    private readonly INameSource _names;
    private readonly INameBlocklist _blocklist;
    private readonly IReadOnlyList<QualityBandWeight> _poolQuality;
    private readonly IReadOnlyList<NationalityWeight> _nationalities;
    private readonly Dictionary<int, List<PersonId>> _birthdays = new();
    private readonly Dictionary<GameDate, ContractId[]> _expiries = new();
    private readonly Dictionary<GameDate, ScheduledArrival[]> _arrivals = new();
    private readonly SortedSet<PersonId> _pool = new();
    private readonly SortedSet<PersonId> _retired = new();
    private readonly List<CareerYearSummary> _years = [];
    private WorldClockState _clock;

    public CareerSession(
        WorldState world,
        ulong masterSeed,
        IReadOnlyList<PersonId> talentPool,
        IReadOnlyList<ScheduledArrival> arrivals,
        CareerSessionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(talentPool);
        ArgumentNullException.ThrowIfNull(arrivals);
        options ??= new CareerSessionOptions();
        if (options.GeneratedIntakePerSeason < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.GeneratedIntakePerSeason,
                "Generated intake cannot be negative.");
        }

        World = world;
        _clock = new WorldClockState(world.CurrentDate, masterSeed);
        _openedYear = world.CurrentDate.Year;
        _intakePerSeason = options.GeneratedIntakePerSeason;
        _names = options.Names ?? new FixtureNameSource();
        _blocklist = options.Blocklist ?? EmptyNameBlocklist.Instance;
        _poolQuality = options.PoolQuality ?? CareerDayEstimates.PoolQualityWeights;
        _nationalities = options.Nationalities ?? CareerDayEstimates.IntakeNationalities;
        if (_intakePerSeason > 0 && (_poolQuality.Count == 0 || _nationalities.Count == 0))
        {
            throw new ArgumentException("Generated intake needs quality weights and nationalities.", nameof(options));
        }

        foreach (var person in world.Persons)
        {
            IndexBirthday(person);
        }

        foreach (var id in talentPool)
        {
            _ = world.GetPerson(id);
            if (!_pool.Add(id))
            {
                throw new ArgumentException("The talent pool lists '" + id.Value + "' twice.", nameof(talentPool));
            }
        }

        var groupedArrivals = new Dictionary<GameDate, List<ScheduledArrival>>();
        foreach (var arrival in arrivals)
        {
            ArgumentNullException.ThrowIfNull(arrival);
            if (arrival.On < world.CurrentDate)
            {
                throw new ArgumentException("An arrival is dated before the world.", nameof(arrivals));
            }

            if (!groupedArrivals.TryGetValue(arrival.On, out var list))
            {
                list = [];
                groupedArrivals.Add(arrival.On, list);
            }

            list.Add(arrival);
        }

        foreach (var pair in groupedArrivals)
        {
            pair.Value.Sort(static (left, right) => string.CompareOrdinal(ArrivalKey(left), ArrivalKey(right)));
            _arrivals.Add(pair.Key, pair.Value.ToArray());
        }

        var groupedContracts = new Dictionary<GameDate, List<ContractId>>();
        foreach (var contract in world.Contracts)
        {
            if (!groupedContracts.TryGetValue(contract.End, out var list))
            {
                list = [];
                groupedContracts.Add(contract.End, list);
            }

            list.Add(contract.Id);
        }

        foreach (var pair in groupedContracts)
        {
            pair.Value.Sort(static (left, right) => string.CompareOrdinal(left.Value, right.Value));
            _expiries.Add(pair.Key, pair.Value.ToArray());
        }

        _registry = new DayHandlerRegistry(
        [
            new IntakeHandler(this),
            new AgeingHandler(this),
            new ContractExpiryHandler(this),
            new SeasonRolloverHandler(),
        ]);
    }

    public WorldState World { get; private set; }

    public GameDate Date => _clock.Date;

    public WorldClockState Clock => _clock;

    public int ContractExpiries { get; private set; }

    public int Intakes { get; private set; }

    public IReadOnlyList<CareerYearSummary> Years => _years;

    public IReadOnlyList<PersonId> TalentPool => _pool.ToArray();

    public IReadOnlyList<PersonId> Retired => _retired.ToArray();

    /// <summary>Lives the current day, then moves the world and the clock to the next morning.</summary>
    public void LiveDay()
    {
        if (World.CurrentDate != _clock.Date)
        {
            throw new InvalidOperationException("The world date and the day clock have diverged.");
        }

        var lived = _clock.Date;
        var step = WorldClock.AdvanceDay(_clock, _registry);
        _clock = step.State;
        World = World.WithDate(_clock.Date);
        if (lived.IsSeasonEnd)
        {
            _years.Add(Summarize(lived.Year));
        }
    }

    private CareerYearSummary Summarize(int year)
    {
        var seasonEnd = GameDate.SeasonEnd(year);
        var alive = 0;
        foreach (var person in World.Persons)
        {
            if (person.BirthDate <= seasonEnd && !_retired.Contains(person.Id))
            {
                alive++;
            }
        }

        var contracts = 0;
        foreach (var contract in World.Contracts)
        {
            if (contract.IsActiveOn(seasonEnd))
            {
                contracts++;
            }
        }

        return new CareerYearSummary(year, alive, _retired.Count, _pool.Count, contracts, World.StateHash());
    }

    private void IndexBirthday(Person person)
    {
        var key = BirthdayKey(person.BirthDate.Month, person.BirthDate.Day);
        if (!_birthdays.TryGetValue(key, out var list))
        {
            list = [];
            _birthdays.Add(key, list);
        }

        list.Add(person.Id);
    }

    private void ForBirthdays(GameDate today, List<PersonId> buffer)
    {
        buffer.Clear();
        CollectBirthdays(today.Month, today.Day, buffer);
        if (today.Month == 2 && today.Day == 28 && !IsLeap(today.Year))
        {
            CollectBirthdays(2, 29, buffer);
        }

        buffer.Sort(static (left, right) => string.CompareOrdinal(left.Value, right.Value));
    }

    private void CollectBirthdays(int month, int day, List<PersonId> buffer)
    {
        if (_birthdays.TryGetValue(BirthdayKey(month, day), out var list))
        {
            buffer.AddRange(list);
        }
    }

    private static int BirthdayKey(int month, int day) => (month * 100) + day;

    private static bool IsLeap(int year) => year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);

    private static bool IsDriver(Person person)
    {
        foreach (var role in person.Roles)
        {
            if (role.IsDriver)
            {
                return true;
            }
        }

        return false;
    }

    private static string ArrivalKey(ScheduledArrival arrival) =>
        arrival.Spec.IsReal ? arrival.Spec.RealId! : arrival.Spec.FamilyName + "\u001f" + arrival.Spec.GivenName;

    private void Admit(PersonSpec spec, DayContext context, string? expectedGeneratedId)
    {
        var (next, id) = World.AddPerson(spec);
        if (expectedGeneratedId is not null && !string.Equals(id.Value, expectedGeneratedId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Generated person id " + expectedGeneratedId + " does not match the world id " + id.Value + ".");
        }

        World = next;
        if (!_pool.Add(id))
        {
            throw new InvalidOperationException("Person '" + id.Value + "' is already in the talent pool.");
        }

        IndexBirthday(World.GetPerson(id));
        Intakes++;
        context.Emit(CareerEventType.PoolEntered, new MarkerPayload(id.Value));
    }

    private sealed class IntakeHandler : IDayHandler
    {
        private readonly CareerSession _session;

        public IntakeHandler(CareerSession session) => _session = session;

        public int Order => 10;

        public void OnDay(DayContext context)
        {
            if (_session._arrivals.TryGetValue(context.Today, out var due))
            {
                foreach (var arrival in due)
                {
                    var realId = arrival.Spec.RealId;
                    if (realId is not null && _session.World.Ids.WasIssued(realId))
                    {
                        throw new InvalidOperationException("Scheduled person '" + realId + "' is already in the world.");
                    }

                    _session.Admit(arrival.Spec, context, null);
                }
            }

            if (context.Today.Year <= _session._openedYear
                || context.Today.Month != CareerDayEstimates.PoolEntryMonth
                || context.Today.Day != CareerDayEstimates.PoolEntryDay
                || _session._intakePerSeason == 0)
            {
                return;
            }

            var xoshiro = context.Stream(RngStreamName.People);
            var people = new RngStream(RngStreamName.People, xoshiro.State);
            var year = context.Today.Year;
            for (var index = 1; index <= _session._intakePerSeason; index++)
            {
                var band = PickBand(people, year, index);
                var generator = new DriverGenerator(new StableIdAllocator(_session.World.Ids.NextPerson), _session._names, _session._blocklist);
                var request = GenerationRequest.ForNew(band, _session._nationalities, year);
                var driver = generator.Generate(people, year, request);
                var spec = new PersonSpec(
                    driver.GivenName,
                    driver.FamilyName,
                    new GameDate(driver.BirthDate.Year, driver.BirthDate.Month, driver.BirthDate.Day),
                    driver.Nationality,
                    false,
                    null,
                    [PersonRole.Driver],
                    PersonTruth.FromDriver(driver.Attributes, driver.PotentialAttributes));
                _session.Admit(spec, context, driver.Id);
            }
        }

        private QualityBand PickBand(RngStream people, int year, int index)
        {
            var weights = _session._poolQuality;
            var total = 0;
            foreach (var weight in weights)
            {
                total += weight.Weight;
            }

            var tag = "intake-band:" + year.ToString(CultureInfo.InvariantCulture) + ":" + index.ToString(CultureInfo.InvariantCulture);
            var roll = people.DeriveChild(tag).NextInt(0, total);
            var cursor = 0;
            foreach (var weight in weights)
            {
                cursor += weight.Weight;
                if (roll < cursor)
                {
                    return weight.Band;
                }
            }

            throw new InvalidOperationException("Quality weights did not cover the roll.");
        }
    }

    private sealed class AgeingHandler : IDayHandler
    {
        private readonly CareerSession _session;
        private readonly List<PersonId> _buffer = [];

        public AgeingHandler(CareerSession session) => _session = session;

        public int Order => 20;

        public void OnDay(DayContext context)
        {
            _session.ForBirthdays(context.Today, _buffer);
            foreach (var id in _buffer)
            {
                if (_session._retired.Contains(id))
                {
                    continue;
                }

                var person = _session.World.GetPerson(id);
                var age = context.Today.Year - person.BirthDate.Year;
                var driver = IsDriver(person);
                if (!RetirementCurve.Retires(age, driver, () => context.Stream(RngStreamName.LifeEvents).NextDouble()))
                {
                    continue;
                }

                if (_session._retired.Add(id))
                {
                    _session._pool.Remove(id);
                    context.Emit(CareerEventType.Retired, new MarkerPayload(id.Value));
                }
            }
        }
    }

    private sealed class ContractExpiryHandler : IDayHandler
    {
        private readonly CareerSession _session;

        public ContractExpiryHandler(CareerSession session) => _session = session;

        public int Order => 30;

        public void OnDay(DayContext context)
        {
            if (!_session._expiries.TryGetValue(context.Today, out var due))
            {
                return;
            }

            foreach (var id in due)
            {
                context.Emit(CareerEventType.ContractExpired, new MarkerPayload(id.Value));
                _session.ContractExpiries++;
            }
        }
    }

    private sealed class SeasonRolloverHandler : IDayHandler
    {
        public int Order => 40;

        public void OnDay(DayContext context)
        {
            if (!context.Today.IsSeasonEnd)
            {
                return;
            }

            context.Emit(
                CareerEventType.SeasonClosed,
                new MarkerPayload(context.Today.Year.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
