using Paddock.Domain.Time;

namespace Paddock.Domain.Racing;

/// <summary>
/// The stable political taste of a team (#275, owner decision 3). It feeds an AI team's vote on top of its own interest.
/// It is a field of the team in the <c>regulations</c> section, stable for now; the one place that reads it
/// (<c>TeamLeaningSeam</c> in Paddock.Simulation) takes the season, so a later change over time (by era, by the car-industry
/// situation) touches that place only.
/// </summary>
public enum PoliticalLeaning
{
    /// <summary>Resists change and prefers the older rule.</summary>
    Traditionalist = 0,

    /// <summary>Favours new technology and newer rules.</summary>
    Progressive = 1,

    /// <summary>Favours rules that narrow the gap between rich and poor teams.</summary>
    Egalitarian = 2,

    /// <summary>Favours show: formats that make the weekend more of a spectacle.</summary>
    Gimmicky = 3,
}

/// <summary>Ids of the racing series. The MVP has one; everything political is keyed by series id so more can follow without a migration.</summary>
public static class SeriesIds
{
    /// <summary>The world championship, the only series of the MVP.</summary>
    public const string WorldChampionship = "f1";

    /// <summary>The ids of every series the game knows, in ordinal order. The authored banned list may name only these.</summary>
    public static IReadOnlyList<string> All { get; } = [WorldChampionship];
}

/// <summary>Who put a ballot item on the agenda.</summary>
public enum BallotOrigin
{
    /// <summary>The FIA itself, from the state of the series' world.</summary>
    Fia = 0,

    /// <summary>One or more teams that paid for a proposal.</summary>
    Teams = 1,
}

/// <summary>The two options every ballot item has besides its variants, plus the id format of a variant.</summary>
public static class BallotOptions
{
    /// <summary>Keep the rule as it is.</summary>
    public const string StatusQuo = "status-quo";

    /// <summary>No vote. In vote-bank mode the vote goes to the team's bank.</summary>
    public const string Abstain = "abstain";

    public static string VariantId(int number) =>
        "v" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// One proposed value of the dimension of a ballot item. Two teams that propose the same value share one variant.
/// An FIA variant has no proposers.
/// </summary>
public sealed record BallotVariant(string Id, string Value, IReadOnlyList<string> ProposerTeamIds)
{
    public string Id { get; } = string.IsNullOrWhiteSpace(Id) ? throw new ArgumentException("A variant needs an id.", nameof(Id)) : Id;

    public string Value { get; } = string.IsNullOrWhiteSpace(Value) ? throw new ArgumentException("A variant needs a value.", nameof(Value)) : Value;

    public IReadOnlyList<string> ProposerTeamIds { get; } = ProposerTeamIds is null
        ? throw new ArgumentNullException(nameof(ProposerTeamIds))
        : [.. ProposerTeamIds.OrderBy(id => id, StringComparer.Ordinal)];
}

/// <summary>
/// A vote a team cast before the deadline. <see cref="Option"/> is <see cref="BallotOptions.StatusQuo"/>,
/// <see cref="BallotOptions.Abstain"/> or the id of a variant. <see cref="Spent"/> is the number of banked votes added to it.
/// </summary>
public sealed record CastVote(string TeamId, string Option, int Spent)
{
    public string TeamId { get; } = string.IsNullOrWhiteSpace(TeamId) ? throw new ArgumentException("A vote needs a team.", nameof(TeamId)) : TeamId;

    public string Option { get; } = string.IsNullOrWhiteSpace(Option) ? throw new ArgumentException("A vote needs an option.", nameof(Option)) : Option;

    public int Spent { get; } = Spent < 0 ? throw new ArgumentOutOfRangeException(nameof(Spent)) : Spent;
}

public enum BallotOutcome
{
    /// <summary>A variant won and was enough: the dimension changes from the next season.</summary>
    Adopted = 0,

    /// <summary>The status quo led, or the leading variant did not reach the threshold: nothing changes.</summary>
    Rejected = 1,

