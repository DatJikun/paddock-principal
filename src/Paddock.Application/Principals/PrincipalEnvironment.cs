using Paddock.Application.Cars;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.Sponsors;
using Paddock.Application.Supply;
using Paddock.Domain.Development;
using Paddock.Domain.Spy;
using Paddock.Domain.World;
using Paddock.Simulation.Ai;
using Paddock.Application.Pool;

namespace Paddock.Application.Principals;

/// <summary>
/// What the principals have heard about the rules of the season to come (they are published ahead). It is the only look into the future the
/// development decision gets, it reaches one season ahead, and it is a statement about the rules, never about results (D-010). A pure
/// query (INV-005).
/// </summary>
public interface IRegulationOutlook
{
    /// <summary>The share (0 to 1) of the technical rules announced to differ in <c>season + 1</c> from <c>season</c>. Zero when none is known.</summary>
    double ChangeAfter(int season);
}

/// <summary>No announced change. The default.</summary>
public sealed class NoRegulationOutlook : IRegulationOutlook
{
    public static NoRegulationOutlook Instance { get; } = new();

    public double ChangeAfter(int season) => 0.0;
}

/// <summary>The outlook read from the era rules development already uses (<see cref="IDevelopmentRules"/>): the changed share of the fingerprint dimensions.</summary>
public sealed class DevelopmentRulesOutlook : IRegulationOutlook
{
    private readonly IDevelopmentRules _rules;
    private readonly Dictionary<int, DevelopmentEra> _eras = [];

    public DevelopmentRulesOutlook(IDevelopmentRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
    }

    public double ChangeAfter(int season)
    {
        var (changed, total) = Era(season + 1).ChangedSince(Era(season));
        return total == 0 ? 0.0 : Math.Min(1.0, changed / (double)total);
    }

    private DevelopmentEra Era(int year)
    {
        if (!_eras.TryGetValue(year, out var era))
        {
            era = _rules.Era(year);
            _eras[year] = era;
        }

        return era;
    }
}

/// <summary>The development module as the principals read and command it.</summary>
public sealed record DevelopmentSources(DevelopmentBook Book, DevelopmentEnvironment Environment, CarBook Cars);

/// <summary>The supply module as the principals read and command it.</summary>
public sealed record SupplySources(SupplyBook Book, SupplyEnvironment Environment);

/// <summary>The sponsor module as the principals read and command it.</summary>
public sealed record SponsorSources(SponsorBook Book, SponsorEnvironment Environment, ObjectiveQuery Objectives);

/// <summary>
/// Everything an AI principal needs from the host, in one place. The market (contracts) is the core: without <see cref="Contracts"/> only the
/// development, supply and sponsor decisions run. Each module is optional, and a decision kind is simply skipped when its module is not
/// given, so a host wires them as they come. <see cref="Controls"/> are the tables of who runs which organization that the modules'
/// environments use; the AI manager of every AI-run team is assigned in all of them (<see cref="PrincipalRegistration"/>).
/// </summary>
public sealed class PrincipalEnvironment
{
    public PrincipalEnvironment(ulong masterSeed, Func<WorldState> readWorld, ManagerRegistry managers, IReadOnlyList<ControlTable> controls)
    {
        ArgumentNullException.ThrowIfNull(readWorld);
        ArgumentNullException.ThrowIfNull(managers);
        ArgumentNullException.ThrowIfNull(controls);
        if (controls.Count == 0)
        {
            throw new ArgumentException("At least one control table is needed.", nameof(controls));
        }

        MasterSeed = masterSeed;
        ReadWorld = readWorld;
        Managers = managers;
        Controls = controls;
    }

    public ulong MasterSeed { get; }

    public Func<WorldState> ReadWorld { get; }

    public ManagerRegistry Managers { get; }

    public IReadOnlyList<ControlTable> Controls { get; }

    /// <summary>The contract engine of the career (drivers and key staff). Null: no market decisions.</summary>
    public ContractEngine? Contracts { get; init; }

    public DevelopmentSources? Development { get; init; }

    public SupplySources? Supply { get; init; }

    public SponsorSources? Sponsors { get; init; }

    /// <summary>The talent pool, for pointing the scouts at it. Null: no scouting decision.</summary>
    public PoolBook? Pool { get; init; }

    /// <summary>Public results: constructors' positions by season. Defaults to the contract environment's, or to none.</summary>
    public IConstructorStandings? Standings { get; init; }

    public IRegulationOutlook Outlook { get; init; } = NoRegulationOutlook.Instance;

    /// <summary>The historical pull on choices (T46). Neutral by default.</summary>
    public IHistoricalBias Bias { get; init; } = NeutralHistoricalBias.Instance;

    /// <summary>Where decision traces go. A passive observer: it never changes a decision (INV-006).</summary>
    public ITraceSink Trace { get; init; } = NullSink.Instance;

    internal IConstructorStandings StandingsOrDefault =>
        Standings ?? Contracts?.Book.Environment.Standings ?? new NoStandings();

    internal IOrganizationControl PrimaryControl => Controls[0];
}
