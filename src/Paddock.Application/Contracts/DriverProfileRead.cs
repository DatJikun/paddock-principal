using System.Globalization;
using Paddock.Application.Racing;
using Paddock.Domain.Contracts;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Contracts;

/// <summary>
/// What this career's archive says about one driver in one season: the races he started and how they ended.
/// Only finished rounds of this career are counted, so a driver who was never in a result has no row.
/// <see cref="Place"/> is his place in the championship that season and <see cref="Points"/> his points, both as invariant text
/// (#331). A past season has no stored table, so both come from the plain totals of that season (<see cref="SeasonPlaces"/>).
/// </summary>
public sealed record DriverSeasonView(
    int Season,
    string TeamId,
    string TeamName,
    int Starts,
    int Wins,
    int Podiums,
    int Retirements,
    int? Best,
    int? Place = null,
    string? Points = null);

/// <summary>The terms of a contract the observer's own team holds. Salary is a nominal placeholder, not a calibrated figure.</summary>
public sealed record DriverContractView(string ContractId, string Seat, string Start, string End, long Salary, int? OptionYears, string? OptionDeadline, long? ReleaseAmount);

/// <summary>
/// One driver as the observer's team may read him. Attributes and potential are the bands the team believes (INV-003), never
/// the hidden number. The contract terms are filled only for a driver the observer's team employs.
/// </summary>
public sealed record DriverProfileView(
    bool Found,
    string PersonId,
    string Name,
    string Nationality,
    int Age,
    string? OrganizationId,
    string? OrganizationName,
    bool Own,
    bool FreeAgent,
    string? Seat,
    string? ContractEnd,
    DriverContractView? Contract,
    IReadOnlyList<KnownAttributeView> Attributes,
    KnownAttributeView? Potential,
    IReadOnlyList<DriverSeasonView> Seasons,
    bool Female = false,
    int? Overall = null,
    DriverContractView? Upcoming = null,
    string? UpcomingOrganizationName = null,
    string? UpcomingStart = null,
    SalaryGuideView? SalaryGuide = null);

/// <summary>Reads one driver for the profile and the comparison. A pure query: no state change and no random number (INV-005).</summary>
public static class DriverProfileRead
{
    /// <summary>The answer for a driver the observer cannot read about.</summary>
    public static DriverProfileView None(string personId) =>
        new(false, personId, "", "", 0, null, null, false, false, null, null, null, [], null, []);

    /// <param name="earlier">
    /// Seasons the driver raced before this career began, read by the host from local data (plain counts, #265). They come first
    /// in <see cref="DriverProfileView.Seasons"/>; a season this career has already archived wins over them.
    /// </param>
    /// <param name="standings">
    /// The championship of the season in progress, when the career has one. Its place and points replace the plain totals of that
    /// season, so the profile and the standings agree (#331).
    /// </param>
    public static DriverProfileView Of(
        WorldState world,
        OrganizationId observer,
        GameDate today,
        string personId,
        IReadOnlyList<DriverSeasonView>? earlier = null,
        StandingsView? standings = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!observer.IsAssigned)
        {
            throw new ArgumentException("Observer id is unassigned.", nameof(observer));
        }

        var person = world.Persons.FirstOrDefault(item => string.Equals(item.Id.Value, personId, StringComparison.Ordinal));
        if (person is null || !person.Roles.Any(role => role.IsDriver))
        {
            return None(personId ?? "");
        }

        Contract? contract = null;
        foreach (var candidate in world.Contracts)
        {
            if (candidate.PersonId == person.Id && candidate.Role.IsDriver && candidate.IsActiveOn(today))
            {
                contract = candidate;
                break;
            }
        }

        // A contract signed for later (a pre-contract or a renewal) starts after today. The profile must show it, or a signed
        // renewal looks as if nothing happened (#265).
        Contract? upcoming = null;
        Contract? last = contract;
        foreach (var candidate in world.Contracts)
        {
            if (candidate.PersonId != person.Id || !candidate.Role.IsDriver || !candidate.Exclusive || candidate.End < today)
            {
                continue;
            }

            if (candidate.Start > today && (upcoming is null || candidate.Start < upcoming.Start))
            {
                upcoming = candidate;
            }

            if (last is null || candidate.End > last.End)
            {
                last = candidate;
            }
        }

        var own = contract is not null && contract.OrganizationId == observer;
        IReadOnlyList<KnownAttributeView> attributes = [];
        KnownAttributeView? potential = null;
        if (world.KnowledgeOf(observer, person.Id) is PersonKnowledgeView belief)
        {
            attributes = belief.Attributes
                .Select(attribute => new KnownAttributeView(attribute.Key, attribute.Band.Low, attribute.Band.High))
                .ToArray();
            potential = belief.Potential is { } band ? new KnownAttributeView("potential", band.Low, band.High) : null;
        }

        DriverContractView? terms = own && contract is not null ? ViewOf(contract) : null;
        DriverContractView? next = upcoming is not null && upcoming.OrganizationId == observer ? ViewOf(upcoming) : null;

