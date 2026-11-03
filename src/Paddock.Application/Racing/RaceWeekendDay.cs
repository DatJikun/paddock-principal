using System.Collections.Immutable;
using System.Globalization;
using Paddock.Application.Career;
using Paddock.Application.Cars;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Supply;
using Paddock.Domain.Cars;
using Paddock.Domain.Career;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.People;
using Paddock.Domain.Racing;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Weather;
using Paddock.Simulation.Racing.Weekend;
using Paddock.Simulation.Time;

namespace Paddock.Application.Racing;

/// <summary>
/// The championship day (T47). Order 50: after the season rollover and before contracts, so a race's table exists when an
/// exit clause, an objective or the board reads it. The weekend itself draws only the streams <see cref="RaceWeekend"/> already
/// owns, each derived from the career seed by season and round (INV-004). It no longer votes the rules: a voted career's
/// proposals, ballots and results are the regulations module's (#275), and this handler only reads the rules in force and lays
/// the next season out on 31 December from whatever that module has decided by then.
/// </summary>
public sealed class RaceWeekendDay : IDayHandler
{
    /// <summary>After season rollover (40) and before negotiations (700).</summary>
    public const int HandlerOrder = 50;

    private static readonly DefaultClimateSource Climate = new();

    private readonly CareerModuleContext _context;
    private readonly RaceWatch _watch;

