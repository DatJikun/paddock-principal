using System.Globalization;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Application.Racing;
using Paddock.Application.Regulation;
using Paddock.Domain.Contracts;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Newspaper;

/// <summary>The kinds of headline. The page picks an icon-free tile style from the kind; the words come from the messages.</summary>
public static class HeadlineKinds
{
    public const string Race = "race";

    public const string Own = "own";

    public const string Injury = "injury";

    public const string Retirement = "retirement";

    public const string Signing = "signing";

    public const string Team = "team";

    public const string Rules = "rules";
}

/// <summary>Where a headline leads: a route of the page (<c>race</c> carries the round, <c>driver</c> and <c>person</c> a person id, <c>inbox</c> an item id).</summary>
public static class LinkKinds
{
    public const string Race = "race";

    public const string Driver = "driver";

    public const string Person = "person";

    public const string Standings = "standings";

    public const string Inbox = "inbox";
}

/// <summary>The place a headline opens. <see cref="Id"/> is empty for a place with no id (the standings, the inbox list).</summary>
public sealed record HeadlineLink(string Kind, string Id);

/// <summary>
/// One tile of the paper. <see cref="Label"/>, <see cref="Title"/> and <see cref="Line"/> are translation messages (PP-021).
/// <see cref="Date"/> is the day it happened in invariant text. <see cref="Mine"/> marks news about the player's own team.
/// </summary>
public sealed record HeadlineView(
    string Id,
    string Kind,
    string Date,
    bool Mine,
    TranslationMessage Label,
    TranslationMessage Title,
    TranslationMessage? Line,
    HeadlineLink Link);

/// <summary>The paper: newest first, at most <see cref="NewspaperEstimates.MaxHeadlines"/> tiles.</summary>
public sealed record NewspaperView(IReadOnlyList<HeadlineView> Headlines);

/// <summary>ESTIMATES: how far back the paper looks and how much of it fits on the page. They are not calibrated.</summary>
public static class NewspaperEstimates
{
    /// <summary>Days back from today a piece of news stays in the paper.</summary>
    public const int WindowDays = 45;

    /// <summary>Tiles the bridge returns.</summary>
    public const int MaxHeadlines = 12;

    /// <summary>How many of those may be about a team hiring staff.</summary>
    public const int MaxTeamNews = 3;
}

/// <summary>The day and the layout of a round, as the stored season plan says.</summary>
public readonly record struct RaceDay(GameDate Date, string LayoutId);

/// <summary>Everything the paper reads. All of it is public knowledge or the player's own; nothing here is a rival's private state.</summary>
public sealed class NewspaperSources
{
    /// <summary>The world as stored. The paper reads results, contracts, retirements and ballots from its sections.</summary>
    public required WorldState World { get; init; }

    /// <summary>The current day.</summary>
    public required GameDate Today { get; init; }

    /// <summary>The player's team, or unassigned when the player runs none.</summary>
    public OrganizationId Own { get; init; }

    /// <summary>The negotiations of the contract book. Only the ones that ended in a signature are read.</summary>
    public ContractsSection? Contracts { get; init; }

    /// <summary>The player's inbox, to find the notice of a vote result.</summary>
    public IReadOnlyList<InboxItemView> Inbox { get; init; } = [];

    /// <summary>The day and layout of a round of a season, from the stored plan; null when the plan does not know it.</summary>
    public required Func<int, int, RaceDay?> RaceDays { get; init; }

    /// <summary>The public name of a circuit by layout id.</summary>
    public required Func<string, string> CircuitName { get; init; }
}

/// <summary>
/// The newspaper of the main screen (#324): headlines made of what the world already stored, with no state of their own, no
/// RNG and no change to the world (INV-005). Every source is public: the classification of a race, the report lines the race page
/// prints, a signed contract, a retirement, a counted vote. A rival's private data (attributes, offers, the terms of a contract,
/// open talks) is never an input, so it cannot reach a headline (INV-003).
/// </summary>
public static class NewspaperRead
{
    private static readonly string[] KindRank =
    [
        HeadlineKinds.Own,
        HeadlineKinds.Race,
        HeadlineKinds.Injury,
        HeadlineKinds.Signing,
        HeadlineKinds.Team,
        HeadlineKinds.Rules,
        HeadlineKinds.Retirement,
    ];

