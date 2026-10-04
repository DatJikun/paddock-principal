using System.Globalization;
using Paddock.Domain.Career;
using Paddock.Domain.People;
using Paddock.Domain.Pool;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Pool;
using Paddock.Simulation.Time;

namespace Paddock.Simulation.Career;

/// <summary>Counts and the world-state hash on the morning after a season closes (1 January).</summary>
public sealed record CareerYearSummary(int Year, int Alive, int Retired, int Pool, int Contracts, string StateHash);

/// <summary>
/// Optional knobs for <see cref="CareerSession"/>. The pool settings default to the ESTIMATES in <see cref="PoolEstimates"/>.
/// </summary>
public sealed class CareerSessionOptions
{
    public TalentPoolOptions Pool { get; init; } = new();

    /// <summary>
    /// The last season each real person was active (T12), by person id. Such a person retires on 31 December of that
    /// season (or of the season they join the world, if later) and does not use the age curve. Null or missing means
    /// the age curve decides.
    /// </summary>
    public IReadOnlyDictionary<string, int>? LastSeasons { get; init; }
}

/// <summary>
/// One career's world and day clock.
/// <see cref="LiveDay"/> is the only way the date moves: it runs <see cref="WorldClock.AdvanceDay"/>
/// with the placeholder handlers and the talent pool (T40). The talent pool is the world section
/// <see cref="TalentPoolSection"/>, so it is part of <see cref="WorldState.StateHash"/>. Retirement is part of the world (<see cref="Person.RetiredOn"/>):
/// a retiree keeps the record (TECH 6.2), their contracts end, and the date is in the hash and the save.
/// </summary>
public sealed class CareerSession
{
    private readonly DayHandlerRegistry _registry;
    private readonly int _openedYear;
    private readonly TalentPoolDayHandler _poolHandler;
    private readonly Dictionary<int, List<PersonId>> _birthdays = new();
    private readonly Dictionary<GameDate, ContractId[]> _expiries = new();
    private readonly Dictionary<GameDate, ScheduledArrival[]> _arrivals = new();
    private readonly IReadOnlyDictionary<string, int> _lastSeasons;
    private readonly Dictionary<GameDate, List<PersonId>> _lastSeasonRetirements = new();
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
        World = world;
        _clock = new WorldClockState(world.CurrentDate, masterSeed);
        _openedYear = world.CurrentDate.Year;
        _lastSeasons = options.LastSeasons ?? new Dictionary<string, int>();

        foreach (var person in world.Persons)
        {
            IndexBirthday(person);
            if (!person.IsRetired)
            {
                ScheduleLastSeason(person, _openedYear);
            }
        }

        var pool = world.Section<TalentPoolSection>(TalentPoolSection.SectionName) ?? TalentPoolSection.Empty;
        var starters = new List<PersonId>();
        foreach (var id in talentPool)
        {
            _ = world.GetPerson(id);
            if (pool.Contains(id) || starters.Contains(id))
            {
                throw new ArgumentException("The talent pool lists '" + id.Value + "' twice.", nameof(talentPool));
            }

            starters.Add(id);
        }

        World = world.WithSection(starters.Count == 0 ? pool : pool.EnterAll(starters, world.CurrentDate));

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

