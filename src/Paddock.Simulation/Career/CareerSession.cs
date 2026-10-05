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
public sealed record CareerYearSummary(
    int Year,
    int Alive,
    int Retired,
    int Pool,
    int Contracts,
    string StateHash,
    int Signed,
    int Renewed,
    int Expired);

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
/// Everything a <see cref="CareerSession"/> keeps besides the people-schedule inputs: the world, the day clock, the
/// opening year (it decides which seasons draw fillers), the tallies, and the per-season summaries. The talent pool is a
/// section of <see cref="World"/>, so it is not listed here.
/// <see cref="CareerSession.Resume"/> turns it back into a session that lives the same future as one that never stopped.
/// </summary>
public sealed record CareerSessionResume(
    WorldState World,
    WorldClockState Clock,
    int OpenedYear,
    int ContractExpiries,
    int Intakes,
    IReadOnlyList<CareerYearSummary> Years,
    int SeasonSigned = 0,
    int SeasonRenewed = 0,
    int SeasonExpired = 0);

/// <summary>
/// One career's world and day clock.
/// <see cref="LiveDay"/> is the only way the date moves: it runs <see cref="WorldClock.AdvanceDay"/>
/// with the placeholder handlers and the talent pool (T40). The talent pool is the world section
/// <see cref="TalentPoolSection"/>, so it is part of <see cref="WorldState.StateHash"/> and of the save. Retirement is part of the world (<see cref="Person.RetiredOn"/>):
/// a retiree keeps the record (TECH 6.2), their contracts end, and the date is in the hash and the save.
/// </summary>
public sealed class CareerSession
{
    private DayHandlerRegistry _registry;
    private bool _handlersAttached;
    private Action<IReadOnlyList<DomainEvent>>? _afterDay;
    private readonly int _openedYear;
    private readonly TalentPoolDayHandler _poolHandler;
    private readonly Dictionary<int, List<PersonId>> _birthdays = new();
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
        : this(
            world ?? throw new ArgumentNullException(nameof(world)),
            new WorldClockState(world.CurrentDate, masterSeed),
            world.CurrentDate.Year,
            talentPool,
            arrivals,
            options)
    {
    }

    private CareerSession(
        WorldState world,
        WorldClockState clock,
        int openedYear,
        IReadOnlyList<PersonId> talentPool,
        IReadOnlyList<ScheduledArrival> arrivals,
        CareerSessionOptions? options)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(talentPool);
        ArgumentNullException.ThrowIfNull(arrivals);
        options ??= new CareerSessionOptions();
        World = world;
        _clock = clock;
        _openedYear = openedYear;
        _lastSeasons = options.LastSeasons ?? new Dictionary<string, int>();

