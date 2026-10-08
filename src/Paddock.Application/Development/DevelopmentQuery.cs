using System.Globalization;
using Paddock.Application.Access;
using Paddock.Application.Cars;
using Paddock.Application.Contracts;
using Paddock.Application.Infrastructure;
using Paddock.Domain.Cars;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using ManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Application.Development;

/// <summary>
/// One part the engineers are building for the car that races. Gain is a band of rating points, never the exact hidden number.
/// The production fields belong to a concept project and are kept for the old callers; the v2 screens read
/// <see cref="NextConceptView"/> for the concept. Own team only, no hidden numbers (INV-003).
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

/// <summary>A rival the grid comparison shows: a public range of its level in one area (PP-066: bands only, from what the paddock can see).</summary>
public sealed record RivalBandView(string OrganizationId, string Name, CarBandView Band);

/// <summary>
/// One area of the car (<c>Engine</c>, <c>Aero</c>, <c>Handling</c>, <c>Reliability</c>, plus <c>Total</c> for the whole car): the own
/// range from the technical staff's estimate, and the same area of the top rivals.
/// </summary>
public sealed record AreaView(string Area, CarBandView Own, IReadOnlyList<RivalBandView> Rivals);

/// <summary>The concept in the car: its name ("Maserati 56"), the year it came in, its character, and a range for its ceiling.</summary>
public sealed record CurrentConceptView(string Name, int Year, int PhilosophyMilli, int AeroMilli, CarBandView Ceiling);

/// <summary>
/// What a character of the next concept would bring, as numbers: the share of its own ceiling it starts at, the range of that ceiling,
/// and the range of the level it would start at.
/// </summary>
public sealed record CharacterOptionView(string Id, int PhilosophyMilli, int StartSharePercent, CarBandView Ceiling, CarBandView StartLevel);

/// <summary>An aero direction and what it does on a straight and in a corner, in percent of performance.</summary>
public sealed record AeroOptionView(string Id, int AeroMilli, int StraightsPercent, int CornersPercent);

/// <summary>
/// The decision a finished concept asks of the principal, with numbers: the range of its ceiling against the ceiling now, the gain as a
/// range, the range of the level it would start at against the level now, how long building takes, what it costs, and the first race
/// with it (null when the host gives no calendar).
/// </summary>
public sealed record ConceptDecisionView(
    string ProjectId,
    string Name,
    CarBandView Ceiling,
    CarBandView CeilingNow,
    CarBandView Gain,
    CarBandView StartLevel,
    CarBandView LevelNow,
    int BuildDays,
    long CostCents,
    DateOnly? FirstRace,
    bool Breakthrough,
    string Timing = "Hold");

/// <summary>
/// The next concept. <see cref="SharePercent"/> is the slider (how the people are split between the car that races and the next one),
/// <see cref="PhilosophyMilli"/> and <see cref="AeroMilli"/> the character chosen. <see cref="Status"/> is <c>None</c>, <c>Active</c>,
/// <c>Ready</c> or <c>InProduction</c>. <see cref="Decision"/> is set while a finished concept waits.
/// </summary>
public sealed record NextConceptView(
    int SharePercent,
    int PhilosophyMilli,
    int AeroMilli,
    IReadOnlyList<CharacterOptionView> Characters,
    IReadOnlyList<AeroOptionView> Aeros,
    string? ProjectId,
    string Status,
    int ProgressPercent,
    DateOnly? ReadyOn,
    DateOnly? GoesLiveOn,
    ConceptDecisionView? Decision);

/// <summary>One line of what the team learned about its car (<c>race</c>, <c>test</c>, <c>part</c>, <c>concept</c>), in understanding points.</summary>
public sealed record UnderstandingNoteView(DateOnly On, string Source, double Points);

/// <summary>How well the team knows its car, as a range, and the latest changes. Rises with tests and race kilometres; falls with a new part or concept.</summary>
public sealed record UnderstandingView(CarBandView Level, IReadOnlyList<UnderstandingNoteView> Notes);

/// <summary>What the manager sees of the own team's development (PP-066).</summary>
public sealed record OwnDevelopmentView(
    string OrganizationId,
    int Headcount,
    CurrentConceptView Concept,
    IReadOnlyList<AreaView> Areas,
    NextConceptView Next,
    UnderstandingView Understanding,
    IReadOnlyList<OwnProjectView> Projects,
    int? DaysToNextRace = null);