    /// <summary>Nobody voted: nothing changes.</summary>
    NoVotes = 2,
}

/// <summary>Weight that one option gathered in the tally.</summary>
public sealed record OptionTally(string Option, int Weight);

/// <summary>
/// How one team stood at the deadline, kept so the result can be explained: the option it backed, the weight it carried,
/// how many banked votes it spent, whether it banked its vote instead, and the translation key of the reason that weighed most
/// (empty for a human who set no reason).
/// </summary>
public sealed record TeamStance(string TeamId, string Option, int Weight, int Spent, bool Banked, string ReasonKey)
{
    public string TeamId { get; } = string.IsNullOrWhiteSpace(TeamId) ? throw new ArgumentException("A stance needs a team.", nameof(TeamId)) : TeamId;

    public string Option { get; } = string.IsNullOrWhiteSpace(Option) ? throw new ArgumentException("A stance needs an option.", nameof(Option)) : Option;

    public string ReasonKey { get; } = ReasonKey ?? throw new ArgumentNullException(nameof(ReasonKey));
}

/// <summary>The result of a ballot item. <see cref="ReasonKey"/> is the translation key that explains it to the player.</summary>
public sealed record BallotResult(
    BallotOutcome Outcome,
    string WinningOption,
    string ReasonKey,
    bool PresidentDecided,
    IReadOnlyList<OptionTally> Tally,
    IReadOnlyList<TeamStance> Stances)
{
    public string WinningOption { get; } = string.IsNullOrWhiteSpace(WinningOption)
        ? throw new ArgumentException("A result needs a winning option.", nameof(WinningOption))
        : WinningOption;

    public string ReasonKey { get; } = string.IsNullOrWhiteSpace(ReasonKey)
        ? throw new ArgumentException("A result needs a reason.", nameof(ReasonKey))
        : ReasonKey;

    public IReadOnlyList<OptionTally> Tally { get; } = Tally is null
        ? throw new ArgumentNullException(nameof(Tally))
        : [.. Tally.OrderBy(entry => entry.Option, StringComparer.Ordinal)];

    public IReadOnlyList<TeamStance> Stances { get; } = Stances is null
        ? throw new ArgumentNullException(nameof(Stances))
        : [.. Stances.OrderBy(entry => entry.TeamId, StringComparer.Ordinal)];
}

/// <summary>
/// One ballot item: one vote on one dimension with the variants proposed for it (owner decision 9: one item per dimension,
/// whoever proposed it). <see cref="Season"/> is the season of the vote; a change takes effect from the season after it.
/// </summary>
public sealed record BallotItem
{
    public BallotItem(
        string id,
        int season,
        string dimensionId,
        BallotOrigin origin,
        GameDate announced,
        GameDate deadline,
        string currentValue,
        string reasonKey,
        IReadOnlyList<KeyValuePair<string, string>> reasonArguments,
        IReadOnlyList<BallotVariant> variants,
        IReadOnlyList<CastVote> votes,
        BallotResult? result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfLessThan(season, 1950);
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonKey);
        ArgumentNullException.ThrowIfNull(reasonArguments);
        ArgumentNullException.ThrowIfNull(variants);
        ArgumentNullException.ThrowIfNull(votes);
        if (variants.Count == 0)
        {
            throw new ArgumentException("A ballot item needs at least one variant.", nameof(variants));
        }

        if (deadline < announced)
        {
            throw new ArgumentException("A deadline cannot be before the announcement.", nameof(deadline));
        }

        var variantIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variant in variants)
        {
            if (!variantIds.Add(variant.Id))
            {
                throw new ArgumentException("Variant '" + variant.Id + "' is listed twice.", nameof(variants));
            }
        }

