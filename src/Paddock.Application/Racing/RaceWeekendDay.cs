using System.Globalization;
using Paddock.Application.Career;
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
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Weather;
using Paddock.Simulation.Racing.Weekend;
using Paddock.Simulation.Regulation;
using Paddock.Simulation.Time;

namespace Paddock.Application.Racing;

/// <summary>
/// The championship day (T47). Order 50: after the season rollover and before contracts, so a race's table exists when an
/// exit clause, an objective or the board reads it. The weekend itself draws only the streams <see cref="RaceWeekend"/> already
/// owns, each derived from the career seed by season and round (INV-004). A voted season draws the Regulations stream
/// the same way, once, on 31 December.
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
            ScheduleNextSeason(context, today.Year + 1);
            VoteNextSeason(today);
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
        if (Simulate(today, payload) is not { } run)
        {
            return;
        }

        var published = run.Published;
        var points = PointsRules.For(run.Rules);
        var standings = LoadStandings(payload.Season, points, run.TotalRounds).Apply(published.Classification);
        StoreChampionship(payload.Season, standings, settled: false);
        ApplyUnderstanding(today, run.Layout.LengthKm, published.CarResults);
        ApplyMoney(today, payload, run.TotalRounds, published, run.Field, run.Layout.Country);
        ApplyPeople(context, today, published.PersonOutcomes, payload);
        PostDueStandInDecisions(today);
        Publish(payload, run);
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
        if (Simulate(day, payload) is not { } run)
        {
            return false;
        }

        Publish(payload, run);
        return true;
    }

    private sealed record WeekendRun(
        RuleSet Rules,
        int TotalRounds,
        TrackLayout Layout,
        RaceField Field,
        RacePublishedFacts Published,
        StrategyCalls Calls);

    private WeekendRun? Simulate(GameDate today, RaceSessionPayload payload)
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
        };
        var calls = new StrategyCalls();
        var request = RaceSimulationRequest.ForWeekend(input, calls);
        _ = RaceSession.Run(request);
        var published = request.Published ?? throw new InvalidOperationException("The lap engine published no race facts.");
        return new WeekendRun(rules, total, layout, field, published, calls);
    }

    /// <summary>The race goes to the watch (the shell and the race mode) and to the round archive.</summary>
    private void Publish(RaceSessionPayload payload, WeekendRun run)
    {
        var published = run.Published;
        _watch.Publish(
            payload.Season,
            payload.Round,
            payload.LayoutId,
            published.Tape,
            Lines(published),
            run.Field.SkippedTeamIds,
            ByDriver(run.Calls.Calls, run.Field.Entries));
        var world = _context.Session.World;
        var archive = world.Section<RaceResultsSection>(RaceResultsSection.SectionName) ?? RaceResultsSection.Empty;
        _context.Session.StoreWorld(world.WithSection(RaceArchive.Record(archive, published, payload.Season, payload.Round, payload.LayoutId, run.Field.StandIns, (int)Math.Round(run.Layout.LengthKm * 1000d, MidpointRounding.AwayFromZero))));
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
            var due = RaceFieldBuilder.TransportCost(organization, typical, circuitCountry, _context.Inputs.TeamCountries);
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

    private void VoteNextSeason(GameDate today)
    {
        if (_context.Inputs.Rules != RulesSource.VotedEachSeason || _context.Inputs.RegulationCatalog is not { } catalog)
        {
            return;
        }

        var nextYear = today.Year + 1;
        var current = ActiveRules(today.Year);
        if (current is null)
        {
            return;
        }

        var teams = CareerTeams.Active(_context.Session.World, today);
        if (teams.Count == 0)
        {
            return;
        }

        var stored = RegulationsOf();
        var rejected = new List<RejectedProposal>();
        if (stored is not null)
        {
            foreach (var item in stored.Rejected)
            {
                rejected.Add(new RejectedProposal(item.DimensionId, item.Value, item.Season));
            }
        }

        var rounds = RoundsIn(today.Year);
        var standings = rounds > 0 && ChampionshipOf() is { Season: var season } && season == today.Year
            ? LoadStandings(today.Year, PointsOf(today.Year), rounds)
            : null;
        var place = new Dictionary<string, (decimal Points, int Position)>(StringComparer.Ordinal);
        if (standings is not null)
        {
            foreach (var row in standings.Constructors())
            {
                place[row.Id] = (row.CountedPoints, row.Position);
            }
        }

        var proposers = new List<string>(teams.Count);
        var voters = new List<Voter>(teams.Count);
        foreach (var team in teams)
        {
            proposers.Add(team.Id.Value);
            var (points, position) = place.TryGetValue(team.Id.Value, out var row) ? row : (0m, teams.Count);
            voters.Add(new Voter(team.Id.Value, new StableTasteInterest(team.Id.Value), (double)points, position));
        }

        var rng = ProposalGenerator.StreamFor(_context.Session.Clock.MasterSeed, nextYear);
        var proposals = ProposalGenerator.Generate(current, catalog, proposers, rejected, new ProposalGeneratorOptions(), rng);
        var outcomes = SeasonVote.Run(current, proposals, voters, VotingBodyDefaults.ForSeason(nextYear), rng);
        var specs = new Dictionary<string, RuleDimensionSpec>(catalog.Count, StringComparer.Ordinal);
        foreach (var spec in catalog)
        {
            specs[spec.Id] = spec;
        }

        var next = SeasonVote.Apply(current, outcomes, specs, nextYear);
        var memory = new List<RejectedRegulation>();
        if (stored is not null)
        {
            memory.AddRange(stored.Rejected);
        }

        foreach (var outcome in outcomes)
        {
            if (!outcome.Passed)
            {
                memory.Add(new RejectedRegulation(outcome.Proposal.DimensionId, outcome.Proposal.ProposedValue, nextYear));
            }
        }

        _context.Session.StoreWorld(_context.Session.World.WithSection(RegulationsSection.Create(nextYear, next.Values, memory)));
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
        var stored = RegulationsOf();
        if (stored is not null && stored.Season == season)
        {
            return stored.ToRuleSet();
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