/// <summary>Only the manager's own teams. Rivals' plans and projects are not in the type at all (INV-003).</summary>
public sealed record DevelopmentOverview(IReadOnlyList<OwnDevelopmentView> Own);

/// <summary>
/// What one manager or AI may see of development. Reads truth and turns it into bands through the technical director's
/// vision and the aero head's skill, as <see cref="CarQuery"/> does; rivals come only as wider public bands. Pure: no change to the
/// world, no RNG (INV-005).
/// </summary>
public sealed class DevelopmentQuery
{
    public const string AreaEngine = "Engine";

    public const string AreaAero = "Aero";

    public const string AreaHandling = "Handling";

    public const string AreaReliability = "Reliability";

    public const string AreaTotal = "Total";

    public static readonly IReadOnlyList<string> Areas = [AreaEngine, AreaAero, AreaHandling, AreaReliability, AreaTotal];

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
                own.Add(Own(organization, world, today).View);
            }
        }

        return new DevelopmentOverview(own);
    }

    /// <summary>The one team this manager runs, or null when they do not run it. Same view as <see cref="View"/> for that team.</summary>
    internal OwnedDevelopment? ViewOf(AccessContext access, OrganizationId organization)
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
        if (!_environment.Control.Controls(manager, organization))
        {
            return null;
        }

        var world = _book.World;
        var today = new GameDate(world.CurrentDate.Year, world.CurrentDate.Month, world.CurrentDate.Day);
        return Own(organization, world, today);
    }

    /// <summary>
    /// The same readout for one team on a day, whoever is asking: the day handler uses it to write inbox items for the team's
    /// human principals. It reads the world given (a step may not have been written yet) and returns bands only.
    /// </summary>
    internal static OwnDevelopmentView ReadOut(
        WorldState world,
        CarsSection cars,
        FinanceSection finance,
        DevelopmentSection section,
        INextRaceSource? races,
        OrganizationId organization,
        GameDate today) =>
        DevelopmentReadout.Own(world, cars, finance, section, races, organization, today).View;

    private OwnedDevelopment Own(OrganizationId organization, WorldState world, GameDate today) =>
        DevelopmentReadout.Own(world, _book.Cars, _book.Finance, _book.Section, _environment.Races, organization, today);

    internal readonly record struct OwnedDevelopment(OwnDevelopmentView View, int Vision, int Aero);
}

