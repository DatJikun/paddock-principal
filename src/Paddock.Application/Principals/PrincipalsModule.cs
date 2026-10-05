using System.Globalization;
using Paddock.Application.Career;
using Paddock.Application.Cars;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Finance;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.Pool;
using Paddock.Application.Sponsors;
using Paddock.Application.Supply;
using Paddock.Domain.Development;
using Paddock.Domain.Objectives;
using Paddock.Domain.Principals;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Ai;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;

namespace Paddock.Application.Principals;

/// <summary>
/// T44 AI team principals in the career loop (#184). One shared <see cref="ControlTable"/> is already the control every module
/// environment uses. This module builds the <see cref="PrincipalEnvironment"/> from whichever of those modules attached (each
/// part is optional), registers the review command, and files the morning's commands after the books have been seated and
/// before the queue is dispatched. A team a human runs is skipped.
/// <para>
/// The seat watch (order 15) only remembers who employs whom. It writes nothing. After the day, a retirement of someone who
/// had a seat, a season whose announced rules differ, or a race day pulls the next review forward. The hint is in memory only.
/// </para>
/// </summary>
public sealed class PrincipalsModule : CareerModule
{
    public const string ModuleName = "principals";

    /// <summary>Before ageing (20), so a retirement later that morning can still name the team the person drove for.</summary>
    public const int SeatOrder = 15;

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections => [PrincipalsSection.SectionName];

    public override IReadOnlyList<CommandCodecEntry> CommandCodecs => PrincipalCommandCodecs.Entries;

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var session = context.Session;
        var control = context.Require<ControlTable>();
        var managers = context.Managers;
        var outlook = OutlookOf(context);
        var environment = new PrincipalEnvironment(session.Clock.MasterSeed, () => session.World, managers, [control])
        {
            Contracts = context.TryGet<ContractEngine>(),
            Development = DevelopmentOf(context),
            Supply = SupplyOf(context),
            Sponsors = SponsorsOf(context),
            Pool = context.TryGet<PoolBook>(),
            Outlook = outlook,
            Trace = NullSink.Instance,
        };

        PrincipalDirector? director = null;
        context.AddCommandHandlers(dispatcher =>
            director = PrincipalRegistration.Register(dispatcher, PrincipalsBook.ForSession(session), environment));
        if (director is null)
        {
            throw new InvalidOperationException("The principal director was not registered.");
        }

        var principals = director;
        context.Provide(principals);
        PrincipalStreaks.Seed(principals, context.CommandLog);
        var book = PrincipalsBook.ForSession(session);
        context.AddMorning(queue =>
        {
            SeatTeams(session, managers, control);
            principals.FileCommands(queue, session.Date);
        });

        var seats = new Dictionary<string, List<OrganizationId>>(StringComparer.Ordinal);
        context.AddDayHandler(new SeatWatch(session, seats));
        context.AddAfterDay(SeatOrder, events => PrincipalTriggers.Apply(principals, book, session.Date, events, outlook, seats));
    }

    /// <summary>
    /// Registers the AI manager of every team a human does not run, and seats them on the shared table. Quiet days still need
    /// the manager: a team with no cars is approved, and an inbox item is addressed, before the next review.
    /// </summary>
    private static void SeatTeams(CareerSession session, ManagerRegistry managers, ControlTable control)
    {
        foreach (var team in CareerTeams.Active(session.World, session.Date))
        {
            if (HumanRuns(control, managers, team.Id))
            {
                continue;
            }

            var manager = PrincipalKeys.ManagerOf(team.Id);
            if (!managers.Contains(manager))
            {
                managers.Register(manager, ManagerKind.Ai, "AI " + team.Id.Value);
            }

            control.Assign(manager, team.Id);
        }
    }

    private static bool HumanRuns(ControlTable control, ManagerRegistry managers, OrganizationId organization)
    {
        foreach (var manager in control.ManagersOf(organization))
        {
            if (managers.Contains(manager) && managers.KindOf(manager) == ManagerKind.Human)
            {
                return true;
            }
        }

        return false;
    }

    private static IRegulationOutlook OutlookOf(CareerModuleContext context) =>
        context.Inputs.RulePeriods is { } periods
            ? new DevelopmentRulesOutlook(new PeriodDevelopmentRules(periods))
            : NoRegulationOutlook.Instance;

    private static DevelopmentSources? DevelopmentOf(CareerModuleContext context)
    {
        var book = context.TryGet<DevelopmentBook>();
        var environment = context.TryGet<DevelopmentEnvironment>();
        var cars = context.TryGet<CarBook>();
        return book is null || environment is null || cars is null ? null : new DevelopmentSources(book, environment, cars);
    }

    private static SupplySources? SupplyOf(CareerModuleContext context)
    {
        var book = context.TryGet<SupplyBook>();
        var environment = context.TryGet<SupplyEnvironment>();
        return book is null || environment is null ? null : new SupplySources(book, environment);
    }

    private static SponsorSources? SponsorsOf(CareerModuleContext context)
    {
        var book = context.TryGet<SponsorBook>();
        var environment = context.TryGet<SponsorEnvironment>();
        var facts = context.TryGet<IObjectiveFacts>();
        var organizations = context.TryGet<IManagerOrganizations>();
        return book is null || environment is null || facts is null || organizations is null
            ? null
            : new SponsorSources(book, environment, new ObjectiveQuery(facts, organizations));
    }

    /// <summary>Copies the morning's contracts into memory. It does not change the world and it draws no random numbers.</summary>
    private sealed class SeatWatch : IDayHandler
    {
        private readonly CareerSession _session;
        private readonly Dictionary<string, List<OrganizationId>> _seats;

        public SeatWatch(CareerSession session, Dictionary<string, List<OrganizationId>> seats)
        {
            _session = session;
            _seats = seats;
        }

        public int Order => SeatOrder;

        public void OnDay(DayContext context)
        {
            _ = context;
            _seats.Clear();
            foreach (var contract in _session.World.Contracts)
            {
                var key = contract.PersonId.Value;
                if (!_seats.TryGetValue(key, out var organizations))
                {
                    organizations = [];
                    _seats.Add(key, organizations);
                }

                if (!organizations.Contains(contract.OrganizationId))
                {
                    organizations.Add(contract.OrganizationId);
                }
            }
        }
    }
}