    public RaceWeekendDay(CareerModuleContext context, RaceWatch watch)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(watch);
        _context = context;
        _watch = watch;
    }

    public int Order => HandlerOrder;

    /// <summary>
    /// The engine of the player's races (PP-052, PP-074): the lap engine until the owner switches. Practice, qualifying and the race
    /// all come from it, so one career never mixes engines.
    /// </summary>
    public const RaceEngineKind Engine = RaceEngineKind.Lap;

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var today = context.Today;
        if (today.IsSeasonStart)
        {
            ClearChampionship();
        }

        if (today.IsSeasonEnd)
        {
            Desk().CloseSeason(today, CareerTeams.Active(_context.Session.World, today).Select(team => team.Id).ToArray());
            ScheduleNextSeason(context, today.Year + 1);
            SettleSeason(today);
        }

        foreach (var due in context.DueEvents)
        {
            if (due.TypeId == ScheduledEventType.Race && due.Payload is RaceSessionPayload payload)
            {
                RunRace(context, today, payload);
            }
        }
    }

    private void RunRace(DayContext context, GameDate today, RaceSessionPayload payload)
    {
        // The day books the round with the plans and the pit wall orders the world holds for it (#316): the commands that set
        // them are logged, so a replay of the log books the same race (INV-002). A round nobody planned or watched has none.
        var weekend = RaceWeekendState.Of(_context.Session, payload.Season, payload.Round);
        var orders = RaceWeekendState.Orders(weekend).Select(issued => issued.Order).ToImmutableArray();
        if (Simulate(today, payload, orders) is not { } run)
        {
            DropWeekend();
            return;
        }

        var published = run.Published;
        var points = PointsRules.For(run.Rules);
        var standings = LoadStandings(payload.Season, points, run.TotalRounds).Apply(published.Classification);
        StoreChampionship(payload.Season, standings, settled: false);
        ApplyUnderstanding(today, run.Layout.LengthKm, published.CarResults);
        ApplyMoney(today, payload, run.TotalRounds, published, run.Field, run.Layout.Country);
        ApplyPeople(context, today, published.PersonOutcomes, payload);
        ApplyDamage(today, payload, run);
        PostDueStandInDecisions(today);
        Publish(payload, run);
        NoteRerun(today, weekend, run);
        DropWeekend();
    }

    /// <summary>
    /// The race the players watched is not the one the day booked (#316): something changed the field or the cars between the
    /// watch and the day (a command on race morning, a contract or a birthday the day handles first). The day books the race it
    /// runs now, and every manager of a team in it is told, with the team's result, so the change is never silent (PP-058).
    /// </summary>
    private void NoteRerun(GameDate today, RaceWeekendSection? weekend, WeekendRun run)
    {
        if (weekend?.WatchedTapeHash is not { } watched || watched == run.Published.Tape.Hash
            || _context.TryGet<InboxBook>() is not { } inbox
            || (_context.TryGet<ControlTable>() ?? _context.TryGet<IOrganizationControl>()) is not { } control
            || _context.Managers is not { } managers)
        {
            return;
        }

        foreach (var team in run.Field.Entries.Select(entry => entry.ConstructorId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!Paddock.Application.Cars.CarCommandSupport.TryOrganization(team, out var organization))
            {
                continue;
            }

            var places = run.Published.Classification.Cars
                .Where(car => car.ConstructorId == team)
                .Select(car => car.IsClassified ? "P" + car.Position.ToString(CultureInfo.InvariantCulture) : "DNF");
            var draft = new InboxItemDraft(
                RaceWeekendKeys.RerunKind,
                RaceWeekendKeys.RerunSubject,
                [new("result", string.Join(", ", places))],
                null,
                null,
                null);
            foreach (var manager in HumanManagers(control, managers, organization))
            {
                inbox.Post(managers, manager, draft, today);
            }
        }
    }

    private void DropWeekend()
    {
        if (_context.Session.World.Section(RaceWeekendSection.SectionName) is not null)
        {
            _context.Session.StoreWorld(_context.Session.World.WithoutSection(RaceWeekendSection.SectionName));
        }
    }

    /// <summary>
    /// One round raced on its own, outside the championship (the quick race, #280). The weekend is the one
    /// <see cref="RunRace"/> runs, from the same field, rules and streams, so a quick race has no rules of its own (PP-058).
    /// Only the watch and the round archive get the race: no table, money, kilometres or injuries. False when the round
    /// cannot be raced (no rules for the season, no field).
    /// </summary>
    public bool RunAlone(GameDate day, RaceSessionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (Simulate(day, payload, []) is not { } run)
        {
            return false;
        }

        // Nothing is booked from a race run on its own, so the pit walls may still steer it while it is watched (#286).
        Publish(payload, run, new RaceSteering(new Rerun(this, day, payload, archive: true)));
        return true;
    }

    /// <summary>
    /// The career's race of today, watched before the day books it (#316). It is the race <see cref="RunRace"/> will run, from the
    /// same field, rules, streams and seed, and the pit walls steer it exactly as in a quick race. Only the watch gets it: nothing is
    /// booked or archived until the day is lived, and then the day runs the round with the orders given here, so watching and
    /// simulating are one race model (PP-058). False when the round cannot be raced.
    /// </summary>
    public bool Preview(GameDate day, RaceSessionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (Simulate(day, payload, []) is not { } run)
        {
            return false;
        }

        // The orders already given (this morning, or before a save) are in the world: the watch opens on the race they make.
        var issued = RaceWeekendState.Orders(RaceWeekendState.Of(_context.Session, payload.Season, payload.Round));
        if (!issued.IsDefaultOrEmpty && Simulate(day, payload, [.. issued.Select(order => order.Order)]) is { } ordered)
        {
            run = ordered;
        }

        Publish(payload, run, new RaceSteering(new Rerun(this, day, payload, archive: false), issued), archived: false);
        return true;
    }

    /// <summary>
    /// What the weekend's sessions before the race see of round <paramref name="payload"/> on <paramref name="day"/> (#316): the
    /// weekend input the race runs from (without orders), its practice, its qualifying and the tyres a plan may use. The sessions
    /// come from <see cref="IWeekendSessions"/>, so the steps do not depend on the engine. Null when the round cannot be raced.
    /// </summary>
    public WeekendSessions? Sessions(GameDate day, RaceSessionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (Prepare(day, payload, []) is not { } prepared)
        {
            return null;
        }

        var sessions = RaceSimulators.Sessions(Engine);
        return new WeekendSessions(
            prepared.Input,
            prepared.Field,
            sessions.Practice(prepared.Input),
            sessions.Qualifying(prepared.Input),
            TyreOffer.For(prepared.Input),
            PitRules.For(prepared.Rules));
    }

    /// <summary>The same round again, with the pit walls' orders: same field, rules, streams and seed (INV-002).</summary>
    private sealed class Rerun(RaceWeekendDay owner, GameDate day, RaceSessionPayload payload, bool archive) : IRaceRerun
    {
        public PreparedRace? Prepare(ImmutableArray<PitWallOrder> orders)
        {
            if (owner.Simulate(day, payload, orders) is not { } run)
            {
                return null;
            }

            var steering = owner._watch.Steering;
            return new PreparedRace(run.Published.Tape, () => owner.Publish(payload, run, steering, archive));
        }
    }

    private sealed record WeekendRun(
        RuleSet Rules,
        int TotalRounds,
        TrackLayout Layout,
        RaceField Field,
        RacePublishedFacts Published,
        StrategyCalls Calls,
        TyreOffer Tyres);

    private sealed record Prepared(RuleSet Rules, int TotalRounds, TrackLayout Layout, RaceField Field, RaceWeekendInput Input);

    private Prepared? Prepare(GameDate today, RaceSessionPayload payload, ImmutableArray<PitWallOrder> orders)
    {
        var rules = ActiveRules(payload.Season);
        if (rules is null)
        {
            return null;
        }

        var total = RoundsIn(payload.Season);
        if (total < 1)
        {
            return null;
        }

        var supply = _context.TryGet<SupplyBook>();
        var profiles = _context.TryGet<SupplyEnvironment>()?.Profiles;
        var traceSink = _context.TryGet<ITraceSink>();
        var layout = Layout(payload.LayoutId);
        var field = RaceFieldBuilder.Build(
            _context.Session.World,
            today,
            rules,
            total,
            supply?.Section,
            profiles,
            payload.Round,
            traceSink,
            layout.Country,
            _context.Inputs.TeamCountries);
        if (field.Entries.IsDefaultOrEmpty)
        {
            return null;
        }

        var era = EraOf(payload.Season);
        var input = new RaceWeekendInput
        {
            MasterSeed = _context.Session.Clock.MasterSeed,
            Season = payload.Season,
            Round = payload.Round,
            IsFinalRound = payload.Round == LastRound(payload.Season),
            Track = layout,
            Rules = rules,
            Safety = EraSafetyProfile.FromAuthored(payload.Season, era.Value("fatality_risk"), rules.Value("safety_car")),
            Fatality = _context.Inputs.Fatality,
            Climate = Climate,
            Month = today.Month,
            TotalLaps = RaceDistance.LapsFor(rules, layout),
            Entries = field.Entries,
            Orders = orders,
            Plans = RaceWeekendState.Plans(RaceWeekendState.Of(_context.Session, payload.Season, payload.Round)),
        };
        return new Prepared(rules, total, layout, field, input);
    }

    private WeekendRun? Simulate(GameDate today, RaceSessionPayload payload, ImmutableArray<PitWallOrder> orders)
    {
        if (Prepare(today, payload, orders) is not { } prepared)
        {
            return null;
        }

        var calls = new StrategyCalls();
        var request = RaceSimulationRequest.ForWeekend(prepared.Input, calls, geometry: _context.Inputs.TrackGeometry?.GeometryOf(payload.LayoutId));
        _ = RaceSession.Run(request, Engine);
        var published = request.Published ?? throw new InvalidOperationException("The race engine published no race facts.");
        return new WeekendRun(prepared.Rules, prepared.TotalRounds, prepared.Layout, prepared.Field, published, calls, TyreOffer.For(prepared.Input));
    }

    /// <summary>The race goes to the watch (the shell and the race mode) and, unless it is only watched before the day, to the round archive.</summary>
    private void Publish(RaceSessionPayload payload, WeekendRun run, RaceSteering? steering = null, bool archived = true)
    {
        var published = run.Published;
        _watch.Publish(
            payload.Season,
            payload.Round,
            payload.LayoutId,
            published.Tape,
            Lines(published),
            ByDriver(run.Calls.Calls, run.Field.Entries));
        _watch.PublishPitWall(published.PitWall, run.Tyres, steering);
        var world = _context.Session.World;
        var archive = world.Section<RaceResultsSection>(RaceResultsSection.SectionName) ?? RaceResultsSection.Empty;
        var recorded = RaceArchive.Record(archive, published, payload.Season, payload.Round, payload.LayoutId, run.Field.StandIns, (int)Math.Round(run.Layout.LengthKm * 1000d, MidpointRounding.AwayFromZero));
        _watch.PublishStored(recorded.Find(payload.Season, payload.Round));
        if (archived)
        {
            _context.Session.StoreWorld(world.WithSection(recorded));
        }
    }

    private CarDamageDesk Desk() => new(
        () => _context.Session.World,
        _context.Session.StoreWorld,
        _context.TryGet<ControlTable>() ?? _context.TryGet<IOrganizationControl>(),
        _context.TryGet<InboxBook>(),
        _context.Managers);

    /// <summary>
    /// A car that crashed out (retired by accident) is damaged: it costs money and days (#270). The same stream and rules serve
    /// every team. A car that ran on the spare and crashed loses the spare instead. Cars left out or on a spare are told to the
    /// team, so a missed race is never silent.
    /// </summary>
    private void ApplyDamage(GameDate today, RaceSessionPayload payload, WeekendRun run)
    {
        var desk = Desk();
        desk.NoteRaceField(run.Field, today);
        var stream = RngStream.Derive(_context.Session.Clock.MasterSeed, RngStreamName.Incidents, payload.Season, payload.Round);
        var crashed = run.Published.PersonOutcomes
            .Where(outcome => outcome.Retired && outcome.Reason == RetirementReason.Accident)
            .OrderBy(outcome => outcome.CarId, StringComparer.Ordinal)
            .ToArray();
        var done = new HashSet<string>(StringComparer.Ordinal);
        foreach (var outcome in crashed)
        {
            if (!done.Add(outcome.CarId))
            {
                continue;
            }

            if (run.Field.SpareRuns.IsDefault is false && run.Field.SpareRuns.FirstOrDefault(spare => spare.CarId == outcome.CarId) is { } onSpare)
            {
                desk.WriteOffSpare(onSpare.Organization, outcome.CarId, today);
                continue;
            }

            var hurt = outcome.Fatal || outcome.Injury != InjuryGrade.None;
            desk.Record(outcome.CarId, DamageSource.Race, today, hurt, stream.DeriveChild($"damage:{outcome.CarId}"));
        }
    }

    private void ApplyUnderstanding(GameDate today, double lengthKm, IReadOnlyList<CarRaceResult> results)
    {
        if (_context.TryGet<DevelopmentBook>() is not { } book || _context.TryGet<DevelopmentEnvironment>() is not { } environment)
        {
            return;
        }

        var runs = new List<(string CarId, int Kilometres)>(results.Count);
        foreach (var result in results)
        {
            var kilometres = (int)Math.Round(result.LapsCompleted * lengthKm, MidpointRounding.AwayFromZero);
            runs.Add((result.CarId, kilometres));
        }

        DevelopmentRaceHook.OnRaceFinished(book, environment, today, runs);
    }

    private void ApplyMoney(GameDate today, RaceSessionPayload payload, int total, RacePublishedFacts published, RaceField field, string circuitCountry)
    {
        if (_context.Inputs.Eras is not { } eras || _context.TryGet<FinanceBook>() is not { } book || !book.Section.HasBooks)
        {
            return;
        }

        var facts = eras.Facts(payload.Season);
        var rows = new List<(string OrganizationId, string DriverId, int Position, bool Classified)>(published.Classification.Cars.Length);
        foreach (var car in published.Classification.Cars)
        {
            if (car.DriverIds.IsDefaultOrEmpty)
            {
                continue;
            }

            rows.Add((car.ConstructorId, car.DriverIds[0], car.Position, car.IsClassified));
        }

        if (rows.Count == 0)
        {
            return;
        }

        var race = FinanceStandings.Race(payload.Season, payload.Round, total, facts.RevenueModel, rows);
        var (section, _) = book.Section.ApplyRace(race, Money.FromDollars(facts.TypicalDollars).Cents, today);
        var typical = section.TypicalCents;
        var seasonCircuits = SeasonCircuitCountries(payload.Season);
        var started = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var entry in field.Entries)
        {
            started.Add(entry.ConstructorId);
        }

        foreach (var teamId in started)
        {
            var organization = default(OrganizationId);
            if (!Paddock.Application.Cars.CarCommandSupport.TryOrganization(teamId, out organization))
            {
                continue;
            }
            var due = RaceFieldBuilder.TransportCost(organization, typical, circuitCountry, _context.Inputs.TeamCountries, seasonCircuits);
            if (due > 0 && section.HasBook(organization))
            {
                section = section.Post(
                    organization,
                    today,
                    LedgerCategories.Logistics,
                    circuitCountry.Length == 0 ? null : circuitCountry,
                    -due,
                    Paddock.Application.Infrastructure.InfrastructureKeys.LedgerLogistics);
            }
        }

        book.Replace(section);
    }

    /// <summary>
    /// The country of every race of the season being raced, from the season's stored calendar: what the transport of a round is split over (#268).
    /// Empty when the calendar or a track is not known, and then a round is priced as one of a usual season. It reads only what exists when the race is posted.
    /// </summary>
    private IReadOnlyList<string?> SeasonCircuitCountries(int season)
    {
        var calendar = _context.Session.World.Section<RaceCalendarSection>(RaceCalendarSection.SectionName);
        if (calendar is null || _context.Inputs.Layouts is not { } layouts)
        {
            return [];
        }

        var countries = new List<string?>();
        foreach (var session in calendar.SeasonSessions(season).Where(session => session.TypeId == ScheduledEventType.Race))
        {
            var layout = layouts.FirstOrDefault(item => string.Equals(item.Id, session.LayoutId, StringComparison.Ordinal));
            if (layout is not null)
            {
                countries.Add(layout.Country);
            }
        }

        return countries;
    }

    private void ApplyPeople(DayContext context, GameDate today, IReadOnlyList<PersonRaceOutcome> outcomes, RaceSessionPayload payload)
    {
        var ordered = outcomes.OrderBy(outcome => outcome.DriverId, StringComparer.Ordinal).ToArray();
        foreach (var outcome in ordered)
        {
            if (outcome.Fatal || outcome.Injury == InjuryGrade.CareerEnding)
            {
                PersonId? id = null;
                foreach (var person in _context.Session.World.Persons)
                {
                    if (person.Id.Value == outcome.DriverId)
                    {
                        id = person.Id;
                        break;
                    }
                }

                if (id is PersonId personId)
                {
                    _context.Session.RetireForRace(personId, today, context);
                }
            }
            else if (outcome.Injury is InjuryGrade.Light or InjuryGrade.Serious)
            {
                PersonId? id = null;
                foreach (var person in _context.Session.World.Persons)
                {
                    if (person.Id.Value == outcome.DriverId)
                    {
                        id = person.Id;
                        break;
                    }
                }

                if (id is not PersonId personId)
                {
                    continue;
                }

                // Draw races out from child stream of Incidents (PP-061)
                var incidentStream = RngStream.Derive(_context.Session.Clock.MasterSeed, RngStreamName.Incidents, payload.Season, payload.Round);
                var rng = incidentStream.DeriveChild($"injury:{outcome.DriverId}:{today}");

                int racesOut;
                if (outcome.Injury == InjuryGrade.Light)
                {
                    racesOut = rng.NextDouble() < IncidentConstants.LightInjuryZeroRacesProbability ? 0 : 1;
                }
                else
                {
                    racesOut = rng.NextInt(IncidentConstants.MinSeriousRaces, IncidentConstants.MaxSeriousRaces + 1);
                }

                var upcomingRaces = _context.Session.Clock.Queue.Events
                    .Where(e => e.TypeId == ScheduledEventType.Race && e.Date > today)
                    .OrderBy(e => e.Date)
                    .Select(e => e.Date)
                    .ToList();

                GameDate injuredUntil;
                if (racesOut == 0)
                {
                    injuredUntil = today;
                }
                else if (upcomingRaces.Count >= racesOut)
                {
                    injuredUntil = upcomingRaces[racesOut - 1];
                }
                else if (upcomingRaces.Count > 0)
                {
                    injuredUntil = upcomingRaces[^1];
                }
                else
                {
                    injuredUntil = new GameDate(today.Year, 12, 31);
                }

                _context.Session.StoreWorld(_context.Session.World.InjurePerson(personId, injuredUntil));
            }
        }
    }

    private void PostDueStandInDecisions(GameDate today)
    {
        var world = _context.Session.World;
        var upcomingRaces = _context.Session.Clock.Queue.Events
            .Where(e => e.TypeId == ScheduledEventType.Race && e.Date > today)
            .OrderBy(e => e.Date)
            .Select(e => e.Date)
            .ToList();

        if (upcomingRaces.Count == 0)
        {
            return;
        }

        var nextRaceDate = upcomingRaces[0];
        var cars = world.Section<CarsSection>(CarsSection.SectionName);
        if (cars is null)
        {
            return;
        }

        var control = _context.TryGet<ControlTable>() ?? _context.TryGet<IOrganizationControl>();
        var managers = _context.Managers;
        var inbox = _context.TryGet<InboxBook>();
        if (control is null || managers is null || inbox is null)
        {
            return;
        }

        foreach (var team in CareerTeams.Active(world, today))
        {
            var humans = HumanManagers(control, managers, team.Id);
            if (humans.Count == 0)
            {
                continue;
            }

            var owned = cars.Of(team.Id).Where(c => c.Season == today.Year);
            foreach (var car in owned)
            {
                // A car with nobody in it (both contracts ended, #253) gets the same decision as an injured driver's car.
                var seated = car.Driver ?? default;
                var person = car.Driver is null ? null : world.GetPerson(seated);
                if (car.Driver is not null && (person is null || !person.IsInjured(nextRaceDate)))
                {
                    continue;
                }

                var subjectId = person is null ? StandInResolver.VacantPrefix + car.Id : seated.Value;

                var existingSection = world.Section<InboxSection>(InboxSection.SectionName);
                if (existingSection is not null)
                {
                    var alreadyOpen = existingSection.Items.Any(i =>
                        i.IsOpen
                        && i.Kind == StandInResolver.Kind
                        && i.Arguments.TryGetValue("driverId", out var dId) && dId == subjectId
                        && i.Arguments.TryGetValue("raceDate", out var rDate) && rDate == nextRaceDate.ToString());
                    if (alreadyOpen)
                    {
                        continue;
                    }
                }

                var draft = BuildStandInDraft(world, team, person, car.Id, nextRaceDate, today);

                foreach (var human in humans)
                {
                    inbox.Post(managers, human, draft, today);
                }
            }
        }
    }

    internal static InboxItemDraft BuildStandInDraft(
        WorldState world,
        Organization team,
        Person? person,
        string carId,
        GameDate nextRaceDate,
        GameDate today)
    {
        var candidates = StandInCandidateFinder.FindCandidates(world, team.Id, nextRaceDate);
        var options = new List<InboxOption>();
        var teamName = team.NameOn(today);

        foreach (var candidate in candidates.Take(5))
        {
            var isReserve = world.Contracts.Any(c =>
                c.OrganizationId == team.Id
                && c.PersonId == candidate.Id
                && c.IsActiveOn(today)
                && c.Role.IsDriver
                && c.Role.Seat == SeatStatus.Reserve);

            var belief = world.KnowledgeOf(team.Id, candidate.Id);
            string bandText;
            if (belief is { } known && known.Attributes.Count > 0)
            {
                var driverAttrs = known.Attributes
                    .Where(a => GenerationEstimates.DriverAttributeKeys.Contains(a.Key))
                    .ToList();
                if (driverAttrs.Count > 0)
                {
                    var low = (int)Math.Round(driverAttrs.Average(a => a.Band.Low));
                    var high = (int)Math.Round(driverAttrs.Average(a => a.Band.High));
                    bandText = $"{low}–{high}";
                }
                else
                {
                    bandText = "1–20";
                }
            }
            else
            {
                bandText = "1–20";
            }

            var costText = "$0";
            var consequenceKey = isReserve
                ? StandInKeys.OptionReserveConsequence
                : StandInKeys.OptionFreeAgentConsequence;

            var optionArguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["candidate"] = candidate.Name,
                ["team"] = teamName,
                ["band"] = bandText,
                ["cost"] = costText,
            };

            options.Add(new InboxOption(
                candidate.Id.Value,
                StandInKeys.OptionCandidateLabel,
                consequenceKey,
                optionArguments));
        }

        options.Add(new InboxOption(
            StandInResolver.OptionSkip,
            StandInKeys.OptionSkipLabel,
            StandInKeys.OptionSkipConsequence));

        var defaultOptionId = candidates.Count > 0 ? candidates[0].Id.Value : StandInResolver.OptionSkip;

        var arguments = new List<KeyValuePair<string, string>>
        {
            new("driver", person?.Name ?? string.Empty),
            new("driverId", person?.Id.Value ?? StandInResolver.VacantPrefix + carId),
            new("team", teamName),
            new("car", carId),
            new("raceDate", nextRaceDate.ToString()),
        };
        if (person is null)
        {
            // An empty seat is filled by the default on its own, so the question never holds the clock.
            arguments.Add(new(InboxItemDraft.FreeClockArgument, InboxItemDraft.FreeClockValue));
        }

        return new InboxItemDraft(
            StandInResolver.Kind,
            person is null ? StandInKeys.VacantSubject : StandInKeys.Subject,
            arguments,
            options,
            nextRaceDate,
            defaultOptionId);
    }

    private static IReadOnlyList<ManagerId> HumanManagers(IOrganizationControl control, ManagerRegistry managers, OrganizationId organization)
    {
        var list = new List<ManagerId>();
        foreach (var manager in control.ManagersOf(organization))
        {
            if (managers.Contains(manager) && managers.KindOf(manager) == ManagerKind.Human)
            {
                list.Add(manager);
            }
        }
        return list;
    }

    private void SettleSeason(GameDate today)
    {
        var section = ChampionshipOf();
        if (section is null || section.Settled || section.Season != today.Year || section.RoundsCompleted == 0)
        {
            return;
        }

        if (_context.Inputs.Eras is null || _context.TryGet<FinanceBook>() is not { } book || !book.Section.HasBooks)
        {
            StoreChampionship(section.Season, LoadStandings(section.Season, PointsOf(section.Season), section.TotalRounds), settled: true);
            return;
        }

        var standings = LoadStandings(section.Season, PointsOf(section.Season), section.TotalRounds);
        var constructors = standings.Constructors();
        var winners = constructors.Where(row => row.Wins > 0).Select(row => row.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var season = FinanceStandings.Season(section.Season, section.RoundsCompleted, constructors, winners, standings.Drivers());
        book.Replace(book.Section.ApplySeason(season));
        StoreChampionship(section.Season, standings, settled: true);
    }

    private void ScheduleNextSeason(DayContext context, int season)
    {
        if (_context.Inputs.Layouts is not { } layouts || _context.Inputs.RaceAssignments is not { } assignments)
        {
            return;
        }

        if (_context.Session.HasChampionship(season))
        {
            return;
        }

        var (plan, world) = SeasonPlans.Ensure(_context.Session.World, season, layouts, assignments, _context.Inputs.RaceDates);
        if (!ReferenceEquals(world, _context.Session.World))
        {
            _context.Session.StoreWorld(world);
        }

        foreach (var session in plan)
        {
            if (session.Date > context.Today)
            {
                context.Schedule(session.Date, session.TypeId, new RaceSessionPayload(session.Season, session.Round, session.LayoutId));
            }
        }
    }

    private void ClearChampionship()
    {
        var world = _context.Session.World;
        if (world.Section(ChampionshipSection.SectionName) is null)
        {
            return;
        }

        _context.Session.StoreWorld(world.WithoutSection(ChampionshipSection.SectionName));
    }

    private RuleSet? ActiveRules(int season)
    {
        var stored = RegulationsOf()?.RuleSetFor(SeriesIds.WorldChampionship, season);
        if (stored is not null)
        {
            return stored;
        }

        if (_context.Inputs.RegulationDimensionIds is not { } dimensions || _context.Inputs.RulePeriods is not { } periods)
        {
            return null;
        }

        return RuleSet.For(season, dimensions, periods);
    }

    private EraSet EraOf(int season)
    {
        var dimensions = _context.Inputs.EraDimensionIds ?? throw new InvalidOperationException("A race needs the era catalog.");
        var periods = _context.Inputs.EraPeriods ?? throw new InvalidOperationException("A race needs the era timeline.");
        return EraSet.For(season, dimensions, periods);
    }

    private PointsRules PointsOf(int season) =>
        PointsRules.For(ActiveRules(season) ?? throw new InvalidOperationException("A table needs the season's rules."));

    private TrackLayout Layout(string layoutId)
    {
        var layouts = _context.Inputs.Layouts ?? throw new InvalidOperationException("A race needs the track catalog.");
        foreach (var layout in layouts)
        {
            if (string.Equals(layout.Id, layoutId, StringComparison.Ordinal))
            {
                return layout;
            }
        }

        throw new InvalidOperationException("Layout '" + layoutId + "' is not in the career inputs.");
    }

    private int RoundsIn(int season)
    {
        // A season laid out keeps the rounds it was laid out with: a voted calendar (#275) has more or fewer than the authored map.
        if (SeasonPlans.Rounds(_context.Session.World, season) is { } laid)
        {
            return laid.Rounds;
        }

        var count = 0;
        if (_context.Inputs.RaceAssignments is not { } assignments)
        {
            return 0;
        }

        foreach (var assignment in assignments)
        {
            if (assignment.Season == season)
            {
                count++;
            }
        }

        return count;
    }

    private int LastRound(int season)
    {
        if (SeasonPlans.Rounds(_context.Session.World, season) is { } laid)
        {
            return laid.LastRound;
        }

        var last = 0;
        foreach (var assignment in _context.Inputs.RaceAssignments ?? [])
        {
            if (assignment.Season == season && assignment.Round > last)
            {
                last = assignment.Round;
            }
        }

        return last;
    }

    private ChampionshipSection? ChampionshipOf() =>
        _context.Session.World.Section<ChampionshipSection>(ChampionshipSection.SectionName);

    private RegulationsSection? RegulationsOf() =>
        _context.Session.World.Section<RegulationsSection>(RegulationsSection.SectionName);

    private Standings LoadStandings(int season, PointsRules rules, int totalRounds)
    {
        var section = ChampionshipOf();
        if (section is null || section.Season != season)
        {
            return Standings.Start(rules, totalRounds);
        }

        return Standings.Restore(rules, section.TotalRounds, section.RoundsCompleted, Snapshots(section.Drivers), Snapshots(section.Constructors));
    }

    private void StoreChampionship(int season, Standings standings, bool settled)
    {
        var (drivers, constructors) = standings.Snapshot();
        var section = ChampionshipSection.Create(
            season,
            standings.TotalRounds,
            standings.RoundsCompleted,
            settled,
            Ledgers(drivers),
            Ledgers(constructors));
        _context.Session.StoreWorld(_context.Session.World.WithSection(section));
    }

    private static Standings.LedgerSnapshot[] Snapshots(IReadOnlyList<ChampionshipLedger> rows)
    {
        var snapshots = new Standings.LedgerSnapshot[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            snapshots[i] = new Standings.LedgerSnapshot(row.Id, [.. row.RoundPoints], [.. row.Positions]);
        }

        return snapshots;
    }

    private static ChampionshipLedger[] Ledgers(IReadOnlyList<Standings.LedgerSnapshot> rows)
    {
        var ledgers = new ChampionshipLedger[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            ledgers[i] = new ChampionshipLedger(row.Id, row.RoundPoints, row.Positions);
        }

        return ledgers;
    }

    /// <summary>The strategist names the car; the tape and the screen name the driver who started it.</summary>
    private static StrategyCall[] ByDriver(IReadOnlyList<StrategyCall> calls, IReadOnlyList<RaceEntry> entries)
    {
        var drivers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            drivers[entry.CarId] = entry.Primary.DriverId;
        }

        return calls
            .Where(call => drivers.ContainsKey(call.CarId))
            .Select(call => call with { CarId = drivers[call.CarId] })
            .ToArray();
    }

    private static RaceResultLine[] Lines(RacePublishedFacts published)
    {
        var lines = new RaceResultLine[published.Classification.Cars.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            var car = published.Classification.Cars[i];
            var driver = car.DriverIds.IsDefaultOrEmpty ? "" : car.DriverIds[0];
            lines[i] = new RaceResultLine(
                car.Position,
                car.IsClassified,
                driver,
                car.ConstructorId,
                car.CarPoints.ToString(CultureInfo.InvariantCulture));
        }

        return lines;
    }
}
