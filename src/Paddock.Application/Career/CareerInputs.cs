using Paddock.Application.Sponsors;
using Paddock.Domain.Board;
using Paddock.Domain.Cars;
using Paddock.Domain.Career;
using Paddock.Domain.Contracts;
using Paddock.Domain.Finance;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Supply;
using Paddock.Domain.World;

namespace Paddock.Application.Career;

/// <summary>
/// The data a career run reads from outside the world: authored eras, the fictional sponsors, and the facts that decide how rich
/// a team starts. The host (SimRunner, later the desktop app) loads them from <c>data/authored</c>; this assembly does not load
/// files. A module whose input is missing does nothing, so a test or a tool that has no such data still runs the rest of the
/// career (finance and sponsors are the ones that need it).
/// </summary>
public sealed class CareerInputs
{
    /// <summary>Era finance facts (budgets, revenue model). Finance and sponsors need it.</summary>
    public IEraFinanceSource? Eras { get; init; }

    /// <summary>The authored regulation periods. Development (in-season testing, rule changes) and supply (fuel rule) need them.</summary>
    public IReadOnlyList<RulePeriod>? RulePeriods { get; init; }

    /// <summary>The authored era periods (budgets and the like). Supply prices deals against them.</summary>
    public IReadOnlyList<RulePeriod>? EraPeriods { get; init; }

    /// <summary>The opening engine supplies the world initializer reported. Supply builds its first deals from them on a new career.</summary>
    public IReadOnlyList<SupplyLink>? SupplyLinks { get; init; }

    /// <summary>Authored facility kinds and ESTIMATE starting levels. Infrastructure needs it.</summary>
    public FacilityCatalog? Facilities { get; init; }

    /// <summary>ISO-3 home country of a constructor id, from founders. Logistics (PP-064) needs it.</summary>
    public IReadOnlyDictionary<string, string>? TeamCountries { get; init; }

    /// <summary>Era slot rules for sponsors. Sponsors need it.</summary>
    public ISponsorEras? SponsorEras { get; init; }

    /// <summary>The fictional sponsors. Sponsors need it.</summary>
    public SponsorCatalog? Sponsors { get; init; }

    /// <summary>
    /// How rich a team starts, from the previous season's standing. The Jolpica results are local only (PP-041), so a host without
    /// them passes an authored ESTIMATE source, and without any source every team starts as a typical one.
    /// </summary>
    public ITeamTierSource? Tiers { get; init; }

    /// <summary>The authored ESTIMATE of constructor car strength. Supply derives a supplier's base engine from it; without it the tier fallback applies.</summary>
    public ICarStrengthSource? CarStrength { get; init; }

    /// <summary>Track layouts for the championship calendar. Without them the career runs no races.</summary>
    public IReadOnlyList<TrackLayout>? Layouts { get; init; }

    /// <summary>Which layout each championship round uses.</summary>
    public IReadOnlyList<RaceAssignment>? RaceAssignments { get; init; }

    /// <summary>
    /// The real race dates the host has (from the local cache, PP-041). A season with every round dated and room for a weekend before
    /// each race is planned on them (#229); any other season keeps the even spacing. Null or empty means even spacing everywhere.
    /// </summary>
    public RaceDateBook? RaceDates { get; init; }

    /// <summary>Catalog dimension ids, in authored order. Required to resolve a season's <see cref="RuleSet"/>.</summary>
    public IReadOnlyList<string>? RegulationDimensionIds { get; init; }

    /// <summary>Era catalog dimension ids. Required for fatality risk and the other era facts a race reads.</summary>
    public IReadOnlyList<string>? EraDimensionIds { get; init; }

    /// <summary>The regulation catalog a voted season proposes against. Historical careers do not need it.</summary>
    public IReadOnlyList<RuleDimensionSpec>? RegulationCatalog { get; init; }

    /// <summary>How this career treats rules. Historical reads the timeline; voted replaces next year's set on 31 December.</summary>
    public RulesSource Rules { get; init; } = RulesSource.Historical;

    /// <summary>Whether a race can kill a driver (PP-006). The default is off.</summary>
    public FatalityLevel Fatality { get; init; } = FatalityLevel.Off;

    /// <summary>
    /// The benchmark pay a contract starts from (the era's driver pay, scaled by stars). Without it every contract starts from one
    /// flat ESTIMATE salary, which is far too high for the early eras once teams pay from a real budget.
    /// </summary>
    public IPayBenchmark? Pay { get; init; }

    /// <summary>What orders two teams of one budget level on the board and in the sponsors' outlook: the previous place, then the car strength (#234).</summary>
    public PublicRankKeys RankKeys() => new(Tiers, CarStrength);

    /// <summary>The inputs a host builds from the authored era periods, the sponsor catalog, the era pay and a tier source.</summary>
    public static CareerInputs From(IReadOnlyList<RulePeriod> eraPeriods, SponsorCatalog sponsors, IPayBenchmark? pay = null, ITeamTierSource? tiers = null)
    {
        ArgumentNullException.ThrowIfNull(eraPeriods);
        ArgumentNullException.ThrowIfNull(sponsors);
        return new CareerInputs
        {
            Eras = new EraPeriodFinance(eraPeriods),
            EraPeriods = eraPeriods,
            SponsorEras = new PeriodSponsorEras(eraPeriods),
            Sponsors = sponsors,
            Pay = pay,
            Tiers = tiers,
        };
    }
}

/// <summary>
/// A human manager and the organization they run. The AI principal of that organization stays quiet. On a resumed career the
/// same assignment is passed again; a manager id of the form <c>human:{organizationId}</c> is also seated from the saved registry.
/// </summary>
public sealed record CareerHuman(string ManagerId, string Name, string OrganizationId);

/// <summary>What a run is given beyond the session: the modules (every career module by default) and the data inputs.</summary>
public sealed class CareerRunOptions
{
    /// <summary>The systems of the run, in attach order. <see cref="CareerModules.Default"/> is what a career runs.</summary>
    public IReadOnlyList<ICareerModule> Modules { get; init; } = CareerModules.Default;

    public CareerInputs Inputs { get; init; } = new();

    /// <summary>Human managers to register and seat. Empty for an AI-only run, which never blocks the day.</summary>
    public IReadOnlyList<CareerHuman> Humans { get; init; } = [];
}