/// <summary>Turns the truth of one team into the view its principal may see. Pure; one place for the player, the AI and the inbox.</summary>
internal static class DevelopmentReadout
{
    public static DevelopmentQuery.OwnedDevelopment Own(
        WorldState world,
        CarsSection allCars,
        FinanceSection finance,
        DevelopmentSection section,
        INextRaceSource? races,
        OrganizationId organization,
        GameDate today)
    {
        var plan = section.PlanOf(organization) ?? DevelopmentPlan.Default(organization);
        var staff = EngineerRoster.Read(world, organization, today);
        var vision = staff.Vision;
        var aero = staff.Aero;
        var skill = (Math.Clamp(vision, 1, 20) + Math.Clamp(aero, 1, 20) - 2d) / 38d;
        var cars = allCars.Of(organization);
        var annual = (long)DevelopmentMath.AnnualBudgetCents(finance.TypicalCents);
        var balance = finance.HasBook(organization) ? finance.BalanceOf(organization) : 0L;
        var capacity = InfrastructureEffect.Apply(
            EngineeringCapacity.Derive(staff.Engineers, today.Year, EngineerRoster.ChairsIn(today.Year), balance, annual),
            world,
            organization,
            today.Year);
        var key = organization.Value + "|" + today.Year.ToString(CultureInfo.InvariantCulture);
        var open = section.OpenOf(organization);
        var next = races?.NextRaceOnOrAfter(organization, today);
        var parts = open
            .Where(project => project.Kind != DevKind.Concept && project.Status == ProjectStatus.Active)
            .Select(project => PartView(project, organization, cars, skill, today))
            .ToArray();
        var days = next is { } race ? today.DaysUntil(race) : (int?)null;
        if (cars.Count == 0)
        {
            var none = new CarBandView(0, 0);
            var empty = new OwnDevelopmentView(
                organization.Value,
                capacity.Headcount,
                new CurrentConceptView(string.Empty, 0, 0, 0, none),
                [],
                new NextConceptView(plan.NextYearPercent, plan.NextPhilosophyMilli, plan.NextAeroMilli, [], [], null, "None", 0, null, null, null),
                new UnderstandingView(none, []),
                parts,
                days);
            return new DevelopmentQuery.OwnedDevelopment(empty, vision, aero);
        }

        var car = cars[0];
        var half = Half(skill);
        var rivals = TopRivals(world, allCars, organization, today, half);
        var areas = new List<AreaView>(DevelopmentQuery.Areas.Count);
        foreach (var area in DevelopmentQuery.Areas)
        {
            areas.Add(new AreaView(
                area,
                Band(Truth(car.Levels, area), half, key + "|" + area),
                rivals.Select(rival => new RivalBandView(
                    rival.Organization.Value,
                    rival.Name,
                    Band(Truth(rival.Levels, area), half + DevelopmentEstimates.RivalBandExtra, rival.Organization.Value + "|" + area + "|" + today.Year.ToString(CultureInfo.InvariantCulture))))
                    .ToArray()));
        }

        var account = section.AccountOf(organization);
        var year = account.ConceptYear == 0 ? car.Season : account.ConceptYear;
        var ceilingNow = Band(car.ConceptCeiling, half, key + "|ceiling");
        var concept = new CurrentConceptView(ConceptName(world, organization, today, year), year, CarEstimates.Milli(car.Concept.Philosophy), CarEstimates.Milli(car.Concept.Aero), ceilingNow);
        var levelNow = Band(DevelopmentMath.Overall(car.Levels), half, key + "|" + DevelopmentQuery.AreaTotal);
        var quality = TeamEngineers.ExecutionQuality(world, organization, today);
        var characters = new[] { -1000, 1000 }
            .Select(philosophy => Character(philosophy, plan.NextAeroMilli, car, quality, half, key))
            .ToArray();
        var aeros = new[] { ("Straights", -DevelopmentEstimates.AeroPresetMilli), ("Balanced", 0), ("Corners", DevelopmentEstimates.AeroPresetMilli) }
            .Select(preset =>
            {
                var (straights, corners) = DevelopmentMath.AeroEffectPercent(preset.Item2);
                return new AeroOptionView(preset.Item1, preset.Item2, straights, corners);
            })
            .ToArray();
        var next0 = NextConcept(world, finance, section, races, organization, plan, open, car, concept, ceilingNow, levelNow, half, key, today, characters, aeros);
        var notes = section.NotesOf(organization)
            .Reverse()
            .Take(5)
            .Select(note => new UnderstandingNoteView(new DateOnly(note.On.Year, note.On.Month, note.On.Day), note.Source, note.DeltaMilli / 1000d))
            .ToArray();
        var view = new OwnDevelopmentView(
            organization.Value,
            capacity.Headcount,
            concept,
            areas,
            next0,
            new UnderstandingView(Band(car.Understanding, half, key + "|understanding"), notes),
            parts,
            days);
        return new DevelopmentQuery.OwnedDevelopment(view, vision, aero);
    }

    private static NextConceptView NextConcept(
        WorldState world,
        FinanceSection finance,
        DevelopmentSection section,
        INextRaceSource? races,
        OrganizationId organization,
        DevelopmentPlan plan,
        IReadOnlyList<DevProject> open,
        TeamCar car,
        CurrentConceptView concept,
        CarBandView ceilingNow,
        CarBandView levelNow,
        double half,
        string key,
        GameDate today,
        IReadOnlyList<CharacterOptionView> characters,
        IReadOnlyList<AeroOptionView> aeros)
    {
        _ = section;
        var project = open.FirstOrDefault(item => item.Kind == DevKind.Concept && item.Status is ProjectStatus.Ready or ProjectStatus.InProduction)
            ?? open.FirstOrDefault(item => item.Kind == DevKind.Concept && item.Status == ProjectStatus.Active);
        if (project is null)
        {
            return new NextConceptView(plan.NextYearPercent, plan.NextPhilosophyMilli, plan.NextAeroMilli, characters, aeros, null, "None", 0, null, null, null);
        }

        DateOnly? readyOn = project.Status == ProjectStatus.Active
            ? ToDateOnly(today.AddDays(Math.Max(0, project.DurationDays - project.ProgressDays)))
            : null;
        DateOnly? live = null;
        ConceptDecisionView? decision = null;
        if (project.Status == ProjectStatus.Ready)
        {
            var plan2 = ConceptProduction.Plan(world, finance, organization, project, today);
            var firstRace = FirstRace(races, organization, today.AddDays(plan2.Days));
            decision = Decision(project, car, concept, ceilingNow, levelNow, half, key, plan2, firstRace, today.AddDays(plan2.Days).Year);
        }
        else if (project.IsInProduction && project.ProductionEnds is { } ends)
        {
            readyOn = ToDateOnly(ends);
            live = FirstRace(races, organization, ends);
        }

        return new NextConceptView(
            plan.NextYearPercent,
            plan.NextPhilosophyMilli,
            plan.NextAeroMilli,
            characters,
            aeros,
            project.Id,
            project.Status.ToString(),
            (int)Math.Round(project.Progress * 100d, MidpointRounding.AwayFromZero),
            readyOn,
            live,
            decision);
    }

