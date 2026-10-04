using Paddock.Application.Access;
using Paddock.Application.Cars;
using Paddock.Application.Contracts;
using Paddock.Domain.Cars;
using Paddock.Domain.Development;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using ManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Application.Development;

/// <summary>
/// One project of the manager's own team. Gain is a band of rating points, never the exact hidden number. For a concept the
/// production fields are what the principal needs to decide (T42c): a <c>Ready</c> concept shows how many days and how much money
/// committing now would take (<see cref="ProductionDays"/>, <see cref="ProductionCostCents"/>) and, when the host knows the
/// calendar, the first race it would then be live at (<see cref="GoesLiveOn"/>). An <c>InProduction</c> one shows the days left,
/// the last production day (<see cref="ProductionEnds"/>) and the first race it is live at. Own team only, no hidden numbers.
/// </summary>
public sealed record OwnProjectView(
    string ProjectId,
    string Kind,
    string? Area,
    string Engineer,
    string Status,
    int ProgressPercent,
    DateOnly ExpectedEnd,
    CarBandView ExpectedGain,
    string Timing,
    int TimingRaces,
    int RacesWaited,
    int? ProductionDays = null,
    long? ProductionCostCents = null,
    DateOnly? ProductionEnds = null,
    DateOnly? GoesLiveOn = null);

/// <summary>
/// FORECAST of the own car at the end of the season from the projects that are running and due by then. It does not guess at
/// projects the engineers have not chosen yet, so it is a floor on the plan's effect, not the plan's effect. A band, like every number.
/// </summary>
public sealed record DevelopmentForecast(
    DateOnly Until,
    CarBandView Downforce,
    CarBandView MechanicalGrip,
    CarBandView Braking,
    CarBandView Reliability);

public sealed record OwnDevelopmentView(
    string OrganizationId,
    int CurrentPercent,
    int AccountPercent,
    int NextYearPercent,
    int AeroPriority,
    int ChassisPriority,
    int ReliabilityPriority,
    int TyresPriority,
    int Headcount,
    CarBandView Account,
    IReadOnlyList<OwnProjectView> Projects,
    DevelopmentForecast Forecast,
    CarBandView OngoingGain = default,
    int? DaysToNextRace = null);

/// <remarks>
/// <see cref="OwnDevelopmentView.OngoingGain"/> is the band of rating points the running upgrades are still expected to add to the
/// current car (the sum of their bands); it uses only what T42 already has, no breakthrough mechanics. <see cref="OwnDevelopmentView.DaysToNextRace"/>
/// is null when the host gives no calendar.
/// </remarks>
/// <summary>Only the manager's own teams. Rivals' plans and projects are not in the type at all (INV-003).</summary>
public sealed record DevelopmentOverview(IReadOnlyList<OwnDevelopmentView> Own);

/// <summary>
/// What one manager or AI may see of development. Reads truth and turns it into bands through the technical director's
/// vision and the aero head's skill, as <see cref="CarQuery"/> does. Pure: no change to the world, no RNG (INV-005).
/// </summary>
public sealed class DevelopmentQuery
{
    private readonly DevelopmentBook _book;
    private readonly DevelopmentEnvironment _environment;

    public DevelopmentQuery(DevelopmentBook book, DevelopmentEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    public DevelopmentOverview View(AccessContext access)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (access.Kind == AccessKind.Developer)
        {
            throw new InvalidOperationException("A developer reads Truth, not the manager overview.");
        }

        if (access.Manager is not { } actor)
        {
            throw new InvalidOperationException("A manager view needs a manager.");
        }

        var manager = new ManagerId(actor.Value);
        var world = _book.World;
        var today = new GameDate(world.CurrentDate.Year, world.CurrentDate.Month, world.CurrentDate.Day);
        var own = new List<OwnDevelopmentView>();
        foreach (var organization in DevelopmentEngine.OrganizationsWithCars(_book.Cars))
        {
            if (_environment.Control.Controls(manager, organization))
            {
                own.Add(Own(organization, world, today));
            }
        }

        return new DevelopmentOverview(own);
    }

    private OwnDevelopmentView Own(OrganizationId organization, WorldState world, GameDate today)
    {
        var section = _book.Section;
        var plan = section.PlanOf(organization) ?? DevelopmentPlan.Default(organization);
        var vision = TeamEngineers.Attribute(world, organization, today, StaffRole.TechnicalDirector, "vision");
        var aero = TeamEngineers.Attribute(world, organization, today, StaffRole.HeadOfAerodynamics, "aerodynamics");
        var skill = (Math.Clamp(vision, 1, 20) + Math.Clamp(aero, 1, 20) - 2d) / 38d;
        var cars = _book.Cars.Of(organization);
        var annual = (long)DevelopmentMath.AnnualBudgetCents(_book.Finance.TypicalCents);
        var balance = _book.Finance.HasBook(organization) ? _book.Finance.BalanceOf(organization) : 0L;
        var capacity = EngineeringCapacity.Derive(
            EngineerRoster.Of(world, organization, today),
            today.Year,
            EngineerRoster.ChairsIn(today.Year),
            balance,
            annual);
        var projects = section.ProjectsOf(organization)
            .Where(project => project.Status is ProjectStatus.Active or ProjectStatus.Ready or ProjectStatus.InProduction)
            .Select(project => ProjectView(project, organization, world, cars, skill, today))
            .ToArray();
        var running = projects.Where(project => project.Status == nameof(ProjectStatus.Active) && project.Kind == nameof(DevKind.Upgrade)).ToArray();
        var ongoing = new CarBandView(
            CarEstimates.ClampRating(running.Sum(project => project.ExpectedGain.Low)),
            CarEstimates.ClampRating(running.Sum(project => project.ExpectedGain.High)));
        var next = _environment.Races?.NextRaceOnOrAfter(organization, today);
        var stock = section.AccountOf(organization).StockMilli / 1000d;
        return new OwnDevelopmentView(
            organization.Value,
            plan.CurrentPercent,
            plan.AccountPercent,
            plan.NextYearPercent,
            plan.AeroPriority,
            plan.ChassisPriority,
            plan.ReliabilityPriority,
            plan.TyresPriority,
            capacity.Headcount,
            Band(stock, vision, aero),
            projects,
            Forecast(section.ProjectsOf(organization), cars, vision, aero, today),
            ongoing,
            next is { } race ? today.DaysUntil(race) : null);
    }

