using System.Globalization;
using System.Text;
using Paddock.Data.Historical;

namespace Paddock.DataPipeline;

/// <summary>
/// Builds the schedule of real drivers from normalized race results.
/// Edge cases:
/// - A start is any result that is not a non-start (FinishStatus: Withdrew, Did not start,
///   Did not qualify, Did not prequalify, 107% Rule). A retirement counts. A non-start does not.
/// - One stint is one season plus one constructor. firstRound and lastRound span every start,
///   so a return to the same constructor in that season stays a single stint. Each departure
///   is its own team-change event.
/// - Role race is more than PeopleScheduleRules.SubstituteMaxStarts for that pair.
///   Role substitute is that many or fewer. Shared-drive rows are kept as role shared_drive
///   and are not folded into the seat, even when there are only a few of them.
/// - A driver with a single Grand Prix start is kept (as a substitute, or as a shared drive).
/// - Indianapolis 500 rounds, 1950–1960, are excluded from stints and listed on the driver.
///   That exclusion wins when the same row is also a shared drive.
/// - Shared drives and Indianapolis starts do not create team changes.
/// - A team change inside one season records the round of the first start for the new constructor.
///   A change between seasons records the arrival season and no round.
/// - Two driver ids that normalize to the same name, or that share a birth date and family name,
///   are listed as suspects and are not merged.
/// - Pool entry uses PeopleScheduleRules. The 1980 half-year split is a placeholder until
///   calibration (ROADMAP open question 4).
/// Output is sorted, so input order does not change the report.
/// </summary>
public static class PeopleScheduleBuilder
{
    public static PeopleScheduleReport Build(NormalizedHistoricalData data, decimal poolLeadYears)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!PeopleScheduleRules.IsValidLead(poolLeadYears))
        {
            throw new ArgumentOutOfRangeException(
                nameof(poolLeadYears),
                "Pool lead years must be a non-negative whole or half year.");
        }

        var races = new Dictionary<(int Season, int Round), HistoricalRace>();
        foreach (var race in data.Races.Races)
        {
            races[(race.Season, race.Round)] = race;
        }

        var bios = new Dictionary<string, HistoricalDriver>(StringComparer.Ordinal);
        foreach (var driver in data.Drivers.Drivers)
        {
            bios[driver.DriverId] = driver;
        }

        var regular = new Dictionary<StintKey, RoundSpan>();
        var shared = new Dictionary<StintKey, RoundSpan>();
        var indy = new Dictionary<string, List<IndianapolisAppearance>>(StringComparer.Ordinal);
        var timeline = new Dictionary<string, HashSet<StartPoint>>(StringComparer.Ordinal);

        foreach (var row in data.Results.Results)
        {
            if (!races.TryGetValue((row.Season, row.Round), out var race))
            {
                throw new InvalidDataException(
                    "Result "
                    + row.Season.ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + row.Round.ToString(CultureInfo.InvariantCulture)
                    + " "
                    + row.DriverId
                    + " has no race.");
            }

            if (FinishStatus.Classify(row.Status) == FinishStatus.Kind.NonStart)
            {
                continue;
            }

            // Indianapolis exclusion wins over the shared-drive role.
            if (race.IsIndianapolis500)
            {
                AddIndy(indy, row);
                continue;
            }

            if (row.IsSharedDrive)
            {
                AddSpan(shared, row);
                continue;
            }

            AddSpan(regular, row);
            AddStart(timeline, row);
        }

        var driverIds = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var key in regular.Keys)
        {
            driverIds.Add(key.DriverId);
        }

        foreach (var key in shared.Keys)
        {
            driverIds.Add(key.DriverId);
        }

        foreach (var driverId in indy.Keys)
        {
            driverIds.Add(driverId);
        }

        var regularByDriver = regular.ToLookup(pair => pair.Key.DriverId, StringComparer.Ordinal);
        var sharedByDriver = shared.ToLookup(pair => pair.Key.DriverId, StringComparer.Ordinal);
        var drivers = new List<ScheduledDriver>(driverIds.Count);
        var changes = new List<TeamChangeEvent>();
        foreach (var driverId in driverIds)
        {
            bios.TryGetValue(driverId, out var bio);
            var stints = new List<DriverStint>();
            foreach (var pair in regularByDriver[driverId])
            {
                stints.Add(ToStint(pair.Key, pair.Value, sharedDrive: false));
            }

            foreach (var pair in sharedByDriver[driverId])
            {
                stints.Add(ToStint(pair.Key, pair.Value, sharedDrive: true));
            }

            stints.Sort(CompareStints);

            int? firstSeason = stints.Count == 0 ? null : stints.Min(stint => stint.Season);
            int? lastSeason = stints.Count == 0 ? null : stints.Max(stint => stint.Season);
            var born = PeopleScheduleRules.BornYear(bio?.DateOfBirth);
            int? poolEntry = firstSeason is null
                ? null
                : PeopleScheduleRules.EntryYear(firstSeason.Value, born, poolLeadYears);

            var appearances = indy.TryGetValue(driverId, out var listed)
                ? listed
                    .OrderBy(appearance => appearance.Season)
                    .ThenBy(appearance => appearance.Round)
                    .ThenBy(appearance => appearance.ConstructorId, StringComparer.Ordinal)
                    .ToList()
                : [];

            drivers.Add(new ScheduledDriver(
                driverId,
                born,
                bio?.Nationality,
                firstSeason,
                lastSeason,
                poolEntry,
                stints,
                appearances));

            if (timeline.TryGetValue(driverId, out var starts))
            {
                changes.AddRange(ChangesFor(driverId, starts));
            }
        }

        changes.Sort(CompareChanges);
        var singles = drivers
            .Where(driver => driver.Stints.Sum(stint => stint.Starts) == 1)
            .Select(driver => driver.DriverId)
            .ToList();

        return new PeopleScheduleReport(
            poolLeadYears,
            PeopleScheduleRules.PoolDebutSplitSeason,
            drivers,
            changes,
            FindSuspects(data.Drivers.Drivers),
            singles);
    }

    private static void AddSpan(Dictionary<StintKey, RoundSpan> spans, HistoricalResult row)
    {
        var key = new StintKey(row.DriverId, row.Season, row.ConstructorId);
        if (!spans.TryGetValue(key, out var span))
        {
            span = new RoundSpan();
            spans[key] = span;
        }

        span.Add(row.Round);
    }

    private static void AddStart(Dictionary<string, HashSet<StartPoint>> timeline, HistoricalResult row)
    {
        if (!timeline.TryGetValue(row.DriverId, out var starts))
        {
            starts = [];
            timeline[row.DriverId] = starts;
        }

        starts.Add(new StartPoint(row.Season, row.Round, row.ConstructorId));
    }

    private static void AddIndy(Dictionary<string, List<IndianapolisAppearance>> indy, HistoricalResult row)
    {
        if (!indy.TryGetValue(row.DriverId, out var appearances))
        {
            appearances = [];
            indy[row.DriverId] = appearances;
        }

        var appearance = new IndianapolisAppearance(row.Season, row.Round, row.ConstructorId);
        if (!appearances.Contains(appearance))
        {
            appearances.Add(appearance);
        }
    }

    private static DriverStint ToStint(StintKey key, RoundSpan span, bool sharedDrive)
    {
        string role;
        if (sharedDrive)
        {
            role = ScheduleRoles.SharedDrive;
        }
        else if (span.Starts <= PeopleScheduleRules.SubstituteMaxStarts)
        {
            role = ScheduleRoles.Substitute;
        }
        else
        {
            role = ScheduleRoles.Race;
        }

        return new DriverStint(key.Season, key.ConstructorId, span.First, span.Last, span.Starts, role);
    }

    private static List<TeamChangeEvent> ChangesFor(string driverId, HashSet<StartPoint> starts)
    {
        var ordered = starts.ToList();
        ordered.Sort();
        var events = new List<TeamChangeEvent>();
        for (var i = 1; i < ordered.Count; i++)
        {
            var previous = ordered[i - 1];
            var current = ordered[i];
            if (string.Equals(previous.ConstructorId, current.ConstructorId, StringComparison.Ordinal))
            {
                continue;
            }

            int? round = current.Season == previous.Season ? current.Round : null;
            events.Add(new TeamChangeEvent(
                current.Season,
                driverId,
                previous.ConstructorId,
                current.ConstructorId,
                round));
        }

        return events;
    }

    private static List<DuplicateDriverSuspect> FindSuspects(IReadOnlyList<HistoricalDriver> drivers)
    {
        var found = new List<DuplicateDriverSuspect>();
        for (var i = 0; i < drivers.Count; i++)
        {
            for (var j = i + 1; j < drivers.Count; j++)
            {
                var reason = SuspectReason(drivers[i], drivers[j]);
                if (reason is null)
                {
                    continue;
                }

                var left = drivers[i].DriverId;
                var right = drivers[j].DriverId;
                if (string.CompareOrdinal(left, right) > 0)
                {
                    (left, right) = (right, left);
                }

                found.Add(new DuplicateDriverSuspect(left, right, reason));
            }
        }

        found.Sort(static (left, right) =>
        {
            var id = string.CompareOrdinal(left.DriverId, right.DriverId);
            if (id != 0)
            {
                return id;
            }

            var other = string.CompareOrdinal(left.OtherDriverId, right.OtherDriverId);
            if (other != 0)
            {
                return other;
            }

            return string.CompareOrdinal(left.Reason, right.Reason);
        });
        return found;
    }

    private static string? SuspectReason(HistoricalDriver left, HistoricalDriver right)
    {
        var leftGiven = NormalizeName(left.GivenName);
        var rightGiven = NormalizeName(right.GivenName);
        var leftFamily = NormalizeName(left.FamilyName);
        var rightFamily = NormalizeName(right.FamilyName);
        var sameGiven = leftGiven is not null
            && rightGiven is not null
            && string.Equals(leftGiven, rightGiven, StringComparison.Ordinal);
        var sameFamily = leftFamily is not null
            && rightFamily is not null
            && string.Equals(leftFamily, rightFamily, StringComparison.Ordinal);

        if (sameGiven && sameFamily)
        {
            return DuplicateSuspectReasons.SameName;
        }

        if (sameFamily
            && !string.IsNullOrEmpty(left.DateOfBirth)
            && string.Equals(left.DateOfBirth, right.DateOfBirth, StringComparison.Ordinal))
        {
            return DuplicateSuspectReasons.SameBirthAndFamily;
        }

        return null;
    }

    /// <summary>
    /// Case, accent, hyphen, and apostrophe insensitive. Empty after that is unknown.
    /// </summary>
    private static string? NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var decomposed = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character is '\'' or '\u2019' or '-')
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static int CompareStints(DriverStint left, DriverStint right)
    {
        var season = left.Season.CompareTo(right.Season);
        if (season != 0)
        {
            return season;
        }

        var round = left.FirstRound.CompareTo(right.FirstRound);
        if (round != 0)
        {
            return round;
        }

        var constructor = string.CompareOrdinal(left.ConstructorId, right.ConstructorId);
        if (constructor != 0)
        {
            return constructor;
        }

        return string.CompareOrdinal(left.Role, right.Role);
    }

    private static int CompareChanges(TeamChangeEvent left, TeamChangeEvent right)
    {
        var season = left.Season.CompareTo(right.Season);
        if (season != 0)
        {
            return season;
        }

        var round = (left.Round ?? -1).CompareTo(right.Round ?? -1);
        if (round != 0)
        {
            return round;
        }

        var driver = string.CompareOrdinal(left.DriverId, right.DriverId);
        if (driver != 0)
        {
            return driver;
        }

        var from = string.CompareOrdinal(left.FromConstructorId, right.FromConstructorId);
        if (from != 0)
        {
            return from;
        }

        return string.CompareOrdinal(left.ToConstructorId, right.ToConstructorId);
    }

    private readonly record struct StintKey(string DriverId, int Season, string ConstructorId);

    private readonly record struct StartPoint(int Season, int Round, string ConstructorId) : IComparable<StartPoint>
    {
        public int CompareTo(StartPoint other)
        {
            var season = Season.CompareTo(other.Season);
            if (season != 0)
            {
                return season;
            }

            var round = Round.CompareTo(other.Round);
            if (round != 0)
            {
                return round;
            }

            return string.CompareOrdinal(ConstructorId, other.ConstructorId);
        }
    }

    private sealed class RoundSpan
    {
        private readonly HashSet<int> _rounds = [];

        public int First { get; private set; } = int.MaxValue;

        public int Last { get; private set; } = int.MinValue;

        public int Starts => _rounds.Count;

        public void Add(int round)
        {
            if (!_rounds.Add(round))
            {
                return;
            }

            if (round < First)
            {
                First = round;
            }

            if (round > Last)
            {
                Last = round;
            }
        }
    }
}