    private static DateOnly? FirstRace(INextRaceSource? races, OrganizationId organization, GameDate lastProductionDay) =>
        races?.NextRaceOnOrAfter(organization, lastProductionDay.AddDays(1)) is { } race ? ToDateOnly(race) : null;

    private static ConceptDecisionView Decision(
        DevProject project,
        TeamCar car,
        CurrentConceptView concept,
        CarBandView ceilingNow,
        CarBandView levelNow,
        double half,
        string key,
        ProductionPlan production,
        DateOnly? firstRace,
        int liveYear)
    {
        var ceiling = project.CeilingMilli / 1000d;
        var ceilingBand = Band(ceiling, half, key + "|new|" + project.Id);
        var redesigned = DevelopmentMath.Redesigned(car.Concept, project.PhilosophyMilli, project.AeroMilli);
        var start = DevelopmentMath.Overall(DevelopmentMath.StartLevels(redesigned, ceiling, project.PhilosophyMilli));
        var startBand = Band(start, half, key + "|start|" + project.Id);
        var mid = ((ceilingBand.Low + ceilingBand.High) / 2d) - ((ceilingNow.Low + ceilingNow.High) / 2d);
        var spread = ((ceilingBand.High - ceilingBand.Low) + (ceilingNow.High - ceilingNow.Low)) / 4d;
        return new ConceptDecisionView(
            project.Id,
            ConceptNameFor(concept.Name, liveYear),
            ceilingBand,
            ceilingNow,
            new CarBandView(CarEstimates.Quantize(mid - spread), CarEstimates.Quantize(mid + spread)),
            startBand,
            levelNow,
            production.Days,
            production.CostCents,
            firstRace,
            project.IsBreakthrough,
            project.Timing.ToString());
    }