        _poolHandler = new TalentPoolDayHandler(
            () => World,
            next => World = next,
            _arrivals,
            AdmitToWorld,
            options.Pool,
            _openedYear);
        _registry = new DayHandlerRegistry(
        [
            _poolHandler,
            new AgeingHandler(this),
            new LastSeasonHandler(this),
            new ContractExpiryHandler(this),
            new SeasonRolloverHandler(),
        ]);
    }

    public WorldState World { get; private set; }

    public GameDate Date => _clock.Date;

    public WorldClockState Clock => _clock;

    public int ContractExpiries { get; private set; }

    /// <summary>People who entered the pool after the session opened: scheduled real drivers and fictional fillers.</summary>
    public int Intakes => _poolHandler.Entries;

    public IReadOnlyList<CareerYearSummary> Years => _years;

    public IReadOnlyList<PersonId> TalentPool => Pool.Members.Select(member => member.Id).ToArray();

    /// <summary>The talent pool as the world holds it.</summary>
    public TalentPoolSection Pool => World.Section<TalentPoolSection>(TalentPoolSection.SectionName) ?? TalentPoolSection.Empty;

    public IReadOnlyList<PersonId> Retired =>
        World.Persons.Where(person => person.IsRetired).Select(person => person.Id).ToArray();

    /// <summary>
    /// Stores the world a command produced. The command layer calls this after a command has changed the world (INV-001). The date
    /// must be the session's own, because only <see cref="LiveDay"/> moves it.
    /// </summary>
    public void StoreWorld(WorldState next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (next.CurrentDate != _clock.Date)
        {
            throw new InvalidOperationException("A command cannot change the date of the world.");
        }

        World = next;
    }

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
        var retired = 0;
        foreach (var person in World.Persons)
        {
            if (person.IsRetired)
            {
                retired++;
            }
            else if (person.BirthDate <= seasonEnd)
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

        return new CareerYearSummary(year, alive, retired, Pool.Count, contracts, World.StateHash());
    }

    /// <summary>
    /// A real person with a known last season leaves on 31 December of it. A last season before the person joined the
    /// world, or before the run opened, means they leave at the end of that first season.
    /// </summary>
    private void ScheduleLastSeason(Person person, int firstSeason)
    {
        if (!person.IsReal || !_lastSeasons.TryGetValue(person.Id.Value, out var last))
        {
            return;
        }

        var on = GameDate.SeasonEnd(Math.Max(last, firstSeason));
        if (!_lastSeasonRetirements.TryGetValue(on, out var list))
        {
            list = [];
            _lastSeasonRetirements.Add(on, list);
        }

        list.Add(person.Id);
    }

    private bool GovernedByLastSeason(Person person) => person.IsReal && _lastSeasons.ContainsKey(person.Id.Value);

    private void Retire(PersonId id, GameDate today, DayContext context)
    {
        World = World.RetirePerson(id, today);
        LeavePool(id);
        context.Emit(CareerEventType.Retired, new MarkerPayload(id.Value));
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

    /// <summary>The pool handler has put a person into the world: the session indexes birthdays and last seasons.</summary>
    private void AdmitToWorld(Person person, GameDate today)
    {
        IndexBirthday(person);
        ScheduleLastSeason(person, today.Year);
    }

    private void LeavePool(PersonId id)
    {
        var pool = Pool;
        if (pool.Contains(id))
        {
            World = World.WithSection(pool.Leave(id));
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
                var person = _session.World.GetPerson(id);
                if (person.IsRetired || _session.GovernedByLastSeason(person))
                {
                    continue;
                }

                var age = context.Today.Year - person.BirthDate.Year;
                var driver = IsDriver(person);
                if (!RetirementCurve.Retires(age, driver, () => context.Stream(RngStreamName.LifeEvents).NextDouble()))
                {
                    continue;
                }

                _session.Retire(id, context.Today, context);
            }
        }
    }

    private sealed class LastSeasonHandler : IDayHandler
    {
        private readonly CareerSession _session;

        public LastSeasonHandler(CareerSession session) => _session = session;

        public int Order => 25;

        public void OnDay(DayContext context)
        {
            if (!_session._lastSeasonRetirements.TryGetValue(context.Today, out var due))
            {
                return;
            }

            due.Sort(static (left, right) => string.CompareOrdinal(left.Value, right.Value));
            foreach (var id in due)
            {
                if (!_session.World.GetPerson(id).IsRetired)
                {
                    _session.Retire(id, context.Today, context);
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

            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (var contract in _session.World.Contracts)
            {
                present.Add(contract.Id.Value);
            }

            foreach (var id in due)
            {
                if (!present.Contains(id.Value))
                {
                    continue;
                }

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
