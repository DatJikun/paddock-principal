using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Finance;

/// <summary>Which benchmark band an organization starts in. ESTIMATE bands, see <see cref="FinanceEstimates.TypicalStandingThrough"/>.</summary>
public enum TeamTier
{
    Low = 0,
    Typical = 1,
    Top = 2,
}

/// <summary>One constructor's championship place. <see cref="Points"/> is the counted total (T27 standings).</summary>
public sealed record ConstructorTitleRow
{
    public ConstructorTitleRow(OrganizationId organization, int position, decimal points)
    {
        if (!organization.IsAssigned)
        {
            throw new ArgumentException("Organization id is unassigned.", nameof(organization));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
        if (points < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), points, "Championship points cannot be negative.");
        }

        Organization = organization;
        Position = position;
        Points = points;
    }

    public OrganizationId Organization { get; }

    public int Position { get; }

    public decimal Points { get; }
}

/// <summary>
/// One entry in a published race. T47 will emit the race; finance does not reference the race engine.
/// One row is one car.
/// </summary>
public sealed record RaceEntryResult
{
    public RaceEntryResult(OrganizationId organization, string driverId, int position, bool classified)
    {
        if (!organization.IsAssigned)
        {
            throw new ArgumentException("Organization id is unassigned.", nameof(organization));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
        Organization = organization;
        DriverId = driverId;
        Position = position;
        Classified = classified;
    }

    public OrganizationId Organization { get; }

    public string DriverId { get; }

    public int Position { get; }

    public bool Classified { get; }
}

public sealed record RaceResultsPublished
{
    public RaceResultsPublished(
        int season,
        int round,
        int racesInSeason,
        string revenueModel,
        IReadOnlyList<RaceEntryResult> entries)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(season, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(racesInSeason, round);
        ArgumentException.ThrowIfNullOrWhiteSpace(revenueModel);
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            throw new ArgumentException("A race needs at least one entry.", nameof(entries));
        }

        Season = season;
        Round = round;
        RacesInSeason = racesInSeason;
        RevenueModel = revenueModel;
        Entries = entries;
    }

    public int Season { get; }

    public int Round { get; }

    public int RacesInSeason { get; }

    public string RevenueModel { get; }

    public IReadOnlyList<RaceEntryResult> Entries { get; }
}

/// <summary>
/// A published season. <see cref="DistinctWinningOrganizations"/> is one id per different race-winning organization.
/// <see cref="Constructors"/> is the final constructors' table (position 1 is the champion).
/// </summary>
public sealed record SeasonEnded
{
    public SeasonEnded(
        int season,
        int races,
        IReadOnlyList<ConstructorTitleRow> constructors,
        IReadOnlyList<OrganizationId> distinctWinningOrganizations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(season, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(races);
        ArgumentNullException.ThrowIfNull(constructors);
        ArgumentNullException.ThrowIfNull(distinctWinningOrganizations);
        if (distinctWinningOrganizations.Count > races)
        {
            throw new ArgumentException("There cannot be more winning organizations than races.", nameof(distinctWinningOrganizations));
        }

        Season = season;
        Races = races;
        Constructors = constructors;
        DistinctWinningOrganizations = distinctWinningOrganizations;
    }

    public int Season { get; }

    public int Races { get; }

    public IReadOnlyList<ConstructorTitleRow> Constructors { get; }

    public IReadOnlyList<OrganizationId> DistinctWinningOrganizations { get; }
}

/// <summary>
/// Rank at the start of a season from the previous season's constructors' table.
/// T22's car effect can replace this. Missing facts yield <see cref="TeamTier.Typical"/>.
/// </summary>
public interface ITeamTierSource
{
    TeamTier TierOf(OrganizationId organization, int startSeason);
}

/// <summary>One authored or Jolpica constructors' place. The host supplies these; the Jolpica cache is not in the repo (PP-041).</summary>
public sealed record ConstructorStandingFact
{
    public ConstructorStandingFact(int season, string organizationId, int position)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(season, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
        Season = season;
        OrganizationId = organizationId;
        Position = position;
    }

    public int Season { get; }

    public string OrganizationId { get; }

    public int Position { get; }
}

/// <summary>
/// Default tier: previous season's constructors' position. ESTIMATE bands in <see cref="FinanceEstimates"/>.
/// </summary>
public sealed class PreviousSeasonStandingTier : ITeamTierSource
{
    private readonly Dictionary<(int Season, string Organization), int> _position = new();

    public PreviousSeasonStandingTier(IEnumerable<ConstructorStandingFact> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        foreach (var fact in facts)
        {
            if (!_position.TryAdd((fact.Season, fact.OrganizationId), fact.Position))
            {
                throw new ArgumentException(
                    "Constructor '" + fact.OrganizationId + "' is listed twice in " + fact.Season.ToString(CultureInfo.InvariantCulture) + ".",
                    nameof(facts));
            }
        }
    }

    public TeamTier TierOf(OrganizationId organization, int startSeason)
    {
        if (!organization.IsAssigned)
        {
            throw new ArgumentException("Organization id is unassigned.", nameof(organization));
        }

        if (!_position.TryGetValue((startSeason - 1, organization.Value), out var position))
        {
            return TeamTier.Typical;
        }

        if (position == 1)
        {
            return TeamTier.Top;
        }

        return position <= FinanceEstimates.TypicalStandingThrough ? TeamTier.Typical : TeamTier.Low;
    }
}

/// <summary>
/// A loan the board could accept (PP-048). Finance v0 does not sell loans; this is the hook PP-050 asked for.
/// Offers stay empty until a follow-up posts drawdowns through <see cref="LedgerCategories.OwnerFunds"/>.
/// </summary>
public sealed record LoanOffer
{
    public LoanOffer(string offerId, long principalCents, int termSeasons, int interestBasisPoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(offerId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(principalCents);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(termSeasons);
        ArgumentOutOfRangeException.ThrowIfNegative(interestBasisPoints);
        OfferId = offerId;
        PrincipalCents = principalCents;
        TermSeasons = termSeasons;
        InterestBasisPoints = interestBasisPoints;
    }

    public string OfferId { get; }

    public long PrincipalCents { get; }

    public int TermSeasons { get; }

    public int InterestBasisPoints { get; }
}

public interface ILoanFacility
{
    IReadOnlyList<LoanOffer> Offers(OrganizationId organization, GameDate on);
}

/// <summary>No loans are on offer. Cash may still go negative for one season (PP-050).</summary>
public sealed class NoLoanFacility : ILoanFacility
{
    public static NoLoanFacility Instance { get; } = new();

    public IReadOnlyList<LoanOffer> Offers(OrganizationId organization, GameDate on) => [];
}
