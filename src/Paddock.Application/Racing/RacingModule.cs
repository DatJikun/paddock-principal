using Paddock.Application.Career;
using Paddock.Application.Development;
using Paddock.Application.Inbox;
using Paddock.Domain.Objectives;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Application.Racing;

/// <summary>
/// T47 race weekends in the career loop. With a track catalog it queues the season's calendar when the career opens and
/// on each 31 December, runs the weekend on a race day, and publishes the table, the money, the car kilometres and the
/// career-ending outcomes. Without a catalog it does nothing, so a fixture career that has no calendar is unchanged.
/// </summary>
public sealed class RacingModule : CareerModule
{
    public const string ModuleName = "racing";

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections =>
        [ChampionshipSection.SectionName, RaceResultsSection.SectionName, RaceCalendarSection.SectionName];

    public override void Configure(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Provide<INextRaceSource>(new ChampionshipCalendar(context.Session));
        context.Provide(new RaceWatch());
    }

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var watch = context.Require<RaceWatch>();
        context.AddDayHandler(new RaceWeekendDay(context, watch));
        if (context.TryGet<InboxResolvers>() is { } resolvers)
        {
            resolvers.Register(new StandInResolver());
        }
        if (context.TryGet<ObjectiveFactRegistry>() is { } facts)
        {
            var session = context.Session;
            var inputs = context.Inputs;
            facts.RegisterNumber(
                ObjectiveFactKeys.ChampionshipPosition,
                owner => ChampionshipFacts.Number(session, inputs, owner, ObjectiveFactKeys.ChampionshipPosition));
            facts.RegisterNumber(
                ObjectiveFactKeys.SeasonPoints,
                owner => ChampionshipFacts.Number(session, inputs, owner, ObjectiveFactKeys.SeasonPoints));
            facts.RegisterNumber(
                ObjectiveFactKeys.SeasonPodiums,
                owner => ChampionshipFacts.Number(session, inputs, owner, ObjectiveFactKeys.SeasonPodiums));
            facts.RegisterNumber(
                ObjectiveFactKeys.SeasonWins,
                owner => ChampionshipFacts.Number(session, inputs, owner, ObjectiveFactKeys.SeasonWins));
        }
    }

    public override void Open(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Inputs.Layouts is not { } layouts
            || context.Inputs.RaceAssignments is not { } assignments
            || context.Inputs.RulePeriods is null)
        {
            return;
        }

        var season = context.Session.Date.Year;
        if (context.Session.HasChampionship(season))
        {
            return;
        }

        var (plan, world) = SeasonPlans.Ensure(context.Session.World, season, layouts, assignments, context.Inputs.RaceDates);
        if (!ReferenceEquals(world, context.Session.World))
        {
            context.Session.StoreWorld(world);
        }

        context.Session.QueuePlanned(plan);
    }
}