        // A person who is still active when the session starts retires no earlier than the end of the season it starts in.
        // For a new run that is the opening season, for a resumed one the season of the save date.
        var firstSeason = world.CurrentDate.Year;
        foreach (var person in world.Persons)
        {
            IndexBirthday(person);
            if (!person.IsRetired)
            {
                ScheduleLastSeason(person, firstSeason);
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

    /// <summary>The season the run opened in. Generated intake starts the season after it.</summary>
    public int OpenedYear => _openedYear;

    public GameDate Date => _clock.Date;

    public WorldClockState Clock => _clock;

    public int ContractExpiries { get; private set; }

    /// <summary>Contracts signed since the current season opened. Reset when the season summary is taken.</summary>
    public int SeasonSigned { get; private set; }

    /// <summary>Renewals of a contract the same organization already held, plus options exercised. Reset with <see cref="SeasonSigned"/>.</summary>
    public int SeasonRenewed { get; private set; }

    /// <summary>Contracts that reached their end date since the current season opened.</summary>
    public int SeasonExpired { get; private set; }

    /// <summary>People who entered the pool after the session opened: scheduled real drivers and fictional fillers.</summary>
    public int Intakes => _poolHandler.Entries;

    public IReadOnlyList<CareerYearSummary> Years => _years;

    public IReadOnlyList<PersonId> TalentPool => Pool.Members.Select(member => member.Id).ToArray();

    /// <summary>The talent pool as the world holds it.</summary>
    public TalentPoolSection Pool => World.Section<TalentPoolSection>(TalentPoolSection.SectionName) ?? TalentPoolSection.Empty;

    public IReadOnlyList<PersonId> Retired =>
        World.Persons.Where(person => person.IsRetired).Select(person => person.Id).ToArray();

    /// <summary>
    /// Continues a saved session. <paramref name="arrivals"/> are the scheduled arrivals that have not happened yet
    /// (dated on or after the saved date), <paramref name="options"/> the same inputs the original run used.
    /// Anything that disagrees with the saved state throws instead of starting a different future.
    /// </summary>
    public static CareerSession Resume(
        CareerSessionResume saved,
        IReadOnlyList<ScheduledArrival> arrivals,
        CareerSessionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(saved.World);
        ArgumentNullException.ThrowIfNull(saved.Years);
        ArgumentNullException.ThrowIfNull(arrivals);
        if (saved.World.CurrentDate != saved.Clock.Date)
        {
            throw new ArgumentException("The saved world date and the saved day clock disagree.", nameof(saved));
        }

        if (saved.OpenedYear > saved.Clock.Date.Year)
        {
            throw new ArgumentException("The run cannot have opened after the saved date.", nameof(saved));
        }

        if (saved.ContractExpiries < 0 || saved.Intakes < 0 || saved.SeasonSigned < 0 || saved.SeasonRenewed < 0 || saved.SeasonExpired < 0)
        {
            throw new ArgumentException("The saved tallies cannot be negative.", nameof(saved));
        }

        if (saved.World.Section<TalentPoolSection>(TalentPoolSection.SectionName) is not { } savedPool)
        {
            throw new ArgumentException("The saved world has no talent pool section.", nameof(saved));
        }

        foreach (var member in savedPool.Members)
        {
            if (saved.World.GetPerson(member.Id).IsRetired)
            {
                throw new ArgumentException("The saved talent pool lists the retired person '" + member.Id.Value + "'.", nameof(saved));
            }
        }

        var previous = int.MinValue;
        foreach (var year in saved.Years)
        {
            ArgumentNullException.ThrowIfNull(year);
            if (year.Year <= previous || year.Year >= saved.Clock.Date.Year)
            {
                throw new ArgumentException("The saved season summaries are out of order or lie in the future.", nameof(saved));
            }

            previous = year.Year;
        }

        foreach (var arrival in arrivals)
        {
            ArgumentNullException.ThrowIfNull(arrival);
            if (arrival.Spec.RealId is { } realId && saved.World.Ids.WasIssued(realId))
            {
                throw new ArgumentException("Scheduled person '" + realId + "' is already in the saved world.", nameof(arrivals));
            }
        }

        var session = new CareerSession(saved.World, saved.Clock, saved.OpenedYear, [], arrivals, options)
        {
            ContractExpiries = saved.ContractExpiries,
            SeasonSigned = saved.SeasonSigned,
            SeasonRenewed = saved.SeasonRenewed,
            SeasonExpired = saved.SeasonExpired,
        };
        session._poolHandler.Entries = saved.Intakes;
        session._years.AddRange(saved.Years);
        return session;
    }

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

    /// <summary>True when a championship session of <paramref name="season"/> is already on the clock.</summary>
    public bool HasChampionship(int season)
    {
        foreach (var scheduled in _clock.Queue.Events)
        {
            if (scheduled.Payload is RaceSessionPayload payload && payload.Season == season)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Queues one season's weekends. Used when a career opens, before any day is lived. A day handler that schedules the next
    /// season uses <see cref="DayContext.Schedule"/> instead, because a clock written during a day is replaced when the day ends.
    /// </summary>
    public void QueueChampionship(int season, IReadOnlyList<TrackLayout> layouts, IReadOnlyList<RaceAssignment> assignments)
    {
        _clock = SeasonCalendar.Schedule(_clock, season, layouts, assignments);
    }

    /// <summary>Queues sessions that are still ahead of the clock. Sessions already in the past are left out.</summary>
    public void QueuePlanned(IReadOnlyList<SeasonCalendar.PlannedSession> sessions)
    {
        _clock = SeasonCalendar.Enqueue(_clock, sessions);
    }

    /// <summary>
    /// Retires a person because a race ended the career (fatal, or a career-ending injury). Same world effect as an age
    /// retirement: the person stays, the contracts end, the pool drops them, and the day emits <see cref="CareerEventType.Retired"/>.
    /// A person who has already retired is left as they are.
    /// </summary>
    public void RetireForRace(PersonId id, GameDate today, DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (today != _clock.Date)
        {
            throw new ArgumentException("A race retirement is dated on the day being lived.", nameof(today));
        }

        if (World.GetPerson(id).IsRetired)
        {
            return;
        }

        Retire(id, today, context);
    }

    /// <summary>
    /// Adds day handlers once, before the first day is lived. The career host uses this for the contract handlers, which live
    /// in the application layer and so cannot be constructed here.
    /// </summary>
    public void AttachHandlers(IReadOnlyList<IDayHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        if (_handlersAttached)
        {
            throw new InvalidOperationException("Day handlers are already attached.");
        }

        _handlersAttached = true;
        if (handlers.Count == 0)
        {
            return;
        }

        var combined = new List<IDayHandler>(_registry.Handlers.Count + handlers.Count);
        combined.AddRange(_registry.Handlers);
        combined.AddRange(handlers);
        _registry = new DayHandlerRegistry(combined);
    }

    /// <summary>
    /// The day handlers of the session in the order they run each day (ascending <see cref="IDayHandler.Order"/>, then the order
    /// they were registered). The built-in ones and the ones the career host attached are in one list, so a test can read the day.
    /// </summary>
    public IReadOnlyList<IDayHandler> DayHandlers => _registry.Handlers;

    /// <summary>
    /// Sets what the host does at the end of <see cref="LiveDay"/>: after the day's handlers ran and the world moved to the next
    /// morning, and before the season summary is taken. It receives the events the day emitted. The host applies the outcomes
    /// of those events here (objectives, sponsors, board) and puts its books into the world, so the hash a summary or a save
    /// carries already includes them. Once only.
    /// </summary>
    public void AttachAfterDay(Action<IReadOnlyList<DomainEvent>> afterDay)
    {
        ArgumentNullException.ThrowIfNull(afterDay);
        if (_afterDay is not null)
        {
            throw new InvalidOperationException("An after-day hook is already attached.");
        }

        _afterDay = afterDay;
    }

    /// <summary>
    /// The named stream for the season being lived, continued from the clock when a previous day already drew from it.
    /// The caller draws, then <see cref="KeepStream"/> writes the generator back so the day tick continues it.
    /// </summary>
    public Xoshiro256StarStar BorrowStream(string streamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        var slot = new RngStreamSlot(streamName, _clock.Date.Year);
        return _clock.RngStates.TryGetValue(slot, out var saved)
            ? new Xoshiro256StarStar(saved)
            : RngStreams.Derive(_clock.MasterSeed, streamName, _clock.Date.Year);
    }

    /// <summary>Stores a stream <see cref="BorrowStream"/> handed out, after the caller has drawn from it.</summary>
    public void KeepStream(string streamName, Xoshiro256StarStar generator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentNullException.ThrowIfNull(generator);
        var slot = new RngStreamSlot(streamName, _clock.Date.Year);
        var copy = new Dictionary<RngStreamSlot, RngState>(_clock.RngStates)
        {
            [slot] = generator.State,
        };
        _clock = _clock with { RngStates = copy };
    }

    /// <summary>Lives the current day, then moves the world and the clock to the next morning.</summary>
    public void LiveDay()
    {
        if (World.CurrentDate != _clock.Date)
        {
            throw new InvalidOperationException("The world date and the day clock have diverged.");
        }

        var before = World.Contracts
            .Select(contract => (contract.Id.Value, contract.PersonId.Value, contract.OrganizationId.Value, contract.End))
            .ToArray();
        var expiries = ContractExpiries;
        var lived = _clock.Date;
        var step = WorldClock.AdvanceDay(_clock, _registry);
        foreach (var emitted in step.Events)
        {
            // ContractLifecycleHandler (order 710) ends an exit-clause contract after ContractExpiryHandler (order 30).
            // The type id is ContractEventTypes.ExitExercised; Simulation does not reference Application.
            if (emitted.TypeId == "contract.exitExercised")
            {
                ContractExpiries++;
            }
        }

        _clock = step.State;
        World = World.WithDate(_clock.Date);
        _afterDay?.Invoke(step.Events);
        AccountContracts(before, expiries);
        if (lived.IsSeasonEnd)
        {
            _years.Add(Summarize(lived.Year));
            SeasonSigned = 0;
            SeasonRenewed = 0;
            SeasonExpired = 0;
        }
    }

    private void AccountContracts(
        (string Id, string Person, string Organization, GameDate End)[] before,
        int expiriesBefore)
    {
        var previous = new Dictionary<string, GameDate>(before.Length, StringComparer.Ordinal);
        foreach (var contract in before)
        {
            previous[contract.Id] = contract.End;
        }

        foreach (var contract in World.Contracts)
        {
            if (!previous.TryGetValue(contract.Id.Value, out var end))
            {
                SeasonSigned++;
                foreach (var earlier in before)
                {
                    if (earlier.Person == contract.PersonId.Value
                        && earlier.Organization == contract.OrganizationId.Value
                        && earlier.End < contract.Start)
                    {
                        SeasonRenewed++;
                        break;
                    }
                }
            }
            else if (contract.End > end)
            {
                SeasonRenewed++;
            }
        }

        SeasonExpired += ContractExpiries - expiriesBefore;
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

        return new CareerYearSummary(year, alive, retired, Pool.Count, contracts, World.StateHash(), SeasonSigned, SeasonRenewed, SeasonExpired);
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
            // The end dates are read from the world, not from a list taken when the session opened. Renewals sign
            // contracts after that, and a resumed session would otherwise count those later contracts while the run
            // that never stopped would not.
            var due = new List<ContractId>();
            foreach (var contract in _session.World.Contracts)
            {
                if (contract.End == context.Today)
                {
                    due.Add(contract.Id);
                }
            }

            due.Sort(static (left, right) => string.CompareOrdinal(left.Value, right.Value));
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