        var voters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var vote in votes)
        {
            if (!voters.Add(vote.TeamId))
            {
                throw new ArgumentException("Team '" + vote.TeamId + "' voted twice.", nameof(votes));
            }

            if (vote.Option is not (BallotOptions.StatusQuo or BallotOptions.Abstain) && !variantIds.Contains(vote.Option))
            {
                throw new ArgumentException("Option '" + vote.Option + "' is not on the ballot.", nameof(votes));
            }
        }

        Id = id;
        Season = season;
        DimensionId = dimensionId;
        Origin = origin;
        Announced = announced;
        Deadline = deadline;
        CurrentValue = currentValue;
        ReasonKey = reasonKey;
        ReasonArguments = [.. reasonArguments.OrderBy(pair => pair.Key, StringComparer.Ordinal)];
        Variants = [.. variants.OrderBy(variant => variant.Id, StringComparer.Ordinal)];
        Votes = [.. votes.OrderBy(vote => vote.TeamId, StringComparer.Ordinal)];
        Result = result;
    }

    public string Id { get; }

    /// <summary>The season of the vote. The change applies from <c>Season + 1</c>.</summary>
    public int Season { get; }

    public string DimensionId { get; }

    public BallotOrigin Origin { get; }

    public GameDate Announced { get; }

    public GameDate Deadline { get; }

    /// <summary>The value in force when the item was announced (the status quo option).</summary>
    public string CurrentValue { get; }

    /// <summary>Why the item is on the ballot, as a translation key (an FIA pressure, or a team proposal).</summary>
    public string ReasonKey { get; }

    public IReadOnlyList<KeyValuePair<string, string>> ReasonArguments { get; }

    public IReadOnlyList<BallotVariant> Variants { get; }

    /// <summary>Votes cast so far by teams that set one (the player). Cleared in meaning once <see cref="Result"/> exists.</summary>
    public IReadOnlyList<CastVote> Votes { get; }

    public BallotResult? Result { get; }

    public bool IsResolved => Result is not null;

    public BallotVariant? VariantOf(string option)
    {
        foreach (var variant in Variants)
        {
            if (string.Equals(variant.Id, option, StringComparison.Ordinal))
            {
                return variant;
            }
        }

        return null;
    }

    public BallotItem WithVotes(IReadOnlyList<CastVote> votes) =>
        new(Id, Season, DimensionId, Origin, Announced, Deadline, CurrentValue, ReasonKey, ReasonArguments, Variants, votes, Result);

    public BallotItem WithResult(BallotResult result) =>
        new(Id, Season, DimensionId, Origin, Announced, Deadline, CurrentValue, ReasonKey, ReasonArguments, Variants, Votes, result);
}

/// <summary>
/// What one team keeps in the political life of a series: its leaning, the first season it may pay for a proposal again, and its
/// private bank of votes (vote-bank mode). The cooldown is on the team only; it never locks a rule (owner decision 6).
/// </summary>
public sealed record TeamPolitics(string TeamId, PoliticalLeaning Leaning, int ProposeFromSeason, int Bank)
{
    public string TeamId { get; } = string.IsNullOrWhiteSpace(TeamId) ? throw new ArgumentException("A team is needed.", nameof(TeamId)) : TeamId;

    public PoliticalLeaning Leaning { get; } = Enum.IsDefined(Leaning) ? Leaning : throw new ArgumentOutOfRangeException(nameof(Leaning));

    public int ProposeFromSeason { get; } = ProposeFromSeason < 1950 ? throw new ArgumentOutOfRangeException(nameof(ProposeFromSeason)) : ProposeFromSeason;

    public int Bank { get; } = Bank < 0 ? throw new ArgumentOutOfRangeException(nameof(Bank), "A bank cannot go negative.") : Bank;

    public TeamPolitics WithProposeFrom(int season) => new(TeamId, Leaning, season, Bank);

    public TeamPolitics WithBank(int bank) => new(TeamId, Leaning, ProposeFromSeason, bank);
}

/// <summary>A proposal a team paid for and that waits for the team ballot to be built. One per team and season.</summary>
public sealed record PendingProposal(string TeamId, string DimensionId, string Value, GameDate Filed, long FeeCents)
{
    public string TeamId { get; } = string.IsNullOrWhiteSpace(TeamId) ? throw new ArgumentException("A team is needed.", nameof(TeamId)) : TeamId;

    public string DimensionId { get; } = string.IsNullOrWhiteSpace(DimensionId) ? throw new ArgumentException("A dimension is needed.", nameof(DimensionId)) : DimensionId;

    public string Value { get; } = string.IsNullOrWhiteSpace(Value) ? throw new ArgumentException("A value is needed.", nameof(Value)) : Value;

    public long FeeCents { get; } = FeeCents <= 0 ? throw new ArgumentOutOfRangeException(nameof(FeeCents), "A fee is never zero or negative.") : FeeCents;
}