        return new DriverProfileView(
            true,
            person.Id.Value,
            person.Name,
            person.Nationality,
            AgeOn(person.BirthDate, today),
            contract?.OrganizationId.Value,
            contract is null ? null : world.GetOrganization(contract.OrganizationId).NameOn(today),
            own,
            contract is null,
            contract?.Role.Seat.ToString(),
            last?.End.ToString(),
            terms,
            attributes,
            potential,
            WithStandings(Merge(earlier, Seasons(world, person.Id.Value)), standings, person.Id.Value),
            person.IsFemale,
            PeopleViews.Overall(NegotiationSubject.DriverSeat, world.KnowledgeOf(observer, person.Id)),
            next,
            upcoming is null ? null : world.GetOrganization(upcoming.OrganizationId).NameOn(upcoming.Start),
            upcoming?.Start.ToString());
    }

    private static DriverContractView ViewOf(Contract contract) =>
        new(
            contract.Id.Value,
            contract.Role.Seat.ToString(),
            contract.Start.ToString(),
            contract.End.ToString(),
            contract.Salary,
            contract.Option is { } option ? option.ExtraYears : null,
            contract.Option is { } held ? held.Deadline.ToString() : null,
            contract.ReleaseClause is { } release ? release.Amount : null);

    private static IReadOnlyList<DriverSeasonView> Merge(IReadOnlyList<DriverSeasonView>? earlier, IReadOnlyList<DriverSeasonView> archived)
    {
        if (earlier is null || earlier.Count == 0)
        {
            return archived;
        }

        var seasons = new SortedDictionary<int, DriverSeasonView>();
        foreach (var row in earlier)
        {
            seasons[row.Season] = row;
        }

        foreach (var row in archived)
        {
            seasons[row.Season] = row;
        }

        return seasons.Values.ToArray();
    }

    /// <summary>The season in progress takes the official place and points from the standings, when the driver is in them.</summary>
    private static IReadOnlyList<DriverSeasonView> WithStandings(IReadOnlyList<DriverSeasonView> seasons, StandingsView? standings, string personId)
    {
        var row = standings?.Drivers.FirstOrDefault(item => string.Equals(item.Id, personId, StringComparison.Ordinal));
        if (standings is null || row is null)
        {
            return seasons;
        }

        return seasons
            .Select(season => season.Season == standings.Season ? season with { Place = row.Position, Points = row.Points } : season)
            .ToArray();
    }

    private static IReadOnlyList<DriverSeasonView> Seasons(WorldState world, string personId)
    {
        var archive = world.Section<RaceResultsSection>(RaceResultsSection.SectionName);
        if (archive is null)
        {
            return [];
        }

        // Every driver of every season is tallied, so the place is read over the whole field (#331).
        var tallies = new Dictionary<(int Season, string DriverId), Tally>();
        foreach (var race in archive.Races)
        {
            foreach (var row in race.Rows)
            {
                var key = (race.Season, row.DriverId);
                if (!tallies.TryGetValue(key, out var tally))
                {
                    tally = new Tally();
                    tallies.Add(key, tally);
                }

                tally.Add(row);
            }
        }

        var places = SeasonPlaces.Of(tallies.Select(entry => new SeasonTotal(
            entry.Key.Season,
            entry.Key.DriverId,
            entry.Value.Points,
            entry.Value.Wins,
            entry.Value.Podiums)));

        var views = new List<DriverSeasonView>();
        foreach (var ((season, driver), tally) in tallies.OrderBy(entry => entry.Key.Season))
        {
            if (!string.Equals(driver, personId, StringComparison.Ordinal))
            {
                continue;
            }

            var team = world.Organizations.FirstOrDefault(item => string.Equals(item.Id.Value, tally.TeamId, StringComparison.Ordinal));
            views.Add(new DriverSeasonView(
                season,
                tally.TeamId,
                team?.Names[^1].Name ?? tally.TeamId,
                tally.Starts,
                tally.Wins,
                tally.Podiums,
                tally.Retirements,
                tally.Best,
                places[(season, driver)],
                tally.Points.ToString("0.##########", CultureInfo.InvariantCulture)));
        }

        return views;
    }

    private static int AgeOn(GameDate born, GameDate on)
    {
        var age = on.Year - born.Year;
        if (on.Month < born.Month || (on.Month == born.Month && on.Day < born.Day))
        {
            age--;
        }

        return Math.Max(0, age);
    }

    private sealed class Tally
    {
        public string TeamId { get; private set; } = "";

        public int Starts { get; private set; }

        public int Wins { get; private set; }

        public int Podiums { get; private set; }

        public int Retirements { get; private set; }

        public int? Best { get; private set; }

        public decimal Points { get; private set; }

        public void Add(RaceResultRow row)
        {
            TeamId = row.TeamId;
            Starts++;
            Points += decimal.TryParse(row.Points, NumberStyles.Number, CultureInfo.InvariantCulture, out var points) ? points : 0m;
            if (!row.Classified)
            {
                Retirements++;
                return;
            }

            if (row.Position == 1)
            {
                Wins++;
            }

            if (row.Position <= 3)
            {
                Podiums++;
            }

            Best = Best is int best ? Math.Min(best, row.Position) : row.Position;
        }
    }
}