    public static NewspaperView Read(NewspaperSources sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var context = new Context(sources);
        var tiles = new List<HeadlineView>();
        Races(context, tiles);
        Signings(context, tiles);
        Retirements(context, tiles);
        Ballots(context, tiles);

        // The teams hire staff all the time; the paper keeps the newest few of those so they never crowd out a race or a driver.
        var surplus = tiles
            .Where(tile => tile.Kind == HeadlineKinds.Team)
            .OrderByDescending(tile => tile.Mine)
            .ThenByDescending(tile => tile.Date, StringComparer.Ordinal)
            .ThenBy(tile => tile.Id, StringComparer.Ordinal)
            .Skip(NewspaperEstimates.MaxTeamNews)
            .Select(tile => tile.Id)
            .ToHashSet(StringComparer.Ordinal);
        var ordered = tiles
            .Where(tile => tile.Kind != HeadlineKinds.Team || !surplus.Contains(tile.Id))
            .OrderByDescending(tile => tile.Date, StringComparer.Ordinal)
            .ThenBy(tile => Array.IndexOf(KindRank, tile.Kind))
            .ThenBy(tile => tile.Id, StringComparer.Ordinal)
            .Take(NewspaperEstimates.MaxHeadlines)
            .ToArray();
        return new NewspaperView(ordered);
    }

    // ---------------------------------------------------------------- races

    private static void Races(Context context, List<HeadlineView> tiles)
    {
        var archive = context.World.Section<RaceResultsSection>(RaceResultsSection.SectionName);
        if (archive is null)
        {
            return;
        }

        foreach (var race in archive.Races)
        {
            if (race.Season < context.Today.Year - 1 || context.Sources.RaceDays(race.Season, race.Round) is not { } day)
            {
                continue;
            }

            if (!context.InWindow(day.Date))
            {
                continue;
            }

            var circuit = context.Sources.CircuitName(day.LayoutId);
            var date = day.Date.ToString();
            var prefix = "race:" + race.Season.ToString(CultureInfo.InvariantCulture) + ":" + race.Round.ToString(CultureInfo.InvariantCulture);
            var thisSeason = race.Season == context.Today.Year;
            var raceLink = thisSeason
                ? new HeadlineLink(LinkKinds.Race, race.Round.ToString(CultureInfo.InvariantCulture))
                : null;

            var classified = race.Rows.Where(row => row.Classified).OrderBy(row => row.Position).ToArray();
            if (classified.Length > 0)
            {
                var winner = classified[0];
                TranslationMessage? podium = classified.Length >= 3
                    ? TranslationMessage.Of(
                        NewspaperKeys.RacePodium,
                        ("second", context.PersonName(classified[1].DriverId)),
                        ("third", context.PersonName(classified[2].DriverId)))
                    : null;
                tiles.Add(new HeadlineView(
                    prefix + ":win",
                    HeadlineKinds.Race,
                    date,
                    context.IsOwnTeam(winner.TeamId),
                    TranslationMessage.Of(NewspaperKeys.KindRace),
                    TranslationMessage.Of(
                        NewspaperKeys.RaceWin,
                        ("driver", context.PersonName(winner.DriverId)),
                        ("circuit", circuit)),
                    podium,
                    raceLink ?? new HeadlineLink(LinkKinds.Driver, winner.DriverId)));
            }

            Own(context, race, circuit, date, prefix, raceLink, tiles);
            Injuries(context, race, circuit, date, prefix, raceLink, tiles);
        }
    }

