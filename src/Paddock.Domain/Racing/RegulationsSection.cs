using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Racing;

/// <summary>A proposal the teams rejected, so it is not brought back at once. <see cref="Season"/> is the season of the vote.</summary>
public sealed record RejectedRegulation
{
    public RejectedRegulation(string dimensionId, string value, int season)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(season, 1950);
        DimensionId = dimensionId;
        Value = value;
        Season = season;
    }

    public string DimensionId { get; }

    public string Value { get; }

    public int Season { get; }
}

/// <summary>
/// The political life of one racing series (#275, owner decision 5): the rules in force, the rules and calendar changes voted for
/// the next season, the cooldown and bank of every team, the proposals waiting for a ballot and the ballot itself.
/// A change voted in season N is stored in <see cref="NextValues"/> and <see cref="NextCalendar"/> and is never visible in
/// season N: <see cref="Promote"/> moves it into force on the first day of season N + 1.
/// </summary>
public sealed record SeriesRegulations
{
    private static readonly IReadOnlyList<KeyValuePair<string, string>> NoPairs = [];

    public SeriesRegulations(
        string seriesId,
        int season,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string> calendar,
        IReadOnlyDictionary<string, string>? nextValues,
        IReadOnlyDictionary<string, string>? nextCalendar,
        IReadOnlyList<RejectedRegulation> rejected,
        IReadOnlyList<TeamPolitics> teams,
        IReadOnlyList<PendingProposal> pending,
        IReadOnlyList<BallotItem> ballot,
        int agendaSeason,
        int fiaSlotsDone,
        int teamBallotSeason)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fiaSlotsDone);
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesId);
        ArgumentOutOfRangeException.ThrowIfLessThan(season, 1950);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(rejected);
        ArgumentNullException.ThrowIfNull(teams);
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(ballot);
        if (values.Count == 0)
        {
            throw new ArgumentException("A voted rule set needs at least one dimension.", nameof(values));
        }

        if (nextValues is { Count: 0 })
        {
            throw new ArgumentException("A stored next rule set needs at least one dimension.", nameof(nextValues));
        }

        SeriesId = seriesId;
        Season = season;
        Values = Sorted(values, nameof(values));
        Calendar = Sorted(calendar, nameof(calendar));
        NextValues = nextValues is null ? null : Sorted(nextValues, nameof(nextValues));
        NextCalendar = nextCalendar is null ? null : Sorted(nextCalendar, nameof(nextCalendar));
        Rejected = SortedRejected(rejected);
        Teams = SortedTeams(teams);
        Pending = SortedPending(pending);
        Ballot = SortedBallot(ballot);
        AgendaSeason = agendaSeason;
        FiaSlotsDone = fiaSlotsDone;
        TeamBallotSeason = teamBallotSeason;
    }

    public string SeriesId { get; init; }

    /// <summary>The season the <see cref="Values"/> and the <see cref="Calendar"/> are in force for.</summary>
    public int Season { get; init; }

    public IReadOnlyList<KeyValuePair<string, string>> Values { get; init; }

    /// <summary>The calendar policy in force (voted drops, additions and layout changes, by circuit). Empty means "as authored".</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Calendar { get; init; }

    /// <summary>The rules voted for <c>Season + 1</c>, or null while nothing is decided (the next season then repeats <see cref="Values"/>).</summary>
    public IReadOnlyList<KeyValuePair<string, string>>? NextValues { get; init; }

    /// <summary>The calendar policy voted for <c>Season + 1</c>, or null while nothing is decided.</summary>
    public IReadOnlyList<KeyValuePair<string, string>>? NextCalendar { get; init; }

    public IReadOnlyList<RejectedRegulation> Rejected { get; init; }

    public IReadOnlyList<TeamPolitics> Teams { get; init; }

    public IReadOnlyList<PendingProposal> Pending { get; init; }

    public IReadOnlyList<BallotItem> Ballot { get; init; }

    /// <summary>The last season whose FIA agenda was set up (so it is set up once).</summary>
    public int AgendaSeason { get; init; }

    /// <summary>How many of the FIA's votes of <see cref="AgendaSeason"/> have been announced (or skipped for want of a candidate).</summary>
    public int FiaSlotsDone { get; init; }

    /// <summary>The last season whose team ballot was built from the pending proposals (so it is built once).</summary>
    public int TeamBallotSeason { get; init; }

    public TeamPolitics? TeamOf(string teamId)
    {
        foreach (var team in Teams)
        {
            if (string.Equals(team.TeamId, teamId, StringComparison.Ordinal))
            {
                return team;
            }
        }

        return null;
    }

    public BallotItem? ItemOf(string itemId)
    {
        foreach (var item in Ballot)
        {
            if (string.Equals(item.Id, itemId, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>The rules of <paramref name="season"/>: the ones in force, or the ones voted for the next season; null for any other.</summary>
    public RuleSet? RuleSetFor(int season)
    {
        if (season == Season)
        {
            return RuleSet.Restore(season, ToDictionary(Values));
        }

        if (season == Season + 1)
        {
            return RuleSet.Restore(season, ToDictionary(NextValues ?? Values));
        }

        return null;
    }

    /// <summary>The calendar policy of <paramref name="season"/>, or null when it is neither the current nor the next one.</summary>
    public IReadOnlyDictionary<string, string>? CalendarFor(int season)
    {
        if (season == Season)
        {
            return ToDictionary(Calendar);
        }

        return season == Season + 1 ? ToDictionary(NextCalendar ?? Calendar) : null;
    }

    public SeriesRegulations WithTeam(TeamPolitics team)
    {
        ArgumentNullException.ThrowIfNull(team);
        var teams = Teams.Where(existing => !string.Equals(existing.TeamId, team.TeamId, StringComparison.Ordinal)).Append(team).ToArray();
        return this with { Teams = SortedTeams(teams) };
    }

    public SeriesRegulations WithItem(BallotItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var ballot = Ballot.Where(existing => !string.Equals(existing.Id, item.Id, StringComparison.Ordinal)).Append(item).ToArray();
        return this with { Ballot = SortedBallot(ballot) };
    }

    public SeriesRegulations WithPending(IReadOnlyList<PendingProposal> pending) =>
        this with { Pending = SortedPending(pending) };

    public SeriesRegulations WithRejected(IReadOnlyList<RejectedRegulation> rejected) =>
        this with { Rejected = SortedRejected(rejected) };

    public SeriesRegulations WithNext(IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, string> calendar) =>
        this with { NextValues = Sorted(values, nameof(values)), NextCalendar = Sorted(calendar, nameof(calendar)) };

    public SeriesRegulations WithAgenda(int season, int slotsDone)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slotsDone);
        return this with { AgendaSeason = season, FiaSlotsDone = slotsDone };
    }

    public SeriesRegulations WithTeamBallotSeason(int season) => this with { TeamBallotSeason = season };

    /// <summary>
    /// The first day of the next season: what was voted comes into force. The ballot keeps the items of the season just ended so
    /// the player can still read what was decided; older items go. A series with nothing voted repeats its rules.
    /// </summary>
    public SeriesRegulations Promote()
    {
        var season = Season + 1;
        return this with
        {
            Season = season,
            Values = NextValues ?? Values,
            Calendar = NextCalendar ?? Calendar,
            NextValues = null,
            NextCalendar = null,
            Pending = [],
            Ballot = [.. Ballot.Where(item => item.Season >= season - 1)],
        };
    }

    private static IReadOnlyDictionary<string, string> ToDictionary(IReadOnlyList<KeyValuePair<string, string>> pairs)
    {
        var map = new Dictionary<string, string>(pairs.Count, StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            map.Add(key, value);
        }

        return map;
    }

    private static IReadOnlyList<KeyValuePair<string, string>> Sorted(IReadOnlyDictionary<string, string> map, string parameter)
    {
        var list = new List<KeyValuePair<string, string>>(map.Count);
        foreach (var (key, value) in map)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A rule dimension and its value are both required.", parameter);
            }

            list.Add(new KeyValuePair<string, string>(key, value));
        }

        list.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));
        return list;
    }

    private static IReadOnlyList<RejectedRegulation> SortedRejected(IReadOnlyList<RejectedRegulation> rejected)
    {
        var memory = new List<RejectedRegulation>(rejected.Count);
        var seen = new HashSet<string>(rejected.Count, StringComparer.Ordinal);
        foreach (var item in rejected)
        {
            ArgumentNullException.ThrowIfNull(item);
            var key = item.DimensionId + "\u001f" + item.Value + "\u001f" + item.Season.ToString(CultureInfo.InvariantCulture);
            if (!seen.Add(key))
            {
                throw new ArgumentException("Rejected proposal '" + item.DimensionId + "' is listed twice.", nameof(rejected));
            }

            memory.Add(item);
        }

        memory.Sort(static (left, right) =>
        {
            var dimension = string.CompareOrdinal(left.DimensionId, right.DimensionId);
            if (dimension != 0)
            {
                return dimension;
            }

            var value = string.CompareOrdinal(left.Value, right.Value);
            return value != 0 ? value : left.Season.CompareTo(right.Season);
        });
        return memory;
    }

    private static IReadOnlyList<TeamPolitics> SortedTeams(IReadOnlyList<TeamPolitics> teams)
    {
        var sorted = new List<TeamPolitics>(teams);
        sorted.Sort(static (left, right) => string.CompareOrdinal(left.TeamId, right.TeamId));
        for (var i = 1; i < sorted.Count; i++)
        {
            if (string.Equals(sorted[i].TeamId, sorted[i - 1].TeamId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Team '" + sorted[i].TeamId + "' is listed twice.", nameof(teams));
            }
        }

        return sorted;
    }

    private static IReadOnlyList<PendingProposal> SortedPending(IReadOnlyList<PendingProposal> pending)
    {
        var sorted = new List<PendingProposal>(pending);
        sorted.Sort(static (left, right) => string.CompareOrdinal(left.TeamId, right.TeamId));
        for (var i = 1; i < sorted.Count; i++)
        {
            if (string.Equals(sorted[i].TeamId, sorted[i - 1].TeamId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Team '" + sorted[i].TeamId + "' has two pending proposals.", nameof(pending));
            }
        }

        return sorted;
    }

    private static IReadOnlyList<BallotItem> SortedBallot(IReadOnlyList<BallotItem> ballot)
    {
        var sorted = new List<BallotItem>(ballot);
        sorted.Sort(static (left, right) => string.CompareOrdinal(left.Id, right.Id));
        for (var i = 1; i < sorted.Count; i++)
        {
            if (string.Equals(sorted[i].Id, sorted[i - 1].Id, StringComparison.Ordinal))
            {
                throw new ArgumentException("Ballot item '" + sorted[i].Id + "' is listed twice.", nameof(ballot));
            }
        }

        return sorted;
    }
}

/// <summary>
/// The <c>regulations</c> world section (T47, T23, #275): the political life of every racing series of a career that votes its
/// rules (<see cref="Paddock.Domain.Career.RulesSource.VotedEachSeason"/>). A historical career does not write this section; it
/// reads the authored timeline. Absent means "use the authored rules".
/// <para>
/// Canonical text (schema 2), per series, in ordinal order of the series id:
/// <code>
/// series &lt;count&gt;
/// s &lt;len&gt;:&lt;id&gt; &lt;season&gt; &lt;agenda season&gt; &lt;FIA slots done&gt; &lt;team ballot season&gt;
/// values / calendar / nextvalues / nextcalendar &lt;count&gt;, then value &lt;len&gt;:&lt;dimension&gt; &lt;len&gt;:&lt;value&gt;
/// rejected &lt;count&gt;, then rejected &lt;len&gt;:&lt;dimension&gt; &lt;len&gt;:&lt;value&gt; &lt;season&gt;
/// teams &lt;count&gt;, then team &lt;len&gt;:&lt;id&gt; &lt;leaning&gt; &lt;propose from&gt; &lt;bank&gt;
/// pending &lt;count&gt;, then pending &lt;len&gt;:&lt;team&gt; &lt;len&gt;:&lt;dimension&gt; &lt;len&gt;:&lt;value&gt; &lt;date&gt; &lt;fee cents&gt;
/// ballot &lt;count&gt;, then per item: item, args, variants, votes and an optional result with its tally and stances
/// </code>
/// Schema 1 (one series, no teams, no ballot) is read as the series <see cref="SeriesIds.WorldChampionship"/>.
/// </para>
/// </summary>
public sealed class RegulationsSection : IWorldSection
{
    public const string SectionName = "regulations";

    private readonly SeriesRegulations[] _series;

    private RegulationsSection(SeriesRegulations[] series)
    {
        _series = series;
    }

    public string Name => SectionName;

    public int SchemaVersion => 2;

    public IReadOnlyList<SeriesRegulations> Series => _series;

    public static RegulationsSection Create(IEnumerable<SeriesRegulations> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        var copy = series.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("A regulations section needs at least one series.", nameof(series));
        }

        foreach (var item in copy)
        {
            ArgumentNullException.ThrowIfNull(item);
        }

        Array.Sort(copy, static (left, right) => string.CompareOrdinal(left.SeriesId, right.SeriesId));
        for (var i = 1; i < copy.Length; i++)
        {
            if (string.Equals(copy[i].SeriesId, copy[i - 1].SeriesId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Series '" + copy[i].SeriesId + "' is listed twice.", nameof(series));
            }
        }

        return new RegulationsSection(copy);
    }

    /// <summary>The schema-1 shape: one series with a rule set and a rejection memory, no teams and no ballot.</summary>
    public static RegulationsSection Create(int season, IReadOnlyDictionary<string, string> values, IReadOnlyList<RejectedRegulation> rejected) =>
        Create([OpeningSeries(SeriesIds.WorldChampionship, season, values, rejected)]);

    public static SeriesRegulations OpeningSeries(
        string seriesId,
        int season,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<RejectedRegulation>? rejected = null) =>
        new(seriesId, season, values, new Dictionary<string, string>(), null, null, rejected ?? [], [], [], [], 0, 0, 0);

    public SeriesRegulations? Find(string seriesId)
    {
        foreach (var series in _series)
        {
            if (string.Equals(series.SeriesId, seriesId, StringComparison.Ordinal))
            {
                return series;
            }
        }

        return null;
    }

    public RegulationsSection WithSeries(SeriesRegulations series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return Create(_series.Where(existing => !string.Equals(existing.SeriesId, series.SeriesId, StringComparison.Ordinal)).Append(series));
    }

    /// <summary>
    /// The rules of <paramref name="season"/> in the series, or null when the section has no rules for that season (the caller
    /// then reads the authored timeline). The season after the stored one is answered from the votes of the stored season.
    /// </summary>
    public RuleSet? RuleSetFor(string seriesId, int season) => Find(seriesId)?.RuleSetFor(season);

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Count("series", _series.Length);
        foreach (var series in _series)
        {
            writer.Begin("s");
            writer.Field(series.SeriesId);
            writer.Raw(" " + Int(series.Season) + " " + Int(series.AgendaSeason) + " " + Int(series.FiaSlotsDone) + " " + Int(series.TeamBallotSeason));
            writer.End();
            Pairs(writer, "values", series.Values);
            Pairs(writer, "calendar", series.Calendar);
            Pairs(writer, "nextvalues", series.NextValues ?? []);
            writer.Flag("hasnext", series.NextValues is not null);
            Pairs(writer, "nextcalendar", series.NextCalendar ?? []);
            writer.Flag("hasnextcalendar", series.NextCalendar is not null);
            writer.Count("rejected", series.Rejected.Count);
            foreach (var item in series.Rejected)
            {
                writer.Begin("rejected");
                writer.Field(item.DimensionId);
                writer.Space();
                writer.Field(item.Value);
                writer.Raw(" " + Int(item.Season));
                writer.End();
            }

            writer.Count("teams", series.Teams.Count);
            foreach (var team in series.Teams)
            {
                writer.Begin("team");
                writer.Field(team.TeamId);
                writer.Raw(" " + Int((int)team.Leaning) + " " + Int(team.ProposeFromSeason) + " " + Int(team.Bank));
                writer.End();
            }

            writer.Count("pending", series.Pending.Count);
            foreach (var proposal in series.Pending)
            {
                writer.Begin("pending");
                writer.Field(proposal.TeamId);
                writer.Space();
                writer.Field(proposal.DimensionId);
                writer.Space();
                writer.Field(proposal.Value);
                writer.Raw(" " + proposal.Filed + " " + proposal.FeeCents.ToString(CultureInfo.InvariantCulture));
                writer.End();
            }

            writer.Count("ballot", series.Ballot.Count);
            foreach (var item in series.Ballot)
            {
                WriteItem(writer, item);
            }
        }
    }

    private static void WriteItem(CanonicalWriter writer, BallotItem item)
    {
        writer.Begin("item");
        writer.Field(item.Id);
        writer.Raw(" " + Int(item.Season) + " ");
        writer.Field(item.DimensionId);
        writer.Raw(" " + Int((int)item.Origin) + " " + item.Announced + " " + item.Deadline + " ");
        writer.Field(item.CurrentValue);
        writer.Space();
        writer.Field(item.ReasonKey);
        writer.End();
        writer.Count("args", item.ReasonArguments.Count);
        foreach (var (name, value) in item.ReasonArguments)
        {
            writer.Begin("arg");
            writer.Field(name);
            writer.Space();
            writer.Field(value);
            writer.End();
        }

        writer.Count("variants", item.Variants.Count);
        foreach (var variant in item.Variants)
        {
            writer.Begin("variant");
            writer.Field(variant.Id);
            writer.Space();
            writer.Field(variant.Value);
            writer.Raw(" " + Int(variant.ProposerTeamIds.Count));
            foreach (var proposer in variant.ProposerTeamIds)
            {
                writer.Space();
                writer.Field(proposer);
            }

            writer.End();
        }

        writer.Count("votes", item.Votes.Count);
        foreach (var vote in item.Votes)
        {
            writer.Begin("vote");
            writer.Field(vote.TeamId);
            writer.Space();
            writer.Field(vote.Option);
            writer.Raw(" " + Int(vote.Spent));
            writer.End();
        }

        if (item.Result is not { } result)
        {
            writer.Flag("resolved", false);
            return;
        }

        writer.Flag("resolved", true);
        writer.Begin("result");
        writer.Raw(Int((int)result.Outcome) + " ");
        writer.Field(result.WinningOption);
        writer.Space();
        writer.Field(result.ReasonKey);
        writer.Raw(" " + (result.PresidentDecided ? "1" : "0"));
        writer.End();
        writer.Count("tally", result.Tally.Count);
        foreach (var entry in result.Tally)
        {
            writer.Begin("t");
            writer.Field(entry.Option);
            writer.Raw(" " + Int(entry.Weight));
            writer.End();
        }

        writer.Count("stances", result.Stances.Count);
        foreach (var stance in result.Stances)
        {
            writer.Begin("stance");
            writer.Field(stance.TeamId);
            writer.Space();
            writer.Field(stance.Option);
            writer.Raw(" " + Int(stance.Weight) + " " + Int(stance.Spent) + " " + (stance.Banked ? "1" : "0") + " ");
            writer.Field(stance.ReasonKey);
            writer.End();
        }
    }

    private static void Pairs(CanonicalWriter writer, string label, IReadOnlyList<KeyValuePair<string, string>> pairs)
    {
        writer.Count(label, pairs.Count);
        foreach (var (dimension, value) in pairs)
        {
            writer.Begin("value");
            writer.Field(dimension);
            writer.Space();
            writer.Field(value);
            writer.End();
        }
    }

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);
}