    private OwnProjectView ProjectView(DevProject project, OrganizationId organization, WorldState world, IReadOnlyList<TeamCar> cars, double skill, GameDate today)
    {
        var reference = cars.Count > 0 ? cars[0] : null;
        var headroom = reference is null || project.Area is not { } area
            ? (reference is null ? 0d : MeanHeadroom(reference))
            : DevelopmentMath.Headroom(reference, area);
        var expected = headroom * project.ShareMilli / 1000d;
        var half = 0.6d - (0.45d * skill);
        var left = Math.Max(0, project.DurationDays - project.ProgressDays);
        int? productionDays = null;
        long? productionCost = null;
        DateOnly? productionEnds = null;
        DateOnly? goesLive = null;
        if (project.Kind == DevKind.Concept && project.Status == ProjectStatus.Ready)
        {
            var plan = ConceptProduction.Plan(world, _book.Finance, organization, project, today);
            productionDays = plan.Days;
            productionCost = plan.CostCents;
            goesLive = LiveOn(organization, today.AddDays(plan.Days));
        }
        else if (project.IsInProduction && project.ProductionEnds is { } ends)
        {
            left = Math.Max(0, today.DaysUntil(ends));
            productionDays = left;
            productionCost = project.ProductionCostCents;
            productionEnds = ToDateOnly(ends);
            goesLive = LiveOn(organization, ends);
        }

        return new OwnProjectView(
            project.Id,
            project.Kind.ToString(),
            project.Area?.ToString(),
            project.Engineer,
            project.Status.ToString(),
            (int)Math.Round(project.Progress * 100d, MidpointRounding.AwayFromZero),
            ToDateOnly(today.AddDays(left)),
            new CarBandView(CarEstimates.ClampRating(expected * (1d - half)), CarEstimates.ClampRating(expected * (1d + half))),
            project.Timing.ToString(),
            project.TimingRaces,
            project.RacesWaited,
            productionDays,
            productionCost,
            productionEnds,
            goesLive);
    }

    /// <summary>The first race after the last production day, or null when the host gives no calendar.</summary>
    private DateOnly? LiveOn(OrganizationId organization, GameDate lastProductionDay) =>
        _environment.Races?.NextRaceOnOrAfter(organization, lastProductionDay.AddDays(1)) is { } race ? ToDateOnly(race) : null;

    private static DevelopmentForecast Forecast(
        IReadOnlyList<DevProject> projects,
        IReadOnlyList<TeamCar> cars,
        int vision,
        int aero,
        GameDate today)
    {
        var end = GameDate.SeasonEnd(today.Year);
        var until = ToDateOnly(end);
        if (cars.Count == 0)
        {
            var none = new CarBandView(0, 0);
            return new DevelopmentForecast(until, none, none, none, none);
        }

        var car = cars[0];
        var levels = car.Levels;
        var due = projects
            .Where(project => project.IsActive && project.Kind == DevKind.Upgrade && project.Area is not null
                && today.AddDays(project.DurationDays - project.ProgressDays) <= end)
            .OrderBy(project => project.DurationDays - project.ProgressDays)
            .ThenBy(project => project.Number);
        foreach (var project in due)
        {
            var area = project.Area!.Value;
            var full = DevelopmentMath.LevelOf(ConceptMapping.Effects(car.Concept, car.ConceptCeiling).Full, area);
            var headroom = Math.Max(0d, full - DevelopmentMath.LevelOf(levels, area));
            levels = DevelopmentMath.WithLevel(
                levels,
                area,
                DevelopmentMath.LevelOf(levels, area) + DevelopmentMath.Gain(headroom, project.ShareMilli / 1000d));
        }

        return new DevelopmentForecast(
            until,
            Band(levels.Downforce, vision, aero),
            Band(levels.MechanicalGrip, vision, aero),
            Band(levels.Braking, vision, aero),
            Band(levels.Reliability, vision, aero));
    }

    private static double MeanHeadroom(TeamCar car) =>
        Enum.GetValues<DevArea>().Sum(area => DevelopmentMath.Headroom(car, area)) / 4d;

    private static CarBandView Band(double truth, int vision, int aero)
    {
        var band = CarKnowledgeBands.Around(truth, vision, aero);
        return new CarBandView(band.Low, band.High);
    }

    private static DateOnly ToDateOnly(GameDate date) => new(date.Year, date.Month, date.Day);
}