    /// <summary>The concept's name once it goes in: the team's name and the year it will come in. The current name's team part is reused.</summary>
    private static string ConceptNameFor(string currentName, int liveYear)
    {
        var cut = currentName.LastIndexOf(' ');
        var team = cut > 0 ? currentName[..cut] : currentName;
        return team + " " + (liveYear % 100).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>The name of the concept a team races: its name and the two digits of the year it came in. "Maserati 56", never an id.</summary>
    internal static string ConceptName(WorldState world, OrganizationId organization, GameDate today, int year)
    {
        var team = organization.Value;
        foreach (var candidate in world.Organizations)
        {
            if (candidate.Id == organization)
            {
                team = candidate.NameOn(today);
                break;
            }
        }

        return team + " " + (year % 100).ToString("00", CultureInfo.InvariantCulture);
    }

    private static CharacterOptionView Character(int philosophy, int aeroMilli, TeamCar car, double quality, double half, string key)
    {
        var expected = DevelopmentMath.ExpectedCeiling(car.ConceptCeiling, philosophy, quality);
        var sd = DevelopmentMath.CeilingSpread(philosophy, CarEstimates.DefaultStaffAttribute);
        var start = DevelopmentMath.StartFraction(philosophy);
        var redesigned = DevelopmentMath.Redesigned(car.Concept, philosophy, aeroMilli);
        var startLevel = DevelopmentMath.Overall(DevelopmentMath.StartLevels(redesigned, expected, philosophy));
        var id = philosophy < 0 ? "Evolution" : "Revolution";
        return new CharacterOptionView(
            id,
            philosophy,
            (int)Math.Round(start * 100d, MidpointRounding.AwayFromZero),
            Band(expected, half + sd, key + "|char|" + id),
            Band(startLevel, half + (sd * start), key + "|charstart|" + id));
    }

    private static OwnProjectView PartView(DevProject project, OrganizationId organization, IReadOnlyList<TeamCar> cars, double skill, GameDate today)
    {
        var reference = cars.Count > 0 ? cars[0] : null;
        var headroom = reference is null || project.Area is not { } area
            ? 0d
            : DevelopmentMath.Headroom(reference, area);
        var expected = headroom * project.ShareMilli / 1000d;
        var halfFraction = 0.6d - (0.45d * skill);
        var left = Math.Max(0, project.DurationDays - project.ProgressDays);
        return new OwnProjectView(
            project.Id,
            project.Kind.ToString(),
            project.Area?.ToString(),
            project.Engineer,
            project.Status.ToString(),
            (int)Math.Round(project.Progress * 100d, MidpointRounding.AwayFromZero),
            ToDateOnly(today.AddDays(left)),
            GainBand(expected, halfFraction, organization.Value + "|gain|" + project.Id),
            project.Timing.ToString(),
            project.TimingRaces,
            project.RacesWaited);
    }

    /// <summary>Half-width of the own band for a staff skill of 0 to 1: the better the technical staff, the narrower the range.</summary>
    internal static double Half(double skill) =>
        CarEstimates.MaxHalfWidth + ((CarEstimates.MinHalfWidth - CarEstimates.MaxHalfWidth) * Math.Clamp(skill, 0d, 1d));

    internal static double Truth(PerformanceLevels levels, string area) => area switch
    {
        DevelopmentQuery.AreaEngine => levels.Power,
        DevelopmentQuery.AreaAero => levels.Downforce,
        DevelopmentQuery.AreaHandling => (levels.MechanicalGrip + levels.Braking) / 2d,
        DevelopmentQuery.AreaReliability => levels.Reliability,
        _ => DevelopmentMath.Overall(levels),
    };

    internal static CarBandView Band(double truth, double half, string biasKey)
    {
        var band = CarKnowledgeBands.OfHalfWidth(truth, half, biasKey);
        return new CarBandView(band.Low, band.High);
    }

    private static CarBandView GainBand(double expected, double halfFraction, string biasKey)
    {
        var band = CarKnowledgeBands.OfHalfWidth(expected, Math.Abs(expected) * halfFraction, biasKey);
        return new CarBandView(band.Low, band.High);
    }

    private sealed record Rival(OrganizationId Organization, string Name, PerformanceLevels Levels);

    /// <summary>
    /// The three best other teams as the paddock sees them: by the points of the latest season with results (public), and before any
    /// result by the middle of their public overall band. Never by the hidden numbers themselves.
    /// </summary>
    private static List<Rival> TopRivals(WorldState world, CarsSection cars, OrganizationId own, GameDate today, double half)
    {
        var ranked = PublicGrid.Ranking(world, today.Year);
        var candidates = new List<(OrganizationId Organization, PerformanceLevels Levels, string Name)>();
        foreach (var organization in DevelopmentEngine.OrganizationsWithCars(cars))
        {
            if (organization == own || cars.Of(organization) is not { Count: > 0 } theirs)
            {
                continue;
            }

            var name = organization.Value;
            foreach (var candidate in world.Organizations)
            {
                if (candidate.Id == organization)
                {
                    name = candidate.NameOn(today);
                    break;
                }
            }

            candidates.Add((organization, theirs[0].Levels, name));
        }

        IEnumerable<(OrganizationId Organization, PerformanceLevels Levels, string Name)> ordered;
        if (ranked.Count > 0)
        {
            ordered = candidates
                .OrderBy(item => ranked.IndexOf(item.Organization.Value) is var index && index >= 0 ? index : int.MaxValue)
                .ThenBy(item => item.Organization.Value, StringComparer.Ordinal);
        }
        else
        {
            ordered = candidates
                .OrderByDescending(item => PublicMid(item.Levels, item.Organization, today, half))
                .ThenBy(item => item.Organization.Value, StringComparer.Ordinal);
        }

        return ordered.Take(DevelopmentEstimates.TopRivals).Select(item => new Rival(item.Organization, item.Name, item.Levels)).ToList();
    }

    private static double PublicMid(PerformanceLevels levels, OrganizationId organization, GameDate today, double half)
    {
        var band = Band(DevelopmentMath.Overall(levels), half + DevelopmentEstimates.RivalBandExtra, organization.Value + "|" + DevelopmentQuery.AreaTotal + "|" + today.Year.ToString(CultureInfo.InvariantCulture));
        return (band.Low + band.High) / 2d;
    }

    private static DateOnly ToDateOnly(GameDate date) => new(date.Year, date.Month, date.Day);
}