/// <summary>One in-memory pull of a review. A null organization means every team.</summary>
public readonly record struct PrincipalRaise(ReviewTrigger Trigger, OrganizationId? Organization);

/// <summary>
/// Turns the day's events into review hints. A retirement pulls the teams that employed the person (the seat list is the
/// morning's contracts, because retirement removes them). A new season pulls everyone when the announced rules differ. A
/// race day pulls everyone. Nothing here is saved.
/// </summary>
public static class PrincipalTriggers
{
    public static IReadOnlyList<PrincipalRaise> Planned(
        IReadOnlyList<DomainEvent> events,
        IRegulationOutlook outlook,
        IReadOnlyDictionary<string, List<OrganizationId>> seats)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(outlook);
        ArgumentNullException.ThrowIfNull(seats);
        var planned = new List<PrincipalRaise>();
        foreach (var evt in events)
        {
            if (evt.TypeId == CareerEventType.Retired && evt.Payload is MarkerPayload retired && seats.TryGetValue(retired.Marker, out var organizations))
            {
                foreach (var organization in organizations)
                {
                    planned.Add(new PrincipalRaise(ReviewTrigger.Vacancy, organization));
                }
            }
            else if (evt.TypeId == CareerEventType.SeasonChanged
                && evt.Payload is MarkerPayload season
                && int.TryParse(season.Marker, NumberStyles.None, CultureInfo.InvariantCulture, out var year)
                && outlook.ChangeAfter(year - 1) > 0)
            {
                planned.Add(new PrincipalRaise(ReviewTrigger.RegulationChange, null));
            }
            else if (evt.TypeId == ScheduledEventType.Race)
            {
                planned.Add(new PrincipalRaise(ReviewTrigger.RaceResult, null));
            }
        }

        return planned;
    }

    public static void Apply(
        PrincipalDirector director,
        PrincipalsBook book,
        GameDate nextMorning,
        IReadOnlyList<DomainEvent> events,
        IRegulationOutlook outlook,
        Dictionary<string, List<OrganizationId>> seats)
    {
        ArgumentNullException.ThrowIfNull(director);
        ArgumentNullException.ThrowIfNull(book);
        foreach (var raise in Planned(events, outlook, seats))
        {
            director.Raise(raise.Trigger, raise.Organization);
            if (raise.Organization is OrganizationId one)
            {
                Pull(book, one, nextMorning);
            }
            else
            {
                foreach (var team in CareerTeams.Active(book.World, nextMorning))
                {
                    Pull(book, team.Id, nextMorning);
                }
            }
        }

        seats.Clear();
    }

    /// <summary>
    /// Writes the pulled morning into the saved record when it is earlier than the scheduled one. The in-memory
    /// <see cref="PrincipalDirector.Raise"/> does not survive a save taken the next morning, and a resumed run must review
    /// the same teams (INV-002).
    /// </summary>
    private static void Pull(PrincipalsBook book, OrganizationId organization, GameDate nextMorning)
    {
        var record = book.Section.Of(organization);
        if (record is null || record.NextReview <= nextMorning)
        {
            return;
        }

        if (record.LastReview is GameDate last && nextMorning < last)
        {
            return;
        }

        book.Replace(book.Section.With(record with { NextReview = nextMorning }));
    }
}

/// <summary>The follow-up streak, read back from the reviews that filed a command.</summary>
public static class PrincipalStreaks
{
    public static void Seed(PrincipalDirector director, CommandLog log)
    {
        ArgumentNullException.ThrowIfNull(director);
        ArgumentNullException.ThrowIfNull(log);
        var reviews = new Dictionary<string, List<DateOnly>>(StringComparer.Ordinal);
        var filed = new HashSet<(string Manager, DateOnly Day)>();
        foreach (var command in log.Entries)
        {
            if (command is RecordPrincipalReviewCommand review)
            {
                if (!reviews.TryGetValue(review.OrganizationId, out var days))
                {
                    days = [];
                    reviews.Add(review.OrganizationId, days);
                }

                days.Add(review.IssuedOn);
            }
            else if (PrincipalKeys.IsPrincipalManager(command.ManagerId))
            {
                filed.Add((command.ManagerId.Value, command.IssuedOn));
            }
        }

        foreach (var (organization, days) in reviews)
        {
            var manager = PrincipalKeys.ManagerPrefix + organization;
            var streak = 0;
            for (var i = days.Count - 1; i >= 0; i--)
            {
                if (!filed.Contains((manager, days[i])))
                {
                    break;
                }

                streak++;
            }

            if (streak > 0 && FinanceIds.TryParse(organization, out var id))
            {
                director.RememberStreak(id, streak);
            }
        }
    }
}
