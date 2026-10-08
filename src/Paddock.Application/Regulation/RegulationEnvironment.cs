using Paddock.Application.Career;
using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Regulation;

/// <summary>Which racing series exist in the world and which teams race in each (#275, owner decision 5). The MVP has one.</summary>
public interface ISeriesDirectory
{
    /// <summary>The ids of the series that vote, in ordinal order.</summary>
    IReadOnlyList<string> SeriesIds { get; }

    /// <summary>The teams that race in the series on <paramref name="today"/>, in ordinal order of their ids.</summary>
    IReadOnlyList<Organization> TeamsOf(string seriesId, WorldState world, GameDate today);
}

/// <summary>The MVP's one series: every team of the career races in <see cref="SeriesIds.WorldChampionship"/>.</summary>
public sealed class SingleSeriesDirectory : ISeriesDirectory
{
    public IReadOnlyList<string> SeriesIds { get; } = [Paddock.Domain.Racing.SeriesIds.WorldChampionship];

    public IReadOnlyList<Organization> TeamsOf(string seriesId, WorldState world, GameDate today) =>
        string.Equals(seriesId, Paddock.Domain.Racing.SeriesIds.WorldChampionship, StringComparison.Ordinal)
            ? CareerTeams.Active(world, today)
            : [];
}

/// <summary>
/// The ports of regulation voting v2: how the career treats rules, the catalog, the authored baseline, the calendar data, who runs
/// which team and which teams race in which series. Built once per run by the module.
/// </summary>
public sealed class RegulationEnvironment
{
    public RegulationEnvironment(
        RulesSource rules,
        VoteMode mode,
        ulong masterSeed,
        IReadOnlyList<RuleDimensionSpec> catalog,
        IReadOnlyList<string> dimensionIds,
        IReadOnlyList<RulePeriod> periods,
        IOrganizationControl control,
        ManagerRegistry managers,
        ISeriesDirectory series,
        IReadOnlyList<TrackLayout>? layouts = null,
        IReadOnlyList<RaceAssignment>? assignments = null,
        IReadOnlyDictionary<string, string>? teamCountries = null,
        IReadOnlyList<OrganizationId>? humanTeams = null,
        RaceDateBook? raceDates = null,
        BannedRules? bannedRules = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(dimensionIds);
        ArgumentNullException.ThrowIfNull(periods);
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(managers);
        ArgumentNullException.ThrowIfNull(series);
        Rules = rules;
        Mode = mode;
        MasterSeed = masterSeed;
        Specs = catalog.ToDictionary(spec => spec.Id, StringComparer.Ordinal);
        DimensionIds = dimensionIds;
        Periods = periods;
        Control = control;
        Managers = managers;
        Series = series;
        Layouts = layouts ?? [];
        Assignments = assignments ?? [];
        TeamCountries = teamCountries ?? new Dictionary<string, string>(StringComparer.Ordinal);
        HumanTeams = humanTeams ?? [];
        RaceDates = raceDates;
        BannedRules = bannedRules ?? BannedRules.None;
    }

    /// <summary>True when the career votes its rules; a historical career has no political life.</summary>
    public bool Votes => Rules == RulesSource.VotedEachSeason;

    public RulesSource Rules { get; }

    public VoteMode Mode { get; }

    public ulong MasterSeed { get; }

    /// <summary>The catalog specs by dimension id; they validate every value a vote may adopt.</summary>
    public IReadOnlyDictionary<string, RuleDimensionSpec> Specs { get; }

    public IReadOnlyList<string> DimensionIds { get; }

    public IReadOnlyList<RulePeriod> Periods { get; }

    public IOrganizationControl Control { get; }

    public ManagerRegistry Managers { get; }

    public ISeriesDirectory Series { get; }

    public IReadOnlyList<TrackLayout> Layouts { get; }

    public IReadOnlyList<RaceAssignment> Assignments { get; }

    public IReadOnlyDictionary<string, string> TeamCountries { get; }

    /// <summary>The real race dates the host has, so a season that is not laid out yet is planned as the host will plan it. Null means even spacing.</summary>
    public RaceDateBook? RaceDates { get; }

    /// <summary>The rules and mechanics that can never be proposed or voted on, per series (#275). Nothing is banned when the host has no such data.</summary>
    public BannedRules BannedRules { get; }

    /// <summary>The teams a human sits at when the career opens: they start with no cooldown (owner decision 6).</summary>
    public IReadOnlyList<OrganizationId> HumanTeams { get; }

    /// <summary>The authored rules of <paramref name="season"/>, the baseline of a career that has not voted yet.</summary>
    public RuleSet AuthoredRules(int season) => RuleSet.For(season, DimensionIds, Periods);

    /// <summary>The managers who run <paramref name="team"/> and are human, in ordinal order.</summary>
    public IReadOnlyList<ManagerId> HumansOf(OrganizationId team)
    {
        var humans = new List<ManagerId>();
        foreach (var manager in Control.ManagersOf(team))
        {
            if (Managers.Contains(manager) && Managers.KindOf(manager) == ManagerKind.Human)
            {
                humans.Add(manager);
            }
        }

        return humans;
    }

    public bool IsHuman(OrganizationId team) => HumansOf(team).Count > 0;

    public string? CountryOf(string teamId) =>
        TeamCountries.TryGetValue(teamId, out var country) ? country : null;
}
