using Paddock.Domain.Cars;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Development;

/// <summary>Everything one development step reads. It names no manager: the same step runs for the player's team and for AI teams.</summary>
public sealed record DevelopmentInputs(
    WorldState World,
    CarsSection Cars,
    DevelopmentSection Development,
    FinanceSection Finance,
    GameDate Today,
    ulong MasterSeed,
    IDevelopmentRules Rules,
    ITraceSink Trace);

/// <summary>What one step changed. <see cref="Changed"/> is false when nothing did, so a host can skip the write.</summary>
public sealed record DevelopmentOutcome(CarsSection Cars, DevelopmentSection Development, FinanceSection Finance, bool Changed);

/// <summary>
/// Path A of PP-043 as a pure service: for each team with cars, one day of development. The principal's only input is the
/// <see cref="DevelopmentPlan"/>; the engineers choose the projects (<see cref="EngineerChoice"/>), the projects run, cost money
/// through the ledger, and on completion move the T41 car levels. No manager, no player, no host: a function of its inputs.
/// <para>
/// Randomness: execution noise and failure come from the <c>Development</c> stream, a child keyed by the project id, so no
/// other draw can shift it (INV-004). A tie between two equally good proposals is broken by the <c>AiDecisions</c> stream, a
/// child keyed by team, day and slot. Nothing here draws from a shared, advancing stream.
/// </para>
/// </summary>
public static class DevelopmentEngine
{
    /// <summary>
    /// The tag of the <c>Development</c> child that draws a project's noise and failure. Built from the team and what the project
    /// is, never from the global project number, so another team's projects cannot shift this team's draws (INV-004).
    /// </summary>
    public static string OutcomeKey(DevProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return "dev-outcome|" + project.Organization.Value + "|" + project.Started + "|" + project.Kind + "|"
            + (project.Area is { } area ? area.ToString() : "-") + "|" + project.Engineer;
    }

    public static DevelopmentOutcome Step(DevelopmentInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var cars = inputs.Cars;
        var development = inputs.Development;
        var finance = inputs.Finance;
        var today = inputs.Today;
        var era = inputs.Rules.Era(today.Year);
        var changed = false;
        foreach (var organization in OrganizationsWithCars(cars))
        {
            var state = new TeamDay(organization, cars, development, finance, inputs, era);
            state.Run();
            if (!state.Changed)
            {
                continue;
            }

            changed = true;
            cars = state.Cars;
            development = state.Development;
            finance = state.Finance;
        }

        return new DevelopmentOutcome(cars, development, finance, changed);
    }

    /// <summary>
    /// A finished race: every car that ran adds understanding from the race and its kilometres (capped by the era's testing
    /// rules), and every finished concept that waits for "after N races" counts one more race.
    /// </summary>
    public static DevelopmentOutcome RaceFinished(DevelopmentInputs inputs, IReadOnlyList<(string CarId, int Kilometres)> runs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(runs);
        var cap = inputs.Rules.Era(inputs.Today.Year).UnderstandingCap;
        var cars = inputs.Cars;
        var development = inputs.Development;
        var changed = false;
        var organizations = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (carId, kilometres) in runs.OrderBy(run => run.CarId, StringComparer.Ordinal))
        {
            if (cars.Find(carId) is not { } car)
            {
                continue;
            }

            var points = DevelopmentEstimates.UnderstandingPerRace
                + (DevelopmentEstimates.UnderstandingPerHundredKm * Math.Max(0, kilometres) / 100d);
            var grown = DevelopmentMath.GrowUnderstanding(car.Understanding, points, cap);
            if (grown != car.Understanding)
            {
                cars = cars.Replace(Restamp(car, car.Levels, grown));
                changed = true;
            }

            organizations.Add(car.Organization.Value);
        }

        foreach (var project in development.Projects)
        {
            if (project.Status == ProjectStatus.Ready
                && project.Timing == ConceptTiming.AfterRaces
                && organizations.Contains(project.Organization.Value))
            {
                development = development.ReplaceProject(project with { RacesWaited = project.RacesWaited + 1 });
                changed = true;
            }
        }

        return new DevelopmentOutcome(cars, development, inputs.Finance, changed);
    }

    /// <summary>
    /// Closes an active project early for a proportional share of its effect and a proportional cost. A concept closed early
    /// becomes ready to deploy with the smaller outcome. The caller has already checked who may do this.
    /// </summary>
    public static DevelopmentOutcome Cut(DevelopmentInputs inputs, string projectId)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var project = inputs.Development.Find(projectId);
        if (project is null || !project.IsActive)
        {
            throw new InvalidOperationException("Only an active project can be cut.");
        }

        var state = new TeamDay(project.Organization, inputs.Cars, inputs.Development, inputs.Finance, inputs, inputs.Rules.Era(inputs.Today.Year));
        state.Cut(project);
        return new DevelopmentOutcome(state.Cars, state.Development, state.Finance, true);
    }

    /// <summary>
    /// Commits a finished (ready) concept to production: the cost is posted now, the old car keeps racing, and the concept goes
    /// live on the day after production ends. The caller has already checked who may do this.
    /// </summary>
    public static DevelopmentOutcome StartProduction(DevelopmentInputs inputs, string projectId)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var project = inputs.Development.Find(projectId);
        if (project is null || project.Kind != DevKind.Concept || project.Status != ProjectStatus.Ready)
        {
            throw new InvalidOperationException("Only a ready concept can be committed to production.");
        }

        var state = new TeamDay(project.Organization, inputs.Cars, inputs.Development, inputs.Finance, inputs, inputs.Rules.Era(inputs.Today.Year));
        state.StartProductionNow(project);
        return new DevelopmentOutcome(state.Cars, state.Development, state.Finance, true);
    }

    /// <summary>Deploys a finished (ready) concept now, whatever its timing, with no production time. Superseded by <see cref="StartProduction"/>; to be retired.</summary>
    public static DevelopmentOutcome DeployNow(DevelopmentInputs inputs, string projectId)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var project = inputs.Development.Find(projectId);
        if (project is null || project.Status != ProjectStatus.Ready)
        {
            throw new InvalidOperationException("Only a ready concept can be deployed.");
        }

        var state = new TeamDay(project.Organization, inputs.Cars, inputs.Development, inputs.Finance, inputs, inputs.Rules.Era(inputs.Today.Year));
        state.DeployNow(project);
        return new DevelopmentOutcome(state.Cars, state.Development, state.Finance, true);
    }

    public static IReadOnlyList<OrganizationId> OrganizationsWithCars(CarsSection cars)
    {
        ArgumentNullException.ThrowIfNull(cars);
        return cars.Cars
            .Select(car => car.Organization)
            .Distinct()
            .OrderBy(organization => organization.Value, StringComparer.Ordinal)
            .ToArray();
    }

    internal static TeamCar Restamp(TeamCar car, PerformanceLevels levels, double understanding) =>
        car.WithDesign(car.Season, car.Concept, levels, car.ConceptCeiling, understanding, car.TyreWearMultiplier, car.SupplierChangeCost);

}