    private static void Own(
        Context context,
        StoredRace race,
        string circuit,
        string date,
        string prefix,
        HeadlineLink? raceLink,
        List<HeadlineView> tiles)
    {
        if (!context.Sources.Own.IsAssigned)
        {
            return;
        }

        var mine = race.Rows.Where(row => context.IsOwnTeam(row.TeamId)).OrderBy(row => row.Position).ToArray();
        if (mine.Length == 0)
        {
            return;
        }

        var best = mine.FirstOrDefault(row => row.Classified) ?? mine[0];
        var team = context.TeamName(best.TeamId);
        var link = raceLink ?? new HeadlineLink(LinkKinds.Driver, best.DriverId);
        var driver = context.PersonName(best.DriverId);
        TranslationMessage? line = null;
        TranslationMessage title;
        if (best.Classified)
        {
            title = TranslationMessage.Of(
                NewspaperKeys.RaceOwn,
                ("team", team),
                ("circuit", circuit),
                ("driver", driver),
                ("position", best.Position.ToString(CultureInfo.InvariantCulture)));
            var points = mine.Sum(row => decimal.TryParse(row.Points, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m);
            if (points > 0m)
            {
                line = TranslationMessage.Of(NewspaperKeys.RaceOwnPoints, ("count", points.ToString(CultureInfo.InvariantCulture)));
            }
        }
        else
        {
            title = TranslationMessage.Of(
                NewspaperKeys.RaceOwnOut,
                ("team", team),
                ("circuit", circuit),
                ("driver", driver));
        }

        tiles.Add(new HeadlineView(
            prefix + ":own",
            HeadlineKinds.Own,
            date,
            true,
            TranslationMessage.Of(NewspaperKeys.KindOwn),
            title,
            line,
            link));
    }

    private static void Injuries(
        Context context,
        StoredRace race,
        string circuit,
        string date,
        string prefix,
        HeadlineLink? raceLink,
        List<HeadlineView> tiles)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in race.Sections)
        {
            if (RaceSpy.IsSpy(section))
            {
                continue;
            }

            foreach (var line in section.Lines)
            {
                var key = InjuryKey(line.Key);
                if (key is null)
                {
                    continue;
                }

                var arg = line.Args.FirstOrDefault(item => item.Name == "driver");
                if (arg is null || !arg.Value.StartsWith(RaceArchive.PersonPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var driverId = arg.Value[RaceArchive.PersonPrefix.Length..];
                if (!seen.Add(driverId))
                {
                    continue;
                }

                if (key is NewspaperKeys.InjuryFatal or NewspaperKeys.InjuryCareerEnding)
                {
                    // The tile already says the career is over; the retirement that follows would be a second one for the same man.
                    context.Ended.Add(driverId);
                }

                var row = race.Rows.FirstOrDefault(item => item.DriverId == driverId);
                tiles.Add(new HeadlineView(
                    prefix + ":injury:" + driverId,
                    HeadlineKinds.Injury,
                    date,
                    row is not null && context.IsOwnTeam(row.TeamId),
                    TranslationMessage.Of(NewspaperKeys.KindInjury),
                    TranslationMessage.Of(key, ("driver", context.PersonName(driverId)), ("circuit", circuit)),
                    row is null ? null : TranslationMessage.Of(NewspaperKeys.LineTeam, ("team", context.TeamName(row.TeamId))),
                    raceLink ?? new HeadlineLink(LinkKinds.Driver, driverId)));
            }
        }
    }

    private static string? InjuryKey(string reportKey) => reportKey switch
    {
        RaceReportKeys.InjuryLight => NewspaperKeys.InjuryLight,
        RaceReportKeys.InjurySerious => NewspaperKeys.InjurySerious,
        RaceReportKeys.InjuryCareerEnding => NewspaperKeys.InjuryCareerEnding,
        RaceReportKeys.InjuryFatal => NewspaperKeys.InjuryFatal,
        _ => null,
    };

    // ---------------------------------------------------------------- contracts

    private static void Signings(Context context, List<HeadlineView> tiles)
    {
        if (context.Sources.Contracts is not { } section)
        {
            return;
        }

        foreach (var negotiation in section.Negotiations)
        {
            if (negotiation.Status != NegotiationStatus.Agreed
                || negotiation.ClosedOn is not { } closed
                || !context.InWindow(closed)
                || !context.Persons.TryGetValue(negotiation.Counterparty.Value, out var person))
            {
                continue;
            }

            var mine = context.IsOwnTeam(negotiation.Proposer.Value);
            var team = context.TeamName(negotiation.Proposer.Value);
            var date = closed.ToString();
            var id = "signing:" + negotiation.Id;
            if (negotiation.Subject.Kind == NegotiationSubjectKind.StaffRole)
            {
                // A rival's key hire is announced; the page has no profile of rival staff, so it opens the standings.
                tiles.Add(new HeadlineView(
                    id,
                    HeadlineKinds.Team,
                    date,
                    mine,
                    TranslationMessage.Of("staff.role." + negotiation.Subject.StaffRole),
                    TranslationMessage.Of(NewspaperKeys.TeamHired, ("team", team), ("person", person.Name)),
                    null,
                    mine ? new HeadlineLink(LinkKinds.Person, person.Id.Value) : new HeadlineLink(LinkKinds.Standings, "")));
                continue;
            }

            // A rival's driver is news only when the signed contract is a race seat the world still holds: an academy junior is no news.
            var contract = negotiation.SignedContract is { } signed ? context.Contract(signed.Value) : null;
            if (!mine && (contract is null || !contract.Role.IsDriver || contract.Role.Seat == SeatStatus.Reserve))
            {
                continue;
            }

            if (negotiation.RenewalOf is not null)
            {
                tiles.Add(new HeadlineView(
                    id,
                    HeadlineKinds.Signing,
                    date,
                    mine,
                    TranslationMessage.Of(NewspaperKeys.KindSigning),
                    TranslationMessage.Of(NewspaperKeys.SigningRenewed, ("driver", person.Name), ("team", team)),
                    null,
                    new HeadlineLink(LinkKinds.Driver, person.Id.Value)));
                continue;
            }

            var from = context.PreviousTeam(person.Id.Value, negotiation.Proposer.Value, contract?.Start ?? closed);
            tiles.Add(new HeadlineView(
                id,
                HeadlineKinds.Signing,
                date,
                mine,
                TranslationMessage.Of(NewspaperKeys.KindSigning),
                TranslationMessage.Of(NewspaperKeys.SigningDriver, ("driver", person.Name), ("team", team)),
                from is null ? null : TranslationMessage.Of(NewspaperKeys.SigningFrom, ("team", context.TeamName(from))),
                new HeadlineLink(LinkKinds.Driver, person.Id.Value)));
        }
    }

    private static void Retirements(Context context, List<HeadlineView> tiles)
    {
        foreach (var person in context.Persons.Values)
        {
            if (person.RetiredOn is not { } retired
                || !context.InWindow(retired)
                || context.Ended.Contains(person.Id.Value)
                || !context.WasADriver(person))
            {
                continue;
            }

            var age = retired.Year - person.BirthDate.Year
                - (retired.Month < person.BirthDate.Month || (retired.Month == person.BirthDate.Month && retired.Day < person.BirthDate.Day) ? 1 : 0);
            tiles.Add(new HeadlineView(
                "retired:" + person.Id.Value,
                HeadlineKinds.Retirement,
                retired.ToString(),
                false,
                TranslationMessage.Of(NewspaperKeys.KindRetirement),
                TranslationMessage.Of(NewspaperKeys.Retired, ("driver", person.Name)),
                TranslationMessage.Of(NewspaperKeys.RetiredAge, ("count", age.ToString(CultureInfo.InvariantCulture))),
                new HeadlineLink(LinkKinds.Driver, person.Id.Value)));
        }
    }

    // ---------------------------------------------------------------- rules

    private static void Ballots(Context context, List<HeadlineView> tiles)
    {
        var regulations = context.World.Section<RegulationsSection>(RegulationsSection.SectionName);
        if (regulations is null)
        {
            return;
        }

        foreach (var series in regulations.Series)
        {
            foreach (var item in series.Ballot)
            {
                if (item.Result is not { } result
                    || !context.InWindow(item.Deadline)
                    || Paddock.Simulation.Regulation.CalendarPolicy.IsCalendarDimension(item.DimensionId))
                {
                    continue;
                }

                string value;
                string titleKey;
                switch (result.Outcome)
                {
                    case BallotOutcome.Adopted when item.VariantOf(result.WinningOption) is { } variant:
                        value = variant.Value;
                        titleKey = NewspaperKeys.RulesAdopted;
                        break;
                    case BallotOutcome.Rejected:
                        value = item.CurrentValue;
                        titleKey = NewspaperKeys.RulesKept;
                        break;
                    default:
                        continue;
                }

                var notice = context.Sources.Inbox.FirstOrDefault(entry =>
                    entry.Kind == RegulationKeys.ResultKind
                    && entry.Subject.Parameters.TryGetValue("item", out var noticed)
                    && string.Equals(noticed, item.Id, StringComparison.Ordinal));
                tiles.Add(new HeadlineView(
                    "rules:" + series.SeriesId + ":" + item.Id,
                    HeadlineKinds.Rules,
                    item.Deadline.ToString(),
                    false,
                    TranslationMessage.Of(RegulationKeys.DimensionKey(item.DimensionId)),
                    TranslationMessage.Of(titleKey, ("season", (item.Season + 1).ToString(CultureInfo.InvariantCulture))),
                    TranslationMessage.Of(RegulationKeys.ValueKey(item.DimensionId, value)),
                    new HeadlineLink(LinkKinds.Inbox, notice?.Id ?? "")));
            }
        }
    }

    private sealed class Context
    {
        private readonly Dictionary<string, Organization> _organizations;
        private readonly Dictionary<string, Contract> _contracts;
        private readonly HashSet<string> _racedIds;

        public Context(NewspaperSources sources)
        {
            Sources = sources;
            World = sources.World;
            Persons = World.Persons.ToDictionary(person => person.Id.Value, StringComparer.Ordinal);
            _organizations = World.Organizations.ToDictionary(organization => organization.Id.Value, StringComparer.Ordinal);
            _contracts = World.Contracts.ToDictionary(contract => contract.Id.Value, StringComparer.Ordinal);
            _racedIds = new HashSet<string>(StringComparer.Ordinal);
            if (World.Section<RaceResultsSection>(RaceResultsSection.SectionName) is { } archive)
            {
                foreach (var race in archive.Races)
                {
                    foreach (var row in race.Rows)
                    {
                        _racedIds.Add(row.DriverId);
                    }
                }
            }
        }

        public NewspaperSources Sources { get; }

        public WorldState World { get; }

        public GameDate Today => Sources.Today;

        public Dictionary<string, Person> Persons { get; }

        /// <summary>People a tile has already told ended their career (a fatal or career-ending injury).</summary>
        public HashSet<string> Ended { get; } = new(StringComparer.Ordinal);

        public bool InWindow(GameDate date) =>
            date <= Sources.Today && date >= Sources.Today.AddDays(-NewspaperEstimates.WindowDays);

        public bool IsOwnTeam(string teamId) =>
            Sources.Own.IsAssigned && string.Equals(Sources.Own.Value, teamId, StringComparison.Ordinal);

        public string PersonName(string id) => Persons.TryGetValue(id, out var person) ? person.Name : id;

        public string TeamName(string id) => _organizations.TryGetValue(id, out var organization) ? organization.NameOn(Sources.Today) : id;

        public Contract? Contract(string id) => _contracts.GetValueOrDefault(id);

        /// <summary>A person the public has seen race: in a stored result, or holding a race seat. A pool junior of a rival is neither.</summary>
        public bool WasADriver(Person person) =>
            _racedIds.Contains(person.Id.Value)
            || _contracts.Values.Any(contract => contract.PersonId == person.Id && contract.Role.IsDriver && contract.Role.Seat != SeatStatus.Reserve);

        /// <summary>The team whose race-seat contract of the person ended last before <paramref name="before"/>, other than <paramref name="except"/>.</summary>
        public string? PreviousTeam(string personId, string except, GameDate before)
        {
            Contract? latest = null;
            foreach (var contract in _contracts.Values)
            {
                if (contract.PersonId.Value != personId
                    || !contract.Role.IsDriver
                    || contract.Role.Seat == SeatStatus.Reserve
                    || contract.OrganizationId.Value == except
                    || contract.End >= before)
                {
                    continue;
                }

                if (latest is null || contract.End > latest.End)
                {
                    latest = contract;
                }
            }

            return latest?.OrganizationId.Value;
        }
    }
}
