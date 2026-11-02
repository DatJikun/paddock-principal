using System.Collections.Immutable;
using Paddock.Application.Career;
using Paddock.Domain.Racing;
using Paddock.Simulation.Career;
using Paddock.Simulation.Racing.Pits;

namespace Paddock.Application.Racing;

/// <summary>
/// The world's <see cref="RaceWeekendSection"/> as the race engine reads it (#316): plans as <see cref="RacePlan"/>, orders as
/// <see cref="IssuedOrder"/>. Pure mapping; the names of the modes are the enum names.
/// </summary>
public static class RaceWeekendState
{
    /// <summary>The section of round <paramref name="round"/> of <paramref name="season"/>, or null when the world holds another or none.</summary>
    public static RaceWeekendSection? Of(CareerSession session, int season, int round)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.World.Section<RaceWeekendSection>(RaceWeekendSection.SectionName) is { } section
            && section.Season == season && section.Round == round
                ? section
                : null;
    }

    public static ImmutableArray<RacePlan> Plans(RaceWeekendSection? section) =>
        section is null ? [] : [.. section.Plans.Select(ToPlan)];

    public static ImmutableArray<IssuedOrder> Orders(RaceWeekendSection? section) =>
        section is null ? [] : [.. section.Orders.Select(order => new IssuedOrder(order.ManagerId, order.AtMs, ToOrder(order)))];

    public static RacePlan ToPlan(WeekendPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new RacePlan(
            plan.CarId,
            plan.StartCompoundId,
            plan.Pace is { } pace ? Enum.Parse<PaceMode>(pace) : null,
            Enum.Parse<EngineMode>(plan.Engine),
            plan.Stops is { } stops ? [.. stops.Select(stop => new PlannedStop(stop.Lap, stop.CompoundId))] : null);
    }

    public static WeekendPlan FromPlan(RacePlan plan, string teamId)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new WeekendPlan(
            plan.CarId,
            teamId,
            plan.StartCompoundId,
            plan.Pace?.ToString(),
            plan.Engine.ToString(),
            plan.Stops is { } stops ? [.. stops.Select(stop => new WeekendPlannedStop(stop.Lap, stop.CompoundId))] : null);
    }

    public static PitWallOrder ToOrder(WeekendOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new PitWallOrder(
            order.CarId,
            order.Lap,
            Enum.Parse<PitWallOrderKind>(order.Kind),
            Enum.Parse<PaceMode>(order.Pace),
            order.CompoundId,
            Enum.Parse<EngineMode>(order.Engine),
            order.On);
    }

    public static WeekendOrder FromOrder(IssuedOrder issued)
    {
        ArgumentNullException.ThrowIfNull(issued);
        var order = issued.Order;
        return new WeekendOrder(
            issued.ManagerId,
            issued.AtMs,
            order.CarId,
            order.Lap,
            order.Kind.ToString(),
            order.Pace.ToString(),
            order.CompoundId,
            order.Engine.ToString(),
            order.On);
    }

    /// <summary>Stores <paramref name="section"/> as the world's race weekend.</summary>
    internal static void Store(CareerModuleContext context, RaceWeekendSection section) =>
        context.Session.StoreWorld(context.Session.World.WithSection(section));
}
