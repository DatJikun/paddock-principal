using System.Globalization;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Contracts;

/// <summary>
/// What this career's archive says about one driver in one season: the races he started and how they ended.
/// Only finished rounds of this career are counted, so a driver who was never in a result has no row.
/// </summary>
public sealed record DriverSeasonView(int Season, string TeamId, string TeamName, int Starts, int Wins, int Podiums, int Retirements, int? Best);

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
    IReadOnlyList<DriverSeasonView> Seasons);

/// <summary>Reads one driver for the profile and the comparison. A pure query: no state change and no random number (INV-005).</summary>
public static class DriverProfileRead
{
    /// <summary>The answer for a driver the observer cannot read about.</summary>
    public static DriverProfileView None(string personId) =>
        new(false, personId, "", "", 0, null, null, false, false, null, null, null, [], null, []);

    public static DriverProfileView Of(WorldState world, OrganizationId observer, GameDate today, string personId)
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

        DriverContractView? terms = null;
        if (own && contract is not null)
        {
            terms = new DriverContractView(
                contract.Id.Value,
                contract.Role.Seat.ToString(),
                contract.Start.ToString(),
                contract.End.ToString(),
                contract.Salary,
                contract.Option is { } option ? option.ExtraYears : null,
                contract.Option is { } held ? held.Deadline.ToString() : null,
                contract.ReleaseClause is { } release ? release.Amount : null);
        }

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
            contract?.End.ToString(),
            terms,
            attributes,
            potential,
            Seasons(world, person.Id.Value));
    }

    private static IReadOnlyList<DriverSeasonView> Seasons(WorldState world, string personId)
    {
        var archive = world.Section<RaceResultsSection>(RaceResultsSection.SectionName);
        if (archive is null)
        {
            return [];
        }

        var seasons = new SortedDictionary<int, Tally>();
        foreach (var race in archive.Races)
        {
            foreach (var row in race.Rows)
            {
                if (!string.Equals(row.DriverId, personId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!seasons.TryGetValue(race.Season, out var tally))
                {
                    tally = new Tally();
                    seasons.Add(race.Season, tally);
                }

                tally.Add(row);
            }
        }

        var views = new List<DriverSeasonView>(seasons.Count);
        foreach (var (season, tally) in seasons)
        {
            var team = world.Organizations.FirstOrDefault(item => string.Equals(item.Id.Value, tally.TeamId, StringComparison.Ordinal));
            views.Add(new DriverSeasonView(
                season,
                tally.TeamId,
                team?.Names[^1].Name ?? tally.TeamId,
                tally.Starts,
                tally.Wins,
                tally.Podiums,
                tally.Retirements,
                tally.Best));
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

        public void Add(RaceResultRow row)
        {
            TeamId = row.TeamId;
            Starts++;
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
